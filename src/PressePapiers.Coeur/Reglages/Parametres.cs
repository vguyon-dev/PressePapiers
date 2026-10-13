using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Reglages;

/// <summary>Réglages de l'utilisateur, avec les valeurs par défaut de la spec.</summary>
public sealed record Parametres
{
    /// <summary>
    /// Raccourci de secours, toujours actif en plus de Win+V. Win+Alt+V : Windows refuse Win+Maj+V et Win+Ctrl+V (réservés),
    /// mais accepte celui-ci, qui reste court à presser et marche aussi devant une fenêtre administrateur.
    /// </summary>
    public string RaccourciSecours { get; init; } = "Win+Alt+V";

    /// <summary>Lancer l'application à l'ouverture de session Windows.</summary>
    public bool DemarrageAvecWindows { get; init; } = true;

    /// <summary>Mode de collage quand l'utilisateur n'en choisit pas.</summary>
    public ModeCollage MiseEnFormeParDefaut { get; init; } = ModeCollage.TexteSeul;

    /// <summary>Nombre maximal d'éléments dans Temporaire ; 0 désactive le plafond.</summary>
    public int PlafondTemporaire { get; init; } = 5000;

    /// <summary>Thème de la fenêtre.</summary>
    public ThemeApplication Theme { get; init; } = ThemeApplication.Systeme;

    /// <summary>Applications dont le texte s'affiche en police à chasse fixe.</summary>
    public IReadOnlyList<string> ApplicationsChasseFixe { get; init; } =
        ["WindowsTerminal", "Code", "devenv", "powershell", "pwsh", "cmd", "Ssms", "rider64", "notepad++"];

    /// <summary>Largeur mémorisée de la fenêtre, en pixels.</summary>
    public int LargeurFenetre { get; init; } = 920;

    /// <summary>Hauteur mémorisée de la fenêtre, en pixels.</summary>
    public int HauteurFenetre { get; init; } = 560;
}
