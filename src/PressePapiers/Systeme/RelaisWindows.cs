#pragma warning disable CS0067 // Coquille : les événements sont levés par la tâche 17.
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Reglages;

namespace PressePapiers.Systeme;

/// <summary>
/// Fenêtre Win32 de premier niveau invisible, sur le fil d'interface : reçoit les copies, le raccourci de secours,
/// les messages de l'icône et les changements de session. Coquille : remplacée par la tâche 17.
/// </summary>
public sealed class RelaisWindows : IDisposable
{
    /// <summary>Message de rappel de l'icône de notification (WM_APP + 1).</summary>
    public const uint MessageIcone = 0x8001;

    /// <summary>Crée la fenêtre sur le fil courant, qui doit être le fil d'interface.</summary>
    public RelaisWindows(IJournal journal) => throw new NotImplementedException("Tâche 17");

    /// <summary>Handle de la fenêtre relais.</summary>
    public nint Handle => throw new NotImplementedException("Tâche 17");

    /// <summary>WM_CLIPBOARDUPDATE.</summary>
    public event Action? CopieDetectee;

    /// <summary>WM_HOTKEY, identifiant 1.</summary>
    public event Action? RaccourciSecoursPresse;

    /// <summary>Message de l'icône ; l'argument est le LOWORD de lParam.</summary>
    public event Action<uint>? MessageIconeRecu;

    /// <summary>Message enregistré « TaskbarCreated » : l'Explorateur a redémarré.</summary>
    public event Action? BarreDesTachesRecreee;

    /// <summary>WM_WTSSESSION_CHANGE avec WTS_SESSION_UNLOCK.</summary>
    public event Action? SessionDeverrouillee;

    /// <summary>WM_POWERBROADCAST avec PBT_APMRESUMEAUTOMATIC.</summary>
    public event Action? SortieDeVeille;

    /// <summary>Commence à écouter le presse-papiers (AddClipboardFormatListener) ; faux en cas d'échec.</summary>
    public bool EcouterPressePapiers() => throw new NotImplementedException("Tâche 17");

    /// <summary>Enregistre le raccourci de secours ; renvoie faux avec le code d'erreur Windows en cas d'échec.</summary>
    public bool EnregistrerRaccourci(Raccourci raccourci, out int codeErreur) => throw new NotImplementedException("Tâche 17");

    /// <summary>Retire le raccourci de secours.</summary>
    public void RetirerRaccourci() => throw new NotImplementedException("Tâche 17");

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 17");
}
