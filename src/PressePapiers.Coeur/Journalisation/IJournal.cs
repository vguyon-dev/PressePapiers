namespace PressePapiers.Coeur.Journalisation;

/// <summary>Journal des événements notables ; Coeur n'en connaît que cette interface.</summary>
public interface IJournal
{
    /// <summary>Note un événement ordinaire.</summary>
    void Info(string message);

    /// <summary>Note un problème qui n'empêche pas de continuer.</summary>
    void Avertissement(string message, Exception? exception = null);

    /// <summary>Note une erreur.</summary>
    void Erreur(string message, Exception? exception = null);
}
