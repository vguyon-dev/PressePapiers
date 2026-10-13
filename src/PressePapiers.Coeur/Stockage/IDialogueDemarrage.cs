namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Ce que le démarrage peut dire ou demander à l'utilisateur avant que la moindre fenêtre existe.
/// L'application l'implémente par une boîte de message native ; les tests, par un espion.
/// </summary>
public interface IDialogueDemarrage
{
    /// <summary>Affiche un message bloquant (problème qui arrête le démarrage).</summary>
    /// <param name="message">Texte clair, en français, qui dit quoi faire.</param>
    void Informer(string message);

    /// <summary>Pose une question oui ou non et renvoie vrai si l'utilisateur répond oui.</summary>
    /// <param name="question">Texte de la question.</param>
    bool Confirmer(string question);
}
