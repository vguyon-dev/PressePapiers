namespace PressePapiers.Coeur.Modele;

/// <summary>Un onglet de rangement ; un seul est temporaire, il ne se renomme ni ne se supprime.</summary>
/// <param name="Id">Identifiant en base.</param>
/// <param name="Nom">Nom affiché.</param>
/// <param name="Ordre">Position d'affichage, l'onglet Temporaire valant 0.</param>
/// <param name="EstTemporaire">Vrai pour l'onglet Temporaire, vidé à chaque nouvelle session Windows.</param>
public sealed record Onglet(long Id, string Nom, int Ordre, bool EstTemporaire);
