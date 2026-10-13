using System.Globalization;
using Microsoft.Data.Sqlite;

namespace PressePapiers.Tests.Outils;

/// <summary>Insertions SQL directes pour les tests qui ne passent pas encore par le dépôt.</summary>
public static class InsertionsSql
{
    /// <summary>Insère un onglet et renvoie son identifiant.</summary>
    public static long InsererOnglet(SqliteConnection connexion, string nom, int ordre, bool temporaire = false)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = "INSERT INTO onglet (nom, ordre, est_temporaire) VALUES ($nom, $ordre, $temp); SELECT last_insert_rowid();";
        commande.Parameters.AddWithValue("$nom", nom);
        commande.Parameters.AddWithValue("$ordre", ordre);
        commande.Parameters.AddWithValue("$temp", temporaire ? 1 : 0);
        return Convert.ToInt64(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Insère un élément texte minimal et renvoie son identifiant.</summary>
    public static long InsererElementTexte(SqliteConnection connexion, long ongletId, string texteRecherche)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO element (onglet_id, type, texte, texte_recherche, empreinte, date_copie, date_usage, taille)
            VALUES ($onglet, 1, $texte, $recherche, x'01', $date, $date, 10);
            SELECT last_insert_rowid();
            """;
        commande.Parameters.AddWithValue("$onglet", ongletId);
        commande.Parameters.AddWithValue("$texte", texteRecherche);
        commande.Parameters.AddWithValue("$recherche", texteRecherche);
        commande.Parameters.AddWithValue("$date", new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToInt64(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Identifiants des lignes de l'index plein texte qui correspondent à une requête MATCH.</summary>
    public static List<long> Trouver(SqliteConnection connexion, string requeteMatch)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = "SELECT rowid FROM element_fts WHERE element_fts MATCH $requete ORDER BY rowid";
        commande.Parameters.AddWithValue("$requete", requeteMatch);
        using var lecteur = commande.ExecuteReader();
        var ids = new List<long>();
        while (lecteur.Read())
        {
            ids.Add(lecteur.GetInt64(0));
        }

        return ids;
    }

    /// <summary>Valeur unique renvoyée par une requête, en texte.</summary>
    public static string? Scalaire(SqliteConnection connexion, string sql)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = sql;
        return Convert.ToString(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
