using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

/// <summary>Remontée d'un doublon de Temporaire : la mise en forme de la seconde copie n'est pas perdue.</summary>
public sealed class DepotDoublonMiseEnFormeTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Un_texte_brut_recopie_avec_mise_en_forme_garde_le_rtf_et_le_html_de_la_seconde_copie()
    {
        var brut = Depot.AjouterCopie(ContenusDeTest.Texte("Titre"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
        var riche = ContenusDeTest.MisEnForme("Titre");

        var remonte = Depot.AjouterCopie(riche);

        Assert.Equal(brut.Id, remonte.Id);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        var relu = Depot.Lire(brut.Id)!;
        Assert.Equal(TypeElement.MisEnForme, relu.Type);
        Assert.Equal(riche.Rtf, relu.Rtf);
        Assert.Equal(riche.Html, relu.Html);
        Assert.Equal(TypeElement.MisEnForme, remonte.Type);
        Assert.Equal(riche.Rtf, remonte.Rtf);
        Assert.Equal(riche.Html, remonte.Html);
    }

    [Fact]
    public void L_element_devenu_mis_en_forme_le_signale_dans_le_resume()
    {
        var brut = Depot.AjouterCopie(ContenusDeTest.Texte("Titre"));
        Assert.False(Assert.Single(Depot.ListerResumes(Depot.OngletTemporaire.Id)).AMiseEnForme);

        Depot.AjouterCopie(ContenusDeTest.MisEnForme("Titre"));

        var resume = Assert.Single(Depot.ListerResumes(Depot.OngletTemporaire.Id));
        Assert.Equal(brut.Id, resume.Id);
        Assert.True(resume.AMiseEnForme);
        Assert.Equal(TypeElement.MisEnForme, resume.Type);
    }

    [Fact]
    public void Le_html_seul_suffit_a_donner_la_mise_en_forme()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("page"));

        var remonte = Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.MisEnForme, Texte = "page", Html = [1, 2, 3] });

        var relu = Depot.Lire(remonte.Id)!;
        Assert.Equal(TypeElement.MisEnForme, relu.Type);
        Assert.Equal([1, 2, 3], relu.Html);
        Assert.Null(relu.Rtf);
    }

    [Fact]
    public void Un_element_deja_mis_en_forme_garde_sa_mise_en_forme_d_origine()
    {
        var premier = ContenusDeTest.MisEnForme("Titre");
        var element = Depot.AjouterCopie(premier);
        var autre = new ContenuCopie { Type = TypeElement.MisEnForme, Texte = "Titre", Rtf = [7, 7, 7], Html = [8, 8] };

        Depot.AjouterCopie(autre);

        var relu = Depot.Lire(element.Id)!;
        Assert.Equal(premier.Rtf, relu.Rtf);
        Assert.Equal(premier.Html, relu.Html);
    }

    [Fact]
    public void La_remontee_ne_relit_pas_les_gros_contenus_et_les_laisse_en_base()
    {
        var riche = Depot.AjouterCopie(ContenusDeTest.MisEnForme("Titre", "Word"));

        // Une copie brute du même texte remonte l'élément : le RTF et le HTML stockés ne sont pas relus pour la réponse...
        var remonte = Depot.AjouterCopie(ContenusDeTest.Texte("Titre", "Notepad"));

        Assert.Equal(riche.Id, remonte.Id);
        Assert.Equal("Titre", remonte.Texte);
        Assert.Null(remonte.Rtf);
        Assert.Null(remonte.Html);
        Assert.Equal("Notepad", remonte.Application);
        // ... mais ils restent intacts en base, et la lecture complète les rend.
        var relu = Depot.Lire(riche.Id)!;
        Assert.Equal(riche.Rtf, relu.Rtf);
        Assert.Equal(riche.Html, relu.Html);
        Assert.Equal(TypeElement.MisEnForme, relu.Type);
    }

    [Fact]
    public void Une_liste_de_fichiers_recopiee_ne_prend_pas_de_mise_en_forme()
    {
        var fichiers = Depot.AjouterCopie(ContenusDeTest.Fichiers("C:/a.txt"));

        Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Fichiers, Fichiers = ["C:/a.txt"], Rtf = [1] });

        var relu = Depot.Lire(fichiers.Id)!;
        Assert.Equal(TypeElement.Fichiers, relu.Type);
        Assert.Null(relu.Rtf);
    }
}
