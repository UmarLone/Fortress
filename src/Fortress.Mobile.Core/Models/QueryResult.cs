namespace Fortress.Mobile.Core.Models
{
    public class QueryResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int StatusCode { get; set; }
        public bool IsServiceAvailable { get => !(StatusCode >= 500 && StatusCode < 600); }

        public QueryResult() { }
    }
    public class QueryResult<T> : QueryResult
    {
        public QueryResult() { }
        public QueryResult(T data) { Data = data; }
        public T Data { get; set; }

    }
}
