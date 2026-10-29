using System;
using System.IO;
using System.Linq;
using System.Text;
using Xceed.Words.NET;
using ClosedXML.Excel;
using UglyToad.PdfPig;

namespace AFMI_Montage_projet_2.Services
{
    public static class DocumentReaderService
    {
        public static string ExtractText(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Le fichier spécifié n'existe pas.", filePath);

            string extension = Path.GetExtension(filePath).ToLower();

            switch (extension)
            {
                case ".docx":
                    return ExtractFromDocx(filePath);
                case ".xlsx":
                    return ExtractFromXlsx(filePath);
                case ".pdf":
                    return ExtractFromPdf(filePath);
                case ".txt":
                case ".md":
                case ".csv":
                    return File.ReadAllText(filePath);
                case ".html":
                case ".htm":
                    return Research.HtmlTextExtractor.Extract(File.ReadAllText(filePath)).Text;
                default:
                    throw new NotSupportedException($"Le format de fichier '{extension}' n'est pas supporté pour l'analyse.");
            }
        }

        private static string ExtractFromDocx(string filePath)
        {
            try
            {
                using var doc = DocX.Load(filePath);
                return doc.Text;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la lecture du fichier Word : {ex.Message}", ex);
            }
        }

        private static string ExtractFromXlsx(string filePath)
        {
            try
            {
                using var workbook = new XLWorkbook(filePath);
                var sb = new StringBuilder();
                foreach (var sheet in workbook.Worksheets)
                {
                    sb.AppendLine($"[Feuille: {sheet.Name}]");
                    foreach (var row in sheet.RowsUsed())
                    {
                        var cellsText = row.CellsUsed().Select(c => c.Value.ToString().Trim());
                        sb.AppendLine(string.Join(" | ", cellsText));
                    }
                    sb.AppendLine();
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la lecture du fichier Excel : {ex.Message}", ex);
            }
        }

        private static string ExtractFromPdf(string filePath)
        {
            try
            {
                using var pdf = PdfDocument.Open(filePath);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la lecture du fichier PDF : {ex.Message}", ex);
            }
        }
    }
}

