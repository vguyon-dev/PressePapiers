namespace PressePapiers.Coeur.Stockage;

/// <summary>Emplacements des données de l'application, tous dérivés d'un dossier racine.</summary>
/// <param name="Racine">Dossier racine des données.</param>
public sealed record DossierDonnees(string Racine)
{
    /// <summary>Nom de la variable d'environnement qui redirige les données (essais et recette).</summary>
    private const string VariableDonnees = "PRESSEPAPIERS_DONNEES";

    /// <summary>Fichier de la base : <c>Racine\historique.db</c>.</summary>
    public string Base => Path.Combine(Racine, "historique.db");

    /// <summary>Dossier des images PNG : <c>Racine\images</c>.</summary>
    public string Images => Path.Combine(Racine, "images");

    /// <summary>Dossier des copies de sécurité : <c>Racine\sauvegardes</c>.</summary>
    public string Sauvegardes => Path.Combine(Racine, "sauvegardes");

    /// <summary>Dossier du journal : <c>Racine\journal</c>.</summary>
    public string Journal => Path.Combine(Racine, "journal");

    /// <summary>Fichier des réglages : <c>Racine\parametres.json</c>.</summary>
    public string Parametres => Path.Combine(Racine, "parametres.json");

    /// <summary>
    /// <c>%LOCALAPPDATA%\PressePapiers</c>, ou le dossier de la variable d'environnement <c>PRESSEPAPIERS_DONNEES</c>
    /// si elle est définie (essais et recette sans toucher aux données réelles).
    /// </summary>
    public static DossierDonnees ParDefaut() =>
        ParDefaut(Environment.GetEnvironmentVariable,
                  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>Variante testable de <see cref="ParDefaut()"/> : la lecture des variables d'environnement est injectée.</summary>
    internal static DossierDonnees ParDefaut(Func<string, string?> lireVariable, string dossierDonneesLocales)
    {
        var redirection = lireVariable(VariableDonnees);
        return string.IsNullOrWhiteSpace(redirection)
            ? new DossierDonnees(Path.Combine(dossierDonneesLocales, "PressePapiers"))
            : new DossierDonnees(Path.GetFullPath(redirection));
    }

    /// <summary>Crée la racine et les trois sous-dossiers s'ils n'existent pas ; sans effet sur l'existant.</summary>
    public void CreerDossiers()
    {
        Directory.CreateDirectory(Images);
        Directory.CreateDirectory(Sauvegardes);
        Directory.CreateDirectory(Journal);
    }
}
