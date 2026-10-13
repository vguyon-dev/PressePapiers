using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PressePapiers.Interface;

// Volet des onglets : synchronisation avec le modèle de vue, invocation par l'utilisateur, menu contextuel.
public sealed partial class FenetrePrincipale
{
    // Aligne les entrées du volet sur les onglets du modèle de vue : reconstruction seulement si la liste a changé.
    private void SynchroniserVolet()
    {
        var onglets = Vue.Onglets;
        var anciens = EntreesOnglets().Select(i => (OngletVue)i.Tag).ToList();
        if (OngletVue.StructureDifferente(anciens, onglets))
        {
            ReconstruireVolet(onglets);
        }

        var entrees = EntreesOnglets();
        for (var i = 0; i < entrees.Count; i++)
        {
            entrees[i].Tag = onglets[i];
            ((InfoBadge)entrees[i].InfoBadge).Value = onglets[i].Compteur;
        }

        // L'entrée Paramètres garde la sélection tant que la page est voulue, même avant le premier chargement du volet.
        Navigation.SelectedItem = _parametresVoulus
            ? Navigation.SettingsItem
            : entrees.FirstOrDefault(i => ((OngletVue)i.Tag).Id == Vue.OngletSelectionne?.Id);
        AfficherPage(_parametresVoulus);
    }

    // Montre la liste des éléments ou la page Paramètres.
    private void AfficherPage(bool parametres)
    {
        ZoneHistorique.Visibility = parametres ? Visibility.Collapsed : Visibility.Visible;
        ZoneParametres.Visibility = parametres ? Visibility.Visible : Visibility.Collapsed;
    }

    private List<NavigationViewItem> EntreesOnglets() =>
        Navigation.MenuItems.OfType<NavigationViewItem>().Where(i => i.Tag is OngletVue).ToList();

    private void ReconstruireVolet(IReadOnlyList<OngletVue> onglets)
    {
        var policeIcones = new FontFamily("Segoe Fluent Icons");
        Navigation.MenuItems.Clear();
        foreach (var onglet in onglets)
        {
            var entree = new NavigationViewItem
            {
                Content = onglet.Nom,
                Tag = onglet,
                Icon = new FontIcon { Glyph = onglet.EstTemporaire ? "\uE121" : "\uE8B7", FontFamily = policeIcones },
                InfoBadge = new InfoBadge
                {
                    Value = onglet.Compteur,
                    Style = (Style)Application.Current.Resources["InformationalValueInfoBadgeStyle"],
                },
            };
            if (!onglet.EstTemporaire)
            {
                entree.ContextFlyout = CreerMenuOnglet(entree);
            }

            Navigation.MenuItems.Add(entree);
        }

        Navigation.MenuItems.Add(new NavigationViewItemSeparator());
        Navigation.MenuItems.Add(new NavigationViewItem
        {
            Content = "Nouvel onglet",
            Tag = "nouvel-onglet",
            SelectsOnInvoked = false,
            Icon = new FontIcon { Glyph = "\uE710", FontFamily = policeIcones },
        });
    }

    // L'utilisateur choisit une entrée du volet (clic, ou Entrée au clavier) : un onglet, les Paramètres, ou « Nouvel onglet »,
    // qui ne se sélectionne pas et ouvre la boîte de saisie du nom. Seule cette invocation change d'onglet : la sélection posée
    // par le code (ouverture, Ctrl+Tab, relecture) ne passe jamais par ici, ce qui évite les allers-retours.
    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            _parametresVoulus = true;
            AfficherPage(parametres: true);
            return;
        }

        switch (args.InvokedItemContainer?.Tag)
        {
            case "nouvel-onglet":
                ExecutionProtegee.Lancer(Vue.Journal, Vue.NouvelOngletAsync, "la création d'un onglet");
                break;
            case OngletVue onglet:
                _parametresVoulus = false;
                AfficherPage(parametres: false);
                Vue.ChoisirOnglet(onglet);
                break;
        }
    }

    // Clic droit sur un onglet de l'utilisateur : renommer, supprimer, changer de place. Temporaire n'a pas de menu.
    private MenuFlyout CreerMenuOnglet(NavigationViewItem entree)
    {
        OngletVue Onglet() => (OngletVue)entree.Tag;
        var menu = new MenuFlyout();
        var renommer = new MenuFlyoutItem { Text = "Renommer" };
        renommer.Click += (_, _) =>
            ExecutionProtegee.Lancer(Vue.Journal, () => Vue.RenommerOngletAsync(Onglet()), "le renommage d'un onglet");
        var supprimer = new MenuFlyoutItem { Text = "Supprimer" };
        supprimer.Click += (_, _) =>
            ExecutionProtegee.Lancer(Vue.Journal, () => Vue.SupprimerOngletAsync(Onglet()), "la suppression d'un onglet");
        var monter = new MenuFlyoutItem { Text = "Monter" };
        monter.Click += (_, _) => Vue.DeplacerOnglet(Onglet(), -1);
        var descendre = new MenuFlyoutItem { Text = "Descendre" };
        descendre.Click += (_, _) => Vue.DeplacerOnglet(Onglet(), 1);
        menu.Items.Add(renommer);
        menu.Items.Add(supprimer);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(monter);
        menu.Items.Add(descendre);
        return menu;
    }

    // Revient à la liste des éléments si la page Paramètres était ouverte.
    private void MontrerHistorique()
    {
        _parametresVoulus = false;
        AfficherPage(parametres: false);
        Navigation.SelectedItem = EntreesOnglets().FirstOrDefault(i => ((OngletVue)i.Tag).Id == Vue.OngletSelectionne?.Id);
    }
}
