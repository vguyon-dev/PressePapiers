using System.Buffers.Binary;
using PressePapiers.Coeur.Formats;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Formats;

/// <summary>
/// En-têtes de DIB hostiles : chaque refus doit être une <see cref="ArgumentException"/> (jamais un dépassement de capacité,
/// une allocation démesurée ou une exception du décodeur), et arriver avant que WIC ne voie un seul octet.
/// </summary>
public sealed class EnteteDibHostileTests
{
    private static readonly byte[] Pixel = [0, 0, 0, 0];

    [Theory]
    [InlineData(int.MinValue, 2)]
    [InlineData(2, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    [InlineData(-3, 2)]
    public void Des_dimensions_impossibles_sont_refusees(int largeur, int hauteur)
    {
        var dib = ImagesSynthetiques.DibBrut(largeur, hauteur, 32, ImagesSynthetiques.BiRgb, suite: Pixel);

        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(dib));
    }

    [Fact]
    public async Task Une_largeur_int_MinValue_est_refusee_par_la_conversion_sans_depassement_de_capacite()
    {
        var dib = ImagesSynthetiques.DibBrut(int.MinValue, 1, 24, ImagesSynthetiques.BiRgb, suite: new byte[64]);

        await Assert.ThrowsAsync<ArgumentException>(() => ConvertisseurImage.DibVersPngAsync(dib));
    }

    [Theory]
    [InlineData(int.MaxValue, int.MaxValue, 32)]    // le produit déborde un entier de 64 bits non signé
    [InlineData(int.MaxValue, 3, 24)]
    [InlineData(100_000, 100_000, 32)]              // 10 milliards de pixels
    [InlineData(65_536, 65_536, 1)]                 // petit en octets (512 Mo) mais 4 milliards de pixels
    [InlineData(10_000, 10_000, 32)]                // 100 millions de pixels : au-dessus du plafond de 64 millions
    public void Des_dimensions_qui_debordent_ou_depassent_le_plafond_sont_refusees(int largeur, int hauteur, int bits)
    {
        var dib = ImagesSynthetiques.DibBrut(largeur, hauteur, bits, ImagesSynthetiques.BiRgb, suite: new byte[64]);

        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(dib));
    }

    [Fact]
    public void Le_plafond_de_64_millions_de_pixels_est_exact()
    {
        // 1 bit par pixel : 8 000 x 8 000 = 64 millions de pixels pour 8 Mo de données.
        var donnees = new byte[(8000 / 8) * 8000];
        var pile = ImagesSynthetiques.DibBrut(8000, 8000, 1, ImagesSynthetiques.BiRgb, suite: [.. new byte[8], .. donnees]);
        var unDeTrop = ImagesSynthetiques.DibBrut(8001, 8000, 1, ImagesSynthetiques.BiRgb, suite: [.. new byte[8], .. new byte[1001 * 8000]]);

        Assert.Equal(48, EnteteDib.DecalagePixels(pile));
        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(unDeTrop));
    }

    [Fact]
    public void Une_image_8K_est_acceptee()
    {
        // 7 680 x 4 320 en 1 bit : le plafond couvre l'ultra-haute définition.
        var dib = ImagesSynthetiques.DibBrut(7680, 4320, 1, ImagesSynthetiques.BiRgb, suite: [.. new byte[8], .. new byte[960 * 4320]]);

        Assert.Equal(48, EnteteDib.DecalagePixels(dib));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(33)]
    [InlineData(48)]
    [InlineData(64)]
    [InlineData(65_535)]
    public void Une_profondeur_de_couleur_hors_liste_est_refusee(int bits)
    {
        var dib = ImagesSynthetiques.DibBrut(2, 2, bits, ImagesSynthetiques.BiRgb, suite: new byte[4096]);

        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(dib));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void Les_sept_profondeurs_de_couleur_valides_sont_acceptees(int bits)
    {
        var dib = ImagesSynthetiques.DibBrut(2, 2, bits, ImagesSynthetiques.BiRgb, suite: new byte[4096]);

        Assert.True(EnteteDib.DecalagePixels(dib) >= 40);
    }

    [Theory]
    [InlineData(4, 32)]     // BI_JPEG
    [InlineData(5, 32)]     // BI_PNG
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    [InlineData(7, 32)]     // inconnue
    [InlineData(11, 32)]    // BI_CMYK
    [InlineData(0xFFFFFFFFu, 32)]
    [InlineData(1, 24)]     // RLE8 n'existe qu'à 8 bits
    [InlineData(2, 8)]      // RLE4 n'existe qu'à 4 bits
    [InlineData(3, 24)]     // BI_BITFIELDS n'existe qu'à 16 et 32 bits
    public void Les_compressions_refusees_ou_incoherentes_levent_ArgumentException(uint compression, int bits)
    {
        var dib = ImagesSynthetiques.DibBrut(2, 2, bits, compression, suite: new byte[4096]);

        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(dib));
    }

    [Fact]
    public async Task Un_png_ou_un_jpeg_embarque_dans_un_dib_est_refuse_par_la_conversion()
    {
        var png = await ImagesSynthetiques.PngDepuis(2, 2, new byte[16]);
        var dibPng = ImagesSynthetiques.DibBrut(2, 2, 0, ImagesSynthetiques.BiPng, suite: png);
        var dibJpeg = ImagesSynthetiques.DibBrut(2, 2, 0, ImagesSynthetiques.BiJpeg, suite: [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0]);

        await Assert.ThrowsAsync<ArgumentException>(() => ConvertisseurImage.DibVersPngAsync(dibPng));
        await Assert.ThrowsAsync<ArgumentException>(() => ConvertisseurImage.DibVersPngAsync(dibJpeg));
    }

    [Fact]
    public async Task Une_bombe_rle_de_1_Ko_annoncant_10_000_x_10_000_est_refusee()
    {
        var dib = BombeRle(10_000, 10_000);

        Assert.True(dib.Length < 2000);
        await Assert.ThrowsAsync<ArgumentException>(() => ConvertisseurImage.DibVersPngAsync(dib));
    }

    [Fact]
    public void Une_bombe_rle_sous_le_plafond_de_pixels_est_aussi_refusee_car_ses_octets_ne_peuvent_pas_coder_autant_de_pixels()
    {
        // 8 000 x 8 000 = 64 millions de pixels annoncés pour quelques centaines d'octets de codes.
        var dib = BombeRle(8000, 8000);

        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(dib));
    }

    [Fact]
    public void Un_dib_rle_dont_les_octets_peuvent_coder_les_pixels_annonces_est_accepte()
    {
        // 255 pixels par paire d'octets au mieux : 16 x 4 = 64 pixels pour 18 octets de codes passe largement.
        var dib = ImagesSynthetiques.Dib8Rle(16, 4, [0, 1, 2, 3], [(0, 0, 0), (1, 1, 1), (2, 2, 2), (3, 3, 3)]);

        Assert.Equal(40 + 1024, EnteteDib.DecalagePixels(dib));
    }

    [Fact]
    public void Une_palette_qui_annonce_plus_de_couleurs_que_la_profondeur_n_en_permet_est_refusee()
    {
        var dib = ImagesSynthetiques.DibBrut(2, 2, 4, ImagesSynthetiques.BiRgb, couleursUtilisees: 17, suite: new byte[4096]);

        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(dib));
    }

    [Fact]
    public void Une_palette_geante_est_refusee_sans_depassement_de_capacite()
    {
        var dib = ImagesSynthetiques.DibBrut(2, 2, 32, ImagesSynthetiques.BiRgb, couleursUtilisees: uint.MaxValue, suite: new byte[4096]);

        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(dib));
    }

    [Fact]
    public void Une_taille_d_en_tete_absurde_est_refusee()
    {
        var dib = ImagesSynthetiques.DibBrut(2, 2, 24, ImagesSynthetiques.BiRgb, suite: new byte[64]);
        BinaryPrimitives.WriteUInt32LittleEndian(dib, uint.MaxValue);

        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(dib));
    }

    [Fact]
    public void L_en_tete_core_est_controle_comme_les_autres()
    {
        var bon = ImagesSynthetiques.DibCore24(2, 2, new byte[16]);
        var tronque = bon[..(bon.Length - 4)];
        var profondeur = (byte[])bon.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(profondeur.AsSpan(10), 7);
        var vide = (byte[])bon.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(vide.AsSpan(4), 0);
        var gros = (byte[])bon.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(gros.AsSpan(4), 65_535);
        BinaryPrimitives.WriteUInt16LittleEndian(gros.AsSpan(6), 65_535);

        Assert.Equal(12, EnteteDib.DecalagePixels(bon));
        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(tronque));
        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(profondeur));
        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(vide));
        Assert.Throws<ArgumentException>(() => EnteteDib.DecalagePixels(gros));
    }

    [Fact]
    public void Un_dib_v4_ou_v5_en_bi_alphabitfields_a_ses_masques_dans_l_en_tete()
    {
        var v4 = ImagesSynthetiques.DibV4(2, 2, new byte[16]);
        var v5 = ImagesSynthetiques.DibV5(2, 2, new byte[16], 0xFF000000, ImagesSynthetiques.BiAlphaBitfields);

        Assert.Equal(108, EnteteDib.DecalagePixels(v4));
        Assert.Equal(124, EnteteDib.DecalagePixels(v5));
    }

    // 1 Ko de codes RLE8 (une suite de 255 pixels, puis fin de ligne, répétés) sous un en-tête qui annonce une énorme image.
    private static byte[] BombeRle(int largeur, int hauteur)
    {
        var codes = new byte[1000];
        for (var i = 0; i + 3 < codes.Length; i += 4)
        {
            codes[i] = 255;
        }

        return ImagesSynthetiques.DibBrut(largeur, hauteur, 8, ImagesSynthetiques.BiRle8, couleursUtilisees: 2,
                                          suite: [.. new byte[8], .. codes]);
    }
}
