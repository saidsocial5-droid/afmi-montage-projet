using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AFMI_Montage_projet_2.Models.Radar;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>
    /// Service d'intégration et d'interopérabilité avec Google NotebookLM (https://notebooklm.google.com/).
    /// Prépare des packs documentaires optimisés pour l'ingestion IA (RAG), génère des fiches de synthèse
    /// et gère l'ouverture directe du carnet institutionnel.
    /// </summary>
    public static class NotebookLmService
    {
        public const string DefaultNotebookLmUrl = "https://notebooklm.google.com";

        static NotebookLmService()
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
            }
            catch { }
        }

        /// <summary>
        /// Ouvre l'URL du carnet NotebookLM dans le navigateur web par défaut.
        /// </summary>
        public static bool OpenNotebookLm(string? url = null)
        {
            try
            {
                string targetUrl = string.IsNullOrWhiteSpace(url) ? DefaultNotebookLmUrl : url.Trim();
                if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    targetUrl = "https://" + targetUrl;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open NotebookLM: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Génère le dossier source institutionnel au format Markdown standard,
        /// structuré de manière optimale pour le moteur de compréhension et d'indexation de Google NotebookLM.
        /// </summary>
        public static string GenerateKnowledgeBaseMarkdown(AfmiProfile p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));

            var sb = new StringBuilder();

            sb.AppendLine($"# DOSSIER INSTITUTIONNEL & BASE DE CONNAISSANCES : {p.OrganizationName.ToUpper()}");
            sb.AppendLine($"*Document source préparé pour Google NotebookLM — Généré le {DateTime.Now:dd MMMM yyyy à HH:mm}*");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            sb.AppendLine("## 1. IDENTITÉ INSTITUTIONNELLE & STATUT JURIDIQUE");
            sb.AppendLine($"- **Nom officiel :** {p.OrganizationName}");
            sb.AppendLine($"- **Sigle / Acronyme :** {p.Acronym}");
            sb.AppendLine($"- **Statut légal :** {p.LegalStatus}");
            sb.AppendLine($"- **Récépissé / Agrément :** {p.RegistrationNumber}");
            sb.AppendLine($"- **Année de création :** {p.CreationDate}");
            sb.AppendLine($"- **Représentant légal :** {p.LegalRepresentative}");
            sb.AppendLine($"- **Siège officiel :** {p.Headquarters}");
            sb.AppendLine($"- **Adresse postale :** {p.Address}");
            sb.AppendLine($"- **Téléphone :** {p.Phone}");
            sb.AppendLine($"- **Courriel officiel :** {p.Email}");
            sb.AppendLine($"- **Site Web officiel :** {p.Website}");
            if (!string.IsNullOrWhiteSpace(p.FacebookUrl)) sb.AppendLine($"- **Page Facebook :** {p.FacebookUrl}");
            if (!string.IsNullOrWhiteSpace(p.LinkedInUrl)) sb.AppendLine($"- **Profil LinkedIn :** {p.LinkedInUrl}");
            if (!string.IsNullOrWhiteSpace(p.TwitterUrl)) sb.AppendLine($"- **Compte X / Twitter :** {p.TwitterUrl}");
            sb.AppendLine();

            sb.AppendLine("## 2. DÉCLARATION DE CAPACITÉ INSTITUTIONNELLE (CAPACITY STATEMENT)");
            sb.AppendLine("> " + (string.IsNullOrWhiteSpace(p.DonorCapacityStatement) ? "Forte de son ancrage territorial et de son expertise, l'organisation réunit toutes les garanties institutionnelles pour mener des projets d'envergure." : p.DonorCapacityStatement.Replace("\n", "\n> ")));
            sb.AppendLine();

            sb.AppendLine("## 3. VISION, MISSION ET AXES STRATÉGIQUES");
            sb.AppendLine($"### Mission");
            sb.AppendLine(p.Mission);
            sb.AppendLine();
            sb.AppendLine($"### Vision Stratégique");
            sb.AppendLine(p.Vision);
            sb.AppendLine();
            sb.AppendLine($"### Valeurs et Principes Directeurs");
            sb.AppendLine(p.CoreValues);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(p.StrategicAxes))
            {
                sb.AppendLine($"### Les 5 Grands Axes Stratégiques d'Intervention (Plan Stratégique AFMI)");
                sb.AppendLine(p.StrategicAxes);
                sb.AppendLine();
            }
            sb.AppendLine($"### Objectifs Stratégiques");
            sb.AppendLine(p.StrategicObjectives);
            sb.AppendLine();

            sb.AppendLine("## 4. ANCRAGE TERRITORIAL ET DOMAINES D'INTERVENTION");
            sb.AppendLine($"### Zones Géographiques Couvertes");
            foreach (var z in p.GeographicZones)
            {
                sb.AppendLine($"- {z}");
            }
            sb.AppendLine();
            sb.AppendLine($"### Thématiques Prioritaires");
            foreach (var t in p.PreferredThemes)
            {
                sb.AppendLine($"- {t}");
            }
            sb.AppendLine();

            sb.AppendLine("## 5. TRAJECTOIRE HISTORIQUE ET ÉVOLUTION");
            sb.AppendLine(p.HistoryTrajectory);
            sb.AppendLine();

            sb.AppendLine("## 6. BILAN D'IMPACT ET RÉALISATIONS PHARES (TRACK RECORD)");
            sb.AppendLine("### Réalisations Majeures");
            sb.AppendLine(p.KeyAchievements);
            sb.AppendLine();
            sb.AppendLine("### Chiffres Clés d'Impact Vérifiables");
            sb.AppendLine($"**{p.ImpactFigures}**");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(p.RecognitionsAwards))
            {
                sb.AppendLine("### Distinctions et Reconnaissances Officielles");
                sb.AppendLine(p.RecognitionsAwards);
                sb.AppendLine();
            }

            sb.AppendLine("## 7. CAPACITÉS ORGANISATIONNELLES ET GOUVERNANCE");
            sb.AppendLine("### Ressources Humaines et Équipe Permanente");
            sb.AppendLine(p.HumanResourcesCapacity);
            sb.AppendLine();
            sb.AppendLine("### Capacité de Gestion Financière, Comptable et Fiduciaire");
            sb.AppendLine(p.FinancialManagementCapacity);
            sb.AppendLine();
            sb.AppendLine("### Moyens Matériels, Équipements de Terrain et Logistique");
            sb.AppendLine(p.LogisticalMaterialCapacity);
            sb.AppendLine();

            sb.AppendLine("## 8. EXPÉRIENCE BAILLEURS DE FONDS ET PARTENARIATS MULTILATÉRAUX");
            sb.AppendLine($"### Bailleurs Partenaires Clés");
            sb.AppendLine(string.Join(" • ", p.PreferredDonors));
            sb.AppendLine();
            sb.AppendLine("### Références de Projets et Subventions Exécutés");
            sb.AppendLine(p.PastProjectsReferences);
            sb.AppendLine();
            sb.AppendLine("### Partenariats Stratégiques et Institutionnels");
            sb.AppendLine(p.StrategicPartnerships);
            sb.AppendLine();
            sb.AppendLine("### Domaines d'Expertise Technique Reconnus");
            sb.AppendLine(p.TechnicalExpertiseSummary);
            sb.AppendLine();

            if (p.AnalyzedSourcesHistory != null && p.AnalyzedSourcesHistory.Count > 0)
            {
                sb.AppendLine("## 9. SOURCES DOCUMENTAIRES & HISTORIQUE DES ANALYSES");
                foreach (var src in p.AnalyzedSourcesHistory)
                {
                    sb.AppendLine($"- {src}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine($"*Base de connaissances institutionnelle certifiée par {p.Acronym} — Prête pour exploitation NotebookLM.*");

            return sb.ToString();
        }

        /// <summary>
        /// Exporte le dossier de connaissances en fichier texte, markdown ou PDF selon le chemin spécifié.
        /// </summary>
        public static void ExportKnowledgeBase(string filePath, AfmiProfile p)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".pdf")
            {
                ExportKnowledgeBaseToPdf(filePath, p);
            }
            else
            {
                string markdown = GenerateKnowledgeBaseMarkdown(p);
                File.WriteAllText(filePath, markdown, Encoding.UTF8);
            }
        }

        private static void ExportKnowledgeBaseToPdf(string filePath, AfmiProfile p)
        {
            string markdown = GenerateKnowledgeBaseMarkdown(p);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Segoe UI"));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"NOTEBOOKLM KNOWLEDGE PACK • {p.Acronym}")
                                    .FontSize(13).Bold().FontColor(Colors.DeepPurple.Darken3);
                                c.Item().Text(p.OrganizationName)
                                    .FontSize(10).FontColor(Colors.Grey.Darken2);
                            });
                            r.ConstantItem(120).AlignRight().Text($"Généré le {DateTime.Now:dd/MM/yyyy}")
                                .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                        });
                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.DeepPurple.Lighten2);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8).Column(box =>
                        {
                            box.Item().Text("📌 Source RAG pour Google NotebookLM").Bold().FontColor(Colors.DeepPurple.Darken2);
                            box.Item().Text("Ce document rassemble l'ensemble des données vérifiées sur la gouvernance, les réalisations, la gestion fiduciaire et l'expérience bailleurs de l'organisation.")
                                .FontSize(8.5f).FontColor(Colors.Grey.Darken3);
                        });

                        col.Item().PaddingTop(8);

                        // Afficher les sections du markdown ligne par ligne
                        var lines = markdown.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("# "))
                            {
                                col.Item().PaddingTop(10).Text(line.Substring(2)).FontSize(13).Bold().FontColor(Colors.DeepPurple.Darken3);
                            }
                            else if (line.StartsWith("## "))
                            {
                                col.Item().PaddingTop(8).Text(line.Substring(3)).FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                            }
                            else if (line.StartsWith("### "))
                            {
                                col.Item().PaddingTop(5).Text(line.Substring(4)).FontSize(10).Bold().FontColor(Colors.Grey.Darken4);
                            }
                            else if (line.StartsWith("> "))
                            {
                                col.Item().Padding(4).Background(Colors.Indigo.Lighten5).BorderLeft(2).BorderColor(Colors.Indigo.Medium)
                                    .PaddingLeft(6).Text(line.Substring(2)).Italic().FontSize(9);
                            }
                            else if (line.StartsWith("- "))
                            {
                                col.Item().PaddingLeft(10).Text($"• {line.Substring(2)}").FontSize(9);
                            }
                            else if (!line.StartsWith("---") && !line.StartsWith("*"))
                            {
                                col.Item().PaddingTop(2).Text(line).FontSize(9);
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                        x.Span($" • {p.Acronym} Dossier Source NotebookLM");
                    });
                });
            }).GeneratePdf(filePath);
        }

        /// <summary>
        /// Liste des questions et prompts d'excellence à utiliser dans Google NotebookLM pour interroger le carnet de l'organisation.
        /// </summary>
        public static List<NotebookLmPromptPreset> GetRecommendedPrompts()
        {
            return new List<NotebookLmPromptPreset>
            {
                new(
                    "Capacité du Demandeur (Union Européenne)",
                    "Rédige une section 'Capacités opérationnelles, techniques et financières du demandeur' prête pour un formulaire de candidature de subvention de l'Union Européenne, en valorisant l'équipe, les audits passés et les réalisations chiffrées.",
                    "Subventions UE"
                ),
                new(
                    "Garanties Fiduciaires & Due Diligence",
                    "Détaille les garanties de gouvernance, la procédure de double signature, le commissariat aux comptes et la gestion fiduciaire de l'organisation pour rassurer un bailleur multilatéral lors de la Due Diligence.",
                    "Audit & Finance"
                ),
                new(
                    "Bilan d'Impact & Chiffres Clés",
                    "Génère un tableau synthétique des réalisations concrètes de l'organisation avec les hectares restaurés, les coopératives appuyées, les bénéficiaires et les montants de subventions gérés.",
                    "Impact & Résultats"
                ),
                new(
                    "Note de Synthèse Partenariats",
                    "Résume l'ensemble des partenariats institutionnels, universitaires et internationaux de l'association, ainsi que son intégration dans le Réseau International des Forêts Modèles.",
                    "Partenariats"
                ),
                new(
                    "Script Audio Overview (Podcast 2 Voix)",
                    "Génère un aperçu engageant de présentation pour un podcast audio à deux voix expliquant la trajectoire unique de l'AFMI dans le Moyen Atlas et son modèle de gouvernance concertée.",
                    "Communication & Audio"
                ),
                new(
                    "Argumentaire Changement Climatique & Eaux",
                    "Rédige un argumentaire technique démontrant la légitimité et l'expérience de l'association pour conduire un programme d'adaptation au changement climatique et de restauration des zones humides.",
                    "Projets Climat"
                )
            };
        }
    }

    public record NotebookLmPromptPreset(string Title, string PromptText, string Category);
}
