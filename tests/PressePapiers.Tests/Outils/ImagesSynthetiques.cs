using System.Buffers.Binary;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PressePapiers.Tests.Outils;

/// <summary>Fabrique des DIB et des PNG synthétiques pour les tests de conversion d'image.</summary>
/// <remarks>Les pixels sont donnés au format BGRA, ligne du haut en premier, alpha non prémultiplié.</remarks>
internal static partial class ImagesSynthetiques
{
    public const uint BiRgb = 0;
    public const uint BiBitfields = 3;

    /// <summary>Pixels BGRA calculés par une fonction (colonne, ligne) vers (b, g, r, a).</summary>
    public static byte[] Motif(int largeur, int hauteur, Func<int, int, (byte B, byte G, byte R, byte A)> pixel)
    {
        var donnees = new byte[largeur * hauteur * 4];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                var (b, g, r, a) = pixel(x, y);
                var i = (y * largeur + x) * 4;
                donnees[i] = b;
                donnees[i + 1] = g;
                donnees[i + 2] = r;
                donnees[i + 3] = a;
            }
        }

        return donnees;
    }

    /// <summary>DIB 24 bits (BITMAPINFOHEADER, BI_RGB), lignes de bas en haut complétées à 4 octets.</summary>
    public static byte[] Dib24(int largeur, int hauteur, byte[] bgraHautBas)
    {
        var pasLigne = (largeur * 3 + 3) / 4 * 4;
        var pixels = new byte[pasLigne * hauteur];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                var source = ((hauteur - 1 - y) * largeur + x) * 4;   // la ligne y du fichier est la ligne (h-1-y) de l'image
                var cible = y * pasLigne + x * 3;
                pixels[cible] = bgraHautBas[source];
                pixels[cible + 1] = bgraHautBas[source + 1];
                pixels[cible + 2] = bgraHautBas[source + 2];
            }
        }

        return [.. EnteteInfo(largeur, hauteur, 24, BiRgb, pixels.Length, 0), .. pixels];
    }

    /// <summary>DIB 32 bits en BITMAPINFOHEADER BI_RGB : le quatrième octet est de l'alpha non déclaré, donc ignoré.</summary>
    public static byte[] Dib32Rgb(int largeur, int hauteur, byte[] bgraHautBas)
    {
        var pixels = Retourner(largeur, hauteur, bgraHautBas);
        return [.. EnteteInfo(largeur, hauteur, 32, BiRgb, pixels.Length, 0), .. pixels];
    }

    /// <summary>DIB 32 bits en BITMAPINFOHEADER BI_BITFIELDS : trois masques de 4 octets suivent l'en-tête de 40 octets.</summary>
    public static byte[] Dib32Bitfields(int largeur, int hauteur, byte[] bgraHautBas)
    {
        var pixels = Retourner(largeur, hauteur, bgraHautBas);
        var masques = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(masques.AsSpan(0), 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(masques.AsSpan(4), 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(masques.AsSpan(8), 0x000000FF);
        return [.. EnteteInfo(largeur, hauteur, 32, BiBitfields, pixels.Length, 0), .. masques, .. pixels];
    }

    /// <summary>DIB 8 bits avec palette de 256 entrées (BGRX) ; <paramref name="indices"/> est donné ligne du haut en premier.</summary>
    public static byte[] Dib8Palette(int largeur, int hauteur, byte[] indices, (byte B, byte G, byte R)[] palette)
    {
        var pasLigne = (largeur + 3) / 4 * 4;
        var pixels = new byte[pasLigne * hauteur];
        for (var y = 0; y < hauteur; y++)
        {
            Array.Copy(indices, (hauteur - 1 - y) * largeur, pixels, y * pasLigne, largeur);
        }

        var table = new byte[256 * 4];
        for (var i = 0; i < palette.Length; i++)
        {
            table[i * 4] = palette[i].B;
            table[i * 4 + 1] = palette[i].G;
            table[i * 4 + 2] = palette[i].R;
        }

        return [.. EnteteInfo(largeur, hauteur, 8, BiRgb, pixels.Length, 256), .. table, .. pixels];
    }

    /// <summary>DIB V5 de 32 bits, masques B, G, R standard ; <paramref name="masqueAlpha"/> à 0 pour déclarer l'absence d'alpha.</summary>
    public static byte[] DibV5(int largeur, int hauteur, byte[] bgraHautBas, uint masqueAlpha, uint compression = BiBitfields,
                               bool hautEnBas = false)
    {
        var pixels = hautEnBas ? bgraHautBas : Retourner(largeur, hauteur, bgraHautBas);
        var entete = new byte[124];
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(0), 124);
        BinaryPrimitives.WriteInt32LittleEndian(entete.AsSpan(4), largeur);
        BinaryPrimitives.WriteInt32LittleEndian(entete.AsSpan(8), hautEnBas ? -hauteur : hauteur);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(14), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(16), compression);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(20), (uint)pixels.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(40), 0x00FF0000);   // rouge
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(44), 0x0000FF00);   // vert
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(48), 0x000000FF);   // bleu
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(52), masqueAlpha);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(56), 0x73524742);   // LCS_sRGB
        return [.. entete, .. pixels];
    }

    /// <summary>Encode des pixels BGRA (non prémultipliés) en PNG par WIC.</summary>
    public static async Task<byte[]> PngDepuis(int largeur, int hauteur, byte[] bgraHautBas)
    {
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(bgraHautBas.AsBuffer(), BitmapPixelFormat.Bgra8, largeur, hauteur,
            BitmapAlphaMode.Straight);
        using var flux = new InMemoryRandomAccessStream();
        var encodeur = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, flux);
        encodeur.SetSoftwareBitmap(bitmap);
        await encodeur.FlushAsync();
        flux.Seek(0);
        using var lecture = flux.AsStreamForRead();
        using var copie = new MemoryStream();
        await lecture.CopyToAsync(copie);
        return copie.ToArray();
    }

    /// <summary>Décode un PNG en pixels BGRA non prémultipliés, ligne du haut en premier.</summary>
    public static async Task<(int Largeur, int Hauteur, byte[] Bgra)> LirePng(byte[] png)
    {
        using var flux = new InMemoryRandomAccessStream();
        await flux.WriteAsync(png.AsBuffer());
        flux.Seek(0);
        var decodeur = await BitmapDecoder.CreateAsync(flux);
        using var bitmap = await decodeur.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyToBuffer(pixels.AsBuffer());
        return (bitmap.PixelWidth, bitmap.PixelHeight, pixels);
    }

    // BITMAPINFOHEADER de 40 octets ; hauteur positive = lignes de bas en haut.
    private static byte[] EnteteInfo(int largeur, int hauteur, int bits, uint compression, int tailleImage, uint couleursUtilisees)
    {
        var entete = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(0), 40);
        BinaryPrimitives.WriteInt32LittleEndian(entete.AsSpan(4), largeur);
        BinaryPrimitives.WriteInt32LittleEndian(entete.AsSpan(8), hauteur);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(entete.AsSpan(14), (ushort)bits);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(16), compression);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(20), (uint)tailleImage);
        BinaryPrimitives.WriteUInt32LittleEndian(entete.AsSpan(32), couleursUtilisees);
        return entete;
    }

    // Un DIB classique stocke la dernière ligne de l'image en premier.
    private static byte[] Retourner(int largeur, int hauteur, byte[] bgraHautBas)
    {
        var pas = largeur * 4;
        var resultat = new byte[bgraHautBas.Length];
        for (var y = 0; y < hauteur; y++)
        {
            Array.Copy(bgraHautBas, (hauteur - 1 - y) * pas, resultat, y * pas, pas);
        }

        return resultat;
    }
}
