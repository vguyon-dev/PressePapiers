using System.Buffers.Binary;

namespace PressePapiers.Tests.Outils;

/// <summary>DIB de profondeurs et d'en-têtes variés (1, 4, 16 bits, V4, CORE, haut en bas, RLE) et en-têtes hostiles.</summary>
internal static partial class ImagesSynthetiques
{
    public const uint BiRle8 = 1;
    public const uint BiJpeg = 4;
    public const uint BiPng = 5;
    public const uint BiAlphaBitfields = 6;

    /// <summary>En-tête BITMAPINFOHEADER de 40 octets quelconque, suivi de <paramref name="suite"/> (palette, pixels...).</summary>
    public static byte[] DibBrut(int largeur, int hauteur, int bits, uint compression, uint couleursUtilisees = 0, byte[]? suite = null) =>
        [.. EnteteInfo(largeur, hauteur, bits, compression, 0, couleursUtilisees), .. suite ?? []];

    /// <summary>DIB 24 bits de haut en bas : hauteur négative, première ligne du fichier = ligne du haut de l'image.</summary>
    public static byte[] Dib24HautEnBas(int largeur, int hauteur, byte[] bgraHautBas)
    {
        var pasLigne = (largeur * 3 + 3) / 4 * 4;
        var pixels = new byte[pasLigne * hauteur];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                var source = (y * largeur + x) * 4;
                var cible = y * pasLigne + x * 3;
                pixels[cible] = bgraHautBas[source];
                pixels[cible + 1] = bgraHautBas[source + 1];
                pixels[cible + 2] = bgraHautBas[source + 2];
            }
        }

        return [.. EnteteInfo(largeur, -hauteur, 24, BiRgb, pixels.Length, 0), .. pixels];
    }

    /// <summary>DIB 16 bits en BI_RGB (5-5-5) : les couleurs doivent valoir 0 ou 255 par canal pour revenir sans perte.</summary>
    public static byte[] Dib16Rgb555(int largeur, int hauteur, byte[] bgraHautBas)
    {
        var pixels = Pixels16(largeur, hauteur, bgraHautBas, (b, g, r) => (ushort)(((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3)));
        return [.. EnteteInfo(largeur, hauteur, 16, BiRgb, pixels.Length, 0), .. pixels];
    }

    /// <summary>DIB 16 bits en BI_BITFIELDS (5-6-5) : trois masques de 4 octets suivent l'en-tête de 40 octets.</summary>
    public static byte[] Dib16Bitfields565(int largeur, int hauteur, byte[] bgraHautBas)
    {
        var pixels = Pixels16(largeur, hauteur, bgraHautBas, (b, g, r) => (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3)));
        var masques = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(masques.AsSpan(0), 0xF800);
        BinaryPrimitives.WriteUInt32LittleEndian(masques.AsSpan(4), 0x07E0);
        BinaryPrimitives.WriteUInt32LittleEndian(masques.AsSpan(8), 0x001F);
        return [.. EnteteInfo(largeur, hauteur, 16, BiBitfields, pixels.Length, 0), .. masques, .. pixels];
    }

    /// <summary>DIB 4 bits avec palette de 16 entrées ; <paramref name="indices"/> (0 à 15) est donné ligne du haut en premier.</summary>
    public static byte[] Dib4Palette(int largeur, int hauteur, byte[] indices, (byte B, byte G, byte R)[] palette) =>
        DibIndexe(largeur, hauteur, indices, palette, bits: 4);

    /// <summary>DIB 1 bit avec palette de 2 entrées ; <paramref name="indices"/> (0 ou 1) est donné ligne du haut en premier.</summary>
    public static byte[] Dib1Palette(int largeur, int hauteur, byte[] indices, (byte B, byte G, byte R)[] palette) =>
        DibIndexe(largeur, hauteur, indices, palette, bits: 1);

    /// <summary>DIB 8 bits en BI_RLE8 : chaque ligne est une seule suite de pixels de l'indice donné (largeur de 255 au plus).</summary>
    public static byte[] Dib8Rle(int largeur, int hauteur, byte[] indiceParLigneHautBas, (byte B, byte G, byte R)[] palette)
    {
        var codes = new List<byte>();
        for (var y = hauteur - 1; y >= 0; y--)
        {
            codes.AddRange([(byte)largeur, indiceParLigneHautBas[y], 0, 0]);   // suite de pixels, puis fin de ligne
        }

        codes[^2] = 0;
        codes[^1] = 1;                                                           // la dernière fin de ligne devient fin de bitmap
        var table = Palette(palette, 256);
        return [.. EnteteInfo(largeur, hauteur, 8, BiRle8, codes.Count, 256), .. table, .. codes];
    }

    /// <summary>DIB V4 (108 octets) de 32 bits avec masques B, G, R, A standard ; <paramref name="compression"/> au choix.</summary>
    public static byte[] DibV4(int largeur, int hauteur, byte[] bgraHautBas, uint compression = BiAlphaBitfields)
    {
        var pixels = Retourner(largeur, hauteur, bgraHautBas);
        var entete = new byte[108];
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(0), 108);
        BinaryPrimitives.WriteInt32LittleEndian(entete.AsSpan(4), largeur);
        BinaryPrimitives.WriteInt32LittleEndian(entete.AsSpan(8), hauteur);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(14), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(16), compression);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(20), (uint)pixels.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(40), 0x00FF0000);   // rouge
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(44), 0x0000FF00);   // vert
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(48), 0x000000FF);   // bleu
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(52), 0xFF000000);   // alpha
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(56), 0x73524742);   // LCS_sRGB
        return [.. entete, .. pixels];
    }

    /// <summary>DIB à l'ancien en-tête BITMAPCOREHEADER (12 octets) de 24 bits, lignes de bas en haut.</summary>
    public static byte[] DibCore24(int largeur, int hauteur, byte[] bgraHautBas)
    {
        var pasLigne = (largeur * 3 + 3) / 4 * 4;
        var pixels = new byte[pasLigne * hauteur];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                var source = ((hauteur - 1 - y) * largeur + x) * 4;
                var cible = y * pasLigne + x * 3;
                pixels[cible] = bgraHautBas[source];
                pixels[cible + 1] = bgraHautBas[source + 1];
                pixels[cible + 2] = bgraHautBas[source + 2];
            }
        }

        var entete = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(0), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(4), (ushort)largeur);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(6), (ushort)hauteur);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(10), 24);
        return [.. entete, .. pixels];
    }

    private static byte[] Pixels16(int largeur, int hauteur, byte[] bgraHautBas, Func<int, int, int, ushort> encoder)
    {
        var pasLigne = (largeur * 2 + 3) / 4 * 4;
        var pixels = new byte[pasLigne * hauteur];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                var source = ((hauteur - 1 - y) * largeur + x) * 4;
                var valeur = encoder(bgraHautBas[source], bgraHautBas[source + 1], bgraHautBas[source + 2]);
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(y * pasLigne + x * 2), valeur);
            }
        }

        return pixels;
    }

    // Pixels à 1 ou 4 bits par indice, ligne du bas en premier, chaque ligne complétée à 4 octets, bits de poids fort d'abord.
    private static byte[] DibIndexe(int largeur, int hauteur, byte[] indices, (byte B, byte G, byte R)[] palette, int bits)
    {
        var pasLigne = (largeur * bits + 31) / 32 * 4;
        var pixels = new byte[pasLigne * hauteur];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                var indice = indices[(hauteur - 1 - y) * largeur + x];
                var bit = x * bits;
                pixels[y * pasLigne + bit / 8] |= (byte)(indice << (8 - bits - bit % 8));
            }
        }

        var couleurs = 1 << bits;
        return [.. EnteteInfo(largeur, hauteur, bits, BiRgb, pixels.Length, (uint)couleurs), .. Palette(palette, couleurs), .. pixels];
    }

    private static byte[] Palette((byte B, byte G, byte R)[] palette, int entrees)
    {
        var table = new byte[entrees * 4];
        for (var i = 0; i < palette.Length; i++)
        {
            table[i * 4] = palette[i].B;
            table[i * 4 + 1] = palette[i].G;
            table[i * 4 + 2] = palette[i].R;
        }

        return table;
    }
}
