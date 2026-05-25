namespace Fortress.Core.Models
{
    public sealed class HibpEmailResult
    {
     public string Email { get; init; } = string.Empty;
      public bool IsBreached { get; init; }
        public int BreachCount { get; init; }
  public IReadOnlyList<string> BreachNames { get; init; } = Array.Empty<string>();
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    }

    public sealed class HibpPasswordResult
    {
        public int PwnCount { get; init; }
        public bool IsPwned => PwnCount > 0;
  }
}
