namespace PressePapiers.Coeur.Clavier;

/// <summary>Ce que la fenêtre fait d'une touche (spec, section 8).</summary>
public enum ActionTouche
{
    /// <summary>La touche garde son rôle normal.</summary>
    Aucune,

    /// <summary>Entrée : coller dans la fenêtre d'origine, avec le mode de mise en forme des réglages.</summary>
    Coller,

    /// <summary>Maj+Entrée : coller dans l'autre mode de mise en forme.</summary>
    CollerAutreMode,

    /// <summary>Ctrl+Entrée : copier seulement, sans coller.</summary>
    CopierSeulement,

    /// <summary>F2 : aller au champ Nom.</summary>
    AllerAuNom,

    /// <summary>Suppr : supprimer l'élément choisi.</summary>
    SupprimerElement,

    /// <summary>Ctrl+Tab : onglet suivant.</summary>
    OngletSuivant,

    /// <summary>Ctrl+Maj+Tab : onglet précédent.</summary>
    OngletPrecedent,

    /// <summary>Ctrl+1 à Ctrl+9 : aller à l'onglet dont le numéro est dans <see cref="CommandeClavier.Numero"/>.</summary>
    AllerAOnglet,

    /// <summary>Échap avec une recherche en cours : effacer la recherche.</summary>
    EffacerRecherche,

    /// <summary>Échap sans recherche : masquer la fenêtre.</summary>
    Masquer,

    /// <summary>Flèche haut depuis la zone de recherche : élément précédent de la liste.</summary>
    SelectionPrecedente,

    /// <summary>Flèche bas depuis la zone de recherche : élément suivant de la liste.</summary>
    SelectionSuivante,
}
