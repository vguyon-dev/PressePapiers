using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PressePapiers.Coeur.Clavier;
using CoreVirtualKeyStates = Windows.UI.Core.CoreVirtualKeyStates;
using VirtualKey = Windows.System.VirtualKey;

namespace PressePapiers.Interface;

// Clavier : la table des touches de la spec (section 8) appliquée à la fenêtre, double-clic, saisie depuis la liste.
public sealed partial class FenetrePrincipale
{
    private void Racine_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var commande = TableDesTouches.Interpreter(
            (uint)e.Key, Enfoncee(VirtualKey.Control), Enfoncee(VirtualKey.Shift), Enfoncee(VirtualKey.Menu),
            ZoneDuFocus(), string.IsNullOrEmpty(Vue.Recherche), Vue.Dialogues?.EnCours == true);

        // Sur la page Paramètres, la liste est masquée : ses touches n'ont rien à faire.
        var surParametres = ZoneParametres.Visibility == Visibility.Visible;
        if (commande.Action == ActionTouche.Aucune
            || (surParametres && commande.Action is not (ActionTouche.Masquer or ActionTouche.OngletSuivant
                                                          or ActionTouche.OngletPrecedent or ActionTouche.AllerAOnglet)))
        {
            return;
        }

        e.Handled = true;
        switch (commande.Action)
        {
            case ActionTouche.Coller:
                Vue.DemanderCollage(autreMode: false, copieSeule: false);
                break;
            case ActionTouche.CollerAutreMode:
                Vue.DemanderCollage(autreMode: true, copieSeule: false);
                break;
            case ActionTouche.CopierSeulement:
                Vue.DemanderCollage(autreMode: false, copieSeule: true);
                break;
            case ActionTouche.AllerAuNom:
                PanneauApercu.FocaliserNom();
                break;
            case ActionTouche.SupprimerElement:
                _ = Vue.SupprimerSelectionAsync();
                break;
            case ActionTouche.OngletSuivant:
                Vue.OngletRelatif(1);
                MontrerHistorique();
                break;
            case ActionTouche.OngletPrecedent:
                Vue.OngletRelatif(-1);
                MontrerHistorique();
                break;
            case ActionTouche.AllerAOnglet:
                Vue.AllerAOnglet(commande.Numero);
                MontrerHistorique();
                break;
            case ActionTouche.EffacerRecherche:
                Vue.EffacerRecherche();
                break;
            case ActionTouche.Masquer:
                Masquer();
                break;
            case ActionTouche.SelectionPrecedente:
            case ActionTouche.SelectionSuivante:
                Vue.DeplacerSelection(commande.Action == ActionTouche.SelectionPrecedente ? -1 : 1);
                if (Vue.ElementSelectionne is not null)
                {
                    Liste.ScrollIntoView(Vue.ElementSelectionne);
                }

                break;
        }
    }

    // Double-clic sur un élément : coller, comme Entrée.
    private void Liste_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (Vue.ElementSelectionne is not null)
        {
            Vue.DemanderCollage(autreMode: false, copieSeule: false);
        }
    }

    // Saisie depuis la liste : le caractère part dans la zone de recherche, qui reprend le focus.
    private void Liste_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (char.IsControl(args.Character))
        {
            return;
        }

        args.Handled = true;
        Vue.Recherche += args.Character;
        ChampRecherche.Focus(FocusState.Programmatic);
        if (Racine.XamlRoot is not null)
        {
            var champ = ContenuDe<TextBox>(ChampRecherche);
            if (champ is not null)
            {
                champ.SelectionStart = champ.Text.Length;
            }
        }
    }

    private static bool Enfoncee(VirtualKey touche) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(touche).HasFlag(CoreVirtualKeyStates.Down);

    // Où est le focus : le sens d'Entrée, de Suppr et des flèches en dépend.
    private ZoneFocus ZoneDuFocus()
    {
        if (Content?.XamlRoot is null || FocusManager.GetFocusedElement(Content.XamlRoot) is not DependencyObject courant)
        {
            return ZoneFocus.Autre;
        }

        for (DependencyObject? noeud = courant; noeud is not null; noeud = VisualTreeHelper.GetParent(noeud))
        {
            if (ReferenceEquals(noeud, ChampRecherche))
            {
                return ZoneFocus.Recherche;
            }

            if (ReferenceEquals(noeud, Liste))
            {
                return ZoneFocus.Liste;
            }

            if (PanneauApercu.ContientChampNom(noeud))
            {
                return ZoneFocus.ChampNom;
            }
        }

        return ZoneFocus.Autre;
    }

    private static T? ContenuDe<T>(DependencyObject racine) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
        {
            var enfant = VisualTreeHelper.GetChild(racine, i);
            if (enfant is T direct)
            {
                return direct;
            }

            if (ContenuDe<T>(enfant) is { } plusBas)
            {
                return plusBas;
            }
        }

        return default;
    }
}
