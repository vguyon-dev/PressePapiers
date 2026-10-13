<#
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
