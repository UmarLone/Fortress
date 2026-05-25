using Fortress.Core.Contracts;
using Fortress.Core.Intelligence;
using Fortress.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fortress.Core.Extensions
{
    public static class FortressCoreServiceExtensions
    {
        /// <summary>
     /// Registers all Fortress.Core services.
        /// Call from any host: Fortress.Service, Fortress.Wpf, Fortress.NativeHost.
        /// The caller must also register an <see cref="IPreferenceService"/> implementation
        /// appropriate for their platform before calling this.
        /// </summary>
        public static IServiceCollection AddFortressCore(this IServiceCollection services,
        string? databaseDirectory = null)
    {
            // Preferences & cryptography
     services.AddSingleton<FortressPreferenceWrapper>();
    services.AddSingleton<ICryptographyService, CryptographyService>(sp =>
  {
var prefs = sp.GetRequiredService<FortressPreferenceWrapper>();
         var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<CryptographyService>>();
 var svc = new CryptographyService(prefs, logger);
    prefs.SetCryptographyService(svc);
      return svc;
      });

      // Storage
            services.AddSingleton<IDataStorageService>(sp =>
   {
   var prefs  = sp.GetRequiredService<IPreferenceService>();
   var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<DataStorageService>>();
     return new DataStorageService(prefs, logger, databaseDirectory);
   });

     // Vault logic
      services.AddSingleton<PasswordAnomalyDetector>();
   services.AddSingleton<VaultHealthCalculator>(sp =>
    {
    var anomaly = sp.GetService<PasswordAnomalyDetector>();
    var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<VaultHealthCalculator>>();
      return new VaultHealthCalculator(null, anomaly, logger);
   });

     // Intelligence
  services.AddSingleton<IDomainRiskAnalyzer, DomainRiskAnalyzer>();
    services.AddSingleton<AutofillSuggestionEngine>();
  services.AddSingleton<IItemClassifier, ItemClassifier>();

  // Credential resolver
  services.AddSingleton<ICredentialResolver, CredentialResolver>();

      return services;
        }
    }
}
