### Tâche 29 : Assemblage, publication et README

**Fichiers :**
- Modifier : `src/PressePapiers/App.xaml.cs` (le plan donne le contenu complet du fichier après modification ; il part du fichier de la tâche 16)
- Créer : `src/PressePapiers/App.Composition.cs` (partie de la classe `App` : composition des briques, pour garder des fichiers courts)
- Créer : `scripts/publier.ps1`
- Créer : `README.md`
- Tests : aucun test xUnit (le projet de tests ne référence que Coeur) ; vérification sur le poste par un programme hors dépôt (étapes 1 et 5)

**Interfaces :**
- Utilise :
  - tâche 2 : `DossierDonnees.ParDefaut()`, `DossierDonnees.CreerDossiers()`, propriétés `Journal`, `Parametres` ;
  - tâches 1, 4 à 8 : `BaseHistorique.VersionSqlite`, `BaseHistorique.Dispose()` ; `new DepotHistorique(BaseHistorique, DossierDonnees, TimeProvider, IJournal)`, `int AppliquerPlafond(int plafond)` ; `DemarrageSession.Appliquer(ISourceSession, DepotHistorique, IJournal)` et `ResultatSession(EtatSession Etat, int ElementsPurges)` ;
  - tâches 3 et 10 : `Raccourci.TryAnalyser(string, out Raccourci)`, `Raccourci.ToString()` ; `Parametres` (`RaccourciSecours`, `DemarrageAvecWindows`, `PlafondTemporaire`) et `new Parametres()` pour le défaut ; `new ServiceParametres(string cheminFichier, IJournal journal)` ;
  - tâche 9 : `new MoteurRecherche(BaseHistorique)` ;
  - tâche 11 : `new AntiRebond(TimeProvider horloge, TimeSpan delai)`, `bool Accepter()` ;
  - tâche 16 : `App` (fichier `App.xaml.cs` de la tâche 16, dont celui-ci reprend le démarrage, `ArretEnCours`, `SignalerActivation`, `PosterSurInterface`, `AfficherFenetre`, `Quitter`, `ExecuterProtege` et l'arrêt de recette Debug), `OuvertureBase.Ouvrir(DossierDonnees, TimeProvider, IJournal, IDialogueDemarrage)` (renvoie `BaseHistorique?`), `DialoguesWindows`, `new JournalSerilog(string dossierJournal)`, `new SourceSessionWts()`, `DemarrageWindows.Appliquer(bool activer, string cheminExecutable)`, `Program` (inchangé) ;
  - tâches 17 et 18 : `new RelaisWindows(IJournal)`, `nint Handle`, événements `RaccourciSecoursPresse`, `SessionDeverrouillee`, `SortieDeVeille`, `bool EcouterPressePapiers()`, `bool EnregistrerRaccourci(Raccourci, out int codeErreur)` (1409 = combinaison déjà prise ; l'ancien raccourci est rétabli), `Dispose()` ; `new IconeNotification(RelaisWindows, IJournal)`, `Afficher(bool alerte, string infobulle)`, `Notifier(string titre, string message)`, événements `OuvrirDemande`, `ParametresDemandes`, `QuitterDemande`, `Dispose()` (NIM_DELETE) ;
  - tâches 19 et 20 : `PremierPlan.FenetreActive()` ; `ICrochetWinV` (`EstActif`, `RaisonInactif`, `event Action<nint>? WinVPresse` levé sur le fil du hook, `Demarrer()`, `Reinstaller()`, `Dispose()`), `new CrochetWinV(IJournal)` ; le hook se réinstalle déjà seul toutes les 15 minutes sur son fil (`SetTimer`, tâche 20) ;
  - tâches 21 à 24 : `new LecteurPressePapiers(nint hwndRelais, IJournal)`, `new EcrivainPressePapiers(nint hwndRelais, IJournal)`, `new ServiceCapture(RelaisWindows, LecteurPressePapiers, DepotHistorique, Func<Parametres>, IJournal)`, `Dispose()` ; `new ServiceRecollage(EcrivainPressePapiers, DepotHistorique, IJournal)` ;
  - tâches 25 à 28 : `new FenetrePrincipaleVue(DepotHistorique, MoteurRecherche, ServiceRecollage, ServiceParametres, IJournal)`, `Parametres Reglages`, `string EtatCrochet`, `Action<string, string>? Notifier`, `Func<Raccourci, string?>? AppliquerRaccourci`, `event Action? ParametresModifies` ; `new FenetrePrincipale(FenetrePrincipaleVue)`, `bool EstVisible`, `AfficherPour(nint fenetreOrigine, OrigineOuverture origine)`, `AfficherParametres()`, `event Action? PremierPlanRefuse`, `AutoriserFermeture()`.
- Fournit : l'application complète. Membres internes de `App` (inchangés depuis la tâche 16) : `static bool ArretEnCours`, `static void SignalerActivation()`, `void PosterSurInterface(Action, string)`, `void AfficherFenetre(OrigineOuverture)`, `void Quitter()`. Script `scripts/publier.ps1 [-Destination <dossier>] [-Lancer]`. `README.md` avec la recette manuelle.

Décisions de cette tâche :
- **Ordre de démarrage** (spec, section 12) : journal, copie de sécurité et ouverture de la base, session et purge, fenêtre principale créée masquée, relais (écoute du presse-papiers et capture, raccourci de secours, icône), hook, démarrage avec Windows. Le relais est créé juste avant la fenêtre (le collage écrit dans le presse-papiers au nom de sa fenêtre), mais n'écoute rien avant que la fenêtre existe.
- **Premier affichage** (point 2 de la section 16, essais des tâches 16 et 27) : la fenêtre est créée sans être affichée, sans `Show(false)` préalable ni `Activate` ; chaque ouverture fait `AppWindow.Show()` puis `PremierPlan.Amener` (dans `FenetrePrincipale`). Le `catch (NotImplementedException)` provisoire de la tâche 16 est retiré.
- **Win+V** : `WinVPresse` (fil du hook) est reposté sur le fil d'interface par `PosterSurInterface` ; **raccourci de secours** : la fenêtre active est relevée puis la fenêtre affichée pendant le traitement de `WM_HOTKEY` (Windows accorde alors le premier plan). Les deux passent par `OuvrirDepuisRaccourci` : ignoré si la fenêtre est visible, ou si le seul `AntiRebond` de 300 ms (commun aux deux sources) refuse.
- **Réinstallation du hook** : au déverrouillage et à la sortie de veille (`Reinstaller`). Écart à la consigne « toutes les 15 minutes par `DispatcherQueueTimer` » : le hook de la tâche 20 se réinstalle déjà toutes les 15 minutes sur son propre fil, comme le veut la spec (section 11) ; une seconde réinstallation périodique doublerait le travail et le journal. La `DispatcherQueueTimer` d'`App` (une minute) relit seulement l'état du hook pour l'icône et les Paramètres, et relance l'installation d'un hook inactif.
- **Raccourci de secours** : lu dans les réglages (le défaut si le texte est illisible) ; refus au démarrage signalé par une notification de l'icône ; dans les Paramètres, `vue.AppliquerRaccourci` renvoie le message (1409 : déjà pris).
- **Quitter** : posté sur le fil d'interface depuis le menu (le relais ne doit pas être détruit pendant sa propre procédure de fenêtre) ; arrête la capture et le hook avant de libérer la base, retire l'icône, appelle `FenetrePrincipale.AutoriserFermeture()` puis `Application.Exit` (`DispatcherShutdownMode` reste `OnExplicitShutdown`).
- **Démarrage avec Windows** : `DemarrageWindows.Appliquer(Reglages.DemarrageAvecWindows, Environment.ProcessPath)` à chaque démarrage (le chemin change après une publication) ; un refus du registre est noté sans bloquer. Pour tout essai, un `parametres.json` avec `"DemarrageAvecWindows": false` doit exister **avant** le premier lancement, sinon la vraie clé Run reçoit l'exécutable d'essai.
- Limite connue, non corrigée ici (fichier de la tâche 23) : la capture enregistre l'élément sur le fil d'interface, donc un texte de plusieurs mégaoctets gèle la fenêtre une à deux secondes. Le verrou partagé du dépôt et de la recherche (tâches 4 et 9) rend sûr un futur passage de cette écriture sur un autre fil.

- [ ] **Étape 1 : Écrire la vérification qui échoue**

Pas de test xUnit possible : `App` vit dans l'application WinUI 3, que le projet de tests ne référence pas. La vérification est un
essai de fumée sur le poste, hors du dépôt, avec un dossier de données jetable. Préparer le dossier :

```bash
DEPOT="$(git rev-parse --show-toplevel)"
ESSAIS="$LOCALAPPDATA/Temp/essais-assemblage"
mkdir -p "$ESSAIS/LectureBase"
```

Créer `$ESSAIS/LectureBase/LectureBase.csproj` (lecture seule de la base pendant que l'application tourne) :

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Programme jetable, hors du dépôt : lit la base d'un dossier de données d'essai, en lecture seule, pendant que
       l'application tourne. Compiler avec -p:Depot=<chemin absolu du dépôt PressePapiers>. -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$(Depot)/src/PressePapiers.Coeur/PressePapiers.Coeur.csproj" />
  </ItemGroup>
</Project>
```

Créer `$ESSAIS/LectureBase/Programme.cs` :

```csharp
using Microsoft.Data.Sqlite;

// Affiche les derniers éléments enregistrés d'une base PressePapiers, une ligne par élément :
// id|type|onglet|application|image_fichier|texte (60 premiers caractères, sauts de ligne remplacés par des espaces).
// La connexion est en lecture seule : la base ouverte par l'application (mode WAL) n'est pas modifiée.
if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage : LectureBase <chemin de historique.db>");
    return 2;
}

var chaine = new SqliteConnectionStringBuilder { DataSource = args[0], Mode = SqliteOpenMode.ReadOnly }.ToString();
using var connexion = new SqliteConnection(chaine);
connexion.Open();

using var commande = connexion.CreateCommand();
commande.CommandText =
    "SELECT e.id, e.type, o.nom, e.application, e.image_fichier, e.texte FROM element e JOIN onglet o ON o.id = e.onglet_id " +
    "ORDER BY e.id DESC LIMIT 5";
using var lecteur = commande.ExecuteReader();
while (lecteur.Read())
{
    var texte = lecteur.IsDBNull(5) ? string.Empty : lecteur.GetString(5).ReplaceLineEndings(" ");
    if (texte.Length > 60)
    {
        texte = texte[..60];
    }

    Console.WriteLine(string.Join('|',
        lecteur.GetInt64(0), lecteur.GetInt64(1), lecteur.GetString(2),
        lecteur.IsDBNull(3) ? string.Empty : lecteur.GetString(3),
        lecteur.IsDBNull(4) ? string.Empty : lecteur.GetString(4),
        texte));
}

return 0;
```

Créer `$ESSAIS/fumee.ps1` :

```powershell
#requires -Version 7
<#
  Essai de fumée de PressePapiers assemblé (tâche 29). Lance l'exécutable Debug avec un dossier de données jetable
  (PRESSEPAPIERS_DONNEES) et un parametres.json dont DemarrageAvecWindows vaut false : ni les vraies données ni la clé Run
  ne sont touchées. Contrôle : démarrage sans fenêtre, icône présente ; copie d'un texte puis d'une image enregistrées en
  base (et le PNG sur disque) ; raccourci de secours par SendInput : fenêtre visible au premier plan ; Échap la masque ;
  second lancement : la première instance affiche sa fenêtre ; Quitter par le menu de l'icône : arrêt propre, icône retirée.
  Il lance une petite fenêtre cible (Windows Forms, dans powershell.exe) qu'il arrête à la fin, remet un texte neutre dans
  le presse-papiers et ne laisse aucun processus.
  Usage : pwsh -NoProfile -File fumee.ps1 -Depot <racine du dépôt> -Lecteur <chemin de LectureBase.exe>
#>
param(
    [Parameter(Mandatory)][string]$Depot,
    [Parameter(Mandatory)][string]$Lecteur,
    [string]$Executable
)

$ErrorActionPreference = 'Stop'
# Par défaut la version Debug ; -Executable essaie la version publiée par scripts/publier.ps1 (sans le filet d'arrêt
# PRESSEPAPIERS_ARRET_APRES_MS, réservé aux versions Debug : seul le menu de l'icône l'arrête).
$exe = if ($Executable) { $Executable } else { Join-Path $Depot 'src/PressePapiers/bin/Debug/net10.0-windows10.0.19041.0/win-x64/PressePapiers.exe' }
if (-not (Test-Path $exe)) { throw "Exécutable introuvable : $exe (compiler d'abord en Debug)" }
if (Get-Process PressePapiers -ErrorAction SilentlyContinue) { throw 'Une instance de PressePapiers tourne déjà : l''arrêter avant l''essai.' }

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Fumee
{
    public delegate bool Rappel(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(Rappel r, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, Entree[] e, int taille);
    [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref Identifiant id, out Rect r);

    [StructLayout(LayoutKind.Sequential)] struct Clavier { public ushort Vk; public ushort Scan; public uint Drapeaux; public uint Temps; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct Souris { public int X; public int Y; public uint Donnee; public uint Drapeaux; public uint Temps; public IntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] struct Union { [FieldOffset(0)] public Souris S; [FieldOffset(0)] public Clavier C; }
    [StructLayout(LayoutKind.Sequential)] struct Entree { public uint Type; public Union U; }
    [StructLayout(LayoutKind.Sequential)] struct Identifiant { public uint Taille; public IntPtr Hwnd; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int G; public int H; public int D; public int B; }

    public static List<IntPtr> Fenetres(uint pid, string titre, bool visiblesSeulement)
    {
        var liste = new List<IntPtr>();
        EnumWindows((h, p) =>
        {
            uint q; GetWindowThreadProcessId(h, out q);
            if (q != pid) return true;
            if (visiblesSeulement && !IsWindowVisible(h)) return true;
            if (titre != null) { var s = new StringBuilder(256); GetWindowText(h, s, 256); if (s.ToString() != titre) return true; }
            liste.Add(h);
            return true;
        }, IntPtr.Zero);
        return liste;
    }

    public static uint Processus(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid; }

    // Icône connue du Shell : Shell_NotifyIconGetRect réussit (identifiant 1 sur la fenêtre du relais).
    public static bool IconePresente(IntPtr relais)
    {
        var id = new Identifiant { Taille = (uint)Marshal.SizeOf<Identifiant>(), Hwnd = relais, Id = 1 };
        Rect r;
        return Shell_NotifyIconGetRect(ref id, out r) == 0;
    }

    // Même recette que la brique Premier plan : entrée souris vide, puis SetForegroundWindow.
    public static bool Amener(IntPtr h)
    {
        var vide = new Entree[1];
        SendInput(1, vide, Marshal.SizeOf<Entree>());
        SetForegroundWindow(h);
        return GetForegroundWindow() == h;
    }

    public static void Touches(params ushort[] vks)
    {
        var e = new Entree[vks.Length * 2];
        for (int i = 0; i < vks.Length; i++) { e[i].Type = 1; e[i].U.C.Vk = vks[i]; }
        for (int i = 0; i < vks.Length; i++) { var j = vks.Length + i; e[j].Type = 1; e[j].U.C.Vk = vks[vks.Length - 1 - i]; e[j].U.C.Drapeaux = 2; }
        SendInput((uint)e.Length, e, Marshal.SizeOf<Entree>());
    }

    // Une frappe injectée est-elle vue par le système ? Faux quand une fenêtre de niveau supérieur (par exemple un outil
    // de bureau à distance lancé en administrateur) est au premier plan : Windows avale alors toute entrée injectée.
    public static bool InjectionPossible()
    {
        var e = new Entree[1];
        e[0].Type = 1; e[0].U.C.Vk = 0x87;   // F24 enfoncée
        SendInput(1, e, Marshal.SizeOf<Entree>());
        System.Threading.Thread.Sleep(50);
        bool vue = (GetAsyncKeyState(0x87) & 0x8000) != 0;
        e[0].U.C.Drapeaux = 2;
        SendInput(1, e, Marshal.SizeOf<Entree>());
        return vue;
    }
}
'@

$echecs = 0
$nonTestes = 0
# $ok non typé : un -match sur un résultat vide rend une collection vide, qui vaut faux ici au lieu de faire échouer l'essai.
function Controle([string]$nom, $ok, [string]$detail = '') {
    if ($ok) { Write-Host "OK          $nom" } else { Write-Host "ECHEC       $nom $detail"; $script:echecs++ }
}
function NonTeste([string]$nom, [string]$raison) { Write-Host "NON TESTE   $nom ($raison)"; $script:nonTestes++ }

function Attendre([scriptblock]$condition, [int]$secondes) {
    $fin = (Get-Date).AddSeconds($secondes)
    while ((Get-Date) -lt $fin) { if (& $condition) { return $true }; Start-Sleep -Milliseconds 100 }
    return $false
}

$donnees = Join-Path ([IO.Path]::GetTempPath()) ('pp-fumee-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $donnees | Out-Null
Set-Content (Join-Path $donnees 'parametres.json') '{ "DemarrageAvecWindows": false }' -Encoding utf8NoBOM
$base = Join-Path $donnees 'historique.db'

function LireJournal {
    $fichier = Get-ChildItem (Join-Path $donnees 'journal') -Filter 'journal-*.log' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $fichier) { return '' }
    $flux = [IO.File]::Open($fichier.FullName, 'Open', 'Read', 'ReadWrite')
    try { return (New-Object IO.StreamReader($flux, [Text.Encoding]::UTF8)).ReadToEnd() } finally { $flux.Dispose() }
}

function DerniersElements { & $Lecteur $base }

# Le presse-papiers est partagé avec tout le poste : une écriture refusée (tenu par un autre processus) est retentée.
function PoserTexte([string]$texte) {
    for ($i = 0; $i -lt 5; $i++) { try { Set-Clipboard -Value $texte; return } catch { Start-Sleep -Milliseconds 300 } }
    throw 'Presse-papiers indisponible.'
}

$cleRun = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
function LireRun { $cle = Get-Item $cleRun; ($cle.GetValueNames() | Sort-Object | ForEach-Object { $_ + '=' + $cle.GetValue($_) }) -join ';' }
$runAvant = LireRun

$env:PRESSEPAPIERS_DONNEES = $donnees
$env:PRESSEPAPIERS_ARRET_APRES_MS = '240000'   # filet : arrêt propre au bout de 4 minutes si le menu de l'icône n'a pas servi

$appli = $null; $seconde = $null; $cible = $null
try {
    # 1. Démarrage sans fenêtre, icône présente.
    $appli = Start-Process $exe -PassThru
    Controle 'démarrage terminé au journal' (Attendre { (LireJournal) -match 'Démarrage terminé' } 40)
    Start-Sleep -Milliseconds 500
    $pidAppli = [uint32]$appli.Id
    Controle 'aucune fenêtre visible au démarrage' ([Fumee]::Fenetres($pidAppli, $null, $true).Count -eq 0)
    $relais = [Fumee]::Fenetres($pidAppli, 'PressePapiers relais', $false) | Select-Object -First 1
    $principale = [Fumee]::Fenetres($pidAppli, 'Presse-papiers', $false) | Select-Object -First 1
    Controle 'fenêtre du relais trouvée' ($null -ne $relais)
    Controle 'fenêtre principale créée, masquée' ($null -ne $principale -and -not [Fumee]::IsWindowVisible($principale))
    Controle 'icône présente dans la zone de notification' ([Fumee]::IconePresente($relais))
    $j = LireJournal
    Controle 'journal : hook Win+V posé' ($j -match 'Hook Win\+V posé')
    Controle 'journal : raccourci de secours enregistré' ($j -match 'Raccourci de secours Ctrl\+Alt\+Maj\+V enregistré')
    Controle 'journal : démarrage avec Windows désactivé' ($j -match 'Démarrage avec Windows : désactivé')
    if ($j -match 'SQLite (\d+\.\d+\.\d+)') { Write-Host "            version de SQLite : $($Matches[1])" }

    # 2. Copie d'un texte : une ligne en base.
    $marque = 'fumee-' + [guid]::NewGuid().ToString('N') + ' é'
    PoserTexte $marque
    Controle 'texte copié enregistré en base (Temporaire)' (Attendre { (DerniersElements | Select-Object -First 1) -match ([regex]::Escape($marque)) } 10) ((DerniersElements) -join ' / ')
    $ligne = DerniersElements | Select-Object -First 1
    Controle 'texte : type 1, onglet Temporaire' ($ligne -match '^\d+\|1\|Temporaire\|') $ligne

    # 3. Copie d'une image par Windows Forms : une ligne de type image et son PNG sur disque.
    $script = 'Add-Type -AssemblyName System.Windows.Forms, System.Drawing; ' +
              '$b = New-Object System.Drawing.Bitmap 64, 48; $g = [System.Drawing.Graphics]::FromImage($b); ' +
              '$g.Clear([System.Drawing.Color]::FromArgb(255, 30, 120, 200)); $g.Dispose(); ' +
              'for ($i = 0; $i -lt 5; $i++) { try { [System.Windows.Forms.Clipboard]::SetImage($b); break } catch { Start-Sleep -Milliseconds 300 } }'
    powershell.exe -NoProfile -STA -Command $script
    $image = Attendre { (DerniersElements | Select-Object -First 1) -match '^\d+\|3\|' } 10
    Controle 'image copiée enregistrée en base (type 3)' $image ((DerniersElements) -join ' / ')
    if ($image) {
        $nomPng = ((DerniersElements | Select-Object -First 1) -split '\|')[4]
        Controle 'PNG de l''image présent sur disque' ((-not [string]::IsNullOrEmpty($nomPng)) -and (Test-Path (Join-Path $donnees "images/$nomPng"))) $nomPng
    }

    # 4. Raccourci de secours depuis une fenêtre tierce : fenêtre visible au premier plan.
    $formulaire = 'Add-Type -AssemblyName System.Windows.Forms; $f = New-Object System.Windows.Forms.Form; ' +
                  '$f.Text = ''Cible fumee PressePapiers''; $f.Width = 400; $f.Height = 200; ' +
                  '$t = New-Object System.Windows.Forms.TextBox; $t.Dock = ''Fill''; $t.Multiline = $true; $f.Controls.Add($t); ' +
                  '[System.Windows.Forms.Application]::Run($f)'
    $cible = Start-Process powershell.exe -ArgumentList '-NoProfile', '-STA', '-Command', $formulaire -PassThru
    $hCible = $null
    Attendre { $script:hCible = [Fumee]::Fenetres([uint32]$cible.Id, 'Cible fumee PressePapiers', $true) | Select-Object -First 1; $null -ne $script:hCible } 15 | Out-Null
    $auPremierPlan = $hCible -and (Attendre { [Fumee]::Amener($hCible) } 5)
    if (-not $auPremierPlan -or -not [Fumee]::InjectionPossible()) {
        $devant = [Fumee]::GetForegroundWindow()
        NonTeste 'raccourci de secours, Échap, relance' "fenêtre cible non amenée au premier plan ou frappes injectées avalées (premier plan : processus $([Fumee]::Processus($devant)))"
    }
    else {
        [Fumee]::Touches(0x11, 0x12, 0x10, 0x56)   # Ctrl+Alt+Maj+V
        Controle 'raccourci de secours : fenêtre principale visible' (Attendre { [Fumee]::IsWindowVisible($principale) } 5)
        Controle 'raccourci de secours : fenêtre principale au premier plan' (Attendre { [Fumee]::GetForegroundWindow() -eq $principale } 5) "premier plan : $([Fumee]::GetForegroundWindow())"
        Controle 'journal : aucune exception' (-not ((LireJournal) -match '\[ERR\]'))

        # 5. Échap : recherche vide, la fenêtre se masque.
        Start-Sleep -Milliseconds 400
        [Fumee]::Touches(0x1B)
        Controle 'Échap : fenêtre masquée' (Attendre { -not [Fumee]::IsWindowVisible($principale) } 5)
        $appli.Refresh()
        Controle 'Échap : l''application continue' (-not $appli.HasExited)

        # 6. Second lancement : la première instance affiche sa fenêtre, la seconde s'arrête.
        Start-Sleep -Milliseconds 400
        $seconde = Start-Process $exe -PassThru
        Controle 'second lancement : la seconde instance s''arrête, code 0' ($seconde.WaitForExit(20000) -and $seconde.ExitCode -eq 0)
        Controle 'second lancement : fenêtre de la première instance visible' (Attendre { [Fumee]::IsWindowVisible($principale) } 5)
        Controle 'second lancement : fenêtre au premier plan' (Attendre { [Fumee]::GetForegroundWindow() -eq $principale } 5)
        Controle 'un seul processus PressePapiers' (@(Get-Process PressePapiers -ErrorAction SilentlyContinue).Count -eq 1)
        Start-Sleep -Milliseconds 400
        [Fumee]::Touches(0x1B)
        Controle 'Échap après la relance : fenêtre masquée' (Attendre { -not [Fumee]::IsWindowVisible($principale) } 5)
    }

    # 7. Quitter par le menu de l'icône : WM_CONTEXTMENU (0x007B) posté comme le Shell le ferait en version 4, puis Q.
    [Fumee]::PostMessage($relais, 0x8001, [IntPtr]::Zero, [IntPtr](0x007B -bor (1 -shl 16))) | Out-Null
    $menu = Attendre { ([Fumee]::Processus([Fumee]::GetForegroundWindow()) -eq $pidAppli) } 5
    if ($menu -and [Fumee]::InjectionPossible()) {
        Start-Sleep -Milliseconds 300
        [Fumee]::Touches(0x51)   # Q : accélérateur de « Quitter »
        $parMenu = $true
    }
    else {
        NonTeste 'Quitter par le menu de l''icône' 'menu non au premier plan ou frappes avalées ; arrêt par le filet PRESSEPAPIERS_ARRET_APRES_MS'
        $parMenu = $false
    }
    $arret = $appli.WaitForExit($(if ($parMenu) { 15000 } else { 250000 }))
    Controle 'arrêt propre : le processus se termine seul' $arret
    if ($arret) { Controle 'arrêt propre : code de sortie 0' ($appli.ExitCode -eq 0) "code $($appli.ExitCode)" }
    Controle 'journal : arrêt noté' ((LireJournal) -match 'Arrêt de PressePapiers')
    Controle 'icône retirée de la zone de notification' (-not [Fumee]::IconePresente($relais))
    Controle 'journal : aucune erreur' (-not ((LireJournal) -match '\[ERR\]'))
}
finally {
    foreach ($p in @($appli, $seconde, $cible)) { if ($p -and -not $p.HasExited) { $p.Kill(); $p.WaitForExit(5000) | Out-Null } }
    try { PoserTexte 'texte neutre' } catch { Write-Host 'Presse-papiers : texte neutre non posé.' }
    $runApres = LireRun
    Controle 'clé Run de l''utilisateur inchangée' ($runAvant -eq $runApres)
    Controle 'aucun processus PressePapiers restant' ($null -eq (Get-Process PressePapiers -ErrorAction SilentlyContinue))
    Write-Host 'Journal complet :'
    Write-Host (LireJournal)
    Remove-Item $donnees -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item Env:PRESSEPAPIERS_DONNEES, Env:PRESSEPAPIERS_ARRET_APRES_MS -ErrorAction SilentlyContinue
}

Write-Host "$nonTestes contrôle(s) non testé(s)."
if ($echecs -gt 0) { Write-Host "$echecs contrôle(s) en échec"; exit 1 }
Write-Host 'Tous les contrôles exécutés sont réussis.'
```

- [ ] **Étape 2 : Lancer la vérification et constater l'échec**

```bash
DEPOT="$(git rev-parse --show-toplevel)"; ESSAIS="$LOCALAPPDATA/Temp/essais-assemblage"
dotnet build "$DEPOT/PressePapiers.slnx" -c Debug
dotnet build "$ESSAIS/LectureBase" -c Debug "-p:Depot=$DEPOT"
pwsh -NoProfile -File "$ESSAIS/fumee.ps1" -Depot "$DEPOT" -Lecteur "$ESSAIS/LectureBase/bin/Debug/net10.0-windows10.0.19041.0/LectureBase.exe"
```

Résultat attendu avant l'étape 3, code de sortie 1 (l'application de la tâche 28 ne pose ni icône, ni écoute, ni raccourci,
ni hook ; l'essai s'arrête par le filet `PRESSEPAPIERS_ARRET_APRES_MS`, au bout de 4 minutes). Les lignes `OK` d'Échap
après le raccourci et de l'icône retirée ne prouvent rien ici (fenêtre jamais ouverte, icône jamais posée) ; la relance par
un second lancement fonctionne déjà depuis les tâches 16 et 27. Sortie relevée le 13/10/2026 en rejouant les tâches 1 à 28
du plan :

```
OK          démarrage terminé au journal
OK          aucune fenêtre visible au démarrage
OK          fenêtre du relais trouvée
OK          fenêtre principale créée, masquée
ECHEC       icône présente dans la zone de notification
ECHEC       journal : hook Win+V posé
ECHEC       journal : raccourci de secours enregistré
ECHEC       journal : démarrage avec Windows désactivé
ECHEC       texte copié enregistré en base (Temporaire)
ECHEC       texte : type 1, onglet Temporaire
ECHEC       image copiée enregistrée en base (type 3)
ECHEC       raccourci de secours : fenêtre principale visible
ECHEC       raccourci de secours : fenêtre principale au premier plan premier plan : 397594
OK          journal : aucune exception
OK          Échap : fenêtre masquée
OK          Échap : l'application continue
OK          second lancement : la seconde instance s'arrête, code 0
OK          second lancement : fenêtre de la première instance visible
OK          second lancement : fenêtre au premier plan
OK          un seul processus PressePapiers
OK          Échap après la relance : fenêtre masquée
NON TESTE   Quitter par le menu de l'icône (menu non au premier plan ou frappes avalées ; arrêt par le filet PRESSEPAPIERS_ARRET_APRES_MS)
OK          arrêt propre : le processus se termine seul
OK          arrêt propre : code de sortie 0
OK          journal : arrêt noté
OK          icône retirée de la zone de notification
OK          journal : aucune erreur
OK          clé Run de l'utilisateur inchangée
OK          aucun processus PressePapiers restant
Journal complet :
[INF] Démarrage de PressePapiers 1.0.0.0 (processus 41188, données : C:\Users\<utilisateur>\AppData\Local\Temp\pp-fumee-01573f01a02e4c1abc76857e983b4599).
[INF] Premier lancement : repère de session enregistré, Temporaire conservé.
[INF] Session Windows : PremierLancement, 0 élément(s) de Temporaire purgé(s).
[INF] Arrêt de recette programmé dans 240000 ms.
[INF] Démarrage terminé, aucune fenêtre affichée.
[INF] Activation redirigée reçue d'une seconde instance : ouverture de la fenêtre demandée.
[INF] Seconde instance (processus 44780) : activation redirigée vers l'instance principale, arrêt.
[INF] Arrêt de PressePapiers.

1 contrôle(s) non testé(s).
9 contrôle(s) en échec
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers/App.xaml.cs` (contenu complet ; il part du fichier de la tâche 16 : composition branchée, `catch (NotImplementedException)` retiré, `Quitter` complété)

```csharp
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PressePapiers.Capture;
using PressePapiers.Coeur.Clavier;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Session;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Interface;
using PressePapiers.Systeme;

namespace PressePapiers;

/// <summary>
/// Application WinUI 3 : cycle de vie et ordre de démarrage de la spec (section 12). Ce fichier porte le démarrage,
/// l'arrêt et les exceptions ; la composition des briques (relais, icône, hook, capture, fenêtre) est dans
/// <c>App.Composition.cs</c>.
/// </summary>
public sealed partial class App : Application
{
    private static readonly Lock _verrou = new();
    private static App? _courante;
    private static bool _relancePendante;

    private readonly DispatcherQueue _fil;
    private readonly IDialogueDemarrage _dialogues = new DialoguesWindows();

    // Un seul anti-rebond pour Win+V et le raccourci de secours : deux signaux à moins de 300 ms n'ouvrent qu'une fois.
    private readonly AntiRebond _antiRebond = new(TimeProvider.System, TimeSpan.FromMilliseconds(300));

    private IJournal _journal = JournalNul.Instance;
    private JournalSerilog? _journalFichier;
    private BaseHistorique? _base;
    private DepotHistorique? _depot;
    private RelaisWindows? _relais;
    private IconeNotification? _icone;
    private ICrochetWinV? _crochet;
    private ServiceCapture? _capture;
    private FenetrePrincipaleVue? _vue;
    private FenetrePrincipale? _fenetre;
    private DispatcherQueueTimer? _minuterieCrochet;
    private bool _prete;
#if DEBUG
    private DispatcherQueueTimer? _minuterieArret;
#endif

    /// <summary>Charge les ressources XAML, règle l'arrêt explicite et branche les gestionnaires d'exceptions.</summary>
    public App()
    {
        InitializeComponent();

        // Masquer la fenêtre, voire la fermer, ne doit pas terminer l'application : seul Quitter le fait.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        _fil = DispatcherQueue.GetForCurrentThread();

        // Branchés tout de suite : une exception du démarrage lui-même doit être notée. Avant la création du journal,
        // ils écrivent dans le journal nul.
        UnhandledException += SurExceptionInterface;
        AppDomain.CurrentDomain.UnhandledException += SurExceptionDomaine;
        TaskScheduler.UnobservedTaskException += SurExceptionTache;

        lock (_verrou)
        {
            _courante = this;
        }
    }

    /// <summary>Vrai dès que Quitter a été demandé : la croix de la fenêtre ne doit plus se contenter de la masquer.</summary>
    internal static bool ArretEnCours { get; private set; }

    /// <summary>
    /// Appelé par l'événement <c>Activated</c> de l'instance principale (sur un fil quelconque) quand une seconde instance
    /// a redirigé son activation. L'ouverture de la fenêtre est ramenée sur le fil d'interface ; si l'application
    /// n'a pas fini de démarrer, elle est jouée à la fin du démarrage.
    /// </summary>
    internal static void SignalerActivation()
    {
        App courante;
        lock (_verrou)
        {
            if (_courante is not { _prete: true } prete)
            {
                _relancePendante = true;
                return;
            }

            courante = prete;
        }

        courante.PosterSurInterface(courante.SurRelance, "la relance");
    }

    /// <summary>
    /// Poste une action sur le fil d'interface en notant ses exceptions. Indispensable : une exception qui sort d'un rappel
    /// de <c>DispatcherQueue</c> ou d'une minuterie n'atteint pas <c>UnhandledException</c> mais arrête le processus (code
    /// 0xC000027B) sans rien écrire au journal, ce qui a été constaté à l'essai.
    /// </summary>
    /// <param name="action">Travail à faire sur le fil d'interface.</param>
    /// <param name="contexte">Ce que l'action fait, pour le message du journal (« la relance »).</param>
    internal void PosterSurInterface(Action action, string contexte) =>
        _fil.TryEnqueue(() => ExecuterProtege(action, contexte));

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Demarrer();
        }
        catch (Exception exception)
        {
            _journal.Erreur("Démarrage impossible.", exception);
            _dialogues.Informer(
                $"PressePapiers n'a pas pu démarrer : {exception.Message}{Environment.NewLine}{Environment.NewLine}"
                + "Le détail est dans le dossier journal des données de l'application.");
            Quitter();
        }
    }

    /// <summary>Affiche la fenêtre principale sans fenêtre d'origine (icône, relance) ; fil d'interface seulement.</summary>
    internal void AfficherFenetre(OrigineOuverture origine)
    {
        if (ArretEnCours)
        {
            return;
        }

        if (_fenetre is null)
        {
            _journal.Avertissement($"Ouverture demandée ({origine}) mais la fenêtre principale n'existe pas.");
            return;
        }

        // Pas de fenêtre d'origine fiable : Entrée copiera seulement.
        _fenetre.AfficherPour(0, origine);
    }

    /// <summary>
    /// Chemin Quitter : lève le drapeau, arrête ce qui écrit dans la base (capture, hook), retire l'icône, autorise la
    /// fenêtre à se fermer, libère le relais, la base et le journal, puis termine l'application. Sans effet la seconde fois.
    /// </summary>
    internal void Quitter()
    {
        if (ArretEnCours)
        {
            return;
        }

        ArretEnCours = true;
        _journal.Info("Arrêt de PressePapiers.");
        try
        {
#if DEBUG
            _minuterieArret?.Stop();
#endif
            _minuterieCrochet?.Stop();
            _capture?.Dispose();
            _crochet?.Dispose();
            _icone?.Dispose();
            _fenetre?.AutoriserFermeture();
            _relais?.Dispose();
            _base?.Dispose();
            _journalFichier?.Dispose();
        }
        catch (Exception exception)
        {
            // Une libération qui échoue ne doit pas empêcher de quitter : le processus s'arrête de toute façon.
            _journal.Erreur("Libération incomplète à l'arrêt.", exception);
        }
        finally
        {
            _capture = null;
            _crochet = null;
            _icone = null;
            _relais = null;
            _base = null;
            Exit();
        }
    }

    // Ordre de la spec : journal, dossiers, copie de sécurité et ouverture de la base, détection de session et purge,
    // fenêtre principale masquée, relais (écoute, raccourci de secours, icône), hook, démarrage avec Windows.
    private void Demarrer()
    {
        var dossiers = DossierDonnees.ParDefaut();
        dossiers.CreerDossiers();
        _journalFichier = new JournalSerilog(dossiers.Journal);
        _journal = _journalFichier;
        _journal.Info($"Démarrage de PressePapiers {typeof(App).Assembly.GetName().Version} "
                      + $"(processus {Environment.ProcessId}, données : {dossiers.Racine}).");

        _base = OuvertureBase.Ouvrir(dossiers, TimeProvider.System, _journal, _dialogues);
        if (_base is null)
        {
            Quitter();
            return;
        }

        _journal.Info($"SQLite {_base.VersionSqlite}.");
        _depot = new DepotHistorique(_base, dossiers, TimeProvider.System, _journal);

        // La purge précède tout ce qui peut capturer une copie : jamais d'effacement d'une copie faite après l'ouverture de session.
        var session = DemarrageSession.Appliquer(new SourceSessionWts(), _depot, _journal);
        _journal.Info($"Session Windows : {session.Etat}, {session.ElementsPurges} élément(s) de Temporaire purgé(s).");

        CreerFenetrePrincipale(_base, _depot, dossiers);
        DemarrerRelais(_depot);
        DemarrerCrochet();
        AppliquerDemarrageWindows();
        ProgrammerArretDeRecette();

        bool relance;
        lock (_verrou)
        {
            _prete = true;
            relance = _relancePendante;
            _relancePendante = false;
        }

        _journal.Info("Démarrage terminé, aucune fenêtre affichée.");
        if (relance)
        {
            SurRelance();
        }
    }

    // Jouée sur le fil d'interface : une seconde instance a redirigé son activation vers celle-ci.
    private void SurRelance()
    {
        _journal.Info("Activation redirigée reçue d'une seconde instance : ouverture de la fenêtre demandée.");
        AfficherFenetre(OrigineOuverture.Relance);
    }

    // Note l'exception au journal au lieu de la laisser sortir du rappel (qui arrêterait le processus sans trace).
    private void ExecuterProtege(Action action, string contexte)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _journal.Erreur($"Exception pendant {contexte}.", exception);
        }
    }

    // Recette automatique (versions Debug seulement) : PRESSEPAPIERS_ARRET_APRES_MS demande un arrêt propre, par le chemin
    // Quitter, au bout de ce délai : un moyen de vérifier l'arrêt sans tuer le processus ni cliquer sur l'icône.
    private void ProgrammerArretDeRecette()
    {
#if DEBUG
        if (int.TryParse(Environment.GetEnvironmentVariable("PRESSEPAPIERS_ARRET_APRES_MS"), out var delai) && delai > 0)
        {
            _minuterieArret = _fil.CreateTimer();
            _minuterieArret.Interval = TimeSpan.FromMilliseconds(delai);
            _minuterieArret.IsRepeating = false;
            _minuterieArret.Tick += (_, _) => ExecuterProtege(Quitter, "l'arrêt de recette");
            _minuterieArret.Start();
            _journal.Info($"Arrêt de recette programmé dans {delai} ms.");
        }
#endif
    }

    // L'outil continue chaque fois que c'est possible (spec, section 13) : l'exception est notée puis déclarée traitée.
    private void SurExceptionInterface(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _journal.Erreur("Exception non gérée de l'interface.", e.Exception);
    }

    // Celle-ci arrive en dernier recours : le processus s'arrête, il ne reste qu'à la noter.
    private void SurExceptionDomaine(object? sender, System.UnhandledExceptionEventArgs e) =>
        _journal.Erreur($"Exception non gérée du processus (arrêt en cours : {e.IsTerminating}).", e.ExceptionObject as Exception);

    private void SurExceptionTache(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        _journal.Erreur("Exception de tâche non observée.", e.Exception);
    }
}
```

`src/PressePapiers/App.Composition.cs` (nouveau fichier)

```csharp
using PressePapiers.Capture;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Interface;
using PressePapiers.Systeme;

namespace PressePapiers;

// Composition des briques : fenêtre et modèle de vue, relais (copies, raccourci de secours, icône), hook Win+V,
// capture, état du hook affiché sur l'icône et dans les Paramètres, démarrage avec Windows.
public sealed partial class App
{
    private const string TitreNotification = "Presse-papiers";
    private const int ErreurRaccourciDejaPris = 1409;

    // Le hook se réinstalle seul toutes les 15 minutes sur son propre fil (CrochetWinV) ; cette minuterie ne fait que
    // relire son état pour l'icône et les Paramètres, et relance l'installation d'un hook inactif.
    private static readonly TimeSpan _periodeEtatCrochet = TimeSpan.FromMinutes(1);

    private (bool Actif, string? Raison)? _etatAffiche;

    // La fenêtre est créée sans être affichée : ni Show(false) préalable ni Activate, l'essai du point 2 de la spec
    // (section 16) a montré que AppWindow.Show puis PremierPlan.Amener suffisent à la première ouverture. Le relais est
    // créé avant elle : le collage écrit dans le presse-papiers au nom de la fenêtre du relais.
    private void CreerFenetrePrincipale(BaseHistorique baseHistorique, DepotHistorique depot, DossierDonnees dossiers)
    {
        var parametres = new ServiceParametres(dossiers.Parametres, _journal);
        _relais = new RelaisWindows(_journal);
        var recollage = new ServiceRecollage(new EcrivainPressePapiers(_relais.Handle, _journal), depot, _journal);
        _vue = new FenetrePrincipaleVue(depot, new MoteurRecherche(baseHistorique), recollage, parametres, _journal);
        _fenetre = new FenetrePrincipale(_vue);

        _vue.Notifier = (titre, message) => _icone?.Notifier(titre, message);
        _vue.AppliquerRaccourci = EnregistrerRaccourci;
        _vue.ParametresModifies += () => SurParametresModifies(depot);
        _fenetre.PremierPlanRefuse += () => _icone?.Notifier(
            TitreNotification, "Windows a refusé d'afficher la fenêtre au premier plan : clique sur l'icône pour l'ouvrir.");
        AppliquerPlafond(depot);
    }

    // Relais : écoute du presse-papiers et capture, raccourci de secours, icône de notification et son menu.
    private void DemarrerRelais(DepotHistorique depot)
    {
        var relais = _relais!;
        var vue = _vue!;

        _capture = new ServiceCapture(relais, new LecteurPressePapiers(relais.Handle, _journal), depot, () => vue.Reglages, _journal);
        if (!relais.EcouterPressePapiers())
        {
            _journal.Erreur("Les copies ne seront pas enregistrées : écoute du presse-papiers impossible.");
        }

        // Levé pendant le traitement de WM_HOTKEY : Windows accorde alors le premier plan au processus, la fenêtre
        // active est relevée avant que quoi que ce soit ne la change.
        relais.RaccourciSecoursPresse += () => OuvrirDepuisRaccourci(PremierPlan.FenetreActive(), OrigineOuverture.RaccourciSecours);
        relais.SessionDeverrouillee += () => ReinstallerCrochet("déverrouillage de la session");
        relais.SortieDeVeille += () => ReinstallerCrochet("sortie de veille");

        _icone = new IconeNotification(relais, _journal);
        _icone.OuvrirDemande += () => AfficherFenetre(OrigineOuverture.Icone);
        _icone.ParametresDemandes += () => _fenetre?.AfficherParametres();

        // Hors de la procédure du relais : Quitter détruit la fenêtre du relais, qui ne doit plus être dans sa pile d'appels.
        _icone.QuitterDemande += () => PosterSurInterface(Quitter, "l'arrêt demandé par l'icône");

        EnregistrerRaccourciAuDemarrage(vue.Reglages.RaccourciSecours);
    }

    // Le hook lève WinVPresse sur son propre fil : l'ouverture est ramenée sur le fil d'interface.
    private void DemarrerCrochet()
    {
        var crochet = new CrochetWinV(_journal);
        _crochet = crochet;
        crochet.WinVPresse += fenetre => PosterSurInterface(
            () => OuvrirDepuisRaccourci(fenetre, OrigineOuverture.WinV), "l'ouverture par Win+V");
        crochet.Demarrer();
        ActualiserEtatCrochet();

        _minuterieCrochet = _fil.CreateTimer();
        _minuterieCrochet.Interval = _periodeEtatCrochet;
        _minuterieCrochet.IsRepeating = true;
        _minuterieCrochet.Tick += (_, _) => ExecuterProtege(SurveillerCrochet, "la surveillance du hook");
        _minuterieCrochet.Start();
    }

    // Win+V ou raccourci de secours : ignoré si la fenêtre est déjà affichée, ou moins de 300 ms après une ouverture.
    private void OuvrirDepuisRaccourci(nint fenetreOrigine, OrigineOuverture origine)
    {
        if (ArretEnCours || _fenetre is null || _fenetre.EstVisible || !_antiRebond.Accepter())
        {
            return;
        }

        _fenetre.AfficherPour(fenetreOrigine, origine);
    }

    private void ReinstallerCrochet(string raison)
    {
        _journal.Info($"Réinstallation du hook Win+V demandée ({raison}).");
        _crochet?.Reinstaller();
    }

    private void SurveillerCrochet()
    {
        if (_crochet is { EstActif: false } crochet)
        {
            crochet.Reinstaller();
        }

        ActualiserEtatCrochet();
    }

    // Aspect d'alerte de l'icône et texte des Paramètres, seulement quand l'état change.
    private void ActualiserEtatCrochet()
    {
        if (_crochet is null || _icone is null || _vue is null)
        {
            return;
        }

        var etat = (_crochet.EstActif, _crochet.RaisonInactif);
        if (_etatAffiche == etat)
        {
            return;
        }

        _etatAffiche = etat;
        var raccourci = _vue.Reglages.RaccourciSecours;
        if (etat.EstActif)
        {
            _vue.EtatCrochet = "Actif : Win+V ouvre PressePapiers.";
            _icone.Afficher(alerte: false, TitreNotification);
        }
        else
        {
            _vue.EtatCrochet = $"Inactif : {etat.RaisonInactif} Le raccourci de secours {raccourci} reste disponible.";
            _icone.Afficher(alerte: true, $"{TitreNotification} : Win+V inactif, utiliser {raccourci}");
            _journal.Avertissement($"Hook Win+V inactif : {etat.RaisonInactif}");
        }
    }

    // Raccourci des réglages ; un texte illisible (fichier modifié à la main) laisse la place au défaut.
    private void EnregistrerRaccourciAuDemarrage(string texte)
    {
        if (!Raccourci.TryAnalyser(texte, out var raccourci))
        {
            _journal.Avertissement($"Raccourci de secours « {texte} » illisible : le défaut est utilisé.");
            Raccourci.TryAnalyser(new Parametres().RaccourciSecours, out raccourci);
        }

        if (EnregistrerRaccourci(raccourci) is { } refus)
        {
            _icone?.Notifier(TitreNotification, refus);
        }
    }

    // Branché sur les Paramètres : null si Windows accepte la combinaison, sinon le message à montrer.
    private string? EnregistrerRaccourci(Raccourci raccourci)
    {
        if (_relais is null)
        {
            return "Le raccourci ne peut pas être enregistré pendant l'arrêt.";
        }

        if (_relais.EnregistrerRaccourci(raccourci, out var code))
        {
            _journal.Info($"Raccourci de secours {raccourci} enregistré.");
            return null;
        }

        return code == ErreurRaccourciDejaPris
            ? $"Le raccourci {raccourci} est déjà utilisé par une autre application : choisis une autre combinaison dans les Paramètres."
            : $"Windows a refusé le raccourci {raccourci} (code {code}).";
    }

    // Le thème, le mode de collage et la chasse fixe sont appliqués par la fenêtre et la vue, le raccourci par
    // EnregistrerRaccourci, le démarrage avec Windows par la vue elle-même. Restent le plafond et le texte d'état du hook,
    // qui cite le raccourci de secours.
    private void SurParametresModifies(DepotHistorique depot)
    {
        AppliquerPlafond(depot);
        _etatAffiche = null;
        ActualiserEtatCrochet();
    }

    // Un plafond abaissé dans les Paramètres s'applique tout de suite, sans attendre la prochaine copie.
    private void AppliquerPlafond(DepotHistorique depot)
    {
        if (_vue is null)
        {
            return;
        }

        try
        {
            var supprimes = depot.AppliquerPlafond(_vue.Reglages.PlafondTemporaire);
            if (supprimes > 0)
            {
                _journal.Info($"Plafond de Temporaire : {supprimes} élément(s) ancien(s) supprimé(s).");
            }
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Plafond de Temporaire non appliqué.", exception);
        }
    }

    // Aligne la clé Run sur les réglages, avec le chemin de l'exécutable en cours (il change après une publication).
    // Un refus du registre est noté sans empêcher le démarrage.
    private void AppliquerDemarrageWindows()
    {
        var activer = _vue?.Reglages.DemarrageAvecWindows ?? false;
        var chemin = Environment.ProcessPath;
        if (string.IsNullOrEmpty(chemin))
        {
            _journal.Avertissement("Chemin de l'exécutable inconnu : démarrage avec Windows non modifié.");
            return;
        }

        try
        {
            DemarrageWindows.Appliquer(activer, chemin);
            _journal.Info($"Démarrage avec Windows : {(activer ? "activé" : "désactivé")}.");
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Démarrage avec Windows non modifié.", exception);
        }
    }
}
```

`scripts/publier.ps1` (nouveau fichier, enregistré en UTF-8 avec BOM pour Windows PowerShell 5.1)

```powershell
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
```

`README.md` (nouveau fichier)

````markdown
# PressePapiers

Gestionnaire de presse-papiers pour Windows 11, qui remplace l'historique de Win+V par un outil personnel plus complet :

- un historique bien plus grand que les 25 éléments de Windows (5 000 éléments dans l'onglet Temporaire par défaut, réglable) ;
- la recherche dans tout l'historique (casse et accents ignorés) ;
- des éléments gardés sous un nom, rangés dans des onglets ;
- un onglet Temporaire pour le reste, vidé à chaque nouvelle session Windows (arrêt, redémarrage, déconnexion), mais pas quand
  l'outil est relancé ;
- l'ouverture par Win+V et le recollage dans la fenêtre d'origine.

Contenus capturés : texte, texte mis en forme (RTF et `HTML Format`), images, listes de fichiers. **Tout est enregistré,
sans exception** : les contenus qu'une application demande à exclure de l'historique (gestionnaires de mots de passe, par
exemple) le sont aussi. C'est un choix assumé ; la base n'est pas chiffrée.

Application WinUI 3 (Windows App SDK 2.5.1) en C# sur .NET 10, non empaquetée et autonome, qui vit dans la zone de
notification. Conception : `docs/superpowers/specs/2026-10-13-pressepapiers-design.md`. Licence MIT.

## Installation

Prérequis pour compiler : Windows 11 x64 et le SDK .NET 10. Aucun droit administrateur n'est nécessaire, et la machine
n'a pas besoin du runtime .NET ni du Windows App SDK : la publication les embarque.

```powershell
pwsh -NoProfile -File scripts\publier.ps1 -Lancer
```

Le script publie en Release (win-x64, autonome) dans `publish\PressePapiers` (environ 231 Mo), arrête l'instance en cours
s'il y en a une, copie la publication dans `%LOCALAPPDATA%\Programs\PressePapiers`, puis lance la version installée
(`-Lancer`). `-Destination <dossier>` choisit un autre dossier d'installation. Pour mettre à jour, relancer le même script.

Au lancement, l'application inscrit son chemin dans `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (valeur
`PressePapiers`) si le démarrage avec Windows est activé dans ses réglages, ce qui est le cas par défaut ; sinon elle retire
cette valeur.

Windows 11 range une nouvelle icône dans le menu de débordement de la zone de notification (la flèche vers le haut).
L'épingler dans la barre des tâches permet de voir son aspect d'alerte (pastille rouge) quand Win+V n'est pas repris.

**Désinstaller :** désactiver « Démarrer avec Windows » dans les Paramètres, quitter par le menu de l'icône, puis supprimer
`%LOCALAPPDATA%\Programs\PressePapiers` (et `%LOCALAPPDATA%\PressePapiers` pour effacer aussi l'historique).

## Utilisation

Copier comme d'habitude (Ctrl+C, menu Copier) : chaque copie arrive en tête de l'onglet Temporaire. Un contenu déjà
présent dans Temporaire n'est pas dupliqué, il remonte en tête.

Win+V ouvre la fenêtre au centre de l'écran de la fenêtre active, sur Temporaire, premier élément choisi, curseur dans la
recherche. Taper pour chercher, choisir avec les flèches, Entrée pour coller dans la fenêtre d'origine. La fenêtre se masque
après un collage, avec Échap ou quand elle perd le focus. Un raccourci de secours fait la même chose (Ctrl+Alt+Maj+V par
défaut) ; il sert aussi quand Win+V n'est pas repris, par exemple devant une fenêtre lancée en administrateur.

Ouverte depuis l'icône de notification ou par un second lancement de l'application, la fenêtre n'a pas de fenêtre d'origine
fiable : Entrée copie alors l'élément, à coller ensuite avec Ctrl+V. Devant une fenêtre lancée en administrateur, Windows
bloque le collage simulé : l'élément est copié et une notification dit de coller avec Ctrl+V.

**Garder un élément :** F2 (ou le bouton Nommer), saisir un nom, choisir un onglet autre que Temporaire, Entrée ou
Enregistrer. Un élément nommé est gardé sans limite de durée ; il peut être renommé, déplacé ou supprimé. Les onglets se
créent par « Nouvel onglet » dans le volet de gauche ; un onglet non vide demande confirmation avant d'être supprimé, avec
ses éléments. L'onglet Temporaire ne se renomme pas et ne se supprime pas.

**Icône de notification :** clic pour ouvrir ; clic droit pour le menu Ouvrir, Paramètres, Quitter.

### Raccourcis de la fenêtre

| Touche | Action |
|---|---|
| saisie | chercher dans tout l'historique (tous les onglets) |
| Haut, Bas | choisir un élément |
| Entrée, double-clic | coller dans la fenêtre d'origine |
| Maj+Entrée | coller dans l'autre mode de mise en forme (texte seul ou avec mise en forme) |
| Ctrl+Entrée (avec ou sans Maj) | copier seulement, sans coller |
| F2 | aller au champ Nom |
| Entrée dans le champ Nom | enregistrer (nom et onglet requis) |
| Suppr | supprimer l'élément, seulement quand la recherche est vide (sinon Suppr efface un caractère) ; un élément nommé demande confirmation |
| Ctrl+Tab, Ctrl+Maj+Tab | onglet suivant, précédent |
| Ctrl+1 à Ctrl+9 | aller à l'onglet n (Ctrl+1 : Temporaire) |
| Échap | effacer la recherche, puis masquer la fenêtre |

Tab garde son rôle normal (passer d'un contrôle à l'autre).

## Réglages

Page Paramètres de la fenêtre (entrée en bas du volet, ou menu de l'icône) :

| Réglage | Défaut |
|---|---|
| Raccourci de secours | Ctrl+Alt+Maj+V (Ctrl ou Alt obligatoire, jamais la touche Windows ; touche : lettre, chiffre, F1 à F24 ou Espace). Un message s'affiche si Windows le refuse parce qu'une autre application l'a déjà pris. |
| Démarrer avec Windows | activé |
| Mise en forme par défaut au collage | texte seul |
| Plafond de Temporaire | 5 000 éléments (0 = sans plafond) ; au-delà, les plus anciens sont supprimés |
| Thème | Système (ou Clair, Sombre) |
| Applications affichées en chasse fixe | terminaux et éditeurs de code courants |

La page montre aussi l'état du hook Win+V (actif, ou inactif avec la raison) et deux boutons, « Ouvrir le dossier des
données » et « Ouvrir le journal ». Les réglages sont dans `parametres.json` ; si le fichier est illisible, l'application
prend les défauts et le note au journal.

## Dossier des données

`%LOCALAPPDATA%\PressePapiers` :

| Élément | Contenu |
|---|---|
| `historique.db` | base SQLite (mode WAL) : onglets, éléments, index de recherche plein texte |
| `images\` | une image PNG par élément de type image |
| `sauvegardes\` | copie de la base faite à chaque démarrage, les 3 dernières gardées ; une base illisible au démarrage propose de restaurer la plus récente |
| `journal\` | journal de l'application, un fichier par jour, 14 jours gardés |
| `parametres.json` | réglages |

La variable d'environnement `PRESSEPAPIERS_DONNEES` remplace ce dossier par un autre : pour essayer l'application sans
toucher aux vraies données. Pour un essai, y créer d'abord un `parametres.json` contenant `{ "DemarrageAvecWindows": false }`,
sinon le premier lancement inscrit l'exécutable d'essai dans la clé Run. Dans une version Debug,
`PRESSEPAPIERS_ARRET_APRES_MS` demande un arrêt propre au bout de ce délai.

## Développement

```powershell
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Projets : `src\PressePapiers.Coeur` (logique sans WinUI : modèle, stockage, recherche, session, réglages, formats),
`src\PressePapiers` (application WinUI 3), `tests\PressePapiers.Tests` (xUnit v3, sur Coeur seulement).
`scripts\generer-icones.ps1` régénère les icônes de la zone de notification (Windows PowerShell 5.1).

Comportements à connaître :

- Le hook Win+V (`WH_KEYBOARD_LL`) vit sur un fil dédié et se réinstalle seul toutes les 15 minutes ; l'application le
  réinstalle aussi au déverrouillage de la session et à la sortie de veille. Arrêté ou planté, l'outil rend la main à
  l'historique natif de Windows, laissé activé.
- La capture lit le presse-papiers sur le fil d'interface et y enregistre l'élément : copier un texte de plusieurs
  mégaoctets gèle la fenêtre une à deux secondes (indexation plein texte). Le dépôt et la recherche partagent un seul
  verrou, la connexion SQLite : ils restent sûrs si cette écriture passe un jour sur un autre fil.
- Une exception qui sort d'un rappel de `DispatcherQueue` ou d'une minuterie arrête le processus sans passer par
  `UnhandledException` : tout travail posté sur le fil d'interface passe par `App.PosterSurInterface`, qui la note au journal.

## Recette manuelle

À jouer sur le poste après installation (spec, section 14). Une case par vérification.

### Win+V et raccourci de secours

- [ ] Win+V dans plusieurs applications (terminal, éditeur de code, navigateur, Office, Explorateur) : la fenêtre s'ouvre au premier plan, au centre de l'écran de la fenêtre active.
- [ ] Le menu Démarrer ne s'ouvre pas au relâchement de Win après Win+V.
- [ ] Win seul, Win+E et Win+Maj+S fonctionnent toujours.
- [ ] Win+V en gardant Win enfoncée après V : une seule ouverture, fenêtre au premier plan.
- [ ] Win+V tenu (répétition automatique) et double Win+V rapide : une seule ouverture.
- [ ] Raccourci de secours (Ctrl+Alt+Maj+V) depuis une application ordinaire : même ouverture.
- [ ] Changer le raccourci de secours dans les Paramètres, puis essayer une combinaison déjà prise par une autre application : message sous le champ, l'ancien raccourci reste actif.
- [ ] Collage spécial d'Excel (Ctrl+Alt+V) toujours disponible pendant que l'outil tourne.

### Fenêtre administrateur

- [ ] Devant une fenêtre lancée en administrateur (Gestionnaire des tâches, terminal administrateur) : Win+V ouvre l'historique natif de Windows.
- [ ] Devant cette même fenêtre : le raccourci de secours ouvre-t-il la fenêtre ? (non établi par la documentation ; noter le résultat) ; puis Entrée : pas de collage, notification « Copié, colle avec Ctrl+V ».
- [ ] Essayer aussi une combinaison Ctrl+Maj+lettre comme raccourci de secours devant cette fenêtre.
- [ ] La fenêtre de l'outil s'affiche-t-elle au premier plan quand une application élevée a le focus ?

### Copie et recollage de chaque type

- [ ] Texte depuis un éditeur, recollé dans un autre.
- [ ] Texte mis en forme copié depuis Word : aperçu fidèle (gras, couleurs, tableau) ; recollé en texte seul par Entrée et avec la mise en forme par Maj+Entrée.
- [ ] Texte mis en forme copié depuis Outlook : même contrôle. Un tableau plus large que la colonne d'aperçu est coupé à droite (limite connue).
- [ ] Texte copié depuis une page web (HTML seul) : aperçu en texte brut avec la mention « mise en forme conservée pour le collage », recollé avec la mise en forme dans Word.
- [ ] Image copiée (capture Win+Maj+S, image d'une page web) et recollée dans Word, Excel, PowerPoint, Outlook, un navigateur et Paint.
- [ ] Image avec transparence (PNG copié depuis un navigateur ou un éditeur d'images) : transparence conservée au recollage dans ces mêmes applications ?
- [ ] Fichiers copiés dans l'Explorateur, recollés dans un autre dossier : les fichiers sont copiés.
- [ ] Copie depuis une application du Store (Calculatrice, Photos) : application d'origine reconnue dans la liste.
- [ ] Ctrl+Entrée : copie seulement, message « Copié dans le presse-papiers ».

### Fenêtre d'origine

- [ ] Fenêtre d'origine réduite entre l'ouverture et Entrée : elle est restaurée et reçoit le collage.
- [ ] Fenêtre d'origine fermée entre-temps : copie seulement, message dans la fenêtre.
- [ ] Ouverture depuis l'icône : Entrée copie seulement, message dans la fenêtre.

### Fenêtre et affichage

- [ ] Première ouverture après le démarrage : pas de cadre vide gênant (la fenêtre peint son contenu quelques dizaines de millisecondes après son apparition).
- [ ] Perte du focus : la fenêtre se masque, sauf pendant une boîte de dialogue.
- [ ] La croix masque la fenêtre sans quitter l'application.
- [ ] Taille de la fenêtre changée puis rouverte : taille gardée.
- [ ] Écran à 150 %, puis deux écrans d'échelles différentes : taille et centrage justes sur chaque écran.
- [ ] Thèmes Clair, Sombre et Système : toute la fenêtre suit, boutons de la barre de titre compris.

### Sessions et onglet Temporaire

- [ ] Arrêt puis rallumage avec démarrage rapide : Temporaire vidé.
- [ ] Redémarrage : Temporaire vidé.
- [ ] Fermeture de session puis reconnexion : Temporaire vidé.
- [ ] Win+L puis déverrouillage : Temporaire conservé, Win+V toujours repris.
- [ ] Veille puis reprise : Temporaire conservé, Win+V toujours repris.
- [ ] Reprise d'une connexion Bureau à distance : Temporaire conservé.
- [ ] Relance de l'outil (Quitter puis relancer) : Temporaire conservé.
- [ ] Outil lancé une fois en administrateur : Temporaire conservé.
- [ ] Les éléments nommés survivent à tous ces cas.

### Icône, instance unique, erreurs

- [ ] Redémarrage de l'Explorateur (Gestionnaire des tâches, Redémarrer l'Explorateur Windows) : l'icône réapparaît.
- [ ] Clic sur l'icône : la fenêtre s'ouvre ; clic droit : menu Ouvrir, Paramètres, Quitter, chacun fonctionne.
- [ ] Hook inactif (rare ; sinon à constater si l'icône prend l'aspect d'alerte) : pastille rouge sur l'icône épinglée et raison dans les Paramètres.
- [ ] Double lancement pendant que la fenêtre est masquée : la fenêtre s'ouvre au premier plan, pas de seconde instance.
- [ ] Démarrage avec Windows : après une ouverture de session, l'icône est là (Windows peut retarder les programmes de démarrage de quelques secondes).
- [ ] Une erreur de l'interface (si l'occasion se présente) est notée au journal et l'outil continue.

## Points de la conception encore à vérifier sur le poste

État au 13/10/2026 des points de la section 16 de la spec, après les essais du développement. Ceux-ci restent à vérifier
à la main :

- [ ] **Point 1, heure d'ouverture de session (`LogonTime`)** : identique pour un processus normal et un processus élevé (terminal administrateur) ; inchangée après Win+L, veille et reprise Bureau à distance ; changée après « Arrêter » en démarrage rapide puis rallumage. Mesuré seulement dans un processus normal (deux lectures identiques, même valeur dans un processus neuf).
- [ ] **Point 3, RTF dans l'aperçu** : rendu d'un RTF réellement copié depuis Word et Outlook (essayé seulement sur un RTF écrit à la main).
- [ ] **Point 4, icône** : vrais clics de souris sur l'icône (le menu et l'ouverture n'ont été essayés que par messages postés) ; redémarrage réel de l'Explorateur.
- [ ] **Point 5, raccourci de secours devant une fenêtre administrateur** (Ctrl+Alt+Maj+V et une combinaison Ctrl+Maj+lettre, devant le Gestionnaire des tâches).
- [ ] **Point 6, Win+V devant une fenêtre administrateur** : l'historique natif reprend la main.
- [ ] **Point 7, format PNG à l'écriture** : accepté par Word, Excel, PowerPoint, Outlook, un navigateur et Paint, transparence comprise (la lecture d'un `CF_DIBV5` avec transparence est vérifiée).
- [ ] **Hook Win+V réel** : l'avalement de Win+V par la vraie touche Windows n'a pas pu être simulé (seule l'injection l'a été) ; si Win+V n'est pas repris, c'est le premier point à examiner (lecture de la touche Windows par `GetAsyncKeyState`).
````

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 576
  échec : 0
  réussie : 576
  ignoré : 0
```

Aucun nouveau test xUnit : les 576 tests sont ceux des tâches 1 à 28.

- [ ] **Étape 5 : Vérifier sur le poste**

1. Essai de fumée de la version Debug (même commande qu'à l'étape 2). Il refuse de démarrer si une instance de PressePapiers
   tourne déjà. Il lance une petite fenêtre Windows Forms comme fenêtre tierce, l'amène au premier plan, envoie les frappes
   par `SendInput`, quitte l'application par son menu (message `WM_CONTEXTMENU` posté au relais comme le Shell le fait, puis
   Q), remet un texte neutre dans le presse-papiers et vérifie qu'aucun processus ne reste et que la clé Run est inchangée.
   Si une fenêtre de niveau d'intégrité supérieur (un outil de bureau à distance lancé en administrateur, par exemple) est au
   premier plan, Windows avale les frappes injectées : les contrôles concernés sortent en `NON TESTE`, il faut relancer avec
   une fenêtre ordinaire au premier plan. Le presse-papiers est partagé avec tout le poste : une copie faite par une autre
   application pendant l'essai (ou une fenêtre qui reprend le premier plan) fait échouer les contrôles qui lisent le dernier
   élément ou le premier plan ; relancer l'essai sans toucher au poste. L'essai remplace le contenu du presse-papiers.

```bash
DEPOT="$(git rev-parse --show-toplevel)"; ESSAIS="$LOCALAPPDATA/Temp/essais-assemblage"
pwsh -NoProfile -File "$ESSAIS/fumee.ps1" -Depot "$DEPOT" -Lecteur "$ESSAIS/LectureBase/bin/Debug/net10.0-windows10.0.19041.0/LectureBase.exe"
```

Résultat attendu, code de sortie 0 (horodatage du journal retiré ; sortie relevée le 13/10/2026 sur le code de cette tâche) :

```
OK          démarrage terminé au journal
OK          aucune fenêtre visible au démarrage
OK          fenêtre du relais trouvée
OK          fenêtre principale créée, masquée
OK          icône présente dans la zone de notification
OK          journal : hook Win+V posé
OK          journal : raccourci de secours enregistré
OK          journal : démarrage avec Windows désactivé
            version de SQLite : 3.53.3
OK          texte copié enregistré en base (Temporaire)
OK          texte : type 1, onglet Temporaire
OK          image copiée enregistrée en base (type 3)
OK          PNG de l'image présent sur disque
OK          raccourci de secours : fenêtre principale visible
OK          raccourci de secours : fenêtre principale au premier plan
OK          journal : aucune exception
OK          Échap : fenêtre masquée
OK          Échap : l'application continue
OK          second lancement : la seconde instance s'arrête, code 0
OK          second lancement : fenêtre de la première instance visible
OK          second lancement : fenêtre au premier plan
OK          un seul processus PressePapiers
OK          Échap après la relance : fenêtre masquée
OK          arrêt propre : le processus se termine seul
OK          arrêt propre : code de sortie 0
OK          journal : arrêt noté
OK          icône retirée de la zone de notification
OK          journal : aucune erreur
OK          clé Run de l'utilisateur inchangée
OK          aucun processus PressePapiers restant
Journal complet :
[INF] Démarrage de PressePapiers 1.0.0.0 (processus 36712, données : C:\Users\<utilisateur>\AppData\Local\Temp\pp-fumee-1ab59523555f4ebca9e6a002c12ec076).
[INF] SQLite 3.53.3.
[INF] Premier lancement : repère de session enregistré, Temporaire conservé.
[INF] Session Windows : PremierLancement, 0 élément(s) de Temporaire purgé(s).
[INF] Raccourci de secours Ctrl+Alt+Maj+V enregistré.
[INF] Hook Win+V posé.
[INF] Démarrage avec Windows : désactivé.
[INF] Arrêt de recette programmé dans 240000 ms.
[INF] Démarrage terminé, aucune fenêtre affichée.
[INF] Activation redirigée reçue d'une seconde instance : ouverture de la fenêtre demandée.
[INF] Seconde instance (processus 34528) : activation redirigée vers l'instance principale, arrêt.
[INF] Arrêt de PressePapiers.

0 contrôle(s) non testé(s).
Tous les contrôles exécutés sont réussis.
```

2. Publication dans un dossier d'essai (jamais le vrai dossier d'installation pendant la vérification), puis le même essai sur
   la version publiée (Release, sans le filet d'arrêt Debug : seul le menu de l'icône l'arrête) :

```bash
DEPOT="$(git rev-parse --show-toplevel)"; ESSAIS="$LOCALAPPDATA/Temp/essais-assemblage"
pwsh -NoProfile -File "$DEPOT/scripts/publier.ps1" -Destination "$ESSAIS/installe"
pwsh -NoProfile -File "$ESSAIS/fumee.ps1" -Depot "$DEPOT" -Lecteur "$ESSAIS/LectureBase/bin/Debug/net10.0-windows10.0.19041.0/LectureBase.exe" -Executable "$ESSAIS/installe/PressePapiers.exe"
```

Attendu : la publication se termine par `Publication : 231,2 Mo dans ...\publish\PressePapiers` (taille relevée le 13/10/2026,
à quelques centaines de kilo-octets près selon les paquets) puis `Installé : ...\installe\PressePapiers.exe`, code de sortie 0 ;
l'essai de fumée donne les mêmes 29 lignes `OK` qu'en Debug, sans la ligne `Arrêt de recette programmé` au journal.
Supprimer ensuite le dossier d'essai : `rm -rf "$ESSAIS/installe"`.

3. Recette manuelle : la liste de cases du README (section « Recette manuelle ») et les points de la section 16 encore
   ouverts (section « Points de la conception encore à vérifier sur le poste ») se jouent après installation réelle.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/App.xaml.cs src/PressePapiers/App.Composition.cs scripts/publier.ps1 README.md
git commit -m "Assembler l'application : relais, icône, hook Win+V, capture, recollage et démarrage avec Windows" -m "App compose toutes les briques : écoute du presse-papiers et capture, raccourci de
secours depuis les réglages, icône avec aspect d'alerte si le hook est inactif, Win+V
ramené sur le fil d'interface avec un anti-rebond de 300 ms, réinstallation du hook au
déverrouillage et à la sortie de veille, démarrage avec Windows selon les réglages.
Script de publication sans droits administrateur et README avec la recette manuelle."
```
