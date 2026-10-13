using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>Transforme une ligne de la table <c>element</c> en <see cref="Element"/>.</summary>
internal static class LecteurElement
{
    /// <summary>Colonnes à sélectionner, dans l'ordre que lit <see cref="Lire"/>.</summary>
    public const string Colonnes = "id, onglet_id, nom, type, texte, rtf, html, image_fichier, miniature, empreinte, application, date_copie, date_usage, taille";

    /// <summary>Lit la ligne courante du lecteur ; les colonnes suivent l'ordre de <see cref="Colonnes"/>.</summary>
    public static Element Lire(SqliteDataReader lecteur) => new()
    {
        Id = lecteur.GetInt64(0),
        OngletId = lecteur.GetInt64(1),
        Nom = Chaine(lecteur, 2),
        Type = (TypeElement)lecteur.GetInt32(3),
        Texte = Chaine(lecteur, 4),
        Rtf = Octets(lecteur, 5),
        Html = Octets(lecteur, 6),
        ImageFichier = Chaine(lecteur, 7),
        Miniature = Octets(lecteur, 8),
        Empreinte = lecteur.GetFieldValue<byte[]>(9),
        Application = Chaine(lecteur, 10),
        DateCopie = Date(lecteur, 11),
        DateUsage = Date(lecteur, 12),
        Taille = lecteur.GetInt64(13),
    };

    /// <summary>
    /// Colonnes du résumé, dans l'ordre que lit <see cref="LireResume"/> : début du texte, miniature des images seulement,
    /// présence de RTF ou de HTML (sans les relire) et nombre de chemins d'une liste de fichiers.
    /// </summary>
    public static readonly string ColonnesResume =
        "id, onglet_id, nom, type, substr(texte, 1, " + ElementResume.LongueurMaxExtrait + "), application, date_copie, date_usage, taille, " +
        "CASE WHEN type = 3 THEN miniature END, (rtf IS NOT NULL OR html IS NOT NULL), " +
        // Chemins non vides : retours à la ligne internes + 1 (ceux de fin sont ignorés), comme le découpage de Element.Fichiers.
        "CASE WHEN type = 4 AND rtrim(texte, char(10)) <> '' " +
        "THEN length(rtrim(texte, char(10))) - length(replace(rtrim(texte, char(10)), char(10), '')) + 1 ELSE 0 END";

    /// <summary>Lit la ligne courante du lecteur ; les colonnes suivent l'ordre de <see cref="ColonnesResume"/>.</summary>
    public static ElementResume LireResume(SqliteDataReader lecteur) => new()
    {
        Id = lecteur.GetInt64(0),
        OngletId = lecteur.GetInt64(1),
        Nom = Chaine(lecteur, 2),
        Type = (TypeElement)lecteur.GetInt32(3),
        Extrait = Chaine(lecteur, 4),
        Application = Chaine(lecteur, 5),
        DateCopie = Date(lecteur, 6),
        DateUsage = Date(lecteur, 7),
        Taille = lecteur.GetInt64(8),
        Miniature = Octets(lecteur, 9),
        AMiseEnForme = lecteur.GetInt32(10) != 0,
        NombreFichiers = lecteur.GetInt32(11),
    };

    private static string? Chaine(SqliteDataReader lecteur, int colonne) =>
        lecteur.IsDBNull(colonne) ? null : lecteur.GetString(colonne);

    private static byte[]? Octets(SqliteDataReader lecteur, int colonne) =>
        lecteur.IsDBNull(colonne) ? null : lecteur.GetFieldValue<byte[]>(colonne);

    // Les dates sont stockées en texte ISO 8601 aller-retour (format "O").
    private static DateTimeOffset Date(SqliteDataReader lecteur, int colonne) =>
        DateTimeOffset.ParseExact(lecteur.GetString(colonne), "O", CultureInfo.InvariantCulture);
}
