using System;
using System.Text;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Collab;
using AFMI_Montage_projet_2.ViewModels;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// ADR 31 : inscription Google. Le code d'accès d'équipe configure l'identifiant client sur le poste,
    /// et les erreurs OAuth courantes deviennent des consignes compréhensibles.
    /// </summary>
    public class GoogleRegistrationTests
    {
        private static string Code(string raw) => Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));

        [Fact]
        public void AccessCode_Valid_StoresClientCredentials()
        {
            string oldId = SettingsService.GetSetting("Google_ClientId", "");
            string oldSecret = SettingsService.GetSetting("Google_ClientSecret", "");
            try
            {
                Assert.True(AuthService.TryApplyAccessCode(Code("AFMI1|123-abc.apps.googleusercontent.com|secret-xyz"), out string error));
                Assert.Equal(string.Empty, error);
                Assert.True(AuthService.TryGetClientCredentials(out var id, out var secret));
                Assert.Equal("123-abc.apps.googleusercontent.com", id);
                Assert.Equal("secret-xyz", secret);
            }
            finally
            {
                SettingsService.SaveSetting("Google_ClientId", oldId);
                SettingsService.SaveSetting("Google_ClientSecret", oldSecret);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("pas du base64 !")]
        public void AccessCode_Invalid_IsRejectedWithMessage(string code)
        {
            Assert.False(AuthService.TryApplyAccessCode(code, out string error));
            Assert.Contains("invalide", error);
        }

        [Fact]
        public void AccessCode_MissingSecret_IsRejected()
        {
            Assert.False(AuthService.TryApplyAccessCode(Code("AFMI1|id-seul|"), out _));
        }

        [Theory]
        [InlineData("Error:\"invalid_client\", Description:\"The OAuth client was not found.\"", "identifiant client")]
        [InlineData("redirect_uri_mismatch", "Application de bureau")]
        [InlineData("access_denied", "utilisateurs de test")]
        [InlineData("", "aucune réponse")]
        public void GoogleErrors_AreExplained(string raw, string expected)
        {
            Assert.Contains(expected, RegisterViewModel.DescribeGoogleError(raw));
        }
    }
}
