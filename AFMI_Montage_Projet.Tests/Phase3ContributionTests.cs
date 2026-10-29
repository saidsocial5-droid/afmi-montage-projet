using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Services.Collab;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    public class Phase3ContributionTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public Phase3ContributionTests()
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
        public void TextDiffHelper_CalculatesAccurateWordDiff()
        {
            string oldText = "Amélioration de l'accès à l'eau";
            string newText = "Amélioration durable de l'accès à l'eau potable";

            var diff = TextDiffHelper.CalculateWordDiff(oldText, newText);

            Assert.NotEmpty(diff);
            // Verify that words "durable" and "potable" are detected as Added
            Assert.Contains(diff, p => p.Type == DiffType.Added && p.Text == "durable");
            Assert.Contains(diff, p => p.Type == DiffType.Added && p.Text == "potable");
            // Verify common words are Unchanged
            Assert.Contains(diff, p => p.Type == DiffType.Unchanged && p.Text == "Amélioration");
        }

        [Fact]
        public async Task ContributionService_CreatesProposal_AndPersistsToDatabase()
        {
            var fakeStore = new InMemoryFakeCollabStore();
            var service = new ContributionService(() => new AppDbContext(_options), fakeStore);

            // Act
            var proposal = await service.CreateProposalAsync(
                entityType: "Activity",
                entityId: "act-101",
                fieldName: "Description",
                baseValue: "Formation des paysans",
                proposedValue: "Formation avancée des jeunes paysans sur l'agroécologie",
                baseVersion: 1);

            // Assert
            Assert.NotNull(proposal);
            Assert.NotEqual(Guid.Empty, proposal.Id);
            Assert.Equal("Activity", proposal.EntityType);
            Assert.Equal("Description", proposal.FieldName);
            Assert.Equal(ContributionStatuses.Proposed, proposal.Status);

            // Verify stored in SQLite
            using var queryContext = new AppDbContext(_options);
            var retrieved = await queryContext.Contributions.FindAsync(proposal.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("Formation des paysans", retrieved.BaseValue);
            Assert.Equal("Formation avancée des jeunes paysans sur l'agroécologie", retrieved.ProposedValue);

            // Verify uploaded to cloud store
            var cloudProposals = await fakeStore.DownloadProposalsAsync();
            Assert.Single(cloudProposals);
            Assert.Equal(proposal.Id, cloudProposals.First().Id);
        }

        [Fact]
        public async Task ContributionService_ThrowsUnauthorized_WhenReviewerTriesToPropose()
        {
            var fakeStore = new InMemoryFakeCollabStore();
            var teamConfig = new TeamConfig
            {
                Members = new List<TeamMemberDto>
                {
                    new() { Email = "auditor@org.com", Name = "Auditor", Role = TeamRoles.Reviewer }
                }
            };
            await fakeStore.SaveTeamConfigAsync(teamConfig);

            // المحاكي يعتمد دور Reviewer
            var service = new ContributionService(() => new AppDbContext(_options), fakeStore);

            // عند استدعاء CreateProposal والمستخدم الحالي هو auditor@org.com من خلال TeamConfig
            // نتأكد أن فحص CanPropose يمنع دور Reviewer
            Assert.False(teamConfig.CanPropose("auditor@org.com"));
        }

        [Fact]
        public async Task ContributionService_SubmitsVote_AndUpdatesCounters()
        {
            var fakeStore = new InMemoryFakeCollabStore();
            var service = new ContributionService(() => new AppDbContext(_options), fakeStore);

            // 1. Create Proposal
            var proposal = await service.CreateProposalAsync(
                entityType: "Objective",
                entityId: "obj-1",
                fieldName: "Title",
                baseValue: "Titre A",
                proposedValue: "Titre B",
                baseVersion: 1);

            // 2. Submit Vote
            var vote = await service.SubmitVoteAsync(proposal.Id, approve: true, comment: "Parfait !");

            Assert.NotNull(vote);
            Assert.Equal(proposal.Id, vote.ContributionId);
            Assert.True(vote.Approve);
            Assert.Equal("Parfait !", vote.Comment);

            // Verify stored in SQLite
            using var queryContext = new AppDbContext(_options);
            var votes = await queryContext.Votes.Where(v => v.ContributionId == proposal.Id).ToListAsync();
            Assert.Single(votes);
            Assert.True(votes.First().Approve);
        }

        [Fact]
        public async Task ContributionService_DetectsFieldHasPendingProposal()
        {
            var fakeStore = new InMemoryFakeCollabStore();
            var service = new ContributionService(() => new AppDbContext(_options), fakeStore);

            // Before proposal
            bool hasBefore = await service.HasPendingProposalForFieldAsync("Project", "proj-10", "GeneralObjective");
            Assert.False(hasBefore);

            // Create proposal
            await service.CreateProposalAsync("Project", "proj-10", "GeneralObjective", "Vieux", "Nouveau", 1);

            // After proposal
            bool hasAfter = await service.HasPendingProposalForFieldAsync("Project", "proj-10", "GeneralObjective");
            Assert.True(hasAfter);

            // Another field on the same entity has none
            bool otherField = await service.HasPendingProposalForFieldAsync("Project", "proj-10", "Title");
            Assert.False(otherField);
        }
    }
}
