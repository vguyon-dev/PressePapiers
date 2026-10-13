namespace PressePapiers.Interface;

/// <summary>
/// Boîtes de dialogue de la fenêtre principale. Une interface pour que le modèle de vue reste sans contrôle WinUI
/// et pour que l'essai remplace les vraies boîtes par des réponses toutes faites.
/// </summary>
public interface IDialogues
{
    /// <summary>Vrai tant qu'une boîte est ouverte : la perte du focus ne doit alors pas masquer la fenêtre.</summary>
    bool EnCours { get; }

    /// <summary>Demande une confirmation ; faux si l'utilisateur refuse ou si aucune boîte ne peut s'ouvrir.</summary>
    Task<bool> ConfirmerAsync(string titre, string message, string libelleConfirmation);

    /// <summary>Demande un nom ; null si l'utilisateur annule ou si aucune boîte ne peut s'ouvrir.</summary>
    Task<string?> DemanderNomAsync(string titre, string nomInitial, string libelleValidation);
}
