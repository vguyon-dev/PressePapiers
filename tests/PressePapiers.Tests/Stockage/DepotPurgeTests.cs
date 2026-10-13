using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DepotPurgeTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    private long Temporaire => Depot.OngletTemporaire.Id;

    public void Dispose() => _banc.Dispose();

    // ---- PurgerTemporaire ----

    [Fact]
    public void Purger_vide_Temporaire_avec_ses_images_et_renvoie_le_nombre()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("texte un"));
        Depot.AjouterCopie(ContenusDeTest.Texte("texte deux"));
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));

        var purges = Depot.PurgerTemporaire();

        Assert.Equal(3, purges);
        Assert.Equal(0, Depot.CompterElements(Temporaire));
        Assert.Empty(_banc.NomsImages());
    }

    [Fact]
    public void Purger_garde_les_elements_nommes_et_leurs_images()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var nomme = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(2)));
        Depot.Nommer(nomme.Id, "Logo", onglet.Id);
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(3)));
        Depot.AjouterCopie(ContenusDeTest.Texte("temporaire"));

        var purges = Depot.PurgerTemporaire();

        Assert.Equal(2, purges);
        Assert.Equal(1, Depot.CompterElements(onglet.Id));
        Assert.Equal([nomme.ImageFichier], _banc.NomsImages());
    }

    [Fact]
    public void Purger_un_temporaire_vide_renvoie_zero_sans_lever_Modifie()
    {
        var appels = 0;
        Depot.Modifie += () => appels++;

        Assert.Equal(0, Depot.PurgerTemporaire());

        Assert.Equal(0, appels);
    }

    [Fact]
    public void Purger_leve_Modifie_une_fois()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("a"));
        Depot.AjouterCopie(ContenusDeTest.Texte("b"));
        var appels = 0;
        Depot.Modifie += () => appels++;

        Depot.PurgerTemporaire();

        Assert.Equal(1, appels);
    }

    [Fact]
    public void Purger_quand_un_png_manque_deja_ne_leve_pas_d_exception()
    {
        var image = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(4)));
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(5)));
        File.Delete(Depot.CheminImage(image));

        var purges = Depot.PurgerTemporaire();

        Assert.Equal(2, purges);
        Assert.Empty(_banc.NomsImages());
    }

    // ---- AppliquerPlafond ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Un_plafond_nul_ou_negatif_est_desactive(int plafond)
    {
        for (var i = 0; i < 5; i++)
        {
            Depot.AjouterCopie(ContenusDeTest.Texte("element " + i));
        }

        Assert.Equal(0, Depot.AppliquerPlafond(plafond));

        Assert.Equal(5, Depot.CompterElements(Temporaire));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(5000)]
    public void Sous_le_plafond_ou_a_sa_hauteur_rien_n_est_supprime(int plafond)
    {
        for (var i = 0; i < 5; i++)
        {
            Depot.AjouterCopie(ContenusDeTest.Texte("element " + i));
        }

        var appels = 0;
        Depot.Modifie += () => appels++;

        Assert.Equal(0, Depot.AppliquerPlafond(plafond));

        Assert.Equal(5, Depot.CompterElements(Temporaire));
        Assert.Equal(0, appels);
    }

    [Fact]
    public void Le_plafond_supprime_les_plus_anciens_par_date_d_usage_et_non_par_id()
    {
        // Cinq éléments créés à une seconde d'intervalle ; le premier (le plus ancien par Id) est ensuite réutilisé.
        var elements = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
            elements.Add(Depot.AjouterCopie(ContenusDeTest.Texte("element " + i)).Id);
        }

        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
        Depot.MarquerUtilise(elements[0]);

        var supprimes = Depot.AppliquerPlafond(3);

        // Par date d'usage, les deux plus anciens sont les éléments 1 et 2 ; l'élément 0, réutilisé, est le plus récent.
        Assert.Equal(2, supprimes);
        Assert.Equal([elements[0], elements[4], elements[3]], Depot.ListerElements(Temporaire).Select(e => e.Id));
    }

    [Fact]
    public void A_date_egale_le_plafond_supprime_les_plus_petits_id()
    {
        var date = new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0, 4).Select(i => _banc.InsererElement(Temporaire, "element " + i, date)).ToArray();

        Assert.Equal(2, Depot.AppliquerPlafond(2));

        // C'est l'ordre inverse de celui de ListerElements : ce qui est affiché en bas de la liste part en premier.
        Assert.Equal([ids[3], ids[2]], Depot.ListerElements(Temporaire).Select(e => e.Id));
    }

    [Fact]
    public void Le_plafond_ne_compte_et_ne_touche_que_Temporaire()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var date = new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 10; i++)
        {
            _banc.InsererElement(onglet.Id, "nomme " + i, date.AddMinutes(-i - 1));
        }

        Depot.AjouterCopie(ContenusDeTest.Texte("temporaire un"));
        Depot.AjouterCopie(ContenusDeTest.Texte("temporaire deux"));

        Assert.Equal(0, Depot.AppliquerPlafond(2));
        Assert.Equal(10, Depot.CompterElements(onglet.Id));

        Assert.Equal(1, Depot.AppliquerPlafond(1));
        Assert.Equal(10, Depot.CompterElements(onglet.Id));
        Assert.Equal(1, Depot.CompterElements(Temporaire));
    }

    [Fact]
    public void Le_plafond_supprime_aussi_les_png_des_elements_retires_et_leve_Modifie_une_fois()
    {
        var ancienne = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(6)));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
        var recente = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(7)));
        var appels = 0;
        Depot.Modifie += () => appels++;

        Assert.Equal(1, Depot.AppliquerPlafond(1));

        Assert.Null(Depot.Lire(ancienne.Id));
        Assert.Equal([recente.ImageFichier], _banc.NomsImages());
        Assert.Equal(1, appels);
    }
}
