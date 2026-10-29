using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Services.Collab;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    public class Phase4SyncServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public Phase4SyncServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task SyncService_PushesOfflineProposals_ToCloudStore()
        {
            // Arrange
            var fakeStore = new InMemoryFakeCollabStore();
            var syncService = new SyncService(fakeStore, () => new AppDbContext(_options));

            var proposalId = Guid.NewGuid();
            using (var db = new AppDbContext(_options))
            {
                // إنشاء مساهمة محلية دون اتصال (DriveFileId فارغ)
                db.Contributions.Add(new Contribution
                {
                    Id = proposalId,
                    EntityType = "Activity",
                    EntityId = "act-offline",
                    FieldName = "Description",
                    BaseValue = "Valeur 1",
                    ProposedValue = "Valeur 2",
                    AuthorEmail = "offline@member.com",
                    AuthorName = "Offline Member",
                    Status = ContributionStatuses.Proposed,
                    DriveFileId = null // لم ترفع بعد
                });
                await db.SaveChangesAsync();
            }

            // Act: تنفيذ دورة المزامنة
            await syncService.ExecuteSyncCycleAsync();

            // Assert
            // 1. تأكيد الرفع إلى المخزن السحابي
            var cloudProposals = await fakeStore.DownloadProposalsAsync();
            Assert.Contains(cloudProposals, p => p.Id == proposalId);

            // 2. تأكيد تحديث DriveFileId في SQLite المحلي
            using (var queryDb = new AppDbContext(_options))
            {
                var local = await queryDb.Contributions.FindAsync(proposalId);
                Assert.NotNull(local);
                Assert.False(string.IsNullOrEmpty(local.DriveFileId));
            }
        }

        [Fact]
        public async Task SyncService_PushesOfflineVotes_ToCloudStore()
        {
            // Arrange
            var fakeStore = new InMemoryFakeCollabStore();
            var syncService = new SyncService(fakeStore, () => new AppDbContext(_options));

            var proposalId = Guid.NewGuid();
            var voteId = Guid.NewGuid();

            using (var db = new AppDbContext(_options))
            {
                db.Contributions.Add(new Contribution
                {
                    Id = proposalId,
                    EntityType = "Project",
                    EntityId = "p-1",
                    FieldName = "Title",
                    AuthorEmail = "author@domain.com",
                    AuthorName = "Author"
                });

                // صوت محلي أنشئ دون اتصال
                db.Votes.Add(new Vote
                {
                    Id = voteId,
                    ContributionId = proposalId,
                    VoterEmail = "voter@domain.com",
                    VoterName = "Voter",
                    Approve = true,
                    DriveFileId = null
                });
                await db.SaveChangesAsync();
            }

            // Act
            await syncService.ExecuteSyncCycleAsync();

            // Assert
            var cloudVotes = await fakeStore.DownloadVotesForProposalAsync(proposalId);
            Assert.Contains(cloudVotes, v => v.VoterEmail == "voter@domain.com");

            using (var queryDb = new AppDbContext(_options))
            {
                var localVote = await queryDb.Votes.FindAsync(voteId);
                Assert.NotNull(localVote);
                Assert.False(string.IsNullOrEmpty(localVote.DriveFileId));
            }
        }

        [Fact]
        public async Task SyncService_PullsRemoteProposals_AndInsertsIntoSQLite()
        {
            // Arrange: مقترح أنشأه عضو آخر وموجود في السحابة
            var fakeStore = new InMemoryFakeCollabStore();
            var remoteProposalId = Guid.NewGuid();

            await fakeStore.UploadProposalAsync(new ProposalDto
            {
                Id = remoteProposalId,
                EntityType = "Result",
                EntityId = "res-20",
                FieldName = "Indicator",
                BaseValue = "Taux 50%",
                ProposedValue = "Taux 85%",
                AuthorEmail = "colleague@domain.com",
                AuthorName = "Colleague Member",
                Status = ContributionStatuses.Proposed,
                CreatedAt = DateTime.UtcNow
            });

            var syncService = new SyncService(fakeStore, () => new AppDbContext(_options));

            // Act
            await syncService.ExecuteSyncCycleAsync();

            // Assert: دمج المقترح بنجاح في SQLite المحلي
            using var queryDb = new AppDbContext(_options);
            var merged = await queryDb.Contributions.FindAsync(remoteProposalId);
            Assert.NotNull(merged);
            Assert.Equal("Result", merged.EntityType);
            Assert.Equal("Taux 85%", merged.ProposedValue);
            Assert.Equal("colleague@domain.com", merged.AuthorEmail);
        }

        [Fact]
        public async Task SyncService_PullsRemoteVotes_AndInsertsIntoSQLite()
        {
            // Arrange
            var fakeStore = new InMemoryFakeCollabStore();
            var proposalId = Guid.NewGuid();

            await fakeStore.UploadProposalAsync(new ProposalDto
            {
                Id = proposalId,
                EntityType = "BudgetItem",
                EntityId = "b-1",
                FieldName = "Amount",
                AuthorEmail = "author@domain.com",
                AuthorName = "Author"
            });

            await fakeStore.UploadVoteAsync(new VoteDto
            {
                Id = Guid.NewGuid(),
                ProposalId = proposalId,
                VoterEmail = "reviewer@domain.com",
                VoterName = "Auditor Reviewer",
                Approve = false,
                Comment = "Budget trop élevé",
                At = DateTime.UtcNow
            });

            var syncService = new SyncService(fakeStore, () => new AppDbContext(_options));

            // Act
            await syncService.ExecuteSyncCycleAsync();

            // Assert: دمج الصوت في SQLite
            using var queryDb = new AppDbContext(_options);
            var localVotes = await queryDb.Votes.Where(v => v.ContributionId == proposalId).ToListAsync();
            Assert.Single(localVotes);
            Assert.False(localVotes.First().Approve);
            Assert.Equal("Budget trop élevé", localVotes.First().Comment);
        }

        [Fact]
        public async Task SyncService_HandlesNetworkExceptions_GracefullyWithoutCrashing()
        {
            // محاكي يرمي استثناء شبكة
            var failingStore = new FailingNetworkCollabStore();
            var syncService = new SyncService(failingStore, () => new AppDbContext(_options));

            // Act: لا يجب أن يتوقف التطبيق أو يرمي استثناء غير معالج
            await syncService.ExecuteSyncCycleAsync();

            // Assert: تم رصد انقطاع الاتصال بنجاح وتفعيل وضع Offline
            Assert.False(syncService.IsOnline);
            Assert.Contains("دون اتصال", syncService.CurrentStatus);
        }
    }

    /// <summary>
    /// محاكي خطأ اتصال لاختبار قدرة المزامنة على معالجة انقطاع الشبكة
    /// </summary>
    public class FailingNetworkCollabStore : InMemoryFakeCollabStore
    {
        public override Task<string?> GetStartPageTokenAsync(CancellationToken cancellationToken = default) =>
            throw new System.Net.Http.HttpRequestException("Connection to Google Drive timed out (Network Offline)");

        public override Task<CollabChangesResult> GetChangesAsync(string pageToken, CancellationToken cancellationToken = default) =>
            throw new System.Net.Http.HttpRequestException("Connection to Google Drive timed out (Network Offline)");

        public override Task<System.Collections.Generic.List<ProposalDto>> DownloadProposalsAsync(CancellationToken cancellationToken = default) =>
            throw new System.Net.Http.HttpRequestException("Connection to Google Drive timed out (Network Offline)");
    }
}
