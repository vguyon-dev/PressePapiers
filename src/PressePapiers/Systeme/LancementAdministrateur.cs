using PressePapiers.Coeur.Session;

namespace PressePapiers.Systeme;

/// <summary>
/// Dit si le processus courant est l'installation administrateur de <c>scripts\publier.ps1 -Administrateur</c> : lancé par
/// la tâche planifiée « PressePapiers », élevé, depuis Program Files. La règle est dans <see cref="ModeAdministrateur"/> ;
/// ici ne sont lus que le jeton du processus et les dossiers protégés du système.
/// </summary>
public static class LancementAdministrateur
{
    // Le jeton et l'emplacement de l'exécutable ne changent pas pendant la vie du processus : une seule lecture.
    private static readonly Lazy<bool> _actif = new(Detecter);

    /// <summary>Vrai si l'application tourne en administrateur depuis un dossier protégé (donc lancée par la tâche planifiée).</summary>
    public static bool Actif => _actif.Value;

    private static bool Detecter() => ModeAdministrateur.EstInstallationAdministrateur(
        NiveauIntegrite.ProcessusCourantEstEleve(),
        Environment.ProcessPath,
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        ]);
}
