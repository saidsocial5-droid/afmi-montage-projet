using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Security;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AFMI_Montage_projet_2.Services.Collab
{
    public class QuorumEvaluationResult
    {
        public bool IsQuorumMet { get; set; }
        public bool IsRejected { get; set; }
        public int ApproveCount { get; set; }
        public int RejectCount { get; set; }
        public int TotalEligibleMembers { get; set; }
        public string Rule { get; set; } = QuorumRules.Unanimous;
        public string Summary { get; set; } = string.Empty;
    }

    public class MergeResult
    {
        public bool Success { get; set; }
        public bool ConflictDetected { get; set; }
        public int NewEntityVersion { get; set; }
        public string Message { get; set; } = string.Empty;
        public Contribution? Contribution { get; set; }

        /// <summary>Vrai si la version officielle locale a été modifiée (à republier).</summary>
        public bool OfficialDataChanged { get; set; }

        public static MergeResult Ok(Contribution contrib, int newVer, string msg) =>
            new() { Success = true, Contribution = contrib, NewEntityVersion = newVer, Message = msg };

        public static MergeResult Conflict(Contribution contrib, string msg) =>
            new() { Success = false, ConflictDetected = true, Contribution = contrib, Message = msg };

        public static MergeResult Failed(string msg) =>
            new() { Success = false, Message = msg };
    }

    /// <summary>
    /// محرك دمج المساهمات المعتمدة (MergeService)
    /// يقيم النصاب القانوني، يستحوذ على قفل merge.lock، يفحص التزامن المتفائل،
    /// يطبق التعديل ويزيد رقم الإصدار، ويوثق العملية في AuditLogs.
    /// Mode « décision de l'administrateur » (ADR 23) : <see cref="ApproveAsync"/> / <see cref="RejectAsync"/>.
    /// Sur le poste officiel, l'approbation est intégrée immédiatement ; ailleurs elle est marquée « Accepted »
    /// et le poste officiel l'intègre à sa prochaine synchronisation (<see cref="ApplyAcceptedProposalsAsync"/>).
    /// </summary>
    public class MergeService
    {
        private static MergeService? _instance;
        public static MergeService Instance => _instance ??= new MergeService();

        private readonly Func<AppDbContext> _dbFactory;
        private readonly ICollabStore? _collabStore;
        private readonly AuthService? _authService;

        public MergeService()
        {
            _dbFactory = () => new AppDbContext();
            _collabStore = null;
            _authService = null;
        }

        public MergeService(Func<AppDbContext> dbFactory, ICollabStore? collabStore, AuthService? authService = null)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _collabStore = collabStore;
            _authService = authService;
        }

        private ICollabStore? GetCollabStore() => _collabStore ?? AuthService.Instance.CollabStore;
        private AuthService GetAuthService() => _authService ?? AuthService.Instance;

        /// <summary>
        /// تقييم النصاب القانوني بناءً على قاعدة الفريق والأصوات المسجلة
        /// </summary>
        public static QuorumEvaluationResult EvaluateQuorum(Contribution contribution, TeamConfig teamConfig)
        {
            if (contribution == null) throw new ArgumentNullException(nameof(contribution));
            teamConfig ??= new TeamConfig();

            int eligibleMembers = Math.Max(1, teamConfig.Members.Count(m => teamConfig.CanVote(m.Email)));
            int approves = contribution.Votes.Count(v => v.Approve);
            int rejects = contribution.Votes.Count(v => !v.Approve);

            var result = new QuorumEvaluationResult
            {
                ApproveCount = approves,
                RejectCount = rejects,
                TotalEligibleMembers = eligibleMembers,
                Rule = teamConfig.QuorumRule
            };

            switch (teamConfig.QuorumRule)
            {
                case QuorumRules.AdminDecision:
                    // Décision d'un administrateur : son vote suffit
                    result.IsQuorumMet = contribution.Votes.Any(v => v.Approve && teamConfig.CanDecide(v.VoterEmail));
                    result.IsRejected = contribution.Votes.Any(v => !v.Approve && teamConfig.CanDecide(v.VoterEmail));
                    result.Summary = "Décision de l'administrateur";
                    break;

                case QuorumRules.Majority:
                    // أغلبية مطلقة: عدد الموافقين أكبر من نصف إجمالي الأعضاء
                    int majorityNeeded = (eligibleMembers / 2) + 1;
                    result.IsQuorumMet = approves >= majorityNeeded;
                    result.IsRejected = rejects >= majorityNeeded;
                    result.Summary = $"{approves} موافق من أصل {eligibleMembers} (المطلوب للأغلبية: {majorityNeeded})";
                    break;

                case QuorumRules.Threshold:
                    // عتبة عددية محددة n
                    int threshold = Math.Max(1, teamConfig.QuorumThreshold);
                    result.IsQuorumMet = approves >= threshold;
                    result.IsRejected = rejects > (eligibleMembers - threshold);
                    result.Summary = $"{approves} موافق (المطلوب بالعتبة: {threshold})";
                    break;

                case QuorumRules.Unanimous:
                default:
                    // إجماع: موافقة جميع الأعضاء دون أي رفض
                    result.IsQuorumMet = approves >= eligibleMembers && rejects == 0;
                    result.IsRejected = rejects > 0;
                    result.Summary = $"{approves} من {eligibleMembers} وافقوا (الإجماع التام مطلوب، رفض: {rejects})";
                    break;
            }

            return result;
        }

        #region Décision de l'administrateur (ADR 23)

        /// <summary>
        /// Approuve une proposition. Poste officiel : intégration immédiate dans la base qui fait foi.
        /// Autre poste : statut « Accepted », intégré ensuite par le poste officiel.
        /// <paramref name="force"/> : intégrer même si la valeur officielle a changé depuis la proposition.
        /// </summary>
        public async Task<MergeResult> ApproveAsync(Guid proposalId, string? notes = null, bool force = false, CancellationToken cancellationToken = default)
        {
            var auth = GetAuthService();
            if (!auth.CanDecide())
                throw new UnauthorizedAccessException("Seuls les administrateurs de l'association peuvent approuver une proposition.");
            string reviewer = auth.CurrentUser!.Email;

            Contribution? contribution;
            using (var db = _dbFactory())
            {
                contribution = await db.Contributions.FirstOrDefaultAsync(c => c.Id == proposalId, cancellationToken);
                if (contribution == null) return MergeResult.Failed("Proposition introuvable.");
                if (contribution.Status is ContributionStatuses.Approved or ContributionStatuses.Rejected)
                    return MergeResult.Failed("Cette proposition a déjà été traitée.");

                if (!auth.IsOfficialStation)
                {
                    contribution.Status = ContributionStatuses.Accepted;
                    contribution.ReviewedByEmail = reviewer;
                    contribution.ReviewedAt = DateTime.UtcNow;
                    contribution.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
                    await db.SaveChangesAsync(cancellationToken);

                    await PublishDecisionAsync(contribution, cancellationToken);
                    AuditLogger.Log("Proposition approuvée", $"{Describe(contribution)} approuvée par {reviewer} (intégration par le poste officiel).", reviewer);
                    return MergeResult.Ok(contribution, 0, "Proposition approuvée. Elle sera intégrée à la version officielle par le poste officiel lors de sa prochaine synchronisation.");
                }
            }

            return await IntegrateAsync(proposalId, reviewer, notes, force, cancellationToken);
        }

        /// <summary>Rejette une proposition (avec motif facultatif communiqué à son auteur).</summary>
        public async Task<MergeResult> RejectAsync(Guid proposalId, string? reason = null, CancellationToken cancellationToken = default)
        {
            var auth = GetAuthService();
            if (!auth.CanDecide())
                throw new UnauthorizedAccessException("Seuls les administrateurs de l'association peuvent rejeter une proposition.");
            string reviewer = auth.CurrentUser!.Email;

            using var db = _dbFactory();
            var contribution = await db.Contributions.FirstOrDefaultAsync(c => c.Id == proposalId, cancellationToken);
            if (contribution == null) return MergeResult.Failed("Proposition introuvable.");
            if (contribution.Status is ContributionStatuses.Approved or ContributionStatuses.Rejected)
                return MergeResult.Failed("Cette proposition a déjà été traitée.");

            contribution.Status = ContributionStatuses.Rejected;
            contribution.ReviewedByEmail = reviewer;
            contribution.ReviewedAt = DateTime.UtcNow;
            contribution.ReviewNotes = string.IsNullOrWhiteSpace(reason) ? "Rejetée par l'administrateur." : reason.Trim();
            await db.SaveChangesAsync(cancellationToken);

            await PublishDecisionAsync(contribution, cancellationToken);
            AuditLogger.Log("Proposition rejetée", $"{Describe(contribution)} rejetée par {reviewer}.", reviewer);
            return MergeResult.Ok(contribution, 0, "Proposition rejetée.");
        }

        /// <summary>
        /// Poste officiel : intègre les propositions approuvées depuis un autre poste (statut « Accepted »).
        /// Les propositions en conflit passent « À revoir » et réapparaissent dans la file de validation.
        /// </summary>
        public async Task<List<MergeResult>> ApplyAcceptedProposalsAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<MergeResult>();
            if (!GetAuthService().IsOfficialStation) return results;

            List<Contribution> accepted;
            using (var db = _dbFactory())
            {
                accepted = await db.Contributions.AsNoTracking()
                    .Where(c => c.Status == ContributionStatuses.Accepted)
                    .OrderBy(c => c.ReviewedAt)
                    .ToListAsync(cancellationToken);
            }

            // Créations d'abord : un parent ajouté doit exister avant ses enfants
            foreach (var c in accepted.OrderByDescending(c => c.FieldName == ProposalFields.Create))
            {
                results.Add(await IntegrateAsync(c.Id, c.ReviewedByEmail ?? SecurityConstants.WorkspaceOwnerEmail, c.ReviewNotes, force: false, cancellationToken));
            }
            return results;
        }

        /// <summary>Applique la proposition à la base locale (qui fait foi) et enregistre la décision.</summary>
        private async Task<MergeResult> IntegrateAsync(Guid proposalId, string reviewer, string? notes, bool force, CancellationToken cancellationToken)
        {
            using var db = _dbFactory();
            var contribution = await db.Contributions.FirstOrDefaultAsync(c => c.Id == proposalId, cancellationToken);
            if (contribution == null) return MergeResult.Failed("Proposition introuvable.");

            MergeResult result;
            try
            {
                result = contribution.FieldName switch
                {
                    ProposalFields.Note => MergeResult.Ok(contribution, 0, "Suggestion prise en compte."),
                    ProposalFields.Create => await ApplyCreationAsync(db, contribution, cancellationToken),
                    _ => await ApplyFieldChangeAsync(db, contribution, force, cancellationToken)
                };
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or JsonException or InvalidOperationException or ArgumentException)
            {
                AppLogger.Error(nameof(MergeService), $"Intégration impossible de {Describe(contribution)}.", ex);
                result = MergeResult.Failed($"Intégration impossible : {ex.Message}");
            }

            contribution.ReviewedByEmail = reviewer;
            contribution.ReviewedAt = DateTime.UtcNow;
            if (result.Success)
            {
                contribution.Status = ContributionStatuses.Approved;
                contribution.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? "Intégrée à la version officielle." : notes.Trim();
            }
            else
            {
                contribution.Status = ContributionStatuses.NeedsReview;
                contribution.ReviewNotes = result.Message;
                result.Contribution = contribution;
            }

            await db.SaveChangesAsync(cancellationToken);
            await PublishDecisionAsync(contribution, cancellationToken);

            if (result.Success)
                AuditLogger.Log("Proposition intégrée", $"{Describe(contribution)} intégrée à la version officielle (décision de {reviewer}).", reviewer);
            return result;
        }

        private static async Task<MergeResult> ApplyFieldChangeAsync(AppDbContext db, Contribution contribution, bool force, CancellationToken cancellationToken)
        {
            var entityType = TeamDataModel.FindEntityType(contribution.EntityType);
            if (entityType == null) return MergeResult.Failed($"Type d'élément inconnu : {contribution.EntityType}.");

            var entity = await FindTargetEntityAsync(db, contribution.EntityType, contribution.EntityId, cancellationToken);
            if (entity == null) return MergeResult.Failed("L'élément visé n'existe plus dans la version officielle.");

            var property = TeamDataModel.EditableProperties(entityType).FirstOrDefault(p => p.Name == contribution.FieldName);
            if (property?.PropertyInfo == null) return MergeResult.Failed($"Le champ « {contribution.FieldName} » ne peut pas être modifié par proposition.");

            string? current = EntityValueCodec.ToRaw(property, property.PropertyInfo.GetValue(entity));
            if (!force && !EntityValueCodec.AreEquivalent(property, current, contribution.BaseValue))
            {
                if (EntityValueCodec.AreEquivalent(property, current, contribution.ProposedValue))
                    return MergeResult.Ok(contribution, entity.Version, "La version officielle contient déjà cette valeur.");
                return MergeResult.Conflict(contribution, "La valeur officielle a changé depuis la rédaction de la proposition. Comparez puis confirmez pour l'appliquer quand même.");
            }

            property.PropertyInfo.SetValue(entity, EntityValueCodec.FromRaw(property, contribution.ProposedValue));
            entity.Version++;
            entity.UpdatedByEmail = contribution.AuthorEmail;
            entity.LastModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return new MergeResult
            {
                Success = true,
                Contribution = contribution,
                NewEntityVersion = entity.Version,
                OfficialDataChanged = true,
                Message = "Modification intégrée à la version officielle."
            };
        }

        private static async Task<MergeResult> ApplyCreationAsync(AppDbContext db, Contribution contribution, CancellationToken cancellationToken)
        {
            var entityType = TeamDataModel.FindEntityType(contribution.EntityType);
            if (entityType == null || !typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                return MergeResult.Failed($"Type d'élément inconnu : {contribution.EntityType}.");

            if (await FindEntityByGlobalIdAsync(db, entityType, contribution.EntityId, cancellationToken) != null)
                return MergeResult.Ok(contribution, 1, "L'élément existe déjà dans la version officielle.");

            var payload = JsonSerializer.Deserialize<CreationPayload>(contribution.ProposedValue ?? "{}") ?? new CreationPayload();
            var entity = (BaseEntity)Activator.CreateInstance(entityType.ClrType)!;

            foreach (var (name, raw) in payload.Columns)
            {
                var property = entityType.FindProperty(name);
                if (property?.PropertyInfo == null || property.IsPrimaryKey() || property.IsForeignKey() || !property.PropertyInfo.CanWrite) continue;
                property.PropertyInfo.SetValue(entity, EntityValueCodec.FromRaw(property, raw));
            }

            foreach (var fk in entityType.GetForeignKeys().Where(fk => fk.Properties.Count == 1))
            {
                var fkProperty = fk.Properties[0];
                if (fkProperty.PropertyInfo == null) continue;

                if (payload.ParentGlobalIds.TryGetValue(fkProperty.Name, out var parentGid) && !string.IsNullOrEmpty(parentGid))
                {
                    var parent = await FindEntityByGlobalIdAsync(db, fk.PrincipalEntityType, parentGid, cancellationToken);
                    if (parent == null)
                        return MergeResult.Failed($"L'élément parent ({TeamDataModel.EntityTypeLabel(fk.PrincipalEntityType.ClrType.Name)}) n'existe pas encore dans la version officielle : approuvez d'abord son ajout.");
                    var principalKey = fk.PrincipalKey.Properties[0];
                    fkProperty.PropertyInfo.SetValue(entity, principalKey.PropertyInfo!.GetValue(parent));
                }
                else if (fk.IsRequired)
                {
                    return MergeResult.Failed($"L'élément ajouté n'est rattaché à aucun {TeamDataModel.EntityTypeLabel(fk.PrincipalEntityType.ClrType.Name)}.");
                }
                else
                {
                    fkProperty.PropertyInfo.SetValue(entity, null);
                }
            }

            entity.GlobalId = contribution.EntityId;
            entity.Version = 1;
            entity.AuthorEmail = contribution.AuthorEmail;
            entity.AuthorName = contribution.AuthorName;
            entity.ContributionStatus = "Official";
            entity.LastModifiedAt = DateTime.UtcNow;

            db.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            return new MergeResult
            {
                Success = true,
                Contribution = contribution,
                NewEntityVersion = 1,
                OfficialDataChanged = true,
                Message = "Élément ajouté à la version officielle."
            };
        }

        private async Task PublishDecisionAsync(Contribution contribution, CancellationToken cancellationToken)
        {
            WeakReferenceMessenger.Default.Send(new ProposalUpdatedMessage(contribution));
            var store = GetCollabStore();
            if (store == null) return;
            try
            {
                await UpdateProposalOnCloudAsync(store, contribution, cancellationToken);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(MergeService), $"Décision sur {contribution.Id} enregistrée localement, envoi différé.", ex);
            }
        }

        private static string Describe(Contribution c) =>
            $"« {(string.IsNullOrEmpty(c.EntityLabel) ? c.EntityType : c.EntityLabel)} » ({(c.FieldName == ProposalFields.Create ? "ajout" : c.FieldName == ProposalFields.Note ? "suggestion" : c.FieldName)}, par {c.AuthorEmail})";

        #endregion

        /// <summary>
        /// تنفيذ عملية دمج المساهمة المتفائلة مع قفل merge.lock
        /// </summary>
        public async Task<MergeResult> MergeProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            var auth = GetAuthService();
            string currentEmail = auth.CurrentUser?.Email ?? "system@afmi";
            string currentName = auth.CurrentUser?.DisplayName ?? "System Admin";

            // 1. تحميل المقترح من قاعدة البيانات المحلية مع أصواته
            Contribution? contribution;
            using (var db = _dbFactory())
            {
                contribution = await db.Contributions
                    .Include(c => c.Votes)
                    .FirstOrDefaultAsync(c => c.Id == proposalId, cancellationToken);
            }

            if (contribution == null)
            {
                return MergeResult.Failed($"المساهمة {proposalId} غير موجودة في قاعدة البيانات.");
            }

            // 2. التحقق من اكتمال النصاب
            var teamConfig = auth.CurrentTeamConfig ?? new TeamConfig();
            var quorum = EvaluateQuorum(contribution, teamConfig);

            if (quorum.IsRejected)
            {
                return await HandleRejectedProposalAsync(contribution, currentEmail, cancellationToken);
            }

            if (!quorum.IsQuorumMet)
            {
                return MergeResult.Failed($"لم يكتمل النصاب بعد: {quorum.Summary}");
            }

            // 3. الاستحواذ على قفل الدمج المتفائل merge.lock على مساحة العمل السحابية
            var store = GetCollabStore();
            LockResult? lockRes = null;

            if (store != null)
            {
                lockRes = await store.AcquireLockAsync("merge", currentEmail, currentName, TimeSpan.FromSeconds(60), cancellationToken);
                if (!lockRes.Acquired)
                {
                    return MergeResult.Failed($"تعذر حيازة قفل الدمج: {lockRes.Message}");
                }
            }

            try
            {
                // 4. فحص التزامن المتفائل (Optimistic Concurrency Check)
                using var db = _dbFactory();
                var entity = await FindTargetEntityAsync(db, contribution.EntityType, contribution.EntityId, cancellationToken);

                if (entity == null)
                {
                    return MergeResult.Failed($"الكيان المستهدف {contribution.EntityType} (Id: {contribution.EntityId}) غير موجود.");
                }

                // فحص هل الإصدار الحالي يطابق BaseVersion المقترح
                if (entity.Version != contribution.BaseVersion)
                {
                    // ⚠️ تعارض إصدارات (Concurrent Version Conflict)
                    contribution.Status = ContributionStatuses.NeedsReview;
                    contribution.ReviewNotes = $"تعارض في التزامن: اعتمدت تعديلات سابقة رفعت إصدار الكيان إلى v{entity.Version}، بينما المقترح مبني على v{contribution.BaseVersion}.";

                    db.Contributions.Update(contribution);
                    await db.SaveChangesAsync(cancellationToken);

                    AuditLogger.Log("تعارض في الدمج (NeedsReview)", $"تم اكتشاف تعارض في المقترح {contribution.Id} على {contribution.EntityType}.{contribution.FieldName} (الإصدار الحالي v{entity.Version} مقابل مقترح v{contribution.BaseVersion})", currentEmail);
                    WeakReferenceMessenger.Default.Send(new ProposalUpdatedMessage(contribution));

                    // تحديث الحالة سحابياً
                    if (store != null)
                    {
                        await UpdateProposalOnCloudAsync(store, contribution, cancellationToken);
                    }

                    return MergeResult.Conflict(contribution, contribution.ReviewNotes);
                }

                // ✅ تطابق الإصدار: تطبيق التعديل بنجاح!
                ApplyValueToEntity(entity, contribution.FieldName, contribution.ProposedValue);

                entity.Version++;
                entity.UpdatedByEmail = contribution.AuthorEmail;
                entity.LastModifiedAt = DateTime.UtcNow;

                // تحديث حالة المساهمة
                contribution.Status = ContributionStatuses.Approved;
                contribution.ReviewedAt = DateTime.UtcNow;
                contribution.ReviewedByEmail = currentEmail;
                contribution.ReviewNotes = "تم اعتماد ودمج المقترح بنجاح بعد اكتمال النصاب.";

                db.Contributions.Update(contribution);
                await db.SaveChangesAsync(cancellationToken);

                // توثيق الاعتماد والدمج في AuditLogs
                AuditLogger.Log("اعتماد ودمج مساهمة", $"تم اعتماد المقترح {contribution.Id} بنجاح. رُفع إصدار {contribution.EntityType} إلى v{entity.Version} بواسطة {currentEmail}.", currentEmail);
                WeakReferenceMessenger.Default.Send(new ProposalUpdatedMessage(contribution));

                if (store != null)
                {
                    // 1. تحديث ملف المقترح على درايف
                    await UpdateProposalOnCloudAsync(store, contribution, cancellationToken);

                    // 2. إنشاء لقطة تراكمية Snapshot
                    string snapshotJson = JsonSerializer.Serialize(entity, new JsonSerializerOptions { WriteIndented = true });
                    await store.UploadSnapshotAsync(entity.Version, snapshotJson, cancellationToken);

                    // 3. إعادة توليد sync/projects.json لتطبيق الهاتف
                    await RegenerateMobileProjectsJsonAsync(db, store, cancellationToken);
                }

                return MergeResult.Ok(contribution, entity.Version, $"تم اعتماد ودمج المقترح بنجاح وإصدار النسخة v{entity.Version}.");
            }
            finally
            {
                // فك قفل الدمج سحابياً دائماً
                if (store != null && lockRes?.LockToken != null)
                {
                    await store.ReleaseLockAsync("merge", lockRes.LockToken, cancellationToken);
                }
            }
        }

        private async Task<MergeResult> HandleRejectedProposalAsync(Contribution contribution, string reviewerEmail, CancellationToken cancellationToken)
        {
            using var db = _dbFactory();
            contribution.Status = ContributionStatuses.Rejected;
            contribution.ReviewedAt = DateTime.UtcNow;
            contribution.ReviewedByEmail = reviewerEmail;
            contribution.ReviewNotes = "تم رفض المقترح بناءً على نتائج تصويت الفريق.";

            db.Contributions.Update(contribution);
            await db.SaveChangesAsync(cancellationToken);

            AuditLogger.Log("رفض مقترح مساهمة", $"تم رفض المقترح {contribution.Id} على {contribution.EntityType}.{contribution.FieldName} وفق نتائج التصويت.", reviewerEmail);
            WeakReferenceMessenger.Default.Send(new ProposalUpdatedMessage(contribution));

            var store = GetCollabStore();
            if (store != null)
            {
                await UpdateProposalOnCloudAsync(store, contribution, cancellationToken);
            }

            return MergeResult.Failed("تم رفض المقترح استناداً إلى تصويت الفريق.");
        }

        private static async Task UpdateProposalOnCloudAsync(ICollabStore store, Contribution contrib, CancellationToken cancellationToken)
        {
            await store.UploadProposalAsync(ProposalMapper.ToDto(contrib), cancellationToken);
        }

        private static async Task RegenerateMobileProjectsJsonAsync(AppDbContext db, ICollabStore store, CancellationToken cancellationToken)
        {
            var projects = await db.Projects.ToListAsync(cancellationToken);
            string json = ProjectsJsonExporter.GenerateJson(projects);
            await store.UploadSyncProjectsJsonAsync(json, cancellationToken);
        }

        private static async Task<BaseEntity?> FindTargetEntityAsync(AppDbContext db, string entityType, string entityId, CancellationToken cancellationToken)
        {
            switch (entityType)
            {
                case "Project": return await db.Projects.FirstOrDefaultAsync(x => x.GlobalId == entityId || x.Id.ToString() == entityId, cancellationToken);
                case "Objective": return await db.Objectives.FirstOrDefaultAsync(x => x.GlobalId == entityId || x.Id.ToString() == entityId, cancellationToken);
                case "Result": return await db.Results.FirstOrDefaultAsync(x => x.GlobalId == entityId || x.Id.ToString() == entityId, cancellationToken);
                case "Activity": return await db.Activities.FirstOrDefaultAsync(x => x.GlobalId == entityId || x.Id.ToString() == entityId, cancellationToken);
                case "BudgetItem": return await db.BudgetItems.FirstOrDefaultAsync(x => x.GlobalId == entityId || x.Id.ToString() == entityId, cancellationToken);
                case "Indicator": return await db.Indicators.FirstOrDefaultAsync(x => x.GlobalId == entityId || x.Id.ToString() == entityId, cancellationToken);
                case "Risk": return await db.Risks.FirstOrDefaultAsync(x => x.GlobalId == entityId || x.Id.ToString() == entityId, cancellationToken);
            }

            // Autres modules (planification stratégique, arbre à problèmes, financement…) : par GlobalId
            var type = TeamDataModel.FindEntityType(entityType);
            return type == null ? null : await FindEntityByGlobalIdAsync(db, type, entityId, cancellationToken);
        }

        /// <summary>Recherche générique par GlobalId, suppressions logiques comprises.</summary>
        private static async Task<BaseEntity?> FindEntityByGlobalIdAsync(AppDbContext db, IReadOnlyEntityType entityType, string globalId, CancellationToken cancellationToken)
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)) return null;
            var set = (IQueryable<BaseEntity>)typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(db, null)!;
            return await set.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.GlobalId == globalId, cancellationToken);
        }

        private static void ApplyValueToEntity(BaseEntity entity, string fieldName, string? newValue)
        {
            var entityType = TeamDataModel.FindEntityType(entity.GetType().Name);
            var property = entityType?.GetProperties().FirstOrDefault(p => string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));
            if (property?.PropertyInfo == null || !property.PropertyInfo.CanWrite || property.IsPrimaryKey()) return;

            try
            {
                property.PropertyInfo.SetValue(entity, EntityValueCodec.FromRaw(property, newValue));
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
            {
                AppLogger.Warn(nameof(MergeService), $"Valeur « {newValue} » non applicable au champ {fieldName}.", ex);
            }
        }

        /// <summary>
        /// حساب توقيع رقمي HMAC-SHA256 لمقترح تعاوني باستخدام المفتاح السري للجهاز
        /// </summary>
        public static string ComputeProposalSignature(ProposalDto dto, string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey)) return string.Empty;
            string canonical = $"{dto.Id}:{dto.EntityType}:{dto.EntityId}:{dto.FieldName}:{dto.ProposedValue}:{dto.AuthorEmail}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        }

        /// <summary>
        /// حساب توقيع رقمي HMAC-SHA256 لصوت تعاوني باستخدام المفتاح السري للجهاز
        /// </summary>
        public static string ComputeVoteSignature(VoteDto dto, string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey)) return string.Empty;
            string canonical = $"{dto.Id}:{dto.ProposalId}:{dto.VoterEmail}:{dto.Approve}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        }

        /// <summary>
        /// التحقق من صحة التوقيع الرقمي للمقترح لمنع التلاعب بالبيانات المنقولة من الأجهزة المقترنة
        /// </summary>
        public static bool VerifyProposalSignature(ProposalDto dto, string? deviceSecret)
        {
            // إذا لم يكن المقترح مصحوباً بمعرف جهاز، يُعامل كمقترح واجهة مباشرة (توافق تراجعي)
            if (string.IsNullOrEmpty(dto.DeviceId)) return true;
            if (string.IsNullOrEmpty(dto.DeviceSignature) || string.IsNullOrEmpty(deviceSecret)) return false;

            string expected = ComputeProposalSignature(dto, deviceSecret);
            return string.Equals(expected, dto.DeviceSignature, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// التحقق من صحة التوقيع الرقمي للصوت
        /// </summary>
        public static bool VerifyVoteSignature(VoteDto dto, string? deviceSecret)
        {
            if (string.IsNullOrEmpty(dto.DeviceId)) return true;
            if (string.IsNullOrEmpty(dto.DeviceSignature) || string.IsNullOrEmpty(deviceSecret)) return false;

            string expected = ComputeVoteSignature(dto, deviceSecret);
            return string.Equals(expected, dto.DeviceSignature, StringComparison.OrdinalIgnoreCase);
        }
    }
}
