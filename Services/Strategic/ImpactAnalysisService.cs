using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;

namespace AFMI_Montage_projet_2.Services.Strategic
{
    public class ImpactedItem
    {
        public string TargetEntityType { get; set; } = string.Empty;
        public int TargetEntityId { get; set; }
        public string TargetTitle { get; set; } = string.Empty;
        public string LinkType { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = "Synced";
        public bool IsOverridden => CurrentStatus == "Overridden";
        public string ActionDescription { get; set; } = string.Empty;
    }

    public class ImpactReport
    {
        public string SourceEntityType { get; set; } = string.Empty;
        public int SourceEntityId { get; set; }
        public string SourceTitle { get; set; } = string.Empty;
        public List<ImpactedItem> ImpactedItems { get; set; } = new();
        public int TotalImpactedCount => ImpactedItems.Count;
        public int ProtectedOverriddenCount => ImpactedItems.Count(i => i.IsOverridden);
        public int StaleToBeMarkedCount => ImpactedItems.Count(i => !i.IsOverridden);
        public string SummaryText => $"تعديل هذا العنصر سيؤثر على {TotalImpactedCount} عنصر لاحق (سيتم وسم {StaleToBeMarkedCount} بحالة 'يحتاج مراجعة'، بينما {ProtectedOverriddenCount} محمي بتعديل يدوي سابق).";
    }

    public class ImpactAnalysisService
    {
        private static ImpactAnalysisService? _instance;
        public static ImpactAnalysisService Instance => _instance ??= new ImpactAnalysisService();

        private readonly Func<AppDbContext> _dbFactory;

        public ImpactAnalysisService()
        {
            _dbFactory = () => new AppDbContext();
        }

        public ImpactAnalysisService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// حساب الأثر المترتب على تعديل عنصر استراتيجي واكتشاف كافة العناصر اللاحقة المرتبطة به
        /// </summary>
        public async Task<ImpactReport> AnalyzeImpactAsync(int projectId, string sourceEntityType, int sourceEntityId, string sourceTitle = "")
        {
            using var db = _dbFactory();
            var report = new ImpactReport
            {
                SourceEntityType = sourceEntityType,
                SourceEntityId = sourceEntityId,
                SourceTitle = sourceTitle
            };

            // 1. استخراج الروابط المباشرة في جدول StrategicLinks
            var directLinks = await db.StrategicLinks
                .Where(l => l.ProjectId == projectId &&
                            l.SourceEntityType == sourceEntityType &&
                            l.SourceEntityId == sourceEntityId &&
                            !l.IsDeleted)
                .ToListAsync();

            foreach (var link in directLinks)
            {
                string targetTitle = await ResolveTargetTitleAsync(db, link.TargetEntityType, link.TargetEntityId);
                report.ImpactedItems.Add(new ImpactedItem
                {
                    TargetEntityType = link.TargetEntityType,
                    TargetEntityId = link.TargetEntityId,
                    TargetTitle = targetTitle,
                    LinkType = link.LinkType,
                    CurrentStatus = link.Status,
                    ActionDescription = link.Status == "Overridden" 
                        ? "محمي: تم تعديله يدوياً ولن يُستبدل تلقائياً" 
                        : "سيتم وسمه بـ 'يحتاج مراجعة' (Stale)"
                });

                // إذا كان الهدف هو Objective، استخراج نتائجه وأنشطته التابعة
                if (link.TargetEntityType == "Objective")
                {
                    var results = await db.Results
                        .Where(r => r.ObjectiveId == link.TargetEntityId)
                        .Include(r => r.Activities)
                        .ToListAsync();

                    foreach (var res in results)
                    {
                        report.ImpactedItems.Add(new ImpactedItem
                        {
                            TargetEntityType = "Result",
                            TargetEntityId = res.Id,
                            TargetTitle = res.Description,
                            LinkType = "CascadeDownstream",
                            CurrentStatus = link.Status,
                            ActionDescription = "سلسلة نتائج متأثرة بتعديل الهدف"
                        });

                        foreach (var act in res.Activities)
                        {
                            report.ImpactedItems.Add(new ImpactedItem
                            {
                                TargetEntityType = "Activity",
                                TargetEntityId = act.Id,
                                TargetTitle = $"{act.Code} - {act.Description}",
                                LinkType = "CascadeDownstream",
                                CurrentStatus = link.Status,
                                ActionDescription = "نشاط تنفيذي وميزانية تابعة متأثرة"
                            });
                        }
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// تطبيق وسم Stale على الروابط المتأثرة غير المحمية
        /// </summary>
        public async Task ApplyStaleStatusAsync(int projectId, string sourceEntityType, int sourceEntityId, string userEmail)
        {
            using var db = _dbFactory();
            var links = await db.StrategicLinks
                .Where(l => l.ProjectId == projectId &&
                            l.SourceEntityType == sourceEntityType &&
                            l.SourceEntityId == sourceEntityId &&
                            l.Status != "Overridden" &&
                            !l.IsDeleted)
                .ToListAsync();

            foreach (var link in links)
            {
                link.Status = "Stale";
                link.LastModifiedAt = DateTime.UtcNow;
                link.Version++;
            }

            if (links.Any())
            {
                await db.SaveChangesAsync();
                Security.AuditLogger.Log("تحديث حالة الأثر (Stale)", $"تم وسم {links.Count} روابط بحالة 'يحتاج مراجعة' بعد تعديل {sourceEntityType} #{sourceEntityId}", userEmail);
            }
        }

        private async Task<string> ResolveTargetTitleAsync(AppDbContext db, string entityType, int entityId)
        {
            try
            {
                switch (entityType)
                {
                    case "Objective":
                        var obj = await db.Objectives.FirstOrDefaultAsync(o => o.Id == entityId);
                        return obj != null ? $"{obj.Code}: {obj.Description}" : $"هدف #{entityId}";
                    case "Result":
                        var res = await db.Results.FirstOrDefaultAsync(r => r.Id == entityId);
                        return res?.Description ?? $"نتيجة #{entityId}";
                    case "Activity":
                        var act = await db.Activities.FirstOrDefaultAsync(a => a.Id == entityId);
                        return act != null ? $"{act.Code}: {act.Description}" : $"نشاط #{entityId}";
                    case "Indicator":
                        var ind = await db.Indicators.FirstOrDefaultAsync(i => i.Id == entityId);
                        return ind != null ? $"{ind.Code}: {ind.Description}" : $"مؤشر #{entityId}";
                    case "ProblemNode":
                        var p = await db.ProblemNodes.FirstOrDefaultAsync(n => n.Id == entityId);
                        return p?.Title ?? $"عقدة مشكلة #{entityId}";
                    case "BudgetItem":
                        var b = await db.BudgetItems.FirstOrDefaultAsync(x => x.Id == entityId);
                        return b?.Description ?? $"بند ميزانية #{entityId}";
                    default:
                        return $"{entityType} #{entityId}";
                }
            }
            catch
            {
                return $"{entityType} #{entityId}";
            }
        }
    }
}
