<#
.SYNOPSIS
    Publie PressePapiers (Release, win-x64, autonome) et l'installe : en mode normal dans le profil de l'utilisateur, en mode
    administrateur dans Program Files avec une tâche planifiée qui le lance en administrateur à l'ouverture de session.

.DESCRIPTION
    MODE NORMAL (par défaut), sans droits administrateur :
    1. Contrôle de la destination (voir plus bas) : rien n'est publié ni arrêté si elle est refusée.
    2. dotnet publish du projet de l'application dans un dossier de publication du dépôt (publish\, ignoré par git).
    3. Arrêt de l'instance installée, s'il y en a une : ses fichiers seraient verrouillés pendant la copie.
    4. Copie miroir de la publication dans le dossier d'installation (par défaut %LOCALAPPDATA%\Programs\PressePapiers).
    5. Lancement de la version installée si -Lancer est donné.
    Au premier lancement de la version installée, l'application inscrit son propre chemin dans la clé Run de
    l'utilisateur si le démarrage avec Windows est activé dans ses réglages (activé par défaut).

    MODE ADMINISTRATEUR (-Administrateur) : pour que Win+V et le collage automatique marchent aussi devant les fenêtres
    lancées en administrateur (un programme non élevé ne voit pas les frappes qu'elles reçoivent, c'est l'isolation UIPI).
    1. Publication comme ci-dessus (sans droits administrateur).
    2. UNE SEULE invite UAC : une étape élevée, relancée par ce même script, fait tout le reste : arrêt de l'instance en cours
       (installée en mode normal ou dans Program Files), copie miroir dans C:\Program Files\PressePapiers, contrôle des droits
       du dossier d'installation, création de la tâche planifiée « PressePapiers », retrait de la valeur Run de l'utilisateur.
    3. Avec -Lancer, la tâche est démarrée par Start-ScheduledTask (aucune invite).
    La destination est dans Program Files, que seuls les administrateurs peuvent modifier : sans cela, n'importe quel processus
    de l'utilisateur pourrait remplacer l'exécutable et obtenir les droits administrateur à la prochaine ouverture de session.
    La tâche : déclencheur à l'ouverture de session de l'utilisateur courant, exécution sous son compte (interactif) avec les
    privilèges les plus élevés, sans limite de durée, lancement et poursuite sur batterie, une seule instance.
    Le dossier installé en mode normal n'est pas supprimé (il ne sert plus ; l'effacer à la main si on le souhaite).

    RETOUR AU MODE NORMAL (-RetirerAdministrateur) : étape élevée (une invite UAC) qui arrête l'instance de Program Files et
    supprime la tâche planifiée. Le dossier de Program Files est laissé en place. Avec -Lancer, la version installée en mode
    normal est lancée (si elle existe ; sinon, lancer publier.ps1 -Lancer la réinstalle) ; au lancement elle réécrit la clé
    Run si le démarrage avec Windows est activé dans ses réglages.

    Les données (historique, images, réglages, journal) sont dans %LOCALAPPDATA%\PressePapiers, ou dans le dossier de
    PRESSEPAPIERS_DONNEES si cette variable est définie : un autre dossier, que le script ne touche jamais.

    La copie miroir supprime tout ce qui n'est pas dans la publication, d'où des garde-fous sur la destination. Elle est
    refusée si :
    - elle contient ou recouvre le dossier des données, le dépôt ou le dossier de publication (dans un sens ou dans l'autre) ;
    - c'est la racine d'un disque, un dossier de profil connu (profil, Bureau, Documents, Téléchargements, AppData,
      Programmes, Program Files lui-même, Windows...) ou l'un de leurs parents ;
    - c'est un fichier, ou un dossier qui n'est ni vide ni déjà une installation (il ne contient pas PressePapiers.exe) ;
    - en mode administrateur, elle n'est pas dans Program Files.
    Une destination relative se résout par rapport au dossier courant de PowerShell.

    L'instance arrêtée est seulement celle dont l'exécutable est celui de l'installation (normale ou de Program Files), jamais
    une autre copie de PressePapiers comme une version Debug en cours d'essai : demande de fermeture d'abord, arrêt forcé au
    bout de 5 secondes.

.PARAMETER Destination
    Dossier d'installation. Par défaut : %LOCALAPPDATA%\Programs\PressePapiers, ou C:\Program Files\PressePapiers avec
    -Administrateur ou -RetirerAdministrateur.

.PARAMETER Lancer
    Lance la version installée une fois la copie faite (par la tâche planifiée en mode administrateur).

.PARAMETER Administrateur
    Installe dans Program Files et crée la tâche planifiée qui lance PressePapiers en administrateur à l'ouverture de session.

.PARAMETER RetirerAdministrateur
    Supprime la tâche planifiée et arrête l'instance de Program Files : retour au mode normal.

.PARAMETER EtapeElevee
    Usage interne : étape élevée relancée par le script lui-même après l'invite UAC. Ne pas utiliser à la main.

.PARAMETER Utilisateur
    Usage interne : compte (DOMAINE\nom) pour lequel la tâche est créée, transmis à l'étape élevée.

.PARAMETER FichierJournal
    Usage interne : fichier où l'étape élevée note ce qu'elle fait, relu par le script d'origine.

.EXAMPLE
    pwsh -NoProfile -File scripts\publier.ps1 -Lancer

.EXAMPLE
    pwsh -NoProfile -File scripts\publier.ps1 -Administrateur -Lancer

.EXAMPLE
    pwsh -NoProfile -File scripts\publier.ps1 -RetirerAdministrateur -Lancer
#>
param(
    [string]$Destination,
    [switch]$Lancer,
    [switch]$Administrateur,
    [switch]$RetirerAdministrateur,
    [switch]$EtapeElevee,
    [string]$Utilisateur,
    [string]$FichierJournal
)

$ErrorActionPreference = 'Stop'

$depot = Split-Path -Parent $PSScriptRoot
$projet = Join-Path $depot 'src\PressePapiers\PressePapiers.csproj'
$publication = Join-Path $depot 'publish\PressePapiers'
$delaiArretMs = 5000
$nomTache = 'PressePapiers'
$cleRun = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

# Message à l'écran et, dans l'étape élevée dont la fenêtre disparaît à la fin, dans le fichier que le script d'origine relit.
function Dire([string]$texte) {
    Write-Host $texte
    if ($FichierJournal) {
        Add-Content -LiteralPath $FichierJournal -Value $texte -Encoding utf8
    }
}

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

# Vrai si le processus courant a un jeton élevé (administrateur). Avec UAC, un jeton filtré répond faux même pour un administrateur.
function EstEleve {
    $principal = [System.Security.Principal.WindowsPrincipal][System.Security.Principal.WindowsIdentity]::GetCurrent()
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Arrête les instances de PressePapiers de la session dont l'exécutable est l'un de ceux donnés : jamais une autre copie
# (version Debug en essai). Le Path d'un processus qu'on ne peut pas inspecter est vide : il est écarté.
function ArreterInstances([string[]]$executables) {
    $session = (Get-Process -Id $PID).SessionId
    $enCours = @(Get-Process -Name 'PressePapiers' -ErrorAction SilentlyContinue |
        Where-Object { $_.SessionId -eq $session -and $_.Path -and ($executables -contains $_.Path) })
    foreach ($processus in $enCours) {
        Dire "Arrêt de l'instance installée (processus $($processus.Id), $($processus.Path)) ..."

        # Demande de fermeture d'abord. La fenêtre de l'application est le plus souvent masquée (elle vit dans la zone de
        # notification) : elle n'a alors pas de fenêtre principale à fermer, et l'arrêt forcé suit après le délai.
        $processus.CloseMainWindow() | Out-Null
        if (-not $processus.WaitForExit($delaiArretMs)) {
            Dire "Pas d'arrêt après $($delaiArretMs / 1000) s : arrêt forcé (l'icône peut rester affichée jusqu'au survol de la souris)."
            Stop-Process -Id $processus.Id -Force
            $processus.WaitForExit(10000) | Out-Null
        }
    }
}

function PublierProjet {
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
}

function CopierPublication([string]$cible) {
    if (-not (Test-Path -LiteralPath (Join-Path $publication 'PressePapiers.exe') -PathType Leaf)) {
        throw "Publication introuvable dans $publication : lancer d'abord le script sans -EtapeElevee."
    }

    New-Item -ItemType Directory -Force -Path $cible | Out-Null
    Dire "Copie vers $cible ..."
    robocopy $publication $cible /MIR /NFL /NDL /NJH /NJS /NP /R:3 /W:1 | Out-Null
    # Codes de robocopy : 0 à 7 = réussite (avec ou sans fichiers copiés), 8 et plus = échec.
    if ($LASTEXITCODE -ge 8) {
        throw "La copie vers $cible a échoué (code robocopy $LASTEXITCODE)."
    }
}

# Le dossier d'installation administrateur et son exécutable ne doivent être modifiables que par les administrateurs : un
# droit d'écriture accordé à Tout le monde, aux Utilisateurs authentifiés ou aux Utilisateurs, ou un propriétaire qui est le
# compte de l'utilisateur, ferait de la tâche planifiée une élévation de privilèges pour tout processus de cet utilisateur.
function VerifierDroitsReservesAuxAdministrateurs([string[]]$chemins, [string]$utilisateur) {
    $sidsLarges = @('S-1-1-0', 'S-1-5-11', 'S-1-5-32-545')   # Tout le monde, Utilisateurs authentifiés, Utilisateurs
    $droits = [System.Security.AccessControl.FileSystemRights]::WriteData -bor
        [System.Security.AccessControl.FileSystemRights]::AppendData -bor
        [System.Security.AccessControl.FileSystemRights]::Delete -bor
        [System.Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor
        [System.Security.AccessControl.FileSystemRights]::ChangePermissions -bor
        [System.Security.AccessControl.FileSystemRights]::TakeOwnership
    $sidUtilisateur = ([System.Security.Principal.NTAccount]$utilisateur).Translate([System.Security.Principal.SecurityIdentifier]).Value

    foreach ($chemin in $chemins) {
        $acl = Get-Acl -LiteralPath $chemin
        $proprietaire = $acl.GetOwner([System.Security.Principal.SecurityIdentifier]).Value
        if ($proprietaire -eq $sidUtilisateur) {
            throw "Droits refusés : $chemin appartient à $utilisateur, qui pourrait en changer les droits."
        }
        foreach ($regle in $acl.GetAccessRules($true, $true, [System.Security.Principal.SecurityIdentifier])) {
            if ($regle.AccessControlType -eq 'Allow' -and ($sidsLarges -contains $regle.IdentityReference.Value) -and
                (($regle.FileSystemRights -band $droits) -ne 0)) {
                throw "Droits refusés : $chemin est modifiable par $($regle.IdentityReference.Value) ($($regle.FileSystemRights)). Le mode administrateur exige un dossier réservé aux administrateurs."
            }
        }
    }
}

# Tâche « PressePapiers » : à l'ouverture de session de l'utilisateur, sous son compte (session interactive), avec les
# privilèges les plus élevés. Sans limite de durée (l'outil tourne tant que la session dure), sur batterie sans arrêt, une
# seule instance. -Force remplace une tâche du même nom.
function EnregistrerTache([string]$utilisateur, [string]$executable, [string]$dossier) {
    $action = New-ScheduledTaskAction -Execute $executable -WorkingDirectory $dossier
    $declencheur = New-ScheduledTaskTrigger -AtLogOn -User $utilisateur
    $principal = New-ScheduledTaskPrincipal -UserId $utilisateur -LogonType Interactive -RunLevel Highest
    $reglages = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName $nomTache -Action $action -Trigger $declencheur -Principal $principal -Settings $reglages `
        -Description 'Lance PressePapiers en administrateur à l''ouverture de session (créée par scripts\publier.ps1 -Administrateur).' `
        -Force | Out-Null
    Dire "Tâche planifiée « $nomTache » créée pour $utilisateur (privilèges les plus élevés, à l'ouverture de session)."
}

# Étape qui exige les droits administrateur du mode -Administrateur. Appelée par le processus élevé.
function InstallerEnAdministrateur([string]$cible, [string]$executable, [string]$utilisateur) {
    $executableNormal = Join-Path $env:LOCALAPPDATA 'Programs\PressePapiers\PressePapiers.exe'
    ArreterInstances @($executable, $executableNormal)
    CopierPublication $cible
    VerifierDroitsReservesAuxAdministrateurs @($cible, $executable) $utilisateur
    EnregistrerTache $utilisateur $executable $cible

    # La tâche lance l'outil : une valeur Run le lancerait une seconde fois, sans droits administrateur.
    if (Get-ItemProperty -Path $cleRun -Name $nomTache -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $cleRun -Name $nomTache
        Dire 'Valeur Run « PressePapiers » retirée (le lancement est confié à la tâche planifiée).'
    }

    Dire "Installé : $executable"
}

# Étape qui exige les droits administrateur du mode -RetirerAdministrateur. Appelée par le processus élevé.
function DesinstallerLeModeAdministrateur([string]$executable, [string]$cible) {
    ArreterInstances @($executable)
    if (Get-ScheduledTask -TaskName $nomTache -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $nomTache -Confirm:$false
        Dire "Tâche planifiée « $nomTache » supprimée."
    } else {
        Dire "Aucune tâche planifiée « $nomTache » : rien à supprimer."
    }
    Dire "Le dossier $cible est laissé en place (il ne sert plus ; le supprimer à la main si on le souhaite)."
}

# Relance ce script, élevé, pour l'étape qui exige les droits administrateur : une seule invite UAC. Les messages de
# l'étape sont relus dans un fichier, car la fenêtre élevée disparaît à la fin.
function LancerEtapeElevee([string]$cible, [string]$utilisateur) {
    $journalEtape = Join-Path ([System.IO.Path]::GetTempPath()) ('publier-admin-{0}.log' -f [guid]::NewGuid().ToString('N'))
    $hote = (Get-Process -Id $PID).Path
    $mode = if ($RetirerAdministrateur) { '-RetirerAdministrateur' } else { '-Administrateur' }
    $arguments = @('-NoProfile', '-File', ('"{0}"' -f $PSCommandPath), '-EtapeElevee', $mode,
        '-Destination', ('"{0}"' -f $cible), '-Utilisateur', ('"{0}"' -f $utilisateur), '-FichierJournal', ('"{0}"' -f $journalEtape))

    Write-Host 'Étape administrateur : une invite UAC va s''ouvrir ...'
    try {
        $etape = Start-Process -FilePath $hote -ArgumentList $arguments -Verb RunAs -Wait -PassThru
    } catch {
        throw "Élévation refusée ou impossible ($($_.Exception.Message)) : rien n'a été installé ni modifié."
    }

    if (Test-Path -LiteralPath $journalEtape) {
        Get-Content -LiteralPath $journalEtape -Encoding utf8 | ForEach-Object { Write-Host $_ }
        Remove-Item -LiteralPath $journalEtape -Force -ErrorAction SilentlyContinue
    }
    if ($etape.ExitCode -ne 0) {
        throw "L'étape administrateur a échoué (code $($etape.ExitCode))."
    }
}

try {
    if ($Administrateur -and $RetirerAdministrateur) {
        throw '-Administrateur et -RetirerAdministrateur s''excluent : choisir l''un ou l''autre.'
    }
    $modeAdministrateur = $Administrateur -or $RetirerAdministrateur
    if ($EtapeElevee -and -not $modeAdministrateur) {
        throw '-EtapeElevee est un paramètre interne, réservé à -Administrateur et -RetirerAdministrateur.'
    }
    $eleve = EstEleve
    if ($EtapeElevee -and -not $eleve) {
        throw '-EtapeElevee doit s''exécuter avec les droits administrateur.'
    }
    if ($EtapeElevee -and [string]::IsNullOrWhiteSpace($Utilisateur)) {
        throw '-EtapeElevee exige -Utilisateur.'
    }
    if ([string]::IsNullOrWhiteSpace($Utilisateur)) {
        $Utilisateur = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    }
    if ($EtapeElevee -and ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name -ine $Utilisateur)) {
        throw "L'élévation s'est faite sous un autre compte que $Utilisateur : la tâche, la clé Run et les dossiers sont ceux de $Utilisateur, rien n'est modifié."
    }

    # Destination par défaut selon le mode : Program Files pour l'installation administrateur.
    if ([string]::IsNullOrWhiteSpace($Destination)) {
        if ($modeAdministrateur) {
            $Destination = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'PressePapiers'
        } else {
            $Destination = Join-Path $env:LOCALAPPDATA 'Programs\PressePapiers'
        }
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

    # Garde-fou 4 (mode administrateur) : l'installation doit être sous Program Files, réservé aux administrateurs. Sinon la
    # tâche lancerait avec les droits administrateur un exécutable que tout processus de l'utilisateur peut remplacer.
    if ($modeAdministrateur) {
        $racinesReservees = @([Environment]::GetFolderPath('ProgramFiles'), [Environment]::GetFolderPath('ProgramFilesX86')) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { ConvertirEnCheminComplet $_ }
        $sousProgramFiles = @($racinesReservees | Where-Object { ($cible -ine $_) -and (EstDans $cible $_) })
        if ($sousProgramFiles.Count -eq 0) {
            throw "Destination refusée en mode administrateur : $cible doit être un dossier de Program Files (réservé aux administrateurs)."
        }
    }

    # Étape élevée relancée par ce script : elle ne republie rien, elle installe ou désinstalle, puis note ce qu'elle a fait.
    if ($EtapeElevee) {
        if ($RetirerAdministrateur) {
            DesinstallerLeModeAdministrateur $executable $cible
        } else {
            InstallerEnAdministrateur $cible $executable $Utilisateur
        }
        exit 0
    }

    if ($RetirerAdministrateur) {
        if ($eleve) {
            DesinstallerLeModeAdministrateur $executable $cible
        } else {
            LancerEtapeElevee $cible $Utilisateur
        }

        if ($Lancer) {
            $executableNormal = Join-Path $env:LOCALAPPDATA 'Programs\PressePapiers\PressePapiers.exe'
            if (Test-Path -LiteralPath $executableNormal -PathType Leaf) {
                Start-Process -FilePath $executableNormal -WorkingDirectory (Split-Path -Parent $executableNormal)
                Write-Host 'PressePapiers (mode normal) est lancé.'
            } else {
                Write-Host 'Aucune installation en mode normal à lancer : lancer scripts\publier.ps1 -Lancer pour l''installer.'
            }
        }
        exit 0
    }

    if ($Administrateur) {
        PublierProjet
        if ($eleve) {
            InstallerEnAdministrateur $cible $executable $Utilisateur
        } else {
            LancerEtapeElevee $cible $Utilisateur
        }

        if ($Lancer) {
            Start-ScheduledTask -TaskName $nomTache
            Write-Host 'PressePapiers est lancé en administrateur par la tâche planifiée (icône dans la zone de notification).'
        }
        exit 0
    }

    # Mode normal. Une tâche administrateur qui existe encore lancerait, à la prochaine ouverture de session, une seconde
    # instance élevée à côté de celle-ci : on le signale sans rien changer.
    if (Get-ScheduledTask -TaskName $nomTache -ErrorAction SilentlyContinue) {
        Write-Warning "Une tâche planifiée « $nomTache » (mode administrateur) existe : elle lancera aussi PressePapiers à l'ouverture de session. Pour revenir au mode normal : scripts\publier.ps1 -RetirerAdministrateur."
    }

    PublierProjet

    # Une seule instance par utilisateur : celle qui tourne verrouille ses fichiers. Seul le processus dont l'exécutable est
    # celui de la destination est concerné : une autre copie de PressePapiers (version Debug, essai) n'est pas touchée.
    ArreterInstances @($executable)
    CopierPublication $cible
    Write-Host "Installé : $executable"

    if ($Lancer) {
        Start-Process -FilePath $executable -WorkingDirectory $cible
        Write-Host 'PressePapiers est lancé (icône dans la zone de notification).'
    }
}
catch {
    if ($EtapeElevee) {
        Dire "ERREUR : $($_.Exception.Message)"
        exit 1
    }
    throw
}

# Le code de robocopy (1 = fichiers copiés) ne doit pas devenir celui du script.
exit 0
