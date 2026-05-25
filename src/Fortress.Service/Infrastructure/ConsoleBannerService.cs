using Fortress.Service.Pipes;
using Microsoft.Extensions.Hosting;

namespace Fortress.Service.Infrastructure;

/// <summary>
/// Writes the startup banner directly to stdout after the host has started.
/// Bypasses ILogger so the formatter pipeline cannot mangle Unicode characters.
/// Only registered when running in console / development mode.
/// </summary>
internal sealed class ConsoleBannerService : IHostedService
{
    private readonly string _dbDir;
    private readonly string _prefsDir;

    public ConsoleBannerService(string dbDir, string prefsDir)
    {
      _dbDir    = dbDir;
 _prefsDir = prefsDir;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Truncate long paths so the box stays aligned (max 32 chars)
        var db    = Fit(_dbDir, 32);
        var prefs = Fit(_prefsDir, 32);
        var pipe  = Fit($@"\\.\pipe\{PipeServer.PipeName}", 32);

        var c = Console.Out;
        c.WriteLine();
        c.WriteLine("  ┌──────────────────────────────────────────────┐");
        c.WriteLine("  │      Fortress Vault Service  [CONSOLE]       │");
        c.WriteLine("  ├──────────────────────────────────────────────┤");
        c.WriteLine($"  │  DB path  : {db,-32} │");
        c.WriteLine($"  │  Prefs    : {prefs,-32} │");
        c.WriteLine($"  │  Pipe     : {pipe,-32} │");
        c.WriteLine("  └──────────────────────────────────────────────┘");
        c.WriteLine("  Press Ctrl+C to stop.");
        c.WriteLine();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("  ┌──────────────────────────────────────────────┐");
        Console.WriteLine("  │      Fortress Vault Service stopped.         │");
        Console.WriteLine("  └──────────────────────────────────────────────┘");
        Console.WriteLine();
        return Task.CompletedTask;
    }

    // Truncate with "…" if the path is too wide for the box column
    private static string Fit(string s, int maxLen) =>
        s.Length <= maxLen ? s : "…" + s[^(maxLen - 1)..];
}
