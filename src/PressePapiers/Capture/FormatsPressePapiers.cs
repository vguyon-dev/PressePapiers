using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using PressePapiers.Natif;

namespace PressePapiers.Capture;

/// <summary>Formats enregistrés du presse-papiers (RTF, HTML, PNG), enregistrés une seule fois par processus.</summary>
internal static class FormatsPressePapiers
{
    // Seuls les succès sont mémorisés : un échec d'enregistrement est retenté à l'appel suivant.
    private static readonly ConcurrentDictionary<string, uint> Enregistres = new(StringComparer.Ordinal);

    /// <summary>« Rich Text Format » (nom exact du format enregistré par Word et WordPad).</summary>
    public static uint Rtf => Enregistrer("Rich Text Format");

    /// <summary>« HTML Format » (en-tête texte puis document, voir FormatHtml).</summary>
    public static uint Html => Enregistrer("HTML Format");

    /// <summary>« PNG » : usage constaté, non documenté par Microsoft ; sert à garder la transparence d'une image.</summary>
    public static uint Png => Enregistrer("PNG");

    /// <summary>Enregistre un format par son nom.</summary>
    /// <exception cref="InvalidOperationException">Windows refuse l'enregistrement (RegisterClipboardFormat renvoie 0).</exception>
    public static uint Enregistrer(string nom)
    {
        if (Enregistres.TryGetValue(nom, out var connu))
        {
            return connu;
        }

        var format = NatifPressePapiers.RegisterClipboardFormat(nom);
        if (format == 0)
        {
            throw new InvalidOperationException(
                $"Windows a refusé d'enregistrer le format « {nom} » (erreur {Marshal.GetLastPInvokeError()}).");
        }

        Enregistres[nom] = format;
        return format;
    }
}
