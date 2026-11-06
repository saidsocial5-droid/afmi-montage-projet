using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services.Collab
{
    /// <summary>
    /// Service d'assistance IA pour la prise de notes automatique à partir des discussions vocales
    /// (enregistrements au microphone, fichiers audio importés et dictée locale).
    /// </summary>
    public class VoiceNoteAssistantService : IDisposable
    {
        // P/Invoke winmm.dll (MCI) natif Windows pour capturer l'audio microphone sans dépendance externe
        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder? lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };
        private readonly AiRouterService _aiRouterService = new();
        private SpeechRecognitionEngine? _speechEngine;
        private readonly StringBuilder _liveRecognizedText = new();
        private string? _currentRecordingFilePath;
        private bool _isRecording;

        public bool IsRecording => _isRecording;
        public string LiveRecognizedText => _liveRecognizedText.ToString();

        public event EventHandler<string>? LiveSpeechChunkReceived;

        public bool StartRecording(out string error)
        {
            error = string.Empty;
            try
            {
                _currentRecordingFilePath = Path.Combine(Path.GetTempPath(), $"AFMI_Voice_{Guid.NewGuid():N}.wav");
                _liveRecognizedText.Clear();

                // 1. Fermer toute session d'enregistrement précédente
                mciSendString("close recsound", null, 0, IntPtr.Zero);

                // 2. Ouvrir le périphérique d'enregistrement audio
                int openRes = mciSendString("open new type waveaudio alias recsound", null, 0, IntPtr.Zero);
                if (openRes != 0)
                {
                    error = $"Impossible d'ouvrir le périphérique audio de capture (Code MCI {openRes}).";
                    return false;
                }

                // 3. Configuration audio standard : 16 kHz, 16 bits, Mono (Idéal pour l'IA vocale et transcription)
                mciSendString("set recsound time format ms bitspersample 16 channels 1 samplespersec 16000", null, 0, IntPtr.Zero);

                // 4. Démarrer l'enregistrement
                int recRes = mciSendString("record recsound", null, 0, IntPtr.Zero);
                if (recRes != 0)
                {
                    error = "Échec du démarrage de l'enregistrement sur le microphone.";
                    mciSendString("close recsound", null, 0, IntPtr.Zero);
                    return false;
                }

                _isRecording = true;

                // 5. Initialiser la reconnaissance vocale Windows locale en tâche de fond si disponible
                StartLocalSpeechRecognition();

                return true;
            }
            catch (Exception ex)
            {
                error = $"Erreur lors de l'initialisation du microphone : {ex.Message}";
                _isRecording = false;
                return false;
            }
        }

        public async Task<(bool Success, string Notes, string ErrorMessage)> StopRecordingAndProcessAsync()
        {
            if (!_isRecording || string.IsNullOrEmpty(_currentRecordingFilePath))
            {
                return (false, string.Empty, "Aucun enregistrement vocal actif à arrêter.");
            }

            try
            {
                // 1. Sauvegarder et fermer l'enregistrement audio MCI
                mciSendString($"save recsound \"{_currentRecordingFilePath}\"", null, 0, IntPtr.Zero);
                mciSendString("close recsound", null, 0, IntPtr.Zero);
                _isRecording = false;

                // 2. Arrêter le moteur de dictée locale
                StopLocalSpeechRecognition();

                // 3. Traiter le fichier audio enregistré
                return await ProcessAudioFileAsync(_currentRecordingFilePath, _liveRecognizedText.ToString());
            }
            catch (Exception ex)
            {
                _isRecording = false;
                return (false, string.Empty, $"Erreur lors de l'arrêt de l'enregistrement : {ex.Message}");
            }
            finally
            {
                // Nettoyage sécurisé du fichier temporaire si existant
                try
                {
                    if (!string.IsNullOrEmpty(_currentRecordingFilePath) && File.Exists(_currentRecordingFilePath))
                    {
                        File.Delete(_currentRecordingFilePath);
                    }
                }
                catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(VoiceNoteAssistantService), $"Erreur ignorée : {swallowedEx.Message}"); }
            }
        }

        public async Task<(bool Success, string Notes, string ErrorMessage)> ProcessAudioFileAsync(string audioFilePath, string? fallbackTranscript = null)
        {
            if (!File.Exists(audioFilePath))
            {
                return (false, string.Empty, "Le fichier audio est introuvable.");
            }

            var fileInfo = new FileInfo(audioFilePath);
            if (fileInfo.Length < 100)
            {
                return (false, string.Empty, "L'enregistrement audio est vide ou trop court.");
            }

            // Récupérer la configuration de l'IA
            using var db = new AppDbContext();
            var config = db.AppConfigs.FirstOrDefault();
            string apiKey = config != null ? EncryptionHelper.Unprotect(config.EncryptedApiKey) : string.Empty;
            string provider = config?.Provider ?? "Gemini";
            string model = config?.SelectedModel ?? "gemini-2.0-flash";

            // L'enregistrement d'une réunion ne quitte le poste qu'avec le consentement cloud explicite,
            // sous le plafond mensuel, comme tout appel routé (ADR 27) ; sinon : dictée Windows locale ci-dessous
            bool cloudAllowed = AiRouterService.IsCloudUseAllowed(config, out string cloudRefusal);
            if (!cloudAllowed && !string.IsNullOrWhiteSpace(apiKey))
                AppLogger.Info(nameof(VoiceNoteAssistantService), $"Audio non envoyé au cloud : {cloudRefusal}");

            // Si Gemini est configuré, envoyer directement l'audio multimodal
            if (cloudAllowed && !string.IsNullOrWhiteSpace(apiKey) && provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var result = await ProcessWithGeminiAudioAsync(audioFilePath, apiKey, model);
                    AiRouterService.LogExternalCall("Cloud (Gemini audio)", $"Notes vocales : {Path.GetFileName(audioFilePath)}", watch.ElapsedMilliseconds, result.Success, result.ErrorMessage);
                    return result;
                }
                catch (Exception ex)
                {
                    AiRouterService.LogExternalCall("Cloud (Gemini audio)", $"Notes vocales : {Path.GetFileName(audioFilePath)}", watch.ElapsedMilliseconds, false, ex.Message);
                    // Si Gemini audio échoue et qu'on a une transcription locale de secours
                    if (!string.IsNullOrWhiteSpace(fallbackTranscript))
                    {
                        return await StructureTranscriptWithAiAsync(fallbackTranscript);
                    }
                    return (false, string.Empty, $"Erreur IA Gemini audio : {ex.Message}");
                }
            }

            // Si OpenAI est configuré, transcrire via Whisper puis structurer
            if (cloudAllowed && !string.IsNullOrWhiteSpace(apiKey) && provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    string transcript = await TranscribeWithOpenAiWhisperAsync(audioFilePath, apiKey);
                    AiRouterService.LogExternalCall("Cloud (OpenAI Whisper)", $"Transcription : {Path.GetFileName(audioFilePath)}", watch.ElapsedMilliseconds, true);
                    return await StructureTranscriptWithAiAsync(transcript);
                }
                catch (Exception ex)
                {
                    AiRouterService.LogExternalCall("Cloud (OpenAI Whisper)", $"Transcription : {Path.GetFileName(audioFilePath)}", watch.ElapsedMilliseconds, false, ex.Message);
                    if (!string.IsNullOrWhiteSpace(fallbackTranscript))
                    {
                        return await StructureTranscriptWithAiAsync(fallbackTranscript);
                    }
                    return (false, string.Empty, $"Erreur OpenAI Whisper : {ex.Message}");
                }
            }

            // Si on dispose d'une transcription capturée en direct par le moteur Windows
            if (!string.IsNullOrWhiteSpace(fallbackTranscript))
            {
                return await StructureTranscriptWithAiAsync(fallbackTranscript);
            }

            // Cas hors-ligne sans clé API configurée
            return (true,
                $"### 🎙️ Enregistrement de discussion vocale capturé\n\n" +
                $"* **Taille de l'audio :** {fileInfo.Length / 1024:N0} Ko\n" +
                $"* **Date :** {DateTime.Now:dd/MM/yyyy à HH:mm}\n\n" +
                $"> [!NOTE]\n" +
                $"> Pour activer la transcription automatique par l'IA, renseignez une clé d'API (Gemini ou OpenAI) **et** autorisez l'envoi vers le cloud dans les **Paramètres IA** du logiciel." +
                (cloudAllowed || string.IsNullOrWhiteSpace(apiKey) ? "" : $"\n>\n> Audio non envoyé : {cloudRefusal}"),
                string.Empty);
        }

        private async Task<(bool Success, string Notes, string ErrorMessage)> ProcessWithGeminiAudioAsync(string audioFilePath, string apiKey, string model)
        {
            if (model == "gemini-1.5-flash" || model == "gemini-2.5-flash" || string.IsNullOrWhiteSpace(model))
            {
                model = "gemini-2.0-flash";
            }
            if (model == "gemini-1.5-pro" || model == "gemini-2.5-pro")
            {
                model = "gemini-2.0-flash";
            }

            // Clé dans l'en-tête x-goog-api-key, jamais dans l'URL (qui finit dans les journaux des proxys)
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath);
            string base64Audio = Convert.ToBase64String(audioBytes);
            string mimeType = GetMimeType(audioFilePath);

            string prompt = @"Tu es un secrétaire de séance et assistant IA expert en management de projets de développement durable (AFMI).
Écoute attentivement cette discussion vocale de réunion et réalise une prise de notes automatique claire, structurée et professionnelle en français.

Directives de rédaction :
- Identifie les grands sujets abordés et le contexte des échanges.
- Retiens fidèlement les arguments clés, constats et faits saillants.
- Consigne explicitement les décisions prises et les consensus validés.
- Établis la liste claire des actions à mener (qui fait quoi, échéances si mentionnées).
- Si la discussion mêle le français et l'arabe/darija, traduis et synthétise fidèlement en français.

Adopte cette structure :
# 📝 Prise de notes de la réunion

### 📌 1. Thématiques & Sujets abordés
(Synthèse des sujets discutés)

### 💬 2. Points clés & Interventions
(Constats et arguments majeurs des participants)

### ✅ 3. Décisions actées & Consensus
(Accords, validations ou arbitrages)

### 🎯 4. Plan d'actions immédiates
(Actions concrètes, porteurs désignés et délais)";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Audio
                                }
                            },
                            new
                            {
                                text = prompt
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = 8192,
                    temperature = 0.2
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("x-goog-api-key", apiKey);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                return (false, string.Empty, $"Erreur Gemini ({(int)response.StatusCode}) : {errorBody}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return (true, text ?? string.Empty, string.Empty);
        }

        private async Task<string> TranscribeWithOpenAiWhisperAsync(string audioFilePath, string apiKey)
        {
            using var form = new MultipartFormDataContent();
            byte[] fileBytes = await File.ReadAllBytesAsync(audioFilePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(audioFilePath));
            form.Add(fileContent, "file", Path.GetFileName(audioFilePath));
            form.Add(new StringContent("whisper-1"), "model");
            form.Add(new StringContent("Réunion de travail, projet de développement durable, AFMI, français, arabe"), "prompt");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = form;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erreur OpenAI Whisper ({(int)response.StatusCode}) : {err}");
            }

            string res = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(res);
            return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
        }

        public async Task<(bool Success, string Notes, string ErrorMessage)> StructureTranscriptWithAiAsync(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return (false, string.Empty, "La transcription brute est vide.");
            }

            string systemPrompt = @"Tu es un secrétaire de séance et assistant IA expert en gestion de projets associatifs et environnementaux.
Transforme ces échanges oraux ou ces notes brutes en une prise de notes professionnelle, synthétique et bien structurée en français.";

            string userPrompt = $@"Voici les propos et discussions brutes de la réunion :
---
{transcript}
---

Rédige une prise de notes claire et immédiatement exploitable selon ce schéma :
# 📝 Prise de notes de la réunion

### 📌 1. Thématiques & Sujets abordés
### 💬 2. Points clés & Interventions
### ✅ 3. Décisions actées & Consensus
### 🎯 4. Plan d'actions immédiates (Responsable, Tâche, Échéance)";

            var result = await _aiRouterService.GenerateResponseAsync(userPrompt, systemPrompt, "VoiceNoteStructuring");
            if (result.IsSuccess)
            {
                return (true, result.ResponseText, string.Empty);
            }
            else
            {
                return (true, $"### 🎙️ Notes brutes de la réunion :\n\n{transcript}", string.Empty);
            }
        }

        private void StartLocalSpeechRecognition()
        {
            try
            {
                var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
                if (recognizers == null || recognizers.Count == 0) return;

                var targetRecognizer = recognizers.FirstOrDefault(r => r.Culture.Name.Equals("fr-FR", StringComparison.OrdinalIgnoreCase))
                                       ?? recognizers.FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase))
                                       ?? recognizers.FirstOrDefault(r => r.Culture.Name.Equals(CultureInfo.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
                                       ?? recognizers.First();

                _speechEngine = new SpeechRecognitionEngine(targetRecognizer.Id);
                _speechEngine.SetInputToDefaultAudioDevice();
                _speechEngine.LoadGrammar(new DictationGrammar());

                _speechEngine.SpeechRecognized += (s, e) =>
                {
                    if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.Text) && e.Result.Confidence >= 0.20f)
                    {
                        lock (_liveRecognizedText)
                        {
                            _liveRecognizedText.AppendLine(e.Result.Text);
                        }
                        LiveSpeechChunkReceived?.Invoke(this, e.Result.Text);
                    }
                };

                _speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch
            {
                _speechEngine = null;
            }
        }

        private void StopLocalSpeechRecognition()
        {
            try
            {
                if (_speechEngine != null)
                {
                    _speechEngine.RecognizeAsyncStop();
                    _speechEngine.Dispose();
                    _speechEngine = null;
                }
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(VoiceNoteAssistantService), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        private static string GetMimeType(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".mp3" => "audio/mp3",
                ".wav" => "audio/wav",
                ".m4a" => "audio/m4a",
                ".ogg" => "audio/ogg",
                ".aac" => "audio/aac",
                ".flac" => "audio/flac",
                ".webm" => "audio/webm",
                _ => "audio/wav"
            };
        }

        public void Dispose()
        {
            StopLocalSpeechRecognition();
            if (_isRecording)
            {
                try
                {
                    mciSendString("close recsound", null, 0, IntPtr.Zero);
                }
                catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(VoiceNoteAssistantService), $"Erreur ignorée : {swallowedEx.Message}"); }
                _isRecording = false;
            }
            _httpClient.Dispose();
        }
    }
}
