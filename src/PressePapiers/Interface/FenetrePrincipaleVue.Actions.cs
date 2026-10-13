using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>Actions sur les éléments et les onglets (spec, section 9) et message d'information de la fenêtre.</summary>
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Journal de l'application, pour les contrôles de la fenêtre.</summary>
    internal IJournal Journal => _journal;

    /// <summary>Chemin complet du PNG d'un élément image.</summary>
    internal string CheminImage(Element element) => _depot.CheminImage(element);

    /// <summary>Boîtes de dialogue de la fenêtre ; sans elles, une action qui doit confirmer ne fait rien.</summary>
    public IDialogues? Dialogues { get; set; }

    /// <summary>Vrai quand le message de l'<see cref="InfoBar"/> est affiché.</summary>
    [ObservableProperty]
    public partial bool AnnonceOuverte { get; set; }

    /// <summary>Gravité du message affiché.</summary>
    [ObservableProperty]
    public partial InfoBarSeverity AnnonceGravite { get; set; } = InfoBarSeverity.Informational;

    /// <summary>Texte du message affiché.</summary>
    [ObservableProperty]
    public partial string AnnonceMessage { get; set; } = string.Empty;

    /// <summary>Levé pour coller ou copier l'élément choisi : mode de collage voulu, copie seule ou non. Branché par la tâche 27.</summary>
    public event Action<ModeCollage, bool>? CollageDemande;

    /// <summary>Mode de collage qui change celui des réglages : celui de Maj+Entrée.</summary>
    public ModeCollage ModeAlternatif =>
        Reglages.MiseEnFormeParDefaut == ModeCollage.TexteSeul ? ModeCollage.AvecMiseEnForme : ModeCollage.TexteSeul;

    /// <summary>Libellé du bouton qui colle dans l'autre mode.</summary>
    public string LibelleAutreMode => ModeAlternatif == ModeCollage.TexteSeul ? "Texte brut" : "Avec mise en forme";

    /// <summary>Affiche un message en haut de la zone de contenu.</summary>
    public void Annoncer(InfoBarSeverity gravite, string message)
    {
        AnnonceGravite = gravite;
        AnnonceMessage = message;
        AnnonceOuverte = true;
    }

    /// <summary>Demande le collage (ou la copie seule) de l'élément choisi, dans le mode des réglages ou dans l'autre.</summary>
    public void DemanderCollage(bool autreMode, bool copieSeule) =>
        CollageDemande?.Invoke(autreMode ? ModeAlternatif : Reglages.MiseEnFormeParDefaut, copieSeule);

    /// <summary>
    /// Donne un nom à l'élément choisi et le range dans un onglet ; un élément temporaire quitte Temporaire.
    /// Le nom et l'onglet sont requis : sinon un message l'explique et rien n'est écrit.
    /// </summary>
    /// <returns>Vrai si l'élément a été enregistré.</returns>
    public bool Enregistrer(string? nom, long? ongletId)
    {
        if (ElementSelectionne is not { } ligne)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(nom) || ongletId is null)
        {
            var sansOnglet = Onglets.All(o => o.EstTemporaire);
            Annoncer(InfoBarSeverity.Warning, sansOnglet
                ? "Créez d'abord un onglet (Nouvel onglet) : un élément nommé ne reste pas dans Temporaire."
                : "Saisissez un nom et choisissez un onglet pour enregistrer.");
            return false;
        }

        try
        {
            var nomPropre = nom.Trim();
            var onglet = _depot.Nommer(ligne.Id, nomPropre, ongletId.Value);
            var nomOnglet = Onglets.FirstOrDefault(o => o.Id == onglet.OngletId)?.Nom ?? "l'onglet";
            Annoncer(InfoBarSeverity.Success, $"« {nomPropre} » enregistré dans {nomOnglet}.");
            return true;
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible d'enregistrer le nom de l'élément.", exception);
            Annoncer(InfoBarSeverity.Error, "L'élément n'a pas pu être enregistré. Le détail est dans le journal.");
            return false;
        }
    }

    /// <summary>Supprime l'élément choisi : sans confirmation s'il est temporaire, avec confirmation s'il porte un nom.</summary>
    public async Task SupprimerSelectionAsync()
    {
        if (ElementSelectionne is not { } ligne)
        {
            return;
        }

        if (ligne.ANom)
        {
            var confirme = Dialogues is not null && await Dialogues.ConfirmerAsync(
                "Supprimer l'élément ?", $"« {ligne.Nom} » sera supprimé définitivement.", "Supprimer");
            if (!confirme)
            {
                return;
            }
        }

        try
        {
            _depot.Supprimer(ligne.Id);
            Annoncer(InfoBarSeverity.Informational, "Élément supprimé.");
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de supprimer l'élément.", exception);
            Annoncer(InfoBarSeverity.Error, "L'élément n'a pas pu être supprimé. Le détail est dans le journal.");
        }
    }

    /// <summary>Demande un nom, crée l'onglet et l'affiche.</summary>
    public async Task NouvelOngletAsync()
    {
        var nom = Dialogues is null ? null : await Dialogues.DemanderNomAsync("Nouvel onglet", string.Empty, "Créer");
        if (string.IsNullOrWhiteSpace(nom))
        {
            return;
        }

        try
        {
            var onglet = _depot.CreerOnglet(nom);
            Rafraichir();
            ChoisirOnglet(Onglets.First(o => o.Id == onglet.Id));
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de créer l'onglet.", exception);
            Annoncer(InfoBarSeverity.Error, "L'onglet n'a pas pu être créé. Le détail est dans le journal.");
        }
    }

    /// <summary>Demande un nouveau nom pour l'onglet. Temporaire ne se renomme pas.</summary>
    public async Task RenommerOngletAsync(OngletVue onglet)
    {
        if (onglet.EstTemporaire || Dialogues is null)
        {
            return;
        }

        var nom = await Dialogues.DemanderNomAsync("Renommer l'onglet", onglet.Nom, "Renommer");
        if (string.IsNullOrWhiteSpace(nom) || nom == onglet.Nom)
        {
            return;
        }

        try
        {
            _depot.RenommerOnglet(onglet.Id, nom);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de renommer l'onglet.", exception);
            Annoncer(InfoBarSeverity.Error, "L'onglet n'a pas pu être renommé. Le détail est dans le journal.");
        }
    }

    /// <summary>Supprime l'onglet et ses éléments ; un onglet non vide demande confirmation. Temporaire ne se supprime pas.</summary>
    public async Task SupprimerOngletAsync(OngletVue onglet)
    {
        if (onglet.EstTemporaire)
        {
            return;
        }

        if (onglet.Compteur > 0)
        {
            var confirme = Dialogues is not null && await Dialogues.ConfirmerAsync(
                "Supprimer l'onglet ?",
                $"L'onglet « {onglet.Nom} » et ses {onglet.Compteur} éléments seront supprimés définitivement.", "Supprimer");
            if (!confirme)
            {
                return;
            }
        }

        try
        {
            _depot.SupprimerOnglet(onglet.Id);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de supprimer l'onglet.", exception);
            Annoncer(InfoBarSeverity.Error, "L'onglet n'a pas pu être supprimé. Le détail est dans le journal.");
        }
    }

    /// <summary>Décale l'onglet d'une place vers le haut (-1) ou vers le bas (+1) du volet. Temporaire reste en tête.</summary>
    public void DeplacerOnglet(OngletVue onglet, int decalage)
    {
        var position = Onglets.ToList().FindIndex(o => o.Id == onglet.Id);
        if (onglet.EstTemporaire || position < 0)
        {
            return;
        }

        try
        {
            _depot.DeplacerOnglet(onglet.Id, position + decalage);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de déplacer l'onglet.", exception);
        }
    }
}
