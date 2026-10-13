namespace PressePapiers.Coeur.Stockage;

/// <summary>Emplacements des données de l'application. Coquille : remplacée par la tâche 2.</summary>
/// <param name="Racine">Dossier racine des données.</param>
public sealed record DossierDonnees(string Racine)
{
    /// <summary>Fichier de la base : <c>Racine\historique.db</c>.</summary>
    public string Base => throw new NotImplementedException("Tâche 2");

    /// <summary>Dossier des images PNG : <c>Racine\images</c>.</summary>
    public string Images => throw new NotImplementedException("Tâche 2");

    /// <summary>Dossier des copies de sécurité : <c>Racine\sauvegardes</c>.</summary>
    public string Sauvegardes => throw new NotImplementedException("Tâche 2");

    /// <summary>Dossier du journal : <c>Racine\journal</c>.</summary>
    public string Journal => throw new NotImplementedException("Tâche 2");

    /// <summary>Fichier des réglages : <c>Racine\parametres.json</c>.</summary>
    public string Parametres => throw new NotImplementedException("Tâche 2");

    /// <summary>
    /// <c>%LOCALAPPDATA%\PressePapiers</c>, ou le dossier de la variable d'environnement <c>PRESSEPAPIERS_DONNEES</c>
    /// si elle est définie (essais et recette sans toucher aux données réelles).
    /// </summary>
    public static DossierDonnees ParDefaut() => throw new NotImplementedException("Tâche 2");

    /// <summary>Variante testable de <see cref="ParDefaut()"/> : la lecture des variables d'environnement est injectée.</summary>
    internal static DossierDonnees ParDefaut(Func<string, string?> lireVariable, string dossierDonneesLocales) =>
        throw new NotImplementedException("Tâche 2");

    /// <summary>Crée la racine et les trois sous-dossiers s'ils n'existent pas.</summary>
    public void CreerDossiers() => throw new NotImplementedException("Tâche 2");
}
