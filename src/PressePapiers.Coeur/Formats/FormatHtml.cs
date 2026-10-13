using System.Globalization;
using System.Text;

namespace PressePapiers.Coeur.Formats;

/// <summary>
/// Format « HTML Format » du presse-papiers : un en-tête texte (<c>Version</c>, <c>StartHTML</c>, <c>EndHTML</c>,
/// <c>StartFragment</c>, <c>EndFragment</c>) suivi d'un document HTML, le tout en UTF-8.
/// Les offsets sont des nombres d'octets depuis le tout début des données, en-tête compris.
/// </summary>
public static class FormatHtml
{
    private const string DebutMarqueur = "<!--StartFragment-->";
    private const string FinMarqueur = "<!--EndFragment-->";
    private const string Prefixe = "<html>\r\n<body>\r\n";
    private const string Suffixe = "\r\n</body>\r\n</html>";

    // Les offsets de l'en-tête ont une largeur fixe (10 chiffres) : la longueur de l'en-tête est connue avant de les calculer.
    private const string ModeleEnTete =
        "Version:1.0\r\nStartHTML:{0}\r\nEndHTML:{1}\r\nStartFragment:{2}\r\nEndFragment:{3}\r\n";

    private const string Zeros = "0000000000";

    /// <summary>Encode un fragment : en-tête <c>Version:1.0</c>, offsets en octets UTF-8, lignes terminées par CRLF.</summary>
    /// <param name="fragmentHtml">Fragment HTML à placer entre les marqueurs StartFragment et EndFragment.</param>
    /// <returns>Les octets UTF-8 du format, sans octet nul final (l'écrivain du presse-papiers l'ajoute).</returns>
    public static byte[] Encoder(string fragmentHtml)
    {
        ArgumentNullException.ThrowIfNull(fragmentHtml);

        var longueurEnTete = Encoding.UTF8.GetByteCount(string.Format(ModeleEnTete, Zeros, Zeros, Zeros, Zeros));
        var debutHtml = longueurEnTete;
        var debutFragment = debutHtml + Encoding.UTF8.GetByteCount(Prefixe + DebutMarqueur);
        var finFragment = debutFragment + Encoding.UTF8.GetByteCount(fragmentHtml);
        var finHtml = finFragment + Encoding.UTF8.GetByteCount(FinMarqueur + Suffixe);

        var texte = string.Format(ModeleEnTete, Offset(debutHtml), Offset(finHtml), Offset(debutFragment), Offset(finFragment))
                    + Prefixe + DebutMarqueur + fragmentHtml + FinMarqueur + Suffixe;
        return Encoding.UTF8.GetBytes(texte);
    }

    /// <summary>Extrait le fragment d'un « HTML Format » ; null si les données sont invalides.</summary>
    /// <param name="donnees">Octets bruts du format, avec ou sans octet nul final.</param>
    /// <returns>Le fragment décodé en UTF-8, ou null si les offsets manquent, sortent des données ou sont inversés.</returns>
    public static string? ExtraireFragment(ReadOnlySpan<byte> donnees)
    {
        var debut = LireOffset(donnees, "StartFragment:");
        var fin = LireOffset(donnees, "EndFragment:");
        if (debut is null || fin is null || debut < 0 || fin < debut || fin > donnees.Length)
        {
            return null;
        }

        return Encoding.UTF8.GetString(donnees[debut.Value..fin.Value]);
    }

    private static string Offset(int valeur) => valeur.ToString("D10", CultureInfo.InvariantCulture);

    // Cherche « Nom: » en début de ligne dans l'en-tête et lit l'entier qui suit. Fins de ligne CRLF ou LF acceptées :
    // les applications ne respectent pas toutes les CRLF.
    private static int? LireOffset(ReadOnlySpan<byte> donnees, string nom)
    {
        var motif = Encoding.ASCII.GetBytes(nom);
        var position = 0;
        while (position < donnees.Length)
        {
            var reste = donnees[position..];
            var finLigne = reste.IndexOf((byte)'\n');
            var ligne = finLigne < 0 ? reste : reste[..finLigne];

            if (ligne.StartsWith(motif))
            {
                var valeur = ligne[motif.Length..];
                if (valeur.Length > 0 && valeur[^1] == (byte)'\r')
                {
                    valeur = valeur[..^1];
                }

                return int.TryParse(Encoding.ASCII.GetString(valeur), NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var entier)
                    ? entier
                    : null;
            }

            // L'en-tête s'arrête au premier document : inutile de parcourir le contenu HTML.
            if (ligne.StartsWith("<"u8))
            {
                return null;
            }

            if (finLigne < 0)
            {
                break;
            }

            position += finLigne + 1;
        }

        return null;
    }
}
