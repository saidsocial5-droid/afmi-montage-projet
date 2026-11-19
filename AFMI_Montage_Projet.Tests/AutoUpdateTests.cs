using System;
using System.IO;
using AFMI_Montage_projet_2.Services.Updates;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// ADR 29 : mise à jour automatique. L'installateur vérifié est lancé une seule fois à la fermeture,
    /// jamais s'il a été modifié depuis sa vérification, et jamais après une installation immédiate.
    /// </summary>
    public class AutoUpdateTests : IDisposable
    {
        private readonly string _installer = Path.Combine(TestEnvironment.DataDirectory, "AFMIMontageProjet_Setup_v99.0.0.exe");

        public AutoUpdateTests()
        {
            AutoUpdateCoordinator.Reset();
            File.WriteAllText(_installer, "installateur de test");
        }

        public void Dispose()
        {
            AutoUpdateCoordinator.Reset();
            if (File.Exists(_installer)) File.Delete(_installer);
        }

        [Fact]
        public void AutoInstall_IsEnabledByDefault_AndCanBeTurnedOff()
        {
            try
            {
                Assert.True(AutoUpdateCoordinator.AutoInstallEnabled);
                AutoUpdateCoordinator.AutoInstallEnabled = false;
                Assert.False(AutoUpdateCoordinator.AutoInstallEnabled);
            }
            finally
            {
                AutoUpdateCoordinator.AutoInstallEnabled = true;
            }
        }

        [Fact]
        public void TokenPrompt_CanBeDisabledAndReenabled()
        {
            try
            {
                Assert.True(AutoUpdateCoordinator.TokenPromptEnabled);
                AutoUpdateCoordinator.TokenPromptEnabled = false;
                Assert.False(AutoUpdateCoordinator.TokenPromptEnabled);
            }
            finally
            {
                AutoUpdateCoordinator.TokenPromptEnabled = true;
            }
        }

        [Fact]
        public void PendingInstaller_IsLaunchedOnceOnExit_Silently_WithoutRestart()
        {
            AutoUpdateCoordinator.SetPending(_installer, GitHubUpdateService.ComputeSha256(_installer), new Version(99, 0, 0));
            int launches = 0;
            string? arguments = null;

            bool first = AutoUpdateCoordinator.InstallPendingOnExit((path, args) => { launches++; arguments = args; return true; });
            bool second = AutoUpdateCoordinator.InstallPendingOnExit((_, _) => { launches++; return true; });

            Assert.True(first);
            Assert.False(second);
            Assert.Equal(1, launches);
            Assert.Contains("/SILENT", arguments);
            Assert.Contains("/NORESTARTAPPLICATIONS", arguments);
        }

        [Fact]
        public void PendingInstaller_ModifiedAfterVerification_IsDeletedAndNotLaunched()
        {
            AutoUpdateCoordinator.SetPending(_installer, GitHubUpdateService.ComputeSha256(_installer), new Version(99, 0, 0));
            File.AppendAllText(_installer, "contenu ajouté");
            bool launched = false;

            bool result = AutoUpdateCoordinator.InstallPendingOnExit((_, _) => launched = true);

            Assert.False(result);
            Assert.False(launched);
            Assert.False(File.Exists(_installer));
        }

        [Fact]
        public void NothingIsLaunchedOnExit_AfterImmediateInstall_OrWithoutPendingUpdate()
        {
            bool launched = false;
            Assert.False(AutoUpdateCoordinator.InstallPendingOnExit((_, _) => launched = true));

            AutoUpdateCoordinator.SetPending(_installer, GitHubUpdateService.ComputeSha256(_installer), new Version(99, 0, 0));
            AutoUpdateCoordinator.MarkInstallerLaunched();
            Assert.False(AutoUpdateCoordinator.InstallPendingOnExit((_, _) => launched = true));
            Assert.False(launched);
        }
    }
}
