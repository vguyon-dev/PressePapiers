using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;

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

    /// <summary>
    /// Crée ou supprime le lancement avec Windows ; lève une exception si le registre refuse. Remplaçable pour l'essai ;
    /// par défaut, <see cref="GestionDemarrage.Modifier"/> (qui n'écrit jamais dans une version Debug).
    /// </summary>
    public Action<bool> AppliquerDemarrage { get; set; }

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
            // Masquée, la fenêtre ne relit rien : la relecture de l'ouverture suivante applique la nouvelle liste.
            _chasseFixe = ResumeElement.EnsembleChasseFixe(nouveaux.ApplicationsChasseFixe);
            if (Affichee)
            {
                Rafraichir();
            }
            else
            {
                _rafraichissementEnAttente = true;
            }
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(LibelleAutreMode)));
        ParametresModifies?.Invoke();
        return true;
    }

    /// <summary>
    /// Valide puis enregistre le raccourci de secours : texte reconnu par <see cref="Raccourci.TryAnalyser"/>, puis accepté par
    /// Windows. Si Windows l'accepte mais que le fichier des réglages refuse l'écriture, l'ancien raccourci est remis.
    /// </summary>
    /// <returns>Null si le raccourci est enregistré, sinon le message à montrer sous le champ.</returns>
    public string? ChangerRaccourci(string texte)
    {
        switch (Raccourci.Analyser(texte, out var raccourci))
        {
            case ResultatAnalyse.ReserveParWindows:
                return "Windows se réserve cette combinaison (Win+V, Win+Maj+V et Win+Ctrl+V ne peuvent pas être enregistrés) : "
                       + "choisis-en une autre, par exemple Win+Alt+V.";
            case ResultatAnalyse.Invalide:
                return "Raccourci non reconnu. Exemple : Win+Alt+V (au moins deux modificateurs parmi Win, Ctrl, Alt et Maj ; "
                       + "touche : lettre, chiffre, F1 à F24 ou Espace).";
        }

        if (AppliquerRaccourci?.Invoke(raccourci) is { } refus)
        {
            return refus;
        }

        if (EnregistrerParametres(p => p with { RaccourciSecours = raccourci.ToString() }))
        {
            return null;
        }

        RemettreRaccourci();
        return "Le raccourci n'a pas pu être enregistré dans les réglages : l'ancien reste actif.";
    }

    // Windows a accepté une combinaison que les réglages n'ont pas gardée : sans ce retour, elle serait active jusqu'au
    // prochain démarrage seulement, sans que les réglages l'indiquent.
    private void RemettreRaccourci()
    {
        if (!Raccourci.TryAnalyser(Reglages.RaccourciSecours, out var ancien))
        {
            Raccourci.TryAnalyser(new Parametres().RaccourciSecours, out ancien);
        }

        if (AppliquerRaccourci?.Invoke(ancien) is { } refus)
        {
            _journal.Avertissement($"L'ancien raccourci de secours {ancien} n'a pas pu être remis : {refus}");
        }
    }

    /// <summary>
    /// Active ou désactive le lancement avec Windows. En cas d'échec, le réglage ne change pas et un message l'explique ;
    /// si le registre est modifié mais que le fichier des réglages refuse l'écriture, le registre est remis dans son état.
    /// </summary>
    public bool ChangerDemarrage(bool activer)
    {
        var avant = Reglages.DemarrageAvecWindows;
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

        if (EnregistrerParametres(p => p with { DemarrageAvecWindows = activer }))
        {
            return true;
        }

        try
        {
            AppliquerDemarrage(avant);
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Le démarrage avec Windows n'a pas pu être remis dans son état d'avant.", exception);
        }

        return false;
    }

    /// <summary>Nombre d'éléments de Temporaire qu'un plafond de cette valeur ferait supprimer tout de suite (0 : aucun, ou plafond désactivé).</summary>
    public int ElementsAuDessusDuPlafond(int plafond)
    {
        if (plafond <= 0 || Onglets.FirstOrDefault(o => o.EstTemporaire) is not { } temporaire)
        {
            return 0;
        }

        try
        {
            return Math.Max(0, _depot.CompterElements(temporaire.Id) - plafond);
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Impossible de compter les éléments de Temporaire.", exception);
            return 0;
        }
    }

    /// <summary>
    /// Change le plafond de Temporaire. Un plafond qui supprimerait des éléments demande d'abord confirmation, avec leur nombre ;
    /// sans confirmation, rien n'est enregistré et rien n'est supprimé.
    /// </summary>
    /// <returns>Vrai si le plafond est enregistré ; faux s'il est refusé ou si l'écriture a échoué.</returns>
    public async Task<bool> ChangerPlafondAsync(int plafond)
    {
        if (plafond == Reglages.PlafondTemporaire)
        {
            return true;
        }

        var aSupprimer = ElementsAuDessusDuPlafond(plafond);
        if (aSupprimer > 0)
        {
            var pluriel = aSupprimer > 1 ? "s" : string.Empty;
            var confirme = Dialogues is not null && await Dialogues.ConfirmerAsync(
                "Abaisser le plafond ?",
                $"Avec un plafond de {plafond}, {aSupprimer} élément{pluriel} temporaire{pluriel} parmi les plus anciens "
                + $"{(aSupprimer > 1 ? "seront supprimés" : "sera supprimé")} définitivement.",
                "Supprimer");
            if (!confirme)
            {
                return false;
            }
        }

        return EnregistrerParametres(p => p with { PlafondTemporaire = plafond });
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
