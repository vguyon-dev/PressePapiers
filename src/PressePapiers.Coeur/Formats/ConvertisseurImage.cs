using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PressePapiers.Coeur.Formats;

/// <summary>
/// Conversions entre DIB du presse-papiers et PNG, par WIC (<c>Windows.Graphics.Imaging</c>).
/// Tout passe par des pixels BGRA non prémultipliés : l'alpha n'existe que pour un CF_DIBV5 qui déclare un masque alpha ;
/// les valeurs sont recopiées telles quelles, sans deviner si la source était prémultipliée.
/// </summary>
public static class ConvertisseurImage
{
    private const int TailleEnteteDib = 40;

    /// <summary>
    /// Convertit un CF_DIB ou CF_DIBV5 en PNG : un BITMAPFILEHEADER de 14 octets est ajouté devant le DIB pour que WIC
    /// le lise comme un fichier BMP. L'alpha est conservé pour un V5 en BI_BITFIELDS avec masque alpha ; dans tous les
    /// autres cas l'image est opaque (le quatrième octet d'un CF_DIB n'est pas de l'alpha).
    /// </summary>
    /// <param name="dib">Octets du DIB (CF_DIB ou CF_DIBV5).</param>
    /// <returns>Les octets du PNG.</returns>
    /// <exception cref="ArgumentException">
    /// L'en-tête du DIB est invalide ou hostile (profondeur de couleur inconnue, dimensions nulles, négatives ou démesurées,
    /// plus de 64 millions de pixels, compression JPEG, PNG ou inconnue, RLE disproportionné), ses pixels sont tronqués, ou le décodeur ne sait pas les lire.
    /// </exception>
    public static async Task<byte[]> DibVersPngAsync(byte[] dib)
    {
        ArgumentNullException.ThrowIfNull(dib);

        var fichier = EnteteDib.AjouterEnteteFichier(dib);
        SoftwareBitmap bitmap;
        try
        {
            bitmap = await DecoderAsync(fichier);
        }
        catch (COMException exception)
        {
            // L'en-tête était plausible mais le décodeur refuse les pixels (données corrompues, variante non gérée).
            throw new ArgumentException("Le décodeur d'images ne sait pas lire ce DIB.", nameof(dib), exception);
        }

        using (bitmap)
        {
            return await EncoderPngAsync(bitmap);
        }
    }

    /// <summary>
    /// Convertit un PNG en DIB, pour écrire CF_DIB : BITMAPINFOHEADER de 40 octets, 32 bits en BI_RGB, lignes de bas en haut.
    /// Le quatrième octet de chaque pixel garde l'alpha du PNG ; la plupart des lecteurs de CF_DIB l'ignorent, c'est le format
    /// « PNG » qui porte la transparence.
    /// </summary>
    /// <param name="png">Octets du PNG.</param>
    /// <returns>Les octets du DIB.</returns>
    public static async Task<byte[]> PngVersDibAsync(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);

        using var bitmap = await DecoderAsync(png);
        var largeur = bitmap.PixelWidth;
        var hauteur = bitmap.PixelHeight;
        var tailleImage = (long)largeur * hauteur * 4;
        if (tailleImage > int.MaxValue - TailleEnteteDib)
        {
            throw new NotSupportedException("L'image est trop grande pour un DIB.");
        }

        var pixels = new byte[tailleImage];
        bitmap.CopyToBuffer(pixels.AsBuffer());

        var dib = new byte[TailleEnteteDib + tailleImage];
        BinaryPrimitives.WriteUInt32LittleEndian(dib.AsSpan(0), TailleEnteteDib);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), largeur);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), hauteur);              // positive : lignes de bas en haut
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(dib.AsSpan(20), (uint)tailleImage);

        // Un DIB commence par la dernière ligne de l'image.
        var pas = largeur * 4;
        for (var y = 0; y < hauteur; y++)
        {
            System.Buffer.BlockCopy(pixels, (hauteur - 1 - y) * pas, dib, TailleEnteteDib + y * pas, pas);
        }

        return dib;
    }

    /// <summary>Réduit un PNG à une miniature de <paramref name="largeurMax"/> pixels de large au plus (jamais agrandie).</summary>
    /// <param name="png">Octets du PNG.</param>
    /// <param name="largeurMax">Largeur maximale en pixels, strictement positive.</param>
    /// <returns>Le PNG de la miniature, ou une copie de l'original s'il est déjà assez étroit.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="largeurMax"/> vaut 0.</exception>
    public static async Task<byte[]> MiniatureAsync(byte[] png, uint largeurMax = 160)
    {
        ArgumentNullException.ThrowIfNull(png);
        ArgumentOutOfRangeException.ThrowIfZero(largeurMax);

        using var flux = await OuvrirFluxAsync(png);
        var decodeur = await BitmapDecoder.CreateAsync(flux);
        if (decodeur.PixelWidth <= largeurMax)
        {
            return (byte[])png.Clone();
        }

        var hauteur = Math.Max(1u, (uint)Math.Round(decodeur.PixelHeight * (double)largeurMax / decodeur.PixelWidth));
        var transformation = new BitmapTransform
        {
            ScaledWidth = largeurMax,
            ScaledHeight = hauteur,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        using var bitmap = await decodeur.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
            transformation, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        return await EncoderPngAsync(bitmap);
    }

    // Décode n'importe quelle image que WIC connaît en pixels Bgra8 non prémultipliés.
    private static async Task<SoftwareBitmap> DecoderAsync(byte[] donnees)
    {
        using var flux = await OuvrirFluxAsync(donnees);
        var decodeur = await BitmapDecoder.CreateAsync(flux);
        return await decodeur.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
    }

    private static async Task<InMemoryRandomAccessStream> OuvrirFluxAsync(byte[] donnees)
    {
        var flux = new InMemoryRandomAccessStream();
        try
        {
            await flux.WriteAsync(donnees.AsBuffer());
            flux.Seek(0);
            return flux;
        }
        catch
        {
            flux.Dispose();
            throw;
        }
    }

    private static async Task<byte[]> EncoderPngAsync(SoftwareBitmap bitmap)
    {
        using var flux = new InMemoryRandomAccessStream();
        var encodeur = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, flux);
        encodeur.SetSoftwareBitmap(bitmap);
        await encodeur.FlushAsync();

        flux.Seek(0);
        using var lecture = flux.AsStreamForRead();
        using var copie = new MemoryStream((int)flux.Size);
        await lecture.CopyToAsync(copie);
        return copie.ToArray();
    }
}
