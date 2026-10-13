using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>
/// Ce que le fil de lecture a copié du presse-papiers, avant toute conversion : des octets et du texte seulement.
/// L'image reste un DIB, convertie en PNG après la fermeture du presse-papiers.
/// </summary>
/// <param name="Type">Type d'élément décidé d'après les formats présents.</param>
/// <param name="Texte">Texte brut (Texte, MisEnForme).</param>
/// <param name="Rtf">Octets RTF sans nul final.</param>
/// <param name="Html">Octets « HTML Format » sans nul final.</param>
/// <param name="Dib">Octets du CF_DIBV5 ou du CF_DIB (Image).</param>
/// <param name="Fichiers">Chemins des fichiers (Fichiers).</param>
internal sealed record ContenuBrut(
    TypeElement Type,
    string? Texte = null,
    byte[]? Rtf = null,
    byte[]? Html = null,
    byte[]? Dib = null,
    IReadOnlyList<string>? Fichiers = null);
