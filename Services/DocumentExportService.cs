using System;
using System.IO;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Services.Radar;
using AFMI_Montage_projet_2.Services.Strategic;
using ClosedXML.Excel;
using Xceed.Words.NET;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.Services
{
    public class DocumentExportService
    {
        static DocumentExportService()
        {
            // Initialiser la licence QuestPDF pour le mode communautaire gratuit
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(DocumentExportService), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        public static void ExportBudgetToExcel(int projectId, string filePath)
        {
            using var db = new AppDbContext();
            var project = db.Projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null) throw new Exception("Projet introuvable.");

            var budgetItems = db.BudgetItems
                .Where(b => b.ProjectId == projectId)
                .Include(b => b.Category)
                .ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Budget Prévisionnel");

            // Styles & Titres
            worksheet.Cell("A1").Value = "BUDGET PRÉVISIONNEL DU PROJET";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 16;
            worksheet.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#1E293B");

            worksheet.Cell("A2").Value = $"Projet : {project.Title}";
            worksheet.Cell("A2").Style.Font.Italic = true;
            worksheet.Cell("A2").Style.Font.FontSize = 12;

            worksheet.Cell("A3").Value = $"Porteur : {project.LeadOrganization}";
            worksheet.Cell("A3").Style.Font.FontSize = 11;

            // En-têtes de colonnes
            string[] headers = {
                "Rubrique / Catégorie", "Description de la dépense", "Unité", "Quantité", 
                "Coût Unitaire", "Fréquence", "Coût Total", "Part Asso.", "Part Cofin.", "Part Demandée"
            };

            int startRow = 6;
            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = worksheet.Cell(startRow, col);
                cell.Value = headers[col - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0EA5E9");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int currentRow = startRow + 1;

            // Regrouper les dépenses par catégorie
            var groupedItems = budgetItems.GroupBy(i => i.Category?.Name ?? "Autres");

            foreach (var group in groupedItems)
            {
                // Ligne de titre de catégorie
                worksheet.Cell(currentRow, 1).Value = group.Key.ToUpper();
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Range(currentRow, 1, currentRow, headers.Length).Merge().Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
                currentRow++;

                int categoryStartRow = currentRow;

                foreach (var item in group)
                {
                    worksheet.Cell(currentRow, 1).Value = item.Category?.Code ?? "";
                    worksheet.Cell(currentRow, 2).Value = item.Description;
                    worksheet.Cell(currentRow, 3).Value = item.Unit;
                    worksheet.Cell(currentRow, 4).Value = item.Quantity;
                    worksheet.Cell(currentRow, 5).Value = item.UnitCost;
                    worksheet.Cell(currentRow, 6).Value = item.Frequency;

                    // Formule dynamique pour le coût total : Quantité * Coût Unit * Fréquence
                    worksheet.Cell(currentRow, 7).FormulaA1 = $"D{currentRow}*E{currentRow}*F{currentRow}";

                    worksheet.Cell(currentRow, 8).Value = item.FundingAssociation;
                    worksheet.Cell(currentRow, 9).Value = item.FundingCofinancing;
                    worksheet.Cell(currentRow, 10).Value = item.FundingRequested;

                    // Formatage monétaire
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00 €";
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00 €";
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00 €";
                    worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00 €";
                    worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00 €";

                    currentRow++;
                }

                // Ligne de sous-total de la catégorie
                int categoryEndRow = currentRow - 1;
                if (categoryEndRow >= categoryStartRow)
                {
                    worksheet.Cell(currentRow, 2).Value = $"Sous-total {group.Key}";
                    worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

                    // Formules de somme
                    worksheet.Cell(currentRow, 7).FormulaA1 = $"SUM(G{categoryStartRow}:G{categoryEndRow})";
                    worksheet.Cell(currentRow, 8).FormulaA1 = $"SUM(H{categoryStartRow}:H{categoryEndRow})";
                    worksheet.Cell(currentRow, 9).FormulaA1 = $"SUM(I{categoryStartRow}:I{categoryEndRow})";
                    worksheet.Cell(currentRow, 10).FormulaA1 = $"SUM(J{categoryStartRow}:J{categoryEndRow})";

                    worksheet.Range(currentRow, 1, currentRow, headers.Length).Style.Font.Bold = true;
                    worksheet.Range(currentRow, 1, currentRow, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00 €";
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00 €";
                    worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00 €";
                    worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00 €";
                    
                    currentRow += 2; // Laisser une ligne d'espace
                }
            }

            // Ligne de Total Général
            currentRow++;
            worksheet.Cell(currentRow, 2).Value = "TOTAL GÉNÉRAL DU BUDGET";
            worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Style.Font.FontSize = 12;

            // Formules globales basées sur les cellules de sous-totaux ou tout simplement la somme de toutes les lignes individuelles
            worksheet.Cell(currentRow, 7).FormulaA1 = $"SUMIF(B{startRow + 1}:B{currentRow - 2}, \"Sous-total*\", G{startRow + 1}:G{currentRow - 2})";
            worksheet.Cell(currentRow, 8).FormulaA1 = $"SUMIF(B{startRow + 1}:B{currentRow - 2}, \"Sous-total*\", H{startRow + 1}:H{currentRow - 2})";
            worksheet.Cell(currentRow, 9).FormulaA1 = $"SUMIF(B{startRow + 1}:B{currentRow - 2}, \"Sous-total*\", I{startRow + 1}:I{currentRow - 2})";
            worksheet.Cell(currentRow, 10).FormulaA1 = $"SUMIF(B{startRow + 1}:B{currentRow - 2}, \"Sous-total*\", J{startRow + 1}:J{currentRow - 2})";

            var totalRange = worksheet.Range(currentRow, 1, currentRow, headers.Length);
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            totalRange.Style.Font.FontColor = XLColor.White;
            worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00 €";
            worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00 €";
            worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00 €";
            worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00 €";

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        /// <summary>
        /// Rubriques « Zone d'intervention » et « Bénéficiaires » du dossier, rédigées à partir du profil territorial
        /// et des groupes de bénéficiaires (ADR 25). Vide si rien n'est renseigné.
        /// </summary>
        public static List<(string Heading, string Body)> BuildTerritorySections(AppDbContext db, int projectId)
        {
            var sections = new List<(string, string)>();
            var t = db.TerritoryProfiles.AsNoTracking().FirstOrDefault(x => x.ProjectId == projectId);
            var groups = db.BeneficiaryGroups.AsNoTracking().Where(b => b.ProjectId == projectId).ToList();

            if (t != null && (t.HasLocation || t.FilledDiagnosticSections > 0))
            {
                var sb = new System.Text.StringBuilder();
                if (t.HasLocation) sb.AppendLine($"Localisation : {t.ZoneLabel}{(string.IsNullOrWhiteSpace(t.AreaType) ? "" : $" ({t.AreaType})")}.");
                if (!string.IsNullOrWhiteSpace(t.Localities)) sb.AppendLine($"Sites d'intervention : {t.Localities}");
                if (!string.IsNullOrWhiteSpace(t.Synthesis)) sb.AppendLine().AppendLine(t.Synthesis);
                else
                {
                    void Line(string label, string value) { if (!string.IsNullOrWhiteSpace(value)) sb.AppendLine($"{label} : {value}"); }
                    Line("Milieu naturel", string.Join(" ; ", new[] { t.Climate, t.Ecosystems, t.NaturalResources, t.WaterResources }.Where(s => !string.IsNullOrWhiteSpace(s))));
                    Line("Population", string.Join(" ; ", new[] { t.Population, t.Households, t.PovertyRate }.Where(s => !string.IsNullOrWhiteSpace(s))));
                    Line("Activités économiques", t.MainLivelihoods);
                    Line("Accès aux services", t.InfrastructureAccess);
                }
                sections.Add(("Présentation de la zone d'intervention :", sb.ToString().Trim()));

                var issues = new List<string>();
                void Issues(string label, string value)
                {
                    var items = Research.TerritoryService.SplitItems(value).ToList();
                    if (items.Any()) issues.Add($"{label} :\n" + string.Join("\n", items.Select(i => "• " + i)));
                }
                Issues("Problèmes environnementaux", t.EnvironmentalIssues);
                Issues("Problèmes sociaux", t.SocialIssues);
                Issues("Problèmes économiques", t.EconomicIssues);
                Issues("Vulnérabilités climatiques", t.ClimateVulnerabilities);
                if (issues.Any()) sections.Add(("Problèmes et vulnérabilités du territoire :", string.Join("\n\n", issues)));
                if (!string.IsNullOrWhiteSpace(t.Sources)) sections.Add(("Sources du diagnostic territorial :", t.Sources));
            }

            string DescribeGroup(BeneficiaryGroup g) =>
                $"• {g.Name} — {g.CountLabel}" +
                (g.WomenPercent.HasValue ? $", dont {g.WomenPercent} % de femmes" : "") +
                (g.YouthPercent.HasValue ? $", {g.YouthPercent} % de jeunes" : "") +
                (string.IsNullOrWhiteSpace(g.ExpectedBenefits) ? "" : $". Bénéfices attendus : {g.ExpectedBenefits}") +
                (string.IsNullOrWhiteSpace(g.SelectionCriteria) ? "" : $". Critères de sélection : {g.SelectionCriteria}");

            if (groups.Any())
            {
                var totals = Research.TerritoryService.ComputeTotals(groups);
                var direct = groups.Where(g => g.IsDirect).ToList();
                var indirect = groups.Where(g => !g.IsDirect).ToList();
                var body = new System.Text.StringBuilder(totals.Summary + ".");
                if (direct.Any()) body.AppendLine().AppendLine().AppendLine("Bénéficiaires directs :").AppendLine(string.Join("\n", direct.Select(DescribeGroup)));
                if (indirect.Any()) body.AppendLine().AppendLine("Bénéficiaires indirects :").AppendLine(string.Join("\n", indirect.Select(DescribeGroup)));
                sections.Add(("Bénéficiaires du projet :", body.ToString().Trim()));
            }
            return sections;
        }

        public static void ExportProjectToWord(int projectId, string filePath)
        {
            using var db = new AppDbContext();
            var project = db.Projects
                .Include(p => p.Objectives)
                    .ThenInclude(o => o.Results)
                        .ThenInclude(r => r.Activities)
                .FirstOrDefault(p => p.Id == projectId);
                
            if (project == null) throw new Exception("Projet introuvable.");

            using var doc = DocX.Create(filePath);

            // Titre Principal / Page de garde
            var titlePara = doc.InsertParagraph();
            titlePara.AppendLine("PROPOSITION DE PROJET DE DÉVELOPPEMENT").FontSize(22).Bold().Font(new Xceed.Document.NET.Font("Segoe UI")).Color(Xceed.Drawing.Color.Black);
            titlePara.AppendLine(project.Title.ToUpper()).FontSize(18).Bold().Color(Xceed.Drawing.Color.Blue);
            titlePara.Alignment = Xceed.Document.NET.Alignment.center;
            titlePara.SpacingBefore(100);
            titlePara.SpacingAfter(50);

            // Infos d'organisation
            var orgPara = doc.InsertParagraph();
            orgPara.AppendLine($"Porteur du projet : {project.LeadOrganization}").FontSize(12).Bold();
            orgPara.AppendLine($"Zone d'intervention : {project.InterventionZone}").FontSize(11);
            orgPara.AppendLine($"Durée : du {project.StartDate?.ToString("dd/MM/yyyy") ?? "N/A"} au {project.EndDate?.ToString("dd/MM/yyyy") ?? "N/A"}").FontSize(11);
            orgPara.AppendLine($"Budget Global : {project.TotalBudget:N2} {project.Currency}").FontSize(11);
            orgPara.SpacingAfter(200);
            orgPara.Alignment = Xceed.Document.NET.Alignment.center;

            doc.InsertSectionPageBreak();

            // Section 1 : Contexte et Justification
            doc.InsertParagraph("1. CONTEXTE ET JUSTIFICATION DU PROJET").FontSize(14).Bold().Color(Xceed.Drawing.Color.Black).SpacingAfter(15);
            
            doc.InsertParagraph("Contexte général :").FontSize(12).Bold().SpacingAfter(5);
            doc.InsertParagraph(string.IsNullOrWhiteSpace(project.Background) ? "Non renseigné." : project.Background).SpacingAfter(15);

            doc.InsertParagraph("Justification du besoin :").FontSize(12).Bold().SpacingAfter(5);
            doc.InsertParagraph(string.IsNullOrWhiteSpace(project.Justification) ? "Non renseigné." : project.Justification).SpacingAfter(15);

            // Zone d'intervention et bénéficiaires (ADR 25)
            foreach (var (heading, body) in BuildTerritorySections(db, projectId))
            {
                doc.InsertParagraph(heading).FontSize(12).Bold().SpacingAfter(5);
                doc.InsertParagraph(body).SpacingAfter(15);
            }

            doc.InsertParagraph("Forces organisationnelles et capacités de l'association :").FontSize(12).Bold().SpacingAfter(5);
            doc.InsertParagraph(string.IsNullOrWhiteSpace(project.OrganizationalStrengths) ? "Non renseigné." : project.OrganizationalStrengths).SpacingAfter(25);

            // Section 2 : Objectifs et Stratégie d'intervention
            doc.InsertParagraph("2. OBJECTIFS ET STRATÉGIE D'INTERVENTION").FontSize(14).Bold().Color(Xceed.Drawing.Color.Black).SpacingAfter(15);
            
            doc.InsertParagraph("Objectif général :").FontSize(12).Bold().SpacingAfter(5);
            doc.InsertParagraph(string.IsNullOrWhiteSpace(project.GeneralObjective) ? "Non renseigné." : project.GeneralObjective).SpacingAfter(15);

            doc.InsertParagraph("Approche et Méthodologie du projet :").FontSize(12).Bold().SpacingAfter(5);
            doc.InsertParagraph(string.IsNullOrWhiteSpace(project.ProjectApproach) ? "Non renseigné." : project.ProjectApproach).SpacingAfter(15);

            doc.InsertParagraph("Impacts attendus à long terme :").FontSize(12).Bold().SpacingAfter(5);
            doc.InsertParagraph(string.IsNullOrWhiteSpace(project.ProjectImpacts) ? "Non renseigné." : project.ProjectImpacts).SpacingAfter(15);

            doc.InsertParagraph("Alignement avec les stratégies nationales et internationales :").FontSize(12).Bold().SpacingAfter(5);
            doc.InsertParagraph(string.IsNullOrWhiteSpace(project.StrategicAlignment) ? "Non renseigné." : project.StrategicAlignment).SpacingAfter(15);

            doc.InsertParagraph("Pérennité et durabilité à long terme :").FontSize(12).Bold().SpacingAfter(5);
            doc.InsertParagraph(string.IsNullOrWhiteSpace(project.Sustainability) ? "Non renseigné." : project.Sustainability).SpacingAfter(15);

            doc.InsertParagraph("Analyse SWOT :").FontSize(12).Bold().SpacingAfter(10);
            var swotTable = doc.AddTable(2, 2);
            swotTable.Alignment = Xceed.Document.NET.Alignment.center;
            
            swotTable.Rows[0].Cells[0].Paragraphs.First().Append("FORCES (Internes)\n").Bold().AppendLine().Append(string.IsNullOrWhiteSpace(project.SwotStrengths) ? "-" : project.SwotStrengths);
            swotTable.Rows[0].Cells[1].Paragraphs.First().Append("FAIBLESSES (Internes)\n").Bold().AppendLine().Append(string.IsNullOrWhiteSpace(project.SwotWeaknesses) ? "-" : project.SwotWeaknesses);
            swotTable.Rows[1].Cells[0].Paragraphs.First().Append("OPPORTUNITÉS (Externes)\n").Bold().AppendLine().Append(string.IsNullOrWhiteSpace(project.SwotOpportunities) ? "-" : project.SwotOpportunities);
            swotTable.Rows[1].Cells[1].Paragraphs.First().Append("MENACES (Externes)\n").Bold().AppendLine().Append(string.IsNullOrWhiteSpace(project.SwotThreats) ? "-" : project.SwotThreats);
            
            doc.InsertTable(swotTable);
            doc.InsertParagraph().SpacingAfter(20);

            doc.InsertParagraph("Objectifs spécifiques :").FontSize(12).Bold().SpacingAfter(10);
            if (project.Objectives.Any())
            {
                foreach (var obj in project.Objectives)
                {
                    doc.InsertParagraph($"• {obj.Code} : {obj.Description}").SpacingAfter(8);
                }
            }
            else
            {
                doc.InsertParagraph("Aucun objectif spécifique configuré.").Italic();
            }

            doc.InsertParagraph().SpacingAfter(25);

            // Section 3 : Cadre Logique (Tableau structuré)
            doc.InsertParagraph("3. CADRE LOGIQUE DU PROJET").FontSize(14).Bold().Color(Xceed.Drawing.Color.Black).SpacingAfter(15);

            // Créer le tableau Word
            int totalRows = 1; // En-tête
            foreach (var obj in project.Objectives)
            {
                totalRows++; // Pour l'objectif lui-même
                foreach (var res in obj.Results)
                {
                    totalRows++; // Pour le résultat
                    totalRows += res.Activities.Count; // Pour les activités
                }
            }

            var table = doc.AddTable(totalRows, 3);
            table.Alignment = Xceed.Document.NET.Alignment.center;

            // En-tête du tableau
            table.Rows[0].Cells[0].Paragraphs.First().Append("Code").Bold();
            table.Rows[0].Cells[1].Paragraphs.First().Append("Logique d'intervention").Bold();
            table.Rows[0].Cells[2].Paragraphs.First().Append("Indicateurs & Sources").Bold();
            foreach (var cell in table.Rows[0].Cells)
            {
                cell.FillColor = Xceed.Drawing.Color.LightGray;
            }

            int tableRowIndex = 1;
            foreach (var obj in project.Objectives)
            {
                table.Rows[tableRowIndex].Cells[0].Paragraphs.First().Append(obj.Code).Bold();
                table.Rows[tableRowIndex].Cells[1].Paragraphs.First().Append($"Objectif Spécifique : {obj.Description}").Bold();
                table.Rows[tableRowIndex].Cells[2].Paragraphs.First().Append("-");
                foreach (var cell in table.Rows[tableRowIndex].Cells)
                {
                    cell.FillColor = Xceed.Drawing.Color.LightBlue;
                }
                tableRowIndex++;

                foreach (var res in obj.Results)
                {
                    table.Rows[tableRowIndex].Cells[0].Paragraphs.First().Append(res.Code).Bold();
                    table.Rows[tableRowIndex].Cells[1].Paragraphs.First().Append($"Résultat attendu : {res.Description}");
                    table.Rows[tableRowIndex].Cells[2].Paragraphs.First().Append("-");
                    foreach (var cell in table.Rows[tableRowIndex].Cells)
                    {
                        cell.FillColor = Xceed.Drawing.Color.LightYellow;
                    }
                    tableRowIndex++;

                    foreach (var act in res.Activities)
                    {
                        table.Rows[tableRowIndex].Cells[0].Paragraphs.First().Append(act.Code);
                        table.Rows[tableRowIndex].Cells[1].Paragraphs.First().Append($"Activité : {act.Description}");

                        // Charger les indicateurs associés à l'activité
                        var indicators = db.Indicators.Where(i => i.ActivityId == act.Id).ToList();
                        var cellPara = table.Rows[tableRowIndex].Cells[2].Paragraphs.First();
                        if (indicators.Any())
                        {
                            foreach (var ind in indicators)
                            {
                                cellPara.AppendLine($"{ind.Code} : {ind.Description} (Cible: {ind.Target})").FontSize(10);
                            }
                        }
                        else
                        {
                            cellPara.Append("Aucun indicateur.");
                        }

                        tableRowIndex++;
                    }
                }
            }

            doc.InsertTable(table);
            doc.Save();
        }

        public static void ExportProjectToPdf(int projectId, string filePath)
        {
            using var db = new AppDbContext();
            var project = db.Projects
                .Include(p => p.Objectives)
                    .ThenInclude(o => o.Results)
                        .ThenInclude(r => r.Activities)
                .FirstOrDefault(p => p.Id == projectId);

            if (project == null) throw new Exception("Projet introuvable.");

            var budgetItems = db.BudgetItems
                .Where(b => b.ProjectId == projectId)
                .Include(b => b.Category)
                .ToList();
            var territorySections = BuildTerritorySections(db, projectId);

            // Génération PDF via QuestPDF
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily(SettingsService.GetSetting("Font_Base", "Arial")).FontSize(10));

                    // En-tête de page
                    page.Header()
                        .Text($"AFMI - PROPOSITION DE PROJET : {project.Title.ToUpper()}")
                        .FontSize(9)
                        .Italic()
                        .FontColor(Colors.Grey.Medium);

                    // Contenu principal
                    page.Content().Column(column =>
                    {
                        column.Spacing(15);

                        // Titre du projet
                        column.Item().Text(project.Title)
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        // Métadonnées
                        column.Item().Text(text =>
                        {
                            text.Span("Porteur de projet : ").Bold();
                            text.Span($"{project.LeadOrganization}\n");
                            text.Span("Zone d'intervention : ").Bold();
                            text.Span($"{project.InterventionZone}\n");
                            text.Span("Budget global : ").Bold();
                            text.Span($"{project.TotalBudget:N2} {project.Currency}\n");
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Section 1 : Contexte et Justification
                        column.Item().Text("1. CONTEXTE ET JUSTIFICATION")
                            .FontSize(14)
                            .Bold()
                            .FontColor(Colors.Blue.Darken1);

                        column.Item().Text("Contexte général :").Bold().FontSize(11);
                        column.Item().Text(string.IsNullOrWhiteSpace(project.Background) ? "Non renseigné." : project.Background);

                        column.Item().Text("Justification du besoin :").Bold().FontSize(11);
                        column.Item().Text(string.IsNullOrWhiteSpace(project.Justification) ? "Non renseigné." : project.Justification);

                        foreach (var (heading, body) in territorySections)
                        {
                            column.Item().Text(heading).Bold().FontSize(11);
                            column.Item().Text(body);
                        }

                        column.Item().Text("Forces organisationnelles de l'association :").Bold().FontSize(11);
                        column.Item().Text(string.IsNullOrWhiteSpace(project.OrganizationalStrengths) ? "Non renseigné." : project.OrganizationalStrengths);

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Section 2 : Stratégie d'intervention et Objectifs
                        column.Item().Text("2. STRATÉGIE D'INTERVENTION ET OBJECTIFS")
                            .FontSize(14)
                            .Bold()
                            .FontColor(Colors.Blue.Darken1);

                        column.Item().Text(text =>
                        {
                            text.Span("Objectif Général : ").Bold();
                            text.Span($"{project.GeneralObjective}\n\n");
                        });

                        column.Item().Text("Approche et Méthodologie :").Bold().FontSize(11);
                        column.Item().Text(string.IsNullOrWhiteSpace(project.ProjectApproach) ? "Non renseigné." : project.ProjectApproach);

                        column.Item().Text("Impacts à long terme :").Bold().FontSize(11);
                        column.Item().Text(string.IsNullOrWhiteSpace(project.ProjectImpacts) ? "Non renseigné." : project.ProjectImpacts);

                        column.Item().Text("Alignement Stratégique (Politiques & ODD) :").Bold().FontSize(11);
                        column.Item().Text(string.IsNullOrWhiteSpace(project.StrategicAlignment) ? "Non renseigné." : project.StrategicAlignment);

                        column.Item().Text("Pérennité & Durabilité :").Bold().FontSize(11);
                        column.Item().Text(string.IsNullOrWhiteSpace(project.Sustainability) ? "Non renseigné." : project.Sustainability);

                        column.Item().Text("Analyse SWOT :").Bold().FontSize(11);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Background(Colors.Green.Lighten5).Padding(8).Column(c =>
                            {
                                c.Item().Text("FORCES (Internes)").Bold().FontColor(Colors.Green.Darken2).FontSize(9);
                                c.Item().Text(string.IsNullOrWhiteSpace(project.SwotStrengths) ? "-" : project.SwotStrengths).FontSize(8);
                            });

                            table.Cell().Background(Colors.Red.Lighten5).Padding(8).Column(c =>
                            {
                                c.Item().Text("FAIBLESSES (Internes)").Bold().FontColor(Colors.Red.Darken2).FontSize(9);
                                c.Item().Text(string.IsNullOrWhiteSpace(project.SwotWeaknesses) ? "-" : project.SwotWeaknesses).FontSize(8);
                            });

                            table.Cell().Background(Colors.Blue.Lighten5).Padding(8).Column(c =>
                            {
                                c.Item().Text("OPPORTUNITÉS (Externes)").Bold().FontColor(Colors.Blue.Darken2).FontSize(9);
                                c.Item().Text(string.IsNullOrWhiteSpace(project.SwotOpportunities) ? "-" : project.SwotOpportunities).FontSize(8);
                            });

                            table.Cell().Background(Colors.Orange.Lighten5).Padding(8).Column(c =>
                            {
                                c.Item().Text("MENACES (Externes)").Bold().FontColor(Colors.Orange.Darken2).FontSize(9);
                                c.Item().Text(string.IsNullOrWhiteSpace(project.SwotThreats) ? "-" : project.SwotThreats).FontSize(8);
                            });
                        });

                        column.Item().Text("Objectifs Spécifiques :").Bold().FontSize(11);
                        foreach (var obj in project.Objectives)
                        {
                            column.Item().Text(text =>
                            {
                                text.Span($"{obj.Code} : ").Bold().FontColor(Colors.Blue.Medium);
                                text.Span($"{obj.Description}");
                            });
                        }

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Section 3 : Budget simplifié
                        column.Item().Text("3. Synthèse Budgétaire")
                            .FontSize(14)
                            .Bold()
                            .FontColor(Colors.Blue.Darken1);

                        // Tableau de budget
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Rubrique
                                columns.RelativeColumn(1); // Total
                                columns.RelativeColumn(1); // Demandé
                            });

                            // En-tête
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Rubrique").Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Coût Total").Bold().FontColor(Colors.White);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Part Demandée").Bold().FontColor(Colors.White);
                            });

                            // Lignes de budget
                            var categories = budgetItems.GroupBy(b => b.Category?.Name ?? "Autres");
                            foreach (var cat in categories)
                            {
                                decimal catTotal = cat.Sum(item => item.TotalCost);
                                decimal catRequested = cat.Sum(item => item.FundingRequested);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(cat.Key);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{catTotal:N2} €");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{catRequested:N2} €");
                            }

                            // Total Général
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("TOTAL GÉNÉRAL").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text($"{project.TotalBudget:N2} €").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text($"{budgetItems.Sum(i => i.FundingRequested):N2} €").Bold();
                        });
                    });

                    // Pied de page de page
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" sur ");
                            x.TotalPages();
                        });
                });
            })
            .GeneratePdf(filePath);
        }

        public static void ExportStrategicPlanningToWord(int projectId, string filePath)
        {
            using var db = new AppDbContext();
            var project = db.Projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null) throw new Exception("Projet introuvable.");

            var foundation = db.StrategicFoundations
                .Include(f => f.DataSources)
                .Include(f => f.ContextIndicators)
                .Include(f => f.Stakeholders)
                .Include(f => f.SwotItems)
                .Include(f => f.PestelItems)
                .Include(f => f.NeedsAssessments)
                .Include(f => f.StrategicOptions)
                .Include(f => f.TheoryOfChangeNodes)
                .Include(f => f.StrategicObjectives)
                .Include(f => f.Risks)
                .Include(f => f.Assumptions)
                .FirstOrDefault(f => f.ProjectId == projectId && !f.IsDeleted);

            if (foundation == null) throw new Exception("Socle stratégique introuvable pour ce projet.");

            using var doc = DocX.Create(filePath);

            // Page de garde
            var titlePara = doc.InsertParagraph();
            titlePara.AppendLine("CADRAGE STRATÉGIQUE ET ANALYSE DES DONNÉES").FontSize(22).Bold().Font(new Xceed.Document.NET.Font("Segoe UI")).Color(Xceed.Drawing.Color.Black);
            titlePara.AppendLine("GESTION AXÉE SUR LES RÉSULTATS (RBM) / CADRE LOGIQUE").FontSize(14).Color(Xceed.Drawing.Color.DarkSlateGray);
            titlePara.AppendLine((foundation.IdeaTitle ?? project.Title).ToUpper()).FontSize(18).Bold().Color(Xceed.Drawing.Color.Blue);
            titlePara.Alignment = Xceed.Document.NET.Alignment.center;
            titlePara.SpacingBefore(80);
            titlePara.SpacingAfter(40);

            var metaPara = doc.InsertParagraph();
            metaPara.AppendLine($"Zone d'intervention : {foundation.TargetZone}").FontSize(11);
            metaPara.AppendLine($"Secteur : {foundation.Sector}").FontSize(11);
            metaPara.AppendLine($"Bénéficiaires cibles : {foundation.TargetBeneficiaries}").FontSize(11);
            metaPara.AppendLine($"Bailleur pressenti : {foundation.PotentialDonor} (Priorités : {foundation.DonorPriorities})").FontSize(11);
            metaPara.AppendLine($"Budget estimatif plafond : {foundation.EstimatedBudget:N0} {foundation.Currency}").FontSize(11);
            metaPara.AppendLine($"Score de complétude : {foundation.CompletenessScore}%").FontSize(11).Bold();
            metaPara.Alignment = Xceed.Document.NET.Alignment.center;
            metaPara.SpacingAfter(150);

            doc.InsertSectionPageBreak();

            // 1. Problème Central et Diagnostic
            doc.InsertParagraph("1. PROBLÈME CENTRAL ET JUSTIFICATION DES BESOINS").FontSize(14).Bold().SpacingAfter(10);
            doc.InsertParagraph(foundation.CoreProblemStatement).SpacingAfter(10);
            if (!string.IsNullOrWhiteSpace(foundation.ProblemEvidenceData))
            {
                doc.InsertParagraph($"Données probantes et évidences : {foundation.ProblemEvidenceData}").Italic().SpacingAfter(15);
            }

            // Besoins
            if (foundation.NeedsAssessments.Any(n => !n.IsDeleted))
            {
                doc.InsertParagraph("Besoins prioritaires identifiés :").FontSize(12).Bold().SpacingAfter(5);
                foreach (var need in foundation.NeedsAssessments.Where(n => !n.IsDeleted).OrderByDescending(n => n.WeightedScore))
                {
                    doc.InsertParagraph($"• {need.NeedDescription} (Public: {need.TargetGroupAffected}, Score pondéré: {need.WeightedScore:F2}/5)").SpacingAfter(5);
                }
                doc.InsertParagraph().SpacingAfter(15);
            }

            // 2. Analyse des Parties Prenantes
            doc.InsertParagraph("2. ANALYSE DES PARTIES PRENANTES").FontSize(14).Bold().SpacingAfter(10);
            if (foundation.Stakeholders.Any(s => !s.IsDeleted))
            {
                var stTable = doc.AddTable(foundation.Stakeholders.Count(s => !s.IsDeleted) + 1, 4);
                stTable.Rows[0].Cells[0].Paragraphs.First().Append("Partie Prenante").Bold();
                stTable.Rows[0].Cells[1].Paragraphs.First().Append("Rôle / Type").Bold();
                stTable.Rows[0].Cells[2].Paragraphs.First().Append("Pouvoir / Intérêt").Bold();
                stTable.Rows[0].Cells[3].Paragraphs.First().Append("Stratégie").Bold();

                int r = 1;
                foreach (var s in foundation.Stakeholders.Where(x => !x.IsDeleted))
                {
                    stTable.Rows[r].Cells[0].Paragraphs.First().Append(s.Name);
                    stTable.Rows[r].Cells[1].Paragraphs.First().Append(s.Role);
                    stTable.Rows[r].Cells[2].Paragraphs.First().Append($"P:{s.PowerLevel}/5, I:{s.InterestLevel}/5");
                    stTable.Rows[r].Cells[3].Paragraphs.First().Append(s.EngagementStrategy);
                    r++;
                }
                doc.InsertTable(stTable);
                doc.InsertParagraph().SpacingAfter(20);
            }

            // 3. Théorie du Changement (ToC)
            doc.InsertParagraph("3. THÉORIE DU CHANGEMENT ET CHAÎNE CAUSALE").FontSize(14).Bold().SpacingAfter(10);
            if (foundation.TheoryOfChangeNodes.Any(t => !t.IsDeleted))
            {
                foreach (var node in foundation.TheoryOfChangeNodes.Where(t => !t.IsDeleted).OrderBy(t => t.OrderIndex))
                {
                    doc.InsertParagraph($"[{node.NodeType}] {node.Title}").Bold();
                    if (!string.IsNullOrWhiteSpace(node.Description)) doc.InsertParagraph(node.Description);
                    if (!string.IsNullOrWhiteSpace(node.LinkedAssumptions)) doc.InsertParagraph($"Hypothèse : {node.LinkedAssumptions}").Italic();
                    doc.InsertParagraph().SpacingAfter(5);
                }
            }

            // 4. Objectifs Stratégiques SMART
            doc.InsertParagraph("4. OBJECTIFS STRATÉGIQUES (SMART)").FontSize(14).Bold().SpacingAfter(10);
            if (!string.IsNullOrWhiteSpace(foundation.GeneralStrategicObjective))
            {
                doc.InsertParagraph($"Objectif Général : {foundation.GeneralStrategicObjective}").Bold().SpacingAfter(10);
            }
            if (foundation.StrategicObjectives.Any(o => !o.IsDeleted))
            {
                foreach (var obj in foundation.StrategicObjectives.Where(o => !o.IsDeleted))
                {
                    doc.InsertParagraph($"• [{obj.Code}] {obj.Title} - {obj.Description}").SpacingAfter(6);
                }
                doc.InsertParagraph().SpacingAfter(15);
            }

            // 5. Registre des Risques
            doc.InsertParagraph("5. GESTION DES RISQUES ET ATTÉNUATION").FontSize(14).Bold().SpacingAfter(10);
            if (foundation.Risks.Any(rk => !rk.IsDeleted))
            {
                var rkTable = doc.AddTable(foundation.Risks.Count(rk => !rk.IsDeleted) + 1, 5);
                rkTable.Rows[0].Cells[0].Paragraphs.First().Append("Code").Bold();
                rkTable.Rows[0].Cells[1].Paragraphs.First().Append("Description du risque").Bold();
                rkTable.Rows[0].Cells[2].Paragraphs.First().Append("Score").Bold();
                rkTable.Rows[0].Cells[3].Paragraphs.First().Append("Stratégie d'atténuation").Bold();
                rkTable.Rows[0].Cells[4].Paragraphs.First().Append("Responsable").Bold();

                int rowIdx = 1;
                foreach (var rk in foundation.Risks.Where(x => !x.IsDeleted))
                {
                    rkTable.Rows[rowIdx].Cells[0].Paragraphs.First().Append(rk.Code);
                    rkTable.Rows[rowIdx].Cells[1].Paragraphs.First().Append(rk.Description);
                    rkTable.Rows[rowIdx].Cells[2].Paragraphs.First().Append($"{rk.RiskScore}/25");
                    rkTable.Rows[rowIdx].Cells[3].Paragraphs.First().Append(rk.MitigationStrategy);
                    rkTable.Rows[rowIdx].Cells[4].Paragraphs.First().Append(rk.ResponsibleParty);
                    rowIdx++;
                }
                doc.InsertTable(rkTable);
            }

            doc.Save();
        }

        public static void ExportStrategicPlanningToPdf(int projectId, string filePath)
        {
            using var db = new AppDbContext();
            var project = db.Projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null) throw new Exception("Projet introuvable.");

            var foundation = db.StrategicFoundations
                .Include(f => f.ContextIndicators)
                .Include(f => f.Stakeholders)
                .Include(f => f.NeedsAssessments)
                .Include(f => f.TheoryOfChangeNodes)
                .Include(f => f.StrategicObjectives)
                .Include(f => f.Risks)
                .FirstOrDefault(f => f.ProjectId == projectId && !f.IsDeleted);

            if (foundation == null) throw new Exception("Socle stratégique introuvable pour ce projet.");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("AFMI MONTAGE PROJET").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text("SOCLE STRATÉGIQUE & GESTION AXÉE SUR LES RÉSULTATS (RBM)").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(100).AlignRight().Text($"Complétude: {foundation.CompletenessScore}%").Bold().FontColor(Colors.Green.Darken2);
                    });

                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        // Titre
                        col.Item().Background(Colors.Grey.Lighten4).Padding(12).Column(c =>
                        {
                            c.Item().Text(foundation.IdeaTitle ?? project.Title).FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                            c.Item().Text($"Zone: {foundation.TargetZone} | Secteur: {foundation.Sector} | Bailleur: {foundation.PotentialDonor}").FontSize(9);
                            c.Item().Text($"Budget estimatif: {foundation.EstimatedBudget:N0} {foundation.Currency}").FontSize(9).Bold();
                        });

                        col.Item().PaddingTop(15).Text("1. Problème Central & Évidences").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().PaddingTop(4).Text(foundation.CoreProblemStatement);
                        if (!string.IsNullOrWhiteSpace(foundation.ProblemEvidenceData))
                        {
                            col.Item().PaddingTop(2).Text($"Données probantes: {foundation.ProblemEvidenceData}").Italic().FontSize(9);
                        }

                        // Objectifs
                        col.Item().PaddingTop(15).Text("2. Objectifs Stratégiques (SMART)").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                        if (!string.IsNullOrWhiteSpace(foundation.GeneralStrategicObjective))
                        {
                            col.Item().PaddingTop(4).Text($"Objectif Général: {foundation.GeneralStrategicObjective}").Bold();
                        }
                        foreach (var obj in foundation.StrategicObjectives.Where(o => !o.IsDeleted))
                        {
                            col.Item().PaddingTop(2).Text($"• [{obj.Code}] {obj.Title} - {obj.Description}");
                        }

                        // Risques principaux
                        var activeRisks = foundation.Risks.Where(r => !r.IsDeleted).ToList();
                        if (activeRisks.Any())
                        {
                            col.Item().PaddingTop(15).Text("3. Registre des Risques & Atténuation").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().PaddingTop(5).Table(tbl =>
                            {
                                tbl.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn(3);
                                    columns.ConstantColumn(45);
                                    columns.RelativeColumn(3);
                                });

                                tbl.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Code").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Risque").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Score").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Atténuation").Bold();
                                });

                                foreach (var rk in activeRisks)
                                {
                                    tbl.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(rk.Code);
                                    tbl.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(rk.Description);
                                    tbl.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"{rk.RiskScore}/25");
                                    tbl.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(rk.MitigationStrategy);
                                }
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" sur ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf(filePath);
        }

        public static void ExportOrganizationProfileToWord(string filePath, AfmiProfile profile = null)
        {
            profile ??= ProjectRadarConfigService.LoadProfile();

            using var doc = DocX.Create(filePath);

            // Titre Principal
            var titlePara = doc.InsertParagraph();
            titlePara.AppendLine("FICHE D'IDENTITÉ & DÉCLARATION DE CAPACITÉ INSTITUTIONNELLE").FontSize(18).Bold().Color(Xceed.Drawing.Color.MidnightBlue);
            titlePara.AppendLine(profile.OrganizationName.ToUpper()).FontSize(14).Bold().Color(Xceed.Drawing.Color.DodgerBlue);
            titlePara.AppendLine($"Dossier de présentation institutionnelle pour bailleurs de fonds — Généré le {DateTime.Now:dd/MM/yyyy}").FontSize(10).Italic().Color(Xceed.Drawing.Color.Gray);
            titlePara.SpacingAfter(20);

            // 1. Identité & Statut Légal
            doc.InsertParagraph("1. IDENTITÉ LÉGALE & COORDONNÉES").FontSize(13).Bold().Color(Xceed.Drawing.Color.MidnightBlue).SpacingAfter(6);
            var idTable = doc.AddTable(6, 2);
            idTable.Rows[0].Cells[0].Paragraphs.First().Append("Nom officiel de l'organisation :").Bold();
            idTable.Rows[0].Cells[1].Paragraphs.First().Append($"{profile.OrganizationName} ({profile.Acronym})");
            idTable.Rows[1].Cells[0].Paragraphs.First().Append("Statut juridique & Récépissé :").Bold();
            idTable.Rows[1].Cells[1].Paragraphs.First().Append($"{profile.LegalStatus} — {profile.RegistrationNumber}");
            idTable.Rows[2].Cells[0].Paragraphs.First().Append("Date de création :").Bold();
            idTable.Rows[2].Cells[1].Paragraphs.First().Append(profile.CreationDate);
            idTable.Rows[3].Cells[0].Paragraphs.First().Append("Siège social & Adresse :").Bold();
            idTable.Rows[3].Cells[1].Paragraphs.First().Append($"{profile.Headquarters} | {profile.Address}");
            idTable.Rows[4].Cells[0].Paragraphs.First().Append("Contact & Site Web :").Bold();
            idTable.Rows[4].Cells[1].Paragraphs.First().Append($"Tél : {profile.Phone} | Email : {profile.Email} | {profile.Website}");
            idTable.Rows[5].Cells[0].Paragraphs.First().Append("Représentant légal :").Bold();
            idTable.Rows[5].Cells[1].Paragraphs.First().Append(profile.LegalRepresentative);
            doc.InsertTable(idTable);
            doc.InsertParagraph().SpacingAfter(15);

            // 2. Vision, Mission & Valeurs
            doc.InsertParagraph("2. VISION, MISSION & AXES STRATÉGIQUES").FontSize(13).Bold().Color(Xceed.Drawing.Color.MidnightBlue).SpacingAfter(6);
            doc.InsertParagraph("Mission institutionnelle :").FontSize(11).Bold();
            doc.InsertParagraph(profile.Mission).SpacingAfter(8);
            doc.InsertParagraph("Vision stratégique :").FontSize(11).Bold();
            doc.InsertParagraph(profile.Vision).SpacingAfter(8);
            doc.InsertParagraph("Valeurs et principes d'action :").FontSize(11).Bold();
            doc.InsertParagraph(profile.CoreValues).SpacingAfter(8);
            if (!string.IsNullOrWhiteSpace(profile.StrategicAxes))
            {
                doc.InsertParagraph("Les 5 Grands Axes Stratégiques d'Intervention (Plan AFMI) :").FontSize(11).Bold();
                doc.InsertParagraph(profile.StrategicAxes).SpacingAfter(8);
            }
            doc.InsertParagraph("Objectifs stratégiques :").FontSize(11).Bold();
            doc.InsertParagraph(profile.StrategicObjectives).SpacingAfter(15);

            // 3. Zones & Thématiques
            doc.InsertParagraph("3. ZONES D'INTERVENTION & DOMAINES D'ACTION").FontSize(13).Bold().Color(Xceed.Drawing.Color.MidnightBlue).SpacingAfter(6);
            doc.InsertParagraph($"Zones géographiques couvertes : {string.Join(", ", profile.GeographicZones)}").SpacingAfter(6);
            doc.InsertParagraph("Thématiques prioritaires :").FontSize(11).Bold();
            foreach (var th in profile.PreferredThemes)
            {
                doc.InsertParagraph($"  • {th}");
            }
            doc.InsertParagraph().SpacingAfter(15);

            // 4. Historique & Trajectoire
            doc.InsertParagraph("4. HISTORIQUE & TRAJECTOIRE DE L'ORGANISATION").FontSize(13).Bold().Color(Xceed.Drawing.Color.MidnightBlue).SpacingAfter(6);
            doc.InsertParagraph(profile.HistoryTrajectory).SpacingAfter(15);

            // 5. Réalisations & Bilan d'Impact
            doc.InsertParagraph("5. RÉALISATIONS PHARES & BILAN D'IMPACT (TRACK RECORD)").FontSize(13).Bold().Color(Xceed.Drawing.Color.MidnightBlue).SpacingAfter(6);
            doc.InsertParagraph(profile.KeyAchievements).SpacingAfter(8);
            doc.InsertParagraph($"Chiffres clés d'impact : {profile.ImpactFigures}").FontSize(10).Bold().Italic().SpacingAfter(8);
            if (!string.IsNullOrWhiteSpace(profile.RecognitionsAwards))
            {
                doc.InsertParagraph($"Reconnaissances et distinctions : {profile.RecognitionsAwards}").SpacingAfter(8);
            }
            doc.InsertParagraph().SpacingAfter(15);

            // 6. Capacités Organisationnelles
            doc.InsertParagraph("6. CAPACITÉS & RESSOURCES ORGANISATIONNELLES").FontSize(13).Bold().Color(Xceed.Drawing.Color.MidnightBlue).SpacingAfter(6);
            doc.InsertParagraph("Ressources humaines & Gouvernance :").FontSize(11).Bold();
            doc.InsertParagraph(profile.HumanResourcesCapacity).SpacingAfter(8);
            doc.InsertParagraph("Capacité de gestion financière et fiducière :").FontSize(11).Bold();
            doc.InsertParagraph(profile.FinancialManagementCapacity).SpacingAfter(8);
            doc.InsertParagraph("Moyens logistiques et équipements de terrain :").FontSize(11).Bold();
            doc.InsertParagraph(profile.LogisticalMaterialCapacity).SpacingAfter(15);

            // 7. Expérience Bailleurs
            doc.InsertParagraph("7. EXPÉRIENCE AVEC LES BAILLEURS DE FONDS & PARTENARIATS").FontSize(13).Bold().Color(Xceed.Drawing.Color.MidnightBlue).SpacingAfter(6);
            doc.InsertParagraph($"Bailleurs partenaires clés : {string.Join(", ", profile.PreferredDonors)}").SpacingAfter(8);
            doc.InsertParagraph("Références de subventions et projets menés :").FontSize(11).Bold();
            doc.InsertParagraph(profile.PastProjectsReferences).SpacingAfter(8);
            doc.InsertParagraph("Partenariats institutionnels :").FontSize(11).Bold();
            doc.InsertParagraph(profile.StrategicPartnerships).SpacingAfter(8);
            doc.InsertParagraph("Domaines d'expertise technique :").FontSize(11).Bold();
            doc.InsertParagraph(profile.TechnicalExpertiseSummary).SpacingAfter(15);

            // 8. Capacity Statement
            doc.InsertParagraph("8. DÉCLARATION SYNTHÉTIQUE DE CAPACITÉ DU DEMANDEUR").FontSize(13).Bold().Color(Xceed.Drawing.Color.MidnightBlue).SpacingAfter(6);
            var callout = doc.InsertParagraph();
            callout.AppendLine(profile.DonorCapacityStatement).Italic().FontSize(10.5);
            callout.SpacingAfter(20);

            doc.Save();
        }

        public static void ExportOrganizationProfileToPdf(string filePath, AfmiProfile profile = null)
        {
            profile ??= ProjectRadarConfigService.LoadProfile();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(titleCol =>
                            {
                                titleCol.Item().Text("FICHE INSTITUTIONNELLE & CAPACITÉS DU DEMANDEUR")
                                    .FontSize(15).Bold().FontColor(Colors.Blue.Darken3);
                                titleCol.Item().Text(profile.OrganizationName.ToUpper())
                                    .FontSize(12).Bold().FontColor(Colors.Blue.Darken1);
                                titleCol.Item().Text($"Dossier de présentation institutionnelle pour bailleurs de fonds • {DateTime.Now:dd/MM/yyyy}")
                                    .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                            });
                        });
                        col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Colors.Blue.Darken3);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // 1. Identité légale
                        col.Item().Text("1. IDENTITÉ LÉGALE & GOUVERNANCE").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingTop(3).Table(tbl =>
                        {
                            tbl.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(160);
                                columns.RelativeColumn();
                            });
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text("Nom & Acronyme :").Bold();
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text($"{profile.OrganizationName} ({profile.Acronym})");
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text("Statut & Agrément :").Bold();
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text($"{profile.LegalStatus} — {profile.RegistrationNumber}");
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text("Date de création & Siège :").Bold();
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text($"Créée en {profile.CreationDate} • Siège : {profile.Headquarters}");
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text("Coordonnées & Contact :").Bold();
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text($"Tél: {profile.Phone} | Email: {profile.Email} | {profile.Website}");
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text("Représentant légal :").Bold();
                            tbl.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(profile.LegalRepresentative);
                        });

                        // 2. Vision, Mission & Valeurs
                        col.Item().PaddingTop(10).Text("2. VISION, MISSION & AXES STRATÉGIQUES").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span("Mission : ").Bold();
                            text.Span(profile.Mission);
                        });
                        col.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span("Vision : ").Bold();
                            text.Span(profile.Vision);
                        });
                        if (!string.IsNullOrWhiteSpace(profile.StrategicAxes))
                        {
                            col.Item().PaddingTop(2).Text(text =>
                            {
                                text.Span("Les 5 Grands Axes Stratégiques AFMI : ").Bold();
                                text.Span(profile.StrategicAxes);
                            });
                        }
                        col.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span("Objectifs stratégiques : ").Bold();
                            text.Span(profile.StrategicObjectives);
                        });

                        // 3. Réalisations & Bilan d'impact
                        col.Item().PaddingTop(10).Text("3. RÉALISATIONS PHARES & BILAN D'IMPACT (TRACK RECORD)").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingTop(2).Text(profile.KeyAchievements);
                        col.Item().PaddingTop(3).Background(Colors.Grey.Lighten4).Padding(4).Text(text =>
                        {
                            text.Span("Chiffres clés d'impact : ").Bold();
                            text.Span(profile.ImpactFigures).Italic();
                        });

                        // 4. Capacités organisationnelles
                        col.Item().PaddingTop(10).Text("4. CAPACITÉS ORGANISATIONNELLES & GESTION FINANCIÈRE").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span("Ressources Humaines : ").Bold();
                            text.Span(profile.HumanResourcesCapacity);
                        });
                        col.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span("Capacité Fiduciaire & Audit : ").Bold();
                            text.Span(profile.FinancialManagementCapacity);
                        });

                        // 5. Expérience avec les Bailleurs
                        col.Item().PaddingTop(10).Text("5. EXPÉRIENCE BAILLEURS & PARTENARIATS").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span("Bailleurs partenaires : ").Bold();
                            text.Span(string.Join(", ", profile.PreferredDonors));
                        });
                        col.Item().PaddingTop(2).Text(profile.PastProjectsReferences);

                        // 6. Déclaration synthétique
                        col.Item().PaddingTop(10).Text("6. DÉCLARATION SYNTHÉTIQUE DE CAPACITÉ").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingTop(3).Border(1).BorderColor(Colors.Blue.Lighten3).Background(Colors.Blue.Lighten5).Padding(6).Text(profile.DonorCapacityStatement).Italic().FontSize(9);
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" sur ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf(filePath);
        }
    }
}


