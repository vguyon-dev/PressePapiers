using System.Globalization;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DepotOngletsTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Le_depot_cree_l_onglet_temporaire_une_seule_fois()
    {
        var temporaire = Depot.OngletTemporaire;

        Assert.Equal("Temporaire", temporaire.Nom);
        Assert.Equal(0, temporaire.Ordre);
        Assert.True(temporaire.EstTemporaire);

        // Un second dépôt sur la même base retrouve l'onglet au lieu d'en créer un autre.
        var second = new DepotHistorique(_banc.Base, _banc.Dossiers, _banc.Horloge, JournalNul.Instance);
        Assert.Equal(temporaire.Id, second.OngletTemporaire.Id);
        Assert.Equal("1", InsertionsSql.Scalaire(_banc.Connexion, "SELECT COUNT(*) FROM onglet WHERE est_temporaire = 1"));
        Assert.Single(Depot.ListerOnglets());
    }

    [Fact]
    public void Rouvrir_la_base_garde_le_meme_onglet_temporaire_et_les_onglets()
    {
        using var racine = new DossierTemporaire();
        var dossiers = new DossierDonnees(racine.Fichier("donnees"));
        dossiers.CreerDossiers();
        long idTemporaire;

        using (var premiere = BaseHistorique.Ouvrir(dossiers.Base))
        {
            var depot = new DepotHistorique(premiere, dossiers, TimeProvider.System, JournalNul.Instance);
            idTemporaire = depot.OngletTemporaire.Id;
            depot.CreerOnglet("Clients");
        }

        using var seconde = BaseHistorique.Ouvrir(dossiers.Base);
        var rouvert = new DepotHistorique(seconde, dossiers, TimeProvider.System, JournalNul.Instance);

        Assert.Equal(idTemporaire, rouvert.OngletTemporaire.Id);
        Assert.Equal(["Temporaire", "Clients"], rouvert.ListerOnglets().Select(o => o.Nom));
    }

    [Fact]
    public void Les_onglets_se_listent_dans_l_ordre_de_creation_Temporaire_en_premier()
    {
        Depot.CreerOnglet("Bravo");
        Depot.CreerOnglet("Alpha");
        Depot.CreerOnglet("Charlie");

        var onglets = Depot.ListerOnglets();

        // L'ordre n'est pas alphabétique : c'est celui de la création.
        Assert.Equal(["Temporaire", "Bravo", "Alpha", "Charlie"], onglets.Select(o => o.Nom));
        Assert.Equal([0, 1, 2, 3], onglets.Select(o => o.Ordre));
        Assert.Equal([true, false, false, false], onglets.Select(o => o.EstTemporaire));
    }

    [Fact]
    public void Creer_un_onglet_renvoie_l_onglet_ajoute_en_dernier()
    {
        Depot.CreerOnglet("Premier");

        var cree = Depot.CreerOnglet("Dernier");

        Assert.Equal("Dernier", cree.Nom);
        Assert.Equal(2, cree.Ordre);
        Assert.False(cree.EstTemporaire);
        Assert.Equal(cree, Depot.ListerOnglets()[^1]);
    }

    [Fact]
    public void Creer_un_onglet_retire_les_espaces_autour_du_nom()
    {
        var cree = Depot.CreerOnglet("  Clients  ");

        Assert.Equal("Clients", cree.Nom);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Creer_un_onglet_sans_nom_leve_ArgumentException(string nom)
    {
        Assert.Throws<ArgumentException>(() => Depot.CreerOnglet(nom));

        Assert.Single(Depot.ListerOnglets());
    }

    [Fact]
    public void Creer_un_onglet_sans_nom_null_leve_une_exception_d_argument()
    {
        Assert.ThrowsAny<ArgumentException>(() => Depot.CreerOnglet(null!));
    }

    [Fact]
    public void Renommer_un_onglet_change_son_nom_seulement()
    {
        var onglet = Depot.CreerOnglet("Ancien");

        Depot.RenommerOnglet(onglet.Id, "Nouveau");

        var relu = Depot.ListerOnglets().Single(o => o.Id == onglet.Id);
        Assert.Equal("Nouveau", relu.Nom);
        Assert.Equal(onglet.Ordre, relu.Ordre);
    }

    [Fact]
    public void Renommer_Temporaire_leve_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => Depot.RenommerOnglet(Depot.OngletTemporaire.Id, "Autre"));

        Assert.Equal("Temporaire", Depot.ListerOnglets()[0].Nom);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Renommer_avec_un_nom_vide_leve_ArgumentException_et_garde_le_nom(string nom)
    {
        var onglet = Depot.CreerOnglet("Garde");

        Assert.Throws<ArgumentException>(() => Depot.RenommerOnglet(onglet.Id, nom));

        Assert.Equal("Garde", Depot.ListerOnglets().Single(o => o.Id == onglet.Id).Nom);
    }

    [Fact]
    public void Renommer_un_onglet_inconnu_leve_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Depot.RenommerOnglet(9999, "Fantome"));
    }

    // Départ : Temporaire, A, B, C, D. L'ordre demandé est la position finale de l'onglet dans la liste (Temporaire = 0).
    [Theory]
    [InlineData("D", 1, "D,A,B,C")]
    [InlineData("D", 3, "A,B,D,C")]
    [InlineData("A", 99, "B,C,D,A")]
    [InlineData("A", 4, "B,C,D,A")]
    [InlineData("A", 1, "A,B,C,D")]
    [InlineData("B", 3, "A,C,B,D")]
    [InlineData("C", 1, "C,A,B,D")]
    [InlineData("C", 0, "C,A,B,D")]
    [InlineData("C", -5, "C,A,B,D")]
    public void Deplacer_un_onglet_le_range_a_la_position_demandee(string nom, int nouvelOrdre, string attendu)
    {
        var parNom = CreerAbcd();

        Depot.DeplacerOnglet(parNom[nom].Id, nouvelOrdre);

        var onglets = Depot.ListerOnglets();
        Assert.Equal(["Temporaire", .. attendu.Split(',')], onglets.Select(o => o.Nom));
        // Les ordres restent consécutifs, sans trou ni doublon.
        Assert.Equal(Enumerable.Range(0, 5), onglets.Select(o => o.Ordre));
        Assert.True(onglets[0].EstTemporaire);
    }

    [Fact]
    public void Deplacer_Temporaire_leve_InvalidOperationException_et_le_laisse_en_premier()
    {
        CreerAbcd();

        Assert.Throws<InvalidOperationException>(() => Depot.DeplacerOnglet(Depot.OngletTemporaire.Id, 3));

        Assert.Equal(["Temporaire", "A", "B", "C", "D"], Depot.ListerOnglets().Select(o => o.Nom));
    }

    [Fact]
    public void Deplacer_un_onglet_inconnu_leve_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Depot.DeplacerOnglet(9999, 1));
    }

    [Fact]
    public void Supprimer_un_onglet_supprime_ses_elements_et_leurs_png_et_laisse_le_reste()
    {
        var condamne = Depot.CreerOnglet("Condamne");
        var epargne = Depot.CreerOnglet("Epargne");
        var png1 = CreerPng("a1.png");
        var png2 = CreerPng("a2.png");
        var pngEpargne = CreerPng("b1.png");
        _banc.InsererElement(condamne.Id, "zebulon un", imageFichier: png1);
        _banc.InsererElement(condamne.Id, "zebulon deux", imageFichier: png2);
        var idEpargne = _banc.InsererElement(epargne.Id, "zebulon trois", imageFichier: pngEpargne);
        var idTemporaire = _banc.InsererElement(Depot.OngletTemporaire.Id, "zebulon quatre");

        Depot.SupprimerOnglet(condamne.Id);

        Assert.Equal(["Temporaire", "Epargne"], Depot.ListerOnglets().Select(o => o.Nom));
        Assert.Equal("0", InsertionsSql.Scalaire(_banc.Connexion, $"SELECT COUNT(*) FROM element WHERE onglet_id = {condamne.Id}"));
        Assert.Equal(["b1.png"], _banc.NomsImages());
        // Les suppressions passent par les déclencheurs : l'index plein texte ne garde que les deux éléments restants.
        Assert.Equal([idEpargne, idTemporaire], InsertionsSql.Trouver(_banc.Connexion, "\"zebulon\""));
    }

    [Fact]
    public void Supprimer_un_onglet_dont_un_png_manque_deja_ne_leve_pas_d_exception()
    {
        var onglet = Depot.CreerOnglet("Abime");
        _banc.InsererElement(onglet.Id, "perdu", imageFichier: "absent-du-disque.png");

        Depot.SupprimerOnglet(onglet.Id);

        Assert.Single(Depot.ListerOnglets());
        Assert.Equal("0", InsertionsSql.Scalaire(_banc.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Supprimer_Temporaire_leve_InvalidOperationException_et_garde_ses_elements()
    {
        _banc.InsererElement(Depot.OngletTemporaire.Id, "precieux");

        Assert.Throws<InvalidOperationException>(() => Depot.SupprimerOnglet(Depot.OngletTemporaire.Id));

        Assert.Single(Depot.ListerOnglets());
        Assert.Equal("1", InsertionsSql.Scalaire(_banc.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Supprimer_un_onglet_inconnu_est_sans_effet()
    {
        Depot.CreerOnglet("Reste");

        Depot.SupprimerOnglet(9999);

        Assert.Equal(["Temporaire", "Reste"], Depot.ListerOnglets().Select(o => o.Nom));
    }

    [Fact]
    public void Un_onglet_cree_apres_une_suppression_passe_quand_meme_en_dernier()
    {
        var parNom = CreerAbcd();
        Depot.SupprimerOnglet(parNom["B"].Id);

        var cree = Depot.CreerOnglet("E");

        // La suppression laisse un trou dans les ordres (1, 3, 4) : le nouvel onglet prend l'ordre après le plus grand.
        Assert.Equal(["Temporaire", "A", "C", "D", "E"], Depot.ListerOnglets().Select(o => o.Nom));
        Assert.True(cree.Ordre > parNom["D"].Ordre);
    }

    [Fact]
    public void Modifie_est_levee_apres_chaque_ecriture_reussie_et_jamais_apres_un_echec()
    {
        var appels = 0;
        Depot.Modifie += () => Interlocked.Increment(ref appels);

        var onglet = Depot.CreerOnglet("A");
        Assert.Equal(1, appels);
        Depot.RenommerOnglet(onglet.Id, "B");
        Assert.Equal(2, appels);
        Depot.DeplacerOnglet(onglet.Id, 1);
        Assert.Equal(3, appels);
        Depot.SupprimerOnglet(onglet.Id);
        Assert.Equal(4, appels);

        // Échecs et suppression d'un onglet absent : rien n'a été écrit, rien n'est signalé.
        Assert.Throws<ArgumentException>(() => Depot.CreerOnglet(" "));
        Assert.Throws<InvalidOperationException>(() => Depot.RenommerOnglet(Depot.OngletTemporaire.Id, "x"));
        Assert.Throws<InvalidOperationException>(() => Depot.SupprimerOnglet(Depot.OngletTemporaire.Id));
        Depot.SupprimerOnglet(9999);
        Assert.Equal(4, appels);
    }

    [Fact]
    public void Un_abonne_qui_leve_une_exception_ne_fait_pas_echouer_l_ecriture_ni_les_autres_abonnes()
    {
        var suivantAppele = false;
        Depot.Modifie += () => throw new InvalidOperationException("abonné défaillant");
        Depot.Modifie += () => suivantAppele = true;

        var cree = Depot.CreerOnglet("Cree quand meme");

        Assert.Contains(cree, Depot.ListerOnglets());
        Assert.True(suivantAppele);
    }

    [Fact]
    public void Un_abonne_peut_relire_le_depot_pendant_l_evenement()
    {
        IReadOnlyList<Onglet>? vus = null;
        Depot.Modifie += () => vus = Depot.ListerOnglets();

        Depot.CreerOnglet("Visible");

        // L'événement est levé après la libération du verrou : un abonné peut rappeler le dépôt sans blocage.
        Assert.Equal(["Temporaire", "Visible"], vus!.Select(o => o.Nom));
    }

    [Fact]
    public void Des_creations_simultanees_donnent_des_ordres_uniques_et_consecutifs()
    {
        Parallel.For(0, 40, i => Depot.CreerOnglet("Onglet " + i.ToString(CultureInfo.InvariantCulture)));

        var onglets = Depot.ListerOnglets();

        Assert.Equal(41, onglets.Count);
        Assert.Equal(Enumerable.Range(0, 41), onglets.Select(o => o.Ordre));
    }

    // Quatre onglets A, B, C, D créés dans cet ordre, indexés par leur nom.
    private Dictionary<string, Onglet> CreerAbcd() =>
        new[] { "A", "B", "C", "D" }.ToDictionary(nom => nom, nom => Depot.CreerOnglet(nom));

    private string CreerPng(string nom)
    {
        Directory.CreateDirectory(_banc.Dossiers.Images);
        File.WriteAllBytes(Path.Combine(_banc.Dossiers.Images, nom), [1, 2, 3]);
        return nom;
    }
}
