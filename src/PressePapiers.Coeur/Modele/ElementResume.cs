namespace PressePapiers.Coeur.Modele;

/// <summary>
/// Un élément de l'historique réduit à ce que la liste affiche : sans texte complet, RTF ni HTML, et avec une miniature
/// pour les seules images. Sert à remplir la liste et les résultats de recherche sans relire des mégaoctets.
/// Le contenu complet se relit avec <c>DepotHistorique.Lire</c>.
/// </summary>
public sealed record ElementResume
{
    /// <summary>Nombre maximal de caractères de <see cref="Extrait"/>.</summary>
    public const int LongueurMaxExtrait = 2000;

    /// <summary>Identifiant en base.</summary>
    public required long Id { get; init; }

    /// <summary>Onglet qui contient l'élément.</summary>
    public required long OngletId { get; init; }

    /// <summary>Nom choisi par l'utilisateur ; absent tant que l'élément est temporaire.</summary>
    public string? Nom { get; init; }

    /// <summary>Nature du contenu.</summary>
    public required TypeElement Type { get; init; }

    /// <summary>
    /// Début du texte, <see cref="LongueurMaxExtrait"/> caractères au plus ; pour <see cref="TypeElement.Fichiers"/>,
    /// début de la liste des chemins séparés par un retour à la ligne.
    /// </summary>
    public string? Extrait { get; init; }

    /// <summary>Nom du processus d'origine, indication au mieux.</summary>
    public string? Application { get; init; }

    /// <summary>Date de la copie.</summary>
    public required DateTimeOffset DateCopie { get; init; }

    /// <summary>Date du dernier usage ; elle fixe l'ordre d'affichage dans l'onglet.</summary>
    public required DateTimeOffset DateUsage { get; init; }

    /// <summary>Taille en octets du contenu principal.</summary>
    public required long Taille { get; init; }

    /// <summary>Miniature PNG, 160 pixels de large au plus ; lue pour les images seulement.</summary>
    public byte[]? Miniature { get; init; }

    /// <summary>Vrai si l'élément garde un contenu RTF ou HTML.</summary>
    public bool AMiseEnForme { get; init; }

    /// <summary>Nombre de chemins de la liste ; zéro pour les autres types.</summary>
    public int NombreFichiers { get; init; }
}
