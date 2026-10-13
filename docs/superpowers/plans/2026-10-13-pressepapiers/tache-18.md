### Tâche 18 : IconeNotification, icône de la zone de notification et son menu natif

**Fichiers :**
- Créer : `src/PressePapiers/Natif/NatifIcone.cs`
- Créer : `scripts/generer-icones.ps1`
- Créer : `src/PressePapiers/Assets/icone-normale.ico` et `src/PressePapiers/Assets/icone-alerte.ico` (binaires, produits par
  le script, 16, 20, 24, 32 et 48 pixels chacun, versionnés tels quels)
- Modifier : `src/PressePapiers/Systeme/IconeNotification.cs` (le plan donne le contenu complet après modification)
- Modifier : `src/PressePapiers/PressePapiers.csproj` (un `ItemGroup` ajouté qui copie `Assets\*.ico` à la sortie ; le plan
  donne le contenu complet après modification)
- Tests : aucun test xUnit possible (Shell Windows). Vérification sur le poste par le programme jetable de la tâche 17 :
  `%LOCALAPPDATA%\Temp\essais-relais\EssaisIcone.cs` (remplace le bouchon), plus `capture.ps1` et `apercu.ps1`.

**Interfaces :**
- Utilise :
  - `RelaisWindows` (tâche 17) : `nint Handle`, `event Action<uint>? MessageIconeRecu`, `event Action? BarreDesTachesRecreee`,
    `const uint MessageIcone = 0x8001` ;
  - `IJournal` (tâche 1) ; la coquille `IconeNotification` de la tâche 1 : toutes ses signatures publiques restent identiques.
- Fournit (signatures du contrat, inchangées) :
  - `public sealed class IconeNotification : IDisposable` dans `PressePapiers.Systeme` ;
  - `public IconeNotification(RelaisWindows relais, IJournal journal)` ; rien n'apparaît avant `Afficher` ;
  - `public void Afficher(bool alerte, string infobulle)` : NIM_ADD la première fois, NIM_MODIFY ensuite ;
  - `public void Notifier(string titre, string message)` : bulle NIF_INFO (fait apparaître l'icône si besoin) ;
  - événements `Action? OuvrirDemande`, `Action? ParametresDemandes`, `Action? QuitterDemande` ;
  - `public void Dispose()` : NIM_DELETE, libération des icônes ;
  - en plus, `internal const uint IdIcone = 1` (identifiant de l'icône auprès du Shell, utile aux essais) et
    `internal static partial class NatifIcone` dans `PressePapiers.Natif`.
- Fichiers produits au build : `Assets\icone-normale.ico` et `Assets\icone-alerte.ico` à côté de l'exécutable (Content copié ;
  `dotnet publish` les reprend aussi, tâche 29).

**Points d'attention pour la suite :**
- Windows 11 range par défaut toute nouvelle icône dans le menu de débordement (le chevron de la zone de notification) : au
  premier lancement, l'aspect d'alerte n'est donc pas visible sans que l'utilisateur épingle l'icône (Paramètres, Personnalisation,
  Barre des tâches, Autres icônes de la barre des tâches). Le plan ne touche pas au registre pour le faire à sa place
  (`NotifyIconSettings` n'est pas une interface documentée). À écrire dans le README (tâche 29, recette manuelle).
- `Shell_NotifyIconGetRect` renvoie, pour une icône masquée, la zone du chevron : on ne peut donc pas viser l'icône à la
  souris sans ouvrir d'abord le menu de débordement.
- Un processus tué sans `Dispose` laisse une icône fantôme jusqu'au survol de la souris (comportement de Windows, spec section 13).
- Les clics sont reçus en `NOTIFYICON_VERSION_4` : sélection par `NIN_SELECT` ou `NIN_KEYSELECT`, menu par `WM_CONTEXTMENU`
  (si la version 4 est refusée, repli sur `WM_LBUTTONUP` et `WM_RBUTTONUP`, avec un avertissement au journal). Le menu
  s'ouvre à la position du curseur (`TPM_BOTTOMALIGN`) et ses commandes sont renvoyées par `TPM_RETURNCMD` ; les événements
  `OuvrirDemande`, `ParametresDemandes` et `QuitterDemande` sont levés après la fermeture du menu. Les commandes portent des
  accélérateurs (O, P, Q) et « Ouvrir » est la commande par défaut.
- Pas de `[InlineArray]` dans `NOTIFYICONDATAW` : le générateur `LibraryImport` refuse ce type sans désactiver le marshaling
  du runtime pour tout l'assembly (SYSLIB1051). Les textes sont des tampons `fixed char` d'une structure `unsafe`.

- [ ] **Étape 1 : Écrire la vérification sur le poste**

Remplacer le bouchon `$ESSAIS/EssaisIcone.cs` (voir la tâche 17 pour la définition de `$DEPOT` et `$ESSAIS`) par le contenu
complet suivant. Le programme crée un relais et l'icône, vérifie par `Shell_NotifyIconGetRect` que le Shell la connaît, change
d'aspect, affiche une bulle, simule un clic gauche et un clic droit (messages d'icône postés au relais, le menu natif
s'ouvre réellement et reçoit ses accélérateurs), retire l'icône de force puis poste `TaskbarCreated` pour vérifier qu'elle
est recréée, et enfin libère le tout. Son mode `voir` laisse l'icône affichée pour la regarder.

```csharp
using PressePapiers.Natif;
using PressePapiers.Systeme;
using static EssaisRelais.Programme;

namespace EssaisRelais;

internal static partial class Essais
{
    // Position de l'icône selon le Shell ; null si le Shell ne la connaît pas.
    private static Interop.Rect? PositionIcone(nint hwnd)
    {
        var id = new Interop.IdentifiantIcone { Taille = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<Interop.IdentifiantIcone>(), Hwnd = hwnd, Id = IconeNotification.IdIcone };
        return Interop.Shell_NotifyIconGetRect(id, out var rect) == 0 ? rect : null;
    }

    private static bool IconePresente(nint hwnd) => PositionIcone(hwnd) is not null;

    private static nint TrouverMenu()
    {
        nint trouve = 0;
        Interop.EnumWindows((h, _) =>
        {
            var nom = new char[64];
            var n = Interop.GetClassNameW(h, nom, nom.Length);
            if (new string(nom, 0, n) == "#32768" && Interop.IsWindowVisible(h)) { trouve = h; return false; }
            return true;
        }, 0);
        return trouve;
    }

    private static void Touche(nint hwnd, uint vk)
    {
        Interop.PostMessageW(hwnd, 0x0100, (nint)vk, 0);
        Interop.PostMessageW(hwnd, 0x0101, (nint)vk, unchecked((nint)0xC0000001));
        Thread.Sleep(150);
    }

    // Frappe d'un caractère : les accélérateurs d'un menu (le « &O » de « &Ouvrir ») répondent à WM_CHAR.
    private static void Caractere(nint hwnd, char c)
    {
        Interop.PostMessageW(hwnd, 0x0102, c, 0);
        Thread.Sleep(150);
    }

    /// <summary>
    /// Ouvre le menu par WM_CONTEXTMENU (comme le Shell pour un clic droit en version 4), fait agir la frappe donnée sur
    /// la fenêtre du menu et dit si le menu s'est refermé. Les accélérateurs évitent de dépendre de l'élément que la souris
    /// survole au moment où le menu apparaît.
    /// </summary>
    private static bool ActionMenu(nint relais, Action<nint> agir, out bool menuVu)
    {
        Interop.PostMessageW(relais, 0x8001, 0, (1 << 16) | 0x7B);
        Thread.Sleep(700);
        var menu = TrouverMenu();
        menuVu = menu != 0;
        if (menu == 0) return false;
        agir(menu);
        Thread.Sleep(300);
        return TrouverMenu() == 0;
    }

    private static void EssaisDeLIcone()
    {
        Console.WriteLine("== Icône de notification ==");
        Verifier("NOTIFYICONDATAW fait 976 octets (x64)", System.Runtime.CompilerServices.Unsafe.SizeOf<NatifIcone.DonneesIcone>() == 976,
            System.Runtime.CompilerServices.Unsafe.SizeOf<NatifIcone.DonneesIcone>().ToString());

        var relais = Sur(() => new RelaisWindows(Journal));
        var icone = Sur(() => new IconeNotification(relais, Journal));
        var h = relais.Handle;
        var ouvrir = new Signal();
        var parametres = new Signal();
        var quitter = new Signal();
        icone.OuvrirDemande += ouvrir.Lever;
        icone.ParametresDemandes += parametres.Lever;
        icone.QuitterDemande += quitter.Lever;
        var avant = Journal.Problemes;

        Verifier("aucune icône avant Afficher", !IconePresente(h));
        Sur(() => icone.Afficher(false, "PressePapiers : essai de l'icône"));
        Verifier("NIM_ADD : l'icône existe pour le Shell", IconePresente(h));
        Sur(() => icone.Afficher(true, "PressePapiers : aspect d'alerte"));
        Verifier("NIM_MODIFY : l'icône existe toujours", IconePresente(h));
        Sur(() => icone.Notifier("PressePapiers", "Essai de la bulle de notification"));
        Verifier("Notifier : aucune erreur du Shell", Journal.Problemes == avant, $"{Journal.Problemes - avant} problème(s)");

        // Clic gauche : en version 4, NIN_SELECT.
        Interop.PostMessageW(h, 0x8001, 0, (1 << 16) | 0x400);
        Verifier("clic gauche (NIN_SELECT) : OuvrirDemande", ouvrir.Attendre(2000));

        // Menu natif : Ouvrir, Paramètres, séparateur, Quitter.
        var ok = ActionMenu(h, m => Caractere(m, 'o'), out var vu);
        Verifier("clic droit : menu natif affiché (#32768)", vu);
        Verifier("menu : accélérateur O (Ouvrir) : OuvrirDemande", ok && ouvrir.Attendre(2000), $"{ouvrir.Compte} signal(aux)");
        ok = ActionMenu(h, m => Caractere(m, 'p'), out _);
        Verifier("menu : accélérateur P (Paramètres) : ParametresDemandes", ok && parametres.Attendre(2000));
        ok = ActionMenu(h, m => Caractere(m, 'q'), out _);
        Verifier("menu : accélérateur Q (Quitter) : QuitterDemande", ok && quitter.Attendre(2000));
        var ouvrirAvant = ouvrir.Compte;
        var parametresAvant = parametres.Compte;
        var quitterAvant = quitter.Compte;
        ok = ActionMenu(h, m => Touche(m, 0x1B), out _);
        Thread.Sleep(300);
        Verifier("menu : Échap : fermé sans commande", ok && ouvrir.Compte == ouvrirAvant && parametres.Compte == parametresAvant
            && quitter.Compte == quitterAvant);

        // Redémarrage de l'Explorateur : le Shell oublie l'icône, puis diffuse TaskbarCreated.
        var donnees = new NatifIcone.DonneesIcone { Taille = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<NatifIcone.DonneesIcone>(), Hwnd = h, Id = IconeNotification.IdIcone };
        NatifIcone.NotifierIcone(NatifIcone.NimDelete, ref donnees);
        Verifier("icône retirée de force (simulation du redémarrage de l'Explorateur)", !IconePresente(h));
        Interop.PostMessageW(h, Interop.RegisterWindowMessageW("TaskbarCreated"), 0, 0);
        Thread.Sleep(800);
        Verifier("TaskbarCreated : l'icône est recréée", IconePresente(h));
        Verifier("recréation sans avertissement", Journal.Problemes == avant);
        Sur(() => icone.Notifier("PressePapiers", "Icône recréée"));
        Verifier("NIM_MODIFY fonctionne sur l'icône recréée", Journal.Problemes == avant);

        // Libération
        Sur(icone.Dispose);
        Sur(icone.Dispose);
        Verifier("Dispose : NIM_DELETE, plus d'icône", !IconePresente(h));
        Sur(relais.Dispose);
        Verifier("aucun avertissement ni erreur de l'icône au journal", Journal.Problemes == avant, $"{Journal.Problemes - avant}");
    }
}

internal static partial class Essais
{
    /// <summary>
    /// Mode « voir » (dotnet run -- voir [secondes]) : affiche l'icône d'alerte avec une bulle puis la laisse en place,
    /// et écrit chaque événement reçu, pour la regarder et la cliquer à la main dans la zone de notification.
    /// </summary>
    private static int Voir(int secondes)
    {
        var relais = Sur(() => new RelaisWindows(Journal));
        var icone = Sur(() => new IconeNotification(relais, Journal));
        icone.OuvrirDemande += () => Console.WriteLine("EVENEMENT OuvrirDemande");
        icone.ParametresDemandes += () => Console.WriteLine("EVENEMENT ParametresDemandes");
        icone.QuitterDemande += () => Console.WriteLine("EVENEMENT QuitterDemande");
        Sur(() => icone.Afficher(true, "PressePapiers : aspect d'alerte"));
        Thread.Sleep(2000);
        Sur(() => icone.Notifier("PressePapiers", "Bulle de notification d'essai"));
        Console.WriteLine("ICONE AFFICHEE");
        Thread.Sleep(secondes * 1000);
        Sur(icone.Dispose);
        Sur(relais.Dispose);
        return 0;
    }
}
```

Créer `$ESSAIS/capture.ps1` (capture le coin inférieur droit de l'écran, pour voir l'icône et la bulle) :

```powershell
<#
    Capture le coin inférieur droit de l'écran principal (zone de notification et bulles) dans un fichier PNG.
    Usage : powershell -NoProfile -ExecutionPolicy Bypass -File capture.ps1 -Png chemin.png [-Largeur 620] [-Hauteur 380]
#>
param([string]$Png, [int]$Largeur = 620, [int]$Hauteur = 380)
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace W -Name D -MemberDefinition '
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(System.IntPtr c);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);'
# Coordonnées réelles : sans cela, un écran mis à l'échelle donnerait une zone décalée.
[void][W.D]::SetProcessDpiAwarenessContext([IntPtr]-4)
$x = [W.D]::GetSystemMetrics(0) - $Largeur
$y = [W.D]::GetSystemMetrics(1) - $Hauteur
$image = New-Object System.Drawing.Bitmap($Largeur, $Hauteur)
$g = [System.Drawing.Graphics]::FromImage($image)
$g.CopyFromScreen($x, $y, 0, 0, $image.Size)
$g.Dispose()
$image.Save($Png, [System.Drawing.Imaging.ImageFormat]::Png)
```

Créer `$ESSAIS/apercu.ps1` (aperçu agrandi des deux fichiers `.ico` sur fond sombre et sur fond clair, pour juger de leur
lisibilité) :

```powershell
param([string]$Dossier, [string]$Png)
Add-Type -AssemblyName System.Drawing
$tailles = 16, 20, 24, 32, 48
$fonds = '#202020', '#F3F3F3'
$zoom = 6
$largeur = [int](20 + $zoom * 140 + 12 * 5)
$hauteurLigne = [int](48 * $zoom + 16)
$noms = 'icone-normale.ico', 'icone-alerte.ico'
$image = New-Object System.Drawing.Bitmap([int]$largeur, [int]($hauteurLigne * 4), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($image)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$ligne = 0
foreach ($nom in $noms) {
    foreach ($fond in $fonds) {
        $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($fond))), 0, $ligne * $hauteurLigne, $largeur, $hauteurLigne)
        $x = 10
        foreach ($t in $tailles) {
            $icone = New-Object System.Drawing.Icon((Join-Path $Dossier $nom), $t, $t)
            $bmp = $icone.ToBitmap()
            $g.DrawImage($bmp, $x, $ligne * $hauteurLigne + 8, $t * $zoom, $t * $zoom)
            # Taille réelle en bas à gauche de chaque agrandissement.
            $g.DrawImageUnscaled($bmp, $x, $ligne * $hauteurLigne + 8 + 48 * $zoom - $t)
            $x += $t * $zoom + 12
        }
        $ligne++
    }
}
$g.Dispose()
$image.Save($Png, [System.Drawing.Imaging.ImageFormat]::Png)
```

- [ ] **Étape 2 : Lancer la vérification contre la coquille et constater l'échec**

```bash
DEPOT="$(git rev-parse --show-toplevel)"; ESSAIS="$LOCALAPPDATA/Temp/essais-relais"
bash "$ESSAIS/synchro.sh" "$DEPOT" && cd "$ESSAIS" && dotnet build -c Debug
```

Résultat attendu : échec de compilation du programme d'essai, `NatifIcone` et `IconeNotification.IdIcone` n'existant pas encore :

```
EssaisIcone.cs(12,175): error CS0117: 'IconeNotification' ne contient pas de définition pour 'IdIcone'
EssaisIcone.cs(65,105): error CS0246: Le nom de type ou d'espace de noms 'NatifIcone' est introuvable
```

- [ ] **Étape 3 : Implémenter**

`scripts/generer-icones.ps1` (nouveau fichier, enregistré en UTF-8 avec BOM pour que Windows PowerShell 5.1 lise correctement
les accents) :

```powershell
﻿<#
.SYNOPSIS
    Génère les deux icônes de la zone de notification : icone-normale.ico et icone-alerte.ico.

.DESCRIPTION
    Dessine un presse-papiers (planche bleue, feuille claire, pince) à 16, 20, 24, 32 et 48 pixels avec System.Drawing,
    puis assemble chaque fichier .ico à la main (entrées BMP 32 bits avec canal alpha). L'aspect d'alerte ajoute une pastille
    rouge portant un point d'exclamation, pour signaler que le hook Win+V n'est pas actif.

    Le dessin ne dépend que des tailles et des couleurs du script : deux exécutions donnent les mêmes octets.
    Les fichiers produits sont versionnés dans src\PressePapiers\Assets ; relancer le script ne sert qu'à les modifier.

.PARAMETER Sortie
    Dossier de destination (créé au besoin). Par défaut : src\PressePapiers\Assets du dépôt.

.EXAMPLE
    powershell -NoProfile -File scripts\generer-icones.ps1
#>
param(
    [string]$Sortie = (Join-Path $PSScriptRoot '..\src\PressePapiers\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$tailles = 16, 20, 24, 32, 48

function Couleur([string]$hex) {
    [System.Drawing.ColorTranslator]::FromHtml($hex)
}

function Pinceau([string]$hex) {
    New-Object System.Drawing.SolidBrush (Couleur $hex)
}

# Rectangle à coins arrondis, en coordonnées d'un dessin de 100 sur 100 ramenées à la taille demandée.
function RectangleArrondi([double]$echelle, [double]$x, [double]$y, [double]$l, [double]$h, [double]$rayon) {
    $chemin = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [single](2 * $rayon * $echelle)
    $gx = [single]($x * $echelle)
    $gy = [single]($y * $echelle)
    $gl = [single]($l * $echelle)
    $gh = [single]($h * $echelle)
    $chemin.AddArc($gx, $gy, $d, $d, 180, 90)
    $chemin.AddArc($gx + $gl - $d, $gy, $d, $d, 270, 90)
    $chemin.AddArc($gx + $gl - $d, $gy + $gh - $d, $d, $d, 0, 90)
    $chemin.AddArc($gx, $gy + $gh - $d, $d, $d, 90, 90)
    $chemin.CloseFigure()
    return , $chemin
}

function Disque($g, $pinceau, [double]$echelle, [double]$cx, [double]$cy, [double]$rayon) {
    $g.FillEllipse($pinceau, [single](($cx - $rayon) * $echelle), [single](($cy - $rayon) * $echelle),
        [single](2 * $rayon * $echelle), [single](2 * $rayon * $echelle))
}

# Dessine une icône carrée de la taille donnée ; lisible sur fond clair comme sur fond sombre : la planche bleue et la
# feuille claire se détachent de l'un et de l'autre, la pince garde un liseré foncé.
function DessinerIcone([int]$taille, [bool]$alerte) {
    $e = $taille / 100.0
    $image = New-Object System.Drawing.Bitmap($taille, $taille, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($image)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $epaisseurTrait = [single][Math]::Max(1.0, $taille * 0.05)

    # Planche.
    $planche = RectangleArrondi $e 12 12 76 84 9
    $g.FillPath((Pinceau '#3478E6'), $planche)
    $g.DrawPath((New-Object System.Drawing.Pen((Couleur '#1F4FA8'), $epaisseurTrait)), $planche)

    # Feuille.
    $feuille = RectangleArrondi $e 23 25 54 63 3
    $g.FillPath((Pinceau '#F7F9FC'), $feuille)

    # Lignes de texte : deux aux petites tailles pour rester nettes, trois sinon.
    $lignes = if ($taille -le 20) { 47, 67 } else { 41, 56, 71 }
    $gris = Pinceau '#7C8AA5'
    foreach ($y in $lignes) {
        $ligne = RectangleArrondi $e 32 ($y - 3.5) 36 7 2
        $g.FillPath($gris, $ligne)
    }

    # Pince.
    $pince = RectangleArrondi $e 34 4 32 20 5
    $g.FillPath((Pinceau '#DCE3EF'), $pince)
    $g.DrawPath((New-Object System.Drawing.Pen((Couleur '#26324F'), $epaisseurTrait)), $pince)
    if ($taille -ge 32) {
        $fente = RectangleArrondi $e 43 10 14 5 2
        $g.FillPath((Pinceau '#26324F'), $fente)
    }

    # Pastille d'alerte : disque rouge cerclé de blanc et point d'exclamation.
    if ($alerte) {
        Disque $g (Pinceau '#FFFFFF') $e 71 71 29
        Disque $g (Pinceau '#E5484D') $e 71 71 25
        $barre = RectangleArrondi $e 67.5 56 7 19 3
        $g.FillPath((Pinceau '#FFFFFF'), $barre)
        Disque $g (Pinceau '#FFFFFF') $e 71 83 4.2
    }

    $g.Dispose()
    return , $image
}

# Octets d'une entrée d'icône : BITMAPINFOHEADER (hauteur doublée), pixels BGRA de bas en haut, masque AND vide.
function OctetsEntree([System.Drawing.Bitmap]$image) {
    $l = $image.Width
    $h = $image.Height
    $zone = New-Object System.Drawing.Rectangle(0, 0, $l, $h)
    $donnees = $image.LockBits($zone, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($l * 4 * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($donnees.Scan0, $pixels, 0, $pixels.Length)
    $image.UnlockBits($donnees)

    $flux = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($flux)
    $w.Write([uint32]40)
    $w.Write([int32]$l)
    $w.Write([int32]($h * 2))
    $w.Write([uint16]1)
    $w.Write([uint16]32)
    $w.Write([uint32]0)
    $w.Write([uint32]0)
    $w.Write([int32]0)
    $w.Write([int32]0)
    $w.Write([uint32]0)
    $w.Write([uint32]0)
    for ($y = $h - 1; $y -ge 0; $y--) {
        $w.Write($pixels, $y * $l * 4, $l * 4)
    }
    $masque = New-Object byte[] ((([int][Math]::Ceiling($l / 32.0)) * 4) * $h)
    $w.Write($masque)
    $w.Flush()
    return , $flux.ToArray()
}

function EcrireIco([string]$chemin, [bool]$alerte) {
    $entrees = @()
    foreach ($t in $tailles) {
        $image = DessinerIcone $t $alerte
        $entrees += , (OctetsEntree $image)
        $image.Dispose()
    }

    $flux = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($flux)
    $w.Write([uint16]0)
    $w.Write([uint16]1)
    $w.Write([uint16]$tailles.Count)
    $decalage = 6 + 16 * $tailles.Count
    for ($i = 0; $i -lt $tailles.Count; $i++) {
        $w.Write([byte]$tailles[$i])
        $w.Write([byte]$tailles[$i])
        $w.Write([byte]0)
        $w.Write([byte]0)
        $w.Write([uint16]1)
        $w.Write([uint16]32)
        $w.Write([uint32]$entrees[$i].Length)
        $w.Write([uint32]$decalage)
        $decalage += $entrees[$i].Length
    }
    foreach ($entree in $entrees) {
        $w.Write($entree)
    }
    $w.Flush()
    [System.IO.File]::WriteAllBytes($chemin, $flux.ToArray())
}

New-Item -ItemType Directory -Force -Path $Sortie | Out-Null
$dossier = (Resolve-Path $Sortie).Path
EcrireIco (Join-Path $dossier 'icone-normale.ico') $false
EcrireIco (Join-Path $dossier 'icone-alerte.ico') $true
Write-Host "Icônes écrites dans $dossier"
```

Générer les deux icônes (la sortie est déterministe : deux exécutions donnent les mêmes octets) :

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/generer-icones.ps1
```

Résultat attendu : `src/PressePapiers/Assets/icone-normale.ico` et `icone-alerte.ico` (19 278 octets chacun) ;
empreintes SHA-256 de référence (le dessin ne dépend que du script) :

```
aa01bd9a81e66c71d693674456dc777fe88cd12e74cd2a30b80ebd5c4d5e5949  icone-normale.ico
2b2e10f55f3bd7b66870c47fbedf266516b92c60d9f050ae1ff55c8f23b31870  icone-alerte.ico
```

`src/PressePapiers/Natif/NatifIcone.cs` (nouveau fichier) :

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>
/// Déclarations Win32 de l'icône de notification (Shell_NotifyIcon), de son menu natif et du chargement de ses icônes.
/// Une même fonction peut être déclarée ailleurs : chaque brique reste autonome.
/// </summary>
internal static partial class NatifIcone
{
    // Commandes de Shell_NotifyIcon.
    internal const uint NimAdd = 0;
    internal const uint NimModify = 1;
    internal const uint NimDelete = 2;
    internal const uint NimSetVersion = 4;

    // Champs renseignés dans NOTIFYICONDATA (NIF_*).
    internal const uint NifMessage = 0x01;
    internal const uint NifIcon = 0x02;
    internal const uint NifTip = 0x04;
    internal const uint NifInfo = 0x10;
    internal const uint NifShowTip = 0x80; // en version 4, l'infobulle standard n'apparaît qu'avec ce drapeau

    internal const uint NiifInfo = 0x01;
    internal const uint VersionIcone4 = 4;

    // Messages reçus en version 4 : LOWORD(lParam) de MessageIcone.
    internal const uint NinSelect = 0x0400;
    internal const uint NinKeySelect = 0x0401;
    internal const uint WmContextMenu = 0x007B;

    // Messages reçus en version 0 (repli si NOTIFYICON_VERSION_4 est refusée).
    internal const uint WmLButtonUp = 0x0202;
    internal const uint WmRButtonUp = 0x0205;

    internal const uint WmNull = 0x0000;

    // Menu contextuel.
    internal const uint MfString = 0x0000;
    internal const uint MfSeparator = 0x0800;
    internal const uint TpmRightButton = 0x0002;
    internal const uint TpmBottomAlign = 0x0020;
    internal const uint TpmReturnCmd = 0x0100;

    // Chargement des icônes.
    internal const uint ImageIcon = 1;
    internal const uint LrLoadFromFile = 0x0010;
    internal const int SmCxSmIcon = 49;
    internal const int IdiApplication = 32512;

    /// <summary>
    /// NOTIFYICONDATAW, dans l'ordre et avec l'alignement natifs (976 octets en x64). Les textes sont des tampons fixes :
    /// la structure reste copiable tel quel vers Windows, sans désactiver le marshaling du runtime pour tout le projet.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DonneesIcone
    {
        public uint Taille;
        public nint Hwnd;
        public uint Id;
        public uint Drapeaux;
        public uint MessageRappel;
        public nint Icone;
        public fixed char Infobulle[128];
        public uint Etat;
        public uint MasqueEtat;
        public fixed char Bulle[256];
        public uint DelaiOuVersion; // union uTimeout / uVersion
        public fixed char TitreBulle[64];
        public uint DrapeauxBulle;
        public Guid GuidIcone;
        public nint IconeBulle;
    }

    /// <summary>POINT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    /// <summary>Écrit l'infobulle (127 caractères au plus, le zéro final est gardé).</summary>
    internal static unsafe void DefinirInfobulle(ref DonneesIcone donnees, string texte)
    {
        fixed (char* tampon = donnees.Infobulle)
        {
            Copier(tampon, 128, texte);
        }
    }

    /// <summary>Écrit le message de la bulle (255 caractères au plus).</summary>
    internal static unsafe void DefinirBulle(ref DonneesIcone donnees, string texte)
    {
        fixed (char* tampon = donnees.Bulle)
        {
            Copier(tampon, 256, texte);
        }
    }

    /// <summary>Écrit le titre de la bulle (63 caractères au plus).</summary>
    internal static unsafe void DefinirTitreBulle(ref DonneesIcone donnees, string texte)
    {
        fixed (char* tampon = donnees.TitreBulle)
        {
            Copier(tampon, 64, texte);
        }
    }

    // Copie un texte dans un tampon de taille fixe, tronqué pour garder le zéro final.
    private static unsafe void Copier(char* tampon, int taille, string texte)
    {
        var n = Math.Min(texte.Length, taille - 1);
        var cible = new Span<char>(tampon, taille);
        texte.AsSpan(0, n).CopyTo(cible);
        cible[n..].Clear();
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool NotifierIcone(uint commande, ref DonneesIcone donnees);

    [LibraryImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    internal static partial nint CreerMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AjouterAuMenu(nint menu, uint drapeaux, nuint identifiant, string? texte);

    [LibraryImport("user32.dll", EntryPoint = "SetMenuDefaultItem", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DefinirCommandeParDefaut(nint menu, uint element, uint parPosition);

    [LibraryImport("user32.dll", EntryPoint = "TrackPopupMenuEx", SetLastError = true)]
    internal static partial int AfficherMenu(nint menu, uint drapeaux, int x, int y, nint hwnd, nint parametres);

    [LibraryImport("user32.dll", EntryPoint = "DestroyMenu", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DetruireMenu(nint menu);

    [LibraryImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AmenerAuPremierPlan(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PosterMessage(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool LirePositionCurseur(out Point position);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint ChargerImage(nint instance, string nom, uint type, int largeur, int hauteur, uint options);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW", SetLastError = true)]
    internal static partial nint ChargerIconeSysteme(nint instance, nint identifiant);

    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DetruireIcone(nint icone);

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForSystem")]
    internal static partial uint LireDpiSysteme();

    [LibraryImport("user32.dll", EntryPoint = "GetSystemMetricsForDpi")]
    internal static partial int LireMetrique(int indice, uint dpi);
}
```

`src/PressePapiers/Systeme/IconeNotification.cs` (contenu complet, il remplace la coquille ; le `#pragma warning disable CS0067`
de la coquille est retiré) :

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Icône de la zone de notification (WinUI 3 n'en fournit pas) et son menu natif. Les messages de l'icône arrivent par
/// le relais ; le menu est un menu Win32 (Ouvrir, Paramètres, Quitter). L'icône est recréée quand l'Explorateur
/// redémarre. À créer et libérer sur le fil d'interface, comme le relais.
/// </summary>
public sealed class IconeNotification : IDisposable
{
    // Identifiant de l'icône auprès du Shell : une seule icône par processus.
    internal const uint IdIcone = 1;

    // Commandes du menu contextuel.
    private const uint CommandeOuvrir = 1;
    private const uint CommandeParametres = 2;
    private const uint CommandeQuitter = 3;

    private const string FichierNormal = "icone-normale.ico";
    private const string FichierAlerte = "icone-alerte.ico";

    private readonly RelaisWindows _relais;
    private readonly IJournal _journal;
    private readonly nint _hwnd;

    private nint _iconeNormale;
    private nint _iconeAlerte;
    private bool _iconeNormalePossedee;
    private bool _iconeAlertePossedee;

    // Dernier état demandé : il sert à recréer l'icône à l'identique.
    private bool _demandee;
    private bool _alerte;
    private string _infobulle = "Presse-papiers";

    private bool _ajoutee;
    private bool _versionQuatre;
    private bool _menuOuvert;
    private bool _libere;

    /// <summary>Crée l'icône, qui reçoit ses messages par le relais ; rien n'apparaît avant <see cref="Afficher"/>.</summary>
    /// <param name="relais">Relais dont la fenêtre reçoit les messages de l'icône.</param>
    /// <param name="journal">Journal des échecs du Shell.</param>
    public IconeNotification(RelaisWindows relais, IJournal journal)
    {
        _relais = relais;
        _journal = journal;
        _hwnd = relais.Handle;
        _relais.MessageIconeRecu += SurMessageIcone;
        _relais.BarreDesTachesRecreee += SurBarreDesTachesRecreee;
    }

    /// <summary>Clic sur l'icône ou commande « Ouvrir ».</summary>
    public event Action? OuvrirDemande;

    /// <summary>Commande « Paramètres ».</summary>
    public event Action? ParametresDemandes;

    /// <summary>Commande « Quitter ».</summary>
    public event Action? QuitterDemande;

    /// <summary>
    /// Affiche l'icône (NIM_ADD la première fois, NIM_MODIFY ensuite), en aspect d'alerte si demandé, avec cette infobulle.
    /// </summary>
    /// <param name="alerte">Vrai pour l'aspect d'alerte (le hook Win+V n'est pas actif).</param>
    /// <param name="infobulle">Texte de l'infobulle, tronqué à 127 caractères.</param>
    public void Afficher(bool alerte, string infobulle)
    {
        ObjectDisposedException.ThrowIf(_libere, this);
        _demandee = true;
        _alerte = alerte;
        _infobulle = infobulle;
        Appliquer();
    }

    /// <summary>Affiche une bulle de notification (NIF_INFO) ; fait apparaître l'icône si elle ne l'était pas encore.</summary>
    /// <param name="titre">Titre de la bulle, tronqué à 63 caractères.</param>
    /// <param name="message">Message de la bulle, tronqué à 255 caractères.</param>
    public void Notifier(string titre, string message)
    {
        ObjectDisposedException.ThrowIf(_libere, this);
        if (!_ajoutee)
        {
            Appliquer();
        }

        var donnees = Creer(NatifIcone.NifInfo);
        NatifIcone.DefinirTitreBulle(ref donnees, titre);
        NatifIcone.DefinirBulle(ref donnees, message);
        donnees.DrapeauxBulle = NatifIcone.NiifInfo;
        if (!NatifIcone.NotifierIcone(NatifIcone.NimModify, ref donnees))
        {
            _journal.Avertissement("Bulle de notification refusée par le Shell.");
        }
    }

    /// <summary>Retire l'icône de la zone de notification (NIM_DELETE) et libère les icônes chargées.</summary>
    public void Dispose()
    {
        if (_libere)
        {
            return;
        }

        _libere = true;
        _relais.MessageIconeRecu -= SurMessageIcone;
        _relais.BarreDesTachesRecreee -= SurBarreDesTachesRecreee;

        if (_ajoutee)
        {
            var donnees = Creer(0);
            NatifIcone.NotifierIcone(NatifIcone.NimDelete, ref donnees);
            _ajoutee = false;
        }

        if (_iconeNormalePossedee)
        {
            NatifIcone.DetruireIcone(_iconeNormale);
        }

        if (_iconeAlertePossedee)
        {
            NatifIcone.DetruireIcone(_iconeAlerte);
        }
    }

    // Ajoute l'icône ou met à jour celle qui existe ; si le Shell ne la connaît plus, elle est ajoutée de nouveau.
    private void Appliquer()
    {
        var donnees = Creer(NatifIcone.NifMessage | NatifIcone.NifIcon | NatifIcone.NifTip | NatifIcone.NifShowTip);
        donnees.MessageRappel = RelaisWindows.MessageIcone;
        donnees.Icone = IconePour(_alerte);
        NatifIcone.DefinirInfobulle(ref donnees, _infobulle);

        if (_ajoutee)
        {
            if (NatifIcone.NotifierIcone(NatifIcone.NimModify, ref donnees))
            {
                return;
            }

            _journal.Avertissement("Mise à jour de l'icône refusée : elle est ajoutée de nouveau.");
            _ajoutee = false;
        }

        if (!NatifIcone.NotifierIcone(NatifIcone.NimAdd, ref donnees))
        {
            _journal.Avertissement("Ajout de l'icône de notification refusé par le Shell.");
            return;
        }

        _ajoutee = true;

        // Version 4 : messages précis (NIN_SELECT, WM_CONTEXTMENU) au lieu des seuls messages de souris.
        donnees.DelaiOuVersion = NatifIcone.VersionIcone4;
        _versionQuatre = NatifIcone.NotifierIcone(NatifIcone.NimSetVersion, ref donnees);
        if (!_versionQuatre)
        {
            _journal.Avertissement("NOTIFYICON_VERSION_4 refusée : l'icône réagit aux messages de souris classiques.");
        }
    }

    private NatifIcone.DonneesIcone Creer(uint drapeaux) => new()
    {
        Taille = (uint)Unsafe.SizeOf<NatifIcone.DonneesIcone>(),
        Hwnd = _hwnd,
        Id = IdIcone,
        Drapeaux = drapeaux,
    };

    // L'Explorateur a redémarré : ses icônes ont disparu. Le retrait préalable évite un doublon si l'avis arrive
    // alors que l'ancienne icône existe encore.
    private void SurBarreDesTachesRecreee()
    {
        if (_libere || !_demandee)
        {
            return;
        }

        var donnees = Creer(0);
        NatifIcone.NotifierIcone(NatifIcone.NimDelete, ref donnees);
        _ajoutee = false;
        Appliquer();
        _journal.Info("Icône de notification recréée après le redémarrage de l'Explorateur.");
    }

    private void SurMessageIcone(uint evenement)
    {
        if (_libere)
        {
            return;
        }

        // En version 4 : NIN_SELECT et WM_CONTEXTMENU ; en repli (version 0) : relâchement du bouton gauche ou droit.
        var selection = _versionQuatre
            ? evenement is NatifIcone.NinSelect or NatifIcone.NinKeySelect
            : evenement == NatifIcone.WmLButtonUp;
        var menu = _versionQuatre
            ? evenement == NatifIcone.WmContextMenu
            : evenement == NatifIcone.WmRButtonUp;

        if (selection)
        {
            OuvrirDemande?.Invoke();
        }
        else if (menu)
        {
            AfficherMenu();
        }
    }

    // Menu natif : SetForegroundWindow avant, pour qu'il se ferme au clic ailleurs ; WM_NULL après, pour que la
    // boucle de messages ne reste pas endormie (recette décrite par Microsoft pour TrackPopupMenu). Chaque commande
    // porte un accélérateur (O, P, Q) pour le clavier ; « Ouvrir » est la commande par défaut, en gras.
    private void AfficherMenu()
    {
        if (_menuOuvert)
        {
            return;
        }

        var menu = NatifIcone.CreerMenu();
        if (menu == 0)
        {
            _journal.Avertissement($"Création du menu de l'icône impossible (code {Marshal.GetLastPInvokeError()}).");
            return;
        }

        var commande = 0;
        _menuOuvert = true;
        try
        {
            NatifIcone.AjouterAuMenu(menu, NatifIcone.MfString, CommandeOuvrir, "&Ouvrir");
            NatifIcone.AjouterAuMenu(menu, NatifIcone.MfString, CommandeParametres, "&Paramètres");
            NatifIcone.AjouterAuMenu(menu, NatifIcone.MfSeparator, 0, null);
            NatifIcone.AjouterAuMenu(menu, NatifIcone.MfString, CommandeQuitter, "&Quitter");
            NatifIcone.DefinirCommandeParDefaut(menu, CommandeOuvrir, 0);

            NatifIcone.LirePositionCurseur(out var position);
            NatifIcone.AmenerAuPremierPlan(_hwnd);
            commande = NatifIcone.AfficherMenu(
                menu, NatifIcone.TpmRightButton | NatifIcone.TpmBottomAlign | NatifIcone.TpmReturnCmd,
                position.X, position.Y, _hwnd, 0);
            NatifIcone.PosterMessage(_hwnd, NatifIcone.WmNull, 0, 0);
        }
        finally
        {
            _menuOuvert = false;
            NatifIcone.DetruireMenu(menu);
        }

        // Les événements sont levés une fois le menu refermé, hors de sa boucle de messages.
        switch ((uint)commande)
        {
            case CommandeOuvrir:
                OuvrirDemande?.Invoke();
                break;
            case CommandeParametres:
                ParametresDemandes?.Invoke();
                break;
            case CommandeQuitter:
                QuitterDemande?.Invoke();
                break;
        }
    }

    private nint IconePour(bool alerte)
    {
        if (alerte)
        {
            if (_iconeAlerte == 0)
            {
                (_iconeAlerte, _iconeAlertePossedee) = Charger(FichierAlerte);
            }

            return _iconeAlerte;
        }

        if (_iconeNormale == 0)
        {
            (_iconeNormale, _iconeNormalePossedee) = Charger(FichierNormal);
        }

        return _iconeNormale;
    }

    // Charge la taille d'icône que la zone de notification affiche à la résolution du système ; à défaut de fichier,
    // l'icône d'application de Windows (partagée, donc non détruite) évite une icône invisible.
    private (nint Icone, bool Possedee) Charger(string fichier)
    {
        var chemin = Path.Combine(AppContext.BaseDirectory, "Assets", fichier);
        var taille = NatifIcone.LireMetrique(NatifIcone.SmCxSmIcon, NatifIcone.LireDpiSysteme());
        var icone = File.Exists(chemin)
            ? NatifIcone.ChargerImage(0, chemin, NatifIcone.ImageIcon, taille, taille, NatifIcone.LrLoadFromFile)
            : 0;
        if (icone != 0)
        {
            return (icone, true);
        }

        _journal.Avertissement($"Icône {chemin} introuvable ou illisible : icône générique utilisée.");
        return (NatifIcone.ChargerIconeSysteme(0, NatifIcone.IdiApplication), false);
    }
}
```

`src/PressePapiers/PressePapiers.csproj` (contenu complet après modification : le premier `ItemGroup` est nouveau ;
`AllowUnsafeBlocks` vient de la tâche 16) :

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>PressePapiers</RootNamespace>
    <AssemblyName>PressePapiers</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>true</EnableMsixTooling>
    <!-- Application non empaquetée et autonome : un dossier à copier, sans installation ni droits administrateur. -->
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
    <!-- Main personnalisé (Program.cs) : instance unique avant toute création de fenêtre. -->
    <DefineConstants>$(DefineConstants);DISABLE_XAML_GENERATED_MAIN</DefineConstants>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <!-- LibraryImport génère des appels natifs qui exigent le code non sûr. -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <!-- Icônes de la zone de notification, chargées au démarrage depuis le dossier Assets de la sortie. -->
    <Content Include="Assets\*.ico" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Serilog" />
    <PackageReference Include="Serilog.Sinks.File" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PressePapiers.Coeur\PressePapiers.Coeur.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)` et `0 Erreur(s)`, et
`src/PressePapiers/bin/Debug/net10.0-windows10.0.19041.0/win-x64/Assets/` contient les deux `.ico`. Tous les tests déjà
présents restent verts ; cette tâche n'en ajoute ni n'en retire (les 513 des tâches 1 à 16) :

```
Résumé de série de tests : Réussite!
  total : 513
  échec : 0
  réussie : 513
  ignoré : 0
```

- [ ] **Étape 5 : Vérifier sur le poste**

1. Essai automatique (relais puis icône) :

```bash
DEPOT="$(git rev-parse --show-toplevel)"; ESSAIS="$LOCALAPPDATA/Temp/essais-relais"
bash "$ESSAIS/synchro.sh" "$DEPOT" && cd "$ESSAIS" && dotnet build -c Debug && dotnet run -c Debug --no-build
```

Résultat attendu (47 lignes `OK`, aucun `ECHEC` ; le nombre de signaux après `Set-Clipboard` varie, 2 ou 3 ; les lignes `journal`
sont voulues) :

```
== Relais Windows ==
OK     fenêtre créée
OK     fenêtre de premier niveau (ni enfant ni message-only)
OK     fenêtre jamais affichée
OK     EcouterPressePapiers
OK     EcouterPressePapiers répété
OK     CopieDetectee levé après Set-Clipboard : 2 signal(aux)
   injection de frappes : active
OK     EnregistrerRaccourci Ctrl+Alt+Maj+V : code 0
OK     RaccourciSecoursPresse levé par SendInput : 1 signal(aux)
OK     second relais : Ctrl+Alt+Maj+W enregistré
   journal [avert] Raccourci de secours Ctrl+Alt+Maj+W refusé (code 1409).
OK     conflit : refus avec le code 1409 : code 1409
OK     après le conflit, l'ancien raccourci fonctionne encore
OK     second relais libéré
OK     remplacement par Ctrl+Alt+Maj+B
OK     l'ancien (V) ne lève plus rien
OK     le nouveau (B) lève l'événement
OK     après RetirerRaccourci, plus d'événement
OK     ré-enregistrement de V pour la suite
OK     BarreDesTachesRecreee levé par TaskbarCreated
OK     verrouillage (7) : pas d'événement de déverrouillage
OK     SessionDeverrouillee levé par WTS_SESSION_UNLOCK
OK     suspension (PBT_APMSUSPEND) : pas d'événement de veille
OK     SortieDeVeille levé par PBT_APMRESUMEAUTOMATIC
OK     MessageIconeRecu : LOWORD(lParam) : 0x400
   journal [erreur] Exception dans un abonné du relais (message 0x0218). : abonné défaillant
OK     exception d'abonné journalisée, processus intact
OK     Dispose : fenêtre détruite
OK     Dispose : Handle remis à zéro
OK     Dispose : plus de copie détectée
OK     Dispose : raccourci libéré
OK     Dispose : combinaison de nouveau disponible
OK     aucun avertissement ni erreur inattendu au journal : 2 (attendu 2 : le conflit 1409 et l'abonné défaillant)
== Icône de notification ==
OK     NOTIFYICONDATAW fait 976 octets (x64) : 976
OK     aucune icône avant Afficher
OK     NIM_ADD : l'icône existe pour le Shell
OK     NIM_MODIFY : l'icône existe toujours
OK     Notifier : aucune erreur du Shell : 0 problème(s)
OK     clic gauche (NIN_SELECT) : OuvrirDemande
OK     clic droit : menu natif affiché (#32768)
OK     menu : accélérateur O (Ouvrir) : OuvrirDemande : 2 signal(aux)
OK     menu : accélérateur P (Paramètres) : ParametresDemandes
OK     menu : accélérateur Q (Quitter) : QuitterDemande
OK     menu : Échap : fermé sans commande
OK     icône retirée de force (simulation du redémarrage de l'Explorateur)
   journal [info] Icône de notification recréée après le redémarrage de l'Explorateur.
OK     TaskbarCreated : l'icône est recréée
OK     recréation sans avertissement
OK     NIM_MODIFY fonctionne sur l'icône recréée
OK     Dispose : NIM_DELETE, plus d'icône
OK     aucun avertissement ni erreur de l'icône au journal : 0
TOUS LES ESSAIS REUSSIS
```

Si des lignes `NON TESTE` apparaissent, voir la remarque sur le niveau d'intégrité de la fenêtre au premier plan à
l'étape 5 de la tâche 17 : refaire l'essai avec une fenêtre ordinaire au premier plan.

2. Lisibilité des icônes. Générer l'aperçu et l'ouvrir :

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File "$ESSAIS/apercu.ps1" -Dossier "$DEPOT/src/PressePapiers/Assets" -Png "$ESSAIS/apercu.png"
powershell -NoProfile -Command "Start-Process '$ESSAIS/apercu.png'"
```

À observer : quatre bandes (icône normale sur fond sombre puis clair, icône d'alerte sur fond sombre puis clair), chacune
avec les cinq tailles 16, 20, 24, 32 et 48 pixels, en grand et, en bas à gauche de chaque grand dessin, à la taille réelle.
Le presse-papiers (planche bleue, feuille claire, pince) reste reconnaissable à 16 pixels sur les deux fonds ; l'aspect d'alerte
se distingue par la pastille rouge à point d'exclamation.

3. Icône et bulle réelles dans la zone de notification :

```bash
(dotnet run -c Debug --no-build -- voir 60 &) ; sleep 6
powershell -NoProfile -ExecutionPolicy Bypass -File "$ESSAIS/capture.ps1" -Png "$ESSAIS/ecran.png"
powershell -NoProfile -Command "Start-Process '$ESSAIS/ecran.png'"
```

À observer : la console affiche `ICONE AFFICHEE` ; la capture montre la bulle « PressePapiers : Bulle de notification d'essai ».
L'icône d'alerte est dans le menu de débordement (chevron « ^ » de la zone de notification) : l'ouvrir pour la voir, passer la
souris dessus (infobulle « PressePapiers : aspect d'alerte »), cliquer pour lire `EVENEMENT OuvrirDemande` dans la console,
faire un clic droit pour voir le menu Ouvrir, Paramètres, Quitter, et choisir une commande pour lire l'événement
correspondant. Le programme se termine seul après 60 secondes et retire l'icône ; si on l'interrompt (Ctrl+C), une icône
fantôme reste jusqu'au survol de la souris. Remettre un texte neutre dans le presse-papiers si besoin (le mode `voir` n'y
touche pas).

Ce qui n'a pas pu être vérifié par script : les vrais clics de souris sur l'icône (elle est dans le menu de débordement,
et Windows avale les clics injectés quand une fenêtre de niveau d'intégrité supérieur est au premier plan) ; le test
automatique poste donc les messages d'icône que le Shell enverrait en version 4. Le point 3 ci-dessus le complète à la main.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Natif/NatifIcone.cs src/PressePapiers/Systeme/IconeNotification.cs src/PressePapiers/PressePapiers.csproj scripts/generer-icones.ps1 src/PressePapiers/Assets/icone-normale.ico src/PressePapiers/Assets/icone-alerte.ico
git commit -m "Implémenter IconeNotification : icône de la zone de notification, menu natif et icônes générées"
```
