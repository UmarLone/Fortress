using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Microsoft.Extensions.Logging;

namespace Fortress.Mobile.Services
{
    /// <summary>
    /// Concrete voice command service that chains:
    ///   ISpeechToTextService  →  VoiceCommandParser→  VoiceCommandResult
    /// </summary>
    public sealed class VoiceCommandService : IVoiceCommandService
    {
        private readonly IServiceProvider _services;
        private readonly VoiceCommandParser _parser;
        private readonly ILogger<VoiceCommandService> _logger;

        public VoiceCommandService(
          IServiceProvider services,
              VoiceCommandParser parser,
           ILogger<VoiceCommandService> logger)
        {
          _services = services;
         _parser = parser;
            _logger = logger;
       }

        // Resolve a fresh ISpeechToTextService each call — it is registered as
        // transient because AndroidSpeechToTextService extends Java.Lang.Object
        // and cannot safely be reused after its Java peer is destroyed.
        private ISpeechToTextService CreateStt() =>
            _services.GetRequiredService<ISpeechToTextService>();

       public Task<bool> RequestPermissionAsync() => CreateStt().RequestPermissionAsync();

        public async Task<VoiceCommandResult> ListenAndParseAsync(
            CancellationToken cancellationToken = default,
            Action? onReady = null,
            Action<string>? onPartialResult = null)
        {
            var stt = CreateStt();

            if (!await stt.RequestPermissionAsync())
            {
                _logger.LogWarning("VoiceCommandService: mic permission denied");
                return VoiceCommandResult.Empty;
            }

            var transcript = await stt.ListenAsync(cancellationToken, onReady, onPartialResult);

            if (string.IsNullOrWhiteSpace(transcript))
        {
                _logger.LogInformation("VoiceCommandService: empty/null transcript");
  return VoiceCommandResult.Empty;
        }

            _logger.LogInformation("VoiceCommandService: transcript = \"{T}\"", transcript);
         return _parser.Parse(transcript);
      }
    }
}
