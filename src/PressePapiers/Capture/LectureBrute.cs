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
/// fil dédié, cesse d'attendre au bout de deux secondes et reprend alors le texte déjà publié comme résultat partiel.
/// </summary>
internal static class LectureBrute
{
    /// <summary>Nombre d'essais d'ouverture quand une autre application tient le presse-papiers.</summary>
    public const int EssaisMax = 10;

    /// <summary>Pause entre deux essais d'ouverture.</summary>
    public static readonly TimeSpan PauseEntreEssais = TimeSpan.FromMilliseconds(25);

    /// <summary>
    /// Taille maximale d'un texte (en octets UTF-8), d'un RTF ou d'un HTML : au-delà, un texte fait ignorer la copie, un RTF
    /// ou un HTML seulement sa mise en forme.
    /// </summary>
    public const long TailleMaxTexte = 10L * 1024 * 1024;

    /// <summary>Taille maximale d'un DIB (une image 8K en 32 bits fait environ 133 Mo).</summary>
    public const long TailleMaxImage = 256L * 1024 * 1024;

    /// <summary>
    /// Lit le contenu selon l'ordre de la spec (section 4) : CF_HDROP donne Fichiers ; sinon un texte donne MisEnForme
    /// si un RTF ou un HTML l'accompagne, Texte sinon ; sinon CF_DIBV5 puis CF_DIB donnent Image.
    /// </summary>
    /// <param name="journal">Journal des copies ignorées.</param>
    /// <param name="publierPartiel">
    /// Reçoit le texte dès qu'il est lu, avant le RTF et le HTML dont la lecture peut bloquer : l'appelant le garde comme
    /// résultat partiel s'il cesse d'attendre. Appelé sur le fil de lecture.
    /// </param>
    /// <returns>Le contenu, ou null (marque interne présente, presse-papiers occupé, vide, illisible ou trop gros).</returns>
    public static ContenuBrut? Lire(IJournal journal, Action<ContenuBrut>? publierPartiel = null)
    {
        if (!Ouvrir(journal))
        {
            return null;
        }

        try
        {
            return LireOuvert(journal, publierPartiel);
        }
        finally
        {
            NatifPressePapiers.CloseClipboard();
        }
    }

    /// <summary>Taille lisible au journal : en octets, en Ko sous 1 Mo, en Mo avec une décimale au-delà.</summary>
    internal static string Taille(long octets) => octets switch
    {
        < 1024 => $"{octets} octets",
        < 1024 * 1024 => $"{octets / 1024.0:0.#} Ko",
        _ => $"{octets / (1024.0 * 1024):0.#} Mo",
    };

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

    private static ContenuBrut? LireOuvert(IJournal journal, Action<ContenuBrut>? publierPartiel)
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
            return LireTexte(journal, publierPartiel);
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
            "la liste de fichiers", "Copie ignorée", journal);
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

    private static ContenuBrut? LireTexte(IJournal journal, Action<ContenuBrut>? publierPartiel)
    {
        // Le bloc est en UTF-16 : un texte de plus de TailleMaxTexte caractères dépasse forcément la limite en UTF-8,
        // inutile de le copier (le petit surplus tient compte de l'arrondi de GlobalSize).
        var octets = Copier(NatifPressePapiers.GetClipboardData(NatifPressePapiers.CfUnicodeText),
            2 * TailleMaxTexte + 128, "le texte", "Copie ignorée", journal, TailleMaxTexte);
        if (octets is null)
        {
            return null;
        }

        var texte = DecoderUtf16(octets);
        var tailleUtf8 = Encoding.UTF8.GetByteCount(texte);
        if (tailleUtf8 > TailleMaxTexte)
        {
            journal.Avertissement($"Copie ignorée : le texte fait {Taille(tailleUtf8)}, au-delà de {Taille(TailleMaxTexte)}.");
            return null;
        }

        if (texte.Length == 0)
        {
            return null;
        }

        // Le texte suffit à garder la copie : il est publié avant les formats de mise en forme, plus souvent rendus en différé.
        publierPartiel?.Invoke(new ContenuBrut(TypeElement.Texte, Texte: texte));

        var rtf = LireFormatTexte(() => FormatsPressePapiers.Rtf, "RTF", journal);
        var html = LireFormatTexte(() => FormatsPressePapiers.Html, "HTML", journal);
        var type = rtf is not null || html is not null ? TypeElement.MisEnForme : TypeElement.Texte;
        return new ContenuBrut(type, Texte: texte, Rtf: rtf, Html: html);
    }

    // RTF et HTML sont des textes terminés par un nul dans un bloc que GlobalSize peut avoir arrondi : on coupe au premier nul.
    // Leur échec ne coûte que la mise en forme : le texte, déjà lu, est gardé.
    private static byte[]? LireFormatTexte(Func<uint> format, string nom, IJournal journal)
    {
        uint identifiant;
        try
        {
            identifiant = format();
        }
        catch (InvalidOperationException exception)
        {
            journal.Avertissement($"Format {nom} indisponible : mise en forme ignorée, le texte est gardé.", exception);
            return null;
        }

        if (!NatifPressePapiers.IsClipboardFormatAvailable(identifiant))
        {
            return null;
        }

        var octets = Copier(NatifPressePapiers.GetClipboardData(identifiant), TailleMaxTexte, $"le {nom}",
            $"Mise en forme {nom} ignorée (le texte est gardé)", journal);
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
        // CF_DIBV5 d'abord : Windows fabrique CF_DIB par conversion, et l'alpha s'y perd. Un échec sur CF_DIBV5 laisse
        // encore CF_DIB : la copie n'est dite ignorée qu'au dernier format possible.
        var dibV5Present = NatifPressePapiers.IsClipboardFormatAvailable(NatifPressePapiers.CfDibV5);
        var dibPresent = NatifPressePapiers.IsClipboardFormatAvailable(NatifPressePapiers.CfDib);

        if (dibV5Present)
        {
            var consequence = dibPresent ? "Image lue en CF_DIB, sans transparence" : "Copie ignorée";
            var dib = Copier(NatifPressePapiers.GetClipboardData(NatifPressePapiers.CfDibV5), TailleMaxImage,
                "l'image CF_DIBV5", consequence, journal);
            if (dib is not null)
            {
                return new ContenuBrut(TypeElement.Image, Dib: dib);
            }
        }

        if (dibPresent)
        {
            var dib = Copier(NatifPressePapiers.GetClipboardData(NatifPressePapiers.CfDib), TailleMaxImage,
                "l'image", "Copie ignorée", journal);
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

    /// <summary>
    /// Copie un bloc mémoire du presse-papiers ; null (noté au journal) si la poignée est nulle, vide, verrouillage
    /// impossible ou trop gros.
    /// </summary>
    /// <param name="poignee">Poignée rendue par GetClipboardData.</param>
    /// <param name="tailleMax">Taille du bloc au-delà de laquelle il n'est pas copié.</param>
    /// <param name="description">Ce que contient le bloc, avec son article (« le texte », « le RTF »).</param>
    /// <param name="consequence">Effet de l'échec, en tête du message (« Copie ignorée », « Mise en forme RTF ignorée »).</param>
    /// <param name="journal">Journal des échecs.</param>
    /// <param name="limiteAffichee">Limite à citer au journal quand elle diffère de <paramref name="tailleMax"/>.</param>
    private static byte[]? Copier(nint poignee, long tailleMax, string description, string consequence, IJournal journal,
                                  long? limiteAffichee = null)
    {
        if (poignee == 0)
        {
            journal.Avertissement($"{consequence} : impossible d'obtenir {description} (erreur Windows {Marshal.GetLastPInvokeError()}).");
            return null;
        }

        var taille = (long)NatifPressePapiers.GlobalSize(poignee);
        if (taille <= 0)
        {
            return null;
        }

        if (taille > tailleMax)
        {
            journal.Avertissement($"{consequence} : {description} fait {Taille(taille)}, au-delà de {Taille(limiteAffichee ?? tailleMax)}.");
            return null;
        }

        var adresse = NatifPressePapiers.GlobalLock(poignee);
        if (adresse == 0)
        {
            journal.Avertissement($"{consequence} : verrouillage impossible pour {description} (erreur Windows {Marshal.GetLastPInvokeError()}).");
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
