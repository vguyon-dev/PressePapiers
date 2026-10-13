namespace PressePapiers.Coeur.Journalisation;

/// <summary>Journal qui ne retient rien, pour les tests et les cas sans journal.</summary>
public sealed class JournalNul : IJournal
{
    /// <summary>Instance partagée.</summary>
    public static readonly JournalNul Instance = new();

    /// <inheritdoc />
    public void Info(string message)
    {
    }

    /// <inheritdoc />
    public void Avertissement(string message, Exception? exception = null)
    {
    }

    /// <inheritdoc />
    public void Erreur(string message, Exception? exception = null)
    {
    }
}
