namespace DynamicFormBuilder.Models.Common.Query
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
    }
}
