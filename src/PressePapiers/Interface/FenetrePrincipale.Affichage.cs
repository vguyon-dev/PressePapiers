using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
}
