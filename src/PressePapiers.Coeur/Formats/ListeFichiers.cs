using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace PressePapiers.Coeur.Formats;

/// <summary>
/// Format CF_HDROP : une structure DROPFILES (20 octets : <c>pFiles</c>, point, <c>fNC</c>, <c>fWide</c>)
/// suivie des chemins terminés chacun par un nul, la liste se finissant par un nul supplémentaire.
/// </summary>
public static class ListeFichiers
{
    private const int TailleEnTete = 20;

    /// <summary>
    /// Encode des chemins en structure DROPFILES, chemins en UTF-16. Les unités UTF-16 sont écrites telles quelles : NTFS
    /// accepte un substitut isolé dans un nom de fichier, et le remplacer par U+FFFD désignerait un autre fichier.
    /// </summary>
    /// <param name="chemins">Chemins complets, sans chemin vide ni caractère nul.</param>
    /// <exception cref="ArgumentException">Un chemin est vide ou contient un nul : il casserait le découpage de la liste.</exception>
    public static byte[] EncoderDropFiles(IReadOnlyList<string> chemins)
    {
        ArgumentNullException.ThrowIfNull(chemins);
        foreach (var chemin in chemins)
        {
            if (string.IsNullOrEmpty(chemin) || chemin.Contains('\0'))
            {
                throw new ArgumentException("Un chemin de fichier est vide ou contient un caractère nul.", nameof(chemins));
            }
        }

        // Liste vide : deux nuls quand même, pour que la structure reste un double nul valide pour les lecteurs.
        var texte = chemins.Count == 0 ? "\0\0" : string.Concat(chemins.Select(c => c + "\0")) + "\0";
        var donnees = new byte[TailleEnTete + texte.Length * sizeof(char)];
        BinaryPrimitives.WriteUInt32LittleEndian(donnees, TailleEnTete);          // pFiles
        BinaryPrimitives.WriteInt32LittleEndian(donnees.AsSpan(16), 1);            // fWide : chemins en UTF-16
        MemoryMarshal.AsBytes(texte.AsSpan()).CopyTo(donnees.AsSpan(TailleEnTete)); // Windows est petit-boutiste, comme DROPFILES
        return donnees;
    }

    /// <summary>Décode une structure DROPFILES en liste de chemins.</summary>
    /// <param name="donnees">Octets du format CF_HDROP.</param>
    /// <returns>
    /// Les chemins dans l'ordre. Liste vide si la structure est invalide ; un dernier chemin sans terminateur
    /// (données tronquées) est abandonné plutôt que rendu coupé.
    /// </returns>
    public static IReadOnlyList<string> DecoderDropFiles(ReadOnlySpan<byte> donnees)
    {
        if (donnees.Length < TailleEnTete)
        {
            return [];
        }

        var debut = BinaryPrimitives.ReadUInt32LittleEndian(donnees);
        if (debut < TailleEnTete || debut > donnees.Length)
        {
            return [];
        }

        var large = BinaryPrimitives.ReadInt32LittleEndian(donnees[16..]) != 0;
        var corps = donnees[(int)debut..];
        return large ? DecoderUtf16(corps) : DecoderAnsi(corps);
    }

    private static List<string> DecoderUtf16(ReadOnlySpan<byte> corps)
    {
        var chemins = new List<string>();
        var debutChemin = 0;
        // Un octet isolé en fin de données est ignoré : on ne lit que des unités UTF-16 entières.
        for (var i = 0; i + 1 < corps.Length; i += 2)
        {
            if (corps[i] != 0 || corps[i + 1] != 0)
            {
                continue;
            }

            if (i == debutChemin)
            {
                break;   // chemin vide : fin de liste (le double nul)
            }

            chemins.Add(TexteUtf16(corps[debutChemin..i]));
            debutChemin = i + 2;
        }

        return chemins;
    }

    // Copie les unités UTF-16 sans les interpréter (Encoding.Unicode remplacerait un substitut isolé par U+FFFD).
    private static string TexteUtf16(ReadOnlySpan<byte> octets)
    {
        var unites = new char[octets.Length / sizeof(char)];
        octets.CopyTo(MemoryMarshal.AsBytes(unites.AsSpan()));
        return new string(unites);
    }

    private static List<string> DecoderAnsi(ReadOnlySpan<byte> corps)
    {
        var encodage = EncodageAnsi();
        var chemins = new List<string>();
        var debutChemin = 0;
        for (var i = 0; i < corps.Length; i++)
        {
            if (corps[i] != 0)
            {
                continue;
            }

            if (i == debutChemin)
            {
                break;
            }

            chemins.Add(encodage.GetString(corps[debutChemin..i]));
            debutChemin = i + 1;
        }

        return chemins;
    }

    // Page de code ANSI du système ; Latin-1 si elle n'est pas disponible (jamais d'exception pour une lecture).
    private static Encoding EncodageAnsi()
    {
        try
        {
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Encoding.Latin1;
        }
    }
}
