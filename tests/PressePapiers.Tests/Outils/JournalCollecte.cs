using System.Collections.Concurrent;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Tests.Outils;

/// <summary>Journal de test qui garde les messages, pour vérifier ce que le code note (avertissements, erreurs).</summary>
public sealed class JournalCollecte : IJournal
{
    private readonly ConcurrentQueue<string> _infos = new();
    private readonly ConcurrentQueue<string> _avertissements = new();
    private readonly ConcurrentQueue<string> _erreurs = new();

    /// <summary>Messages d'information, dans l'ordre.</summary>
    public IReadOnlyList<string> Infos => [.. _infos];

    /// <summary>Messages d'avertissement, dans l'ordre.</summary>
    public IReadOnlyList<string> Avertissements => [.. _avertissements];

    /// <summary>Messages d'erreur, dans l'ordre.</summary>
    public IReadOnlyList<string> Erreurs => [.. _erreurs];

    /// <inheritdoc />
    public void Info(string message) => _infos.Enqueue(message);

    /// <inheritdoc />
    public void Avertissement(string message, Exception? exception = null) => _avertissements.Enqueue(message);

    /// <inheritdoc />
    public void Erreur(string message, Exception? exception = null) => _erreurs.Enqueue(message);
}
