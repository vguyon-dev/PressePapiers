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
