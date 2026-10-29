using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models.Radar;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>
    /// Service d'analyse documentaire et web/réseaux sociaux pour l'enrichissement automatique et continu du profil de l'Association.
    /// Traite les fichiers (PDF, Word, Excel, TXT) et les pages/publications web (sites, articles, réseaux sociaux),
    /// puis utilise l'IA pour extraire des propositions structurées de mise à jour du profil institutionnel.
    /// </summary>
    public class ProfileSourceAnalysisService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(25)
        };

        static ProfileSourceAnalysisService()
        {
            if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                HttpClient.DefaultRequestHeaders.Add("Accept-Language", "fr-FR,fr;q=0.9,ar;q=0.8,en;q=0.7");
            }
        }

        #region Extraction Web & Réseaux Sociaux

        /// <summary>
        /// Télécharge et extrait le texte significatif ainsi que les métadonnées OpenGraph d'une URL web ou réseau social.
        /// </summary>
        public async Task<(bool Success, string ExtractedText, string ErrorMessage)> FetchUrlContentAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return (false, string.Empty, "L'URL est vide.");

            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var (isValid, reason) = SsrfValidator.ValidateUrl(url);
            if (!isValid)
            {
                return (false, string.Empty, $"URL non autorisée : {reason}");
            }

            try
            {
                var response = await HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return (false, string.Empty, $"Erreur HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");
                }

                string html = await response.Content.ReadAsStringAsync();
                string extracted = ExtractTextAndMetadataFromHtml(html, url);

                if (string.IsNullOrWhiteSpace(extracted) || extracted.Length < 30)
                {
                    return (true, $"[Lien Web : {url}]\nAucun texte textuel lisible n'a pu être extrait automatiquement (possible protection anti-scraping ou chargement dynamique).", string.Empty);
                }

                // Limiter à 35 000 caractères par page pour respecter les fenêtres de contexte IA
                if (extracted.Length > 35000)
                {
                    extracted = extracted.Substring(0, 35000) + "\n... [Texte tronqué pour l'analyse IA]";
                }

                return (true, extracted, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Erreur lors de la récupération du lien : {ex.Message}");
            }
        }

        private static string ExtractTextAndMetadataFromHtml(string html, string sourceUrl)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- SOURCE WEB : {sourceUrl} ---");

            // Extraction du titre
            var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (titleMatch.Success)
            {
                string title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
                sb.AppendLine($"Titre : {title}");
            }

            // Extraction de meta description
            var metaDescMatch = Regex.Match(html, @"<meta\s+name=[""']description[""']\s+content=[""'](.*?)[""']", RegexOptions.IgnoreCase);
            if (!metaDescMatch.Success)
            {
                metaDescMatch = Regex.Match(html, @"<meta\s+content=[""'](.*?)[""']\s+name=[""']description[""']", RegexOptions.IgnoreCase);
            }
            if (metaDescMatch.Success)
            {
                string desc = System.Net.WebUtility.HtmlDecode(metaDescMatch.Groups[1].Value.Trim());
                sb.AppendLine($"Description : {desc}");
            }

            // Extraction OpenGraph (og:title, og:description)
            var ogDescMatch = Regex.Match(html, @"<meta\s+property=[""']og:description[""']\s+content=[""'](.*?)[""']", RegexOptions.IgnoreCase);
            if (ogDescMatch.Success)
            {
                string ogDesc = System.Net.WebUtility.HtmlDecode(ogDescMatch.Groups[1].Value.Trim());
                sb.AppendLine($"Extrait Réseau/OG : {ogDesc}");
            }

            // Nettoyage des balises script, style, svg, nav, footer pour garder le cœur du contenu
            string cleaned = Regex.Replace(html, @"<script[^>]*>.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            cleaned = Regex.Replace(cleaned, @"<style[^>]*>.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            cleaned = Regex.Replace(cleaned, @"<svg[^>]*>.*?</svg>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            cleaned = Regex.Replace(cleaned, @"<!--.*?-->", " ", RegexOptions.Singleline);

            // Remplacer les balises de paragraphe et saut de ligne par de vrais retours à la ligne
            cleaned = Regex.Replace(cleaned, @"</?(?:p|div|br|h1|h2|h3|h4|h5|h6|li|tr)[^>]*>", "\n", RegexOptions.IgnoreCase);

            // Supprimer toutes les autres balises HTML
            cleaned = Regex.Replace(cleaned, @"<[^>]+>", " ");

            // Décoder les entités HTML
            cleaned = System.Net.WebUtility.HtmlDecode(cleaned);

            // Nettoyer les espaces multiples tout en préservant les sauts de lignes utiles
            var lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => Regex.Replace(l, @"\s+", " ").Trim())
                               .Where(l => l.Length > 20); // Ignorer les lignes de navigation courtes

            sb.AppendLine("Contenu principal :");
            sb.AppendLine(string.Join("\n", lines));

            return sb.ToString();
        }

        #endregion

        #region Extraction Fichiers Locaux

        public (bool Success, string ExtractedText, string ErrorMessage) ExtractTextFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return (false, string.Empty, "Fichier introuvable sur le disque.");

            try
            {
                string text = DocumentReaderService.ExtractText(filePath);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return (false, string.Empty, "Le document ne contient aucun texte exploitable.");
                }

                if (text.Length > 40000)
                {
                    text = text.Substring(0, 40000) + "\n... [Texte tronqué pour l'analyse]";
                }

                return (true, text, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Erreur d'extraction : {ex.Message}");
            }
        }

        #endregion

        #region Analyse IA & Génération de Propositions

        /// <summary>
        /// Analyse le corpus de sources combinées (documents, URLs, posts réseaux sociaux) et compare aux données actuelles du profil
        /// pour produire une liste de propositions d'enrichissement claires et sélectables.
        /// </summary>
        public async Task<List<ProfileEnrichmentProposal>> AnalyzeCorpusAsync(
            List<(string SourceTitle, string Content)> sources,
            AfmiProfile currentProfile,
            string userSpecificFocus = "")
        {
            if (sources == null || sources.Count == 0)
                return new List<ProfileEnrichmentProposal>();

            var combinedText = new StringBuilder();
            foreach (var s in sources)
            {
                combinedText.AppendLine($"==================================================");
                combinedText.AppendLine($"SOURCE : {s.SourceTitle}");
                combinedText.AppendLine($"==================================================");
                combinedText.AppendLine(s.Content);
                combinedText.AppendLine();
            }

            string prompt = BuildAnalysisPrompt(combinedText.ToString(), currentProfile, userSpecificFocus);
            string systemPrompt = "Tu es un consultant senior en renforcement des capacités institutionnelles des ONG et spécialiste de la conformité aux exigences des bailleurs internationaux (UE, PNUD, FEM, AFD, Banque Mondiale). Tu analyses des documents et liens pour enrichir rigoureusement le profil d'une association.";

            try
            {
                if (AiAssistantService.IsConfigured())
                {
                    string aiResponse = await AiAssistantService.CallAiAsync(prompt, systemPrompt);
                    if (!string.IsNullOrWhiteSpace(aiResponse))
                    {
                        var proposals = ParseAiProposalsJson(aiResponse, currentProfile);
                        if (proposals.Count > 0)
                        {
                            return proposals;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Call Error: {ex.Message}");
            }

            // Fallback intelligent heuristique si IA indisponible ou JSON non lisible
            return GenerateHeuristicProposals(sources, currentProfile);
        }

        private static string BuildAnalysisPrompt(string corpusText, AfmiProfile p, string focus)
        {
            return $@"Tu disposes ci-dessous d'un ensemble de documents, pages web ou publications relatifs à l'organisation '{p.OrganizationName}' ({p.Acronym}).

Voici les informations institutionnelles actuellement enregistrées dans l'application :
- Statut & Récépissé : {p.LegalStatus} | {p.RegistrationNumber}
- Siège & Contacts : {p.Headquarters} | {p.Email} | {p.Website}
- Mission : {p.Mission}
- Vision : {p.Vision}
- Objectifs stratégiques : {p.StrategicObjectives}
- Historique : {p.HistoryTrajectory}
- Réalisations phares : {p.KeyAchievements}
- Chiffres clés : {p.ImpactFigures}
- Capacités RH & Gouvernance : {p.HumanResourcesCapacity}
- Capacité financière & fiduciaire : {p.FinancialManagementCapacity}
- Moyens matériels & logistique : {p.LogisticalMaterialCapacity}
- Projets bailleurs passés : {p.PastProjectsReferences}
- Partenariats stratégiques : {p.StrategicPartnerships}
- Domaines d'expertise : {p.TechnicalExpertiseSummary}

{(string.IsNullOrWhiteSpace(focus) ? "" : $"ATTENTION PARTICULIÈRE DEMANDÉE PAR L'UTILISATEUR : {focus}")}

CORPUS DES DOCUMENTS ET LIENS ANALYSÉS :
{corpusText}

MISSION :
Examine minutieusement ce corpus et identifie TOUTES les nouvelles informations factuelles, réalisations, chiffres, partenariats, bailleurs, reconnaissances ou coordonnées permettant de compléter, mettre à jour ou enrichir le profil de l'association.

Formate ta réponse STRICTEMENT sous forme d'un tableau JSON valide, sans texte d'introduction ni de conclusion, selon le schéma suivant :
[
  {{
    ""fieldName"": ""KeyAchievements"",
    ""fieldDisplayName"": ""Réalisations Phares & Track Record"",
    ""category"": ""Impact"",
    ""currentValue"": ""valeur actuelle résumée"",
    ""suggestedValue"": ""nouveau texte complet ou éléments à ajouter"",
    ""mergeMode"": ""Append"",
    ""sourceOrigin"": ""Nom du document ou URL d'où provient l'info"",
    ""rationale"": ""Précise ce que cette information apporte de concret pour les évaluateurs et bailleurs de fonds.""
  }}
]

Règles pour les champs disponibles (fieldName) :
- ""KeyAchievements"" (Réalisations Phares & Track Record)
- ""ImpactFigures"" (Chiffres Clés d'Impact)
- ""PastProjectsReferences"" (Références de Projets & Bailleurs Passés)
- ""StrategicPartnerships"" (Partenariats Stratégiques & Institutionnels)
- ""TechnicalExpertiseSummary"" (Domaines d'Expertise Technique)
- ""HumanResourcesCapacity"" (Capacités RH & Équipe Permanente)
- ""FinancialManagementCapacity"" (Capacités Financières & Fiducières)
- ""LogisticalMaterialCapacity"" (Moyens Matériels & Logistiques)
- ""Mission"" (Mission Institutionnelle)
- ""Vision"" (Vision Stratégique)
- ""StrategicObjectives"" (Objectifs Stratégiques)
- ""HistoryTrajectory"" (Historique & Jalons Clés)
- ""Website"" (Site Web Officiel)
- ""FacebookUrl"" (Page Facebook)
- ""LinkedInUrl"" (Profil LinkedIn)

Règles de mergeMode :
- ""Append"" : s'il s'agit d'ajouter de nouvelles réalisations, chiffres, projets ou partenaires sans effacer l'existant.
- ""Replace"" : s'il s'agit d'une correction de coordonnées, date, ou reformulation supérieure de la mission/vision.";
        }

        private static List<ProfileEnrichmentProposal> ParseAiProposalsJson(string response, AfmiProfile currentProfile)
        {
            var results = new List<ProfileEnrichmentProposal>();
            try
            {
                // Nettoyer d'éventuels blocs de code markdown ```json ... ```
                string json = response.Trim();
                if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    json = json.Substring(7);
                }
                else if (json.StartsWith("```"))
                {
                    json = json.Substring(3);
                }
                if (json.EndsWith("```"))
                {
                    json = json.Substring(0, json.Length - 3);
                }
                json = json.Trim();

                // Trouver le début du tableau JSON '['
                int start = json.IndexOf('[');
                int end = json.LastIndexOf(']');
                if (start >= 0 && end > start)
                {
                    json = json.Substring(start, end - start + 1);
                }

                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string fieldName = el.TryGetProperty("fieldName", out var fn) ? fn.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(fieldName)) continue;

                    string displayName = el.TryGetProperty("fieldDisplayName", out var dn) ? dn.GetString() ?? fieldName : fieldName;
                    string category = el.TryGetProperty("category", out var cat) ? cat.GetString() ?? "Général" : "Général";
                    string suggested = el.TryGetProperty("suggestedValue", out var sv) ? sv.GetString() ?? "" : "";
                    string modeStr = el.TryGetProperty("mergeMode", out var mm) ? mm.GetString() ?? "Append" : "Append";
                    string source = el.TryGetProperty("sourceOrigin", out var so) ? so.GetString() ?? "Document source" : "Document source";
                    string rationale = el.TryGetProperty("rationale", out var rat) ? rat.GetString() ?? "" : "";

                    var mergeMode = modeStr.Equals("Replace", StringComparison.OrdinalIgnoreCase) 
                        ? EnrichmentMergeMode.Replace 
                        : EnrichmentMergeMode.Append;

                    string current = GetCurrentFieldValue(currentProfile, fieldName);

                    results.Add(new ProfileEnrichmentProposal
                    {
                        FieldName = fieldName,
                        FieldDisplayName = displayName,
                        Category = category,
                        CurrentValue = current,
                        SuggestedValue = suggested,
                        MergeMode = mergeMode,
                        SourceOrigin = source,
                        Rationale = rationale,
                        IsSelected = true
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing AI JSON proposals: {ex.Message}");
            }

            return results;
        }

        private static string GetCurrentFieldValue(AfmiProfile p, string fieldName) => fieldName switch
        {
            "OrganizationName" => p.OrganizationName,
            "Acronym" => p.Acronym,
            "LegalStatus" => p.LegalStatus,
            "RegistrationNumber" => p.RegistrationNumber,
            "Headquarters" => p.Headquarters,
            "Address" => p.Address,
            "Phone" => p.Phone,
            "Email" => p.Email,
            "Website" => p.Website,
            "FacebookUrl" => p.FacebookUrl,
            "LinkedInUrl" => p.LinkedInUrl,
            "Mission" => p.Mission,
            "Vision" => p.Vision,
            "CoreValues" => p.CoreValues,
            "StrategicObjectives" => p.StrategicObjectives,
            "HistoryTrajectory" => p.HistoryTrajectory,
            "KeyAchievements" => p.KeyAchievements,
            "ImpactFigures" => p.ImpactFigures,
            "RecognitionsAwards" => p.RecognitionsAwards,
            "HumanResourcesCapacity" => p.HumanResourcesCapacity,
            "FinancialManagementCapacity" => p.FinancialManagementCapacity,
            "LogisticalMaterialCapacity" => p.LogisticalMaterialCapacity,
            "PastProjectsReferences" => p.PastProjectsReferences,
            "StrategicPartnerships" => p.StrategicPartnerships,
            "TechnicalExpertiseSummary" => p.TechnicalExpertiseSummary,
            "DonorCapacityStatement" => p.DonorCapacityStatement,
            _ => string.Empty
        };

        private static List<ProfileEnrichmentProposal> GenerateHeuristicProposals(
            List<(string SourceTitle, string Content)> sources, 
            AfmiProfile currentProfile)
        {
            var list = new List<ProfileEnrichmentProposal>();
            string allText = string.Join("\n", sources.Select(s => s.Content));
            string primarySource = sources.FirstOrDefault().SourceTitle ?? "Source documentaire";

            // Détection de chiffres clés et métriques (ex: "15 000", "2024", "hectares", "DH", "EUR")
            var metricMatches = Regex.Matches(allText, @"(\b\d{1,3}(?:[ \.,]\d{3})*(?:\s*(?:hectares|ha|arbres|plants|bénéficiaires|femmes|jeunes|coopératives|DH|MAD|€|EUR|\$|USD))\b)", RegexOptions.IgnoreCase);
            if (metricMatches.Count > 0)
            {
                var discoveredMetrics = metricMatches.Cast<Match>().Select(m => m.Value.Trim()).Distinct().Take(5).ToList();
                string newMetrics = string.Join(" | ", discoveredMetrics);

                list.Add(new ProfileEnrichmentProposal
                {
                    FieldName = "ImpactFigures",
                    FieldDisplayName = "Chiffres Clés d'Impact Détectés",
                    Category = "Impact",
                    CurrentValue = currentProfile.ImpactFigures,
                    SuggestedValue = $"{currentProfile.ImpactFigures} | Données récentes : {newMetrics}",
                    MergeMode = EnrichmentMergeMode.Append,
                    SourceOrigin = primarySource,
                    Rationale = "Chiffres et indicateurs quantitatifs identifiés dans les documents récents renforçant la crédibilité auprès des bailleurs.",
                    IsSelected = true
                });
            }

            // Détection de mentions de partenaires ou bailleurs
            var donorKeywords = new[] { "Union Européenne", "UE", "PNUD", "UNDP", "FEM", "GEF", "AFD", "Banque Mondiale", "World Bank", "CEPF", "UICN", "IUCN", "FAO", "ANEF", "Université" };
            var foundDonors = donorKeywords.Where(d => allText.Contains(d, StringComparison.OrdinalIgnoreCase)).ToList();
            if (foundDonors.Count > 0)
            {
                list.Add(new ProfileEnrichmentProposal
                {
                    FieldName = "StrategicPartnerships",
                    FieldDisplayName = "Partenariats & Bailleurs Mentionnés",
                    Category = "Partenariats",
                    CurrentValue = currentProfile.StrategicPartnerships,
                    SuggestedValue = $"{currentProfile.StrategicPartnerships}\n• Partenariats confirmés dans les documents récents : {string.Join(", ", foundDonors)}",
                    MergeMode = EnrichmentMergeMode.Append,
                    SourceOrigin = primarySource,
                    Rationale = "Présence de collaborations avec des bailleurs ou institutions clés identifiée dans le corpus.",
                    IsSelected = true
                });
            }

            // Détection d'URLs de réseaux sociaux
            var fbMatch = Regex.Match(allText, @"https?://(?:www\.)?facebook\.com/[a-zA-Z0-9_\-\.]+");
            if (fbMatch.Success && string.IsNullOrWhiteSpace(currentProfile.FacebookUrl))
            {
                list.Add(new ProfileEnrichmentProposal
                {
                    FieldName = "FacebookUrl",
                    FieldDisplayName = "Page Facebook Officielle",
                    Category = "Identité & Réseaux",
                    CurrentValue = currentProfile.FacebookUrl,
                    SuggestedValue = fbMatch.Value,
                    MergeMode = EnrichmentMergeMode.Replace,
                    SourceOrigin = primarySource,
                    Rationale = "Lien officiel vers la communauté en ligne de l'association.",
                    IsSelected = true
                });
            }

            var liMatch = Regex.Match(allText, @"https?://(?:www\.)?linkedin\.com/(?:company|in)/[a-zA-Z0-9_\-\.]+");
            if (liMatch.Success && string.IsNullOrWhiteSpace(currentProfile.LinkedInUrl))
            {
                list.Add(new ProfileEnrichmentProposal
                {
                    FieldName = "LinkedInUrl",
                    FieldDisplayName = "Profil LinkedIn Institutionnel",
                    Category = "Identité & Réseaux",
                    CurrentValue = currentProfile.LinkedInUrl,
                    SuggestedValue = liMatch.Value,
                    MergeMode = EnrichmentMergeMode.Replace,
                    SourceOrigin = primarySource,
                    Rationale = "Lien professionnel améliorant la visibilité institutionnelle.",
                    IsSelected = true
                });
            }

            // Extraction d'extraits d'activités / réalisations
            var lines = allText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => l.Trim())
                               .Where(l => l.Length > 50 && (l.Contains("projet", StringComparison.OrdinalIgnoreCase) || l.Contains("réalisation", StringComparison.OrdinalIgnoreCase) || l.Contains("restauration", StringComparison.OrdinalIgnoreCase) || l.Contains("formation", StringComparison.OrdinalIgnoreCase)))
                               .Take(3)
                               .ToList();

            if (lines.Count > 0)
            {
                string extraAchievements = string.Join("\n", lines.Select(l => $"• {l}"));
                list.Add(new ProfileEnrichmentProposal
                {
                    FieldName = "KeyAchievements",
                    FieldDisplayName = "Nouvelles Réalisations Détectées",
                    Category = "Réalisations",
                    CurrentValue = currentProfile.KeyAchievements,
                    SuggestedValue = $"{currentProfile.KeyAchievements}\n{extraAchievements}",
                    MergeMode = EnrichmentMergeMode.Append,
                    SourceOrigin = primarySource,
                    Rationale = "Activités concrètes et réalisations de terrain extraites des rapports ou publications.",
                    IsSelected = true
                });
            }

            return list;
        }

        #endregion
    }
}
