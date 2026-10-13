using PressePapiers.Coeur.Formats;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Formats;

/// <summary>Profondeurs et en-têtes de DIB variés : chacun doit donner les bonnes couleurs dans le bon sens.</summary>
public sealed class ConvertisseurImageFormatsTests
{
    // 2 x 2 : haut = rouge, vert ; bas = bleu, blanc. Que des canaux à 0 ou 255 : les formats 16 bits les restituent sans perte.
    private static readonly byte[] Quatre =
    [
        0, 0, 255, 255,       0, 255, 0, 255,
        255, 0, 0, 255,       255, 255, 255, 255,
    ];

    private static readonly (byte B, byte G, byte R)[] PaletteRvbN = [(0, 0, 255), (0, 255, 0), (255, 0, 0), (0, 0, 0)];

    private static async Task<(int Largeur, int Hauteur, byte[] Pixels)> Convertir(byte[] dib)
    {
        var png = await ConvertisseurImage.DibVersPngAsync(dib);
        return await ImagesSynthetiques.LirePng(png);
    }

    [Fact]
    public async Task Un_dib_24_bits_de_haut_en_bas_est_converti_dans_le_bon_sens()
    {
        var (largeur, hauteur, pixels) = await Convertir(ImagesSynthetiques.Dib24HautEnBas(2, 2, Quatre));

        Assert.Equal((2, 2), (largeur, hauteur));
        Assert.Equal(Quatre, pixels);
    }

    [Fact]
    public async Task Un_dib_16_bits_en_bi_rgb_est_converti()
    {
        var (_, _, pixels) = await Convertir(ImagesSynthetiques.Dib16Rgb555(2, 2, Quatre));

        Assert.Equal(Quatre, pixels);
    }

    [Fact]
    public async Task Un_dib_16_bits_bitfields_565_est_converti()
    {
        var (_, _, pixels) = await Convertir(ImagesSynthetiques.Dib16Bitfields565(2, 2, Quatre));

        Assert.Equal(Quatre, pixels);
    }

    [Fact]
    public async Task Un_dib_4_bits_avec_palette_est_converti_meme_si_la_largeur_n_est_pas_paire()
    {
        // 3 x 2 : un quartet de bourrage en fin de ligne, puis le bourrage à 4 octets.
        byte[] indices = [0, 1, 2, 3, 2, 1];

        var (largeur, hauteur, pixels) = await Convertir(ImagesSynthetiques.Dib4Palette(3, 2, indices, PaletteRvbN));

        Assert.Equal((3, 2), (largeur, hauteur));
        for (var i = 0; i < indices.Length; i++)
        {
            var (b, g, r) = PaletteRvbN[indices[i]];
            Assert.Equal(new byte[] { b, g, r, 255 }, pixels[(i * 4)..(i * 4 + 4)]);
        }
    }

    [Fact]
    public async Task Un_dib_1_bit_avec_palette_est_converti_sur_plusieurs_octets_par_ligne()
    {
        // 10 x 2 : une ligne tient sur deux octets utiles, bits de poids fort d'abord.
        byte[] indices = [1, 0, 1, 1, 0, 0, 0, 1, 1, 0,    0, 1, 0, 0, 1, 1, 1, 0, 0, 1];
        (byte B, byte G, byte R)[] palette = [(0, 0, 0), (255, 255, 255)];

        var (largeur, hauteur, pixels) = await Convertir(ImagesSynthetiques.Dib1Palette(10, 2, indices, palette));

        Assert.Equal((10, 2), (largeur, hauteur));
        for (var i = 0; i < indices.Length; i++)
        {
            var attendu = indices[i] == 1 ? (byte)255 : (byte)0;
            Assert.Equal(new byte[] { attendu, attendu, attendu, 255 }, pixels[(i * 4)..(i * 4 + 4)]);
        }
    }

    [Fact]
    public async Task Un_dib_8_bits_rle_raisonnable_est_converti()
    {
        // 16 x 4 : chaque ligne est une suite de 16 pixels d'un même indice.
        byte[] lignes = [0, 1, 2, 3];

        var (largeur, hauteur, pixels) = await Convertir(ImagesSynthetiques.Dib8Rle(16, 4, lignes, PaletteRvbN));

        Assert.Equal((16, 4), (largeur, hauteur));
        for (var y = 0; y < 4; y++)
        {
            var (b, g, r) = PaletteRvbN[lignes[y]];
            Assert.Equal(new byte[] { b, g, r, 255 }, pixels[((y * 16 + 5) * 4)..((y * 16 + 5) * 4 + 4)]);
        }
    }

    [Fact]
    public async Task Un_dib_v4_en_bi_alphabitfields_garde_la_transparence_ou_au_moins_les_couleurs()
    {
        byte[] translucide =
        [
            0, 0, 255, 255,     0, 255, 0, 128,
            255, 0, 0, 64,      255, 255, 255, 255,
        ];

        var (_, _, pixels) = await Convertir(ImagesSynthetiques.DibV4(2, 2, translucide));

        Assert.Equal(translucide[..3], pixels[..3]);
        Assert.Equal(translucide[4..7], pixels[4..7]);
        Assert.Equal(translucide[8..11], pixels[8..11]);
    }

    [Fact]
    public async Task Un_dib_v5_en_bi_alphabitfields_est_converti()
    {
        var (_, _, pixels) = await Convertir(
            ImagesSynthetiques.DibV5(2, 2, Quatre, masqueAlpha: 0xFF000000, compression: ImagesSynthetiques.BiAlphaBitfields));

        Assert.Equal(Quatre[..3], pixels[..3]);
        Assert.Equal(Quatre[4..7], pixels[4..7]);
        Assert.Equal(Quatre[8..11], pixels[8..11]);
    }

    [Fact]
    public async Task Un_dib_a_l_ancien_en_tete_core_est_converti()
    {
        var (largeur, hauteur, pixels) = await Convertir(ImagesSynthetiques.DibCore24(2, 2, Quatre));

        Assert.Equal((2, 2), (largeur, hauteur));
        Assert.Equal(Quatre, pixels);
    }
}
