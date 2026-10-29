# Génération, signature (Code Signing) et création de l'installateur AFMI Montage Projet.
#
# Prérequis : SDK .NET 9, Inno Setup 6, signtool (Windows SDK) si la signature est demandée.
#
# Exemples :
#   .\build_and_sign.ps1                                   # build + installateur, sans signature
#   .\build_and_sign.ps1 -CertThumbprint "AB12..."         # certificat du magasin Windows (recommandé)
#   .\build_and_sign.ps1 -PfxPath C:\certs\afmi.pfx        # fichier PFX (mot de passe demandé de façon masquée)

#   .\build_and_sign.ps1 -GitHubRepository proprietaire/depot  # dépôt des mises à jour intégré à l'application
#
# Publication sur GitHub (recommandé) : l'envoi d'une étiquette vX.Y.Z déclenche .github/workflows/release.yml,
# qui produit et publie le même installateur ; ce script sert aux constructions locales.

param(
    [string]$GitHubRepository = "",
    [switch]$SkipTests,  # tests déjà exécutés (certains appellent l'IA locale réelle et ne sont pas déterministes)
    [string]$CertThumbprint = "",
    [string]$PfxPath = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"

$ProjectDir  = $PSScriptRoot
$ProjectFile = Join-Path $ProjectDir "AFMI Montage projet 2.csproj"
$PublishDir  = Join-Path $ProjectDir "bin\Release\net9.0-windows\win-x64\publish"
$IssFile     = Join-Path $ProjectDir "setup.iss"
$OutputDir   = Join-Path $ProjectDir "Installer"

function Invoke-Sign([string]$file) {
    if (-not $CertThumbprint -and -not $PfxPath) { return }
    if (-not (Get-Command signtool -ErrorAction SilentlyContinue)) {
        Write-Host "Avertissement : signtool introuvable (utilisez le Developer PowerShell / Windows SDK). Fichier non signé : $file" -ForegroundColor Yellow
        return
    }

    $args = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256")
    if ($CertThumbprint) {
        $args += @("/sha1", $CertThumbprint)
    } else {
        $securePwd = Read-Host "Mot de passe du certificat PFX" -AsSecureString
        $plainPwd = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePwd))
        $args += @("/f", $PfxPath, "/p", $plainPwd)
    }
    & signtool @args $file
    if ($LASTEXITCODE -ne 0) { throw "Échec de la signature de $file" }
    Write-Host "Signé : $file" -ForegroundColor Green
}

# Vérification de cohérence des versions (.csproj <-> setup.iss)
$csprojVersion = ([xml](Get-Content $ProjectFile)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$issVersion = (Select-String -Path $IssFile -Pattern '#define MyAppVersion "([^"]+)"').Matches[0].Groups[1].Value
if ($csprojVersion -ne $issVersion) {
    throw "Version incohérente : .csproj = $csprojVersion, setup.iss = $issVersion. Alignez-les avant de publier."
}
Write-Host "Version $csprojVersion" -ForegroundColor Cyan

if ($GitHubRepository) {
    $GitHubRepository = $GitHubRepository -replace '^https?://(www\.)?github\.com/', '' -replace '\.git$', '' -replace '/$', ''
    if ($GitHubRepository -notmatch '^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$') { throw "Dépôt GitHub invalide : $GitHubRepository (attendu : proprietaire/depot)" }
    @{ GitHubRepository = $GitHubRepository } | ConvertTo-Json | Out-File (Join-Path $ProjectDir "Config\update.json") -Encoding utf8
    Write-Host "Dépôt des mises à jour : $GitHubRepository" -ForegroundColor Cyan
}

if ($SkipTests) {
    Write-Host "1. Tests unitaires ignorés (-SkipTests)." -ForegroundColor Yellow
} else {
    Write-Host "1. Tests unitaires..." -ForegroundColor Cyan
    dotnet test (Join-Path $ProjectDir "AFMI_Montage_Projet.Tests")
    if ($LASTEXITCODE -ne 0) { throw "Des tests échouent : publication annulée." }
}

Write-Host "2. Publication de l'application (Self-Contained win-x64)..." -ForegroundColor Cyan
dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { throw "Échec de dotnet publish." }

Write-Host "3. Signature de l'exécutable..." -ForegroundColor Cyan
Invoke-Sign (Join-Path $PublishDir "AFMI Montage projet 2.exe")

Write-Host "4. Création de l'installateur (Inno Setup)..." -ForegroundColor Cyan
if (-not (Test-Path $InnoSetupCompiler)) { throw "Inno Setup introuvable : $InnoSetupCompiler" }
& $InnoSetupCompiler $IssFile
if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation Inno Setup." }

Write-Host "5. Signature de l'installateur..." -ForegroundColor Cyan
$setupExe = Join-Path $OutputDir "AFMIMontageProjet_Setup_v$csprojVersion.exe"
if (Test-Path $setupExe) { Invoke-Sign $setupExe } else { throw "Installateur attendu introuvable : $setupExe" }

Write-Host "6. Empreinte SHA-256 (exigée par la mise à jour automatique)..." -ForegroundColor Cyan
$hash = (Get-FileHash $setupExe -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $setupExe -Leaf)" | Out-File "$setupExe.sha256" -Encoding ascii

Write-Host "Terminé. Installateur : $setupExe" -ForegroundColor Green
Write-Host "SHA-256 : $hash" -ForegroundColor Green
Write-Host "Pour une publication GitHub manuelle, joindre à la « Release » vX.Y.Z les fichiers .exe et .exe.sha256." -ForegroundColor Cyan
