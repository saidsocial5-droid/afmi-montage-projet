# Ce script supprime toutes les données locales générées lors du développement
# pour s'assurer que l'installateur (Setup.exe) est propre.

$ErrorActionPreference = "Continue"

Write-Host "--- Nettoyage des données de développement ---" -ForegroundColor Cyan

$roaming = [Environment]::GetFolderPath('ApplicationData')
$local = [Environment]::GetFolderPath('LocalApplicationData')

$appRoaming = Join-Path $roaming "AFMI_Montage_Projet"
$appLocal = Join-Path $local "AFMI_Montage_Projet"
$legacySettings = Join-Path $roaming "AFMIV3"
$legacyAuth = Join-Path $roaming "AFMI_ProjectManager"

function Clean-Dir {
    param([string]$path)
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path
        Write-Host "[OK] Supprimé : $path" -ForegroundColor Green
    } else {
        Write-Host "[--] Introuvable (déjà propre) : $path" -ForegroundColor Gray
    }
}

Clean-Dir $appRoaming
Clean-Dir $appLocal
Clean-Dir $legacySettings
Clean-Dir $legacyAuth

Write-Host "Nettoyage terminé ! Votre environnement est prêt pour la génération de l'installateur." -ForegroundColor Cyan
