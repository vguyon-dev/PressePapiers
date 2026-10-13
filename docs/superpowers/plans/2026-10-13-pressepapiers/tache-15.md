### Tâche 15 : ConvertisseurImage

**Fichiers :**
- Créer : `src/PressePapiers.Coeur/Formats/EnteteDib.cs` (assistant interne : ajoute le BITMAPFILEHEADER, contrôle le DIB)
- Modifier : `src/PressePapiers.Coeur/Formats/ConvertisseurImage.cs` (remplace la coquille ; contenu complet à l'étape 3)
- Créer : `tests/PressePapiers.Tests/Outils/ImagesSynthetiques.cs` (fabrique de DIB et de PNG pour les tests, réutilisable)
- Tests : `tests/PressePapiers.Tests/Formats/ConvertisseurImageTests.cs`

**Interfaces :**
- Utilise (tâche 1) : la coquille `ConvertisseurImage` (mêmes signatures). Aucun paquet de plus : le projet Coeur cible
  `net10.0-windows10.0.19041.0`, qui fournit la projection `Windows.Graphics.Imaging` ; WIC ne demande ni fil d'interface ni
  initialisation (les tests tournent sur le pool de fils).
- Fournit :
  - `public static Task<byte[]> DibVersPngAsync(byte[] dib)` : `dib` = octets de CF_DIB ou CF_DIBV5 (sans en-tête de
    fichier) ; lève `ArgumentNullException`, ou `ArgumentException` si l'en-tête est invalide ou si les pixels sont tronqués
    (la tâche est alors en échec) ; l'alpha n'existe que pour un V5 en BI_BITFIELDS avec masque alpha non nul ;
  - `public static Task<byte[]> PngVersDibAsync(byte[] png)` : BITMAPINFOHEADER de 40 octets, 32 bits, BI_RGB, hauteur
    positive (lignes de bas en haut), alpha du PNG conservé dans le quatrième octet de chaque pixel ;
  - `public static Task<byte[]> MiniatureAsync(byte[] png, uint largeurMax = 160)` : PNG de la miniature (algorithme Fant,
    alpha conservé, hauteur proportionnelle d'au moins 1 pixel) ; une image déjà plus étroite que `largeurMax` est rendue
    en copie, jamais agrandie ; `ArgumentOutOfRangeException` si `largeurMax` vaut 0.
  - Interne : `EnteteDib.AjouterEnteteFichier(byte[] dib)` et `EnteteDib.DecalagePixels(ReadOnlySpan<byte> dib)`.
  - Consommateurs : tâche 21 (`LecteurPressePapiers` : DIB vers PNG puis miniature), tâche 22 (`EcrivainPressePapiers` :
    PNG vers DIB), tâche 26 (aperçu d'image).

**Point 7 de la spec, section 16 (conversion CF_DIBV5 vers PNG avec transparence) : mesures de cette tâche.**
Le décodeur BMP de WIC lit l'alpha d'un `BITMAPV5HEADER` en `BI_BITFIELDS` dont le masque alpha est non nul (`0xFF000000`),
et rend les valeurs **non prémultipliées, telles qu'écrites** : un pixel (B, G, R, A) = (64, 32, 16, 128) ressort à
l'identique. Il n'y a aucune transparence (alpha 255 partout) pour : un V5 sans masque alpha, un V5 en `BI_RGB`, un CF_DIB
de 32 bits en `BI_RGB` (le quatrième octet n'est alors pas de l'alpha, même s'il vaut 0). Cas prémultiplié : la conversion ne
devine rien. Un V5 dont l'alpha serait prémultiplié (ce que la documentation ne permet pas de savoir) donne un PNG aux bords
semi-transparents un peu plus sombres ; demander à WIC une sortie prémultipliée aurait, à l'inverse, **modifié** les
couleurs (mesuré : 64, 32, 16 devient 32, 16, 8 pour un alpha de 128). Le test
`L_alpha_d_un_dib_v5_est_lu_comme_non_premultiplie_et_les_couleurs_ne_sont_pas_modifiees` fige ce comportement. Durée mesurée
pour un DIB V5 de 3840 x 2160 (bruit structuré) vers PNG : environ 150 ms ; un bruit aléatoire pur, pire cas du codec PNG,
a mesuré 0,9 s à l'encodage lors de l'essai préalable, ce qui reste hors du délai de lecture du presse-papiers (la
conversion se fait après la fermeture du presse-papiers, tâche 21).

- [ ] **Étape 1 : Écrire le test qui échoue**

Fabrique de DIB et de PNG pour les tests :

`tests/PressePapiers.Tests/Outils/ImagesSynthetiques.cs`

```csharp
using System.Buffers.Binary;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PressePapiers.Tests.Outils;

/// <summary>Fabrique des DIB et des PNG synthétiques pour les tests de conversion d'image.</summary>
/// <remarks>Les pixels sont donnés au format BGRA, ligne du haut en premier, alpha non prémultiplié.</remarks>
internal static class ImagesSynthetiques
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
```

Tests :

`tests/PressePapiers.Tests/Formats/ConvertisseurImageTests.cs`

```csharp
using System.Buffers.Binary;
using System.Diagnostics;
using PressePapiers.Coeur.Formats;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Formats;

public sealed class ConvertisseurImageTests
{
    // Image 2 x 2 : haut = rouge opaque, vert à moitié transparent ; bas = bleu transparent, pixel « prémultiplié » (64, 32, 16, 128).
    private static readonly byte[] Quatre =
    [
        0, 0, 255, 255,     0, 255, 0, 128,
        255, 0, 0, 0,       64, 32, 16, 128,
    ];

    // Même image sans transparence.
    private static readonly byte[] QuatreOpaques =
    [
        0, 0, 255, 255,     0, 255, 0, 255,
        255, 0, 0, 255,     64, 32, 16, 255,
    ];

    private static async Task<byte[]> PngDe(byte[] dib)
    {
        var png = await ConvertisseurImage.DibVersPngAsync(dib);
        var (_, _, pixels) = await ImagesSynthetiques.LirePng(png);
        return pixels;
    }

    [Fact]
    public async Task Un_dib_24_bits_est_converti_avec_les_bonnes_couleurs_et_le_bon_sens()
    {
        var png = await ConvertisseurImage.DibVersPngAsync(ImagesSynthetiques.Dib24(2, 2, QuatreOpaques));

        var (largeur, hauteur, pixels) = await ImagesSynthetiques.LirePng(png);
        Assert.Equal((2, 2), (largeur, hauteur));
        // Un DIB stocke la dernière ligne en premier : une image à l'envers échouerait ici.
        Assert.Equal(QuatreOpaques, pixels);
    }

    [Fact]
    public async Task Un_png_valide_est_produit()
    {
        var png = await ConvertisseurImage.DibVersPngAsync(ImagesSynthetiques.Dib24(2, 2, QuatreOpaques));

        // Signature PNG.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
    }

    [Fact]
    public async Task Un_dib_32_bits_sans_masque_alpha_est_opaque_meme_si_le_quatrieme_octet_est_nul()
    {
        // CF_DIB : le quatrième octet n'est pas de l'alpha (BI_RGB), il vaut souvent 0 : l'image ne doit pas devenir invisible.
        var pixels = await PngDe(ImagesSynthetiques.Dib32Rgb(2, 2, Quatre));

        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(255, pixels[i * 4 + 3]));
        Assert.Equal(QuatreOpaques[..3], pixels[..3]);
    }

    [Fact]
    public async Task Un_dib_32_bits_bitfields_a_en_tete_de_40_octets_est_converti()
    {
        // Les trois masques placés après l'en-tête décalent le début des pixels de 12 octets.
        var pixels = await PngDe(ImagesSynthetiques.Dib32Bitfields(2, 2, QuatreOpaques));

        Assert.Equal(QuatreOpaques, pixels);
    }

    [Fact]
    public async Task Un_dib_8_bits_avec_palette_est_converti()
    {
        (byte B, byte G, byte R)[] palette = [(0, 0, 255), (0, 255, 0), (255, 0, 0)];
        var indices = new byte[] { 0, 1, 2, 0, 1, 2 };    // 3 x 2 : la largeur n'est pas multiple de 4

        var png = await ConvertisseurImage.DibVersPngAsync(ImagesSynthetiques.Dib8Palette(3, 2, indices, palette));

        var (largeur, hauteur, pixels) = await ImagesSynthetiques.LirePng(png);
        Assert.Equal((3, 2), (largeur, hauteur));
        // Rouge, vert, bleu sur chaque ligne ; la palette décale le début des pixels de 1 024 octets.
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixels[0..4]);
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, pixels[4..8]);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixels[8..12]);
    }

    [Fact]
    public async Task Un_dib_v5_avec_masque_alpha_garde_la_transparence()
    {
        var pixels = await PngDe(ImagesSynthetiques.DibV5(2, 2, Quatre, masqueAlpha: 0xFF000000));

        Assert.Equal(255, pixels[3]);    // rouge opaque
        Assert.Equal(128, pixels[7]);    // vert à moitié transparent
        Assert.Equal(0, pixels[11]);     // bleu entièrement transparent
        Assert.Equal(128, pixels[15]);
    }

    [Fact]
    public async Task L_alpha_d_un_dib_v5_est_lu_comme_non_premultiplie_et_les_couleurs_ne_sont_pas_modifiees()
    {
        // Cas prémultiplié (point 7 de la spec) : un V5 est lu comme du texte « droit » (valeurs telles qu'écrites).
        // Si le fichier source était prémultiplié, les bords semi-transparents seront un peu sombres ; la conversion
        // ne devine rien et n'altère aucun octet de couleur.
        var pixels = await PngDe(ImagesSynthetiques.DibV5(2, 2, Quatre, masqueAlpha: 0xFF000000));

        Assert.Equal(Quatre, pixels);
    }

    [Fact]
    public async Task Un_dib_v5_sans_masque_alpha_ou_en_bi_rgb_est_opaque()
    {
        var sansMasque = await PngDe(ImagesSynthetiques.DibV5(2, 2, Quatre, masqueAlpha: 0));
        var biRgb = await PngDe(ImagesSynthetiques.DibV5(2, 2, Quatre, masqueAlpha: 0xFF000000, compression: ImagesSynthetiques.BiRgb));

        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(255, sansMasque[i * 4 + 3]));
        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(255, biRgb[i * 4 + 3]));
    }

    [Fact]
    public async Task Un_dib_v5_de_haut_en_bas_hauteur_negative_est_converti_dans_le_bon_sens()
    {
        var pixels = await PngDe(ImagesSynthetiques.DibV5(2, 2, Quatre, masqueAlpha: 0xFF000000, hautEnBas: true));

        Assert.Equal(Quatre, pixels);
    }

    [Fact]
    public async Task Une_image_4K_est_convertie_en_png_sans_perte_et_vite()
    {
        const int largeur = 3840, hauteur = 2160;
        var source = ImagesSynthetiques.Motif(largeur, hauteur, (x, y) => ((byte)x, (byte)y, (byte)(x ^ y), (byte)(x % 251 + 4)));
        var dib = ImagesSynthetiques.DibV5(largeur, hauteur, source, masqueAlpha: 0xFF000000);

        var chrono = Stopwatch.StartNew();
        var png = await ConvertisseurImage.DibVersPngAsync(dib);
        chrono.Stop();
        TestContext.Current.SendDiagnosticMessage($"DIB 4K vers PNG : {chrono.ElapsedMilliseconds} ms, {png.Length} octets");

        var (l, h, pixels) = await ImagesSynthetiques.LirePng(png);
        Assert.Equal((largeur, hauteur), (l, h));
        Assert.Equal(source, pixels);
        Assert.True(chrono.Elapsed < TimeSpan.FromSeconds(20), $"Conversion 4K trop lente : {chrono.Elapsed}.");
    }

    [Fact]
    public async Task Une_image_4K_donne_une_miniature_de_160_pixels_de_large()
    {
        const int largeur = 3840, hauteur = 2160;
        var source = ImagesSynthetiques.Motif(largeur, hauteur, (x, y) => ((byte)(x / 16), (byte)(y / 9), 128, 255));
        var png = await ImagesSynthetiques.PngDepuis(largeur, hauteur, source);

        var miniature = await ConvertisseurImage.MiniatureAsync(png);

        var (l, h, _) = await ImagesSynthetiques.LirePng(miniature);
        Assert.Equal((160, 90), (l, h));
    }

    [Fact]
    public async Task La_miniature_respecte_la_largeur_demandee_et_les_proportions()
    {
        var png = await ImagesSynthetiques.PngDepuis(400, 1000,
            ImagesSynthetiques.Motif(400, 1000, (x, y) => (10, 20, 30, 255)));

        var (l, h, _) = await ImagesSynthetiques.LirePng(await ConvertisseurImage.MiniatureAsync(png, 100));

        Assert.Equal((100, 250), (l, h));
    }

    [Fact]
    public async Task Une_image_deja_petite_n_est_pas_agrandie()
    {
        var source = ImagesSynthetiques.Motif(100, 50, (x, y) => ((byte)x, (byte)y, 0, 255));
        var png = await ImagesSynthetiques.PngDepuis(100, 50, source);

        var miniature = await ConvertisseurImage.MiniatureAsync(png);

        var (l, h, pixels) = await ImagesSynthetiques.LirePng(miniature);
        Assert.Equal((100, 50), (l, h));
        Assert.Equal(source, pixels);
    }

    [Fact]
    public async Task Une_image_tres_plate_garde_au_moins_un_pixel_de_haut()
    {
        var png = await ImagesSynthetiques.PngDepuis(1000, 2,
            ImagesSynthetiques.Motif(1000, 2, (x, y) => (1, 2, 3, 255)));

        var (l, h, _) = await ImagesSynthetiques.LirePng(await ConvertisseurImage.MiniatureAsync(png));

        Assert.Equal(160, l);
        Assert.True(h >= 1);
    }

    [Fact]
    public async Task La_miniature_garde_la_transparence()
    {
        var png = await ImagesSynthetiques.PngDepuis(320, 320,
            ImagesSynthetiques.Motif(320, 320, (x, y) => (0, 0, 255, 100)));

        var (_, _, pixels) = await ImagesSynthetiques.LirePng(await ConvertisseurImage.MiniatureAsync(png));

        Assert.Equal(100, pixels[3]);
        Assert.Equal(100, pixels[^1]);
    }

    [Fact]
    public async Task Un_png_converti_en_dib_a_un_en_tete_de_40_octets_32_bits_et_des_lignes_de_bas_en_haut()
    {
        var png = await ImagesSynthetiques.PngDepuis(2, 2, QuatreOpaques);

        var dib = await ConvertisseurImage.PngVersDibAsync(png);

        Assert.Equal(40u, BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(0)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(4)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8)));      // positive : de bas en haut
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(12)));
        Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(14)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(16)));   // BI_RGB
        Assert.Equal(16u, BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(20)));  // taille des pixels
        Assert.Equal(40 + 16, dib.Length);
        // La première ligne stockée est la dernière de l'image : bleu puis (64, 32, 16).
        Assert.Equal(new byte[] { 255, 0, 0, 255, 64, 32, 16, 255 }, dib[40..48]);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 }, dib[48..56]);
    }

    [Fact]
    public async Task Un_png_avec_transparence_garde_son_alpha_dans_le_quatrieme_octet_du_dib()
    {
        var png = await ImagesSynthetiques.PngDepuis(2, 2, Quatre);

        var dib = await ConvertisseurImage.PngVersDibAsync(png);

        Assert.Equal(new byte[] { 255, 0, 0, 0, 64, 32, 16, 128 }, dib[40..48]);
    }

    [Fact]
    public async Task Un_aller_retour_png_dib_png_rend_les_memes_pixels_pour_une_image_opaque()
    {
        const int largeur = 37, hauteur = 23;    // dimensions impaires
        var source = ImagesSynthetiques.Motif(largeur, hauteur, (x, y) => ((byte)(x * 7), (byte)(y * 11), (byte)(x + y), 255));
        var png = await ImagesSynthetiques.PngDepuis(largeur, hauteur, source);

        var retour = await ConvertisseurImage.DibVersPngAsync(await ConvertisseurImage.PngVersDibAsync(png));

        var (l, h, pixels) = await ImagesSynthetiques.LirePng(retour);
        Assert.Equal((largeur, hauteur), (l, h));
        Assert.Equal(source, pixels);
    }

    [Fact]
    public async Task Un_png_4K_est_converti_en_dib()
    {
        const int largeur = 3840, hauteur = 2160;
        var png = await ImagesSynthetiques.PngDepuis(largeur, hauteur,
            ImagesSynthetiques.Motif(largeur, hauteur, (x, y) => ((byte)x, (byte)y, 0, 255)));

        var dib = await ConvertisseurImage.PngVersDibAsync(png);

        Assert.Equal(40 + largeur * hauteur * 4, dib.Length);
    }

    [Fact]
    public async Task Des_donnees_invalides_leve_une_exception_sans_planter()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ConvertisseurImage.DibVersPngAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ConvertisseurImage.PngVersDibAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ConvertisseurImage.MiniatureAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => ConvertisseurImage.DibVersPngAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() => ConvertisseurImage.DibVersPngAsync(new byte[10]));
        await Assert.ThrowsAsync<ArgumentException>(() => ConvertisseurImage.DibVersPngAsync(new byte[200]));
        await Assert.ThrowsAnyAsync<Exception>(() => ConvertisseurImage.PngVersDibAsync([1, 2, 3, 4]));
        await Assert.ThrowsAnyAsync<Exception>(() => ConvertisseurImage.MiniatureAsync([1, 2, 3, 4]));
    }

    [Fact]
    public async Task Un_dib_dont_les_pixels_sont_tronques_est_refuse()
    {
        var dib = ImagesSynthetiques.Dib24(8, 8, ImagesSynthetiques.Motif(8, 8, (x, y) => (1, 2, 3, 255)));

        await Assert.ThrowsAsync<ArgumentException>(() => ConvertisseurImage.DibVersPngAsync(dib[..(dib.Length - 40)]));
    }

    [Fact]
    public async Task Une_largeur_maximale_nulle_est_refusee()
    {
        var png = await ImagesSynthetiques.PngDepuis(2, 2, QuatreOpaques);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ConvertisseurImage.MiniatureAsync(png, 0));
    }
}
```

Ce que ces tests prouvent : `Un_dib_24_bits_est_converti_avec_les_bonnes_couleurs_et_le_bon_sens` et
`Un_dib_v5_de_haut_en_bas_hauteur_negative_est_converti_dans_le_bon_sens` échouent si l'image sort à l'envers ;
`Un_dib_8_bits_avec_palette_est_converti` et `Un_dib_32_bits_bitfields_a_en_tete_de_40_octets_est_converti` échouent si le
début des pixels est mal calculé (palette de 1 024 octets, masques de 12 octets) ;
`Un_dib_32_bits_sans_masque_alpha_est_opaque_meme_si_le_quatrieme_octet_est_nul` échoue si un CF_DIB devient transparent ;
`Un_dib_dont_les_pixels_sont_tronques_est_refuse` échoue si l'en-tête n'est pas confronté à la taille des données.

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : la compilation réussit (la coquille a les bonnes signatures) et les 22 nouveaux tests échouent avec
`System.NotImplementedException : Tâche 15` ; les 482 tests précédents restent verts :

```
Résumé de série de tests : Échec!
  total : 504
  échec : 22
  réussie : 482
```

- [ ] **Étape 3 : Implémenter**

En-tête de fichier BMP et contrôles du DIB :

`src/PressePapiers.Coeur/Formats/EnteteDib.cs`

```csharp
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
```

Conversions :

`src/PressePapiers.Coeur/Formats/ConvertisseurImage.cs`

```csharp
using System.Buffers.Binary;
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
    /// <exception cref="ArgumentException">L'en-tête du DIB est invalide ou ses pixels sont tronqués.</exception>
    public static async Task<byte[]> DibVersPngAsync(byte[] dib)
    {
        ArgumentNullException.ThrowIfNull(dib);

        using var bitmap = await DecoderAsync(EnteteDib.AjouterEnteteFichier(dib));
        return await EncoderPngAsync(bitmap);
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
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 504
  échec : 0
  réussie : 504
  ignoré : 0
```

Les 22 nouveaux tests : `ConvertisseurImageTests` 22.

- [ ] **Étape 5 : Vérifier sur le poste**

Point de vigilance 2 : relever la durée de la conversion 4K, bien inférieure à la limite de 20 s du test :

```bash
tests/PressePapiers.Tests/bin/Debug/net10.0-windows10.0.19041.0/PressePapiers.Tests.exe -diagnostics -method "*4K*"
```

À observer, sur le poste de référence : `DIB 4K vers PNG : 153 ms, 159439 octets` (ordre de grandeur ; seule la réussite et
l'égalité des pixels sont exigées par le test).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Formats/ConvertisseurImage.cs src/PressePapiers.Coeur/Formats/EnteteDib.cs tests/PressePapiers.Tests/Formats/ConvertisseurImageTests.cs tests/PressePapiers.Tests/Outils/ImagesSynthetiques.cs
git commit -m "Ajouter la conversion des images du presse-papiers" -m "DIB et DIBV5 vers PNG par WIC après ajout d'un BITMAPFILEHEADER (palette, masques et pixels tronqués contrôlés), alpha conservé pour un V5 avec masque alpha et recopié sans correction, PNG vers DIB 32 bits, miniature de 160 pixels au plus. Testé sur des images synthétiques dont une 3840 x 2160."
```
