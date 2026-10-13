namespace PressePapiers.Coeur.Modele;

/// <summary>Nature du contenu d'un élément ; les valeurs sont celles stockées dans la colonne <c>type</c>.</summary>
public enum TypeElement
{
    /// <summary>Texte brut seul.</summary>
    Texte = 1,

    /// <summary>Texte accompagné de RTF et/ou de HTML.</summary>
    MisEnForme = 2,

    /// <summary>Image, conservée en PNG dans le dossier des images.</summary>
    Image = 3,

    /// <summary>Liste de fichiers (chemins séparés par des retours à la ligne).</summary>
    Fichiers = 4,
}
