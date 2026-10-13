using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;

namespace PressePapiers.Interface;

/// <summary>
/// Page Paramètres (entrée du volet) : raccourci de secours, démarrage avec Windows, mise en forme par défaut, plafond de
/// Temporaire, thème, applications à chasse fixe, état du hook Win+V, accès aux dossiers. Chaque réglage s'enregistre dès
/// qu'il est modifié, par <see cref="FenetrePrincipaleVue.EnregistrerParametres"/>.
/// </summary>
public sealed partial class PageParametres : UserControl
{
    private FenetrePrincipaleVue? _vue;

    // Vrai pendant le remplissage des contrôles : leurs événements de changement ne sont alors pas des choix de l'utilisateur.
    private bool _chargement;

    /// <summary>Crée la page ; <see cref="Lier"/> la branche sur le modèle de vue.</summary>
    public PageParametres()
    {
        InitializeComponent();
    }

    /// <summary>Branche la page sur le modèle de vue et affiche les réglages en vigueur.</summary>
    public void Lier(FenetrePrincipaleVue vue)
    {
        _vue = vue;
        vue.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FenetrePrincipaleVue.EtatCrochet))
            {
                EtatCrochet.Text = vue.EtatCrochet;
            }
        };
        Charger();
    }

    /// <summary>Remplit les contrôles avec les réglages en vigueur.</summary>
    public void Charger()
    {
        if (_vue is null)
        {
            return;
        }

        var reglages = _vue.Reglages;
        _chargement = true;
        try
        {
            ChampRaccourci.Text = reglages.RaccourciSecours;
            InterrupteurDemarrage.IsOn = reglages.DemarrageAvecWindows;
            ChoixMiseEnForme.SelectedIndex = (int)reglages.MiseEnFormeParDefaut;
            ChampPlafond.Value = reglages.PlafondTemporaire;
            ChoixTheme.SelectedIndex = (int)reglages.Theme;
            ChampChasseFixe.Text = string.Join(Environment.NewLine, reglages.ApplicationsChasseFixe);
            EtatCrochet.Text = _vue.EtatCrochet;
            MessageRaccourci.Visibility = Visibility.Collapsed;
            ErreurRaccourci.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _chargement = false;
        }
    }

    private void Raccourci_Click(object sender, RoutedEventArgs e) => AppliquerRaccourci();

    private void ChampRaccourci_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            AppliquerRaccourci();
        }
    }

    private void AppliquerRaccourci()
    {
        if (_vue is null)
        {
            return;
        }

        var erreur = _vue.ChangerRaccourci(ChampRaccourci.Text);
        if (erreur is null)
        {
            ChampRaccourci.Text = _vue.Reglages.RaccourciSecours;
            MontrerMessageRaccourci($"Raccourci de secours actif : {_vue.Reglages.RaccourciSecours}.", erreur: false);
        }
        else
        {
            MontrerMessageRaccourci(erreur, erreur: true);
        }
    }

    // Deux zones de texte plutôt qu'un pinceau posé par le code : les pinceaux de thème doivent venir du XAML pour suivre
    // le thème clair ou sombre de la fenêtre.
    private void MontrerMessageRaccourci(string message, bool erreur)
    {
        var cible = erreur ? ErreurRaccourci : MessageRaccourci;
        var autre = erreur ? MessageRaccourci : ErreurRaccourci;
        cible.Text = message;
        cible.Visibility = Visibility.Visible;
        autre.Visibility = Visibility.Collapsed;
    }

    private void Demarrage_Toggled(object sender, RoutedEventArgs e)
    {
        if (_chargement || _vue is null || _vue.ChangerDemarrage(InterrupteurDemarrage.IsOn))
        {
            return;
        }

        // Le registre a refusé : l'interrupteur revient à l'état réel.
        _chargement = true;
        InterrupteurDemarrage.IsOn = !InterrupteurDemarrage.IsOn;
        _chargement = false;
    }

    private void MiseEnForme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_chargement && _vue is not null && ChoixMiseEnForme.SelectedIndex >= 0)
        {
            var mode = (ModeCollage)ChoixMiseEnForme.SelectedIndex;
            _vue.EnregistrerParametres(p => p with { MiseEnFormeParDefaut = mode });
        }
    }

    private void Plafond_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_chargement || _vue is null)
        {
            return;
        }

        // Une zone vidée donne NaN : on revient à la valeur enregistrée plutôt que d'inventer un plafond.
        if (double.IsNaN(args.NewValue))
        {
            _chargement = true;
            sender.Value = _vue.Reglages.PlafondTemporaire;
            _chargement = false;
            return;
        }

        var plafond = (int)Math.Clamp(args.NewValue, 0, 1_000_000);
        _vue.EnregistrerParametres(p => p with { PlafondTemporaire = plafond });
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_chargement && _vue is not null && ChoixTheme.SelectedIndex >= 0)
        {
            var theme = (ThemeApplication)ChoixTheme.SelectedIndex;
            _vue.EnregistrerParametres(p => p with { Theme = theme });
        }
    }

    // La liste s'enregistre quand on quitte la zone (et non à chaque frappe) : un nom à moitié tapé ne ferait rien de bon.
    private void ChasseFixe_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_chargement || _vue is null)
        {
            return;
        }

        var noms = ChampChasseFixe.Text
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!noms.SequenceEqual(_vue.Reglages.ApplicationsChasseFixe))
        {
            _vue.EnregistrerParametres(p => p with { ApplicationsChasseFixe = noms });
        }

        ChampChasseFixe.Text = string.Join(Environment.NewLine, _vue.Reglages.ApplicationsChasseFixe);
    }

    private void Dossier_Click(object sender, RoutedEventArgs e) => _vue?.OuvrirDossierDonnees();

    private void Journal_Click(object sender, RoutedEventArgs e) => _vue?.OuvrirJournal();
}
