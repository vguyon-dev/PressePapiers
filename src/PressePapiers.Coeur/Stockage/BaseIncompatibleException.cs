namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// La SQLite embarquée ne convient pas : version inférieure à 3.34.0 (tokenizer <c>trigram</c>) ou FTS5 absent.
/// L'application affiche le message et s'arrête proprement.
/// </summary>
/// <param name="message">Explication destinée à l'utilisateur.</param>
public sealed class BaseIncompatibleException(string message) : Exception(message);
