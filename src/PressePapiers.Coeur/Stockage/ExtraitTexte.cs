namespace PressePapiers.Coeur.Stockage;

/// <summary>Calcul de l'extrait stocké avec chaque élément, pour que la liste n'ait jamais à relire le texte complet.</summary>
internal static class ExtraitTexte
{
    /// <summary>
    /// Début du texte, <see cref="Modele.ElementResume.LongueurMaxExtrait"/> caractères Unicode au plus (un emoji compte
    /// pour un, comme dans <c>substr</c> de SQLite, que la migration emploie) ; null si le texte est absent.
    /// </summary>
    public static string? Calculer(string? texte)
    {
        if (texte is null || texte.Length <= Modele.ElementResume.LongueurMaxExtrait)
        {
            return texte;
        }

        var caracteres = 0;
        var fin = 0;
        foreach (var rune in texte.EnumerateRunes())
        {
            if (caracteres == Modele.ElementResume.LongueurMaxExtrait)
            {
                break;
            }

            fin += rune.Utf16SequenceLength;
            caracteres++;
        }

        return texte[..fin];
    }
}
