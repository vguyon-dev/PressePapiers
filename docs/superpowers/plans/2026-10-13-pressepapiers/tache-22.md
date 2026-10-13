### Tâche 22 : EcrivainPressePapiers

**Fichiers :**
- Modifier : `src/PressePapiers/Capture/EcrivainPressePapiers.cs` (remplace la coquille ; contenu complet à l'étape 3)
- Modifier : `src/PressePapiers/Natif/NatifPressePapiers.cs` (créé par la tâche 21 ; ajoute `GmemMoveable`, `EmptyClipboard`, `SetClipboardData`, `GlobalAlloc`, `GlobalFree` ; le plan donne le contenu complet après modification)
- Tests : aucun test xUnit (code natif). Vérification sur le poste par le mode `ecriture` du programme jetable créé à la tâche 21
  (`essais-pp/verif`, hors dépôt), qui relit le presse-papiers avec des lecteurs indépendants (PowerShell, Windows Forms).

**Interfaces :**
- Utilise :
  - tâche 1 : `Element` (dont `Fichiers`), `TypeElement`, `ModeCollage`, `IJournal` ;
  - tâche 14 : `ListeFichiers.EncoderDropFiles(IReadOnlyList<string>)` ;
  - tâche 15 : `ConvertisseurImage.PngVersDibAsync(byte[])` ;
  - tâche 21 : `MarqueInterne.Format`, `FormatsPressePapiers.Rtf/Html/Png`, `LectureBrute.EssaisMax` et `PauseEntreEssais`,
    `NatifPressePapiers` (constantes de formats, `OpenClipboard`, `CloseClipboard`, `GlobalLock`, `GlobalUnlock`).
- Fournit :
  - `public sealed class EcrivainPressePapiers` : `EcrivainPressePapiers(nint hwndRelais, IJournal journal)` et
    `Task<bool> EcrireAsync(Element element, ModeCollage mode, string? cheminImage)` ; vrai si le contenu principal est dans le
    presse-papiers, faux (noté au journal) sinon, **jamais d'exception** ; la marque interne est toujours posée ;
  - `internal readonly record struct FormatAEcrire(uint Format, byte[] Octets)` et
    `internal Task<List<FormatAEcrire>?> PreparerAsync(Element, ModeCollage, string?)` (construction pure des formats, avant toute ouverture du presse-papiers) ;
  - `NatifPressePapiers` : `const uint GmemMoveable = 0x0002`, `EmptyClipboard()`, `SetClipboardData(uint, nint)`,
    `GlobalAlloc(uint, nuint)`, `GlobalFree(nint)`.
  - Consommateur : tâche 24 (`ServiceRecollage.CollerAsync` appelle `EcrireAsync`), tâche 29.

Règles appliquées (spec, sections 4 et 10.2) :
- **Quoi écrire** : `Texte` donne `CF_UNICODETEXT` ; `MisEnForme` donne `CF_UNICODETEXT`, plus le RTF et le HTML bruts de l'élément
  (chacun suivi d'un octet nul, seulement s'il existe) quand le mode est `AvecMiseEnForme`, le texte seul en mode `TexteSeul` ;
  `Image` donne `CF_DIB` (PNG converti par `PngVersDibAsync`) et le format enregistré « PNG » (octets du fichier, tels quels),
  le mode est sans effet ; `Fichiers` donne `CF_HDROP` seul. La **marque interne** est le dernier format posé, dans tous les cas.
- **Cas d'échec sans exception** : texte absent ou vide, PNG introuvable sur le disque (point de vigilance 4), aucun chemin de
  fichier, type inconnu : avertissement au journal, résultat faux, et le presse-papiers n'est pas touché (rien n'est vidé).
- **Ouverture** : `OpenClipboard` sur la fenêtre relais, 10 essais espacés de 25 ms par `await Task.Delay` (le fil d'interface n'est
  jamais bloqué), puis `EmptyClipboard` (la fenêtre relais devient propriétaire). Les formats sont rendus tout de suite (pas de
  rendu différé) et le presse-papiers est refermé dans un `finally`.
- **Mémoire** : `GlobalAlloc(GMEM_MOVEABLE)`, remplie sous `GlobalLock`, confiée à `SetClipboardData` ; si l'appel échoue, la
  mémoire est libérée par l'appelant. L'échec d'un format secondaire (RTF, HTML, PNG, marque) est noté mais n'annule pas la copie ;
  l'échec du format principal rend faux.
- **Fil** : `EcrireAsync` s'appelle sur le fil d'interface (la fenêtre relais y vit) ; après un `await`, la suite revient sur ce fil
  grâce au contexte de synchronisation installé par `Program` (tâche 16).

- [ ] **Étape 1 : La vérification est déjà écrite**

Le mode `ecriture` du programme `essais-pp/verif` (tâche 21, étape 1) vérifie l'écrivain : modes de collage, formats relus un à
un, relecture par PowerShell 7 (`Get-Clipboard`), Windows Forms (`Clipboard.GetImage`) et Windows PowerShell 5.1
(`Get-Clipboard -Format FileDropList`), marque interne ignorée par le lecteur, PNG manquant. Rien à écrire.

- [ ] **Étape 2 : Lancer la vérification et constater l'échec**

```bash
cd essais-pp/verif
dotnet run -c Debug -p:Depot=<chemin absolu du dépôt> -- ecriture
```

Résultat attendu avec la coquille : `OK    fenêtre relais créée`, puis `Unhandled exception. System.NotImplementedException: Tâche 22`
(levée par le constructeur de `EcrivainPressePapiers`).

- [ ] **Étape 3 : Implémenter**

Compléments à `NatifPressePapiers` (fichier complet après modification) :

`src/PressePapiers/Natif/NatifPressePapiers.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>
/// Appels Windows de la lecture du presse-papiers et de la recherche de l'application d'origine
/// (user32 et kernel32). Chaque brique du projet déclare ses propres appels : une même fonction peut figurer ailleurs.
/// </summary>
internal static partial class NatifPressePapiers
{
    /// <summary>Texte Unicode (CF_UNICODETEXT).</summary>
    public const uint CfUnicodeText = 13;

    /// <summary>Image DIB (CF_DIB).</summary>
    public const uint CfDib = 8;

    /// <summary>Image DIB version 5 (CF_DIBV5), seule à porter un canal alpha.</summary>
    public const uint CfDibV5 = 17;

    /// <summary>Liste de fichiers (CF_HDROP).</summary>
    public const uint CfHDrop = 15;

    /// <summary>Mémoire déplaçable : la seule acceptée par SetClipboardData.</summary>
    public const uint GmemMoveable = 0x0002;

    /// <summary>Ouvre le presse-papiers ; <paramref name="hwndProprietaire"/> à 0 l'associe à la tâche courante.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenClipboard(nint hwndProprietaire);

    /// <summary>Referme le presse-papiers.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseClipboard();

    /// <summary>Vide le presse-papiers et en donne la propriété à la fenêtre qui l'a ouvert (il doit en avoir une).</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EmptyClipboard();

    /// <summary>
    /// Pose un format. Si l'appel réussit, la mémoire appartient désormais au système ; sinon l'appelant la libère.
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint SetClipboardData(uint format, nint memoire);

    /// <summary>Vrai si un format est présent (le presse-papiers n'a pas besoin d'être ouvert).</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsClipboardFormatAvailable(uint format);

    /// <summary>Poignée mémoire d'un format ; le presse-papiers doit être ouvert, la mémoire reste à lui.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint GetClipboardData(uint format);

    /// <summary>Enregistre (ou retrouve) un format nommé ; 0 en cas d'échec.</summary>
    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial uint RegisterClipboardFormat(string nom);

    /// <summary>Fenêtre actuellement au premier plan.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    /// <summary>Fenêtre propriétaire du contenu du presse-papiers (repli pour l'application d'origine).</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetClipboardOwner();

    /// <summary>Fil et processus d'une fenêtre ; renvoie l'identifiant du fil.</summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hwnd, out uint idProcessus);

    /// <summary>Énumère les fenêtres filles directes : passer 0 pour <paramref name="apres"/> la première fois.</summary>
    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindowEx(nint parent, nint apres, string? classe, string? titre);

    /// <summary>Alloue un bloc de mémoire globale ; 0 en cas d'échec.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalAlloc(uint drapeaux, nuint taille);

    /// <summary>Libère un bloc de mémoire globale ; renvoie 0 en cas de réussite.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalFree(nint memoire);

    /// <summary>Verrouille un bloc mémoire globale et renvoie son adresse.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalLock(nint memoire);

    /// <summary>Déverrouille un bloc mémoire globale.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint memoire);

    /// <summary>Taille en octets d'un bloc mémoire globale (peut dépasser la taille demandée).</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nuint GlobalSize(nint memoire);
}
```

Écrivain :

`src/PressePapiers/Capture/EcrivainPressePapiers.cs`

```csharp
using System.Runtime.InteropServices;
using System.Text;
using PressePapiers.Coeur.Formats;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Natif;

namespace PressePapiers.Capture;

/// <summary>Écriture d'un élément dans le presse-papiers, toujours marquée comme venant de l'outil.</summary>
public sealed class EcrivainPressePapiers
{
    private readonly nint _hwndRelais;
    private readonly IJournal _journal;

    /// <summary>Crée l'écrivain ; <paramref name="hwndRelais"/> est la fenêtre qui ouvre le presse-papiers et en devient propriétaire.</summary>
    /// <param name="hwndRelais">Fenêtre relais, créée sur le fil d'interface.</param>
    /// <param name="journal">Journal des écritures en échec.</param>
    public EcrivainPressePapiers(nint hwndRelais, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        _hwndRelais = hwndRelais;
        _journal = journal;
    }

    /// <summary>
    /// Écrit l'élément dans le mode demandé, en posant toujours la marque interne : texte seul, ou texte avec RTF et HTML
    /// pour un élément mis en forme en mode <see cref="ModeCollage.AvecMiseEnForme"/> ; CF_DIB et « PNG » pour une image ;
    /// CF_HDROP pour des fichiers. À appeler sur le fil d'interface.
    /// </summary>
    /// <param name="element">Élément à remettre dans le presse-papiers.</param>
    /// <param name="mode">Texte seul ou avec mise en forme (sans effet sur les images et les fichiers).</param>
    /// <param name="cheminImage">Chemin du PNG de l'élément (obligatoire pour une image).</param>
    /// <returns>Vrai si le contenu principal est dans le presse-papiers ; faux (noté au journal) sinon, jamais d'exception.</returns>
    public async Task<bool> EcrireAsync(Element element, ModeCollage mode, string? cheminImage)
    {
        ArgumentNullException.ThrowIfNull(element);
        try
        {
            var formats = await PreparerAsync(element, mode, cheminImage);
            if (formats is null)
            {
                return false;
            }

            return await OuvrirAsync() && EcrireOuvert(formats);
        }
        catch (Exception exception)
        {
            _journal.Erreur($"Écriture de l'élément {element.Id} dans le presse-papiers impossible.", exception);
            return false;
        }
    }

    /// <summary>Un format à poser : identifiant et octets (le premier de la liste est le contenu principal).</summary>
    internal readonly record struct FormatAEcrire(uint Format, byte[] Octets);

    /// <summary>
    /// Construit les formats à poser, contenu principal d'abord, marque interne en dernier ; null (noté au journal) si
    /// l'élément n'a pas ce qu'il faut (texte absent, PNG manquant sur disque, aucun chemin de fichier).
    /// </summary>
    internal async Task<List<FormatAEcrire>?> PreparerAsync(Element element, ModeCollage mode, string? cheminImage)
    {
        var formats = new List<FormatAEcrire>();
        switch (element.Type)
        {
            case TypeElement.Texte or TypeElement.MisEnForme:
                if (string.IsNullOrEmpty(element.Texte))
                {
                    _journal.Avertissement($"Élément {element.Id} sans texte : rien à écrire.");
                    return null;
                }

                formats.Add(new FormatAEcrire(NatifPressePapiers.CfUnicodeText, Encoding.Unicode.GetBytes(element.Texte + "\0")));
                if (element.Type == TypeElement.MisEnForme && mode == ModeCollage.AvecMiseEnForme)
                {
                    AjouterTexteTermine(formats, FormatsPressePapiers.Rtf, element.Rtf);
                    AjouterTexteTermine(formats, FormatsPressePapiers.Html, element.Html);
                }

                break;

            case TypeElement.Image:
                if (string.IsNullOrEmpty(cheminImage) || !File.Exists(cheminImage))
                {
                    _journal.Avertissement($"Image de l'élément {element.Id} introuvable sur le disque : rien à écrire.");
                    return null;
                }

                var png = await File.ReadAllBytesAsync(cheminImage);
                formats.Add(new FormatAEcrire(NatifPressePapiers.CfDib, await ConvertisseurImage.PngVersDibAsync(png)));
                formats.Add(new FormatAEcrire(FormatsPressePapiers.Png, png));
                break;

            case TypeElement.Fichiers:
                if (element.Fichiers.Count == 0)
                {
                    _journal.Avertissement($"Élément {element.Id} sans chemin de fichier : rien à écrire.");
                    return null;
                }

                formats.Add(new FormatAEcrire(NatifPressePapiers.CfHDrop, ListeFichiers.EncoderDropFiles(element.Fichiers)));
                break;

            default:
                _journal.Avertissement($"Type d'élément inconnu ({element.Type}) : rien à écrire.");
                return null;
        }

        formats.Add(new FormatAEcrire(MarqueInterne.Format, [1]));
        return formats;
    }

    // RTF et HTML sont posés avec un nul final, comme les écrivent les autres applications.
    private static void AjouterTexteTermine(List<FormatAEcrire> formats, uint format, byte[]? octets)
    {
        if (octets is { Length: > 0 })
        {
            formats.Add(new FormatAEcrire(format, [.. octets, 0]));
        }
    }

    // OpenClipboard échoue tant qu'une autre application le tient : 10 essais espacés de 25 ms, sans bloquer le fil d'interface.
    private async Task<bool> OuvrirAsync()
    {
        var erreur = 0;
        for (var essai = 1; essai <= LectureBrute.EssaisMax; essai++)
        {
            if (NatifPressePapiers.OpenClipboard(_hwndRelais))
            {
                return true;
            }

            erreur = Marshal.GetLastPInvokeError();
            if (essai < LectureBrute.EssaisMax)
            {
                await Task.Delay(LectureBrute.PauseEntreEssais);
            }
        }

        _journal.Avertissement($"Écriture impossible : le presse-papiers est resté occupé après {LectureBrute.EssaisMax} essais (erreur Windows {erreur}).");
        return false;
    }

    private bool EcrireOuvert(List<FormatAEcrire> formats)
    {
        try
        {
            if (!NatifPressePapiers.EmptyClipboard())
            {
                _journal.Avertissement($"Écriture impossible : le presse-papiers n'a pas pu être vidé (erreur Windows {Marshal.GetLastPInvokeError()}).");
                return false;
            }

            var principal = Poser(formats[0]);
            for (var i = 1; i < formats.Count; i++)
            {
                Poser(formats[i]);   // un format secondaire (RTF, PNG, marque) qui échoue est noté mais n'annule pas la copie
            }

            return principal;
        }
        finally
        {
            NatifPressePapiers.CloseClipboard();
        }
    }

    // Mémoire déplaçable remplie puis confiée au système : en cas d'échec de SetClipboardData, c'est à nous de la libérer.
    private bool Poser(FormatAEcrire format)
    {
        var memoire = NatifPressePapiers.GlobalAlloc(NatifPressePapiers.GmemMoveable, (nuint)format.Octets.Length);
        if (memoire == 0)
        {
            _journal.Avertissement($"Mémoire insuffisante pour le format {format.Format} ({format.Octets.Length} octets).");
            return false;
        }

        var adresse = NatifPressePapiers.GlobalLock(memoire);
        if (adresse == 0)
        {
            NatifPressePapiers.GlobalFree(memoire);
            _journal.Avertissement($"Verrouillage mémoire impossible pour le format {format.Format}.");
            return false;
        }

        try
        {
            Marshal.Copy(format.Octets, 0, adresse, format.Octets.Length);
        }
        finally
        {
            NatifPressePapiers.GlobalUnlock(memoire);
        }

        if (NatifPressePapiers.SetClipboardData(format.Format, memoire) != 0)
        {
            return true;
        }

        var erreur = Marshal.GetLastPInvokeError();
        NatifPressePapiers.GlobalFree(memoire);
        _journal.Avertissement($"SetClipboardData a échoué pour le format {format.Format} (erreur Windows {erreur}).");
        return false;
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 513
  échec : 0
  réussie : 513
  ignoré : 0
```

(aucun test ajouté par cette tâche.)

- [ ] **Étape 5 : Vérifier sur le poste**

```bash
cd essais-pp/verif
dotnet run -c Debug -p:Depot=<chemin absolu du dépôt> -- ecriture
dotnet run -c Debug -p:Depot=<chemin absolu du dépôt> -- tout
```

À observer : `17/17 vérifications réussies` pour le mode `ecriture`, puis `47/47` pour `tout` (lecture et écriture ensemble), aucun
`ECHEC`. Détail à retrouver dans la sortie : en mode `AvecMiseEnForme`, les formats du presse-papiers sont
`13, Rich Text Format, HTML Format, PressePapiers.Interne` (plus les formats synthétisés par Windows) ; en mode `TexteSeul`,
`13, PressePapiers.Interne` ; pour une image, `8, PNG, PressePapiers.Interne` ; pour des fichiers, `15, PressePapiers.Interne`.
Le texte, l'image (64 x 48) et la liste de fichiers sont relus à l'identique par les trois programmes tiers, et le lecteur
de la tâche 21 ignore chaque écriture (marque interne). Le programme remet un texte neutre dans le presse-papiers en finissant.
Pendant l'essai ne rien copier à la main : une copie concurrente peut faire échouer un contrôle (relancer).

À noter pour la recette manuelle (spec, section 14) : la prise en charge du format « PNG » par Word, Excel, PowerPoint, Outlook, un
navigateur et Paint, et la transparence qui l'accompagne, restent à constater dans ces applications ; ce programme ne prouve que
l'écriture correcte des formats.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Capture/EcrivainPressePapiers.cs src/PressePapiers/Natif/NatifPressePapiers.cs
git commit -m "Ajouter l'écriture dans le presse-papiers" -m "Texte, RTF et HTML selon le mode, CF_DIB et PNG pour une image lue depuis le fichier, CF_HDROP pour des fichiers ; marque interne toujours posée, mémoire GMEM_MOVEABLE, réessais d'ouverture sans bloquer le fil d'interface, jamais d'exception (PNG manquant, texte absent)."
```
