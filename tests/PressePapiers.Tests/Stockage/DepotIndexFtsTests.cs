using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

/// <summary>L'index plein texte doit suivre chaque écriture du dépôt : insertion, renommage, suppression, purge, plafond.</summary>
public sealed class DepotIndexFtsTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Le_controle_d_integrite_detecte_un_index_desynchronise()
    {
        // Preuve que l'assistant des autres tests voit bien une désynchronisation.
        Depot.AjouterCopie(ContenusDeTest.Texte("zebulon"));
        ControlerIndex();

        Executer("INSERT INTO element_fts(element_fts) VALUES ('delete-all')");

        Assert.Throws<SqliteException>(ControlerIndex);
    }

    [Fact]
    public void Un_element_ajoute_est_aussitot_dans_l_index()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("Le zébulon bondit", "Code"));

        // Texte normalisé (sans accent), mot entier ou fragment de trois lettres et plus, et application d'origine.
        Assert.Equal([element.Id], Trouver("zebulon"));
        Assert.Equal([element.Id], Trouver("bul"));
        Assert.Equal([element.Id], Trouver("code"));
        Assert.Empty(Trouver("girafe"));
        ControlerIndex();
    }

    [Fact]
    public void Un_element_recopie_avec_une_autre_application_met_l_index_a_jour()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon", "Code"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));

        Depot.AjouterCopie(ContenusDeTest.Texte("zebulon", "Chrome"));

        Assert.Equal([element.Id], Trouver("chrome"));
        Assert.Empty(Trouver("code"));
        ControlerIndex();
    }

    [Fact]
    public void Nommer_met_l_index_a_jour_le_nom_remplace_l_ancien_nom()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("adresse postale"));
        Assert.Empty(Trouver("facturation"));

        Depot.Nommer(element.Id, "Facturation", onglet.Id);
        Assert.Equal([element.Id], Trouver("facturation"));
        Assert.Equal([element.Id], Trouver("postale"));
        ControlerIndex();

        Depot.Nommer(element.Id, "Livraison", onglet.Id);
        Assert.Empty(Trouver("facturation"));
        Assert.Equal([element.Id], Trouver("livraison"));
        Assert.Equal([element.Id], Trouver("postale"));
        ControlerIndex();
    }

    [Fact]
    public void Marquer_utilise_laisse_l_index_intact()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));

        Depot.MarquerUtilise(element.Id);

        Assert.Equal([element.Id], Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Supprimer_retire_l_element_de_l_index()
    {
        var garde = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon garde"));
        var condamne = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon condamne"));
        Assert.Equal([garde.Id, condamne.Id], Trouver("zebulon"));

        Depot.Supprimer(condamne.Id);

        Assert.Equal([garde.Id], Trouver("zebulon"));
        Assert.Empty(Trouver("condamne"));
        ControlerIndex();
    }

    [Fact]
    public void Purger_Temporaire_retire_ses_elements_de_l_index_et_garde_les_nommes()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var nomme = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon nomme"));
        Depot.Nommer(nomme.Id, "Garde", onglet.Id);
        Depot.AjouterCopie(ContenusDeTest.Texte("zebulon temporaire"));

        Depot.PurgerTemporaire();

        Assert.Equal([nomme.Id], Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Le_plafond_retire_les_elements_supprimes_de_l_index()
    {
        var ancien = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon ancien"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
        var recent = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon recent"));

        Depot.AppliquerPlafond(1);

        Assert.Equal([recent.Id], Trouver("zebulon"));
        Assert.DoesNotContain(ancien.Id, Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Supprimer_un_onglet_retire_ses_elements_de_l_index()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon rangé"));
        Depot.Nommer(element.Id, "Rangé", onglet.Id);
        var reste = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon reste"));

        Depot.SupprimerOnglet(onglet.Id);

        Assert.Equal([reste.Id], Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Reconstruire_l_index_repare_un_index_vide_ou_desynchronise()
    {
        var premier = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon un"));
        var second = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon deux"));
        Executer("INSERT INTO element_fts(element_fts) VALUES ('delete-all')");
        Assert.Empty(Trouver("zebulon"));
        Assert.Throws<SqliteException>(ControlerIndex);

        Depot.ReconstruireIndex();

        Assert.Equal([premier.Id, second.Id], Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Reconstruire_l_index_d_une_base_saine_ne_change_rien_et_leve_Modifie()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon"));
        var appels = 0;
        Depot.Modifie += () => appels++;

        Depot.ReconstruireIndex();

        Assert.Equal([element.Id], Trouver("zebulon"));
        Assert.Equal(1, appels);
        ControlerIndex();
    }

    // Mot ou fragment entre guillemets doubles : une requête MATCH sans syntaxe FTS5 (le moteur de recherche fait de même).
    private List<long> Trouver(string mot) => InsertionsSql.Trouver(_banc.Connexion, "\"" + mot + "\"");

    // Compare l'index à la table element : lève SqliteException à la moindre divergence.
    private void ControlerIndex() => Executer("INSERT INTO element_fts(element_fts, rank) VALUES ('integrity-check', 1)");

    private void Executer(string sql)
    {
        using var commande = _banc.Connexion.CreateCommand();
        commande.CommandText = sql;
        commande.ExecuteNonQuery();
    }
}
