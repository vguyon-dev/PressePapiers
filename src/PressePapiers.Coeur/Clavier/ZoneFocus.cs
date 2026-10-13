namespace PressePapiers.Coeur.Clavier;

/// <summary>Où se trouve le focus clavier dans la fenêtre : de lui dépend le sens d'une touche comme Entrée ou Suppr.</summary>
public enum ZoneFocus
{
    /// <summary>La zone de recherche.</summary>
    Recherche,

    /// <summary>La liste des éléments.</summary>
    Liste,

    /// <summary>Le champ Nom de l'aperçu, qui a ses propres touches (Entrée enregistre).</summary>
    ChampNom,

    /// <summary>Tout autre contrôle (boutons, liste d'onglets, volet) : la touche garde son rôle normal.</summary>
    Autre,
}
