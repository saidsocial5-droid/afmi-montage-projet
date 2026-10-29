using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Services.Collab;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    public class Phase2AuthAndTeamTests
    {
        [Fact]
        public void TeamRoles_RoleChecksWorkAsExpected()
        {
            var config = new TeamConfig
            {
                Members = new List<TeamMemberDto>
                {
                    new() { Email = "admin@org.com", Name = "Admin User", Role = TeamRoles.Admin },
                    new() { Email = "member@org.com", Name = "Regular Member", Role = TeamRoles.Member },
                    new() { Email = "reviewer@org.com", Name = "Auditor Reviewer", Role = TeamRoles.Reviewer }
                }
            };

            // Admin checks
            Assert.True(config.IsAdmin("admin@org.com"));
            Assert.True(config.IsAdmin("ADMIN@ORG.COM")); // Case-insensitive
            Assert.False(config.IsAdmin("member@org.com"));
            Assert.False(config.IsAdmin("reviewer@org.com"));

            // Propose checks (Admin and Member can propose, Reviewer cannot)
            Assert.True(config.CanPropose("admin@org.com"));
            Assert.True(config.CanPropose("member@org.com"));
            Assert.False(config.CanPropose("reviewer@org.com"));
            Assert.False(config.CanPropose("unknown@org.com"));

            // Vote checks (All active members can vote)
            Assert.True(config.CanVote("admin@org.com"));
            Assert.True(config.CanVote("member@org.com"));
            Assert.True(config.CanVote("reviewer@org.com"));
            Assert.False(config.CanVote("intruder@org.com"));
        }

        [Fact]
        public void TeamConfig_SerializesAndDeserializesCorrectly()
        {
            var original = new TeamConfig
            {
                TeamId = "test-team-1",
                TeamName = "Projet AFMI Équipe",
                QuorumRule = QuorumRules.Majority,
                QuorumThreshold = 3,
                UpdatedByEmail = "admin@org.com",
                Members = new List<TeamMemberDto>
                {
                    new() { Email = "admin@org.com", Name = "Admin", Role = TeamRoles.Admin },
                    new() { Email = "collab@org.com", Name = "Collab", Role = TeamRoles.Member }
                }
            };

            string json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            var deserialized = JsonSerializer.Deserialize<TeamConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(deserialized);
            Assert.Equal("test-team-1", deserialized.TeamId);
            Assert.Equal(QuorumRules.Majority, deserialized.QuorumRule);
            Assert.Equal(3, deserialized.QuorumThreshold);
            Assert.Equal(2, deserialized.Members.Count);
            Assert.Equal("admin@org.com", deserialized.FindMember("ADMIN@ORG.COM")?.Email);
        }

        [Fact]
        public void ComputeEmailHash_ProducesDeterministicNormalizedHash()
        {
            string email1 = "User.Name+Test@Gmail.COM ";
            string email2 = "user.name+test@gmail.com";

            string hash1 = GoogleDriveCollabStore.ComputeEmailHash(email1);
            string hash2 = GoogleDriveCollabStore.ComputeEmailHash(email2);

            Assert.Equal(hash1, hash2);
            Assert.Equal(16, hash1.Length);
            Assert.Matches("^[0-9a-f]{16}$", hash1);
        }

        [Fact]
        public void LockFileDto_DetectsExpirationCorrectly()
        {
            var activeLock = new LockFileDto
            {
                HolderEmail = "user@org.com",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };
            Assert.False(activeLock.IsExpired);

            var expiredLock = new LockFileDto
            {
                HolderEmail = "user@org.com",
                ExpiresAt = DateTime.UtcNow.AddSeconds(-5)
            };
            Assert.True(expiredLock.IsExpired);
        }

        [Fact]
        public async Task InMemoryFakeCollabStore_ImplementsDecoupledContract()
        {
            // إثبات إمكانية استخدام مزود تخزين بديل (مثل Firestore أو Fake) بفضل ICollabStore
            var fakeStore = new InMemoryFakeCollabStore();

            // 1. Team Config
            var config = new TeamConfig { TeamName = "Workspace Fake" };
            await fakeStore.SaveTeamConfigAsync(config);
            var loaded = await fakeStore.GetTeamConfigAsync();
            Assert.Equal("Workspace Fake", loaded?.TeamName);

            // 2. Proposal
            var proposal = new ProposalDto
            {
                Id = Guid.NewGuid(),
                EntityType = "Activity",
                FieldName = "Description",
                AuthorEmail = "user@domain.com",
                ProposedValue = "Test Value"
            };
            await fakeStore.UploadProposalAsync(proposal);
            var proposals = await fakeStore.DownloadProposalsAsync();
            Assert.Single(proposals);

            // 3. Locking
            var lockRes1 = await fakeStore.AcquireLockAsync("merge", "user1@org.com", "User 1", TimeSpan.FromMinutes(1));
            Assert.True(lockRes1.Acquired);

            // حيازة القفل من مستخدم آخر تفشل طالما القفل نشط
            var lockRes2 = await fakeStore.AcquireLockAsync("merge", "user2@org.com", "User 2", TimeSpan.FromMinutes(1));
            Assert.False(lockRes2.Acquired);

            // بعد فك القفل ينجح المستخدم الثاني
            await fakeStore.ReleaseLockAsync("merge", lockRes1.LockToken!);
            var lockRes3 = await fakeStore.AcquireLockAsync("merge", "user2@org.com", "User 2", TimeSpan.FromMinutes(1));
            Assert.True(lockRes3.Acquired);
        }
    }

    /// <summary>
    /// نموذج محاكاة خفيف في الذاكرة لواجهة ICollabStore يُستخدم للاختبارات ويثبت تجريد طبقة التخزين
    /// </summary>
    public class InMemoryFakeCollabStore : ICollabStore
    {
        private TeamConfig? _teamConfig;
        private readonly Dictionary<Guid, ProposalDto> _proposals = new();
        private readonly List<VoteDto> _votes = new();
        private LockFileDto? _activeLock;

        public Task<bool> EnsureWorkspaceStructureAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<TeamConfig?> GetTeamConfigAsync(CancellationToken cancellationToken = default) => Task.FromResult(_teamConfig);

        public Task SaveTeamConfigAsync(TeamConfig config, CancellationToken cancellationToken = default)
        {
            _teamConfig = config;
            return Task.CompletedTask;
        }

        public Task<bool> InviteMemberAsync(string email, string role, CancellationToken cancellationToken = default)
        {
            _teamConfig ??= new TeamConfig();
            _teamConfig.Members.Add(new TeamMemberDto { Email = email, Role = role });
            return Task.FromResult(true);
        }

        public Task<bool> RemoveMemberAsync(string email, CancellationToken cancellationToken = default)
        {
            _teamConfig?.Members.RemoveAll(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(true);
        }

        // Version officielle : fichier copié en mémoire
        private byte[]? _officialSnapshot;
        private OfficialSnapshotInfo? _officialInfo;

        public Task<OfficialSnapshotInfo> UploadOfficialSnapshotAsync(string dbFilePath, int projectCount, CancellationToken cancellationToken = default)
        {
            _officialSnapshot = System.IO.File.ReadAllBytes(dbFilePath);
            _officialInfo = new OfficialSnapshotInfo { FileId = "official", PublishedAtUtc = DateTime.UtcNow, PublishedBy = "projet.afmi@gmail.com", ProjectCount = projectCount };
            return Task.FromResult(_officialInfo);
        }

        public Task<OfficialSnapshotInfo?> GetOfficialSnapshotInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult(_officialInfo);

        public Task<OfficialSnapshotInfo?> DownloadOfficialSnapshotAsync(string destPath, CancellationToken cancellationToken = default)
        {
            if (_officialSnapshot == null) return Task.FromResult<OfficialSnapshotInfo?>(null);
            System.IO.File.WriteAllBytes(destPath, _officialSnapshot);
            return Task.FromResult(_officialInfo);
        }

        public Task<string> UploadProposalAsync(ProposalDto proposal, CancellationToken cancellationToken = default)
        {
            _proposals[proposal.Id] = proposal;
            return Task.FromResult(proposal.Id.ToString());
        }

        public virtual Task<List<ProposalDto>> DownloadProposalsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ProposalDto>(_proposals.Values));

        public Task<ProposalDto?> DownloadProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            _proposals.TryGetValue(proposalId, out var p);
            return Task.FromResult(p);
        }

        public Task<string> UploadVoteAsync(VoteDto vote, CancellationToken cancellationToken = default)
        {
            _votes.Add(vote);
            return Task.FromResult(vote.Id.ToString());
        }

        public Task<List<VoteDto>> DownloadVotesForProposalAsync(Guid proposalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_votes.FindAll(v => v.ProposalId == proposalId));

        public Task<List<VoteDto>> DownloadAllVotesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<VoteDto>(_votes));

        public Task<LockResult> AcquireLockAsync(string lockName, string holderEmail, string holderName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (_activeLock != null && !_activeLock.IsExpired)
            {
                if (string.Equals(_activeLock.HolderEmail, holderEmail, StringComparison.OrdinalIgnoreCase))
                {
                    _activeLock.ExpiresAt = DateTime.UtcNow.Add(timeout);
                    return Task.FromResult(LockResult.Success(_activeLock.LockId));
                }
                return Task.FromResult(LockResult.Failed(_activeLock.HolderEmail, "Lock held by someone else"));
            }

            _activeLock = new LockFileDto
            {
                HolderEmail = holderEmail,
                HolderName = holderName,
                ExpiresAt = DateTime.UtcNow.Add(timeout)
            };
            return Task.FromResult(LockResult.Success(_activeLock.LockId));
        }

        public Task ReleaseLockAsync(string lockName, string lockToken, CancellationToken cancellationToken = default)
        {
            _activeLock = null;
            return Task.CompletedTask;
        }

        public Task UploadSnapshotAsync(int version, string jsonContent, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> DownloadLatestSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public virtual Task UploadSyncProjectsJsonAsync(string projectsJson, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public virtual Task<string?> GetStartPageTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("token-1");

        public virtual Task<CollabChangesResult> GetChangesAsync(string pageToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CollabChangesResult
            {
                NewPageToken = "token-2",
                ModifiedProposals = new List<ProposalDto>(_proposals.Values),
                ModifiedVotes = new List<VoteDto>(_votes)
            });
    }
}
