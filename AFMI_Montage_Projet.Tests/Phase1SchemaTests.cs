using System;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    public class Phase1SchemaTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public Phase1SchemaTests()
        {
            // إنشاء اتصال SQLite في الذاكرة مخصص للاختبارات
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
        public void ContributionModel_InitializesWithExpectedDefaults()
        {
            // Arrange & Act
            var contribution = new Contribution
            {
                EntityType = "Activity",
                EntityId = "act-123",
                FieldName = "Description",
                BaseValue = "Old Description",
                ProposedValue = "New Proposed Description",
                AuthorEmail = "member@domain.com",
                AuthorName = "Team Member"
            };

            // Assert
            Assert.NotEqual(Guid.Empty, contribution.Id);
            Assert.Equal(ContributionStatuses.Proposed, contribution.Status);
            Assert.Equal(1, contribution.BaseVersion);
            Assert.True(contribution.CreatedAt <= DateTime.UtcNow);
            Assert.Empty(contribution.Votes);
        }

        [Fact]
        public void BaseEntity_HasVersionAndUpdatedByEmailForOptimisticConcurrency()
        {
            // Arrange & Act
            var project = new Project
            {
                Title = "Test Project",
                GeneralObjective = "Objective Test"
            };

            var activity = new Activity
            {
                Description = "Activity Test"
            };

            // Assert
            Assert.Equal(1, project.Version);
            Assert.Null(project.UpdatedByEmail);
            Assert.Equal(1, activity.Version);
            Assert.Null(activity.UpdatedByEmail);

            // Simulation of update
            project.Version++;
            project.UpdatedByEmail = "admin@domain.com";
            Assert.Equal(2, project.Version);
            Assert.Equal("admin@domain.com", project.UpdatedByEmail);
        }

        [Fact]
        public void DbContext_CanInsertAndRetrieveContributionWithVotes()
        {
            // Arrange
            using var context = new AppDbContext(_options);
            var contribId = Guid.NewGuid();

            var contribution = new Contribution
            {
                Id = contribId,
                EntityType = "Objective",
                EntityId = "obj-456",
                FieldName = "Title",
                BaseValue = "Original Title",
                ProposedValue = "Modified Title",
                AuthorEmail = "author@domain.com",
                AuthorName = "Original Author",
                BaseVersion = 1,
                Status = ContributionStatuses.Proposed
            };

            var vote = new Vote
            {
                ContributionId = contribId,
                VoterEmail = "voter1@domain.com",
                VoterName = "Voter One",
                Approve = true,
                Comment = "Looks good to me."
            };

            // Act
            context.Contributions.Add(contribution);
            context.Votes.Add(vote);
            context.SaveChanges();

            // Assert
            using var queryContext = new AppDbContext(_options);
            var retrieved = queryContext.Contributions
                .Include(c => c.Votes)
                .FirstOrDefault(c => c.Id == contribId);

            Assert.NotNull(retrieved);
            Assert.Equal("Objective", retrieved.EntityType);
            Assert.Equal("Original Title", retrieved.BaseValue);
            Assert.Equal("Modified Title", retrieved.ProposedValue);
            Assert.Single(retrieved.Votes);
            Assert.True(retrieved.Votes.First().Approve);
            Assert.Equal("voter1@domain.com", retrieved.Votes.First().VoterEmail);
        }

        [Fact]
        public void DbContext_CascadeDeletesVotesWhenContributionIsDeleted()
        {
            // Arrange
            using var context = new AppDbContext(_options);
            var contribId = Guid.NewGuid();

            var contribution = new Contribution
            {
                Id = contribId,
                EntityType = "Project",
                EntityId = "proj-1",
                FieldName = "Title",
                AuthorEmail = "user@domain.com",
                AuthorName = "User"
            };

            var vote = new Vote
            {
                ContributionId = contribId,
                VoterEmail = "voter@domain.com",
                VoterName = "Voter",
                Approve = true
            };

            context.Contributions.Add(contribution);
            context.Votes.Add(vote);
            context.SaveChanges();

            // Act: حذف المساهمة
            context.Contributions.Remove(contribution);
            context.SaveChanges();

            // Assert: يجب أن تحذف الأصوات المرتبطة بها تلقائياً
            using var queryContext = new AppDbContext(_options);
            Assert.Empty(queryContext.Contributions.Where(c => c.Id == contribId));
            Assert.Empty(queryContext.Votes.Where(v => v.ContributionId == contribId));
        }

        [Fact]
        public void DbContext_PreventsDuplicateVotesFromSameUserOnSameContribution()
        {
            // Arrange
            using var context = new AppDbContext(_options);
            var contribId = Guid.NewGuid();

            var contribution = new Contribution
            {
                Id = contribId,
                EntityType = "BudgetItem",
                EntityId = "bgt-1",
                FieldName = "Amount",
                AuthorEmail = "author@domain.com",
                AuthorName = "Author"
            };
            context.Contributions.Add(contribution);
            context.SaveChanges();

            var vote1 = new Vote
            {
                ContributionId = contribId,
                VoterEmail = "duplicate@domain.com",
                VoterName = "User A",
                Approve = true
            };
            context.Votes.Add(vote1);
            context.SaveChanges();

            var vote2 = new Vote
            {
                ContributionId = contribId,
                VoterEmail = "duplicate@domain.com", // نفس البريد على نفس المقترح
                VoterName = "User A",
                Approve = false
            };
            context.Votes.Add(vote2);

            // Act & Assert
            // يجب أن يفشل الحفظ بسبب الفهرس الفريد IX_Votes_ContributionId_VoterEmail
            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        }
    }
}
