using Microsoft.Extensions.Hosting;
using System.Text;
using System.Threading.Tasks;

namespace Fortress.Service;

/// <summary>
/// Entry point for the Fortress Vault Service.
///
/// Supports two run modes:
///   Console  — dotnet run  (or any interactive / debugger session)
///   Service  — installed via sc.exe and started by the Windows SCM
///
/// All service registration lives in <see cref="Startup"/>.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        // Force UTF-8 output so box-drawing and other Unicode characters
        // render correctly in Windows Terminal / PowerShell / cmd.exe.
        Console.OutputEncoding = Encoding.UTF8;

       await CreateHostBuilder(args).Build().RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
      Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(Startup.ConfigureAppConfiguration)
       .ConfigureLogging((ctx, logging)  => Startup.ConfigureLogging(ctx, logging, args))
            .ConfigureServices((ctx, services) => Startup.ConfigureServices(ctx, services, args))
  .UseWindowsService(options =>
    options.ServiceName = Startup.ServiceName);
}
