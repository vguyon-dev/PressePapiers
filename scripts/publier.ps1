<#
.SYNOPSIS
    Publie PressePapiers (Release, win-x64, autonome) et l'installe dans le dossier des programmes de l'utilisateur.

.DESCRIPTION
    1. Contrôle de la destination (voir plus bas) : rien n'est publié ni arrêté si elle est refusée.
    2. dotnet publish du projet de l'application dans un dossier de publication du dépôt (publish\, ignoré par git).
    3. Arrêt de l'instance installée, s'il y en a une : ses fichiers seraient verrouillés pendant la copie.
    4. Copie miroir de la publication dans le dossier d'installation (par défaut %LOCALAPPDATA%\Programs\PressePapiers).
    5. Lancement de la version installée si -Lancer est donné.

    Aucun droit administrateur : tout se passe dans le profil de l'utilisateur. Les données (historique, images,
    réglages, journal) sont dans %LOCALAPPDATA%\PressePapiers, ou dans le dossier de PRESSEPAPIERS_DONNEES si cette variable
    est définie : un autre dossier, que le script ne touche jamais.
    Au premier lancement de la version installée, l'application inscrit son propre chemin dans la clé Run de
    l'utilisateur si le démarrage avec Windows est activé dans ses réglages (activé par défaut).

    La copie miroir supprime tout ce qui n'est pas dans la publication, d'où des garde-fous sur la destination. Elle est
    refusée si :
    - elle contient ou recouvre le dossier des données, le dépôt ou le dossier de publication (dans un sens ou dans l'autre) ;
    - c'est la racine d'un disque, un dossier de profil connu (profil, Bureau, Documents, Téléchargements, AppData,
      Programmes, Windows...) ou l'un de leurs parents ;
    - c'est un fichier, ou un dossier qui n'est ni vide ni déjà une installation (il ne contient pas PressePapiers.exe).
    Une destination relative se résout par rapport au dossier courant de PowerShell.

    L'instance arrêtée est seulement celle dont l'exécutable est celui de la destination (jamais une autre copie de
    PressePapiers, comme une version Debug en cours d'essai) : demande de fermeture d'abord, arrêt forcé au bout de 5 secondes.

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
$delaiArretMs = 5000

# Chemin complet sans barre finale ; la racine d'un disque (C:\) garde la sienne pour rester reconnaissable.
function ConvertirEnCheminComplet([string]$chemin) {
    $complet = [System.IO.Path]::GetFullPath($chemin)
    if ($complet.Length -gt [System.IO.Path]::GetPathRoot($complet).Length) {
        $complet = $complet.TrimEnd('\')
    }
    return $complet
}

# Vrai si $dedans est $dehors ou se trouve dessous (comparaison sans casse, par dossiers entiers).
function EstDans([string]$dedans, [string]$dehors) {
    $a = $dedans.TrimEnd('\') + '\'
    $b = $dehors.TrimEnd('\') + '\'
    return $a.StartsWith($b, [System.StringComparison]::OrdinalIgnoreCase)
}

# Dossier des données : celui de PRESSEPAPIERS_DONNEES si elle est définie (comme l'application), sinon le dossier par défaut.
$redirection = $env:PRESSEPAPIERS_DONNEES
if ([string]::IsNullOrWhiteSpace($redirection)) {
    $donnees = ConvertirEnCheminComplet (Join-Path $env:LOCALAPPDATA 'PressePapiers')
} else {
    $donnees = ConvertirEnCheminComplet $redirection
}

# Destination relative : par rapport au dossier courant de PowerShell (et non à celui du processus .NET, qui peut différer).
$destinationAbsolue = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
$cible = ConvertirEnCheminComplet $destinationAbsolue
$depotComplet = ConvertirEnCheminComplet $depot
$publicationComplete = ConvertirEnCheminComplet $publication

# Garde-fou 1 : la copie miroir supprime ce qui n'est pas dans la publication.
if ((EstDans $donnees $cible) -or (EstDans $cible $donnees)) {
    throw "Destination refusée : $cible recouvre le dossier des données ($donnees)."
}
if ((EstDans $depotComplet $cible) -or (EstDans $cible $depotComplet)) {
    throw "Destination refusée : $cible recouvre le dépôt ($depotComplet)."
}
if ((EstDans $cible $publicationComplete) -or (EstDans $publicationComplete $cible)) {
    throw "Destination refusée : $cible recouvre le dossier de publication ($publicationComplete)."
}

# Garde-fou 2 : une racine de disque, un dossier de profil connu ou l'un de leurs parents n'est jamais une destination.
if ($cible -ieq [System.IO.Path]::GetPathRoot($cible)) {
    throw "Destination refusée : $cible est la racine d'un disque."
}
$profil = [Environment]::GetFolderPath('UserProfile')
$protegees = @(
    $profil,
    [Environment]::GetFolderPath('Desktop'),
    [Environment]::GetFolderPath('MyDocuments'),
    [Environment]::GetFolderPath('MyPictures'),
    [Environment]::GetFolderPath('MyMusic'),
    [Environment]::GetFolderPath('MyVideos'),
    (Join-Path $profil 'Downloads'),
    [Environment]::GetFolderPath('ApplicationData'),
    [Environment]::GetFolderPath('LocalApplicationData'),
    (Join-Path $env:LOCALAPPDATA 'Programs'),
    [Environment]::GetFolderPath('ProgramFiles'),
    [Environment]::GetFolderPath('ProgramFilesX86'),
    [Environment]::GetFolderPath('CommonApplicationData'),
    [Environment]::GetFolderPath('Windows'),
    [Environment]::GetFolderPath('System')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { ConvertirEnCheminComplet $_ }
foreach ($protegee in $protegees) {
    if (EstDans $protegee $cible) {
        throw "Destination refusée : $cible est un dossier de profil ou de système connu, ou contient $protegee."
    }
}
$systeme = ConvertirEnCheminComplet ([Environment]::GetFolderPath('Windows'))
if (EstDans $cible $systeme) {
    throw "Destination refusée : $cible est dans le dossier de Windows."
}

# Garde-fou 3 : un dossier existant doit être vide ou déjà contenir une installation de PressePapiers.
$executable = Join-Path $cible 'PressePapiers.exe'
if (Test-Path -LiteralPath $cible -PathType Leaf) {
    throw "Destination refusée : $cible est un fichier."
}
if (Test-Path -LiteralPath $cible -PathType Container) {
    $contenu = @(Get-ChildItem -LiteralPath $cible -Force)
    if ($contenu.Count -gt 0 -and -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Destination refusée : $cible n'est pas vide et ne contient pas PressePapiers.exe (ce n'est pas une installation)."
    }
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

# Une seule instance par utilisateur : celle qui tourne verrouille ses fichiers. Seul le processus dont l'exécutable est
# celui de la destination est concerné : une autre copie de PressePapiers (version Debug, essai) n'est pas touchée. Le Path
# d'un processus qu'on ne peut pas inspecter est vide : il est écarté.
$session = (Get-Process -Id $PID).SessionId
$enCours = @(Get-Process -Name 'PressePapiers' -ErrorAction SilentlyContinue |
    Where-Object { $_.SessionId -eq $session -and $_.Path -and ($_.Path -ieq $executable) })
foreach ($processus in $enCours) {
    Write-Host "Arrêt de l'instance installée (processus $($processus.Id)) ..."

    # Demande de fermeture d'abord. La fenêtre de l'application est le plus souvent masquée (elle vit dans la zone de
    # notification) : elle n'a alors pas de fenêtre principale à fermer, et l'arrêt forcé suit après le délai.
    $processus.CloseMainWindow() | Out-Null
    if (-not $processus.WaitForExit($delaiArretMs)) {
        Write-Host "Pas d'arrêt après $($delaiArretMs / 1000) s : arrêt forcé (l'icône peut rester affichée jusqu'au survol de la souris)."
        Stop-Process -Id $processus.Id -Force
        $processus.WaitForExit(10000) | Out-Null
    }
}

New-Item -ItemType Directory -Force -Path $cible | Out-Null
Write-Host "Copie vers $cible ..."
robocopy $publication $cible /MIR /NFL /NDL /NJH /NJS /NP /R:3 /W:1 | Out-Null
# Codes de robocopy : 0 à 7 = réussite (avec ou sans fichiers copiés), 8 et plus = échec.
if ($LASTEXITCODE -ge 8) {
    throw "La copie vers $cible a échoué (code robocopy $LASTEXITCODE)."
}

Write-Host "Installé : $executable"

if ($Lancer) {
    Start-Process -FilePath $executable -WorkingDirectory $cible
    Write-Host 'PressePapiers est lancé (icône dans la zone de notification).'
}

# Le code de robocopy (1 = fichiers copiés) ne doit pas devenir celui du script.
exit 0
