using PressePapiers.Coeur.Modele;

namespace PressePapiers.Tests.Outils;

/// <summary>Fabrique de contenus copiés pour les tests du dépôt.</summary>
public static class ContenusDeTest
{
    /// <summary>Texte brut.</summary>
    public static ContenuCopie Texte(string texte, string? application = null) =>
        new() { Type = TypeElement.Texte, Texte = texte, Application = application };

    /// <summary>Texte accompagné de RTF et de HTML.</summary>
    public static ContenuCopie MisEnForme(string texte, string? application = null) =>
        new()
        {
            Type = TypeElement.MisEnForme,
            Texte = texte,
            Rtf = System.Text.Encoding.ASCII.GetBytes("{\\rtf1\\ansi " + texte + "}"),
            Html = System.Text.Encoding.UTF8.GetBytes("<b>" + texte + "</b>"),
            Application = application,
        };

    /// <summary>Image déjà convertie en PNG, avec une miniature.</summary>
    public static ContenuCopie Image(byte[] png, string? application = null) =>
        new() { Type = TypeElement.Image, ImagePng = png, Miniature = [9, 8, 7], Application = application };

    /// <summary>Liste de fichiers.</summary>
    public static ContenuCopie Fichiers(params string[] chemins) =>
        new() { Type = TypeElement.Fichiers, Fichiers = chemins };

    /// <summary>
    /// Octets qui tiennent lieu de PNG : le dépôt ne les décode pas. Deux graines différentes donnent deux contenus différents.
    /// </summary>
    public static byte[] Png(int graine, int taille = 64)
    {
        var octets = new byte[taille];
        new Random(graine).NextBytes(octets);
        return octets;
    }
}
