namespace PressePapiers.Coeur.Modele;

/// <summary>Un élément de l'historique, tel qu'il est lu en base.</summary>
public sealed record Element
{
    /// <summary>Identifiant en base.</summary>
    public required long Id { get; init; }

    /// <summary>Onglet qui contient l'élément.</summary>
    public required long OngletId { get; init; }

    /// <summary>Nom choisi par l'utilisateur ; absent tant que l'élément est temporaire.</summary>
    public string? Nom { get; init; }

    /// <summary>Nature du contenu.</summary>
    public required TypeElement Type { get; init; }

    /// <summary>Texte brut ; pour <see cref="TypeElement.Fichiers"/>, les chemins séparés par un retour à la ligne.</summary>
    public string? Texte { get; init; }

    /// <summary>Contenu RTF brut.</summary>
    public byte[]? Rtf { get; init; }

    /// <summary>Octets bruts du format « HTML Format ».</summary>
    public byte[]? Html { get; init; }

    /// <summary>Nom du PNG dans le dossier des images.</summary>
    public string? ImageFichier { get; init; }

    /// <summary>Miniature PNG, 160 pixels de large au plus.</summary>
    public byte[]? Miniature { get; init; }

    /// <summary>Empreinte SHA-256 du contenu principal, utilisée pour le dédoublonnage.</summary>
    public required byte[] Empreinte { get; init; }

    /// <summary>Nom du processus d'origine, indication au mieux.</summary>
    public string? Application { get; init; }

    /// <summary>Date de la copie.</summary>
    public required DateTimeOffset DateCopie { get; init; }

    /// <summary>Date du dernier usage ; elle fixe l'ordre d'affichage dans l'onglet.</summary>
    public required DateTimeOffset DateUsage { get; init; }

    /// <summary>Taille en octets du contenu principal.</summary>
    public required long Taille { get; init; }

    /// <summary>Chemins de la liste de fichiers (découpe de <see cref="Texte"/>) ; vide pour les autres types.</summary>
    public IReadOnlyList<string> Fichiers =>
        Type == TypeElement.Fichiers && !string.IsNullOrEmpty(Texte)
            ? Texte.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            : [];
}
