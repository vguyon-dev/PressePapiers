using System.Diagnostics;
using PressePapiers.Coeur.Recherche;

namespace PressePapiers.Tests.Recherche;

public sealed class NormalisationTests
{
    [Theory]
    [InlineData("Éàçü", "eacu")]
    [InlineData("ÉCOLE", "ecole")]
    [InlineData("naïve  café", "naive cafe")]
    [InlineData("Ça va", "ca va")]
    [InlineData("cœur", "coeur")]
    [InlineData("Œuvre", "oeuvre")]
    [InlineData("ŒUF", "oeuf")]
    [InlineData("Æon", "aeon")]
    [InlineData("æther", "aether")]
    [InlineData("e\u0301", "e")]
    [InlineData("  a \t\r\n b  ", "a b")]
    [InlineData("\u00A0a\u2003b", "a b")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("MAJUSCULES et minuscules", "majuscules et minuscules")]
    [InlineData("a😀b", "a😀b")]
    [InlineData("100 % sûr_", "100 % sur_")]
    public void Normaliser_ramene_casse_accents_ligatures_et_espaces(string entree, string attendu) =>
        Assert.Equal(attendu, Normalisation.Normaliser(entree));

    [Fact]
    public void Normaliser_rend_une_chaine_vide_pour_null() =>
        Assert.Equal("", Normalisation.Normaliser(null));

    [Fact]
    public void Normaliser_remplace_un_substitut_isole_au_lieu_de_lever_une_exception()
    {
        // Un presse-papiers peut contenir un demi-emoji : la saisie hostile ne doit jamais faire planter la capture.
        var resultat = Normalisation.Normaliser("a\uD83Db\uDE00c");

        Assert.Equal("a\uFFFDb\uFFFDc", resultat);
    }

    [Theory]
    [InlineData("Éàçü  Œuvre")]
    [InlineData("Déjà   vu\n\tà bientôt")]
    [InlineData("déjà normalisé")]
    public void Normaliser_est_idempotent(string entree)
    {
        var une = Normalisation.Normaliser(entree);

        Assert.Equal(une, Normalisation.Normaliser(une));
    }

    [Fact]
    public void TexteRecherche_reunit_nom_texte_fichiers_et_application_dans_cet_ordre()
    {
        var resultat = Normalisation.TexteRecherche(
            "Mon Café",
            "Ligne\nDeux",
            [@"C:\Dossier\Éé.txt", @"D:\Autre.doc"],
            "WindowsTerminal");

        Assert.Equal(@"mon cafe ligne deux c:\dossier\ee.txt d:\autre.doc windowsterminal", resultat);
    }

    [Fact]
    public void TexteRecherche_ignore_les_parties_absentes()
    {
        Assert.Equal("", Normalisation.TexteRecherche(null, null, null, null));
        Assert.Equal("", Normalisation.TexteRecherche("", "  ", [], ""));
        Assert.Equal("seulement le nom", Normalisation.TexteRecherche("Seulement le NOM", null, null, null));
        Assert.Equal("texte code", Normalisation.TexteRecherche(null, "Texte", null, "Code"));
    }

    [Fact]
    public void TexteRecherche_donne_le_meme_texte_que_Normaliser_de_la_concatenation()
    {
        var attendu = Normalisation.Normaliser("Nom Œuvre texte Été chemin Application");

        Assert.Equal(attendu, Normalisation.TexteRecherche("Nom Œuvre", "texte Été", ["chemin"], "Application"));
    }

    [Fact]
    public void Un_texte_de_9_Mo_se_normalise_en_moins_d_une_seconde()
    {
        // Échauffement : le premier appel paie la compilation à la volée, pas le calcul.
        Normalisation.Normaliser("chauffe é");
        Normalisation.TexteRecherche("n", "chauffe é", ["f"], "a");

        var accentue = Repeter("Été à l'œuvre, déjà ", 9 * 1024 * 1024);
        var simple = Repeter("Bonjour le monde ", 9 * 1024 * 1024);

        var chrono = Stopwatch.StartNew();
        var normaliseAccentue = Normalisation.Normaliser(accentue);
        var dureeAccentue = chrono.Elapsed;

        chrono.Restart();
        var normaliseSimple = Normalisation.Normaliser(simple);
        var dureeSimple = chrono.Elapsed;

        chrono.Restart();
        var recherche = Normalisation.TexteRecherche("nom", accentue, [@"C:\a.txt"], "Code");
        var dureeRecherche = chrono.Elapsed;

        TestContext.Current.SendDiagnosticMessage(
            $"9 Mo : accentué {dureeAccentue.TotalMilliseconds:F0} ms, simple {dureeSimple.TotalMilliseconds:F0} ms, " +
            $"TexteRecherche {dureeRecherche.TotalMilliseconds:F0} ms");

        Assert.StartsWith("ete a l'oeuvre, deja ete", normaliseAccentue);
        Assert.StartsWith("bonjour le monde bonjour", normaliseSimple);
        Assert.StartsWith("nom ete a l'oeuvre", recherche);
        Assert.True(dureeAccentue < TimeSpan.FromSeconds(1), $"Texte accentué de 9 Mo : {dureeAccentue.TotalMilliseconds:F0} ms");
        Assert.True(dureeSimple < TimeSpan.FromSeconds(1), $"Texte simple de 9 Mo : {dureeSimple.TotalMilliseconds:F0} ms");
        Assert.True(dureeRecherche < TimeSpan.FromSeconds(1), $"TexteRecherche de 9 Mo : {dureeRecherche.TotalMilliseconds:F0} ms");
    }

    // Texte de longueur donnée fait de la répétition d'un motif.
    private static string Repeter(string motif, int longueur) =>
        string.Concat(Enumerable.Repeat(motif, (longueur / motif.Length) + 1))[..longueur];
}
