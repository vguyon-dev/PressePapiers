namespace PressePapiers.Coeur.Stockage;

/// <summary>Schéma SQL de la base (spec, section 5), écrit pour pouvoir être rejoué sans effet sur une base existante.</summary>
internal static class SchemaBase
{
    /// <summary>Instructions de création : tables, index plein texte et déclencheurs de synchronisation.</summary>
    public const string Creation = """
        CREATE TABLE IF NOT EXISTS onglet (
          id              INTEGER PRIMARY KEY,
          nom             TEXT NOT NULL,
          ordre           INTEGER NOT NULL,
          est_temporaire  INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS element (
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

        CREATE VIRTUAL TABLE IF NOT EXISTS element_fts USING fts5(
          texte_recherche, content='element', content_rowid='id', tokenize='trigram'
        );

        CREATE TRIGGER IF NOT EXISTS element_ai AFTER INSERT ON element BEGIN
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;

        CREATE TRIGGER IF NOT EXISTS element_ad AFTER DELETE ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
        END;

        CREATE TRIGGER IF NOT EXISTS element_au AFTER UPDATE ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;

        CREATE TABLE IF NOT EXISTS meta (cle TEXT PRIMARY KEY, valeur TEXT);
        """;
}
