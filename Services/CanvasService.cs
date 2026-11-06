using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using ClosedXML.Excel;
using Xceed.Words.NET;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AFMI_Montage_projet_2.Services
{
    public class CanvasSectionModel
    {
        public string Section { get; set; } = "1. Généralités";
        public List<CanvasFieldModel> Fields { get; set; } = new();
    }

    public class CanvasFieldModel
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = "TextLong"; // TextLong, TextShort, Dropdown, Checkbox
        public string Placeholder { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
    }

    /// <summary>
    /// خدمة متكاملة لإدارة واستيراد وتصدير وتصميم كانفات الممولين (Word, PDF, Excel)
    /// والتحليل والملء الآلي عبر الذكاء الاصطناعي.
    /// </summary>
    public static class CanvasService
    {
        static CanvasService()
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(CanvasService), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        #region 1. Import Canvas from Document (Word, PDF, Excel)

        /// <summary>
        /// استيراد ملف كانفا خارجي (Word, PDF, Excel) وتحليله لاستخراج الأقسام والأسئلة تلقائياً
        /// </summary>
        public static async Task<FormTemplate> ImportCanvasFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("الملف المحدد غير موجود.", filePath);

            string rawText = DocumentReaderService.ExtractText(filePath);
            if (string.IsNullOrWhiteSpace(rawText))
                throw new InvalidOperationException("لم يتم العثور على نصوص قابلة للقراءة في هذا المستند.");

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

            // استخراج الهيكل باستخدام الذكاء الاصطناعي إن توفر، أو عبر المحلل الذكي البديل
            List<CanvasSectionModel> sections;
            try
            {
                sections = await ExtractStructureWithAiAsync(fileNameWithoutExt, rawText);
            }
            catch
            {
                sections = ExtractStructureFallback(fileNameWithoutExt, rawText);
            }

            if (sections.Count == 0 || sections.All(s => s.Fields.Count == 0))
            {
                sections = ExtractStructureFallback(fileNameWithoutExt, rawText);
            }

            string jsonSchema = JsonSerializer.Serialize(sections, new JsonSerializerOptions { WriteIndented = true });

            using var db = new AppDbContext();
            var template = new FormTemplate
            {
                Name = $"Canevas : {fileNameWithoutExt}",
                Description = $"مستورد من الملف: {Path.GetFileName(filePath)} بتاريخ {DateTime.Now:dd/MM/yyyy HH:mm}",
                JsonSchema = jsonSchema,
                Version = 1,
                IsActive = true
            };

            db.FormTemplates.Add(template);
            await db.SaveChangesAsync();

            return template;
        }

        private static async Task<List<CanvasSectionModel>> ExtractStructureWithAiAsync(string title, string rawText)
        {
            // نأخذ عينة كافية من النص دون تجاوز حجم الـ context
            string truncated = rawText.Length > 8000 ? rawText.Substring(0, 8000) : rawText;

            string prompt = $@"Tu es un expert en analyse de canevas et formulaires de bailleurs de fonds internationaux (FEM, UE, AFD, Banque Mondiale, USAID).
Voici le contenu extrait d'un document de canevas intitulé '{title}' :
---
{truncated}
---

TÂCHE :
Analyse ce texte et extrait la liste ordonnée des sections et des questions/champs à renseigner par le porteur de projet.
Renvoie UNIQUEMENT un tableau JSON valide (SANS BALISES MARKDOWN, SANS COMMENTAIRES, COMMENCE PAR [ ET FINIS PAR ]) avec la structure suivante :
[
  {{
    ""Section"": ""Nom de la section (ex: 1. Présentation du projet)"",
    ""Fields"": [
      {{
        ""Name"": ""IdentifiantUnique_SansEspaces"",
        ""Label"": ""Texte de la question ou consigne du bailleur"",
        ""Type"": ""TextLong"",
        ""Placeholder"": ""Consigne ou détails pour répondre...""
      }}
    ]
  }}
]

Pour 'Type', utilise principalement 'TextLong' (questions narratives), 'TextShort' (dates, montants, titres), ou 'Dropdown' si des choix sont explicitement listés.";

            string systemPrompt = "Tu es un parseur JSON strict pour l'ingénierie documentaire.";
            string aiResponse = await AiAssistantService.CallAiAsync(prompt, systemPrompt);

            aiResponse = CleanJsonString(aiResponse);

            try
            {
                var sections = JsonSerializer.Deserialize<List<CanvasSectionModel>>(aiResponse);
                if (sections != null && sections.Count > 0) return sections;
            }
            catch
            {
                // Fallback sur l'extraction manuelle/textuelle si le JSON retourné par l'IA est invalide
            }

            return ExtractStructureFallback(title, rawText);
        }

        private static List<CanvasSectionModel> ExtractStructureFallback(string title, string rawText)
        {
            var sections = new List<CanvasSectionModel>();
            var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            var currentSection = new CanvasSectionModel { Section = "1. Informations Générales" };
            int fieldCounter = 1;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length < 3) continue;

                // كشف العناوين المحتملة للأقسام
                if ((trimmed.StartsWith("Section", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("Partie", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("Chapitre", StringComparison.OrdinalIgnoreCase) ||
                     (char.IsDigit(trimmed[0]) && trimmed.Contains('.')) ||
                     trimmed.EndsWith(":") && trimmed.Length < 60) && trimmed.Length < 80)
                {
                    if (currentSection.Fields.Count > 0)
                    {
                        sections.Add(currentSection);
                        currentSection = new CanvasSectionModel { Section = trimmed };
                    }
                    else
                    {
                        currentSection.Section = trimmed;
                    }
                    continue;
                }

                // كشف الأسئلة أو البنود
                if (trimmed.EndsWith("?") || trimmed.EndsWith(":") || trimmed.Length >= 15)
                {
                    currentSection.Fields.Add(new CanvasFieldModel
                    {
                        Name = $"Field_{fieldCounter++}",
                        Label = trimmed,
                        Type = trimmed.Length > 80 || trimmed.EndsWith("?") ? "TextLong" : "TextShort",
                        Placeholder = "Saisissez votre réponse ici..."
                    });
                }

                if (currentSection.Fields.Count >= 10)
                {
                    sections.Add(currentSection);
                    currentSection = new CanvasSectionModel { Section = $"Section {sections.Count + 1}" };
                }
            }

            if (currentSection.Fields.Count > 0)
            {
                sections.Add(currentSection);
            }

            if (sections.Count == 0)
            {
                sections.Add(new CanvasSectionModel
                {
                    Section = "1. Formulation Générale",
                    Fields = new List<CanvasFieldModel>
                    {
                        new() { Name = "DescriptionProjet", Label = "Description et objectifs du projet", Type = "TextLong" },
                        new() { Name = "ResultatsAttendus", Label = "Résultats attendus et indicateurs", Type = "TextLong" },
                        new() { Name = "BudgetEtPerennite", Label = "Budget et pérennité des actions", Type = "TextLong" }
                    }
                });
            }

            return sections;
        }

        #endregion

        #region 2. Export Canvas to Word (.docx)

        /// <summary>
        /// تصدير الكانفا مع الأسئلة ومربعات الإجابات إلى مستند Word احترافي
        /// </summary>
        public static void ExportCanvasToDocx(FormTemplate template, Dictionary<string, string> answers, Project? project, string filePath)
        {
            var sections = ParseSchema(template.JsonSchema);

            using var doc = DocX.Create(filePath);

            // 1. غلاف / ترويسة المستند
            var titlePara = doc.InsertParagraph();
            titlePara.AppendLine("CANEVAS SECTORIEL BAILLEUR DE FONDS").FontSize(18).Bold().Color(Xceed.Drawing.Color.DarkBlue);
            titlePara.AppendLine(template.Name.ToUpper()).FontSize(14).Bold().Color(Xceed.Drawing.Color.Blue);
            titlePara.SpacingAfter(15);

            if (project != null)
            {
                var projPara = doc.InsertParagraph();
                projPara.AppendLine($"Projet : {project.Title}").Bold().FontSize(12);
                if (!string.IsNullOrWhiteSpace(project.LeadOrganization))
                    projPara.AppendLine($"Porteur : {project.LeadOrganization}").FontSize(11);
                projPara.AppendLine($"Date d'export : {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(10).Italic();
                projPara.SpacingAfter(20);
            }

            // 2. إدراج الأقسام والأسئلة ومربعات الإجابة
            foreach (var section in sections)
            {
                var secTitle = doc.InsertParagraph(section.Section.ToUpper());
                secTitle.FontSize(13).Bold().Color(Xceed.Drawing.Color.DarkBlue);
                secTitle.SpacingBefore(15).SpacingAfter(10);

                // إدراج الأسئلة في جدول أو بطاقات منسقة
                foreach (var field in section.Fields)
                {
                    answers.TryGetValue(field.Name, out var userAns);
                    if (string.IsNullOrWhiteSpace(userAns)) userAns = "[Non renseigné]";

                    // مربع السؤال
                    var qPara = doc.InsertParagraph($"❓ {field.Label}");
                    qPara.FontSize(11).Bold().Color(Xceed.Drawing.Color.Black);
                    qPara.SpacingBefore(8).SpacingAfter(4);

                    // مربع الإجابة المظلل
                    var ansTable = doc.AddTable(1, 1);
                    ansTable.Rows[0].Cells[0].FillColor = Xceed.Drawing.Color.LightGray;
                    var cellPara = ansTable.Rows[0].Cells[0].Paragraphs.First();
                    cellPara.Append(userAns).FontSize(10.5);
                    cellPara.SpacingBefore(6).SpacingAfter(6);

                    doc.InsertTable(ansTable);
                    doc.InsertParagraph().SpacingAfter(10);
                }
            }

            doc.Save();
        }

        #endregion

        #region 3. Export Canvas to PDF (.pdf via QuestPDF)

        /// <summary>
        /// تصدير الكانفا إلى ملف PDF منسق ببطاقات الأسئلة والأجوبة الرسمية
        /// </summary>
        public static void ExportCanvasToPdf(FormTemplate template, Dictionary<string, string> answers, Project? project, string filePath)
        {
            var sections = ParseSchema(template.JsonSchema);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.8f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily(SettingsService.GetSetting("Font_Base", "Arial")).FontSize(10));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("AFMI MONTAGE PROJET • CANEVAS BAILLEUR").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
                            col.Item().Text(template.Name).FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                        });

                        if (project != null)
                        {
                            row.AutoItem().Text($"Projet: {project.Title}").FontSize(10).Italic();
                        }
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(12);

                        foreach (var section in sections)
                        {
                            col.Item().Background(Colors.Blue.Darken3).Padding(8).Text(section.Section.ToUpper())
                                .FontSize(11).Bold().FontColor(Colors.White);

                            foreach (var field in section.Fields)
                            {
                                answers.TryGetValue(field.Name, out var userAns);
                                if (string.IsNullOrWhiteSpace(userAns)) userAns = "[Non renseigné]";

                                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).CornerRadius(4).Column(box =>
                                {
                                    // Question Header
                                    box.Item().Background(Colors.Grey.Lighten4).Padding(6).Row(qRow =>
                                    {
                                        qRow.AutoItem().Text("❓ ").FontSize(10);
                                        qRow.RelativeItem().Text(field.Label).FontSize(10.5f).Bold().FontColor(Colors.Grey.Darken3);
                                    });

                                    // Answer Body
                                    box.Item().Padding(8).Text(userAns).FontSize(10).FontColor(Colors.Grey.Darken4);
                                });
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                        x.Span($" • Généré le {DateTime.Now:dd/MM/yyyy HH:mm}");
                    });
                });
            }).GeneratePdf(filePath);
        }

        #endregion

        #region 4. Export Canvas to Excel (.xlsx via ClosedXML)

        /// <summary>
        /// تصدير الكانفا إلى ملف Excel منسق بجدول للأسئلة والأجوبة
        /// </summary>
        public static void ExportCanvasToExcel(FormTemplate template, Dictionary<string, string> answers, Project? project, string filePath)
        {
            var sections = ParseSchema(template.JsonSchema);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Canevas Bailleur");

            // Header
            worksheet.Cell("A1").Value = "CANEVAS SECTORIEL BAILLEUR";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 16;
            worksheet.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#0F172A");

            worksheet.Cell("A2").Value = $"Modèle : {template.Name}";
            worksheet.Cell("A2").Style.Font.Bold = true;
            worksheet.Cell("A2").Style.Font.FontSize = 12;
            worksheet.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#0284C7");

            if (project != null)
            {
                worksheet.Cell("A3").Value = $"Projet associé : {project.Title}";
                worksheet.Cell("A3").Style.Font.Italic = true;
            }

            // Columns
            string[] headers = { "N°", "Section / Rubrique", "Question / Exigence du Bailleur", "Type", "Réponse / Formulation" };
            int rowIdx = 5;

            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = worksheet.Cell(rowIdx, col);
                cell.Value = headers[col - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0EA5E9");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            rowIdx++;
            int qIndex = 1;

            foreach (var section in sections)
            {
                // Section Title Row
                var secCell = worksheet.Cell(rowIdx, 2);
                secCell.Value = section.Section;
                worksheet.Range(rowIdx, 1, rowIdx, headers.Length).Merge().Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
                worksheet.Range(rowIdx, 1, rowIdx, headers.Length).Style.Font.Bold = true;
                rowIdx++;

                foreach (var field in section.Fields)
                {
                    answers.TryGetValue(field.Name, out var userAns);
                    if (string.IsNullOrWhiteSpace(userAns)) userAns = "";

                    worksheet.Cell(rowIdx, 1).Value = qIndex++;
                    worksheet.Cell(rowIdx, 2).Value = section.Section;
                    worksheet.Cell(rowIdx, 3).Value = field.Label;
                    worksheet.Cell(rowIdx, 4).Value = field.Type;
                    worksheet.Cell(rowIdx, 5).Value = userAns;

                    worksheet.Cell(rowIdx, 3).Style.Alignment.WrapText = true;
                    worksheet.Cell(rowIdx, 5).Style.Alignment.WrapText = true;
                    worksheet.Cell(rowIdx, 5).Style.Font.Bold = !string.IsNullOrWhiteSpace(userAns);

                    rowIdx++;
                }
            }

            worksheet.Column(1).Width = 6;
            worksheet.Column(2).Width = 25;
            worksheet.Column(3).Width = 45;
            worksheet.Column(4).Width = 12;
            worksheet.Column(5).Width = 60;

            workbook.SaveAs(filePath);
        }

        #endregion

        #region Helpers

        public static List<CanvasSectionModel> ParseSchema(string jsonSchema)
        {
            if (string.IsNullOrWhiteSpace(jsonSchema)) return new List<CanvasSectionModel>();
            try
            {
                return JsonSerializer.Deserialize<List<CanvasSectionModel>>(jsonSchema) ?? new List<CanvasSectionModel>();
            }
            catch
            {
                return new List<CanvasSectionModel>();
            }
        }

        private static string CleanJsonString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "[]";
            string cleaned = input.Trim();

            var matchMarkdown = System.Text.RegularExpressions.Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)\s*```", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (matchMarkdown.Success)
            {
                cleaned = matchMarkdown.Groups[1].Value.Trim();
            }

            int firstBracket = cleaned.IndexOf('[');
            int lastBracket = cleaned.LastIndexOf(']');
            if (firstBracket >= 0 && lastBracket > firstBracket)
            {
                cleaned = cleaned.Substring(firstBracket, lastBracket - firstBracket + 1);
            }

            return cleaned;
        }

        #endregion
    }
}
