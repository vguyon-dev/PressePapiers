using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Coeur.Recherche;

/// <summary>Recherche plein texte dans tous les onglets. Coquille : remplacée par la tâche 9.</summary>
public sealed class MoteurRecherche
{
    /// <summary>Crée le moteur sur une base ouverte.</summary>
    public MoteurRecherche(BaseHistorique baseHistorique) => throw new NotImplementedException("Tâche 9");

    /// <summary>
    /// Tous les mots sont requis ; un mot de 3 caractères ou plus passe par MATCH (entre guillemets doubles),
    /// un mot de 1 ou 2 caractères par LIKE avec ESCAPE ; liste vide si la requête est vide ; DateUsage décroissante.
    /// </summary>
    public IReadOnlyList<Element> Chercher(string requete, int limite = 500) => throw new NotImplementedException("Tâche 9");
}
