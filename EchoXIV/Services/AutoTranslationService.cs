using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EchoXIV.Services
{
    /// <summary>
    /// Servicio de traducción inteligente que selecciona dinámicamente el motor
    /// más óptimo en base al idioma de destino (Target/Output Language).
    /// </summary>
    public class AutoTranslationService : ITranslationService
    {
        private readonly ITranslationService _googleTranslator;
        private readonly ITranslationService _papagoTranslator;
        private readonly ITranslationService _geminiTranslator;
        private readonly Configuration _configuration;

        public AutoTranslationService(
            ITranslationService googleTranslator, 
            ITranslationService papagoTranslator,
            ITranslationService geminiTranslator,
            Configuration configuration)
        {
            _googleTranslator = googleTranslator;
            _papagoTranslator = papagoTranslator;
            _geminiTranslator = geminiTranslator;
            _configuration = configuration;
        }

        public string Name => "Auto";

        public Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(text);

            // 1. If Gemini API Key is provided, use Gemini for all languages to provide top-tier translations.
            if (!string.IsNullOrWhiteSpace(_configuration.GeminiApiKey))
            {
                return _geminiTranslator.TranslateAsync(text, sourceLang, targetLang, cancellationToken);
            }

            // 2. Otherwise, fall back to the original Auto rules (Papago for East Asian and Thai, Google for European/other languages).
            var cleanLang = targetLang.Split('-')[0].ToLowerInvariant();
            var papagoPreferred = new HashSet<string> { "ja", "ko", "zh", "th" };

            var translator = papagoPreferred.Contains(cleanLang)
                ? _papagoTranslator
                : _googleTranslator;

            return translator.TranslateAsync(text, sourceLang, targetLang, cancellationToken);
        }

        public void Dispose()
        {
            // Los motores subyacentes se liberan en la clase Plugin principal.
        }
    }
}
