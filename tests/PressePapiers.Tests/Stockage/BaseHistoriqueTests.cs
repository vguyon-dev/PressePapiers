using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class BaseHistoriqueTests : IDisposable
{
    private readonly DossierTemporaire _dossier = new();

    public void Dispose() => _dossier.Dispose();

    [Fact]
    public void Ouvrir_cree_le_schema_complet()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));

        var objets = new List<string>();
        using (var commande = baseHistorique.Connexion.CreateCommand())
        {
            commande.CommandText = "SELECT name FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' AND name NOT LIKE 'element_fts_%'";
            using var lecteur = commande.ExecuteReader();
            while (lecteur.Read())
            {
                objets.Add(lecteur.GetString(0));
            }
        }

        foreach (var attendu in new[] { "onglet", "element", "element_fts", "meta", "element_ai", "element_ad", "element_au" })
        {
            Assert.Contains(attendu, objets);
        }

        Assert.Equal("1", InsertionsSql.Scalaire(baseHistorique.Connexion, "SELECT valeur FROM meta WHERE cle = 'schema.version'"));
    }

    [Fact]
    public void Ouvrir_active_le_mode_wal_et_les_cles_etrangeres()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));

        Assert.Equal("wal", InsertionsSql.Scalaire(baseHistorique.Connexion, "PRAGMA journal_mode"));
        Assert.Equal("1", InsertionsSql.Scalaire(baseHistorique.Connexion, "PRAGMA foreign_keys"));
    }

    [Fact]
    public void Une_cle_etrangere_inconnue_est_refusee()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));

        // L'onglet 999 n'existe pas : sans foreign_keys=ON, la ligne serait acceptée.
        Assert.Throws<SqliteException>(() => InsertionsSql.InsererElementTexte(baseHistorique.Connexion, 999, "orphelin"));
    }

    [Fact]
    public void Rouvrir_une_base_existante_garde_les_donnees()
    {
        var chemin = _dossier.Fichier("historique.db");
        using (var premiere = BaseHistorique.Ouvrir(chemin))
        {
            InsertionsSql.InsererOnglet(premiere.Connexion, "Temporaire", 0, temporaire: true);
        }

        using var seconde = BaseHistorique.Ouvrir(chemin);

        Assert.Equal("1", InsertionsSql.Scalaire(seconde.Connexion, "SELECT COUNT(*) FROM onglet"));
        Assert.Equal("1", InsertionsSql.Scalaire(seconde.Connexion, "SELECT valeur FROM meta WHERE cle = 'schema.version'"));
    }

    [Fact]
    public void L_index_trigram_trouve_un_mot_de_trois_lettres_et_suit_les_ecritures()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));
        var connexion = baseHistorique.Connexion;
        var onglet = InsertionsSql.InsererOnglet(connexion, "Temporaire", 0, temporaire: true);

        var id = InsertionsSql.InsererElementTexte(connexion, onglet, "bonjour le monde");

        // Insertion : le mot de trois lettres est trouvé, un mot absent ne l'est pas.
        Assert.Equal([id], InsertionsSql.Trouver(connexion, "\"jou\""));
        Assert.Empty(InsertionsSql.Trouver(connexion, "\"zzz\""));

        // Mise à jour : l'ancien texte disparaît de l'index, le nouveau y entre.
        using (var maj = connexion.CreateCommand())
        {
            maj.CommandText = "UPDATE element SET texte_recherche = 'au revoir' WHERE id = $id";
            maj.Parameters.AddWithValue("$id", id);
            maj.ExecuteNonQuery();
        }

        Assert.Empty(InsertionsSql.Trouver(connexion, "\"jou\""));
        Assert.Equal([id], InsertionsSql.Trouver(connexion, "\"rev\""));

        // Suppression : plus rien dans l'index.
        using (var suppression = connexion.CreateCommand())
        {
            suppression.CommandText = "DELETE FROM element WHERE id = $id";
            suppression.Parameters.AddWithValue("$id", id);
            suppression.ExecuteNonQuery();
        }

        Assert.Empty(InsertionsSql.Trouver(connexion, "\"rev\""));
    }

    [Fact]
    public void La_version_de_sqlite_est_au_moins_3_34_0()
    {
        // Point 8 de la section 16 de la spec : la version réelle du paquet Microsoft.Data.Sqlite.
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));

        TestContext.Current.SendDiagnosticMessage($"Version de SQLite embarquée : {baseHistorique.VersionSqlite}");

        Assert.True(Version.Parse(baseHistorique.VersionSqlite) >= new Version(3, 34, 0));
    }

    [Theory]
    [InlineData("3.34.0")]
    [InlineData("3.45.1")]
    [InlineData("4.0.0")]
    public void ControlerVersion_accepte_une_version_recente(string version) =>
        BaseHistorique.ControlerVersion(version);

    [Theory]
    [InlineData("3.33.9")]
    [InlineData("2.8.17")]
    [InlineData("n'importe quoi")]
    [InlineData("")]
    public void ControlerVersion_refuse_une_version_ancienne_ou_illisible(string version)
    {
        var exception = Assert.Throws<BaseIncompatibleException>(() => BaseHistorique.ControlerVersion(version));

        Assert.Contains("3.34.0", exception.Message);
    }

    [Theory]
    [InlineData("pas une base")]
    [InlineData("Ceci est un fichier texte ordinaire, bien plus long qu'un en-tête SQLite de seize octets.")]
    public void Un_fichier_texte_n_est_pas_ouvert_comme_une_base_et_reste_intact(string contenu)
    {
        var chemin = _dossier.Fichier("texte.db");
        File.WriteAllText(chemin, contenu);

        Assert.Throws<SqliteException>(() => BaseHistorique.Ouvrir(chemin));

        // Le fichier de l'utilisateur n'est ni modifié ni gardé ouvert.
        Assert.Equal(contenu, File.ReadAllText(chemin));
        File.Delete(chemin);
        Assert.False(File.Exists(chemin));
    }

    [Fact]
    public void Un_dossier_absent_leve_une_exception_sqlite()
    {
        var chemin = Path.Combine(_dossier.Chemin, "absent", "historique.db");

        Assert.Throws<SqliteException>(() => BaseHistorique.Ouvrir(chemin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_chemin_vide_est_refuse(string chemin) =>
        Assert.Throws<ArgumentException>(() => BaseHistorique.Ouvrir(chemin));
}
