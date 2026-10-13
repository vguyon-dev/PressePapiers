namespace PressePapiers.Coeur.Recherche;

/// <summary>Normalisation du texte de recherche, partagée entre l'écriture de l'index et la requête. Coquille : tâche 2.</summary>
public static class Normalisation
{
    /// <summary>Minuscules, accents retirés (décomposition Unicode), ligatures ramenées (œ en oe, æ en ae), espaces réduits.</summary>
    public static string Normaliser(string? texte) => throw new NotImplementedException("Tâche 2");

    /// <summary>Texte normalisé qui réunit le nom, le texte, les chemins de fichiers et l'application d'origine.</summary>
    public static string TexteRecherche(string? nom, string? texte, IReadOnlyList<string>? fichiers, string? application) =>
        throw new NotImplementedException("Tâche 2");
}
