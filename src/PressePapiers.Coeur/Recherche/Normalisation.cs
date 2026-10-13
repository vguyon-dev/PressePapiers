using System.Globalization;
using System.Text;

namespace PressePapiers.Coeur.Recherche;

/// <summary>Normalisation du texte de recherche, partagée entre l'écriture de l'index et la requête.</summary>
public static class Normalisation
{
    /// <summary>
    /// Minuscules, accents retirés (décomposition Unicode), ligatures ramenées (œ en oe, æ en ae), espaces réduits à un seul.
    /// Un substitut UTF-16 isolé (un demi-emoji copié) est remplacé par U+FFFD au lieu de lever une exception.
    /// </summary>
    public static string Normaliser(string? texte)
    {
        if (string.IsNullOrEmpty(texte))
        {
            return "";
        }

        var decompose = Decomposer(texte.ToLowerInvariant());
        var resultat = new StringBuilder(decompose.Length);
        var dernierEstEspace = true; // vrai au départ : les espaces de tête sont supprimés

        foreach (var c in decompose)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!dernierEstEspace)
                {
                    resultat.Append(' ');
                    dernierEstEspace = true;
                }

                continue;
            }

            // Les accents sont des marques séparées après la décomposition : on les écarte.
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            switch (c)
            {
                case 'œ':
                    resultat.Append("oe");
                    break;
                case 'æ':
                    resultat.Append("ae");
                    break;
                default:
                    resultat.Append(c);
                    break;
            }

            dernierEstEspace = false;
        }

        // Au plus un espace final, issu d'un séparateur en fin de texte.
        if (resultat.Length > 0 && resultat[^1] == ' ')
        {
            resultat.Length--;
        }

        return resultat.ToString();
    }

    /// <summary>
    /// Texte normalisé qui réunit, dans cet ordre, le nom, le texte, les chemins de fichiers et l'application d'origine.
    /// Les parties absentes ou vides sont ignorées.
    /// </summary>
    public static string TexteRecherche(string? nom, string? texte, IReadOnlyList<string>? fichiers, string? application)
    {
        var parties = new List<string>(4);
        Ajouter(parties, nom);
        Ajouter(parties, texte);
        if (fichiers is { Count: > 0 })
        {
            Ajouter(parties, string.Join(' ', fichiers));
        }

        Ajouter(parties, application);

        // Une seule normalisation de l'ensemble : le texte copié est la partie volumineuse.
        return Normaliser(string.Join(' ', parties));
    }

    private static void Ajouter(List<string> parties, string? partie)
    {
        if (!string.IsNullOrWhiteSpace(partie))
        {
            parties.Add(partie);
        }
    }

    // Normalize lève ArgumentException devant un substitut isolé : on le remplace d'abord, puis on réessaie.
    private static string Decomposer(string texte)
    {
        try
        {
            return texte.Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            return RemplacerSubstitutsIsoles(texte).Normalize(NormalizationForm.FormD);
        }
    }

    private static string RemplacerSubstitutsIsoles(string texte)
    {
        var resultat = new StringBuilder(texte.Length);
        for (var i = 0; i < texte.Length; i++)
        {
            var c = texte[i];
            if (char.IsHighSurrogate(c) && i + 1 < texte.Length && char.IsLowSurrogate(texte[i + 1]))
            {
                resultat.Append(c).Append(texte[++i]);
            }
            else if (char.IsSurrogate(c))
            {
                resultat.Append('�');
            }
            else
            {
                resultat.Append(c);
            }
        }

        return resultat.ToString();
    }
}
