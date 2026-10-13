using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Natif;
using Windows.Graphics;

namespace PressePapiers.Interface;

// Aspect et position : fond Mica, thème, taille mémorisée, centrage sur l'écran de la fenêtre d'origine.
public sealed partial class FenetrePrincipale
{
    /// <summary>Fond Mica, ou fond uni quand le matériau n'est pas pris en charge (Windows 10, mode économie d'énergie).</summary>
    internal void AppliquerFond(bool mica)
    {
        if (mica)
        {
            SystemBackdrop = new MicaBackdrop();
            Racine.Background = null;
        }
        else
        {
            SystemBackdrop = null;
            Racine.Background = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"];
        }
    }

    /// <summary>Applique le thème des réglages : celui de Windows (Système), clair ou sombre, à toute la fenêtre.</summary>
    internal void AppliquerTheme(ThemeApplication theme)
    {
        Racine.RequestedTheme = theme switch
        {
            ThemeApplication.Clair => ElementTheme.Light,
            ThemeApplication.Sombre => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        // Les boutons de la barre de titre suivent le thème de Windows, pas celui de la fenêtre : un thème clair forcé sur
        // un Windows sombre les rendrait blancs sur blanc. Avec le thème Système, les couleurs par défaut reviennent.
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var barre = AppWindow.TitleBar;
        var (encre, survol, appui) = theme switch
        {
            ThemeApplication.Clair => ((Windows.UI.Color?)Colors.Black, Windows.UI.Color.FromArgb(0x1A, 0, 0, 0), Windows.UI.Color.FromArgb(0x33, 0, 0, 0)),
            ThemeApplication.Sombre => (Colors.White, Windows.UI.Color.FromArgb(0x1A, 255, 255, 255), Windows.UI.Color.FromArgb(0x33, 255, 255, 255)),
            _ => (null, default(Windows.UI.Color), default(Windows.UI.Color)),
        };
        var personnalise = encre is not null;
        barre.ButtonForegroundColor = encre;
        barre.ButtonHoverForegroundColor = encre;
        barre.ButtonPressedForegroundColor = encre;
        barre.ButtonInactiveForegroundColor = personnalise ? Colors.Gray : null;
        barre.ButtonHoverBackgroundColor = personnalise ? survol : null;
        barre.ButtonPressedBackgroundColor = personnalise ? appui : null;
    }

    // Fenêtre de taille réglable mais ni agrandissable ni réductible, et absente d'Alt+Tab : c'est un outil de passage,
    // qui disparaît dès qu'il perd le focus.
    private void AppliquerPresentation()
    {
        if (AppWindow.Presenter is OverlappedPresenter presentateur)
        {
            presentateur.IsMaximizable = false;
            presentateur.IsMinimizable = false;
        }

        AppWindow.IsShownInSwitchers = false;
    }

    // Taille mémorisée en pixels logiques : la même largeur de 920 doit donner la même fenêtre à 100 % et à 150 %.
    private double EchelleAffichage => Content?.XamlRoot?.RasterizationScale ?? 1.0;

    private void AppliquerTailleMemorisee()
    {
        var echelle = EchelleAffichage;
        AppWindow.Resize(new SizeInt32((int)Math.Round(Vue.Reglages.LargeurFenetre * echelle),
                                       (int)Math.Round(Vue.Reglages.HauteurFenetre * echelle)));
    }

    private void MemoriserTaille()
    {
        var echelle = EchelleAffichage;
        Vue.MemoriserTaille((int)Math.Round(AppWindow.Size.Width / echelle), (int)Math.Round(AppWindow.Size.Height / echelle));
    }

    // Au centre de la zone de travail de l'écran de la fenêtre d'origine, à la taille mémorisée de cet écran.
    private void PlacerAuCentre(nint fenetreOrigine)
    {
        var ecran = EcranDe(fenetreOrigine);
        var travail = ecran.WorkArea;
        var echelle = EchelleDe(ecran) ?? EchelleAffichage;
        var largeur = Math.Min((int)Math.Round(Vue.Reglages.LargeurFenetre * echelle), travail.Width);
        var hauteur = Math.Min((int)Math.Round(Vue.Reglages.HauteurFenetre * echelle), travail.Height);
        AppWindow.MoveAndResize(new RectInt32(travail.X + (travail.Width - largeur) / 2,
                                              travail.Y + (travail.Height - hauteur) / 2, largeur, hauteur));
    }

    // Écran de la fenêtre d'origine ; à défaut, celui du curseur (ouverture depuis l'icône), puis l'écran principal.
    private static DisplayArea EcranDe(nint fenetreOrigine)
    {
        try
        {
            if (fenetreOrigine != 0)
            {
                return DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(fenetreOrigine), DisplayAreaFallback.Nearest);
            }

            if (NatifOuverture.GetCursorPos(out var curseur))
            {
                return DisplayArea.GetFromPoint(new PointInt32(curseur.X, curseur.Y), DisplayAreaFallback.Nearest);
            }
        }
        catch (Exception)
        {
            // Une fenêtre détruite entre-temps : l'écran principal fait l'affaire.
        }

        return DisplayArea.Primary;
    }

    // Échelle d'affichage de cet écran (1,5 pour 150 %) : la taille mémorisée est en pixels logiques.
    private static double? EchelleDe(DisplayArea ecran)
    {
        var moniteur = Win32Interop.GetMonitorFromDisplayId(ecran.DisplayId);
        return NatifOuverture.GetDpiForMonitor(moniteur, NatifOuverture.MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 ? dpi / 96.0 : null;
    }
}
