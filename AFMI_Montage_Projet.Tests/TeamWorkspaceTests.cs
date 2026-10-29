using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Espace d'équipe (ADR 23) : version officielle assainie, copie membre, détection des contributions,
    /// décision de l'administrateur et intégration par le poste officiel.
    /// </summary>
    public class TeamWorkspaceTests : IDisposable
    {
        private const string Owner = "projet.afmi@gmail.com";
        private const string SecondAdmin = "said.afmi@gmail.com";
        private const string MemberEmail = "membre.equipe@gmail.com";

        private readonly string _dir;
        private readonly string _officialDb;
        private readonly string _memberDb;
        private readonly string _baseDb;
        private readonly TeamConfig _team;

        public TeamWorkspaceTests()
        {
            _dir = Path.Combine(TestEnvironment.DataDirectory, "team_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _officialDb = Path.Combine(_dir, "official.db");
            _memberDb = Path.Combine(_dir, "member.db");
            _baseDb = Path.Combine(_dir, "official_base.db");
            _team = new TeamConfig
            {
                QuorumRule = QuorumRules.AdminDecision,
                Members = new List<TeamMemberDto> { new() { Email = MemberEmail, Name = "Membre", Role = TeamRoles.Member } }
            };
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        }

        private static AppDbContext Ctx(string path) =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);

        private static void CreateDb(string path)
        {
            using var db = Ctx(path);
            db.Database.EnsureCreated();
        }

        private AuthService MemberAuth() =>
            AuthService.CreateDetached(new UserProfile { Email = MemberEmail, DisplayName = "Membre", IsAuthenticated = true }, _team, isOfficialStation: false);

        private AuthService AdminAuth(string email = Owner, bool official = true) =>
            AuthService.CreateDetached(new UserProfile { Email = email, DisplayName = "Admin", IsAuthenticated = true }, _team, isOfficialStation: official);

        /// <summary>Base officielle : un projet, un risque, plus des données propres au poste (compte, réglage secret).</summary>
        private (string projectGid, int projectId) SeedOfficial()
        {
            CreateDb(_officialDb);
            using var db = Ctx(_officialDb);
            var project = new Project { Title = "Projet Forêt", GeneralObjective = "Préserver la cédraie", Currency = "MAD" };
            db.Projects.Add(project);
            db.SaveChanges();
            db.Risks.Add(new Risk { ProjectId = project.Id, Description = "Incendie", Probability = 4, Impact = 5 });
            db.Users.Add(new User { Username = "admin", Email = SecondAdmin, PasswordHash = "mot-de-passe-secret", Status = "Active", Role = "SuperAdmin" });
            db.AppSettings.Add(new AppSetting { Key = "Google_ClientSecret", Value = "secret-chiffre" });
            db.SaveChanges();
            return (project.GlobalId, project.Id);
        }

        /// <summary>Publie la base officielle et l'applique sur le poste membre (comme « Mettre à jour mes projets »).</summary>
        private void PublishToMember()
        {
            string snapshot = Path.Combine(_dir, $"snap_{Guid.NewGuid():N}.db");
            OfficialSnapshotService.CreateSanitizedSnapshot(_officialDb, snapshot);
            if (!File.Exists(_memberDb)) CreateDb(_memberDb);
            OfficialSnapshotService.ApplySnapshotToLocal(snapshot, _memberDb);
            File.Copy(snapshot, _baseDb, overwrite: true);
        }

        /// <summary>Simule la synchronisation : les fichiers de propositions arrivent dans la base du poste officiel.</summary>
        private static async Task DeliverProposalsAsync(InMemoryFakeCollabStore store, string dbPath)
        {
            using var db = Ctx(dbPath);
            foreach (var dto in await store.DownloadProposalsAsync())
            {
                var existing = await db.Contributions.FindAsync(dto.Id);
                if (existing == null) db.Contributions.Add(ProposalMapper.ToEntity(dto));
                else existing.Status = dto.Status;
            }
            await db.SaveChangesAsync();
        }

        private static long Count(string path, string table)
        {
            using var conn = new SqliteConnection($"Data Source={path};Pooling=False");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            return (long)cmd.ExecuteScalar()!;
        }

        [Fact]
        public void TeamConfig_TwoAssociationAdmins_AlwaysAdmin_OthersFromTeamFile()
        {
            var config = new TeamConfig { Members = new List<TeamMemberDto> { new() { Email = MemberEmail, Role = TeamRoles.Member } } };

            Assert.True(config.IsAdmin(Owner));
            Assert.True(config.IsAdmin("SAID.AFMI@gmail.com"));
            Assert.True(config.CanDecide(SecondAdmin));
            Assert.Equal(TeamRoles.Member, config.RoleOf(MemberEmail));
            Assert.False(config.CanDecide(MemberEmail));
            Assert.True(config.CanPropose(MemberEmail));
            Assert.Equal(TeamRoles.Guest, config.RoleOf("inconnu@gmail.com"));
            Assert.False(config.CanPropose("inconnu@gmail.com"));
        }

        [Fact]
        public void OfficialSnapshot_NeverContainsAccountsSettingsOrSecrets()
        {
            SeedOfficial();
            string snapshot = Path.Combine(_dir, "snap.db");

            int projects = OfficialSnapshotService.CreateSanitizedSnapshot(_officialDb, snapshot);

            Assert.Equal(1, projects);
            Assert.Equal(0, Count(snapshot, "Users"));
            Assert.Equal(0, Count(snapshot, "AppSettings"));
            Assert.Equal(1, Count(snapshot, "Risks"));
            // La base officielle elle-même est intacte
            Assert.Equal(1, Count(_officialDb, "Users"));
            Assert.Equal(1, Count(_officialDb, "AppSettings"));
        }

        [Fact]
        public void ApplySnapshot_ReplacesProjects_KeepsMemberAccountsAndSettings_AndBacksUp()
        {
            SeedOfficial();
            CreateDb(_memberDb);
            using (var db = Ctx(_memberDb))
            {
                db.Projects.Add(new Project { Title = "Projet personnel" });
                db.Users.Add(new User { Username = "membre", Email = MemberEmail, PasswordHash = "x", Status = "Active", Role = "Reader" });
                db.AppSettings.Add(new AppSetting { Key = "Reglage_local", Value = "conserve" });
                db.SaveChanges();
            }

            string snapshot = Path.Combine(_dir, "snap.db");
            OfficialSnapshotService.CreateSanitizedSnapshot(_officialDb, snapshot);
            var result = OfficialSnapshotService.ApplySnapshotToLocal(snapshot, _memberDb);

            Assert.True(File.Exists(result.BackupPath));
            using var check = Ctx(_memberDb);
            Assert.Equal(new[] { "Projet Forêt" }, check.Projects.Select(p => p.Title).ToArray());
            Assert.Single(check.Risks);
            Assert.Equal(MemberEmail, Assert.Single(check.Users).Email);
            Assert.Contains(check.AppSettings, s => s.Key == "Reglage_local" && s.Value == "conserve");
            Assert.DoesNotContain(check.AppSettings, s => s.Key == "Google_ClientSecret");
        }

        [Fact]
        public void Detector_FindsFieldEditsAndNewItems_WithoutFalsePositives()
        {
            var (projectGid, _) = SeedOfficial();
            PublishToMember();
            Assert.Empty(TeamChangeDetector.Detect(_baseDb, _memberDb));

            string newRiskGid;
            using (var db = Ctx(_memberDb))
            {
                var project = db.Projects.Single();
                project.Title = "Projet Forêt durable";
                var risk = new Risk { ProjectId = project.Id, Description = "Sécheresse prolongée", Probability = 3, Impact = 4 };
                db.Risks.Add(risk);
                db.SaveChanges();
                newRiskGid = risk.GlobalId;
            }

            var changes = TeamChangeDetector.Detect(_baseDb, _memberDb);

            Assert.Equal(2, changes.Count);
            var edit = Assert.Single(changes, c => !c.IsCreation);
            Assert.Equal("Project", edit.EntityType);
            Assert.Equal(projectGid, edit.EntityId);
            Assert.Equal("Title", edit.FieldName);
            Assert.Equal("Projet Forêt", edit.BaseValue);
            Assert.Equal("Projet Forêt durable", edit.NewValue);
            Assert.Equal(projectGid, edit.ProjectGlobalId);

            var creation = Assert.Single(changes, c => c.IsCreation);
            Assert.Equal("Risk", creation.EntityType);
            Assert.Equal(newRiskGid, creation.EntityId);
            var payload = JsonSerializer.Deserialize<CreationPayload>(creation.NewValue!)!;
            Assert.Equal(projectGid, payload.ParentGlobalIds["ProjectId"]);
            Assert.Equal("Sécheresse prolongée", payload.Columns["Description"]);
        }

        [Fact]
        public async Task EndToEnd_MemberProposes_AdminApprovesOnOfficialStation_ThenMemberReceivesIt()
        {
            var (projectGid, projectId) = SeedOfficial();
            PublishToMember();
            using (var db = Ctx(_memberDb))
            {
                var project = db.Projects.Single();
                project.GeneralObjective = "Préserver et restaurer la cédraie du Moyen Atlas";
                db.Risks.Add(new Risk { ProjectId = project.Id, Description = "Surpâturage", Probability = 4, Impact = 4 });
                db.SaveChanges();
            }

            // Poste membre : envoi des propositions (les doublons ne sont pas renvoyés)
            var store = new InMemoryFakeCollabStore();
            var memberService = new ContributionService(() => Ctx(_memberDb), store, MemberAuth());
            var changes = TeamChangeDetector.Detect(_baseDb, _memberDb);
            Assert.Equal(2, await memberService.SubmitDetectedChangesAsync(changes));
            Assert.Equal(0, await memberService.SubmitDetectedChangesAsync(changes));
            Assert.Equal(2, (await store.DownloadProposalsAsync()).Count);

            // Poste officiel : décision de l'administrateur, intégration immédiate
            await DeliverProposalsAsync(store, _officialDb);
            var merge = new MergeService(() => Ctx(_officialDb), store, AdminAuth());
            foreach (var p in (await store.DownloadProposalsAsync()).OrderByDescending(p => p.FieldName == ProposalFields.Create))
            {
                var result = await merge.ApproveAsync(p.Id);
                Assert.True(result.Success, result.Message);
                Assert.True(result.OfficialDataChanged);
            }

            using (var db = Ctx(_officialDb))
            {
                var project = db.Projects.Single();
                Assert.Equal("Préserver et restaurer la cédraie du Moyen Atlas", project.GeneralObjective);
                Assert.Equal(2, project.Version);
                Assert.Equal(MemberEmail, project.UpdatedByEmail);
                var added = db.Risks.Single(r => r.Description == "Surpâturage");
                Assert.Equal(projectId, added.ProjectId);
                Assert.Equal(MemberEmail, added.AuthorEmail);
                Assert.All(db.Contributions, c => Assert.Equal(ContributionStatuses.Approved, c.Status));
            }
            Assert.All(await store.DownloadProposalsAsync(), p => Assert.Equal(ContributionStatuses.Approved, p.Status));

            // Nouvelle publication : le membre reçoit la version intégrée, plus aucune modification en attente
            PublishToMember();
            Assert.Empty(TeamChangeDetector.Detect(_baseDb, _memberDb));
            using (var db = Ctx(_memberDb))
            {
                Assert.Equal(2, db.Risks.Count());
                Assert.Equal(projectGid, db.Projects.Single().GlobalId);
            }
        }

        [Fact]
        public async Task Member_CannotApprove_OrReject()
        {
            SeedOfficial();
            var store = new InMemoryFakeCollabStore();
            var service = new ContributionService(() => Ctx(_officialDb), store, MemberAuth());
            var note = await service.CreateNoteAsync("gid", "Projet Forêt", "Ajouter un volet éducation environnementale.");

            var merge = new MergeService(() => Ctx(_officialDb), store, MemberAuth());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => merge.ApproveAsync(note.Id));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => merge.RejectAsync(note.Id));
        }

        [Fact]
        public async Task GuestAccount_CannotPropose()
        {
            SeedOfficial();
            var guest = AuthService.CreateDetached(new UserProfile { Email = "inconnu@gmail.com", IsAuthenticated = true }, _team, false);
            var service = new ContributionService(() => Ctx(_officialDb), new InMemoryFakeCollabStore(), guest);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateNoteAsync("gid", "P", "texte"));
        }

        [Fact]
        public async Task AdminOnOtherStation_Accepts_ThenOfficialStationIntegrates()
        {
            var (projectGid, _) = SeedOfficial();
            var store = new InMemoryFakeCollabStore();
            var memberService = new ContributionService(() => Ctx(_officialDb), store, MemberAuth());
            var proposal = await memberService.CreateProposalAsync("Project", projectGid, "Title", "Projet Forêt", "Projet Forêt Ifrane", 1);

            // Second administrateur, depuis son propre poste : décision seulement
            var remoteAdmin = new MergeService(() => Ctx(_officialDb), store, AdminAuth(SecondAdmin, official: false));
            var decision = await remoteAdmin.ApproveAsync(proposal.Id, "D'accord");
            Assert.True(decision.Success);
            Assert.False(decision.OfficialDataChanged);
            using (var db = Ctx(_officialDb))
            {
                Assert.Equal("Projet Forêt", db.Projects.Single().Title);
                Assert.Equal(ContributionStatuses.Accepted, db.Contributions.Single().Status);
            }

            // Poste officiel : intégration lors de la synchronisation
            var official = new MergeService(() => Ctx(_officialDb), store, AdminAuth(Owner, official: true));
            var results = await official.ApplyAcceptedProposalsAsync();
            Assert.True(Assert.Single(results).OfficialDataChanged);
            using (var db = Ctx(_officialDb))
            {
                Assert.Equal("Projet Forêt Ifrane", db.Projects.Single().Title);
                var c = db.Contributions.Single();
                Assert.Equal(ContributionStatuses.Approved, c.Status);
                Assert.Equal(SecondAdmin, c.ReviewedByEmail);
            }
        }

        [Fact]
        public async Task Approve_DetectsChangedOfficialValue_ThenForceApplies()
        {
            var (projectGid, _) = SeedOfficial();
            var store = new InMemoryFakeCollabStore();
            var memberService = new ContributionService(() => Ctx(_officialDb), store, MemberAuth());
            var proposal = await memberService.CreateProposalAsync("Project", projectGid, "Title", "Projet Forêt", "Titre du membre", 1);

            using (var db = Ctx(_officialDb))
            {
                db.Projects.Single().Title = "Titre modifié par l'administrateur";
                db.SaveChanges();
            }

            var merge = new MergeService(() => Ctx(_officialDb), store, AdminAuth());
            var first = await merge.ApproveAsync(proposal.Id);
            Assert.True(first.ConflictDetected);
            using (var db = Ctx(_officialDb))
            {
                Assert.Equal("Titre modifié par l'administrateur", db.Projects.Single().Title);
                Assert.Equal(ContributionStatuses.NeedsReview, db.Contributions.Single().Status);
            }

            var forced = await merge.ApproveAsync(proposal.Id, force: true);
            Assert.True(forced.Success, forced.Message);
            using (var db = Ctx(_officialDb))
            {
                Assert.Equal("Titre du membre", db.Projects.Single().Title);
            }
        }

        [Fact]
        public async Task Reject_RecordsReasonForAuthor()
        {
            SeedOfficial();
            var store = new InMemoryFakeCollabStore();
            var note = await new ContributionService(() => Ctx(_officialDb), store, MemberAuth())
                .CreateNoteAsync("gid", "Projet Forêt", "Supprimer le volet budget.");

            var result = await new MergeService(() => Ctx(_officialDb), store, AdminAuth()).RejectAsync(note.Id, "Le budget est exigé par le bailleur.");

            Assert.True(result.Success);
            var dto = Assert.Single(await store.DownloadProposalsAsync());
            Assert.Equal(ContributionStatuses.Rejected, dto.Status);
            Assert.Equal("Le budget est exigé par le bailleur.", dto.ReviewNotes);
            Assert.Equal(Owner, dto.ReviewedByEmail);
        }

        /// <summary>Stockage dont le flux de changements renvoie toutes les propositions, sans jeton à mémoriser.</summary>
        private sealed class ReplayAllChangesStore : InMemoryFakeCollabStore
        {
            public override async Task<CollabChangesResult> GetChangesAsync(string pageToken, CancellationToken cancellationToken = default) =>
                new() { NewPageToken = null, ModifiedProposals = await DownloadProposalsAsync(cancellationToken) };
        }

        [Fact]
        public async Task Sync_IgnoresForgedAuthors_AndDecisionsNotMadeByAdmins()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            using (var db = new AppDbContext(options)) db.Database.EnsureCreated();

            var store = new ReplayAllChangesStore();
            ProposalDto Dto(string author, string owner, string lastModifiedBy, string status) => new()
            {
                Id = Guid.NewGuid(), EntityType = "Project", EntityId = "p1", FieldName = "Title",
                ProposedValue = "x", AuthorEmail = author, AuthorName = author, CreatedAt = DateTime.UtcNow,
                Status = status, DriveOwnerEmail = owner, DriveLastModifiedBy = lastModifiedBy
            };
            var forgedAuthor = Dto(SecondAdmin, MemberEmail, MemberEmail, ContributionStatuses.Proposed);
            var selfApproved = Dto(MemberEmail, MemberEmail, MemberEmail, ContributionStatuses.Accepted);
            var adminApproved = Dto(MemberEmail, MemberEmail, SecondAdmin, ContributionStatuses.Accepted);
            foreach (var d in new[] { forgedAuthor, selfApproved, adminApproved }) await store.UploadProposalAsync(d);

            var sync = new SyncService(store, () => new AppDbContext(options), MemberAuth()) { PageToken = "token-1" };
            await sync.ExecuteSyncCycleAsync();

            using (var db = new AppDbContext(options))
            {
                Assert.Null(await db.Contributions.FindAsync(forgedAuthor.Id));
                Assert.Equal(ContributionStatuses.Proposed, (await db.Contributions.FindAsync(selfApproved.Id))!.Status);
                Assert.Equal(ContributionStatuses.Accepted, (await db.Contributions.FindAsync(adminApproved.Id))!.Status);
            }
            connection.Dispose();
        }
    }
}
