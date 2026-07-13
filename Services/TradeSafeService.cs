using Cylo_Backend.Models.DTOs;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cylo_Backend.Services
{
    public class TradeSafeService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private string _cachedToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public TradeSafeService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            var clientId = _config["TradeSafe:ClientId"];
            var clientSecret = _config["TradeSafe:ClientSecret"];
            var authUrl = _config["TradeSafe:AuthUrl"];

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, authUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"TradeSafe Auth Failed: {content}");

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            _cachedToken = root.GetProperty("access_token").GetString();
            int expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 600);

            return _cachedToken;
        }

        // --- CORE TRANSACTION LIFE CYCLE METHODS ---

        // FIXED SIGNATURE: Changed from Task<object> to Task<TradeSafeTransactionResponse>
        public async Task<TradeSafeTransactionResponse> CreateTransactionAsync(string buyerToken, string sellerToken, decimal amount, string itemDescription)
        {
            var createQuery = @"
                mutation transactionCreate($input: CreateTransactionInput!) {
                    transactionCreate(input: $input) {
                        id
                        allocations {
                            id
                        }
                    }
                }";

            var variables = new
            {
                input = new
                {
                    title = itemDescription,
                    description = $"Cylo Purchase: {itemDescription}",
                    industry = "GENERAL_GOODS_SERVICES",
                    currency = "ZAR",
                    feeAllocation = "SELLER",
                    workflow = "STANDARD",
                    reference = $"CYLO-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    allocations = new
                    {
                        create = new[] {
                            new {
                                title = itemDescription,
                                description = "Item Purchase",
                                value = (double)amount,
                                daysToDeliver = 7,
                                daysToInspect = 3
                            }
                        }
                    },
                    parties = new
                    {
                        create = new[] {
                            new {
                                token = buyerToken,
                                role = "BUYER",
                                fee = (double?)null,
                                feeType = (string)null,
                                feeAllocation = (string)null
                            },
                            new {
                                token = sellerToken,
                                role = "SELLER",
                                fee = (double?)null,
                                feeType = (string)null,
                                feeAllocation = (string)null
                            },
                            new {
                                token = _config["TradeSafe:AgentToken"],
                                role = "AGENT",
                                fee = (double?)10.0,
                                feeType = "PERCENT",
                                feeAllocation = "SELLER"
                            }
                        }
                    }
                }
            };

            var createResult = await SendGraphQLRequestAsync(createQuery, variables);
            var transactionId = createResult.GetProperty("transactionCreate").GetProperty("id").GetString();

            var allocationId = createResult.GetProperty("transactionCreate")
                                           .GetProperty("allocations")[0]
                                           .GetProperty("id").GetString();

            var linkQuery = @"
                mutation checkoutLink($transactionId: ID!) {
                    checkoutLink(transactionId: $transactionId)
                }";

            var linkVariables = new { transactionId = transactionId };
            var linkResult = await SendGraphQLRequestAsync(linkQuery, linkVariables);
            var secureUrl = linkResult.GetProperty("checkoutLink").GetString();

            // Native explicit mapping aligns cleanly with method signature rules
            return new TradeSafeTransactionResponse
            {
                TransactionId = transactionId ?? string.Empty,
                AllocationId = allocationId ?? string.Empty,
                CheckoutUrl = secureUrl ?? string.Empty
            };
        }

        public async Task<JsonElement> StartDeliveryAsync(string allocationId)
        {
            var query = @"
                mutation allocationStartDelivery($id: ID!) {
                  allocationStartDelivery(id: $id) {
                    id
                    state
                  }
                }";

            var variables = new { id = allocationId };
            return await SendGraphQLRequestAsync(query, variables);
        }

        public async Task<JsonElement> SetAllocationInTransitAsync(string allocationId)
        {
            var query = @"
                mutation allocationInTransit($id: ID!) {
                  allocationInTransit(id: $id) {
                    id
                    state
                  }
                }";

            var variables = new { id = allocationId };
            return await SendGraphQLRequestAsync(query, variables);
        }

        public async Task<JsonElement> CompleteAllocationDeliveryAsync(string allocationId)
        {
            var query = @"
                mutation allocationCompleteDelivery($id: ID!) {
                  allocationCompleteDelivery(id: $id) {
                    id
                    state
                  }
                }";

            var variables = new { id = allocationId };
            return await SendGraphQLRequestAsync(query, variables);
        }

        public async Task<JsonElement> ReleaseFundsAsync(string allocationId)
        {
            var query = @"
                mutation allocationAcceptDelivery($id: ID!) {
                  allocationAcceptDelivery(id: $id) {
                    id
                    state
                  }
                }";

            var variables = new { id = allocationId };
            return await SendGraphQLRequestAsync(query, variables);
        }

        public async Task<JsonElement> DisputeAllocationAsync(string allocationId)
        {
            var query = @"
                mutation allocationDisputeDelivery($id: ID!) {
                  allocationDisputeDelivery(id: $id) {
                    id
                    state
                  }
                }";

            var variables = new { id = allocationId };
            return await SendGraphQLRequestAsync(query, variables);
        }

        public async Task<JsonElement> CancelTransactionAsync(string tradeSafeId, string reason)
        {
            var query = @"
                mutation transactionCancel($id: ID!, $comment: String) {
                  transactionCancel(id: $id, comment: $comment) {
                    state
                  }
                }";
            var variables = new { id = tradeSafeId, comment = reason };
            return await SendGraphQLRequestAsync(query, variables);
        }

        public async Task<JsonElement> DeleteTransactionAsync(string tradeSafeId)
        {
            var query = @"
                mutation transactionDelete($id: ID!) {
                    transactionDelete(id: $id)
                }";

            var variables = new { id = tradeSafeId };
            return await SendGraphQLRequestAsync(query, variables);
        }

        // --- ACCOUNT MANAGEMENT ---

        public async Task<string> CreateUserTokenAsync(Cylo_Backend.Models.User user)
        {
            var tokenQuery = @"
                mutation tokenCreate($input: TokenInput!) {
                    tokenCreate(input: $input) {
                        id
                    }
                }";

            var variables = new
            {
                input = new
                {
                    user = new
                    {
                        givenName = user.Name,
                        familyName = user.Name,
                        email = user.Email,
                        mobile = user.Mobile
                    }
                }
            };

            var result = await SendGraphQLRequestAsync(tokenQuery, variables);
            return result.GetProperty("tokenCreate").GetProperty("id").GetString();
        }

        // --- REFACTOR SAFE TRANSMISSION ENGINE ---

        public async Task<JsonElement> SendGraphQLRequestAsync(string query, object variables)
        {
            var token = await GetAccessTokenAsync();
            var requestBody = new { query, variables };

            var request = new HttpRequestMessage(HttpMethod.Post, _config["TradeSafe:ApiUrl"]);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                var errorMsg = errors[0].TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown GraphQL Error Frame.";
                System.Diagnostics.Debug.WriteLine($"TRADESAFE RAW LOG: {content}");
                throw new Exception($"TradeSafe API: {errorMsg}");
            }

            if (!root.TryGetProperty("data", out var data))
                throw new Exception("TradeSafe API response structure invalid: Data segment missing.");

            return data.Clone();
        }
    }
}