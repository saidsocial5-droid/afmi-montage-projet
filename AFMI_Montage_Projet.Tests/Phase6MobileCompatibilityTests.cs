using System;
using System.Collections.Generic;
using System.Text.Json;
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
    public class Phase6MobileCompatibilityTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public Phase6MobileCompatibilityTests()
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
        public void ProjectsJsonExporter_ProducesExactCamelCaseSchemaExpectedByAndroid()
        {
            // Arrange
            var fixedDate = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
            var project = new Project
            {
                Id = 42,
                Title = "Projet d'Appui Agricole & Écologique",
                GeneralObjective = "Renforcer la résilience des communautés rurales face au climat",
                StartDate = fixedDate,
                EndDate = fixedDate.AddMonths(12),
                TotalBudget = 150000.50m
            };

            var syncTimestamp = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

            // Act
            string json = ProjectsJsonExporter.GenerateJson(new[] { project }, syncTimestamp);

            // Assert: Parse raw JSON to inspect property names and types
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(JsonValueKind.Array, root.ValueKind);
            Assert.Equal(1, root.GetArrayLength());

            var first = root[0];

            // Verify exact camelCase property names expected by Android Kotlin Room entity
            Assert.True(first.TryGetProperty("id", out var idProp));
            Assert.Equal("42", idProp.GetString());

            Assert.True(first.TryGetProperty("title", out var titleProp));
            Assert.Equal("Projet d'Appui Agricole & Écologique", titleProp.GetString());

            Assert.True(first.TryGetProperty("description", out var descProp));
            Assert.Equal("Renforcer la résilience des communautés rurales face au climat", descProp.GetString());

            Assert.True(first.TryGetProperty("startDate", out var startProp));
            Assert.True(startProp.GetInt64() > 0);

            Assert.True(first.TryGetProperty("endDate", out var endProp));
            Assert.True(endProp.GetInt64() > startProp.GetInt64());

            Assert.True(first.TryGetProperty("budgetTotal", out var budgetProp));
            Assert.Equal(150000.50, budgetProp.GetDouble());

            Assert.True(first.TryGetProperty("budgetSpent", out var spentProp));
            Assert.Equal(0.0, spentProp.GetDouble());

            Assert.True(first.TryGetProperty("progressPercentage", out var progProp));
            Assert.Equal(0, progProp.GetInt32());

            Assert.True(first.TryGetProperty("lastSyncAt", out var syncProp));
            Assert.Equal(syncTimestamp.ToUnixTimeMilliseconds(), syncProp.GetInt64());
        }

        [Fact]
        public void ProjectsJsonExporter_HandlesNullDatesSafelyAsZero()
        {
            var project = new Project
            {
                Id = 1,
                Title = "Projet Sans Dates",
                StartDate = null,
                EndDate = null,
                TotalBudget = 0m
            };

            string json = ProjectsJsonExporter.GenerateJson(new[] { project });
            using var doc = JsonDocument.Parse(json);
            var item = doc.RootElement[0];

            Assert.Equal(0, item.GetProperty("startDate").GetInt64());
            Assert.Equal(0, item.GetProperty("endDate").GetInt64());
        }

        [Fact]
        public void ProjectsJsonExporter_PreservesArabicAndFrenchUtf8Integrity()
        {
            var project = new Project
            {
                Id = 10,
                Title = "مشروع تنمية الواحات وحماية البيئة",
                GeneralObjective = "حماية التنوع الحيوي وإعادة تأهيل النظم البيئية التقليدية",
                TotalBudget = 75000m
            };

            string json = ProjectsJsonExporter.GenerateJson(new[] { project });

            // Ensure no \uFFFD replacement characters or double encoding
            Assert.DoesNotContain("\ufffd", json);
            Assert.Contains("مشروع تنمية الواحات", json);
            Assert.Contains("حماية التنوع الحيوي", json);
        }

        [Fact]
        public async Task MergeService_UploadsRegeneratedProjectsJson_OnSuccessfulMerge()
        {
            // Test that a successful merge invokes UploadSyncProjectsJsonAsync on ICollabStore
            var fakeStore = new TrackingCollabStore();
            var teamConfig = new TeamConfig
            {
                QuorumRule = QuorumRules.Threshold,
                QuorumThreshold = 1,
                Members = new List<TeamMemberDto> { new() { Email = "admin@org.com", Role = TeamRoles.Admin } }
            };
            await fakeStore.SaveTeamConfigAsync(teamConfig);

            int projId;
            var proposalId = Guid.NewGuid();

            using (var db = new AppDbContext(_options))
            {
                var p = new Project { Title = "Ancien Titre", Version = 1 };
                db.Projects.Add(p);
                await db.SaveChangesAsync();
                projId = p.Id;

                var contrib = new Contribution
                {
                    Id = proposalId,
                    EntityType = "Project",
                    EntityId = projId.ToString(),
                    FieldName = "Title",
                    BaseValue = "Ancien Titre",
                    ProposedValue = "Nouveau Titre Pour Mobile",
                    BaseVersion = 1,
                    AuthorEmail = "member@org.com",
                    AuthorName = "Member",
                    Status = ContributionStatuses.Proposed
                };
                contrib.Votes.Add(new Vote { VoterEmail = "admin@org.com", Approve = true });
                db.Contributions.Add(contrib);
                await db.SaveChangesAsync();
            }

            var mergeService = new MergeService(() => new AppDbContext(_options), fakeStore);
            var result = await mergeService.MergeProposalAsync(proposalId);

            Assert.True(result.Success);
            // Verify that sync/projects.json was uploaded with the newly merged title
            Assert.NotNull(fakeStore.LastUploadedProjectsJson);
            Assert.Contains("Nouveau Titre Pour Mobile", fakeStore.LastUploadedProjectsJson);
        }

        [Fact]
        public async Task BackwardCompatibility_ExistingSQLiteTablesAndEntities_RemainFullyFunctional()
        {
            using var db = new AppDbContext(_options);

            // Create baseline project with associated logic model items
            var project = new Project
            {
                Title = "Compat Test Project",
                GeneralObjective = "General Objective"
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var objective = new Objective
            {
                ProjectId = project.Id,
                Code = "OS1",
                Description = "Specific Objective"
            };
            db.Objectives.Add(objective);
            await db.SaveChangesAsync();

            var result = new Result
            {
                ObjectiveId = objective.Id,
                Code = "R1",
                Description = "Expected Result"
            };
            db.Results.Add(result);
            await db.SaveChangesAsync();

            var activity = new Activity
            {
                ResultId = result.Id,
                Code = "A1",
                Description = "Activity Details"
            };
            db.Activities.Add(activity);

            var auditLog = new AuditLog
            {
                Action = "Test Action",
                Details = "Test Details",
                UserEmail = "test@domain.com"
            };
            db.AuditLogs.Add(auditLog);

            await db.SaveChangesAsync();

            // Assert everything is queryable
            Assert.True(await db.Projects.AnyAsync(p => p.Id == project.Id));
            Assert.True(await db.Objectives.AnyAsync(o => o.Id == objective.Id));
            Assert.True(await db.Results.AnyAsync(r => r.Id == result.Id));
            Assert.True(await db.Activities.AnyAsync(a => a.Id == activity.Id));
            Assert.True(await db.AuditLogs.AnyAsync(l => l.Id == auditLog.Id));
        }
    }

    public class TrackingCollabStore : InMemoryFakeCollabStore
    {
        public string? LastUploadedProjectsJson { get; private set; }

        public override Task UploadSyncProjectsJsonAsync(string projectsJson, CancellationToken cancellationToken = default)
        {
            LastUploadedProjectsJson = projectsJson;
            return Task.CompletedTask;
        }
    }
}
