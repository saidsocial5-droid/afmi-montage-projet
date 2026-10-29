using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services.Collab
{
    public class MobileProjectDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long StartDate { get; set; }
        public long EndDate { get; set; }
        public double BudgetTotal { get; set; }
        public double BudgetSpent { get; set; }
        public int ProgressPercentage { get; set; }
        public long LastSyncAt { get; set; }
    }

    /// <summary>
    /// المصدّر الرسمي لملف sync/projects.json المخصص لتطبيق الهاتف (Android)
    /// يضمن المحافظة على التوافق التام مع تطبيق Kotlin/Compose/Room دون أي تغيير في بنية أو أسماء الحقول.
    /// </summary>
    public static class ProjectsJsonExporter
    {
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        public static List<MobileProjectDto> ConvertToMobileDtos(IEnumerable<Project> projects, DateTimeOffset? syncTimestamp = null)
        {
            long nowMillis = (syncTimestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();
            var list = new List<MobileProjectDto>();

            foreach (var p in projects)
            {
                long startMillis = p.StartDate.HasValue 
                    ? new DateTimeOffset(DateTime.SpecifyKind(p.StartDate.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds() 
                    : 0;

                long endMillis = p.EndDate.HasValue 
                    ? new DateTimeOffset(DateTime.SpecifyKind(p.EndDate.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds() 
                    : 0;

                list.Add(new MobileProjectDto
                {
                    Id = p.Id.ToString(),
                    Title = p.Title ?? string.Empty,
                    Description = p.GeneralObjective ?? string.Empty,
                    StartDate = startMillis,
                    EndDate = endMillis,
                    BudgetTotal = (double)p.TotalBudget,
                    BudgetSpent = 0.0,
                    ProgressPercentage = 0,
                    LastSyncAt = nowMillis
                });
            }

            return list;
        }

        public static string GenerateJson(IEnumerable<Project> projects, DateTimeOffset? syncTimestamp = null)
        {
            var dtos = ConvertToMobileDtos(projects, syncTimestamp);
            return JsonSerializer.Serialize(dtos, JsonOptions);
        }
    }
}
