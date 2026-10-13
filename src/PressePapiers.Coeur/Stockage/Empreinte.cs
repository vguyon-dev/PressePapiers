using System.Security.Cryptography;
using System.Text;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>Empreinte du contenu principal, pour le dédoublonnage dans Temporaire.</summary>
public static class Empreinte
{
    /// <summary>
    /// SHA-256 : texte en UTF-8 (Texte, MisEnForme), octets du PNG (Image), chemins joints par un retour à la ligne (Fichiers).
    /// La mise en forme (RTF, HTML) n'entre pas dans l'empreinte : un même texte copié avec ou sans elle reste un seul élément.
    /// </summary>
    /// <param name="contenu">Contenu copié.</param>
    /// <exception cref="ArgumentException">Le contenu principal du type demandé est absent.</exception>
    public static byte[] Calculer(ContenuCopie contenu)
    {
        ArgumentNullException.ThrowIfNull(contenu);

        return contenu.Type switch
        {
            TypeElement.Texte or TypeElement.MisEnForme =>
                SHA256.HashData(Encoding.UTF8.GetBytes(Requis(contenu.Texte, "Texte"))),
            TypeElement.Image =>
                SHA256.HashData(Requis(contenu.ImagePng, "ImagePng")),
            TypeElement.Fichiers =>
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', Requis(contenu.Fichiers, "Fichiers")))),
            _ => throw new ArgumentException($"Type d'élément inconnu : {contenu.Type}.", nameof(contenu)),
        };
    }

    // Un contenu sans sa partie principale est un bogue de l'appelant : on le dit au lieu de hacher du vide.
    private static T Requis<T>(T? valeur, string nom) where T : class =>
        valeur ?? throw new ArgumentException($"Le contenu copié n'a pas de {nom}.", "contenu");
}
