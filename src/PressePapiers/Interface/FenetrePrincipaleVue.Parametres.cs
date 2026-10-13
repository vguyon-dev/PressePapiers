using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Systeme;

namespace PressePapiers.Interface;

/// <summary>Enregistrement des réglages et actions de la page Paramètres (spec, section 8).</summary>
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Levé après chaque enregistrement réussi des réglages : la fenêtre applique le thème, l'application les autres.</summary>
    public event Action? ParametresModifies;

    /// <summary>
    /// Enregistre le raccourci de secours auprès de Windows ; renvoie le message à montrer si la combinaison est refusée
    /// (déjà prise par une autre application), ou null si elle est enregistrée. Branché par l'application.
    /// </summary>
    public Func<Raccourci, string?>? AppliquerRaccourci { get; set; }

    /// <summary>Crée ou supprime le lancement avec Windows ; lève une exception si le registre refuse. Remplaçable pour l'essai.</summary>
    public Action<bool> AppliquerDemarrage { get; set; } =
        activer => DemarrageWindows.Appliquer(activer, Environment.ProcessPath ?? string.Empty);

    /// <summary>Ouvre un dossier ou un fichier avec l'application associée de Windows. Remplaçable pour l'essai.</summary>
    public Action<string> OuvrirChemin { get; set; } =
        chemin => Process.Start(new ProcessStartInfo(chemin) { UseShellExecute = true })?.Dispose();

    /// <summary>
    /// Enregistre de nouveaux réglages, tirés des réglages en vigueur par <paramref name="modifier"/>, puis prévient les abonnés.
    /// </summary>
    /// <returns>Vrai si le fichier a été écrit ; sinon un message d'erreur est affiché et les réglages restent ceux d'avant.</returns>
    public bool EnregistrerParametres(Func<Parametres, Parametres> modifier)
    {
        var nouveaux = modifier(Reglages);
        if (nouveaux == Reglages)
        {
            return true;
        }

        try
        {
            _parametres.Enregistrer(nouveaux);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible d'enregistrer les réglages.", exception);
            Annoncer(InfoBarSeverity.Error, "Les réglages n'ont pas pu être enregistrés. Le détail est dans le journal.");
            return false;
        }

        var chasseFixeChangee = !nouveaux.ApplicationsChasseFixe.SequenceEqual(Reglages.ApplicationsChasseFixe);
        Reglages = nouveaux;
        if (chasseFixeChangee)
        {
            _chasseFixe = ResumeElement.EnsembleChasseFixe(nouveaux.ApplicationsChasseFixe);
            Rafraichir();
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(LibelleAutreMode)));
        ParametresModifies?.Invoke();
        return true;
    }

    /// <summary>
    /// Valide puis enregistre le raccourci de secours : texte reconnu par <see cref="Raccourci.TryAnalyser"/>, puis accepté par Windows.
    /// </summary>
    /// <returns>Null si le raccourci est enregistré, sinon le message à montrer sous le champ.</returns>
    public string? ChangerRaccourci(string texte)
    {
        if (!Raccourci.TryAnalyser(texte, out var raccourci))
        {
            return "Raccourci non reconnu. Exemple : Ctrl+Alt+Maj+V (Ctrl ou Alt obligatoire, jamais la touche Windows ; "
                   + "touche : lettre, chiffre, F1 à F24 ou Espace).";
        }

        if (AppliquerRaccourci?.Invoke(raccourci) is { } refus)
        {
            return refus;
        }

        return EnregistrerParametres(p => p with { RaccourciSecours = raccourci.ToString() })
            ? null
            : "Le raccourci n'a pas pu être enregistré dans les réglages.";
    }

    /// <summary>Active ou désactive le lancement avec Windows. En cas d'échec, le réglage ne change pas et un message l'explique.</summary>
    public bool ChangerDemarrage(bool activer)
    {
        try
        {
            AppliquerDemarrage(activer);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de modifier le démarrage avec Windows.", exception);
            Annoncer(InfoBarSeverity.Error, "Le démarrage avec Windows n'a pas pu être modifié. Le détail est dans le journal.");
            return false;
        }

        return EnregistrerParametres(p => p with { DemarrageAvecWindows = activer });
    }

    /// <summary>Ouvre le dossier des données dans l'Explorateur.</summary>
    public void OuvrirDossierDonnees() => Ouvrir(DossierDonnees.ParDefaut().Racine);

    /// <summary>Ouvre le fichier du journal le plus récent, ou à défaut le dossier du journal.</summary>
    public void OuvrirJournal()
    {
        var dossier = DossierDonnees.ParDefaut().Journal;
        var recent = Directory.Exists(dossier)
            ? new DirectoryInfo(dossier).EnumerateFiles().OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
            : null;
        if (recent is null || !Ouvrir(recent.FullName))
        {
            Ouvrir(dossier);
        }
    }

    private bool Ouvrir(string chemin)
    {
        try
        {
            OuvrirChemin(chemin);
            return true;
        }
        catch (Exception exception)
        {
            _journal.Avertissement($"Impossible d'ouvrir {chemin}.", exception);
            Annoncer(InfoBarSeverity.Warning, "Impossible d'ouvrir ce dossier ou ce fichier. Le détail est dans le journal.");
            return false;
        }
    }
}
