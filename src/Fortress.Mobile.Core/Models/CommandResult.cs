
namespace Fortress.Mobile.Core.Models
{
    public class CommandResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }
        public int StatusCode { get; set; }
        public bool IsServiceAvailable { get=>  !(StatusCode >= 500 && StatusCode < 600); }
        public CommandResult() { }
    }
    public class CommandResult<T> : CommandResult
    {
        public CommandResult() { }

        public CommandResult(T data) { Data = data; }
        public T Data { get; set; }
    }
}
