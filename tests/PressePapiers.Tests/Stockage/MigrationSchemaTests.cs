using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class MigrationSchemaTests : IDisposable
{
    private static readonly string[] ColonnesAttendues =
    [
        "id", "onglet_id", "nom", "type", "application", "date_copie", "date_usage", "taille", "empreinte", "image_fichier",
        "a_mise_en_forme", "nombre_fichiers", "extrait", "miniature", "texte", "texte_recherche", "rtf", "html",
    ];

    private readonly DossierTemporaire _dossier = new();

    public void Dispose() => _dossier.Dispose();

    private string Chemin => _dossier.Fichier("historique.db");

    private static List<string> Colonnes(SqliteConnection connexion)
    {
        var noms = new List<string>();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "SELECT name FROM pragma_table_info('element') ORDER BY cid";
        using var lecteur = commande.ExecuteReader();
        while (lecteur.Read())
        {
            noms.Add(lecteur.GetString(0));
        }

        return noms;
    }

    [Fact]
    public void Une_base_neuve_a_les_petites_colonnes_d_abord_et_les_gros_contenus_en_dernier()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(Chemin);

        Assert.Equal(ColonnesAttendues, Colonnes(baseHistorique.Connexion));
        Assert.Equal("2", InsertionsSql.Scalaire(baseHistorique.Connexion, "SELECT valeur FROM meta WHERE cle = 'schema.version'"));
    }

    [Fact]
    public void Une_base_de_version_1_est_migree_et_garde_ses_lignes()
    {
        BaseVersion1.Creer(Chemin);

        using var baseHistorique = BaseHistorique.Ouvrir(Chemin);
        var connexion = baseHistorique.Connexion;

        Assert.Equal(ColonnesAttendues, Colonnes(connexion));
        Assert.Equal("2", InsertionsSql.Scalaire(connexion, "SELECT valeur FROM meta WHERE cle = 'schema.version'"));
        Assert.Equal("5", InsertionsSql.Scalaire(connexion, "SELECT COUNT(*) FROM element"));
        Assert.Equal("2", InsertionsSql.Scalaire(connexion, "SELECT COUNT(*) FROM onglet"));
        Assert.Equal(BaseVersion1.TexteLong, InsertionsSql.Scalaire(connexion, "SELECT texte FROM element WHERE id = 1"));
        Assert.Equal("Mon nom", InsertionsSql.Scalaire(connexion, "SELECT nom FROM element WHERE id = 5"));
        Assert.Equal("image.png", InsertionsSql.Scalaire(connexion, "SELECT image_fichier FROM element WHERE id = 3"));
        Assert.Equal("ok", InsertionsSql.Scalaire(connexion, "PRAGMA integrity_check"));
        Assert.Equal("0", InsertionsSql.Scalaire(connexion, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public void La_migration_calcule_l_extrait_l_indicateur_de_mise_en_forme_et_le_nombre_de_fichiers()
    {
        BaseVersion1.Creer(Chemin);
        using var baseHistorique = BaseHistorique.Ouvrir(Chemin);
        var depot = new DepotHistorique(baseHistorique, new DossierDonnees(_dossier.Fichier("donnees")), TimeProvider.System, JournalNul.Instance);

        var resumes = depot.ListerResumes(depot.OngletTemporaire.Id).ToDictionary(r => r.Id);

        // Texte long : 500 caractères Unicode, l'emoji compris, pas un de plus (comme à l'écriture par le dépôt).
        Assert.Equal(new string('a', 499) + char.ConvertFromUtf32(0x1F600), resumes[1].Extrait);
        Assert.False(resumes[1].AMiseEnForme);
        Assert.Equal("Titre riche", resumes[2].Extrait);
        Assert.True(resumes[2].AMiseEnForme);
        Assert.Equal([9, 8, 7], resumes[3].Miniature);
        Assert.Null(resumes[3].Extrait);
        Assert.Equal(3, resumes[4].NombreFichiers);
        Assert.Equal("C:\\a.txt\nC:\\b.txt\nC:\\c.txt\n", resumes[4].Extrait);
        Assert.Equal(0, resumes[1].NombreFichiers);
        Assert.Equal(depot.ListerElements(depot.OngletTemporaire.Id).Select(e => e.Id), resumes.Values.OrderByDescending(r => r.DateUsage).Select(r => r.Id));
    }

    [Fact]
    public void Apres_la_migration_l_index_plein_texte_et_ses_declencheurs_fonctionnent()
    {
        BaseVersion1.Creer(Chemin);
        using var baseHistorique = BaseHistorique.Ouvrir(Chemin);
        var connexion = baseHistorique.Connexion;

        // L'index reconstruit retrouve les anciennes lignes...
        Assert.Equal([5L], InsertionsSql.Trouver(connexion, "\"chat\""));
        Assert.Equal([1L], InsertionsSql.Trouver(connexion, "\"bbb\""));

        // ...et les déclencheurs recréés le tiennent à jour : insertion, mise à jour, suppression.
        var nouveau = InsertionsSql.InsererElementTexte(connexion, 1, "souris grise");
        Assert.Equal([nouveau], InsertionsSql.Trouver(connexion, "\"souris\""));
        using (var maj = connexion.CreateCommand())
        {
            maj.CommandText = "UPDATE element SET texte_recherche = 'renard roux' WHERE id = 5";
            maj.ExecuteNonQuery();
        }

        Assert.Empty(InsertionsSql.Trouver(connexion, "\"chat\""));
        Assert.Equal([5L], InsertionsSql.Trouver(connexion, "\"renard\""));
        using (var suppression = connexion.CreateCommand())
        {
            suppression.CommandText = "DELETE FROM element WHERE id = $id";
            suppression.Parameters.AddWithValue("$id", nouveau);
            suppression.ExecuteNonQuery();
        }

        Assert.Empty(InsertionsSql.Trouver(connexion, "\"souris\""));
        Assert.Equal("2", InsertionsSql.Scalaire(connexion, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name LIKE 'element_onglet_%'"));
    }

    [Fact]
    public void Rouvrir_une_base_migree_ne_la_migre_pas_une_seconde_fois()
    {
        BaseVersion1.Creer(Chemin);
        using (BaseHistorique.Ouvrir(Chemin))
        {
        }

        using var seconde = BaseHistorique.Ouvrir(Chemin);

        Assert.Equal(ColonnesAttendues, Colonnes(seconde.Connexion));
        Assert.Equal("5", InsertionsSql.Scalaire(seconde.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Une_migration_qui_echoue_laisse_la_base_en_version_1_intacte()
    {
        BaseVersion1.Creer(Chemin);
        var chaine = new SqliteConnectionStringBuilder { DataSource = Chemin, Pooling = false }.ToString();
        using (var preparation = new SqliteConnection(chaine))
        {
            preparation.Open();
            using var commande = preparation.CreateCommand();
            // Le nom provisoire de la migration est déjà pris : sa première instruction échoue.
            commande.CommandText = "CREATE TABLE element_v2 (x INTEGER)";
            commande.ExecuteNonQuery();
        }

        Assert.Throws<SqliteException>(() => BaseHistorique.Ouvrir(Chemin));

        using var controle = new SqliteConnection(chaine);
        controle.Open();
        Assert.Equal("1", InsertionsSql.Scalaire(controle, "SELECT valeur FROM meta WHERE cle = 'schema.version'"));
        Assert.Equal("5", InsertionsSql.Scalaire(controle, "SELECT COUNT(*) FROM element"));
        Assert.Equal("texte", InsertionsSql.Scalaire(controle, "SELECT name FROM pragma_table_info('element') WHERE cid = 4"));
    }

    [Fact]
    public void Une_base_d_une_version_superieure_est_refusee_sans_etre_modifiee()
    {
        using (var premiere = BaseHistorique.Ouvrir(Chemin))
        {
            using var commande = premiere.Connexion.CreateCommand();
            commande.CommandText = "UPDATE meta SET valeur = '3' WHERE cle = 'schema.version'";
            commande.ExecuteNonQuery();
        }

        var exception = Assert.Throws<BaseIncompatibleException>(() => BaseHistorique.Ouvrir(Chemin));

        Assert.Contains("plus récente", exception.Message);
        var chaine = new SqliteConnectionStringBuilder { DataSource = Chemin, Pooling = false }.ToString();
        using var controle = new SqliteConnection(chaine);
        controle.Open();
        Assert.Equal("3", InsertionsSql.Scalaire(controle, "SELECT valeur FROM meta WHERE cle = 'schema.version'"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2.5")]
    public void Un_numero_de_schema_illisible_est_refuse(string valeur)
    {
        using (var premiere = BaseHistorique.Ouvrir(Chemin))
        {
            using var commande = premiere.Connexion.CreateCommand();
            commande.CommandText = "UPDATE meta SET valeur = $valeur WHERE cle = 'schema.version'";
            commande.Parameters.AddWithValue("$valeur", valeur);
            commande.ExecuteNonQuery();
        }

        Assert.Throws<BaseIncompatibleException>(() => BaseHistorique.Ouvrir(Chemin));
    }

}
