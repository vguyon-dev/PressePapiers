using Microsoft.Data.Sqlite;

namespace PressePapiers.Tests.Outils;

/// <summary>
/// Fabrique une base telle que la version 1 de l'application la créait (colonnes <c>texte</c> et <c>texte_recherche</c>
/// avant les petites colonnes, pas de colonne <c>extrait</c>), pour éprouver la migration vers le schéma 2.
/// </summary>
public static class BaseVersion1
{
    /// <summary>Texte de plus de 500 caractères, dont un emoji à cheval sur la limite de l'extrait.</summary>
    public static readonly string TexteLong = new string('a', 499) + char.ConvertFromUtf32(0x1F600) + new string('b', 300);

    private const string Schema = """
        CREATE TABLE onglet (
          id              INTEGER PRIMARY KEY,
          nom             TEXT NOT NULL,
          ordre           INTEGER NOT NULL,
          est_temporaire  INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE element (
          id              INTEGER PRIMARY KEY,
          onglet_id       INTEGER NOT NULL REFERENCES onglet(id),
          nom             TEXT,
          type            INTEGER NOT NULL,
          texte           TEXT,
          texte_recherche TEXT NOT NULL,
          rtf             BLOB,
          html            BLOB,
          image_fichier   TEXT,
          miniature       BLOB,
          empreinte       BLOB NOT NULL,
          application     TEXT,
          date_copie      TEXT NOT NULL,
          date_usage      TEXT NOT NULL,
          taille          INTEGER NOT NULL
        );
        CREATE VIRTUAL TABLE element_fts USING fts5(
          texte_recherche, content='element', content_rowid='id', tokenize='trigram'
        );
        CREATE TRIGGER element_ai AFTER INSERT ON element BEGIN
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;
        CREATE TRIGGER element_ad AFTER DELETE ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
        END;
        CREATE TRIGGER element_au AFTER UPDATE OF texte_recherche ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;
        CREATE INDEX element_onglet_empreinte ON element(onglet_id, empreinte);
        CREATE INDEX element_onglet_usage ON element(onglet_id, date_usage DESC, id DESC);
        CREATE TABLE meta (cle TEXT PRIMARY KEY, valeur TEXT);
        INSERT INTO meta (cle, valeur) VALUES ('schema.version', '1');

        INSERT INTO onglet (id, nom, ordre, est_temporaire) VALUES (1, 'Temporaire', 0, 1), (2, 'Projets', 1, 0);
        """;

    /// <summary>
    /// Crée la base du premier schéma avec cinq éléments : un texte long (id 1), un texte mis en forme (id 2),
    /// une image (id 3), une liste de trois fichiers terminée par un retour à la ligne (id 4) et un texte nommé
    /// dans l'onglet Projets (id 5) qui parle de « chat ».
    /// </summary>
    public static void Creer(string chemin)
    {
        var chaine = new SqliteConnectionStringBuilder { DataSource = chemin, Pooling = false }.ToString();
        using var connexion = new SqliteConnection(chaine);
        connexion.Open();
        Executer(connexion, "PRAGMA journal_mode = WAL");
        Executer(connexion, Schema);

        Inserer(connexion, 1, 1, 1, TexteLong, TexteLong, null, null, null, null);
        Inserer(connexion, 2, 1, 2, "Titre riche", "titre riche", [1, 2], [3], null, null);
        Inserer(connexion, 3, 1, 3, null, "capture", null, null, "image.png", [9, 8, 7]);
        Inserer(connexion, 4, 1, 4, "C:\\a.txt\nC:\\b.txt\nC:\\c.txt\n", "c:\\a.txt c:\\b.txt c:\\c.txt", null, null, null, null);
        Inserer(connexion, 5, 2, 1, "le chat dort", "mon nom le chat dort", null, null, null, null, "Mon nom");
    }

    private static void Inserer(SqliteConnection connexion, long id, long onglet, int type, string? texte, string recherche,
                                byte[]? rtf, byte[]? html, string? image, byte[]? miniature, string? nom = null)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO element (id, onglet_id, nom, type, texte, texte_recherche, rtf, html, image_fichier, miniature,
                                 empreinte, application, date_copie, date_usage, taille)
            VALUES ($id, $onglet, $nom, $type, $texte, $recherche, $rtf, $html, $image, $miniature,
                    $empreinte, 'Code', $date, $date, 10)
            """;
        commande.Parameters.AddWithValue("$id", id);
        commande.Parameters.AddWithValue("$onglet", onglet);
        commande.Parameters.AddWithValue("$nom", (object?)nom ?? DBNull.Value);
        commande.Parameters.AddWithValue("$type", type);
        commande.Parameters.AddWithValue("$texte", (object?)texte ?? DBNull.Value);
        commande.Parameters.AddWithValue("$recherche", recherche);
        commande.Parameters.AddWithValue("$rtf", (object?)rtf ?? DBNull.Value);
        commande.Parameters.AddWithValue("$html", (object?)html ?? DBNull.Value);
        commande.Parameters.AddWithValue("$image", (object?)image ?? DBNull.Value);
        commande.Parameters.AddWithValue("$miniature", (object?)miniature ?? DBNull.Value);
        commande.Parameters.AddWithValue("$empreinte", new byte[] { (byte)id });
        commande.Parameters.AddWithValue("$date", new DateTimeOffset(2026, 10, 13, 8, (int)id, 0, TimeSpan.Zero).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        commande.ExecuteNonQuery();
    }

    private static void Executer(SqliteConnection connexion, string sql)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = sql;
        commande.ExecuteNonQuery();
    }
}
