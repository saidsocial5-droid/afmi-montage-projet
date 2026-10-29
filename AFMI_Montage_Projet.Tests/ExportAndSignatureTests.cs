using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Collab;

namespace AFMI_Montage_Projet.Tests
{
    public class ExportAndSignatureTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public ExportAndSignatureTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_exp_test_{Guid.NewGuid():N}.db");
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var db = new AppDbContext(_dbOptions);
            db.Database.EnsureCreated();
            db.SeedDefaultData();
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            }
            catch { }
        }

        [Fact]
        public void DeviceSignature_ValidatesCorrectly()
        {
            string secret = "AFMI_TEST_KEY_123456789";
            var proposal = new ProposalDto
            {
                Id = Guid.NewGuid(),
                EntityType = "Objective",
                EntityId = "1",
                FieldName = "Description",
                ProposedValue = "Nouvel objectif signé",
                AuthorEmail = "user@afmi.dz",
                DeviceId = "DEV_ANDROID_001"
            };

            // Compute signature
            proposal.DeviceSignature = MergeService.ComputeProposalSignature(proposal, secret);
            Assert.False(string.IsNullOrEmpty(proposal.DeviceSignature));

            // Verify matches
            bool isValid = MergeService.VerifyProposalSignature(proposal, secret);
            Assert.True(isValid);

            // Verify tampering fails
            proposal.ProposedValue = "Valeur altérée par un tiers";
            bool isTamperedValid = MergeService.VerifyProposalSignature(proposal, secret);
            Assert.False(isTamperedValid);
        }

        [Fact]
        public void VoteSignature_ValidatesCorrectly()
        {
            string secret = "AFMI_TEST_KEY_VOTE";
            var vote = new VoteDto
            {
                Id = Guid.NewGuid(),
                ProposalId = Guid.NewGuid(),
                VoterEmail = "voter@afmi.dz",
                Approve = true,
                DeviceId = "DEV_DESKTOP_002"
            };

            vote.DeviceSignature = MergeService.ComputeVoteSignature(vote, secret);
            Assert.True(MergeService.VerifyVoteSignature(vote, secret));

            // Wrong secret fails
            Assert.False(MergeService.VerifyVoteSignature(vote, "WRONG_SECRET"));
        }
    }
}
