using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>Collage de l'élément choisi dans la fenêtre d'origine, et navigation au clavier dans les onglets et la liste.</summary>
public sealed partial class FenetrePrincipaleVue
{
    private const string TitreNotification = "Presse-papiers";

    /// <summary>Fenêtre qui avait le focus quand la fenêtre a été ouverte ; zéro quand elle est inconnue (ouverture depuis l'icône).</summary>
    public nint FenetreOrigine { get; set; }

    /// <summary>Handle de la fenêtre principale, pour que le recollage n'y revienne jamais.</summary>
    public nint HandleOutil { get; set; }

    /// <summary>Masque la fenêtre ; appelée par le recollage au bon moment de sa séquence.</summary>
    public Action MasquerFenetre { get; set; } = () => { };

    /// <summary>Notification de l'icône (titre, message), pour un collage dont la fenêtre est déjà masquée. Branchée par l'application.</summary>
    public Action<string, string>? Notifier { get; set; }

    /// <summary>
    /// Vrai pendant la séquence de recollage : la fenêtre cède le premier plan à la fenêtre d'origine, ce qui ne doit
    /// pas la faire se masquer comme une perte de focus ordinaire.
    /// </summary>
    public bool CollageEnCours { get; private set; }

    /// <summary>
    /// Colle l'élément choisi dans la fenêtre d'origine (ou le copie seulement), puis dit à l'utilisateur ce qui s'est passé :
    /// dans la fenêtre si elle reste affichée, par la notification de l'icône si elle est déjà masquée. À appeler sur le fil
    /// d'interface : le recollage y reprend après chaque attente. L'élément complet n'est lu qu'ici, pas dans la liste.
    /// </summary>
    public async Task CollerAsync(ModeCollage mode, bool copieSeule)
    {
        if (ElementSelectionne is not { } ligne || CollageEnCours)
        {
            return;
        }

        CollageEnCours = true;
        var masquee = false;
        try
        {
            var element = LireElement(ligne.Id);
            if (element is null)
            {
                Annoncer(InfoBarSeverity.Warning, "Cet élément n'existe plus.");
                return;
            }

            var issue = await _recollage.CollerAsync(element, mode, FenetreOrigine, copieSeule, HandleOutil, () =>
            {
                masquee = true;
                MasquerFenetre();
            });
            Informer(issue, masquee || !Affichee);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Le collage a échoué.", exception);
            Informer(IssueCollage.Echec, masquee || !Affichee);
        }
        finally
        {
            CollageEnCours = false;
        }
    }

    /// <summary>
    /// Message qui accompagne l'issue d'un collage : rien après un collage réussi. Fenêtre masquée, seule la notification
    /// de l'icône peut encore parler : aucune issue autre que <see cref="IssueCollage.Colle"/> ne reste muette.
    /// </summary>
    /// <param name="issue">Issue rendue par le recollage.</param>
    /// <param name="fenetreMasquee">Vrai si la fenêtre n'est plus affichée.</param>
    internal void Informer(IssueCollage issue, bool fenetreMasquee)
    {
        if (issue == IssueCollage.Colle)
        {
            return;
        }

        const string bulleCopie = "Copié, colle avec Ctrl+V";
        const string bulleImpossible = "Collage impossible, l'élément est copié : colle avec Ctrl+V";
        var (gravite, message, bulle) = issue switch
        {
            IssueCollage.CopieDemandee => (InfoBarSeverity.Success, "Copié dans le presse-papiers.", "Copié dans le presse-papiers"),
            IssueCollage.CopieSansFenetreOrigine => (InfoBarSeverity.Informational,
                "Copié. La fenêtre d'origine est inconnue (ouverture depuis l'icône) : colle avec Ctrl+V.", bulleCopie),
            IssueCollage.CopieFenetreFermee => (InfoBarSeverity.Warning,
                "Copié. La fenêtre d'origine n'existe plus : colle avec Ctrl+V.", bulleCopie),
            IssueCollage.CopieFenetreElevee => (InfoBarSeverity.Informational,
                "Copié. La fenêtre d'origine est lancée en administrateur : colle avec Ctrl+V.", bulleCopie),
            IssueCollage.CollageNonEnvoye => (InfoBarSeverity.Warning, bulleImpossible, bulleImpossible),
            IssueCollage.Echec => (InfoBarSeverity.Error, "Le collage a échoué. Le détail est dans le journal.", bulleImpossible),

            // Toute issue future non prévue ici reste annoncée comme une copie sans collage, jamais muette.
            _ => (InfoBarSeverity.Warning, bulleImpossible, bulleImpossible),
        };

        if (!fenetreMasquee)
        {
            Annoncer(gravite, message);
            return;
        }

        try
        {
            Notifier?.Invoke(TitreNotification, bulle);
        }
        catch (Exception exception)
        {
            _journal.Avertissement("La notification du collage n'a pas pu être affichée.", exception);
        }
    }

    /// <summary>Remet la fenêtre dans l'état d'une ouverture : onglet Temporaire, recherche vide, premier élément choisi.</summary>
    internal void PreparerOuverture(OrigineOuverture origine, bool messageOrigine = true)
    {
        AnnonceOuverte = false;

        // Une seule relecture : Temporaire est toujours le premier onglet, et ChoisirOnglet relit tout.
        if (Onglets.Count > 0)
        {
            ChoisirOnglet(Onglets[0]);
        }
        else
        {
            ChangerRecherche(string.Empty);
            Rafraichir(premierElement: true);
        }

        if (messageOrigine && origine is OrigineOuverture.Icone or OrigineOuverture.Relance)
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
