using System.Buffers.Binary;

namespace PressePapiers.Coeur.Formats;

/// <summary>
/// Transforme un DIB du presse-papiers (CF_DIB ou CF_DIBV5, sans en-tête de fichier) en fichier BMP en lui
/// ajoutant un BITMAPFILEHEADER de 14 octets : c'est la forme que le décodeur BMP de WIC sait lire.
/// L'en-tête vient d'une autre application, donc d'une source hostile : toutes les tailles sont contrôlées en
/// arithmétique vérifiée avant que le moindre octet parte vers le décodeur.
/// </summary>
internal static class EnteteDib
{
    /// <summary>Plafond de pixels d'une image acceptée : 64 millions, ce qui couvre largement une image 8K (33 millions).</summary>
    public const long PixelsMax = 64_000_000;

    private const int TailleEnteteFichier = 14;
    private const uint BiRgb = 0;
    private const uint BiRle8 = 1;
    private const uint BiRle4 = 2;
    private const uint BiBitfields = 3;
    private const uint BiAlphaBitfields = 6;

    // Une suite de codes RLE n'écrit jamais plus de 255 pixels par paire d'octets : au-delà, l'image annoncée est
    // une « bombe » (quelques centaines d'octets pour des centaines de mégaoctets de pixels).
    private const long PixelsMaxParOctetRle = 255;

    /// <summary>Ajoute le BITMAPFILEHEADER devant le DIB.</summary>
    /// <param name="dib">Octets du DIB.</param>
    /// <exception cref="ArgumentException">L'en-tête est invalide ou hostile, ou les pixels dépassent les données.</exception>
    public static byte[] AjouterEnteteFichier(byte[] dib)
    {
        var decalagePixels = DecalagePixels(dib);

        var fichier = new byte[checked(TailleEnteteFichier + dib.Length)];
        fichier[0] = (byte)'B';
        fichier[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(fichier.AsSpan(2), (uint)fichier.Length);                    // bfSize
        BinaryPrimitives.WriteUInt32LittleEndian(fichier.AsSpan(10), (uint)(TailleEnteteFichier + decalagePixels)); // bfOffBits
        dib.CopyTo(fichier, TailleEnteteFichier);

        // Dans un en-tête de 52 octets ou plus, les masques (alpha compris) sont déjà dans l'en-tête : BI_ALPHABITFIELDS
        // y veut dire BI_BITFIELDS, seule forme que WIC décode. Seule la copie envoyée à WIC change.
        if (BinaryPrimitives.ReadUInt32LittleEndian(dib) > 40
            && BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(16)) == BiAlphaBitfields)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(fichier.AsSpan(TailleEnteteFichier + 16), BiBitfields);
        }

        return fichier;
    }

    /// <summary>
    /// Position des pixels dans le DIB : taille de l'en-tête, plus les trois (ou quatre) masques qui suivent un
    /// BITMAPINFOHEADER de 40 octets en BI_BITFIELDS, plus la palette. Refuse aussi tout en-tête que le décodeur ne
    /// devrait jamais voir : profondeur de couleur hors de 1, 2, 4, 8, 16, 24 ou 32 bits, largeur ou hauteur nulle,
    /// négative (largeur) ou égale à <see cref="int.MinValue"/>, image de plus de <see cref="PixelsMax"/> pixels,
    /// compressions JPEG, PNG ou inconnues, RLE annonçant plus de pixels que ses octets ne peuvent en écrire.
    /// </summary>
    /// <param name="dib">Octets du DIB.</param>
    /// <exception cref="ArgumentException">L'en-tête est invalide ou hostile, ou les pixels dépassent les données.</exception>
    public static int DecalagePixels(ReadOnlySpan<byte> dib)
    {
        if (dib.Length < 12)
        {
            throw new ArgumentException("Le DIB est trop court pour contenir un en-tête.", nameof(dib));
        }

        var tailleEntete = BinaryPrimitives.ReadUInt32LittleEndian(dib);
        return tailleEntete == 12 ? DecalageCore(dib) : DecalageInfo(dib, tailleEntete);
    }

    // BITMAPCOREHEADER (ancien format OS/2) : largeur et hauteur sur 16 bits, palette de 3 octets par couleur, jamais de compression.
    private static int DecalageCore(ReadOnlySpan<byte> dib)
    {
        long largeur = BinaryPrimitives.ReadUInt16LittleEndian(dib[4..]);
        long hauteur = BinaryPrimitives.ReadUInt16LittleEndian(dib[6..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(dib[10..]);
        ControlerBits(bits);
        ControlerDimensions(largeur, hauteur);

        var couleurs = bits <= 8 ? 1L << bits : 0L;
        var decalage = 12 + 3 * couleurs;
        ControlerPixelsPresents(decalage, largeur, hauteur, bits, dib.Length);
        return (int)decalage;
    }

    private static int DecalageInfo(ReadOnlySpan<byte> dib, uint tailleEntete)
    {
        if (tailleEntete < 40 || tailleEntete > 124 || tailleEntete > dib.Length)
        {
            throw new ArgumentException($"Taille d'en-tête DIB inattendue : {tailleEntete}.", nameof(dib));
        }

        var largeur = BinaryPrimitives.ReadInt32LittleEndian(dib[4..]);
        var hauteur = BinaryPrimitives.ReadInt32LittleEndian(dib[8..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(dib[14..]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(dib[16..]);
        var utilisees = BinaryPrimitives.ReadUInt32LittleEndian(dib[32..]);

        // int.MinValue n'a pas d'opposé : on le refuse avant tout calcul de valeur absolue. Une largeur négative n'a pas de sens.
        if (largeur == int.MinValue || hauteur == int.MinValue || largeur <= 0)
        {
            throw new ArgumentException($"Dimensions DIB invalides : {largeur} x {hauteur}.", nameof(dib));
        }

        ControlerBits(bits);
        ControlerCompression(compression, bits);
        var hauteurAbsolue = Math.Abs((long)hauteur);
        ControlerDimensions(largeur, hauteurAbsolue);

        var masques = tailleEntete == 40
            ? compression switch { BiBitfields => 12, BiAlphaBitfields => 16, _ => 0 }
            : 0;   // à partir de 52 octets, les masques font partie de l'en-tête
        var couleurs = utilisees > 0 ? utilisees : (bits <= 8 ? 1u << bits : 0u);
        if (bits <= 8 && couleurs > 1u << bits)
        {
            throw new ArgumentException($"La palette annonce {couleurs} couleurs pour {bits} bits.", nameof(dib));
        }

        var decalage = checked((long)tailleEntete + masques + 4L * couleurs);
        if (decalage > dib.Length)
        {
            throw new ArgumentException("La palette ou les masques du DIB dépassent les données.", nameof(dib));
        }

        if (compression is BiRle8 or BiRle4)
        {
            // Les codes RLE ne se mesurent pas en lignes complètes, mais leur rendement par octet est borné.
            if (largeur * hauteurAbsolue > PixelsMaxParOctetRle * (dib.Length - decalage))
            {
                throw new ArgumentException("Le DIB compressé annonce bien plus de pixels que ses données n'en peuvent coder.", nameof(dib));
            }
        }
        else
        {
            // Pour les formats non compressés, on exige toutes les lignes : un DIB tronqué ne donne pas une image à moitié vide.
            ControlerPixelsPresents(decalage, largeur, hauteurAbsolue, bits, dib.Length);
        }

        return (int)decalage;
    }

    private static void ControlerBits(int bits)
    {
        if (bits is not (1 or 2 or 4 or 8 or 16 or 24 or 32))
        {
            throw new ArgumentException($"Profondeur de couleur DIB non gérée : {bits} bits.", "dib");
        }
    }

    // Seules les compressions que le décodeur BMP gère sans réserve passent ; JPEG et PNG embarqués sont refusés
    // (ils feraient lire n'importe quoi à WIC), et chaque compression doit aller avec une profondeur cohérente.
    private static void ControlerCompression(uint compression, int bits)
    {
        var coherent = compression switch
        {
            BiRgb => true,
            BiRle8 => bits == 8,
            BiRle4 => bits == 4,
            BiBitfields or BiAlphaBitfields => bits is 16 or 32,
            _ => false,
        };
        if (!coherent)
        {
            throw new ArgumentException($"Compression DIB refusée : {compression} avec {bits} bits.", "dib");
        }
    }

    private static void ControlerDimensions(long largeur, long hauteur)
    {
        if (largeur <= 0 || hauteur <= 0 || checked(largeur * hauteur) > PixelsMax)
        {
            throw new ArgumentException($"Dimensions DIB refusées : {largeur} x {hauteur} (au plus {PixelsMax} pixels).", "dib");
        }
    }

    // Lignes complètes de 4 octets en 4 octets ; les dimensions sont déjà plafonnées, le calcul ne peut pas déborder.
    private static void ControlerPixelsPresents(long decalage, long largeur, long hauteur, int bits, int longueur)
    {
        var pas = checked((largeur * bits + 31) / 32 * 4);
        var attendu = checked(decalage + pas * hauteur);
        if (attendu > longueur)
        {
            throw new ArgumentException("Les pixels du DIB sont absents ou tronqués.", "dib");
        }
    }
}
