namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Schéma SQL de la base (spec, section 5), écrit pour pouvoir être rejoué sans effet sur une base existante.
/// Version 2 : les petites colonnes de <c>element</c> viennent d'abord, les gros contenus en dernier. SQLite range une
/// ligne trop grosse pour une page sur des pages de débordement : lire une date ou une taille n'oblige à les parcourir
/// que si ces colonnes sont rangées après le texte.
/// </summary>
internal static class SchemaBase
{
    /// <summary>
    /// Colonnes de <c>element</c>, dans l'ordre physique voulu : métadonnées, indicateurs, extrait et miniature, puis
    /// les gros contenus (<c>texte</c>, <c>texte_recherche</c>, <c>rtf</c>, <c>html</c>). Réservé à ce fichier et à la migration.
    /// </summary>
    internal const string ColonnesElement = """
        id              INTEGER PRIMARY KEY,
        onglet_id       INTEGER NOT NULL REFERENCES onglet(id),
        nom             TEXT,
        type            INTEGER NOT NULL,
        application     TEXT,
        date_copie      TEXT NOT NULL,
        date_usage      TEXT NOT NULL,
        taille          INTEGER NOT NULL,
        empreinte       BLOB NOT NULL,
        image_fichier   TEXT,
        a_mise_en_forme INTEGER NOT NULL DEFAULT 0,
        nombre_fichiers INTEGER NOT NULL DEFAULT 0,
        extrait         TEXT,
        miniature       BLOB,
        texte           TEXT,
        texte_recherche TEXT NOT NULL,
        rtf             BLOB,
        html            BLOB
        """;

    /// <summary>Instructions de création : tables, index plein texte et déclencheurs de synchronisation.</summary>
    public const string Creation = $"""
        CREATE TABLE IF NOT EXISTS onglet (
          id              INTEGER PRIMARY KEY,
          nom             TEXT NOT NULL,
          ordre           INTEGER NOT NULL,
          est_temporaire  INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS element (
        {ColonnesElement}
        );

        {Complements}
        """;

    /// <summary>
    /// Ce qui dépend de la table <c>element</c> et part avec elle quand on la remplace : index plein texte,
    /// déclencheurs, index ordinaires et table <c>meta</c>. Rejoué après la migration.
    /// </summary>
    internal const string Complements = """
        CREATE VIRTUAL TABLE IF NOT EXISTS element_fts USING fts5(
          texte_recherche, content='element', content_rowid='id', tokenize='trigram'
        );

        CREATE TRIGGER IF NOT EXISTS element_ai AFTER INSERT ON element BEGIN
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;

        CREATE TRIGGER IF NOT EXISTS element_ad AFTER DELETE ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
        END;

        CREATE TRIGGER IF NOT EXISTS element_au AFTER UPDATE OF texte_recherche ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;

        CREATE INDEX IF NOT EXISTS element_onglet_empreinte ON element(onglet_id, empreinte);
        CREATE INDEX IF NOT EXISTS element_onglet_usage ON element(onglet_id, date_usage DESC, id DESC);

        CREATE TABLE IF NOT EXISTS meta (cle TEXT PRIMARY KEY, valeur TEXT);
        """;
}
