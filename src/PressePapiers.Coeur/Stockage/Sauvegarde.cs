using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

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
    /// s'il n'y a pas de base (fichier absent ou vide). Une base illisible lève <see cref="SqliteException"/> :
    /// aucune copie n'est alors créée et la rotation ne supprime rien, pour que les bonnes copies survivent à une base abîmée.
    /// </summary>
    /// <param name="cheminBase">Fichier de la base, ouverte ou non.</param>
    /// <param name="dossierSauvegardes">Dossier des copies, créé au besoin.</param>
    /// <param name="horloge">Source de l'heure du nom.</param>
    /// <param name="nombreGarde">Nombre de copies gardées, au moins 1.</param>
    /// <exception cref="SqliteException">Le fichier n'est pas une base SQLite lisible.</exception>
    public static string? Creer(string cheminBase, string dossierSauvegardes, TimeProvider horloge, int nombreGarde = 3)
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
    /// puis mise en place d'un seul geste ; si quelque chose échoue (base encore ouverte), la base actuelle est laissée telle quelle.
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
        try
        {
            File.Delete(cheminBase + "-wal");
            File.Delete(cheminBase + "-shm");
            File.Move(provisoire, cheminBase, overwrite: true);
        }
        catch
        {
            SupprimerSansBruit(provisoire);
            throw;
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
