using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
