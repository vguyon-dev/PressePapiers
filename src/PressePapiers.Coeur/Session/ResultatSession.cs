namespace PressePapiers.Coeur.Session;

/// <summary>Issue de la détection de session au démarrage.</summary>
/// <param name="Etat">Situation constatée.</param>
/// <param name="ElementsPurges">Nombre d'éléments supprimés de Temporaire.</param>
public sealed record ResultatSession(EtatSession Etat, int ElementsPurges);
