namespace PressePapiers.Coeur.Collage;

/// <summary>Résultat d'une demande de collage.</summary>
public enum IssueCollage
{
    /// <summary>Ctrl+V a été envoyé à la fenêtre d'origine.</summary>
    Colle,

    /// <summary>Copie seule demandée par l'utilisateur (Ctrl+Entrée).</summary>
    CopieDemandee,

    /// <summary>Pas de fenêtre d'origine fiable : copie seulement.</summary>
    CopieSansFenetreOrigine,

    /// <summary>La fenêtre d'origine n'existe plus : copie seulement.</summary>
    CopieFenetreFermee,

    /// <summary>La fenêtre d'origine est de niveau d'intégrité supérieur : copie seulement.</summary>
    CopieFenetreElevee,

    /// <summary>L'écriture dans le presse-papiers ou le retour au premier plan a échoué.</summary>
    Echec,
}
