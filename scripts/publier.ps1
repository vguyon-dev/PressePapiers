<#
.SYNOPSIS
    Publie PressePapiers (Release, win-x64, autonome) et l'installe dans le dossier des programmes de l'utilisateur.

.DESCRIPTION
    1. dotnet publish du projet de l'application dans un dossier de publication du dépôt (publish\, ignoré par git).
    2. Arrêt de l'instance en cours, s'il y en a une : ses fichiers seraient verrouillés pendant la copie.
    3. Copie miroir de la publication dans le dossier d'installation (par défaut %LOCALAPPDATA%\Programs\PressePapiers).
    4. Lancement de la version installée si -Lancer est donné.

    Aucun droit administrateur : tout se passe dans le profil de l'utilisateur. Les données (historique, images,
    réglages, journal) sont dans %LOCALAPPDATA%\PressePapiers, un autre dossier, que le script ne touche jamais.
    Au premier lancement de la version installée, l'application inscrit son propre chemin dans la clé Run de
    l'utilisateur si le démarrage avec Windows est activé dans ses réglages (activé par défaut).

.PARAMETER Destination
    Dossier d'installation. Par défaut : %LOCALAPPDATA%\Programs\PressePapiers.

.PARAMETER Lancer
    Lance la version installée une fois la copie faite.

.EXAMPLE
    pwsh -NoProfile -File scripts\publier.ps1 -Lancer
#>
param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\PressePapiers'),
    [switch]$Lancer
)

$ErrorActionPreference = 'Stop'

$depot = Split-Path -Parent $PSScriptRoot
$projet = Join-Path $depot 'src\PressePapiers\PressePapiers.csproj'
$publication = Join-Path $depot 'publish\PressePapiers'
$donnees = Join-Path $env:LOCALAPPDATA 'PressePapiers'

# Garde-fou : la copie miroir supprime ce qui n'est pas dans la publication, elle ne doit jamais viser les données.
$cible = [System.IO.Path]::GetFullPath($Destination).TrimEnd('\')
if ($cible -ieq [System.IO.Path]::GetFullPath($donnees).TrimEnd('\') -or $cible -ieq [System.IO.Path]::GetFullPath($depot).TrimEnd('\')) {
    throw "Destination refusée : $cible est le dossier des données ou le dépôt."
}

Write-Host "Publication de $projet ..."
if (Test-Path $publication) {
    Remove-Item -Recurse -Force $publication
}
dotnet publish $projet -c Release -r win-x64 -p:Platform=x64 -o $publication --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish a échoué (code $LASTEXITCODE)."
}

$taille = (Get-ChildItem -Recurse -File $publication | Measure-Object -Property Length -Sum).Sum
Write-Host ("Publication : {0:N1} Mo dans {1}" -f ($taille / 1MB), $publication)

# Une seule instance par utilisateur : celle qui tourne verrouille ses fichiers. L'arrêt est brutal (l'application n'a
# pas de commande d'arrêt externe) : son icône peut rester affichée jusqu'au survol de la souris.
$enCours = @(Get-Process -Name 'PressePapiers' -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq (Get-Process -Id $PID).SessionId })
foreach ($processus in $enCours) {
    Write-Host "Arrêt de l'instance en cours (processus $($processus.Id)) ..."
    Stop-Process -Id $processus.Id -Force
    $processus.WaitForExit(10000) | Out-Null
}

New-Item -ItemType Directory -Force -Path $cible | Out-Null
Write-Host "Copie vers $cible ..."
robocopy $publication $cible /MIR /NFL /NDL /NJH /NJS /NP /R:3 /W:1 | Out-Null
# Codes de robocopy : 0 à 7 = réussite (avec ou sans fichiers copiés), 8 et plus = échec.
if ($LASTEXITCODE -ge 8) {
    throw "La copie vers $cible a échoué (code robocopy $LASTEXITCODE)."
}

$executable = Join-Path $cible 'PressePapiers.exe'
Write-Host "Installé : $executable"

if ($Lancer) {
    Start-Process -FilePath $executable -WorkingDirectory $cible
    Write-Host 'PressePapiers est lancé (icône dans la zone de notification).'
}

# Le code de robocopy (1 = fichiers copiés) ne doit pas devenir celui du script.
exit 0
