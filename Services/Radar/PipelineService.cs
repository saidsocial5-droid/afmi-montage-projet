using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Views;

namespace AFMI_Montage_projet_2.Services.Radar
{
    public class PipelineService
    {
        private readonly Func<AppDbContext> _dbContextFactory;

        public PipelineService(Func<AppDbContext>? dbContextFactory = null)
        {
            _dbContextFactory = dbContextFactory ?? (() => new AppDbContext());
        }

        /// <summary>
        /// إضافة فرصة تمويل إلى محفظة المشاريع (grants_pipeline) مع منع التكرار.
        /// </summary>
        public async Task<(bool Success, string Message, GrantOpportunity? Opportunity)> AddToPipelineAsync(
            RssItemCache item,
            OpportunityScoringResult? scoreResult = null)
        {
            using var db = _dbContextFactory();

            // 1. Vérification stricte anti-doublon (par URL originale ou ExternalId)
            bool exists = await db.GrantsPipeline
                .AnyAsync(g => !g.IsDeleted && (g.ExternalId == item.ExternalId || (!string.IsNullOrEmpty(g.OriginalUrl) && g.OriginalUrl == item.Link)));

            if (exists)
            {
                return (false, "Cette opportunité est déjà présente dans votre Pipeline des Projets.", null);
            }

            // 2. Préparation de l'entité GrantOpportunity
            var opp = new GrantOpportunity
            {
                ExternalId = !string.IsNullOrWhiteSpace(item.ExternalId) ? item.ExternalId : Guid.NewGuid().ToString("N"),
                Title = item.Title,
                Donor = item.Donor,
                OriginalUrl = item.Link,
                EstimatedBudget = scoreResult?.EstimatedBudget ?? item.AiEstimatedBudget,
                Currency = scoreResult?.Currency ?? item.AiCurrency ?? "EUR",
                Deadline = scoreResult?.Deadline ?? item.AiDeadline,
                SummaryAr = scoreResult?.SummaryAr ?? item.AiSummaryAr ?? string.Empty,
                SummaryFr = scoreResult?.SummaryFr ?? item.AiSummaryFr ?? item.CleanDescription,
                RelevanceScore = scoreResult?.RelevanceScore ?? item.AiScore ?? 5.0,
                MatchedAxes = scoreResult?.MatchedAxes != null ? string.Join(", ", scoreResult.MatchedAxes) : (item.AiMatchedAxes ?? string.Empty),
                Rationale = scoreResult?.Rationale ?? item.AiScoreRationale ?? string.Empty,
                PipelineStatus = "Mise sous veille",
                AddedAt = DateTime.UtcNow,
                Notes = $"Ajouté depuis le Radar des Projets ({item.FeedName})"
            };

            db.GrantsPipeline.Add(opp);

            // Mettre à jour l'élément en cache
            var cached = await db.RssItemsCache.FirstOrDefaultAsync(i => i.ExternalId == item.ExternalId);
            if (cached != null)
            {
                cached.IsInPipeline = true;
            }

            await db.SaveChangesAsync();

            return (true, "Opportunité enregistrée avec succès dans le Pipeline des Projets !", opp);
        }

        public async Task<List<GrantOpportunity>> GetPipelineAsync()
        {
            using var db = _dbContextFactory();
            return await db.GrantsPipeline
                .Where(g => !g.IsDeleted)
                .OrderByDescending(g => g.AddedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdatePipelineStatusAsync(int id, string newStatus)
        {
            using var db = _dbContextFactory();
            var opp = await db.GrantsPipeline.FindAsync(id);
            if (opp == null || opp.IsDeleted) return false;

            opp.PipelineStatus = newStatus;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePipelineNotesAsync(int id, string notes)
        {
            using var db = _dbContextFactory();
            var opp = await db.GrantsPipeline.FindAsync(id);
            if (opp == null || opp.IsDeleted) return false;

            opp.Notes = notes;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveFromPipelineAsync(int id)
        {
            using var db = _dbContextFactory();
            var opp = await db.GrantsPipeline.FindAsync(id);
            if (opp == null) return false;

            opp.IsDeleted = true;
            opp.DeletedAt = DateTime.UtcNow;

            // Démarquer dans le cache
            var cached = await db.RssItemsCache.FirstOrDefaultAsync(i => i.ExternalId == opp.ExternalId || i.Link == opp.OriginalUrl);
            if (cached != null)
            {
                cached.IsInPipeline = false;
            }

            await db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// فتح رابط الإعلان الرسمي في المتصفح الافتراضي بأمان (http/https uniquement via UseShellExecute).
        /// </summary>
        public static bool OpenOriginalUrlSafely(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;

            // Protection absolue : uniquement http ou https
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir le lien dans le navigateur :\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
    }
}
