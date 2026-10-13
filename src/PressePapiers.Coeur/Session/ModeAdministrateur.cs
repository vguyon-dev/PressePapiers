namespace PressePapiers.Coeur.Session;

/// <summary>
/// Reconnaît le mode « lancé en administrateur par une tâche planifiée » que <c>scripts\publier.ps1 -Administrateur</c> installe.
/// Le processus n'a aucun moyen direct de savoir qui l'a lancé, et interroger le Planificateur de tâches coûterait un
/// processus externe ou du COM à chaque démarrage. La règle repose donc sur deux faits lisibles sans rien appeler : le jeton
/// du processus est élevé, et son exécutable est dans un dossier que seuls les administrateurs peuvent modifier (Program Files).
/// Un exécutable élevé mais rangé dans un dossier modifiable par l'utilisateur n'est pas ce mode : le script n'y installe jamais.
/// </summary>
public static class ModeAdministrateur
{
    /// <summary>
    /// Dit si l'exécutable en cours est celui de l'installation administrateur : processus élevé, exécutable sous l'un des
    /// dossiers protégés.
    /// </summary>
    /// <param name="processusEleve">Vrai si le jeton du processus est élevé.</param>
    /// <param name="cheminExecutable">Chemin complet de l'exécutable en cours ; vide ou inconnu : jamais ce mode.</param>
    /// <param name="dossiersProteges">Dossiers modifiables par les seuls administrateurs (Program Files, Windows...) ; les vides sont ignorés.</param>
    /// <returns>Vrai dans le mode administrateur.</returns>
    public static bool EstInstallationAdministrateur(bool processusEleve, string? cheminExecutable, IEnumerable<string?> dossiersProteges)
    {
        ArgumentNullException.ThrowIfNull(dossiersProteges);
        if (!processusEleve || string.IsNullOrWhiteSpace(cheminExecutable))
        {
            return false;
        }

        string executable;
        try
        {
            executable = Path.GetFullPath(cheminExecutable);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        foreach (var dossier in dossiersProteges)
        {
            if (!string.IsNullOrWhiteSpace(dossier) && EstSous(executable, dossier))
            {
                return true;
            }
        }

        return false;
    }

    // Par dossiers entiers : « C:\Program Files Extra\x.exe » n'est pas sous « C:\Program Files ».
    private static bool EstSous(string chemin, string dossier)
    {
        string racine;
        try
        {
            racine = Path.GetFullPath(dossier);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        racine = racine.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return chemin.StartsWith(racine, StringComparison.OrdinalIgnoreCase);
    }
}
