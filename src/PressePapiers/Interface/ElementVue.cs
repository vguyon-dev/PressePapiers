using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PressePapiers.Coeur.Modele;
using Windows.Storage.Streams;

namespace PressePapiers.Interface;

/// <summary>
/// Une ligne de la liste : l'élément du dépôt et les textes déjà prêts pour l'affichage (icône, nom, aperçu,
/// application et heure). Créée une fois par rafraîchissement, jamais modifiée ensuite.
/// </summary>
public sealed class ElementVue
{
    private static readonly FontFamily _policeTexte = new("Segoe UI Variable");
    private static readonly FontFamily _policeChasseFixe = new("Cascadia Mono");

    private BitmapImage? _vignette;

    /// <summary>Prépare l'affichage d'un élément.</summary>
    /// <param name="element">Élément du dépôt.</param>
    /// <param name="nomOnglet">Nom de son onglet, fourni seulement pour les résultats de recherche (tous onglets confondus).</param>
    /// <param name="chasseFixe">Vrai si l'application d'origine figure dans la liste des applications à police à chasse fixe.</param>
    /// <param name="horloge">Horloge qui fixe « maintenant » pour le format de l'heure.</param>
    public ElementVue(Element element, string? nomOnglet, bool chasseFixe, TimeProvider horloge)
    {
        Element = element;
        NomOnglet = nomOnglet;
        Police = chasseFixe ? _policeChasseFixe : _policeTexte;
        Apercu = ResumeElement.Apercu(element);
        Meta = (string.IsNullOrWhiteSpace(element.Application) ? "Application inconnue" : element.Application)
               + " · " + ResumeElement.Heure(element.DateUsage, horloge);
    }

    /// <summary>Élément du dépôt.</summary>
    public Element Element { get; }

    /// <summary>Identifiant de l'élément.</summary>
    public long Id => Element.Id;

    /// <summary>Glyphe de l'icône de type.</summary>
    public string Glyphe => ResumeElement.Glyphe(Element.Type);

    /// <summary>Nom choisi par l'utilisateur, ou null.</summary>
    public string? Nom => Element.Nom;

    /// <summary>Vrai si l'élément porte un nom (affiché en gras, l'aperçu ne prend alors qu'une ligne).</summary>
    public bool ANom => !string.IsNullOrWhiteSpace(Element.Nom);

    /// <summary>Nombre de lignes de l'aperçu : deux, ou une sous un nom.</summary>
    public int LignesApercu => ANom ? 1 : 2;

    /// <summary>Aperçu du contenu.</summary>
    public string Apercu { get; }

    /// <summary>Police de l'aperçu : à chasse fixe pour les terminaux et éditeurs de code.</summary>
    public FontFamily Police { get; }

    /// <summary>Application d'origine et heure.</summary>
    public string Meta { get; }

    /// <summary>Nom de l'onglet, affiché seulement dans les résultats de recherche.</summary>
    public string? NomOnglet { get; }

    /// <summary>Vrai si le nom de l'onglet doit apparaître sur la ligne.</summary>
    public bool OngletVisible => NomOnglet is not null;

    /// <summary>Vrai pour un texte conservé avec sa mise en forme (RTF ou HTML).</summary>
    public bool MiseEnFormeVisible => Element.Type == TypeElement.MisEnForme;

    /// <summary>Vrai si la ligne montre une vignette (image dont la miniature est connue).</summary>
    public bool VignetteVisible => Element.Type == TypeElement.Image && Element.Miniature is { Length: > 0 };

    /// <summary>Vignette de l'image, décodée à la première lecture seulement (donc pour les lignes réellement affichées).</summary>
    public ImageSource? Vignette
    {
        get
        {
            if (!VignetteVisible)
            {
                return null;
            }

            if (_vignette is null)
            {
                _vignette = new BitmapImage { DecodePixelWidth = 128 };
                var flux = new InMemoryRandomAccessStream();
                // Le flux reste vivant jusqu'à la fin du décodage, qui se fait sans bloquer le fil d'interface.
                _ = ChargerAsync(_vignette, flux, Element.Miniature!);
            }

            return _vignette;
        }
    }

    private static async Task ChargerAsync(BitmapImage image, InMemoryRandomAccessStream flux, byte[] octets)
    {
        using (flux)
        {
            await flux.WriteAsync(octets.AsBuffer());
            flux.Seek(0);
            await image.SetSourceAsync(flux);
        }
    }
}
