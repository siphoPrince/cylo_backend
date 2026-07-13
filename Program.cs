using Cylo_Backend.Data;
using Cylo_Backend.Hubs;
using Cylo_Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net.Http.Headers;
using Cylo_Backend.Models.Interface;
using System.Globalization;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Services Configuration ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<TradeSafeService>();

// Database - Automatically picks Dev or Prod connection strings based on the active environment file
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null
        )
    ));

// --- 🚀 CLOUDFLARE R2 CLIENT REGISTRATION ---
var r2Config = builder.Configuration.GetSection("CloudflareR2");
var s3Config = new AmazonS3Config
{
    ServiceURL = $"https://{r2Config["AccountId"]}.r2.cloudflarestorage.com",
    ForcePathStyle = true
};
builder.Services.AddSingleton<IAmazonS3>(new AmazonS3Client(
    r2Config["AccessKey"],
    r2Config["SecretKey"],
    s3Config
));

builder.Services.AddScoped<IStorageService, R2StorageService>();
builder.Services.AddTransient<EmailService>();

// --- 2. SignalR & Real-time Chat ---
builder.Services.AddSignalR();
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024;
});

// --- 3. Authentication (JWT) ---
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key");

// CRITICAL SECURITY FIX: Allow fallback ONLY during local development. 
// If production lacks a key, crash immediately so your production app isn't left insecure.
if (string.IsNullOrEmpty(jwtKey))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtKey = "TemporarySuperSecretDeploymentKey1234567890!";
    }
    else
    {
        throw new InvalidOperationException("Production JWT Key is missing! App initialization stopped for safety.");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "CyloIssuer",
            ValidAudience = jwtSection["Audience"] ?? "CyloAudience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// --- 4. CORS & Security ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                   "http://localhost:5173",
                   "http://localhost:51817",
                  "http://localhost:5078",
                  "http://localhost:5000",
                  "https://localhost:7124",
                  "https://cylosocials.co.za",
                  "https://www.cylosocials.co.za",
                  "https://app.cylosocials.co.za"
              )
              .SetIsOriginAllowed(origin =>
              {
                  var host = new Uri(origin).Host;
                  return host == "localhost"
                         || host == "cylosocials.co.za"
                         || host.EndsWith(".cylosocials.co.za");
              })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddAuthorization();

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// --- 5. Conditional Pipeline Environments ---
if (app.Environment.IsDevelopment())
{
    // Swagger is exposed ONLY when working offline/locally
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    // Production settings: Hides swagger endpoints from public scanning
    app.UseExceptionHandler("/Error");
    app.UseHsts();

    //app.UseHttpsRedirection();
}


app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    // Alternatively, map fallback only if the path does not start with /api
});

// 🚀 CLEAN AUTOMATIC DATABASE SCHEMA GENERATION
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        // --- 1. NOTIFICATIONS TABLE ---
        var fixNotificationsSql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Notifications')
            BEGIN
                CREATE TABLE Notifications (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    Type NVARCHAR(50) NOT NULL,
                    IsRead BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
                );
            END";
        dbContext.Database.ExecuteSqlRaw(fixNotificationsSql);

        // --- 2. LIKES TABLE ---
        var fixLikesSql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Likes')
            BEGIN
                CREATE TABLE Likes (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    PostId INT NOT NULL,
                    UserId INT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT UC_Like_User_Post UNIQUE (PostId, UserId)
                );
            END";
        dbContext.Database.ExecuteSqlRaw(fixLikesSql);

        // --- 3. COMMENTS TABLE ---
        var fixCommentsSql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Comments')
            BEGIN
                CREATE TABLE Comments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Content NVARCHAR(MAX) NOT NULL,
                    PostId INT NOT NULL,
                    UserId INT NOT NULL,
                    ParentCommentId INT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                    CommentsId INT NULL
                );
            END";
        dbContext.Database.ExecuteSqlRaw(fixCommentsSql);

        // --- 4. COMMENT LIKES TABLE ---
        var fixCommentLikesSql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CommentLikes')
            BEGIN
                CREATE TABLE CommentLikes (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    CommentId INT NOT NULL,
                    UserId INT NOT NULL,
                    CONSTRAINT UC_CommentLike_User_Comment UNIQUE (CommentId, UserId),
                    CommentsId INT NULL
                );
            END";
        dbContext.Database.ExecuteSqlRaw(fixCommentLikesSql);

        // Only run full migrations automatically if we're working locally, 
        // or ensure your live DB user has strict DDL privileges if executing in prod.
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while building the clean database.");
    }
}

app.Run();