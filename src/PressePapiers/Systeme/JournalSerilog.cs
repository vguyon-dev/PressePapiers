using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Systeme;

/// <summary>Journal fichier par Serilog, un fichier par jour, 14 gardés. Coquille : remplacée par la tâche 16.</summary>
public sealed class JournalSerilog : IJournal, IDisposable
{
    /// <summary>Crée le journal dans un dossier.</summary>
    public JournalSerilog(string dossierJournal) => throw new NotImplementedException("Tâche 16");

    /// <inheritdoc />
    public void Info(string message) => throw new NotImplementedException("Tâche 16");

    /// <inheritdoc />
    public void Avertissement(string message, Exception? exception = null) => throw new NotImplementedException("Tâche 16");

    /// <inheritdoc />
    public void Erreur(string message, Exception? exception = null) => throw new NotImplementedException("Tâche 16");

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 16");
}
