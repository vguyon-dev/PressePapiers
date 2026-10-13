using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;

namespace PressePapiers.Interface;

/// <summary>
/// Page Paramètres (entrée du volet) : raccourci de secours, démarrage avec Windows, mise en forme par défaut, plafond de
/// Temporaire, thème, applications à chasse fixe, état du hook Win+V, accès aux dossiers. Chaque réglage s'enregistre dès
/// qu'il est modifié, par <see cref="FenetrePrincipaleVue.EnregistrerParametres"/> ; le plafond et la liste des applications
/// à chasse fixe, qui se saisissent au clavier, attendent une courte pause de la frappe (et se valident à la perte du focus
/// et au masquage de la fenêtre).
/// </summary>
public sealed partial class PageParametres : UserControl
{
    /// <summary>Pause de la saisie avant d'enregistrer la liste des applications à chasse fixe.</summary>
    private static readonly TimeSpan _delaiChasseFixe = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Pause avant de valider le plafond : plus longue que celle d'une frappe, pour que des clics répétés sur les flèches
    /// du champ numérique ne proposent pas un plafond à chaque pas.
    /// </summary>
    private static readonly TimeSpan _delaiPlafond = TimeSpan.FromSeconds(1);

    private readonly DispatcherQueueTimer _minuteurChasseFixe;
    private readonly DispatcherQueueTimer _minuteurPlafond;
    private bool _plafondEnCours;
    private FenetrePrincipaleVue? _vue;

    // Vrai pendant le remplissage des contrôles : leurs événements de changement ne sont alors pas des choix de l'utilisateur.
    private bool _chargement;

    /// <summary>Crée la page ; <see cref="Lier"/> la branche sur le modèle de vue.</summary>
    public PageParametres()
    {
        InitializeComponent();
        var file = DispatcherQueue.GetForCurrentThread();
        _minuteurChasseFixe = file.CreateTimer();
        _minuteurChasseFixe.Interval = _delaiChasseFixe;
        _minuteurChasseFixe.IsRepeating = false;
        _minuteurPlafond = file.CreateTimer();
        _minuteurPlafond.Interval = _delaiPlafond;
        _minuteurPlafond.IsRepeating = false;
    }

    /// <summary>Branche la page sur le modèle de vue et affiche les réglages en vigueur.</summary>
    public void Lier(FenetrePrincipaleVue vue)
    {
        _vue = vue;
        ExecutionProtegee.Brancher(_minuteurChasseFixe, vue.Journal, () => EnregistrerChasseFixe(reecrire: false), "l'enregistrement des applications à chasse fixe");
        ExecutionProtegee.Brancher(_minuteurPlafond, vue.Journal, ValiderPlafond, "la validation du plafond");
        vue.FenetreMasquee += SurFenetreMasquee;
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

        // Chaque pas des flèches repousse l'échéance : seule la valeur sur laquelle l'utilisateur s'arrête est validée.
        _minuteurPlafond.Stop();
        _minuteurPlafond.Start();
    }

    private void Plafond_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_chargement && _minuteurPlafond.IsRunning)
        {
            ValiderPlafond();
        }
    }

    // Valide la valeur affichée. Une zone vidée donne NaN : on revient à la valeur enregistrée plutôt que d'inventer un
    // plafond. Un plafond qui supprimerait des éléments passe par la confirmation de la vue ; refusé, il est retiré du champ.
    private void ValiderPlafond()
    {
        _minuteurPlafond.Stop();
        if (_vue is null || _plafondEnCours)
        {
            return;
        }

        if (double.IsNaN(ChampPlafond.Value))
        {
            RestaurerPlafond();
            return;
        }

        var plafond = (int)Math.Clamp(ChampPlafond.Value, 0, 1_000_000);
        if (plafond == _vue.Reglages.PlafondTemporaire)
        {
            return;
        }

        _plafondEnCours = true;
        ExecutionProtegee.Lancer(_vue.Journal, () => ChangerPlafondAsync(plafond), "le changement du plafond");
    }

    private async Task ChangerPlafondAsync(int plafond)
    {
        try
        {
            if (!await _vue!.ChangerPlafondAsync(plafond))
            {
                RestaurerPlafond();
            }
        }
        finally
        {
            _plafondEnCours = false;
        }
    }

    // Le champ reprend le plafond enregistré, sans que ce changement soit pris pour une saisie.
    private void RestaurerPlafond()
    {
        _chargement = true;
        try
        {
            ChampPlafond.Value = _vue!.Reglages.PlafondTemporaire;
        }
        finally
        {
            _chargement = false;
        }
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_chargement && _vue is not null && ChoixTheme.SelectedIndex >= 0)
        {
            var theme = (ThemeApplication)ChoixTheme.SelectedIndex;
            _vue.EnregistrerParametres(p => p with { Theme = theme });
        }
    }

    // La liste s'enregistre 500 ms après la dernière frappe, quand on quitte la zone et quand la fenêtre se masque : fermer
    // la fenêtre pendant la saisie ne perd rien. La zone n'est réécrite qu'à la perte du focus, jamais sous les doigts.
    private void ChasseFixe_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_chargement || _vue is null)
        {
            return;
        }

        _minuteurChasseFixe.Stop();
        _minuteurChasseFixe.Start();
    }

    private void ChasseFixe_LostFocus(object sender, RoutedEventArgs e) => EnregistrerChasseFixe(reecrire: true);

    // Fenêtre masquée : ce qui est en cours de saisie est enregistré ; un plafond encore en attente est validé (sa
    // confirmation ne peut plus s'afficher, donc un plafond qui supprimerait des éléments est abandonné).
    private void SurFenetreMasquee()
    {
        EnregistrerChasseFixe(reecrire: true);
        if (_minuteurPlafond.IsRunning)
        {
            ValiderPlafond();
        }
    }

    private void EnregistrerChasseFixe(bool reecrire)
    {
        _minuteurChasseFixe.Stop();
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

        if (!reecrire)
        {
            return;
        }

        _chargement = true;
        try
        {
            ChampChasseFixe.Text = string.Join(Environment.NewLine, _vue.Reglages.ApplicationsChasseFixe);
        }
        finally
        {
            _chargement = false;
        }
    }

    private void Dossier_Click(object sender, RoutedEventArgs e) => _vue?.OuvrirDossierDonnees();

    private void Journal_Click(object sender, RoutedEventArgs e) => _vue?.OuvrirJournal();
}
