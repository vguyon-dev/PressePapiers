using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DepotMetaTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Une_cle_absente_donne_null()
    {
        Assert.Null(Depot.LireMeta("inconnue"));
    }

    [Fact]
    public void Une_valeur_ecrite_se_relit()
    {
        Depot.EcrireMeta("session.heure_ouverture", "133750000000000000");

        Assert.Equal("133750000000000000", Depot.LireMeta("session.heure_ouverture"));
    }

    [Fact]
    public void Ecrire_une_cle_existante_remplace_la_valeur_sans_creer_de_doublon()
    {
        Depot.EcrireMeta("cle", "avant");
        Depot.EcrireMeta("cle", "apres");

        Assert.Equal("apres", Depot.LireMeta("cle"));
        Assert.Equal("1", InsertionsSql.Scalaire(_banc.Connexion, "SELECT COUNT(*) FROM meta WHERE cle = 'cle'"));
    }

    [Fact]
    public void Les_cles_sont_independantes_et_sensibles_a_la_casse()
    {
        Depot.EcrireMeta("a", "1");
        Depot.EcrireMeta("A", "2");

        Assert.Equal("1", Depot.LireMeta("a"));
        Assert.Equal("2", Depot.LireMeta("A"));
    }

    [Fact]
    public void Une_valeur_vide_ou_accentuee_se_relit_telle_quelle()
    {
        Depot.EcrireMeta("vide", "");
        Depot.EcrireMeta("accents", "été à Noël");

        Assert.Equal("", Depot.LireMeta("vide"));
        Assert.Equal("été à Noël", Depot.LireMeta("accents"));
    }

    [Fact]
    public void La_version_du_schema_ecrite_a_la_creation_se_lit()
    {
        Assert.Equal("2", Depot.LireMeta("schema.version"));
    }

    [Fact]
    public void Les_valeurs_survivent_a_la_fermeture_de_la_base()
    {
        using var racine = new DossierTemporaire();
        var dossiers = new DossierDonnees(racine.Fichier("donnees"));
        dossiers.CreerDossiers();

        using (var premiere = BaseHistorique.Ouvrir(dossiers.Base))
        {
            new DepotHistorique(premiere, dossiers, TimeProvider.System, JournalNul.Instance).EcrireMeta("cle", "valeur");
        }

        using var seconde = BaseHistorique.Ouvrir(dossiers.Base);
        Assert.Equal("valeur", new DepotHistorique(seconde, dossiers, TimeProvider.System, JournalNul.Instance).LireMeta("cle"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Une_cle_vide_ou_nulle_est_refusee(string? cle)
    {
        Assert.ThrowsAny<ArgumentException>(() => Depot.LireMeta(cle!));
        Assert.ThrowsAny<ArgumentException>(() => Depot.EcrireMeta(cle!, "valeur"));
    }

    [Fact]
    public void Une_valeur_nulle_est_refusee()
    {
        Assert.Throws<ArgumentNullException>(() => Depot.EcrireMeta("cle", null!));
    }

    [Fact]
    public void Ecrire_une_valeur_ne_leve_pas_Modifie_car_aucune_liste_n_en_est_changee()
    {
        var appels = 0;
        Depot.Modifie += () => appels++;

        Depot.EcrireMeta("cle", "valeur");
        Depot.EcrireMeta("cle", "autre");
        _ = Depot.LireMeta("cle");

        Assert.Equal(0, appels);
        Assert.Equal("autre", Depot.LireMeta("cle"));
    }
}
