using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class OuvertureBaseTests : IDisposable
{
    private const string Abime = "du texte dans le fichier d'historique, pas une base SQLite";

    private static readonly DateTimeOffset Maintenant = new(2026, 10, 13, 8, 15, 30, TimeSpan.Zero);

    private readonly DossierTemporaire _racine = new();
    private readonly FakeTimeProvider _horloge = new(Maintenant);
    private readonly JournalCollecte _journal = new();
    private readonly DialogueEspion _dialogue = new();
    private readonly DossierDonnees _dossiers;

    public OuvertureBaseTests()
    {
        _dossiers = new DossierDonnees(_racine.Fichier("donnees"));
        _dossiers.CreerDossiers();
    }

    public void Dispose() => _racine.Dispose();

    // ---- Démarrage normal ----

    [Fact]
    public void Sans_fichier_de_base_une_base_neuve_est_creee_sans_rien_demander()
    {
        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        Assert.Equal("0", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
        Assert.Empty(_dialogue.Informations);
        Assert.Empty(_dialogue.Questions);
        Assert.Empty(Directory.GetFiles(_dossiers.Sauvegardes));
    }

    [Fact]
    public void Une_base_saine_est_sauvegardee_puis_ouverte_avec_ses_donnees()
    {
        PreparerBase(2);

        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        Assert.Equal("2", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
        Assert.Equal(["historique-20261013-081530.db"], NomsDeFichiers(_dossiers.Sauvegardes));
        Assert.Empty(_dialogue.Informations);
        Assert.Empty(_dialogue.Questions);
    }

    [Fact]
    public void Une_copie_de_securite_impossible_n_empeche_pas_de_demarrer()
    {
        PreparerBase(2);
        // Un fichier à la place du dossier des sauvegardes : la copie ne peut pas être créée.
        Directory.Delete(_dossiers.Sauvegardes);
        File.WriteAllText(_dossiers.Sauvegardes, "pas un dossier");

        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        Assert.Equal("2", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
        Assert.Single(_journal.Avertissements);
        Assert.Empty(_dialogue.Questions);
    }

    // ---- Base illisible ----

    [Fact]
    public void Une_base_illisible_sans_copie_arrete_le_demarrage_en_le_disant()
    {
        File.WriteAllText(_dossiers.Base, Abime);

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        Assert.Equal(Abime, File.ReadAllText(_dossiers.Base));
        Assert.Empty(_dialogue.Questions);
        var message = Assert.Single(_dialogue.Informations);
        Assert.Contains("copie de sécurité", message);
        Assert.Contains(_dossiers.Base, message);
        Assert.NotEmpty(_journal.Erreurs);
    }

    [Fact]
    public void Une_base_illisible_est_restauree_depuis_la_copie_la_plus_recente_quand_l_utilisateur_accepte()
    {
        // Deux copies de contenus différents : choisir la plus ancienne rendrait 1 élément au lieu de 3.
        CreerCopie(elements: 1, quand: Maintenant.AddHours(-2));
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));
        File.WriteAllText(_dossiers.Base, Abime);
        _dialogue.Reponse = true;

        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        Assert.Equal("3", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
        Assert.Single(_dialogue.Questions);
        Assert.Empty(_dialogue.Informations);
        // Les deux bonnes copies survivent à la base abîmée, et celle-ci est gardée à côté pour examen.
        Assert.Equal(
            ["historique-20261013-061530.db", "historique-20261013-071530.db", "illisible-20261013-081530.db"],
            NomsDeFichiers(_dossiers.Sauvegardes));
        Assert.Equal(Abime, File.ReadAllText(Path.Combine(_dossiers.Sauvegardes, "illisible-20261013-081530.db")));
    }

    [Fact]
    public void Le_refus_de_l_utilisateur_laisse_la_base_et_les_copies_en_place()
    {
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));
        File.WriteAllText(_dossiers.Base, Abime);
        _dialogue.Reponse = false;

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        Assert.Single(_dialogue.Questions);
        Assert.Equal(Abime, File.ReadAllText(_dossiers.Base));
        Assert.Equal(["historique-20261013-071530.db"], NomsDeFichiers(_dossiers.Sauvegardes));
    }

    [Fact]
    public void Le_refus_de_l_utilisateur_lui_dit_quoi_faire_avant_l_arret()
    {
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));
        File.WriteAllText(_dossiers.Base, Abime);
        _dialogue.Reponse = false;

        Assert.Null(Ouvrir());

        var message = Assert.Single(_dialogue.Informations);
        Assert.Contains(_dossiers.Base, message);
        Assert.Contains("déplacez", message);
        Assert.Contains("acceptez la restauration", message);
        Assert.Contains(_dossiers.Sauvegardes, message);
    }

    [Fact]
    public void La_restauration_garde_aussi_le_wal_et_le_shm_de_la_base_abimee()
    {
        CreerCopie(elements: 2, quand: Maintenant.AddHours(-1));
        File.WriteAllText(_dossiers.Base, Abime);
        File.WriteAllText(_dossiers.Base + "-wal", "journal de la base abimee");
        File.WriteAllText(_dossiers.Base + "-shm", "memoire de la base abimee");
        _dialogue.Reponse = true;

        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        var ecartee = Path.Combine(_dossiers.Sauvegardes, "illisible-20261013-081530.db");
        Assert.Equal(Abime, File.ReadAllText(ecartee));
        Assert.Equal("journal de la base abimee", File.ReadAllText(ecartee + "-wal"));
        // SQLite réécrit la mémoire partagée en tentant d'ouvrir la base : on vérifie sa présence, pas son contenu.
        Assert.True(File.Exists(ecartee + "-shm"));
        // La base restaurée ne reçoit pas le journal de l'ancienne.
        Assert.Equal("2", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Une_base_creee_par_une_version_plus_recente_arrete_le_demarrage_sans_restauration()
    {
        PreparerBase(1);
        using (var baseHistorique = BaseHistorique.Ouvrir(_dossiers.Base))
        {
            using var commande = baseHistorique.Connexion.CreateCommand();
            commande.CommandText = "UPDATE meta SET valeur = '9' WHERE cle = 'schema.version'";
            commande.ExecuteNonQuery();
        }

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        Assert.Empty(_dialogue.Questions);
        Assert.Contains("plus récente", Assert.Single(_dialogue.Informations));
    }

    [Fact]
    public void Une_copie_elle_meme_abimee_arrete_le_demarrage_en_le_disant()
    {
        File.WriteAllText(Path.Combine(_dossiers.Sauvegardes, "historique-20261013-071530.db"), Abime);
        File.WriteAllText(_dossiers.Base, Abime);
        _dialogue.Reponse = true;

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        var message = Assert.Single(_dialogue.Informations);
        Assert.Contains("restauration", message);
        Assert.NotEmpty(_journal.Erreurs);
    }

    // ---- Erreurs qui ne justifient pas d'écraser la base ----

    [Fact]
    public void Un_probleme_d_acces_a_la_base_ne_propose_jamais_de_restauration()
    {
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));
        // Un dossier à la place du fichier : SQLite répond « impossible d'ouvrir », pas « base corrompue ».
        Directory.CreateDirectory(_dossiers.Base);

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        Assert.Empty(_dialogue.Questions);
        Assert.Single(_dialogue.Informations);
        Assert.True(Directory.Exists(_dossiers.Base));
    }

    [Fact]
    public void Une_version_de_SQLite_trop_ancienne_est_expliquee_sans_proposer_de_restauration()
    {
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));

        var ouverte = OuvertureBase.Ouvrir(_dossiers, _horloge, _journal, _dialogue,
            _ => throw new BaseIncompatibleException("SQLite 3.30 est trop ancienne."));

        Assert.Null(ouverte);
        Assert.Empty(_dialogue.Questions);
        var message = Assert.Single(_dialogue.Informations);
        Assert.Contains("SQLite 3.30 est trop ancienne.", message);
        Assert.NotEmpty(_journal.Erreurs);
    }

    // ---- Outils ----

    private BaseHistorique? Ouvrir() => OuvertureBase.Ouvrir(_dossiers, _horloge, _journal, _dialogue);

    // Crée une base de test avec ce nombre d'éléments, puis la ferme.
    private void PreparerBase(int elements)
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossiers.Base);
        var depot = new DepotHistorique(baseHistorique, _dossiers, _horloge, JournalNul.Instance);
        for (var i = 0; i < elements; i++)
        {
            depot.AjouterCopie(ContenusDeTest.Texte("element " + i));
        }
    }

    // Pose une copie de sécurité datée de « quand » (nom en UTC), puis retire la base d'origine.
    private void CreerCopie(int elements, DateTimeOffset quand)
    {
        PreparerBase(elements);
        Sauvegarde.Creer(_dossiers.Base, _dossiers.Sauvegardes, new FakeTimeProvider(quand));
        File.Delete(_dossiers.Base);
    }

    private static string[] NomsDeFichiers(string dossier) =>
        [.. Directory.GetFiles(dossier).Select(chemin => Path.GetFileName(chemin)).Order()];

    // Dialogue de test : note ce qui est dit à l'utilisateur et répond à la question par la valeur réglée.
    private sealed class DialogueEspion : IDialogueDemarrage
    {
        public bool Reponse { get; set; }

        public List<string> Informations { get; } = [];

        public List<string> Questions { get; } = [];

        public void Informer(string message) => Informations.Add(message);

        public bool Confirmer(string question)
        {
            Questions.Add(question);
            return Reponse;
        }
    }
}
