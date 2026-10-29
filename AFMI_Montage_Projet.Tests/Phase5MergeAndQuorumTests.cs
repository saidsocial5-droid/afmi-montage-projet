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
    public class Phase5MergeAndQuorumTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public Phase5MergeAndQuorumTests()
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
        public void QuorumEvaluation_UnanimousRule_RequiresAllMembersApproval()
        {
            var config = new TeamConfig
            {
                QuorumRule = QuorumRules.Unanimous,
                Members = new List<TeamMemberDto>
                {
                    new() { Email = "m1@org.com", Role = TeamRoles.Member },
                    new() { Email = "m2@org.com", Role = TeamRoles.Member },
                    new() { Email = "m3@org.com", Role = TeamRoles.Member }
                }
            };

            var contrib = new Contribution();

            // 1 vote only -> Not met
            contrib.Votes.Add(new Vote { VoterEmail = "m1@org.com", Approve = true });
            var res1 = MergeService.EvaluateQuorum(contrib, config);
            Assert.False(res1.IsQuorumMet);
            Assert.False(res1.IsRejected);

            // 1 reject -> Rejected!
            contrib.Votes.Add(new Vote { VoterEmail = "m2@org.com", Approve = false });
            var res2 = MergeService.EvaluateQuorum(contrib, config);
            Assert.False(res2.IsQuorumMet);
            Assert.True(res2.IsRejected);

            // Change reject to approve and add 3rd approve -> Met!
            contrib.Votes.Clear();
            contrib.Votes.Add(new Vote { VoterEmail = "m1@org.com", Approve = true });
            contrib.Votes.Add(new Vote { VoterEmail = "m2@org.com", Approve = true });
            contrib.Votes.Add(new Vote { VoterEmail = "m3@org.com", Approve = true });
            var res3 = MergeService.EvaluateQuorum(contrib, config);
            Assert.True(res3.IsQuorumMet);
            Assert.False(res3.IsRejected);
        }

        [Fact]
        public void QuorumEvaluation_MajorityRule_RequiresStrictMajority()
        {
            var config = new TeamConfig
            {
                QuorumRule = QuorumRules.Majority,
                Members = new List<TeamMemberDto>
                {
                    new() { Email = "m1@org.com", Role = TeamRoles.Member },
                    new() { Email = "m2@org.com", Role = TeamRoles.Member },
                    new() { Email = "m3@org.com", Role = TeamRoles.Member },
                    new() { Email = "m4@org.com", Role = TeamRoles.Member },
                    new() { Email = "m5@org.com", Role = TeamRoles.Member }
                }
            };

            var contrib = new Contribution();
            // 2 approves of 5 -> Not met
            contrib.Votes.Add(new Vote { VoterEmail = "m1@org.com", Approve = true });
            contrib.Votes.Add(new Vote { VoterEmail = "m2@org.com", Approve = true });
            var res1 = MergeService.EvaluateQuorum(contrib, config);
            Assert.False(res1.IsQuorumMet);

            // 3rd approve -> Majority met! (3 out of 5)
            contrib.Votes.Add(new Vote { VoterEmail = "m3@org.com", Approve = true });
            var res2 = MergeService.EvaluateQuorum(contrib, config);
            Assert.True(res2.IsQuorumMet);
        }

        [Fact]
        public void QuorumEvaluation_ThresholdRule_MeetsWhenThresholdReached()
        {
            var config = new TeamConfig
            {
                QuorumRule = QuorumRules.Threshold,
                QuorumThreshold = 2,
                Members = new List<TeamMemberDto>
                {
                    new() { Email = "m1@org.com", Role = TeamRoles.Member },
                    new() { Email = "m2@org.com", Role = TeamRoles.Member },
                    new() { Email = "m3@org.com", Role = TeamRoles.Member },
                    new() { Email = "m4@org.com", Role = TeamRoles.Member }
                }
            };

            var contrib = new Contribution();
            contrib.Votes.Add(new Vote { VoterEmail = "m1@org.com", Approve = true });
            contrib.Votes.Add(new Vote { VoterEmail = "m2@org.com", Approve = true });

            var res = MergeService.EvaluateQuorum(contrib, config);
            Assert.True(res.IsQuorumMet);
        }

        [Fact]
        public async Task MergeService_SuccessfullyMerges_WhenVersionMatchesOptimisticCheck()
        {
            var fakeStore = new InMemoryFakeCollabStore();
            var teamConfig = new TeamConfig
            {
                QuorumRule = QuorumRules.Threshold,
                QuorumThreshold = 1,
                Members = new List<TeamMemberDto>
                {
                    new() { Email = "admin@org.com", Role = TeamRoles.Admin }
                }
            };
            await fakeStore.SaveTeamConfigAsync(teamConfig);

            int projectId;
            var proposalId = Guid.NewGuid();

            // 1. إنشاء مشروع أصلي في SQLite بنسخة Version = 1
            using (var db = new AppDbContext(_options))
            {
                var project = new Project
                {
                    Title = "Titre Initial",
                    GeneralObjective = "Objectif",
                    Version = 1
                };
                db.Projects.Add(project);
                await db.SaveChangesAsync();
                projectId = project.Id;

                // 2. إنشاء مقترح بنسخة BaseVersion = 1
                var contrib = new Contribution
                {
                    Id = proposalId,
                    EntityType = "Project",
                    EntityId = projectId.ToString(),
                    FieldName = "Title",
                    BaseValue = "Titre Initial",
                    ProposedValue = "Titre Validé et Fusionné",
                    BaseVersion = 1,
                    AuthorEmail = "member@domain.com",
                    AuthorName = "Member A",
                    Status = ContributionStatuses.Proposed
                };
                contrib.Votes.Add(new Vote { VoterEmail = "admin@org.com", Approve = true });
                db.Contributions.Add(contrib);
                await db.SaveChangesAsync();
            }

            // Act: تنفيذ الدمج
            var mergeService = new MergeService(() => new AppDbContext(_options), fakeStore);
            var result = await mergeService.MergeProposalAsync(proposalId);

            // Assert:
            Assert.True(result.Success);
            Assert.Equal(2, result.NewEntityVersion);

            // التحقق من قاعدة البيانات
            using (var queryDb = new AppDbContext(_options))
            {
                var updatedProject = await queryDb.Projects.FindAsync(projectId);
                Assert.NotNull(updatedProject);
                Assert.Equal("Titre Validé et Fusionné", updatedProject.Title);
                Assert.Equal(2, updatedProject.Version);
                Assert.Equal("member@domain.com", updatedProject.UpdatedByEmail);

                var approvedContrib = await queryDb.Contributions.FindAsync(proposalId);
                Assert.NotNull(approvedContrib);
                Assert.Equal(ContributionStatuses.Approved, approvedContrib.Status);
            }
        }

        [Fact]
        public async Task MergeService_DetectsConcurrencyConflict_AndSetsNeedsReview()
        {
            var fakeStore = new InMemoryFakeCollabStore();
            var teamConfig = new TeamConfig
            {
                QuorumRule = QuorumRules.Threshold,
                QuorumThreshold = 1,
                Members = new List<TeamMemberDto>
                {
                    new() { Email = "admin@org.com", Role = TeamRoles.Admin }
                }
            };
            await fakeStore.SaveTeamConfigAsync(teamConfig);

            int projectId;
            var staleProposalId = Guid.NewGuid();

            using (var db = new AppDbContext(_options))
            {
                // المشروع تم تعديله ونسخته الحالية اصبحت Version = 2
                var project = new Project
                {
                    Title = "Titre Concurremment Modifié",
                    GeneralObjective = "Objectif",
                    Version = 2 // ⚠️ إصدار محدث
                };
                db.Projects.Add(project);
                await db.SaveChangesAsync();
                projectId = project.Id;

                // المقترح يستند إلى نسخة قديمة BaseVersion = 1
                var staleContrib = new Contribution
                {
                    Id = staleProposalId,
                    EntityType = "Project",
                    EntityId = projectId.ToString(),
                    FieldName = "Title",
                    BaseValue = "Titre Dépassé",
                    ProposedValue = "Titre en Conflit",
                    BaseVersion = 1, // ⚠️ غير متطابق
                    AuthorEmail = "stale@domain.com",
                    AuthorName = "Stale Author",
                    Status = ContributionStatuses.Proposed
                };
                staleContrib.Votes.Add(new Vote { VoterEmail = "admin@org.com", Approve = true });
                db.Contributions.Add(staleContrib);
                await db.SaveChangesAsync();
            }

            // Act: محاولة الدمج
            var mergeService = new MergeService(() => new AppDbContext(_options), fakeStore);
            var result = await mergeService.MergeProposalAsync(staleProposalId);

            // Assert:
            Assert.False(result.Success);
            Assert.True(result.ConflictDetected);

            // التحقق من قاعدة البيانات: لم يتم تعديل الكيان وتغيرت حالة المساهمة إلى NeedsReview
            using (var queryDb = new AppDbContext(_options))
            {
                var project = await queryDb.Projects.FindAsync(projectId);
                Assert.NotNull(project);
                Assert.Equal("Titre Concurremment Modifié", project.Title); // لم يكتب عليه!
                Assert.Equal(2, project.Version);

                var contrib = await queryDb.Contributions.FindAsync(staleProposalId);
                Assert.NotNull(contrib);
                Assert.Equal(ContributionStatuses.NeedsReview, contrib.Status);
                Assert.Contains("تعارض", contrib.ReviewNotes);
            }
        }
    }
}
