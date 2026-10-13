using System.Text;
using PressePapiers.Coeur.Modele;
using PressePapiers.Interface;

namespace PressePapiers.Tests.Interface;

/// <summary>Aperçu d'une ligne de la liste : l'ellipse n'apparaît que si l'extrait est réellement coupé.</summary>
public sealed class ResumeElementTests
{
    private const int Max = ElementResume.LongueurMaxExtrait;

    // Un caractère hors du plan de base : deux unités UTF-16 pour un seul caractère Unicode.
    private static readonly string Emoji = char.ConvertFromUtf32(0x1F600);

    private static ElementResume Resume(string extrait, long taille) => new()
    {
        Id = 1,
        OngletId = 1,
        Type = TypeElement.Texte,
        Extrait = extrait,
        DateCopie = DateTimeOffset.UnixEpoch,
        DateUsage = DateTimeOffset.UnixEpoch,
        Taille = taille,
    };

    [Fact]
    public void Un_texte_court_n_a_pas_d_ellipse()
    {
        var resume = Resume("bonjour", Encoding.UTF8.GetByteCount("bonjour"));

        Assert.Equal("bonjour", ResumeElement.Apercu(resume));
    }

    [Fact]
    public void Un_texte_d_exactement_la_longueur_maximale_n_a_pas_d_ellipse_quand_la_taille_le_confirme()
    {
        var texte = new string('a', Max);

        Assert.Equal(texte, ResumeElement.Apercu(Resume(texte, Max)));
    }

    [Fact]
    public void Un_extrait_coupe_a_la_longueur_maximale_a_une_ellipse()
    {
        var extrait = new string('a', Max);

        Assert.Equal(extrait + " …", ResumeElement.Apercu(Resume(extrait, Max + 800)));
    }

    [Fact]
    public void Des_emojis_en_unites_utf16_depassant_la_longueur_sans_depasser_les_caracteres_n_ont_pas_d_ellipse()
    {
        // 300 emojis = 600 unités UTF-16 mais 300 caractères Unicode : l'extrait n'est pas coupé.
        var texte = string.Concat(Enumerable.Repeat(Emoji, 300));
        Assert.True(texte.Length >= Max);

        Assert.Equal(texte, ResumeElement.Apercu(Resume(texte, Encoding.UTF8.GetByteCount(texte))));
        Assert.Equal(texte, ResumeElement.ApercuTexte(texte, Encoding.UTF8.GetByteCount(texte)));
    }

    [Fact]
    public void Sans_indicateur_de_taille_seule_la_longueur_en_caracteres_unicode_compte()
    {
        var emojis = string.Concat(Enumerable.Repeat(Emoji, 300));
        var plein = string.Concat(Enumerable.Repeat(Emoji, Max));

        Assert.Equal(emojis, ResumeElement.ApercuTexte(emojis));
        Assert.Equal(plein + " …", ResumeElement.ApercuTexte(plein));
    }

    [Fact]
    public void Plus_de_deux_lignes_ont_une_ellipse_meme_sans_coupure()
    {
        var texte = "un\ndeux\ntrois";

        Assert.Equal("un\ndeux …", ResumeElement.Apercu(Resume(texte, Encoding.UTF8.GetByteCount(texte))));
    }
}
