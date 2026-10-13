using PressePapiers.Coeur.Session;

namespace PressePapiers.Systeme;

/// <summary>Repère de session lu par WTSQuerySessionInformation. Coquille : remplacée par la tâche 16.</summary>
public sealed class SourceSessionWts : ISourceSession
{
    /// <inheritdoc />
    public long? LireHeureOuverture() => throw new NotImplementedException("Tâche 16");
}
