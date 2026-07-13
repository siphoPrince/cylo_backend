namespace Cylo_Backend.Models.DTOs
{
    public class ExploreSearchFilterDto
    {
        public string? Search { get; set; }
        public string? Category { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SelectedTag { get; set; }

        // Location parameters
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public int RadiusInKm { get; set; } = 50; // Defaults to 50km radius

        // Pagination support
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }
}
