using System.Globalization;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>
/// Textes courts qui décrivent un élément dans la liste : aperçu sur deux lignes, heure, taille, icône de type.
/// Logique pure, sans contrôle WinUI, pour rester lisible et vérifiable à part.
/// </summary>
internal static class ResumeElement
{
    /// <summary>Glyphe Segoe Fluent Icons du type d'élément.</summary>
    public static string Glyphe(TypeElement type) => type switch
    {
        TypeElement.MisEnForme => "\uE8D2",
        TypeElement.Image => "\uEB9F",
        TypeElement.Fichiers => "\uE8B7",
        _ => "\uE8A5",
    };

    /// <summary>
    /// Deux premières lignes non vides du texte, débarrassées de leur indentation ; une ellipse signale la suite.
    /// Un texte de plusieurs mégaoctets n'est lu que sur ses premiers caractères.
    /// </summary>
    public static string ApercuTexte(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return string.Empty;
        }

        const int caracteresLus = 2000;
        var debut = texte.Length > caracteresLus ? texte[..caracteresLus] : texte;
        var lignes = debut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var resume = lignes.Length == 1 ? lignes[0] : lignes[0] + "\n" + lignes[1];
        var suite = lignes.Length > 2 || texte.Length > caracteresLus;
        return suite ? resume + " …" : resume;
    }

    /// <summary>Texte d'aperçu d'un élément selon son type.</summary>
    public static string Apercu(Element element)
    {
        switch (element.Type)
        {
            case TypeElement.Image:
                return "Image, " + Taille(element.Taille);
            case TypeElement.Fichiers:
                var chemins = element.Fichiers;
                var noms = chemins.Take(3).Select(c => Path.GetFileName(c.TrimEnd('\\', '/')) is { Length: > 0 } nom ? nom : c);
                var reste = chemins.Count > 3 ? ", …" : string.Empty;
                var pluriel = chemins.Count > 1 ? "fichiers" : "fichier";
                return $"{chemins.Count} {pluriel} : {string.Join(", ", noms)}{reste}";
            default:
                var apercu = ApercuTexte(element.Texte);
                return apercu.Length > 0 ? apercu : "(contenu vide)";
        }
    }

    /// <summary>Taille lisible en français : « 812 o », « 12 Ko », « 3,4 Mo ».</summary>
    public static string Taille(long octets)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        return octets switch
        {
            < 1024 => $"{octets} o",
            < 1024 * 1024 => (octets / 1024.0).ToString("0.#", fr) + " Ko",
            _ => (octets / 1024.0 / 1024.0).ToString("0.#", fr) + " Mo",
        };
    }

    /// <summary>Heure du jour, « hier 18:03 », « 08/10 » ou « 08/10/2025 » selon l'ancienneté, dans le fuseau de l'horloge.</summary>
    public static string Heure(DateTimeOffset date, TimeProvider horloge)
    {
        var local = TimeZoneInfo.ConvertTime(date, horloge.LocalTimeZone);
        var maintenant = TimeZoneInfo.ConvertTime(horloge.GetUtcNow(), horloge.LocalTimeZone);
        var invariant = CultureInfo.InvariantCulture;

        if (local.Date == maintenant.Date)
        {
            return local.ToString("HH:mm", invariant);
        }

        if (local.Date == maintenant.Date.AddDays(-1))
        {
            return "hier " + local.ToString("HH:mm", invariant);
        }

        return local.Year == maintenant.Year ? local.ToString("dd/MM", invariant) : local.ToString("dd/MM/yyyy", invariant);
    }

    /// <summary>Vrai si le nom d'application figure dans la liste des applications à chasse fixe (casse et « .exe » ignorés).</summary>
    public static bool EstChasseFixe(string? application, IReadOnlySet<string> applicationsChasseFixe) =>
        !string.IsNullOrWhiteSpace(application) && applicationsChasseFixe.Contains(NomSansExtension(application));

    /// <summary>Ensemble comparable (insensible à la casse, sans « .exe ») des noms de la liste réglable.</summary>
    public static HashSet<string> EnsembleChasseFixe(IEnumerable<string> noms) =>
        new(noms.Where(n => !string.IsNullOrWhiteSpace(n)).Select(NomSansExtension), StringComparer.OrdinalIgnoreCase);

    private static string NomSansExtension(string nom)
    {
        var propre = nom.Trim();
        return propre.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? propre[..^4] : propre;
    }
}
