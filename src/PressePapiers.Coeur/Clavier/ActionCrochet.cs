namespace PressePapiers.Coeur.Clavier;

/// <summary>Décision de l'automate Win+V pour une frappe vue par le hook.</summary>
public enum ActionCrochet
{
    /// <summary>Laisser la frappe suivre son chemin.</summary>
    Laisser,

    /// <summary>Avaler la frappe.</summary>
    Avaler,

    /// <summary>Avaler la frappe et ouvrir la fenêtre.</summary>
    AvalerEtOuvrir,

    /// <summary>Envoyer la touche sans effet pour masquer le menu Démarrer, puis laisser passer le relâchement de Win.</summary>
    MasquerPuisLaisser,
}
