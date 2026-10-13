namespace PressePapiers.Coeur.Modele;

/// <summary>Ce qui a demandé l'ouverture de la fenêtre principale.</summary>
public enum OrigineOuverture
{
    /// <summary>Win+V intercepté par le hook clavier.</summary>
    WinV,

    /// <summary>Raccourci de secours enregistré par <c>RegisterHotKey</c>.</summary>
    RaccourciSecours,

    /// <summary>Clic sur l'icône de notification ou commande « Ouvrir » de son menu.</summary>
    Icone,

    /// <summary>Second lancement de l'application, redirigé vers l'instance principale.</summary>
    Relance,
}
