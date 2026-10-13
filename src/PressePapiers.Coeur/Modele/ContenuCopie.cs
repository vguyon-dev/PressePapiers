namespace PressePapiers.Coeur.Modele;

/// <summary>Ce que la capture a lu dans le presse-papiers, avant enregistrement.</summary>
public sealed record ContenuCopie
{
    /// <summary>Nature du contenu.</summary>
    public required TypeElement Type { get; init; }

    /// <summary>Texte brut.</summary>
    public string? Texte { get; init; }

    /// <summary>Contenu RTF brut.</summary>
    public byte[]? Rtf { get; init; }

    /// <summary>Octets bruts du format « HTML Format ».</summary>
    public byte[]? Html { get; init; }

    /// <summary>Image convertie en PNG.</summary>
    public byte[]? ImagePng { get; init; }

    /// <summary>Miniature PNG de l'image.</summary>
    public byte[]? Miniature { get; init; }

    /// <summary>Chemins des fichiers copiés.</summary>
    public IReadOnlyList<string>? Fichiers { get; init; }

    /// <summary>Nom du processus d'origine, indication au mieux.</summary>
    public string? Application { get; init; }
}
