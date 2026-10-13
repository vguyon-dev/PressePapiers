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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="hwndRelais"/> est nul : EmptyClipboard échouerait à chaque écriture, faute de fenêtre propriétaire.
    /// </exception>
    public EcrivainPressePapiers(nint hwndRelais, IJournal journal)
    {
        ArgumentOutOfRangeException.ThrowIfZero(hwndRelais);
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

            // Le presse-papiers est déjà vidé : si le contenu principal ne passe pas, l'ancien contenu de l'utilisateur est
            // perdu et rien ne le remplace. Les formats secondaires seuls n'auraient pas de sens : on s'arrête là.
            if (!Poser(formats[0]))
            {
                _journal.Erreur("Le presse-papiers a été vidé mais le contenu principal n'a pas pu y être posé : il reste vide.");
                return false;
            }

            for (var i = 1; i < formats.Count; i++)
            {
                Poser(formats[i]);   // un format secondaire (RTF, PNG, marque) qui échoue est noté mais n'annule pas la copie
            }

            return true;
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
