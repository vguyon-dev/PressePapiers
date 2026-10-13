using Microsoft.Data.Sqlite;

namespace PressePapiers.Tests.Outils;

/// <summary>Dossier jetable, propre à un test, supprimé avec tout son contenu à la fin.</summary>
public sealed class DossierTemporaire : IDisposable
{
    /// <summary>Crée un dossier neuf sous le dossier temporaire du système.</summary>
    public DossierTemporaire()
    {
        Chemin = Path.Combine(Path.GetTempPath(), "pp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Chemin);
    }

    /// <summary>Chemin complet du dossier.</summary>
    public string Chemin { get; }

    /// <summary>Chemin d'un fichier ou d'un sous-dossier du dossier jetable (rien n'est créé).</summary>
    public string Fichier(string nom) => Path.Combine(Chemin, nom);

    /// <inheritdoc />
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Chemin, recursive: true);
        }
        catch (IOException)
        {
            // Un fichier encore verrouillé ne doit pas faire échouer un test déjà jugé.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
