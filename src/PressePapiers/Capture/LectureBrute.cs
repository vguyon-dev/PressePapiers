using System.Runtime.InteropServices;
using System.Text;
using PressePapiers.Coeur.Formats;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Natif;

namespace PressePapiers.Capture;

/// <summary>
/// Lecture synchrone du presse-papiers : ouvre avec réessais, copie les octets des formats retenus puis referme aussitôt.
/// Elle peut bloquer longtemps si le propriétaire d'un format en rendu différé ne répond pas : l'appelant la lance sur un
/// fil dédié et abandonne l'attente au bout de deux secondes.
/// </summary>
internal static class LectureBrute
{
    /// <summary>Nombre d'essais d'ouverture quand une autre application tient le presse-papiers.</summary>
    public const int EssaisMax = 10;

    /// <summary>Pause entre deux essais d'ouverture.</summary>
    public static readonly TimeSpan PauseEntreEssais = TimeSpan.FromMilliseconds(25);

    /// <summary>Taille maximale d'un texte (en octets UTF-8), d'un RTF ou d'un HTML : au-delà, la copie est ignorée.</summary>
    public const long TailleMaxTexte = 10L * 1024 * 1024;

    /// <summary>Taille maximale d'un DIB (une image 8K en 32 bits fait environ 133 Mo).</summary>
    public const long TailleMaxImage = 256L * 1024 * 1024;

    /// <summary>
    /// Lit le contenu selon l'ordre de la spec (section 4) : CF_HDROP donne Fichiers ; sinon un texte donne MisEnForme
    /// si un RTF ou un HTML l'accompagne, Texte sinon ; sinon CF_DIBV5 puis CF_DIB donnent Image.
    /// </summary>
    /// <returns>Le contenu, ou null (marque interne présente, presse-papiers occupé, vide, illisible ou trop gros).</returns>
    public static ContenuBrut? Lire(IJournal journal)
    {
        if (!Ouvrir(journal))
        {
            return null;
        }

        try
        {
            return LireOuvert(journal);
        }
        finally
        {
            NatifPressePapiers.CloseClipboard();
        }
    }

    // OpenClipboard(NULL) : l'outil ne lie pas la lecture à sa fenêtre relais, qui vit sur un autre fil.
    private static bool Ouvrir(IJournal journal)
    {
        var erreur = 0;
        for (var essai = 1; essai <= EssaisMax; essai++)
        {
            if (NatifPressePapiers.OpenClipboard(0))
            {
                return true;
            }

            erreur = Marshal.GetLastPInvokeError();
            if (essai < EssaisMax)
            {
                Thread.Sleep(PauseEntreEssais);
            }
        }

        journal.Avertissement($"Copie ignorée : le presse-papiers est resté occupé après {EssaisMax} essais (erreur Windows {erreur}).");
        return false;
    }

    private static ContenuBrut? LireOuvert(IJournal journal)
    {
        if (MarquePresente(journal))
        {
            return null;
        }

        if (NatifPressePapiers.IsClipboardFormatAvailable(NatifPressePapiers.CfHDrop))
        {
            return LireFichiers(journal);
        }

        if (NatifPressePapiers.IsClipboardFormatAvailable(NatifPressePapiers.CfUnicodeText))
        {
            return LireTexte(journal);
        }

        return LireImage(journal);
    }

    // Si le format de la marque ne peut pas être enregistré, on lit quand même : mieux vaut un doublon qu'une capture muette.
    private static bool MarquePresente(IJournal journal)
    {
        try
        {
            return NatifPressePapiers.IsClipboardFormatAvailable(MarqueInterne.Format);
        }
        catch (InvalidOperationException exception)
        {
            journal.Avertissement("Marque interne indisponible : les écritures de l'outil ne seront pas reconnues.", exception);
            return false;
        }
    }

    private static ContenuBrut? LireFichiers(IJournal journal)
    {
        var octets = Copier(NatifPressePapiers.GetClipboardData(NatifPressePapiers.CfHDrop), TailleMaxTexte,
            "liste de fichiers", journal);
        if (octets is null)
        {
            return null;
        }

        var chemins = ListeFichiers.DecoderDropFiles(octets);
        if (chemins.Count == 0)
        {
            journal.Avertissement("Copie ignorée : la liste de fichiers est vide ou illisible.");
            return null;
        }

        return new ContenuBrut(TypeElement.Fichiers, Fichiers: chemins);
    }

    private static ContenuBrut? LireTexte(IJournal journal)
    {
        // Le bloc est en UTF-16 : un texte de plus de TailleMaxTexte caractères dépasse forcément la limite en UTF-8,
        // inutile de le copier (le petit surplus tient compte de l'arrondi de GlobalSize).
        var octets = Copier(NatifPressePapiers.GetClipboardData(NatifPressePapiers.CfUnicodeText),
            2 * TailleMaxTexte + 128, "texte", journal, TailleMaxTexte);
        if (octets is null)
        {
            return null;
        }

        var texte = DecoderUtf16(octets);
        var tailleUtf8 = Encoding.UTF8.GetByteCount(texte);
        if (tailleUtf8 > TailleMaxTexte)
        {
            journal.Avertissement($"Copie ignorée : texte de {tailleUtf8 / (1024 * 1024)} Mo, au-delà de {TailleMaxTexte / (1024 * 1024)} Mo.");
            return null;
        }

        if (texte.Length == 0)
        {
            return null;
        }

        var rtf = LireFormatTexte(() => FormatsPressePapiers.Rtf, "RTF", journal);
        var html = LireFormatTexte(() => FormatsPressePapiers.Html, "HTML", journal);
        var type = rtf is not null || html is not null ? TypeElement.MisEnForme : TypeElement.Texte;
        return new ContenuBrut(type, Texte: texte, Rtf: rtf, Html: html);
    }

    // RTF et HTML sont des textes terminés par un nul dans un bloc que GlobalSize peut avoir arrondi : on coupe au premier nul.
    private static byte[]? LireFormatTexte(Func<uint> format, string nom, IJournal journal)
    {
        uint identifiant;
        try
        {
            identifiant = format();
        }
        catch (InvalidOperationException exception)
        {
            journal.Avertissement($"Format {nom} indisponible : mise en forme ignorée.", exception);
            return null;
        }

        if (!NatifPressePapiers.IsClipboardFormatAvailable(identifiant))
        {
            return null;
        }

        var octets = Copier(NatifPressePapiers.GetClipboardData(identifiant), TailleMaxTexte, nom, journal);
        if (octets is null)
        {
            return null;
        }

        var fin = Array.IndexOf(octets, (byte)0);
        var utile = fin < 0 ? octets : octets[..fin];
        return utile.Length == 0 ? null : utile;
    }

    private static ContenuBrut? LireImage(IJournal journal)
    {
        // CF_DIBV5 d'abord : Windows fabrique CF_DIB par conversion, et l'alpha s'y perd.
        foreach (var format in new[] { NatifPressePapiers.CfDibV5, NatifPressePapiers.CfDib })
        {
            if (!NatifPressePapiers.IsClipboardFormatAvailable(format))
            {
                continue;
            }

            var dib = Copier(NatifPressePapiers.GetClipboardData(format), TailleMaxImage, "image", journal);
            if (dib is not null)
            {
                return new ContenuBrut(TypeElement.Image, Dib: dib);
            }
        }

        return null;
    }

    // Texte UTF-16 terminé par un nul : le bloc peut être plus grand que le texte, ou ne pas porter de nul du tout.
    private static string DecoderUtf16(byte[] octets)
    {
        var caracteres = MemoryMarshal.Cast<byte, char>(octets.AsSpan(0, octets.Length & ~1));
        var fin = caracteres.IndexOf('\0');
        return new string(fin < 0 ? caracteres : caracteres[..fin]);
    }

    /// <summary>Copie un bloc mémoire du presse-papiers ; null si la poignée est nulle, vide, verrouillage impossible ou trop gros.</summary>
    private static byte[]? Copier(nint poignee, long tailleMax, string description, IJournal journal, long? limiteAffichee = null)
    {
        if (poignee == 0)
        {
            journal.Avertissement($"Copie ignorée : les données « {description} » n'ont pas pu être obtenues (erreur Windows {Marshal.GetLastPInvokeError()}).");
            return null;
        }

        var taille = (long)NatifPressePapiers.GlobalSize(poignee);
        if (taille <= 0)
        {
            return null;
        }

        if (taille > tailleMax)
        {
            journal.Avertissement($"Copie ignorée : {description} de {taille / (1024 * 1024)} Mo, au-delà de {(limiteAffichee ?? tailleMax) / (1024 * 1024)} Mo.");
            return null;
        }

        var adresse = NatifPressePapiers.GlobalLock(poignee);
        if (adresse == 0)
        {
            journal.Avertissement($"Copie ignorée : verrouillage impossible des données « {description} » (erreur Windows {Marshal.GetLastPInvokeError()}).");
            return null;
        }

        try
        {
            var octets = new byte[taille];
            Marshal.Copy(adresse, octets, 0, (int)taille);
            return octets;
        }
        finally
        {
            NatifPressePapiers.GlobalUnlock(poignee);
        }
    }
}
