using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using PressePapiers.Coeur.Modele;
using Windows.Storage.Streams;
using Windows.System;

namespace PressePapiers.Interface;

/// <summary>
/// Colonne de droite de la fenêtre : commandes, nom et onglet de rangement, contenu complet de l'élément choisi
/// (texte, RTF, image, liste de fichiers), application d'origine et date.
/// </summary>
public sealed partial class VueApercu : UserControl
{
    // Un TextBlock ne digère pas des mégaoctets : au-delà, l'aperçu est tronqué (le collage reste complet).
    private const int CaracteresAffiches = 100_000;

    // Largeur de décodage des grandes images : assez pour une zone d'aperçu large, sans décoder une capture 4K entière.
    private const int LargeurMaxDecodee = 1000;

    private FenetrePrincipaleVue? _vue;
    private (long Id, string? Nom, long OngletId, TypeElement Type)? _affiche;
    private List<OngletVue> _ongletsProposes = [];

    /// <summary>Crée l'aperçu, vide : <see cref="Lier"/> le branche sur le modèle de vue.</summary>
    public VueApercu()
    {
        InitializeComponent();
        Afficher(null);
    }

    /// <summary>Branche l'aperçu sur le modèle de vue : il suit l'élément choisi et la liste des onglets.</summary>
    public void Lier(FenetrePrincipaleVue vue)
    {
        _vue = vue;
        vue.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FenetrePrincipaleVue.ElementSelectionne))
            {
                Afficher(vue.ElementSelectionne);
            }
        };
        vue.Rafraichi += SynchroniserOnglets;

        // Changer le mode par défaut dans les Paramètres change le libellé du bouton d'autre mode.
        vue.ParametresModifies += () => BoutonAutreMode.Label = vue.LibelleAutreMode;
        SynchroniserOnglets();
        Afficher(vue.ElementSelectionne);
    }

    /// <summary>Levé après l'enregistrement d'un nom et d'un onglet : la fenêtre redonne alors le focus à la recherche.</summary>
    public event Action? ElementEnregistre;

    /// <summary>Vrai si ce nœud de l'arbre visuel est le champ Nom : ses touches (Entrée, flèches) lui appartiennent.</summary>
    internal bool ContientChampNom(DependencyObject noeud) => ReferenceEquals(noeud, ChampNom);

    /// <summary>Place le curseur dans le champ Nom (F2 et bouton Nommer).</summary>
    public void FocaliserNom()
    {
        ChampNom.Focus(FocusState.Programmatic);
        ChampNom.SelectAll();
    }

    // L'élément complet n'est lu qu'ici, à la sélection : la liste ne garde que des résumés.
    private void Afficher(ElementVue? ligne)
    {
        // Une relecture du dépôt recrée les lignes : si c'est le même élément, rien ne change, et un nom en cours
        // de saisie n'est pas effacé. Le type fait partie de la clé : un doublon enrichi de sa mise en forme change de
        // type (texte brut devenu mis en forme) et doit rafraîchir l'aperçu.
        (long, string?, long, TypeElement)? cle = ligne is null ? null : (ligne.Id, ligne.Nom, ligne.OngletId, ligne.Resume.Type);
        if (cle is not null && cle == _affiche)
        {
            return;
        }

        var element = ligne is null ? null : _vue?.LireElement(ligne.Id);
        _affiche = element is null ? null : cle;
        Vider();

        var present = element is not null;
        Vide.Visibility = present ? Visibility.Collapsed : Visibility.Visible;
        foreach (var bouton in new[] { BoutonColler, BoutonAutreMode, BoutonNommer, BoutonSupprimer, BoutonEnregistrer })
        {
            bouton.IsEnabled = present;
        }

        ChampNom.IsEnabled = ChoixOnglet.IsEnabled = present;
        BoutonAutreMode.Visibility = element?.Type == TypeElement.MisEnForme ? Visibility.Visible : Visibility.Collapsed;
        BoutonAutreMode.Label = _vue?.LibelleAutreMode ?? "Texte brut";
        ChampNom.Text = element?.Nom ?? string.Empty;
        ChoixOnglet.SelectedItem = _ongletsProposes.FirstOrDefault(o => o.Id == element?.OngletId);
        Meta.Text = element is null ? string.Empty : Description(element);

        if (element is null || ligne is null)
        {
            return;
        }

        switch (element.Type)
        {
            case TypeElement.Image:
                ExecutionProtegee.Lancer(_vue!.Journal, () => AfficherImageAsync(element), "l'affichage de l'image");
                break;
            case TypeElement.Fichiers:
                ListeFichiers.ItemsSource = element.Fichiers;
                DefilementFichiers.Visibility = Visibility.Visible;
                break;
            case TypeElement.MisEnForme when element.Rtf is { Length: > 0 } rtf && RenduRtf.Charger(ApercuRiche, rtf, _vue?.Journal):
                ApercuRiche.Visibility = Visibility.Visible;
                break;
            case TypeElement.MisEnForme:
                // Pas de RTF exploitable (page web en HTML seul, RTF refusé) : texte brut, la mise en forme reste pour le collage.
                AfficherTexte(element, ligne, "Mise en forme conservée pour le collage.");
                break;
            default:
                AfficherTexte(element, ligne, null);
                break;
        }
    }

    // Masque tous les blocs et libère leur contenu : fenêtre masquée ou aucun élément choisi, le texte, l'image et le RTF
    // d'un gros élément ne restent pas en mémoire.
    private void Vider()
    {
        if (ApercuRiche.Visibility == Visibility.Visible)
        {
            RenduRtf.Vider(ApercuRiche);
        }

        ApercuTexte.Visibility = Visibility.Collapsed;
        DefilementTexte.Visibility = Visibility.Collapsed;
        ApercuRiche.Visibility = Visibility.Collapsed;
        ZoneImage.Visibility = Visibility.Collapsed;
        DefilementFichiers.Visibility = Visibility.Collapsed;
        Mention.Text = string.Empty;
        ApercuTexte.Text = string.Empty;
        ApercuImage.Source = null;
        ListeFichiers.ItemsSource = null;
    }

    private void AfficherTexte(Element element, ElementVue ligne, string? mention)
    {
        var texte = element.Texte ?? string.Empty;
        if (texte.Length > CaracteresAffiches)
        {
            mention = $"Aperçu limité aux {Nombre(CaracteresAffiches)} premiers caractères ({Nombre(texte.Length)} au total) ; "
                      + "le collage porte sur le texte entier.";
            texte = texte[..CaracteresAffiches];
        }

        ApercuTexte.Text = texte;
        ApercuTexte.FontFamily = ligne.Police;
        Mention.Text = mention ?? string.Empty;
        ApercuTexte.Visibility = Visibility.Visible;
        DefilementTexte.Visibility = Visibility.Visible;
    }

    // L'image est lue en mémoire : un BitmapImage sur le fichier le garderait ouvert et empêcherait de le supprimer.
    // Elle n'est décodée à une largeur réduite que si elle dépasse la zone d'aperçu ; une petite image garde sa taille réelle
    // (et le contrôle est borné à sa taille réelle : Uniform réduit une grande image mais agrandirait une petite).
    private async Task AfficherImageAsync(Element element)
    {
        var cle = _affiche;
        ZoneImage.Visibility = Visibility.Visible;
        ApercuImage.Source = null;
        ImageAbsente.Visibility = Visibility.Collapsed;
        try
        {
            var octets = await File.ReadAllBytesAsync(_vue!.CheminImage(element));
            using var flux = new InMemoryRandomAccessStream();
            await flux.WriteAsync(octets.AsBuffer());
            flux.Seek(0);
            var image = new BitmapImage();
            if (LargeurPng(octets) > LargeurMaxDecodee)
            {
                image.DecodePixelWidth = LargeurMaxDecodee;
            }

            await image.SetSourceAsync(flux);
            if (cle == _affiche)
            {
                ApercuImage.MaxWidth = image.PixelWidth;
                ApercuImage.MaxHeight = image.PixelHeight;
                ApercuImage.Source = image;
            }
        }
        catch (Exception exception)
        {
            // Un fichier absent ou un PNG refusé sont attendus ; toute autre exception est notée au journal.
            if (exception is not (IOException or UnauthorizedAccessException or InvalidOperationException
                                  or System.Runtime.InteropServices.COMException))
            {
                _vue?.Journal.Erreur("Aperçu de l'image impossible.", exception);
            }

            if (cle == _affiche)
            {
                ImageAbsente.Text = "L'image est introuvable ou illisible sur le disque.";
                ImageAbsente.Visibility = Visibility.Visible;
            }
        }
    }

    // Largeur lue dans l'en-tête IHDR du PNG (signature de 8 octets, longueur et type de bloc, puis la largeur sur 4 octets
    // grand-boutiste) ; zéro si l'en-tête n'est pas celui d'un PNG.
    private static int LargeurPng(byte[] octets)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (octets.Length < 24 || !octets.AsSpan(0, 8).SequenceEqual(signature))
        {
            return 0;
        }

        var largeur = BinaryPrimitives.ReadUInt32BigEndian(octets.AsSpan(16, 4));
        return largeur > int.MaxValue ? int.MaxValue : (int)largeur;
    }

    // Nombre à la française, groupes de chiffres séparés par une espace insécable.
    private static string Nombre(int valeur) =>
        valeur.ToString("N0", CultureInfo.GetCultureInfo("fr-FR")).Replace('\u202F', '\u00A0');

    private string Description(Element element)
    {
        var origine = string.IsNullOrWhiteSpace(element.Application) ? "une application inconnue" : element.Application;
        var taille = element.Type switch
        {
            TypeElement.Image => ResumeElement.Taille(element.Taille),
            TypeElement.Fichiers => $"{element.Fichiers.Count} fichier(s)",
            _ => Nombre(element.Texte?.Length ?? 0) + " caractères",
        };
        return $"Copié depuis {origine}, {ResumeElement.Heure(element.DateCopie, _vue?.Horloge ?? TimeProvider.System)} · {taille}";
    }

    // Les onglets proposés sont ceux de l'utilisateur : un élément nommé ne reste pas dans Temporaire.
    private void SynchroniserOnglets()
    {
        if (_vue is null)
        {
            return;
        }

        BoutonAutreMode.Label = _vue.LibelleAutreMode;
        var utilisateur = _vue.Onglets.Where(o => !o.EstTemporaire).ToList();
        if (utilisateur.Select(o => (o.Id, o.Nom)).SequenceEqual(_ongletsProposes.Select(o => (o.Id, o.Nom))))
        {
            return;
        }

        var choisi = (ChoixOnglet.SelectedItem as OngletVue)?.Id;
        _ongletsProposes = utilisateur;
        ChoixOnglet.ItemsSource = utilisateur;
        ChoixOnglet.SelectedItem = utilisateur.FirstOrDefault(o => o.Id == choisi);
    }

    private void Coller_Click(object sender, RoutedEventArgs e) => _vue?.DemanderCollage(autreMode: false, copieSeule: false);

    private void AutreMode_Click(object sender, RoutedEventArgs e) => _vue?.DemanderCollage(autreMode: true, copieSeule: false);

    private void Nommer_Click(object sender, RoutedEventArgs e) => FocaliserNom();

    private void Supprimer_Click(object sender, RoutedEventArgs e)
    {
        if (_vue is { } vue)
        {
            ExecutionProtegee.Lancer(vue.Journal, vue.SupprimerSelectionAsync, "la suppression de l'élément");
        }
    }

    private void Enregistrer_Click(object sender, RoutedEventArgs e) => Enregistrer();

    private void ChampNom_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Enregistrer();
        }
    }

    // Après un enregistrement réussi, le focus revient à la recherche (par la fenêtre) pour enchaîner au clavier.
    private void Enregistrer()
    {
        if (_vue?.Enregistrer(ChampNom.Text, (ChoixOnglet.SelectedItem as OngletVue)?.Id) == true)
        {
            ElementEnregistre?.Invoke();
        }
    }
}
