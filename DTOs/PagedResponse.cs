namespace backend.DTOs;

/// <summary>
/// API response with pagination
/// </summary>
public class PagedResponse<T>
{
    public IEnumerable<T> Data { get; set; } = new List<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public long TotalRecords { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
}
