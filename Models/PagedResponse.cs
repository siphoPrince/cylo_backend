namespace Cylo_Backend.Models
{
    public class PagedResponse<T>
    {
        // The actual list of data (Posts, in this case)
        public IEnumerable<T> Data { get; set; }

        // Information for the frontend to handle the next fetch
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public bool HasNextPage { get; set; }

        // Optional: Helpful for the frontend to show "Total items found"
        public int TotalCount { get; set; }
    }
}
