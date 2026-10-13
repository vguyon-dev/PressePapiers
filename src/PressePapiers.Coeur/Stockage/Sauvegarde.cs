using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Copies de sécurité de la base, par l'API de sauvegarde de SQLite : le fichier <c>-wal</c> fait partie de l'état de la base,
/// donc le fichier n'est jamais copié à la main.
/// </summary>
public static class Sauvegarde
{
    // Seules les copies que cette classe crée sont comptées et supprimées par la rotation : rien d'autre dans le dossier.
    private static readonly Regex NomDeCopie = new(@"^historique-\d{8}-\d{6}\.db$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Copie la base dans <paramref name="dossierSauvegardes"/> (<c>historique-AAAAMMJJ-HHmmss.db</c>, heure UTC) puis garde les
    /// <paramref name="nombreGarde"/> plus récentes, celle qui vient d'être créée comprise. Renvoie le chemin créé, ou null
    /// s'il n'y a pas de base (fichier absent ou vide) ou si la copie échoue au contrôle <c>PRAGMA quick_check</c> : elle est
    /// alors jetée, notée au journal, et la rotation ne supprime rien, pour que les anciennes copies saines survivent.
    /// Une base illisible lève <see cref="SqliteException"/> : aucune copie n'est créée et la rotation ne supprime rien non plus.
    /// </summary>
    /// <param name="cheminBase">Fichier de la base, ouverte ou non.</param>
    /// <param name="dossierSauvegardes">Dossier des copies, créé au besoin.</param>
    /// <param name="horloge">Source de l'heure du nom.</param>
    /// <param name="nombreGarde">Nombre de copies gardées, au moins 1.</param>
    /// <param name="journal">Reçoit l'avertissement d'une copie jetée ; aucun journal par défaut.</param>
    /// <exception cref="SqliteException">Le fichier n'est pas une base SQLite lisible.</exception>
    public static string? Creer(string cheminBase, string dossierSauvegardes, TimeProvider horloge, int nombreGarde = 3,
                                IJournal? journal = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminBase);
        ArgumentException.ThrowIfNullOrWhiteSpace(dossierSauvegardes);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentOutOfRangeException.ThrowIfLessThan(nombreGarde, 1);

        var fichier = new FileInfo(cheminBase);
        if (!fichier.Exists || fichier.Length == 0)
        {
            return null;
        }

        Directory.CreateDirectory(dossierSauvegardes);
        var nom = $"historique-{horloge.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.db";
        var chemin = Path.Combine(dossierSauvegardes, nom);

        // Écrite sous un nom provisoire : une copie qui échoue ne détruit pas une bonne copie de la même seconde.
        var provisoire = chemin + ".partiel";
        try
        {
            Copier(cheminBase, provisoire);
            var anomalie = ControlerCopie(provisoire);
            if (anomalie is not null)
            {
                // Une copie abîmée ne doit ni prendre la place d'une bonne ni déclencher la suppression des anciennes.
                SupprimerSansBruit(provisoire);
                (journal ?? JournalNul.Instance).Avertissement(
                    $"Copie de sécurité jetée : le contrôle d'intégrité de la copie a échoué ({anomalie}). Les copies précédentes sont gardées.");
                return null;
            }

            File.Move(provisoire, chemin, overwrite: true);
        }
        catch
        {
            SupprimerSansBruit(provisoire);
            throw;
        }

        AppliquerRotation(dossierSauvegardes, nom, nombreGarde);
        return chemin;
    }

    /// <summary>Chemin de la copie la plus récente (par son nom), ou null s'il n'y en a pas.</summary>
    public static string? PlusRecente(string dossierSauvegardes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dossierSauvegardes);

        var nom = NomsDeCopies(dossierSauvegardes).FirstOrDefault();
        return nom is null ? null : Path.Combine(dossierSauvegardes, nom);
    }

    /// <summary>
    /// Remplace la base par une copie. La base doit être fermée. Les fichiers <c>-wal</c> et <c>-shm</c> de l'ancienne base
    /// sont supprimés : appliqués à la base restaurée, ils la corrompraient. La copie est d'abord posée à côté de la base,
    /// les deux fichiers annexes sont renommés (ce qui échoue tout de suite si la base est encore ouverte), la copie est mise
    /// en place d'un seul geste, puis les fichiers renommés sont supprimés. Si quelque chose échoue, la base actuelle et ses
    /// fichiers annexes sont laissés tels quels.
    /// </summary>
    /// <exception cref="FileNotFoundException">La copie n'existe pas.</exception>
    public static void Restaurer(string cheminSauvegarde, string cheminBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminSauvegarde);
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminBase);

        if (!File.Exists(cheminSauvegarde))
        {
            throw new FileNotFoundException("La copie de sécurité à restaurer n'existe pas.", cheminSauvegarde);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cheminBase))!);
        var provisoire = cheminBase + ".restauration";
        File.Copy(cheminSauvegarde, provisoire, overwrite: true);
        var ecartes = new List<(string Origine, string Ecarte)>();
        try
        {
            foreach (var suffixe in new[] { "-wal", "-shm" })
            {
                var origine = cheminBase + suffixe;
                if (File.Exists(origine))
                {
                    var ecarte = origine + ".avant-restauration";
                    File.Move(origine, ecarte, overwrite: true);
                    ecartes.Add((origine, ecarte));
                }
            }

            File.Move(provisoire, cheminBase, overwrite: true);
        }
        catch
        {
            SupprimerSansBruit(provisoire);
            foreach (var (origine, ecarte) in ecartes)
            {
                RemettreEnPlace(ecarte, origine);
            }

            throw;
        }

        foreach (var (_, ecarte) in ecartes)
        {
            SupprimerSansBruit(ecarte);
        }
    }

    // Contrôle rapide d'intégrité de la copie (en lecture écriture : fermée en dernier, la connexion retire les fichiers -wal et -shm
    // de la copie, qu'une connexion en lecture seule laisserait à côté). Renvoie null si tout va bien,
    // sinon un résumé de l'anomalie (premier message de SQLite, ou l'exception qui a empêché le contrôle).
    private static string? ControlerCopie(string chemin)
    {
        var chaine = new SqliteConnectionStringBuilder { DataSource = chemin, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString();
        try
        {
            using var connexion = new SqliteConnection(chaine);
            connexion.Open();
            using var commande = connexion.CreateCommand();
            commande.CommandText = "PRAGMA quick_check";
            using var lecteur = commande.ExecuteReader();
            var premier = lecteur.Read() ? lecteur.GetString(0) : "aucun résultat";
            return premier == "ok" ? null : premier;
        }
        catch (SqliteException exception)
        {
            return exception.Message;
        }
    }

    // Remet un fichier annexe à sa place après un échec ; si même cela échoue, le fichier reste sous son nom d'attente.
    private static void RemettreEnPlace(string ecarte, string origine)
    {
        try
        {
            File.Move(ecarte, origine, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Source en lecture seule : la sauvegarde ne peut rien écrire dans la base qu'elle copie.
    private static void Copier(string cheminBase, string destination)
    {
        var chaineSource = new SqliteConnectionStringBuilder
        {
            DataSource = cheminBase,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        var chaineDestination = new SqliteConnectionStringBuilder
        {
            DataSource = destination,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        using var source = new SqliteConnection(chaineSource);
        using var cible = new SqliteConnection(chaineDestination);
        source.Open();
        cible.Open();
        source.BackupDatabase(cible);
    }

    // Garde la copie qui vient d'être créée et les (nombreGarde - 1) plus récentes parmi les autres. Si l'horloge a reculé,
    // la nouvelle copie peut être la plus ancienne par son nom : elle reste quand même.
    private static void AppliquerRotation(string dossierSauvegardes, string nomCree, int nombreGarde)
    {
        var aSupprimer = NomsDeCopies(dossierSauvegardes).Where(nom => nom != nomCree).Skip(nombreGarde - 1);
        foreach (var nom in aSupprimer.ToList())
        {
            SupprimerSansBruit(Path.Combine(dossierSauvegardes, nom));
        }
    }

    // Noms des copies horodatées, de la plus récente à la plus ancienne (l'horodatage en tête du nom se trie comme du texte).
    private static IEnumerable<string> NomsDeCopies(string dossierSauvegardes) =>
        Directory.Exists(dossierSauvegardes)
            ? Directory.EnumerateFiles(dossierSauvegardes)
                .Select(chemin => Path.GetFileName(chemin))
                .Where(nom => NomDeCopie.IsMatch(nom))
                .OrderDescending(StringComparer.Ordinal)
            : [];

    // Une ancienne copie qu'on ne peut pas supprimer (ouverte ailleurs) reste simplement là jusqu'à la prochaine rotation.
    private static void SupprimerSansBruit(string chemin)
    {
        try
        {
            File.Delete(chemin);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
