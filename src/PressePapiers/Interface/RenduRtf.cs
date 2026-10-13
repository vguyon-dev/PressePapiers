using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Interface;

/// <summary>Charge un RTF dans un <see cref="RichEditBox"/> en lecture seule (spec, section 8).</summary>
internal static class RenduRtf
{
    /// <summary>Au-delà, l'aperçu reste en texte brut : charger un RTF de plusieurs mégaoctets gèlerait l'interface.</summary>
    public const int TailleMaximale = 1024 * 1024;

    /// <summary>Vide la boîte (libère le texte d'un gros RTF) puis la remet en lecture seule.</summary>
    public static void Vider(RichEditBox boite)
    {
        try
        {
            boite.IsReadOnly = false;
            boite.Document.SetText(TextSetOptions.None, string.Empty);
        }
        catch (Exception)
        {
            // Libérer la mémoire est un plus : une boîte qui refuse d'être vidée sera remplie à la prochaine sélection.
        }
        finally
        {
            boite.IsReadOnly = true;
        }
    }

    /// <summary>
    /// Charge le RTF puis remet la boîte en lecture seule, quoi qu'il arrive : le contrôle ne se remplit que lorsqu'il
    /// est modifiable. Renvoie faux si le RTF est trop gros, refusé ou ne donne aucun texte : l'appelant montre alors
    /// le texte brut avec la mention « mise en forme conservée pour le collage ».
    /// </summary>
    public static bool Charger(RichEditBox boite, byte[] rtf, IJournal? journal = null)
    {
        if (rtf.Length == 0 || rtf.Length > TailleMaximale)
        {
            return false;
        }

        try
        {
            boite.IsReadOnly = false;

            // Latin1 recopie les octets tels quels : un RTF est de l'ASCII, ses accents sont des séquences \'e9.
            // Le zéro final du format du presse-papiers est retiré.
            boite.Document.SetText(TextSetOptions.FormatRtf, Encoding.Latin1.GetString(rtf).TrimEnd('\0'));
            boite.Document.GetText(TextGetOptions.None, out var texte);
            boite.Document.Selection.SetRange(0, 0);
            return !string.IsNullOrWhiteSpace(texte);
        }
        catch (Exception exception)
        {
            journal?.Avertissement("Aperçu RTF impossible : l'élément s'affiche en texte brut.", exception);
            return false;
        }
        finally
        {
            boite.IsReadOnly = true;
        }
    }
}
