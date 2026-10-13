using System.Globalization;
using System.Text;
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
    /// Seuls les <see cref="ElementResume.LongueurMaxExtrait"/> premiers caractères Unicode sont lus (un emoji compte pour
    /// un, comme dans l'extrait stocké) : c'est tout ce que contient un résumé.
    /// </summary>
    /// <param name="texte">Extrait du texte.</param>
    /// <param name="tailleTotaleOctets">
    /// Taille en octets UTF-8 du texte complet (<see cref="ElementResume.Taille"/>) : indicateur fiable d'un extrait coupé,
    /// y compris quand le texte fait exactement la longueur maximale. Sans elle, un extrait qui atteint cette longueur
    /// est supposé coupé.
    /// </param>
    public static string ApercuTexte(string? texte, long? tailleTotaleOctets = null)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return string.Empty;
        }

        const int caracteresLus = ElementResume.LongueurMaxExtrait;
        var (debut, caracteres) = Debut(texte, caracteresLus);
        var lignes = debut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var resume = lignes.Length == 1 ? lignes[0] : lignes[0] + "\n" + lignes[1];
        var extraitCoupe = tailleTotaleOctets is long total
            ? Encoding.UTF8.GetByteCount(texte) < total
            : caracteres >= caracteresLus;
        return lignes.Length > 2 || extraitCoupe ? resume + " …" : resume;
    }

    // Les premiers caractères Unicode du texte (jamais une paire de substitution coupée en deux) et leur nombre.
    private static (string Debut, int Caracteres) Debut(string texte, int maximum)
    {
        var caracteres = 0;
        var fin = 0;
        foreach (var rune in texte.EnumerateRunes())
        {
            if (caracteres == maximum)
            {
                break;
            }

            fin += rune.Utf16SequenceLength;
            caracteres++;
        }

        return (texte[..fin], caracteres);
    }

    /// <summary>Texte d'aperçu d'un élément selon son type, tiré de son résumé (le contenu complet n'est pas lu).</summary>
    public static string Apercu(ElementResume resume)
    {
        switch (resume.Type)
        {
            case TypeElement.Image:
                return "Image, " + Taille(resume.Taille);
            case TypeElement.Fichiers:
                var chemins = (resume.Extrait ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

                // L'extrait est coupé à sa longueur maximale : son dernier chemin peut être incomplet.
                if ((resume.Extrait?.EnumerateRunes().Count() ?? 0) >= ElementResume.LongueurMaxExtrait && chemins.Count > 1)
                {
                    chemins.RemoveAt(chemins.Count - 1);
                }

                var total = Math.Max(resume.NombreFichiers, chemins.Count);
                var noms = chemins.Take(3).Select(c => Path.GetFileName(c.TrimEnd('\\', '/')) is { Length: > 0 } nom ? nom : c);
                var reste = total > 3 ? ", …" : string.Empty;
                var pluriel = total > 1 ? "fichiers" : "fichier";
                return $"{total} {pluriel} : {string.Join(", ", noms)}{reste}";
            default:
                var apercu = ApercuTexte(resume.Extrait, resume.Taille);
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
