using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Security;

namespace AFMI_Montage_projet_2.Services.Strategic
{
    public class StrategicFoundationService
    {
        private static StrategicFoundationService? _instance;
        public static StrategicFoundationService Instance => _instance ??= new StrategicFoundationService();

        private readonly Func<AppDbContext> _dbFactory;

        public StrategicFoundationService()
        {
            _dbFactory = () => new AppDbContext();
        }

        public StrategicFoundationService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// تحميل الأساس الاستراتيجي للمشروع مع كافة الجداول والكيانات التابعة
        /// </summary>
        public async Task<StrategicFoundation> GetOrCreateFoundationAsync(int projectId, string userEmail = "Système")
        {
            using var db = _dbFactory();
            var foundation = await db.StrategicFoundations
                .Include(sf => sf.DataSources)
                .Include(sf => sf.ContextIndicators)
                .Include(sf => sf.Stakeholders)
                .Include(sf => sf.SwotItems)
                .Include(sf => sf.PestelItems)
                .Include(sf => sf.NeedsAssessments)
                .Include(sf => sf.StrategicOptions)
                .Include(sf => sf.TheoryOfChangeNodes)
                .Include(sf => sf.StrategicObjectives)
                .Include(sf => sf.Risks)
                .Include(sf => sf.Assumptions)
                .FirstOrDefaultAsync(sf => sf.ProjectId == projectId);

            if (foundation == null)
            {
                var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                foundation = new StrategicFoundation
                {
                    ProjectId = projectId,
                    IdeaTitle = project?.Title ?? string.Empty,
                    TargetZone = project?.InterventionZone ?? string.Empty,
                    EstimatedBudget = project?.TotalBudget ?? 0m,
                    Currency = project?.Currency ?? "EUR",
                    CoreProblemStatement = project?.Justification ?? string.Empty,
                    GeneralStrategicObjective = project?.GeneralObjective ?? string.Empty,
                    CreatedBy = userEmail,
                    CreatedAt = DateTime.UtcNow
                };

                db.StrategicFoundations.Add(foundation);
                await db.SaveChangesAsync();

                AuditLogger.Log("إنشاء الأساس الاستراتيجي", $"تم إنشاء الأساس الاستراتيجي للمشروع {projectId}", userEmail);
            }

            foundation.CompletenessScore = CalculateCompleteness(foundation);
            return foundation;
        }

        /// <summary>
        /// حساب مؤشر اكتمال التخطيط الاستراتيجي (0 - 100%)
        /// </summary>
        public int CalculateCompleteness(StrategicFoundation sf)
        {
            if (sf == null) return 0;
            int score = 0;

            // 1. بطاقة المشروع (15%)
            if (!string.IsNullOrWhiteSpace(sf.IdeaTitle)) score += 5;
            if (!string.IsNullOrWhiteSpace(sf.TargetZone)) score += 5;
            if (sf.EstimatedBudget > 0) score += 5;

            // 2. المعطيات والمؤشرات (15%)
            if (sf.DataSources.Any(d => !d.IsDeleted)) score += 8;
            if (sf.ContextIndicators.Any(c => !c.IsDeleted)) score += 7;

            // 3. الأطراف المعنية (10%)
            if (sf.Stakeholders.Any(s => !s.IsDeleted)) score += 10;

            // 4. التحليل الاستراتيجي SWOT/PESTEL/Needs (15%)
            if (sf.SwotItems.Any(s => !s.IsDeleted)) score += 5;
            if (sf.PestelItems.Any(p => !p.IsDeleted)) score += 5;
            if (sf.NeedsAssessments.Any(n => !n.IsDeleted)) score += 5;

            // 5. المشكلة والخيارات (15%)
            if (!string.IsNullOrWhiteSpace(sf.CoreProblemStatement)) score += 10;
            if (sf.StrategicOptions.Any(o => !o.IsDeleted && o.IsSelected)) score += 5;

            // 6. نظرية التغيير (10%)
            if (sf.TheoryOfChangeNodes.Any(t => !t.IsDeleted)) score += 10;

            // 7. الأهداف الاستراتيجية (10%)
            if (sf.StrategicObjectives.Any(o => !o.IsDeleted)) score += 10;

            // 8. المخاطر والفرضيات (10%)
            if (sf.Risks.Any(r => !r.IsDeleted)) score += 5;
            if (sf.Assumptions.Any(a => !a.IsDeleted)) score += 5;

            return Math.Min(100, score);
        }

        /// <summary>
        /// الحذف المنطقي لكيان استراتيجي مع توثيق التدقيق
        /// </summary>
        public async Task<bool> SoftDeleteAsync<T>(int entityId, string userEmail) where T : StrategicBaseEntity
        {
            using var db = _dbFactory();
            var entity = await db.Set<T>().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == entityId);
            if (entity == null || entity.IsDeleted) return false;

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = userEmail;
            entity.LastModifiedAt = DateTime.UtcNow;
            entity.Version++;

            await db.SaveChangesAsync();
            AuditLogger.Log("حذف منطقي (Soft-Delete)", $"تم نقل {typeof(T).Name} (Id: {entityId}) إلى سلة المحذوفات", userEmail);
            return true;
        }

        /// <summary>
        /// استرجاع كيان من سلة المحذوفات
        /// </summary>
        public async Task<bool> RestoreAsync<T>(int entityId, string userEmail) where T : StrategicBaseEntity
        {
            using var db = _dbFactory();
            var entity = await db.Set<T>().IgnoreQueryFilters().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == entityId);
            if (entity == null || !entity.IsDeleted) return false;

            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedBy = null;
            entity.LastModifiedAt = DateTime.UtcNow;
            entity.Version++;

            await db.SaveChangesAsync();
            AuditLogger.Log("استرجاع من المحذوفات", $"تم استرجاع {typeof(T).Name} (Id: {entityId}) من سلة المحذوفات", userEmail);
            return true;
        }

        /// <summary>
        /// استرجاع العناصر المحذوفة منطقياً (سلة المحذوفات)
        /// </summary>
        public async Task<List<T>> GetDeletedItemsAsync<T>(int strategicFoundationId) where T : StrategicBaseEntity
        {
            using var db = _dbFactory();
            return await db.Set<T>()
                .IgnoreQueryFilters()
                .Where(e => e.IsDeleted && EF.Property<int>(e, "StrategicFoundationId") == strategicFoundationId)
                .ToListAsync();
        }
    }
}
