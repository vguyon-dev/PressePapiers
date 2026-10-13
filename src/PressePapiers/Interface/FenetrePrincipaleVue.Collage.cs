using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>Collage de l'élément choisi dans la fenêtre d'origine, et navigation au clavier dans les onglets et la liste.</summary>
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Fenêtre qui avait le focus quand la fenêtre a été ouverte ; zéro quand elle est inconnue (ouverture depuis l'icône).</summary>
    public nint FenetreOrigine { get; set; }

    /// <summary>Handle de la fenêtre principale, pour que le recollage n'y revienne jamais.</summary>
    public nint HandleOutil { get; set; }

    /// <summary>Masque la fenêtre ; appelée par le recollage au bon moment de sa séquence.</summary>
    public Action MasquerFenetre { get; set; } = () => { };

    /// <summary>Notification de l'icône (titre, message), pour un collage impossible dont la fenêtre est déjà masquée. Branchée par l'application.</summary>
    public Action<string, string>? Notifier { get; set; }

    /// <summary>
    /// Vrai pendant la séquence de recollage : la fenêtre cède le premier plan à la fenêtre d'origine, ce qui ne doit
    /// pas la faire se masquer comme une perte de focus ordinaire.
    /// </summary>
    public bool CollageEnCours { get; private set; }

    /// <summary>
    /// Colle l'élément choisi dans la fenêtre d'origine (ou le copie seulement), puis dit à l'utilisateur ce qui s'est passé
    /// quand la fenêtre reste affichée. À appeler sur le fil d'interface : le recollage y reprend après chaque attente.
    /// </summary>
    public async Task CollerAsync(ModeCollage mode, bool copieSeule)
    {
        if (ElementSelectionne is not { } ligne || CollageEnCours)
        {
            return;
        }

        CollageEnCours = true;
        try
        {
            var issue = await _recollage.CollerAsync(ligne.Element, mode, FenetreOrigine, copieSeule, HandleOutil, MasquerFenetre);
            Informer(issue);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Le collage a échoué.", exception);
            Annoncer(InfoBarSeverity.Error, "Le collage a échoué. Le détail est dans le journal.");
        }
        finally
        {
            CollageEnCours = false;
        }
    }

    /// <summary>Message qui accompagne l'issue d'un collage : rien après un collage réussi, la fenêtre s'est masquée.</summary>
    internal void Informer(IssueCollage issue)
    {
        switch (issue)
        {
            case IssueCollage.CopieDemandee:
                Annoncer(InfoBarSeverity.Success, "Copié dans le presse-papiers.");
                break;
            case IssueCollage.CopieSansFenetreOrigine:
                Annoncer(InfoBarSeverity.Informational,
                         "Copié. La fenêtre d'origine est inconnue (ouverture depuis l'icône) : colle avec Ctrl+V.");
                break;
            case IssueCollage.CopieFenetreFermee:
                Annoncer(InfoBarSeverity.Warning, "Copié. La fenêtre d'origine n'existe plus : colle avec Ctrl+V.");
                break;
            case IssueCollage.CopieFenetreElevee:
                // La fenêtre est déjà masquée : seule la notification de l'icône peut encore le dire.
                Notifier?.Invoke("Presse-papiers", "Copié, colle avec Ctrl+V");
                break;
            case IssueCollage.Echec:
                Annoncer(InfoBarSeverity.Error, "Le collage a échoué. Le détail est dans le journal.");
                break;
        }
    }

    /// <summary>Remet la fenêtre dans l'état d'une ouverture : onglet Temporaire, recherche vide, premier élément choisi.</summary>
    internal void PreparerOuverture(OrigineOuverture origine)
    {
        AnnonceOuverte = false;
        ChoisirOnglet(Onglets.Count > 0 ? Onglets[0] : OngletSelectionne!);

        if (origine is OrigineOuverture.Icone or OrigineOuverture.Relance)
        {
            Annoncer(InfoBarSeverity.Informational,
                     "Ouvert sans fenêtre d'origine : Entrée copie l'élément, colle-le ensuite avec Ctrl+V.");
        }
    }

    /// <summary>Affiche l'onglet dont le numéro est donné (1 pour Temporaire) ; sans effet si cet onglet n'existe pas.</summary>
    public void AllerAOnglet(int numero)
    {
        if (numero >= 1 && numero <= Onglets.Count)
        {
            ChoisirOnglet(Onglets[numero - 1]);
        }
    }

    /// <summary>Passe à l'onglet suivant (+1) ou précédent (-1), en bouclant.</summary>
    public void OngletRelatif(int decalage)
    {
        var courant = Onglets.ToList().FindIndex(o => o.Id == OngletSelectionne?.Id);
        if (courant >= 0)
        {
            var total = Onglets.Count;
            ChoisirOnglet(Onglets[((courant + decalage) % total + total) % total]);
        }
    }

    /// <summary>Déplace le choix dans la liste d'une ligne vers le haut (-1) ou le bas (+1), sans sortir de la liste.</summary>
    public void DeplacerSelection(int decalage)
    {
        if (Elements.Count == 0)
        {
            return;
        }

        var courant = ElementSelectionne is null ? -1 : Elements.ToList().FindIndex(e => e.Id == ElementSelectionne.Id);
        ElementSelectionne = Elements[Math.Clamp(courant + decalage, 0, Elements.Count - 1)];
    }
}
