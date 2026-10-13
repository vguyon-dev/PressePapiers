using System.Buffers.Binary;

namespace PressePapiers.Coeur.Formats;

/// <summary>
/// Transforme un DIB du presse-papiers (CF_DIB ou CF_DIBV5, sans en-tête de fichier) en fichier BMP en lui
/// ajoutant un BITMAPFILEHEADER de 14 octets : c'est la forme que le décodeur BMP de WIC sait lire.
/// </summary>
internal static class EnteteDib
{
    private const int TailleEnteteFichier = 14;
    private const uint BiRgb = 0;
    private const uint BiBitfields = 3;
    private const uint BiAlphaBitfields = 6;

    /// <summary>Ajoute le BITMAPFILEHEADER devant le DIB.</summary>
    /// <param name="dib">Octets du DIB.</param>
    /// <exception cref="ArgumentException">L'en-tête est invalide, ou les pixels dépassent les données.</exception>
    public static byte[] AjouterEnteteFichier(byte[] dib)
    {
        var decalagePixels = DecalagePixels(dib);

        var fichier = new byte[TailleEnteteFichier + dib.Length];
        fichier[0] = (byte)'B';
        fichier[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(fichier.AsSpan(2), (uint)fichier.Length);                    // bfSize
        BinaryPrimitives.WriteUInt32LittleEndian(fichier.AsSpan(10), (uint)(TailleEnteteFichier + decalagePixels)); // bfOffBits
        dib.CopyTo(fichier, TailleEnteteFichier);
        return fichier;
    }

    /// <summary>
    /// Position des pixels dans le DIB : taille de l'en-tête, plus les trois (ou quatre) masques qui suivent un
    /// BITMAPINFOHEADER de 40 octets en BI_BITFIELDS, plus la palette.
    /// </summary>
    /// <param name="dib">Octets du DIB.</param>
    /// <exception cref="ArgumentException">L'en-tête est invalide, ou les pixels dépassent les données.</exception>
    public static int DecalagePixels(ReadOnlySpan<byte> dib)
    {
        if (dib.Length < 12)
        {
            throw new ArgumentException("Le DIB est trop court pour contenir un en-tête.", nameof(dib));
        }

        var tailleEntete = BinaryPrimitives.ReadUInt32LittleEndian(dib);
        if (tailleEntete == 12)
        {
            // BITMAPCOREHEADER (ancien format OS/2) : palette de 3 octets par couleur, jamais de compression.
            var bitsCore = BinaryPrimitives.ReadUInt16LittleEndian(dib[10..]);
            var couleursCore = bitsCore <= 8 ? 1 << bitsCore : 0;
            return Verifier(12 + 3 * couleursCore, dib.Length);
        }

        if (tailleEntete < 40 || tailleEntete > 124 || tailleEntete > dib.Length)
        {
            throw new ArgumentException($"Taille d'en-tête DIB inattendue : {tailleEntete}.", nameof(dib));
        }

        var largeur = BinaryPrimitives.ReadInt32LittleEndian(dib[4..]);
        var hauteur = BinaryPrimitives.ReadInt32LittleEndian(dib[8..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(dib[14..]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(dib[16..]);
        var utilisees = BinaryPrimitives.ReadUInt32LittleEndian(dib[32..]);

        var masques = tailleEntete == 40
            ? compression switch { BiBitfields => 12, BiAlphaBitfields => 16, _ => 0 }
            : 0;   // à partir de 52 octets, les masques font partie de l'en-tête
        var couleurs = utilisees > 0 ? utilisees : (bits is > 0 and <= 8 ? 1u << bits : 0u);
        var decalage = (long)tailleEntete + masques + 4L * couleurs;
        if (decalage > dib.Length)
        {
            throw new ArgumentException("La palette ou les masques du DIB dépassent les données.", nameof(dib));
        }

        // Pour les formats non compressés, on exige toutes les lignes : un DIB tronqué ne donne pas une image à moitié vide.
        if (compression is BiRgb or BiBitfields or BiAlphaBitfields)
        {
            var pas = ((long)Math.Abs(largeur) * bits + 31) / 32 * 4;
            var attendu = decalage + pas * Math.Abs((long)hauteur);
            if (largeur == 0 || hauteur == 0 || attendu > dib.Length)
            {
                throw new ArgumentException("Les pixels du DIB sont absents ou tronqués.", nameof(dib));
            }
        }

        return (int)decalage;
    }

    private static int Verifier(int decalage, int longueur) =>
        decalage <= longueur ? decalage : throw new ArgumentException("La palette du DIB dépasse les données.", "dib");
}
