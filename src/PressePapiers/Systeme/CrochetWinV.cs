#pragma warning disable CS0067 // Coquille : l'événement est levé par la tâche 20.
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Systeme;

/// <summary>Hook clavier bas niveau (WH_KEYBOARD_LL) sur un fil dédié. Coquille : remplacée par la tâche 20.</summary>
public sealed class CrochetWinV : ICrochetWinV
{
    /// <summary>Crée le hook, sans le démarrer.</summary>
    public CrochetWinV(IJournal journal) => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public bool EstActif => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public string? RaisonInactif => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public event Action<nint>? WinVPresse;

    /// <inheritdoc />
    public void Demarrer() => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public void Reinstaller() => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 20");
}
