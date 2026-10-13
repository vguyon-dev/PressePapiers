using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using Windows.Storage.Streams;

namespace PressePapiers.Interface;

/// <summary>
/// Une ligne de la liste : le résumé de l'élément (pas son contenu complet) et les textes déjà prêts pour l'affichage
/// (icône, nom, aperçu, application et heure). Créée une fois par rafraîchissement, jamais modifiée ensuite.
/// </summary>
public sealed class ElementVue
{
    private static readonly FontFamily _policeTexte = new("Segoe UI Variable");
    private static readonly FontFamily _policeChasseFixe = new("Cascadia Mono");

    private readonly IJournal _journal;
    private readonly string _description;
    private BitmapImage? _vignette;

    /// <summary>Prépare l'affichage d'un élément.</summary>
    /// <param name="resume">Résumé de l'élément, lu par le dépôt.</param>
    /// <param name="nomOnglet">Nom de son onglet, fourni seulement pour les résultats de recherche (tous onglets confondus).</param>
    /// <param name="chasseFixe">Vrai si l'application d'origine figure dans la liste des applications à police à chasse fixe.</param>
    /// <param name="horloge">Horloge qui fixe « maintenant » pour le format de l'heure.</param>
    /// <param name="journal">Journal où sont notés les échecs de décodage de la vignette.</param>
    internal ElementVue(ElementResume resume, string? nomOnglet, bool chasseFixe, TimeProvider horloge, IJournal journal)
    {
        Resume = resume;
        NomOnglet = nomOnglet;
        _journal = journal;
        Police = chasseFixe ? _policeChasseFixe : _policeTexte;
        Apercu = ResumeElement.Apercu(resume);
        var application = string.IsNullOrWhiteSpace(resume.Application) ? "Application inconnue" : resume.Application;
        var heure = ResumeElement.Heure(resume.DateUsage, horloge);
        Meta = application + " · " + heure;

        // Ce que les lecteurs d'écran annoncent pour la ligne : nom, aperçu sur une seule ligne, application et heure.
        var apercuLu = Apercu.Replace('\n', ' ');
        _description = ANom ? $"{Nom}, {apercuLu}, {application}, {heure}" : $"{apercuLu}, {application}, {heure}";
    }

    /// <summary>Résumé de l'élément ; son contenu complet se lit avec <c>DepotHistorique.Lire</c>.</summary>
    internal ElementResume Resume { get; }

    /// <summary>Identifiant de l'élément.</summary>
    public long Id => Resume.Id;

    /// <summary>Onglet qui contient l'élément.</summary>
    public long OngletId => Resume.OngletId;

    /// <summary>Glyphe de l'icône de type.</summary>
    public string Glyphe => ResumeElement.Glyphe(Resume.Type);

    /// <summary>Nom choisi par l'utilisateur, ou null.</summary>
    public string? Nom => Resume.Nom;

    /// <summary>Vrai si l'élément porte un nom (affiché en gras, l'aperçu ne prend alors qu'une ligne).</summary>
    public bool ANom => !string.IsNullOrWhiteSpace(Resume.Nom);

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
    public bool MiseEnFormeVisible => Resume.Type == TypeElement.MisEnForme;

    /// <summary>Vrai si la ligne montre une vignette (image dont la miniature est connue).</summary>
    public bool VignetteVisible => Resume.Type == TypeElement.Image && Resume.Miniature is { Length: > 0 };

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
                ExecutionProtegee.Lancer(_journal, () => ChargerAsync(_vignette, flux, Resume.Miniature!), "le décodage d'une vignette");
            }

            return _vignette;
        }
    }

    /// <summary>
    /// Nom accessible de la ligne, annoncé par les lecteurs d'écran : l'élément de liste prend le texte de l'objet lié, qui
    /// sinon serait le nom de la classe.
    /// </summary>
    public override string ToString() => _description;

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
