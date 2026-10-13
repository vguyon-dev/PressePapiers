using Microsoft.Data.Sqlite;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// La base SQLite de l'historique : une connexion ouverte, le schéma en place, le mode WAL et les clés étrangères actifs.
/// La connexion n'est pas sûre entre fils : <see cref="DepotHistorique"/> la protège par un verrou.
/// </summary>
public sealed class BaseHistorique : IDisposable
{
    /// <summary>Clé de <c>meta</c> qui porte la version du schéma, pour les migrations futures.</summary>
    internal const string CleVersionSchema = "schema.version";

    /// <summary>Numéro du schéma créé par cette version de l'application (2 : petites colonnes d'abord, extrait calculé).</summary>
    internal const int NumeroVersionSchema = 2;

    /// <summary>Version du schéma telle qu'elle est écrite dans <c>meta</c>.</summary>
    internal static readonly string VersionSchema = NumeroVersionSchema.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Première version de SQLite qui connaît le tokenizer <c>trigram</c>.</summary>
    internal static readonly Version VersionMinimale = new(3, 34, 0);

    private BaseHistorique(SqliteConnection connexion, string versionSqlite)
    {
        Connexion = connexion;
        VersionSqlite = versionSqlite;
    }

    /// <summary>Connexion ouverte sur la base.</summary>
    public SqliteConnection Connexion { get; }

    /// <summary>Valeur de <c>sqlite_version()</c> de la bibliothèque embarquée.</summary>
    public string VersionSqlite { get; }

    /// <summary>
    /// Vrai si cette base vient d'être restaurée depuis une copie de sécurité parce que la précédente était illisible
    /// (voir <see cref="OuvertureBase"/>). Les PNG de la base abîmée, gardée à part, restent alors récupérables : tant
    /// qu'ils n'ont pas été retrouvés, aucun ménage ne doit les supprimer.
    /// </summary>
    public bool RestaureeDepuisSauvegarde { get; internal set; }

    /// <summary>
    /// Ouvre ou crée la base : schéma de la spec (section 5), <c>journal_mode=WAL</c>, <c>foreign_keys=ON</c>.
    /// Une base de version 1 est migrée vers la version 2 (une transaction, tout ou rien) ; une base d'une version plus
    /// récente que celle que cette application connaît est refusée, sans y toucher.
    /// </summary>
    /// <param name="cheminFichier">Chemin du fichier de base ; le dossier doit exister.</param>
    /// <exception cref="BaseIncompatibleException">SQLite inférieure à 3.34.0, FTS5 absent, ou base créée par une version plus récente.</exception>
    /// <exception cref="SqliteException">Le fichier n'est pas une base SQLite, ou n'est pas lisible.</exception>
    public static BaseHistorique Ouvrir(string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminFichier);

        // Pas de réserve de connexions : fermer la base libère le fichier tout de suite (sauvegarde, restauration, tests).
        var chaine = new SqliteConnectionStringBuilder
        {
            DataSource = cheminFichier,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        var connexion = new SqliteConnection(chaine);
        try
        {
            connexion.Open();
            var version = Scalaire(connexion, "SELECT sqlite_version()");
            ControlerVersion(version);
            ControlerFts5(connexion);

            Executer(connexion, "PRAGMA journal_mode = WAL");
            Executer(connexion, "PRAGMA foreign_keys = ON");
            PreparerSchema(connexion);
            return new BaseHistorique(connexion, version);
        }
        catch
        {
            connexion.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Connexion.Dispose();

    /// <summary>Refuse une version de SQLite qui ne connaît pas le tokenizer <c>trigram</c>.</summary>
    internal static void ControlerVersion(string version)
    {
        if (!Version.TryParse(version, out var lue) || lue < VersionMinimale)
        {
            throw new BaseIncompatibleException(
                $"La version de SQLite embarquée ({version}) est trop ancienne : la version {VersionMinimale} ou plus est nécessaire pour la recherche.");
        }
    }

    // Une table temporaire jetable prouve à la fois la présence de FTS5 et celle du tokenizer trigram.
    private static void ControlerFts5(SqliteConnection connexion)
    {
        try
        {
            Executer(connexion, "CREATE VIRTUAL TABLE temp.essai_fts5 USING fts5(x, tokenize='trigram')");
            Executer(connexion, "DROP TABLE temp.essai_fts5");
        }
        catch (SqliteException exception)
        {
            throw new BaseIncompatibleException(
                $"La SQLite embarquée ne gère pas l'index de recherche FTS5 avec trigrammes : {exception.Message}");
        }
    }

    // Crée le schéma d'une base neuve, migre une base ancienne, refuse une base plus récente que l'application.
    private static void PreparerSchema(SqliteConnection connexion)
    {
        var version = LireVersionSchema(connexion);
        if (version > NumeroVersionSchema)
        {
            throw new BaseIncompatibleException(
                $"La base de l'historique a été créée par une version plus récente de PressePapiers (schéma {version}, "
                + $"alors que cette version ne connaît que le schéma {NumeroVersionSchema}). "
                + "Installez la dernière version de PressePapiers : la base n'a pas été modifiée.");
        }

        if (version == 1)
        {
            MigrationSchema.DeVersion1(connexion);
        }

        CreerSchema(connexion);
    }

    // Null pour une base neuve. Une base sans clé de version mais avec une table element est une base du premier schéma.
    private static int? LireVersionSchema(SqliteConnection connexion)
    {
        if (Scalaire(connexion, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'meta'") != "0")
        {
            var valeur = Scalaire(connexion, $"SELECT valeur FROM meta WHERE cle = '{CleVersionSchema}'");
            if (valeur.Length > 0)
            {
                return int.TryParse(valeur, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var numero) && numero >= 1
                    ? numero
                    : throw new BaseIncompatibleException($"Le numéro de schéma de la base de l'historique est illisible (« {valeur} »).");
            }
        }

        return Scalaire(connexion, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'element'") != "0" ? 1 : null;
    }

    private static void CreerSchema(SqliteConnection connexion)
    {
        using var transaction = connexion.BeginTransaction();
        Executer(connexion, SchemaBase.Creation, transaction);

        using var commande = connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = "INSERT OR IGNORE INTO meta (cle, valeur) VALUES ($cle, $valeur)";
        commande.Parameters.AddWithValue("$cle", CleVersionSchema);
        commande.Parameters.AddWithValue("$valeur", VersionSchema);
        commande.ExecuteNonQuery();

        transaction.Commit();
    }

    private static void Executer(SqliteConnection connexion, string sql, SqliteTransaction? transaction = null)
    {
        using var commande = connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = sql;
        commande.ExecuteNonQuery();
    }

    private static string Scalaire(SqliteConnection connexion, string sql)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = sql;
        return Convert.ToString(commande.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
