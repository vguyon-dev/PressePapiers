using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>Migrations du schéma de la base d'une version à la suivante, chacune dans une seule transaction.</summary>
internal static class MigrationSchema
{
    /// <summary>
    /// Version 1 vers 2 : la table <c>element</c> est reconstruite avec les petites colonnes en tête et trois colonnes
    /// calculées (extrait, indicateur de mise en forme, nombre de fichiers), les lignes sont recopiées, puis l'index plein
    /// texte est reconstruit. Tout ou rien : en cas d'échec, la base reste en version 1.
    /// </summary>
    public static void DeVersion1(SqliteConnection connexion)
    {
        using var transaction = connexion.BeginTransaction();

        Executer(connexion, transaction, $"""
            CREATE TABLE element_v2 (
            {SchemaBase.ColonnesElement}
            )
            """);
        Executer(connexion, transaction, $"""
            INSERT INTO element_v2 (id, onglet_id, nom, type, application, date_copie, date_usage, taille, empreinte,
                                    image_fichier, a_mise_en_forme, nombre_fichiers, extrait, miniature,
                                    texte, texte_recherche, rtf, html)
            SELECT id, onglet_id, nom, type, application, date_copie, date_usage, taille, empreinte,
                   image_fichier,
                   rtf IS NOT NULL OR html IS NOT NULL,
                   CASE WHEN type = 4 AND rtrim(texte, char(10)) <> ''
                        THEN length(rtrim(texte, char(10))) - length(replace(rtrim(texte, char(10)), char(10), '')) + 1
                        ELSE 0 END,
                   substr(texte, 1, {ElementResume.LongueurMaxExtrait}),
                   miniature, texte, texte_recherche, rtf, html
            FROM element
            ORDER BY id
            """);

        // Les index et déclencheurs de l'ancienne table partent avec elle ; Complements les recrée sur la nouvelle.
        // Le nombre de chemins compte les lignes non vides : retours à la ligne internes + 1, comme Element.Fichiers.
        Executer(connexion, transaction, "DROP TABLE element");
        Executer(connexion, transaction, "ALTER TABLE element_v2 RENAME TO element");
        Executer(connexion, transaction, SchemaBase.Complements);
        Executer(connexion, transaction, "INSERT INTO element_fts(element_fts) VALUES ('rebuild')");
        Executer(connexion, transaction,
                 $"UPDATE meta SET valeur = '{BaseHistorique.VersionSchema}' WHERE cle = '{BaseHistorique.CleVersionSchema}'");

        transaction.Commit();
    }

    private static void Executer(SqliteConnection connexion, SqliteTransaction transaction, string sql)
    {
        using var commande = connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = sql;
        commande.ExecuteNonQuery();
    }
}
