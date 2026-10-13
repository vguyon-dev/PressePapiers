namespace PressePapiers.Coeur.Collage;

/// <summary>Choix entre coller et seulement copier (spec, sections 10.2 et 10.3). Coquille : remplacée par la tâche 12.</summary>
public static class DecisionCollage
{
    /// <summary>
    /// Décide de l'issue d'un collage. <paramref name="niveauCible"/> null (lecture impossible) est traité
    /// comme un niveau plus élevé que celui de l'outil.
    /// </summary>
    public static IssueCollage Decider(bool copieSeuleDemandee, bool fenetreOrigineConnue, bool fenetreExiste,
                                       int? niveauCible, int niveauOutil) =>
        throw new NotImplementedException("Tâche 12");
}
