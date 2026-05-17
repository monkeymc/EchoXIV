using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EchoXIV.Services
{
    public class GeminiTranslatorService : ITranslationService
    {
        public string Name => "Gemini";

        private static readonly HttpClient SharedHttpClient = CreateHttpClient();
        private readonly Configuration _configuration;
        private string? _resolvedModelName = null;
        private readonly object _modelLock = new object();

        public GeminiTranslatorService(Configuration configuration)
        {
            _configuration = configuration;
        }

        private static HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "EchoXIV-Dalamud-Plugin");
            return httpClient;
        }

        private string GetLanguageName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "the target language";

            return code.ToLowerInvariant() switch
            {
                "en" => "English",
                "ja" => "Japanese",
                "de" => "German",
                "fr" => "French",
                "es" => "Spanish",
                "it" => "Italian",
                "ko" => "Korean",
                "no" => "Norwegian",
                "pt" => "Portuguese",
                "ru" => "Russian",
                "zh-cn" => "Simplified Chinese",
                "zh-tw" => "Traditional Chinese",
                "th" => "Thai",
                _ => code
            };
        }

        private async Task<string> ResolveModelNameAsync(string apiKey, CancellationToken cancellationToken)
        {
            if (_resolvedModelName != null)
                return _resolvedModelName;

            try
            {
                Plugin.PluginLog.Info("Attempting to auto-discover active Gemini models...");
                var listUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await SharedHttpClient.GetAsync(listUrl, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var jObject = JObject.Parse(json);
                    var modelsArray = jObject["models"] as JArray;
                    
                    if (modelsArray != null)
                    {
                        var discoveredModels = new List<string>();
                        string? selected = null;
                        
                        foreach (var modelToken in modelsArray)
                        {
                            var name = modelToken["name"]?.ToString();
                            var methods = modelToken["supportedMethodNames"]?.ToObject<List<string>>();
                            
                            if (!string.IsNullOrEmpty(name))
                            {
                                discoveredModels.Add(name);
                                
                                // Clean up prefix "models/" if present
                                var modelId = name.StartsWith("models/") ? name.Substring(7) : name;
                                
                                if (methods != null && methods.Contains("generateContent"))
                                {
                                    if (modelId.Contains("flash", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Rank and prefer stable gemini-2.5-flash as the first choice,
                                        // followed by gemini-3-flash, gemini-2.0-flash, and gemini-1.5-flash.
                                        if (selected == null || 
                                            modelId.Contains("gemini-2.5", StringComparison.OrdinalIgnoreCase) || 
                                            (modelId.Contains("gemini-3", StringComparison.OrdinalIgnoreCase) && !selected.Contains("gemini-2.5")) ||
                                            (modelId.Contains("gemini-2.0", StringComparison.OrdinalIgnoreCase) && !selected.Contains("gemini-2.5") && !selected.Contains("gemini-3")) ||
                                            (modelId.Contains("gemini-1.5", StringComparison.OrdinalIgnoreCase) && !selected.Contains("gemini-2.5") && !selected.Contains("gemini-3") && !selected.Contains("gemini-2.0")))
                                        {
                                            selected = modelId;
                                        }
                                    }
                                }
                            }
                        }
                        
                        Plugin.PluginLog.Info($"Discovered Gemini models: {string.Join(", ", discoveredModels)}");
                        
                        if (selected != null)
                        {
                            Plugin.PluginLog.Info($"Automatically selected Gemini model: {selected}");
                            lock (_modelLock)
                            {
                                _resolvedModelName = selected;
                            }
                            return selected;
                        }
                    }
                }
                else
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    Plugin.PluginLog.Warning($"Failed to list Gemini models. Status: {response.StatusCode}, Details: {err}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error(ex, "Failed to auto-discover Gemini models.");
            }

            // Fallbacks if discovery is not successful
            Plugin.PluginLog.Warning("Using fallback Gemini model name: gemini-2.5-flash");
            return "gemini-2.5-flash"; 
        }

        public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var apiKey = _configuration.GeminiApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Plugin.PluginLog.Warning("Gemini API Key is empty. Returning original text.");
                return text;
            }

            var targetLangName = GetLanguageName(targetLang);

            try
            {
                // Resolve the model name dynamically to prevent 404s
                var modelName = await ResolveModelNameAsync(apiKey, cancellationToken);
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                // TOP-LEVEL systemInstruction in official Gemini API JSON schema.
                // This strictly isolates instructions and prevents the LLM from conversing or answering questions.
                var systemInstructionText = $"You are a professional MMORPG translator for Final Fantasy XIV chat logs. " +
                                            $"Translate the given chat message into {targetLangName}. " +
                                            $"Understand and accurately translate game terminology (e.g. jobs like PLD/MCH/DNC, items, duties, races like Miqo'te/Hrothgar), emoticons, and online gaming slang (LFG, PF, static). " +
                                            $"Specifically, correctly interpret phonetic spelling emphasis, prolonged vowels, and slang variations used for cute or dramatic gaming effects (such as 'beeeeg' meaning 'big/giant', 'smol' meaning 'small', 'plssss' meaning 'please', 'thicc' meaning 'large'). " +
                                            $"Do not answer questions, chat, or converse. Do not add introductory text, explanations, notes, or quotes. " +
                                            $"Translate ONLY the chat message, maintaining its casual gaming tone.";

                var requestBody = new
                {
                    systemInstruction = new
                    {
                        parts = new[]
                        {
                            new { text = systemInstructionText }
                        }
                    },
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = text }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.0 // Minimum temperature for highly deterministic, zero-fluff translations
                    }
                };

                var jsonPayload = JsonConvert.SerializeObject(requestBody);
                using var requestContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await SharedHttpClient.PostAsync(url, requestContent, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // Clear the cached resolved model on 404 so we re-discover next time
                    lock (_modelLock)
                    {
                        _resolvedModelName = null;
                    }
                    var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                    Plugin.PluginLog.Error($"Gemini model not found (404). Model: {modelName}. Details: {errorDetails}");
                    return text;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new TranslationRateLimitException(Name, "Gemini API rate limit exceeded");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                    Plugin.PluginLog.Error($"Gemini API returned failed status code: {response.StatusCode}. Details: {errorDetails}");
                    return text;
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var jObject = JObject.Parse(responseJson);

                var translatedText = jObject["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrEmpty(translatedText))
                {
                    return text;
                }

                // Strip any leading/trailing quotes that Gemini might sometimes output in excess of instructions
                var result = translatedText.Trim();
                if (result.StartsWith("\"") && result.EndsWith("\"") && result.Length >= 2)
                {
                    result = result.Substring(1, result.Length - 2).Trim();
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TranslationRateLimitException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error(ex, $"Error during Gemini translation. Text: '{text}'");
                return text;
            }
        }

        public void Dispose()
        {
            // SharedHttpClient is static and process-wide. No need to dispose here.
        }
    }
}
