### Tâche 26 : Aperçu, rangement des éléments et boîtes de dialogue (VueApercu, IDialogues)

**Fichiers :**
- Créer : `src/PressePapiers/Interface/IDialogues.cs`
- Créer : `src/PressePapiers/Interface/GestionnaireDialogues.cs`
- Créer : `src/PressePapiers/Interface/RenduRtf.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipaleVue.Actions.cs`
- Créer : `src/PressePapiers/Interface/VueApercu.xaml`
- Créer : `src/PressePapiers/Interface/VueApercu.xaml.cs`
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.xaml` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.xaml.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.Volet.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : aucun test xUnit (interface WinUI). Vérification sur le poste par le banc d'essai de la tâche 25 : `$ESSAIS/Scenario26.cs`

**Interfaces :**

- Utilise :
  - tâche 25 : `FenetrePrincipaleVue` (`Onglets`, `ElementSelectionne`, `Reglages`, `Horloge`, `Rafraichir`, `ChoisirOnglet`, `Rafraichi`),
    `ElementVue`, `OngletVue`, `ResumeElement.Taille` et `Heure`, `FenetrePrincipale` (volet, `Racine`) ;
  - tâches 4 à 7 : `DepotHistorique.Nommer(long elementId, string nom, long ongletId) : Element`, `Supprimer(long)`,
    `CreerOnglet(string)`, `RenommerOnglet(long, string)`, `DeplacerOnglet(long, int nouvelOrdre)` (Temporaire vaut 0),
    `SupprimerOnglet(long)`, `CheminImage(Element) : string` ;
  - tâche 10 : `Parametres.MiseEnFormeParDefaut`, `ModeCollage` (tâche 1) ;
  - tâche 1 : `IJournal`.

- Fournit :
  - `public interface IDialogues` : `bool EnCours`, `Task<bool> ConfirmerAsync(string titre, string message, string libelleConfirmation)`,
    `Task<string?> DemanderNomAsync(string titre, string nomInitial, string libelleValidation)` ;
  - `public sealed class GestionnaireDialogues : IDialogues` (`ContentDialog` sur la racine du contenu, un seul à la fois, seulement
    fenêtre affichée) ;
  - `public sealed partial class VueApercu : UserControl` : `void Lier(FenetrePrincipaleVue)`, `void FocaliserNom()` ;
  - `internal static class RenduRtf` : `bool Charger(RichEditBox, byte[] rtf, IJournal? journal = null)`, `int TailleMaximale` (1 Mio) ;
  - dans la vue (fichier `FenetrePrincipaleVue.Actions.cs`) : `IDialogues? Dialogues { get; set; }`, `bool AnnonceOuverte`,
    `InfoBarSeverity AnnonceGravite`, `string AnnonceMessage`, `event Action<ModeCollage, bool>? CollageDemande` (mode voulu, copie
    seule), `ModeCollage ModeAlternatif`, `string LibelleAutreMode`, `void Annoncer(InfoBarSeverity, string)`,
    `void DemanderCollage(bool autreMode, bool copieSeule)`, `bool Enregistrer(string? nom, long? ongletId)`,
    `Task SupprimerSelectionAsync()`, `Task NouvelOngletAsync()`, `Task RenommerOngletAsync(OngletVue)`,
    `Task SupprimerOngletAsync(OngletVue)`, `void DeplacerOnglet(OngletVue, int decalage)`, et en interne `IJournal Journal`,
    `string CheminImage(Element)` ;
  - dans la fenêtre : l'`InfoBar` (`Annonce`), l'aperçu (`PanneauApercu`), `Vue.Dialogues` renseigné, le menu contextuel des onglets
    de l'utilisateur (Renommer, Supprimer, Monter, Descendre), « Nouvel onglet » qui ouvre la boîte de saisie.
  - Consommateurs : tâche 27 (`CollageDemande`, `Dialogues.EnCours`, `FocaliserNom`, `Annoncer`), tâche 28 (`Annoncer`).

- [ ] **Étape 1 : Écrire la vérification sur le poste (scénario du banc)**

Le banc existe déjà (tâche 25). Ce scénario ouvre la vraie fenêtre, choisit chaque type d'élément, charge un RTF réaliste (gras, italique,
couleur, souligné, tableau bordé à ligne d'en-tête grisée, comme Word ou Outlook en produisent), exerce les boutons, les champs, les
frappes réelles et les boîtes de dialogue (qu'il active comme le ferait un lecteur d'écran : modèle d'automatisation, sans souris).

Créez dans `$ESSAIS`.

`$ESSAIS/Scenario26.cs`

```csharp
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Modele;
using PressePapiers.Interface;

namespace PressePapiers.Banc;

/// <summary>
/// Tâche 26 : aperçu (texte, RTF, image, fichiers), commandes, nom et onglet, InfoBar, boîtes de dialogue
/// (nouvel onglet, suppression d'un élément nommé, suppression d'un onglet non vide).
/// </summary>
internal static class Scenario26
{
    public static async Task ExecuterAsync(ContexteBanc c)
    {
        var vue = c.Vue;
        var apercu = c.Controle<VueApercu>("PanneauApercu");
        T Sous<T>(string nom) where T : class => apercu.FindName(nom) as T ?? throw new InvalidOperationException(nom);
        var texte = Sous<TextBlock>("ApercuTexte");
        var riche = Sous<RichEditBox>("ApercuRiche");
        var mention = Sous<TextBlock>("Mention");
        var meta = Sous<TextBlock>("Meta");
        var champNom = Sous<TextBox>("ChampNom");
        var choixOnglet = Sous<ComboBox>("ChoixOnglet");
        var annonce = c.Controle<InfoBar>("Annonce");
        var navigation = c.Controle<NavigationView>("Navigation");

        async Task Choisir(Func<ElementVue, bool> critere)
        {
            vue.ElementSelectionne = vue.Elements.First(critere);
            await c.Attendre(250);
        }

        await c.AfficherAsync();

        // --- Texte : contenu complet, police à chasse fixe pour un terminal.
        c.Verifier(texte.Text.StartsWith("git log --oneline --graph") && texte.Visibility == Visibility.Visible, "texte : contenu complet affiché");
        c.Verifier(texte.FontFamily.Source == "Cascadia Mono", "texte : chasse fixe pour WindowsTerminal");
        c.Verifier(meta.Text.StartsWith("Copié depuis WindowsTerminal, ") && meta.Text.Contains("caractères"), $"méta : « {meta.Text} »");
        c.Verifier(!Sous<AppBarButton>("BoutonAutreMode").IsEnabled || Sous<AppBarButton>("BoutonAutreMode").Visibility == Visibility.Collapsed,
                   "texte brut : pas de bouton d'autre mode");
        c.Verifier(champNom.Text.Length == 0 && choixOnglet.SelectedItem is null, "élément temporaire : nom vide, aucun onglet choisi");
        await c.CapturerAsync("26-texte.png");

        // --- Texte énorme : tronqué à l'affichage, instantané.
        c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte, Texte = new string('x', 300_000) + " fin", Application = "WINWORD" });
        await c.Attendre(250);
        var chrono = System.Diagnostics.Stopwatch.StartNew();
        vue.ElementSelectionne = vue.Elements[0];
        await c.Attendre(50);
        chrono.Stop();
        c.Verifier(texte.Text.Length == 100_000 && mention.Text.Contains("100\u00A0000") && mention.Text.Contains("300\u00A0004"),
                   $"texte de 300 004 caractères : aperçu limité à 100 000, mention : « {mention.Text} »");
        c.Verifier(chrono.ElapsedMilliseconds < 1000, $"choisir ce texte prend moins d'une seconde (mesuré {chrono.ElapsedMilliseconds} ms)");
        c.Depot.Supprimer(vue.Elements[0].Id);
        await c.Attendre(250);

        // --- RTF : rendu par RichEditBox en lecture seule (point 3 de la section 16).
        await Choisir(e => e.Element.Application == "OUTLOOK" && e.Element.Type == TypeElement.MisEnForme && !e.ANom);
        riche.Document.GetText(TextGetOptions.None, out var contenuRiche);
        c.Verifier(riche.Visibility == Visibility.Visible && texte.Visibility == Visibility.Collapsed, "RTF : le RichEditBox remplace le texte brut");
        c.Verifier(riche.IsReadOnly, "RTF : le RichEditBox est remis en lecture seule après le chargement");
        c.Verifier(contenuRiche.Contains("Lefebvre-Dumontier") && contenuRiche.Contains("2026-10-0457") && contenuRiche.Contains("Palettes de film"),
                   "RTF : le texte, accents et tableau compris, est chargé");
        var gras = riche.Document.GetRange(contenuRiche.IndexOf("Lefebvre", StringComparison.Ordinal), contenuRiche.IndexOf("Lefebvre", StringComparison.Ordinal) + 8);
        c.Verifier(gras.CharacterFormat.Bold == FormatEffect.On, "RTF : le gras est rendu");
        var rouge = riche.Document.GetRange(contenuRiche.IndexOf("2026-10", StringComparison.Ordinal), contenuRiche.IndexOf("2026-10", StringComparison.Ordinal) + 7);
        c.Verifier(rouge.CharacterFormat.ForegroundColor is { R: 192, G: 0, B: 0 }, $"RTF : la couleur est rendue (mesuré {rouge.CharacterFormat.ForegroundColor})");
        var autreMode = Sous<AppBarButton>("BoutonAutreMode");
        c.Verifier(autreMode.Visibility == Visibility.Visible && autreMode.Label == "Avec mise en forme",
                   $"élément mis en forme, mode par défaut texte brut : bouton « {autreMode.Label} »");
        c.Verifier(mention.Text.Length == 0, "RTF rendu : pas de mention de repli");
        await c.CapturerAsync("26-rtf.png");

        // --- RenduRtf : chargement refusé ou RTF abîmé, la boîte revient toujours en lecture seule.
        var essai = new RichEditBox { IsReadOnly = true };
        c.Verifier(!RenduRtf.Charger(essai, [], c.Journal) && essai.IsReadOnly, "RTF vide : repli, lecture seule");
        c.Verifier(!RenduRtf.Charger(essai, new byte[RenduRtf.TailleMaximale + 1], c.Journal) && essai.IsReadOnly, "RTF de plus de 1 Mo : repli, lecture seule");
        c.Verifier(!RenduRtf.Charger(essai, System.Text.Encoding.ASCII.GetBytes("{\\rtf1}"), c.Journal) && essai.IsReadOnly, "RTF sans texte : repli, lecture seule");
        var abime = RenduRtf.Charger(essai, System.Text.Encoding.ASCII.GetBytes("{\\rtf1\\ansi {\\b oups \\par \\'zz"), c.Journal);
        c.Ecrire($"INFO   RTF tronqué : chargé = {abime}");
        c.Verifier(essai.IsReadOnly, "RTF tronqué : lecture seule dans tous les cas");
        c.Verifier(RenduRtf.Charger(essai, System.Text.Encoding.ASCII.GetBytes("{\\rtf1\\ansi bonjour\\par}\0"), c.Journal) && essai.IsReadOnly,
                   "RTF correct avec zéro final : chargé, lecture seule");

        // --- HTML seul : texte brut avec la mention.
        c.Depot.AjouterCopie(new ContenuCopie
        {
            Type = TypeElement.MisEnForme, Texte = "Titre de la page web\nUn paragraphe copié depuis un navigateur.", Application = "msedge",
            Html = System.Text.Encoding.UTF8.GetBytes("Version:1.0\r\n<html><body><!--StartFragment--><b>Titre</b><!--EndFragment--></body></html>"),
        });
        await c.Attendre(250);
        await Choisir(e => e.Element.Application == "msedge" && e.Element.Type == TypeElement.MisEnForme);
        c.Verifier(texte.Visibility == Visibility.Visible && riche.Visibility == Visibility.Collapsed && mention.Text == "Mise en forme conservée pour le collage.",
                   "HTML seul : texte brut et mention « mise en forme conservée pour le collage »");

        // --- Fichiers.
        await Choisir(e => e.Element.Type == TypeElement.Fichiers);
        var fichiers = Sous<ItemsControl>("ListeFichiers");
        c.Verifier(fichiers.Items.Count == 3 && ((IEnumerable<string>)fichiers.ItemsSource).Last().EndsWith("(version 3).docx"), "fichiers : les 3 chemins complets sont listés");
        c.Verifier(meta.Text.Contains("3 fichier(s)"), "fichiers : la méta compte les fichiers");
        await c.CapturerAsync("26-fichiers.png");

        // --- Image : affichée depuis le disque sans verrouiller le PNG.
        await Choisir(e => e.Element.Type == TypeElement.Image);
        var image = Sous<Image>("ApercuImage");
        await c.Attendre(400);
        c.Verifier(image.Source is not null && Sous<TextBlock>("ImageAbsente").Visibility == Visibility.Collapsed, "image : affichée en grand");
        var elementImage = vue.ElementSelectionne!.Element;
        var chemin = c.Depot.CheminImage(elementImage);
        c.Verifier(File.Exists(chemin), "image : le PNG est dans le dossier des images");
        await c.CapturerAsync("26-image.png");
        // Une copie d'image dont le PNG disparaît (antivirus, nettoyage) ne doit pas faire planter l'aperçu.
        var png = await DonneesBanc.PngDegradeAsync(64, 64);
        var perdu = c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Image, ImagePng = png, Miniature = png, Application = "Paint" });
        File.Delete(c.Depot.CheminImage(perdu));
        await c.Attendre(250);
        await Choisir(e => e.Id == perdu.Id);
        await c.Attendre(300);
        c.Verifier(Sous<TextBlock>("ImageAbsente").Visibility == Visibility.Visible && image.Source is null, "PNG manquant sur disque : message, pas d'exception");
        c.Depot.Supprimer(perdu.Id);
        await c.Attendre(250);
        await Choisir(e => e.Id == elementImage.Id);
        await c.Attendre(400);
        c.Depot.Supprimer(elementImage.Id);
        await c.Attendre(250);
        c.Verifier(!File.Exists(chemin), "image affichée puis supprimée : le PNG n'était pas verrouillé par l'aperçu");

        // --- Aucun élément : aperçu vide.
        var ongletVide = c.Depot.CreerOnglet("Vide");
        await c.Attendre(250);
        await c.InvoquerEntreeAsync(c.EntreeOnglet("Vide"));
        c.Verifier(vue.ElementSelectionne is null && Sous<TextBlock>("Vide").Visibility == Visibility.Visible && !champNom.IsEnabled,
                   "aucun élément : « Aucun élément sélectionné. », champs désactivés");
        c.Depot.SupprimerOnglet(ongletVide.Id);
        await c.Attendre(250);
        await c.InvoquerEntreeAsync(c.EntreeOnglet("Temporaire"));

        // --- Nommer : nom et onglet requis.
        await Choisir(e => e.Element.Application == "ms-teams");
        var teams = vue.ElementSelectionne!.Id;
        var avant = vue.Onglets.Select(o => o.Compteur).ToList();
        ContexteBanc.Cliquer(Sous<Button>("BoutonEnregistrer"));
        c.Verifier(annonce.IsOpen && annonce.Severity == InfoBarSeverity.Warning && annonce.Message.StartsWith("Saisissez un nom"),
                   $"Enregistrer sans rien saisir : avertissement « {annonce.Message} »");
        champNom.Text = "Téléphone de la logistique";
        ContexteBanc.Cliquer(Sous<Button>("BoutonEnregistrer"));
        c.Verifier(c.Depot.Lire(teams)!.Nom is null && c.Depot.Lire(teams)!.OngletId == c.Depot.OngletTemporaire.Id, "nom sans onglet : rien n'est écrit");
        champNom.Text = "  ";
        choixOnglet.SelectedItem = choixOnglet.Items.Cast<OngletVue>().First(o => o.Nom == "Adresses");
        ContexteBanc.Cliquer(Sous<Button>("BoutonEnregistrer"));
        c.Verifier(c.Depot.Lire(teams)!.Nom is null, "onglet sans nom (espaces seuls) : rien n'est écrit");
        champNom.Text = "Téléphone de la logistique";
        ContexteBanc.Cliquer(Sous<Button>("BoutonEnregistrer"));
        await c.Attendre(300);
        var nomme = c.Depot.Lire(teams)!;
        c.Verifier(nomme.Nom == "Téléphone de la logistique" && c.Depot.ListerOnglets().First(o => o.Id == nomme.OngletId).Nom == "Adresses",
                   "Enregistrer : l'élément a son nom et quitte Temporaire pour Adresses");
        c.Verifier(annonce.IsOpen && annonce.Severity == InfoBarSeverity.Success && annonce.Message == "« Téléphone de la logistique » enregistré dans Adresses.",
                   $"InfoBar de confirmation : « {annonce.Message} »");
        var apres = vue.Onglets.Select(o => o.Compteur).ToList();
        c.Verifier(apres[0] == avant[0] - 1 && apres[2] == avant[2] + 1, "compteurs : Temporaire -1, Adresses +1");

        // --- Renommer un élément déjà nommé : le nom et l'onglet reviennent dans les champs.
        await c.InvoquerEntreeAsync(c.EntreeOnglet("Adresses"));
        await Choisir(e => e.Nom == "Siège");
        c.Verifier(champNom.Text == "Siège" && (choixOnglet.SelectedItem as OngletVue)?.Nom == "Adresses", "élément nommé : nom et onglet repris dans les champs");
        champNom.Text = "Siège social";
        ContexteBanc.Cliquer(Sous<Button>("BoutonEnregistrer"));
        await c.Attendre(300);
        c.Verifier(c.Depot.ListerElements(vue.OngletSelectionne!.Id).Any(e => e.Nom == "Siège social") && !c.Depot.ListerElements(vue.OngletSelectionne!.Id).Any(e => e.Nom == "Siège"),
                   "renommer : même onglet, nouveau nom");
        c.Verifier(champNom.Text == "Siège social", "après renommage, le champ montre le nouveau nom");

        // --- Une relecture du dépôt n'efface pas un nom en cours de saisie.
        await Choisir(e => e.Nom == "Siège social");
        champNom.Text = "saisie en cours";
        c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte, Texte = "copie qui arrive pendant la saisie", Application = "msedge" });
        await c.Attendre(400);
        c.Verifier(champNom.Text == "saisie en cours", "une copie qui arrive ne remet pas à zéro le nom en cours de saisie");

        // --- Entrée dans le champ Nom enregistre (frappe réelle).
        await c.InvoquerEntreeAsync(c.EntreeOnglet("Temporaire"));
        await Choisir(e => e.Element.Texte == "copie qui arrive pendant la saisie");
        var cible = vue.ElementSelectionne!.Id;
        champNom.Text = "Copie nommée par Entrée";
        choixOnglet.SelectedItem = choixOnglet.Items.Cast<OngletVue>().First(o => o.Nom == "Code");
        champNom.Focus(FocusState.Programmatic);
        await c.Attendre(150);
        if (Touches.Taper(c.Fenetre.Handle, Touches.Entree))
        {
            await c.Attendre(400);
            c.Verifier(c.Depot.Lire(cible)!.Nom == "Copie nommée par Entrée", "Entrée dans le champ Nom enregistre (frappe réelle)");
        }
        else
        {
            c.Ecrire("NON TESTE  Entrée dans le champ Nom : la fenêtre n'est pas au premier plan, aucune frappe envoyée");
        }

        // --- Supprimer : sans confirmation pour un temporaire, sans effet visible d'une boîte.
        await Choisir(e => e.Element.Application == "msedge" && !e.ANom && e.Element.Type == TypeElement.MisEnForme);
        var temporaire = vue.ElementSelectionne!.Id;
        ContexteBanc.Cliquer(Sous<AppBarButton>("BoutonSupprimer"));
        await c.Attendre(300);
        c.Verifier(c.Depot.Lire(temporaire) is null && !vue.Dialogues!.EnCours, "supprimer un élément temporaire : immédiat, sans boîte");
        c.Verifier(annonce.IsOpen && annonce.Message == "Élément supprimé.", "supprimer : InfoBar « Élément supprimé. »");

        // --- Supprimer un élément nommé : confirmation, refus puis accord.
        await c.InvoquerEntreeAsync(c.EntreeOnglet("Code"));
        await Choisir(e => e.Nom == "Publier la version Release autonome");
        var nomm = vue.ElementSelectionne!.Id;
        ContexteBanc.Cliquer(Sous<AppBarButton>("BoutonSupprimer"));
        await c.Attendre(400);
        c.Verifier(vue.Dialogues!.EnCours && c.BoutonBoite("PrimaryButton") is { Content: "Supprimer" }, "élément nommé : une boîte de confirmation s'ouvre");
        await c.CapturerAsync("26-dialogue-suppression.png");
        ContexteBanc.Cliquer(c.BoutonBoite("CloseButton")!);
        await c.Attendre(400);
        c.Verifier(!vue.Dialogues.EnCours && c.Depot.Lire(nomm) is not null, "Annuler : l'élément nommé est conservé, la boîte est fermée");
        ContexteBanc.Cliquer(Sous<AppBarButton>("BoutonSupprimer"));
        await c.Attendre(400);
        ContexteBanc.Cliquer(c.BoutonBoite("PrimaryButton")!);
        await c.Attendre(400);
        c.Verifier(c.Depot.Lire(nomm) is null && !vue.Dialogues.EnCours, "Supprimer dans la boîte : l'élément nommé est supprimé");

        // --- Nouvel onglet : un seul à la fois, nom requis, seulement fenêtre affichée.

        // Clavier réel : focus sur l'entrée puis Entrée, comme le ferait l'utilisateur.
        async Task InvoquerNouvelOnglet()
        {
            navigation.MenuItems.OfType<NavigationViewItem>().First(i => i.Tag is "nouvel-onglet").Focus(FocusState.Keyboard);
            await c.Attendre(200);
            if (!Touches.Taper(c.Fenetre.Handle, Touches.Entree))
            {
                c.Ecrire("NON TESTE  Entrée sur « Nouvel onglet » : fenêtre non au premier plan, appel direct du modèle de vue");
                _ = vue.NouvelOngletAsync();
            }
        }

        await InvoquerNouvelOnglet();
        await c.Attendre(500);
        c.Verifier(vue.Dialogues.EnCours && c.ChampBoite() is not null, "Nouvel onglet : la boîte de saisie s'ouvre");
        c.Verifier(c.BoutonBoite("PrimaryButton")!.IsEnabled == false, "Nouvel onglet : Créer est désactivé tant que le nom est vide");
        var secondeBoite = vue.Dialogues.ConfirmerAsync("Seconde", "ne doit pas s'ouvrir", "Oui");
        c.Verifier(await secondeBoite is false, "une seule boîte à la fois : la seconde est refusée sans exception");
        c.ChampBoite()!.Text = "Contrats";
        await c.Attendre(200);
        c.Verifier(c.BoutonBoite("PrimaryButton")!.IsEnabled, "Créer s'active quand un nom est saisi");
        await c.CapturerAsync("26-dialogue-onglet.png");
        ContexteBanc.Cliquer(c.BoutonBoite("PrimaryButton")!);
        await c.Attendre(500);
        c.Verifier(vue.OngletSelectionne?.Nom == "Contrats" && vue.Onglets.Any(o => o.Nom == "Contrats") && !vue.Dialogues.EnCours,
                   "Créer : l'onglet « Contrats » existe et s'affiche");
        c.Verifier(navigation.MenuItems.OfType<NavigationViewItem>().Any(i => i.Tag is OngletVue { Nom: "Contrats" }) && ReferenceEquals(navigation.SelectedItem, navigation.MenuItems.OfType<NavigationViewItem>().First(i => i.Tag is OngletVue { Nom: "Contrats" })),
                   "le volet montre le nouvel onglet, sélectionné");

        // Entrée dans la boîte valide (bouton par défaut), frappes réelles.
        var avantTous = vue.Onglets.Count;
        await InvoquerNouvelOnglet();
        await c.Attendre(500);
        c.Ecrire($"INFO   boîte ouverte : {vue.Dialogues.EnCours}, champ : {c.ChampBoite() is not null}, premier plan : {c.Fenetre.Handle == PressePapiers.Systeme.PremierPlan.FenetreActive()}");
        if (c.ChampBoite() is not null && Touches.Ecrire(c.Fenetre.Handle, "Factures"))
        {
            await c.Attendre(300);
            c.Ecrire($"INFO   texte saisi dans la boîte : « {c.ChampBoite()?.Text} », Créer actif : {c.BoutonBoite("PrimaryButton")?.IsEnabled}");
            Touches.Taper(c.Fenetre.Handle, Touches.Entree);
            await c.Attendre(500);
            c.Ecrire($"INFO   après Entrée : boîte ouverte {vue.Dialogues.EnCours}");
            c.Verifier(vue.Onglets.Count == avantTous + 1 && vue.OngletSelectionne?.Nom == "Factures", "boîte Nouvel onglet : saisie réelle puis Entrée créent l'onglet");
        }
        else
        {
            c.Ecrire("NON TESTE  saisie réelle dans la boîte Nouvel onglet (fenêtre non au premier plan)");
            if (c.BoutonBoite("CloseButton") is { } fermer)
            {
                ContexteBanc.Cliquer(fermer);
            }

            await c.Attendre(300);
        }

        // --- Fenêtre masquée : aucune boîte ne s'ouvre.
        c.Fenetre.AppWindow.Hide();
        await c.Attendre(200);
        c.Verifier(await vue.Dialogues.ConfirmerAsync("Fenêtre masquée", "non", "Oui") is false && !vue.Dialogues.EnCours, "fenêtre masquée : aucune boîte, aucune exception");
        c.Fenetre.AppWindow.Show();
        await c.Attendre(400);

        // --- Onglets : renommer, déplacer, supprimer (vide sans confirmation, non vide avec).
        var contrats = vue.Onglets.First(o => o.Nom == "Contrats");
        _ = vue.RenommerOngletAsync(contrats);
        await c.Attendre(500);
        c.Verifier(c.ChampBoite()?.Text == "Contrats", "renommer un onglet : la boîte reprend le nom actuel");
        c.ChampBoite()!.Text = "Contrats 2026";
        ContexteBanc.Cliquer(c.BoutonBoite("PrimaryButton")!);
        await c.Attendre(500);
        c.Verifier(vue.Onglets.Any(o => o.Nom == "Contrats 2026") && !vue.Onglets.Any(o => o.Nom == "Contrats"), "onglet renommé");
        var avantOrdre = vue.Onglets.Select(o => o.Nom).ToList();
        vue.DeplacerOnglet(vue.Onglets.First(o => o.Nom == "Contrats 2026"), -1);
        await c.Attendre(300);
        var apresOrdre = vue.Onglets.Select(o => o.Nom).ToList();
        c.Verifier(apresOrdre.IndexOf("Contrats 2026") == avantOrdre.IndexOf("Contrats 2026") - 1 && apresOrdre[0] == "Temporaire", "Monter : l'onglet remonte d'une place, Temporaire reste en tête");
        vue.DeplacerOnglet(vue.Onglets[0], 1);
        c.Verifier(vue.Onglets[0].EstTemporaire, "Temporaire ne bouge pas");
        _ = vue.RenommerOngletAsync(vue.Onglets[0]);
        await c.Attendre(200);
        c.Verifier(!vue.Dialogues.EnCours, "Temporaire ne se renomme pas");

        c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Image, ImagePng = png, Miniature = png, Application = "Paint" });
        var tabCapture = c.Depot.CreerOnglet("Captures");
        var imageNommee = c.Depot.Nommer(c.Depot.ListerElements(c.Depot.OngletTemporaire.Id).First(e => e.Type == TypeElement.Image).Id, "Capture à supprimer", tabCapture.Id);
        var cheminNomme = c.Depot.CheminImage(imageNommee);
        await c.Attendre(300);
        var captures = vue.Onglets.First(o => o.Nom == "Captures");
        _ = vue.SupprimerOngletAsync(captures);
        await c.Attendre(500);
        c.Verifier(vue.Dialogues.EnCours && c.BoutonBoite("PrimaryButton") is { Content: "Supprimer" }, "onglet non vide : confirmation demandée");
        ContexteBanc.Cliquer(c.BoutonBoite("CloseButton")!);
        await c.Attendre(400);
        c.Verifier(vue.Onglets.Any(o => o.Nom == "Captures") && File.Exists(cheminNomme), "Annuler : l'onglet, son élément et son PNG sont conservés");
        _ = vue.SupprimerOngletAsync(captures);
        await c.Attendre(500);
        ContexteBanc.Cliquer(c.BoutonBoite("PrimaryButton")!);
        await c.Attendre(500);
        c.Verifier(!vue.Onglets.Any(o => o.Nom == "Captures") && !File.Exists(cheminNomme), "Supprimer : l'onglet, ses éléments et leurs PNG disparaissent");
        var factures = vue.Onglets.First(o => o.Nom == "Factures");
        _ = vue.SupprimerOngletAsync(factures);
        await c.Attendre(400);
        c.Verifier(!vue.Dialogues.EnCours && !vue.Onglets.Any(o => o.Nom == "Factures"), "onglet vide : supprimé sans confirmation");
        _ = vue.SupprimerOngletAsync(vue.Onglets[0]);
        await c.Attendre(200);
        c.Verifier(vue.Onglets[0].EstTemporaire && !vue.Dialogues.EnCours, "Temporaire ne se supprime pas");

        // --- Boutons Coller : la demande part vers l'abonné (tâche 27).
        ModeCollage? demande = null;
        var copieSeule = true;
        vue.CollageDemande += (mode, seule) => { demande = mode; copieSeule = seule; };
        vue.ChoisirOnglet(vue.Onglets[0]);
        await c.Attendre(250);
        await Choisir(e => e.Element.Type == TypeElement.MisEnForme && e.Element.Rtf is not null);
        ContexteBanc.Cliquer(Sous<AppBarButton>("BoutonColler"));
        c.Verifier(demande == ModeCollage.TexteSeul && !copieSeule, "Coller : mode des réglages (texte brut), collage");
        ContexteBanc.Cliquer(Sous<AppBarButton>("BoutonAutreMode"));
        c.Verifier(demande == ModeCollage.AvecMiseEnForme && !copieSeule, "bouton d'autre mode : avec mise en forme");

        c.Verifier(c.Journal.Erreurs.Count == 0, $"aucune erreur au journal ({c.Journal.Erreurs.Count}) ; avertissements : {c.Journal.Avertissements.Count}");
    }
}
```

- [ ] **Étape 2 : Lancer le banc et constater l'échec**

```bash
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 26
```

Résultat attendu : la compilation échoue : `CS0246` (`VueApercu` introuvable), `CS1061` (`'FenetrePrincipaleVue' ne contient pas de
définition pour 'Dialogues'`, `'Annoncer'`, `'DemanderCollage'`...). À la rédaction : 34 erreurs.

- [ ] **Étape 3 : Implémenter**

Décisions et pièges à connaître :
- **RTF (spec, section 16, point 3)** : `RichEditBox` en lecture seule rend le RTF réaliste du banc fidèlement : gras, italique, couleur
  (le rouge `#C00000` est relu par le test), souligné, tableau bordé avec ligne d'en-tête grisée. Il ne retourne pas à la ligne dans
  un tableau : une table plus large que la colonne (ici 402 px) est coupée à droite, avec une barre de défilement horizontale
  (`ScrollViewer.HorizontalScrollBarVisibility="Auto"`). Le RTF du banc est écrit à la main : il reste à relire avec un vrai copier
  depuis Word et depuis Outlook à la recette.
- Le contrôle ne se remplit que modifiable : `IsReadOnly` passe à faux, `Document.SetText(TextSetOptions.FormatRtf, ...)`, puis
  retour à vrai dans un `finally` (test : 4 échecs si le `finally` disparaît). Un RTF de plus de 1 Mio, vide, ou qui ne donne aucun
  texte est refusé : l'aperçu montre alors le texte brut et la mention « Mise en forme conservée pour le collage. » (comme pour une
  page web en HTML seul).
- Les octets du RTF passent par `Encoding.Latin1` (un octet, un caractère) : un RTF est de l'ASCII, ses accents sont des séquences
  `\'e9`. Le zéro final du format du presse-papiers est retiré.
- L'image est lue en mémoire (`File.ReadAllBytesAsync`) puis décodée à 1000 px de large : un `BitmapImage` sur le fichier le tiendrait
  ouvert et empêcherait de le supprimer (le banc supprime l'élément image affiché et constate que le PNG a disparu). Un PNG absent du
  disque donne un message, jamais une exception.
- Un texte de plusieurs mégaoctets gèle un `TextBlock` : l'aperçu est tronqué à 100 000 caractères avec une mention ; le collage
  porte toujours sur le texte entier.
- Une relecture du dépôt recrée toutes les lignes : l'aperçu ne remet à zéro ses champs que si l'élément (identifiant, nom, onglet)
  a changé, pour ne pas effacer un nom en cours de saisie.
- Les commandes Nommer et Supprimer de la barre de commandes sont **sans libellé** (icône, info-bulle « Nommer (F2) », « Supprimer
  (Suppr) ») : à 920 px la colonne d'aperçu ne mesure que 402 px et quatre boutons libellés renvoient Supprimer dans le menu de débordement.
- La boîte « Nouvel onglet » se valide par Entrée (bouton par défaut) ; Créer est inactif tant que le nom est vide. Une seconde boîte
  demandée pendant qu'une autre est ouverte est refusée sans exception (`ContentDialog.ShowAsync` lèverait une `COMException`),
  et aucune boîte ne s'ouvre fenêtre masquée.
- Un `x:Name="Apercu"` sur la fenêtre est refusé par le compilateur XAML (`WMC9999`) : le contrôle s'appelle `PanneauApercu`.
- `DemanderCollage` ne fait que lever `CollageDemande` : la tâche 27 y branche le recollage.

`src/PressePapiers/Interface/IDialogues.cs`

```csharp
namespace PressePapiers.Interface;

/// <summary>
/// Boîtes de dialogue de la fenêtre principale. Une interface pour que le modèle de vue reste sans contrôle WinUI
/// et pour que l'essai remplace les vraies boîtes par des réponses toutes faites.
/// </summary>
public interface IDialogues
{
    /// <summary>Vrai tant qu'une boîte est ouverte : la perte du focus ne doit alors pas masquer la fenêtre.</summary>
    bool EnCours { get; }

    /// <summary>Demande une confirmation ; faux si l'utilisateur refuse ou si aucune boîte ne peut s'ouvrir.</summary>
    Task<bool> ConfirmerAsync(string titre, string message, string libelleConfirmation);

    /// <summary>Demande un nom ; null si l'utilisateur annule ou si aucune boîte ne peut s'ouvrir.</summary>
    Task<string?> DemanderNomAsync(string titre, string nomInitial, string libelleValidation);
}
```

`src/PressePapiers/Interface/GestionnaireDialogues.cs`

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PressePapiers.Interface;

/// <summary>
/// Ouvre les <see cref="ContentDialog"/> sur la racine du contenu de la fenêtre (spec, section 9) : un seul à la fois,
/// et seulement quand la fenêtre est affichée (WinUI 3 refuse d'ouvrir une boîte sur une fenêtre masquée).
/// </summary>
public sealed class GestionnaireDialogues : IDialogues
{
    private readonly Func<FrameworkElement?> _racine;
    private readonly Func<bool> _fenetreAffichee;
    private ContentDialog? _ouverte;

    /// <summary>Crée le gestionnaire.</summary>
    /// <param name="racine">Racine du contenu de la fenêtre : elle fournit le XamlRoot et le thème.</param>
    /// <param name="fenetreAffichee">Dit si la fenêtre est affichée.</param>
    public GestionnaireDialogues(Func<FrameworkElement?> racine, Func<bool> fenetreAffichee)
    {
        _racine = racine;
        _fenetreAffichee = fenetreAffichee;
    }

    /// <inheritdoc />
    public bool EnCours => _ouverte is not null;

    /// <inheritdoc />
    public async Task<bool> ConfirmerAsync(string titre, string message, string libelleConfirmation)
    {
        var dialogue = Creer(titre, libelleConfirmation);
        dialogue.Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        return await AfficherAsync(dialogue) == ContentDialogResult.Primary;
    }

    /// <inheritdoc />
    public async Task<string?> DemanderNomAsync(string titre, string nomInitial, string libelleValidation)
    {
        var dialogue = Creer(titre, libelleValidation);
        var champ = new TextBox { Text = nomInitial, PlaceholderText = "Nom", MaxLength = 120 };
        dialogue.IsPrimaryButtonEnabled = nomInitial.Trim().Length > 0;
        champ.TextChanged += (_, _) => dialogue.IsPrimaryButtonEnabled = champ.Text.Trim().Length > 0;
        dialogue.Content = champ;
        dialogue.Opened += (_, _) =>
        {
            champ.Focus(FocusState.Programmatic);
            champ.SelectAll();
        };

        return await AfficherAsync(dialogue) == ContentDialogResult.Primary ? champ.Text.Trim() : null;
    }

    private static ContentDialog Creer(string titre, string libelleConfirmation) => new()
    {
        Title = titre,
        PrimaryButtonText = libelleConfirmation,
        CloseButtonText = "Annuler",
        DefaultButton = ContentDialogButton.Primary,
    };

    private async Task<ContentDialogResult> AfficherAsync(ContentDialog dialogue)
    {
        if (_ouverte is not null || !_fenetreAffichee() || _racine() is not { XamlRoot: { } racine } element)
        {
            return ContentDialogResult.None;
        }

        dialogue.XamlRoot = racine;
        dialogue.RequestedTheme = element.ActualTheme;
        _ouverte = dialogue;
        try
        {
            return await dialogue.ShowAsync();
        }
        finally
        {
            _ouverte = null;
        }
    }
}
```

`src/PressePapiers/Interface/RenduRtf.cs`

```csharp
using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Interface;

/// <summary>Charge un RTF dans un <see cref="RichEditBox"/> en lecture seule (spec, section 8).</summary>
internal static class RenduRtf
{
    /// <summary>Au-delà, l'aperçu reste en texte brut : charger un RTF de plusieurs mégaoctets gèlerait l'interface.</summary>
    public const int TailleMaximale = 1024 * 1024;

    /// <summary>
    /// Charge le RTF puis remet la boîte en lecture seule, quoi qu'il arrive : le contrôle ne se remplit que lorsqu'il
    /// est modifiable. Renvoie faux si le RTF est trop gros, refusé ou ne donne aucun texte : l'appelant montre alors
    /// le texte brut avec la mention « mise en forme conservée pour le collage ».
    /// </summary>
    public static bool Charger(RichEditBox boite, byte[] rtf, IJournal? journal = null)
    {
        if (rtf.Length == 0 || rtf.Length > TailleMaximale)
        {
            return false;
        }

        try
        {
            boite.IsReadOnly = false;

            // Latin1 recopie les octets tels quels : un RTF est de l'ASCII, ses accents sont des séquences \'e9.
            // Le zéro final du format du presse-papiers est retiré.
            boite.Document.SetText(TextSetOptions.FormatRtf, Encoding.Latin1.GetString(rtf).TrimEnd('\0'));
            boite.Document.GetText(TextGetOptions.None, out var texte);
            boite.Document.Selection.SetRange(0, 0);
            return !string.IsNullOrWhiteSpace(texte);
        }
        catch (Exception exception)
        {
            journal?.Avertissement("Aperçu RTF impossible : l'élément s'affiche en texte brut.", exception);
            return false;
        }
        finally
        {
            boite.IsReadOnly = true;
        }
    }
}
```

`src/PressePapiers/Interface/FenetrePrincipaleVue.Actions.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>Actions sur les éléments et les onglets (spec, section 9) et message d'information de la fenêtre.</summary>
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Journal de l'application, pour les contrôles de la fenêtre.</summary>
    internal IJournal Journal => _journal;

    /// <summary>Chemin complet du PNG d'un élément image.</summary>
    internal string CheminImage(Element element) => _depot.CheminImage(element);

    /// <summary>Boîtes de dialogue de la fenêtre ; sans elles, une action qui doit confirmer ne fait rien.</summary>
    public IDialogues? Dialogues { get; set; }

    /// <summary>Vrai quand le message de l'<see cref="InfoBar"/> est affiché.</summary>
    [ObservableProperty]
    public partial bool AnnonceOuverte { get; set; }

    /// <summary>Gravité du message affiché.</summary>
    [ObservableProperty]
    public partial InfoBarSeverity AnnonceGravite { get; set; } = InfoBarSeverity.Informational;

    /// <summary>Texte du message affiché.</summary>
    [ObservableProperty]
    public partial string AnnonceMessage { get; set; } = string.Empty;

    /// <summary>Levé pour coller ou copier l'élément choisi : mode de collage voulu, copie seule ou non. Branché par la tâche 27.</summary>
    public event Action<ModeCollage, bool>? CollageDemande;

    /// <summary>Mode de collage qui change celui des réglages : celui de Maj+Entrée.</summary>
    public ModeCollage ModeAlternatif =>
        Reglages.MiseEnFormeParDefaut == ModeCollage.TexteSeul ? ModeCollage.AvecMiseEnForme : ModeCollage.TexteSeul;

    /// <summary>Libellé du bouton qui colle dans l'autre mode.</summary>
    public string LibelleAutreMode => ModeAlternatif == ModeCollage.TexteSeul ? "Texte brut" : "Avec mise en forme";

    /// <summary>Affiche un message en haut de la zone de contenu.</summary>
    public void Annoncer(InfoBarSeverity gravite, string message)
    {
        AnnonceGravite = gravite;
        AnnonceMessage = message;
        AnnonceOuverte = true;
    }

    /// <summary>Demande le collage (ou la copie seule) de l'élément choisi, dans le mode des réglages ou dans l'autre.</summary>
    public void DemanderCollage(bool autreMode, bool copieSeule) =>
        CollageDemande?.Invoke(autreMode ? ModeAlternatif : Reglages.MiseEnFormeParDefaut, copieSeule);

    /// <summary>
    /// Donne un nom à l'élément choisi et le range dans un onglet ; un élément temporaire quitte Temporaire.
    /// Le nom et l'onglet sont requis : sinon un message l'explique et rien n'est écrit.
    /// </summary>
    /// <returns>Vrai si l'élément a été enregistré.</returns>
    public bool Enregistrer(string? nom, long? ongletId)
    {
        if (ElementSelectionne is not { } ligne)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(nom) || ongletId is null)
        {
            var sansOnglet = Onglets.All(o => o.EstTemporaire);
            Annoncer(InfoBarSeverity.Warning, sansOnglet
                ? "Créez d'abord un onglet (Nouvel onglet) : un élément nommé ne reste pas dans Temporaire."
                : "Saisissez un nom et choisissez un onglet pour enregistrer.");
            return false;
        }

        try
        {
            var nomPropre = nom.Trim();
            var onglet = _depot.Nommer(ligne.Id, nomPropre, ongletId.Value);
            var nomOnglet = Onglets.FirstOrDefault(o => o.Id == onglet.OngletId)?.Nom ?? "l'onglet";
            Annoncer(InfoBarSeverity.Success, $"« {nomPropre} » enregistré dans {nomOnglet}.");
            return true;
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible d'enregistrer le nom de l'élément.", exception);
            Annoncer(InfoBarSeverity.Error, "L'élément n'a pas pu être enregistré. Le détail est dans le journal.");
            return false;
        }
    }

    /// <summary>Supprime l'élément choisi : sans confirmation s'il est temporaire, avec confirmation s'il porte un nom.</summary>
    public async Task SupprimerSelectionAsync()
    {
        if (ElementSelectionne is not { } ligne)
        {
            return;
        }

        if (ligne.ANom)
        {
            var confirme = Dialogues is not null && await Dialogues.ConfirmerAsync(
                "Supprimer l'élément ?", $"« {ligne.Nom} » sera supprimé définitivement.", "Supprimer");
            if (!confirme)
            {
                return;
            }
        }

        try
        {
            _depot.Supprimer(ligne.Id);
            Annoncer(InfoBarSeverity.Informational, "Élément supprimé.");
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de supprimer l'élément.", exception);
            Annoncer(InfoBarSeverity.Error, "L'élément n'a pas pu être supprimé. Le détail est dans le journal.");
        }
    }

    /// <summary>Demande un nom, crée l'onglet et l'affiche.</summary>
    public async Task NouvelOngletAsync()
    {
        var nom = Dialogues is null ? null : await Dialogues.DemanderNomAsync("Nouvel onglet", string.Empty, "Créer");
        if (string.IsNullOrWhiteSpace(nom))
        {
            return;
        }

        try
        {
            var onglet = _depot.CreerOnglet(nom);
            Rafraichir();
            ChoisirOnglet(Onglets.First(o => o.Id == onglet.Id));
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de créer l'onglet.", exception);
            Annoncer(InfoBarSeverity.Error, "L'onglet n'a pas pu être créé. Le détail est dans le journal.");
        }
    }

    /// <summary>Demande un nouveau nom pour l'onglet. Temporaire ne se renomme pas.</summary>
    public async Task RenommerOngletAsync(OngletVue onglet)
    {
        if (onglet.EstTemporaire || Dialogues is null)
        {
            return;
        }

        var nom = await Dialogues.DemanderNomAsync("Renommer l'onglet", onglet.Nom, "Renommer");
        if (string.IsNullOrWhiteSpace(nom) || nom == onglet.Nom)
        {
            return;
        }

        try
        {
            _depot.RenommerOnglet(onglet.Id, nom);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de renommer l'onglet.", exception);
            Annoncer(InfoBarSeverity.Error, "L'onglet n'a pas pu être renommé. Le détail est dans le journal.");
        }
    }

    /// <summary>Supprime l'onglet et ses éléments ; un onglet non vide demande confirmation. Temporaire ne se supprime pas.</summary>
    public async Task SupprimerOngletAsync(OngletVue onglet)
    {
        if (onglet.EstTemporaire)
        {
            return;
        }

        if (onglet.Compteur > 0)
        {
            var confirme = Dialogues is not null && await Dialogues.ConfirmerAsync(
                "Supprimer l'onglet ?",
                $"L'onglet « {onglet.Nom} » et ses {onglet.Compteur} éléments seront supprimés définitivement.", "Supprimer");
            if (!confirme)
            {
                return;
            }
        }

        try
        {
            _depot.SupprimerOnglet(onglet.Id);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de supprimer l'onglet.", exception);
            Annoncer(InfoBarSeverity.Error, "L'onglet n'a pas pu être supprimé. Le détail est dans le journal.");
        }
    }

    /// <summary>Décale l'onglet d'une place vers le haut (-1) ou vers le bas (+1) du volet. Temporaire reste en tête.</summary>
    public void DeplacerOnglet(OngletVue onglet, int decalage)
    {
        var position = Onglets.ToList().FindIndex(o => o.Id == onglet.Id);
        if (onglet.EstTemporaire || position < 0)
        {
            return;
        }

        try
        {
            _depot.DeplacerOnglet(onglet.Id, position + decalage);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de déplacer l'onglet.", exception);
        }
    }
}
```

`src/PressePapiers/Interface/VueApercu.xaml`

```xml
<UserControl
    x:Class="PressePapiers.Interface.VueApercu"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:PressePapiers.Interface">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- Coller, autre mode de mise en forme, Nommer, Supprimer. -->
        <CommandBar x:Name="Commandes" Grid.Row="0" DefaultLabelPosition="Right" Background="Transparent"
                    HorizontalAlignment="Left" IsOpen="False">
            <AppBarButton x:Name="BoutonColler" Label="Coller" Click="Coller_Click">
                <AppBarButton.Icon><FontIcon Glyph="&#xE77F;" FontFamily="Segoe Fluent Icons" /></AppBarButton.Icon>
            </AppBarButton>
            <AppBarButton x:Name="BoutonAutreMode" Label="Texte brut" Click="AutreMode_Click">
                <AppBarButton.Icon><FontIcon Glyph="&#xE8D2;" FontFamily="Segoe Fluent Icons" /></AppBarButton.Icon>
            </AppBarButton>
            <AppBarButton x:Name="BoutonNommer" Label="Nommer" LabelPosition="Collapsed"
                          ToolTipService.ToolTip="Nommer (F2)" Click="Nommer_Click">
                <AppBarButton.Icon><FontIcon Glyph="&#xE70F;" FontFamily="Segoe Fluent Icons" /></AppBarButton.Icon>
            </AppBarButton>
            <AppBarButton x:Name="BoutonSupprimer" Label="Supprimer" LabelPosition="Collapsed"
                          ToolTipService.ToolTip="Supprimer (Suppr)" Click="Supprimer_Click">
                <AppBarButton.Icon><FontIcon Glyph="&#xE74D;" FontFamily="Segoe Fluent Icons" /></AppBarButton.Icon>
            </AppBarButton>
        </CommandBar>

        <!-- Nom et onglet de rangement. -->
        <StackPanel x:Name="ZoneChamps" Grid.Row="1" Spacing="8" Margin="16,4,16,0">
            <TextBox x:Name="ChampNom" Header="Nom" PlaceholderText="Sans nom : reste dans Temporaire" MaxLength="120"
                     KeyDown="ChampNom_KeyDown" />
            <Grid ColumnSpacing="8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <ComboBox x:Name="ChoixOnglet" Grid.Column="0" Header="Onglet" PlaceholderText="Choisir un onglet"
                          HorizontalAlignment="Stretch" DisplayMemberPath="Nom" />
                <Button x:Name="BoutonEnregistrer" Grid.Column="1" Content="Enregistrer"
                        Style="{StaticResource AccentButtonStyle}" VerticalAlignment="Bottom" Click="Enregistrer_Click" />
            </Grid>
        </StackPanel>

        <!-- Contenu complet : un seul des blocs est visible à la fois. -->
        <Grid Grid.Row="2" Margin="0,12,0,0">
            <ScrollViewer x:Name="DefilementTexte" Padding="16,0,16,0">
                <StackPanel Spacing="8">
                    <TextBlock x:Name="ApercuTexte" TextWrapping="Wrap" IsTextSelectionEnabled="True" />
                    <TextBlock x:Name="Mention" TextWrapping="Wrap" Style="{StaticResource CaptionTextBlockStyle}"
                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                </StackPanel>
            </ScrollViewer>
            <RichEditBox x:Name="ApercuRiche" IsReadOnly="True" Visibility="Collapsed" Background="Transparent"
                         BorderThickness="0" Padding="16,0,16,0" IsSpellCheckEnabled="False" TextWrapping="Wrap"
                         ScrollViewer.HorizontalScrollBarVisibility="Auto"
                         AutomationProperties.Name="Aperçu avec mise en forme" />
            <Grid x:Name="ZoneImage" Margin="16,0,16,0" Visibility="Collapsed">
                <Image x:Name="ApercuImage" Stretch="Uniform" HorizontalAlignment="Left" VerticalAlignment="Top" />
                <TextBlock x:Name="ImageAbsente" TextWrapping="Wrap" Visibility="Collapsed"
                           Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
            </Grid>
            <ScrollViewer x:Name="DefilementFichiers" Padding="16,0,16,0" Visibility="Collapsed">
                <ItemsControl x:Name="ListeFichiers">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate x:DataType="x:String">
                            <Grid ColumnSpacing="8" Margin="0,0,0,6">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <FontIcon Glyph="&#xE8A5;" FontFamily="Segoe Fluent Icons" FontSize="14"
                                          VerticalAlignment="Top" Margin="0,3,0,0"
                                          Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                <TextBlock Grid.Column="1" Text="{x:Bind}" TextWrapping="Wrap" IsTextSelectionEnabled="True" />
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>
            <TextBlock x:Name="Vide" Text="Aucun élément sélectionné." Margin="16,0,16,0"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
        </Grid>

        <!-- Application d'origine, date de copie, taille. -->
        <TextBlock x:Name="Meta" Grid.Row="3" Margin="16,8,16,14" TextWrapping="Wrap"
                   Style="{StaticResource CaptionTextBlockStyle}"
                   Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
    </Grid>
</UserControl>
```

`src/PressePapiers/Interface/VueApercu.xaml.cs`

```csharp
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

    private FenetrePrincipaleVue? _vue;
    private (long Id, string? Nom, long OngletId)? _affiche;
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
        SynchroniserOnglets();
        Afficher(vue.ElementSelectionne);
    }

    /// <summary>Place le curseur dans le champ Nom (F2 et bouton Nommer).</summary>
    public void FocaliserNom()
    {
        ChampNom.Focus(FocusState.Programmatic);
        ChampNom.SelectAll();
    }

    private void Afficher(ElementVue? ligne)
    {
        var element = ligne?.Element;

        // Une relecture du dépôt recrée les lignes : si c'est le même élément, rien ne change, et un nom en cours
        // de saisie n'est pas effacé.
        (long, string?, long)? cle = element is null ? null : (element.Id, element.Nom, element.OngletId);
        if (cle is not null && cle == _affiche)
        {
            return;
        }

        _affiche = cle;
        ApercuTexte.Visibility = Visibility.Collapsed;
        DefilementTexte.Visibility = Visibility.Collapsed;
        ApercuRiche.Visibility = Visibility.Collapsed;
        ZoneImage.Visibility = Visibility.Collapsed;
        DefilementFichiers.Visibility = Visibility.Collapsed;
        Mention.Text = string.Empty;

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
                _ = AfficherImageAsync(element);
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
            var image = new BitmapImage { DecodePixelWidth = 1000 };
            await image.SetSourceAsync(flux);
            if (cle == _affiche)
            {
                ApercuImage.Source = image;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                                       or System.Runtime.InteropServices.COMException)
        {
            if (cle == _affiche)
            {
                ImageAbsente.Text = "L'image est introuvable ou illisible sur le disque.";
                ImageAbsente.Visibility = Visibility.Visible;
            }
        }
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

    private void Supprimer_Click(object sender, RoutedEventArgs e) => _ = _vue?.SupprimerSelectionAsync();

    private void Enregistrer_Click(object sender, RoutedEventArgs e) => Enregistrer();

    private void ChampNom_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Enregistrer();
        }
    }

    private void Enregistrer() => _vue?.Enregistrer(ChampNom.Text, (ChoixOnglet.SelectedItem as OngletVue)?.Id);
}
```

`src/PressePapiers/Interface/FenetrePrincipale.xaml`

```xml
<Window
    x:Class="PressePapiers.Interface.FenetrePrincipale"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:PressePapiers.Interface"
    Title="Presse-papiers">

    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>

    <Grid x:Name="Racine">
        <Grid.RowDefinitions>
            <RowDefinition Height="32" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- Barre de titre fondue : icône, nom de l'application, badge du raccourci. -->
        <Grid x:Name="ZoneTitre" Grid.Row="0">
            <StackPanel Orientation="Horizontal" Spacing="8" Margin="12,0,0,0" VerticalAlignment="Center">
                <FontIcon Glyph="&#xE77F;" FontFamily="Segoe Fluent Icons" FontSize="14" />
                <TextBlock Text="Presse-papiers" Style="{StaticResource CaptionTextBlockStyle}" VerticalAlignment="Center" />
                <Border Background="{ThemeResource ControlFillColorSecondaryBrush}"
                        BorderBrush="{ThemeResource ControlStrokeColorDefaultBrush}" BorderThickness="1"
                        CornerRadius="4" Padding="6,1" VerticalAlignment="Center">
                    <TextBlock Text="Win+V" FontSize="11" Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                </Border>
            </StackPanel>
        </Grid>

        <!-- Volet de gauche de 200 px : les onglets sont ajoutés par le code (compteurs, Nouvel onglet). -->
        <NavigationView x:Name="Navigation" Grid.Row="1"
                        PaneDisplayMode="Left" OpenPaneLength="200"
                        IsBackButtonVisible="Collapsed" IsPaneToggleButtonVisible="False"
                        IsSettingsVisible="True"
                        SelectionChanged="Navigation_SelectionChanged"
                        ItemInvoked="Navigation_ItemInvoked">

            <!-- Zone de contenu : le NavigationView lui donne le fond en couche arrondie. -->
            <Grid x:Name="Contenu">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>

                <!-- Messages (confirmations, problèmes) en haut de la zone de contenu. -->
                <InfoBar x:Name="Annonce" Grid.Row="0" Margin="12,12,12,0" IsClosable="True"
                         Severity="{x:Bind Vue.AnnonceGravite, Mode=OneWay}"
                         Message="{x:Bind Vue.AnnonceMessage, Mode=OneWay}"
                         IsOpen="{x:Bind Vue.AnnonceOuverte, Mode=TwoWay}" />

                <Grid Grid.Row="1">
                    <Grid x:Name="ZoneHistorique">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="300" />
                            <ColumnDefinition Width="1" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>

                        <!-- Colonne du milieu : recherche, légende de Temporaire, liste. -->
                        <Grid Grid.Column="0" Padding="12,12,0,0">
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="*" />
                            </Grid.RowDefinitions>

                            <AutoSuggestBox x:Name="ChampRecherche" Grid.Row="0" Margin="0,0,12,0"
                                            PlaceholderText="Rechercher dans tout l'historique"
                                            QueryIcon="Find"
                                            Text="{x:Bind Vue.Recherche, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

                            <TextBlock Grid.Row="1" Margin="2,8,12,4" TextWrapping="Wrap"
                                       Style="{StaticResource CaptionTextBlockStyle}"
                                       Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                       Text="{x:Bind Vue.LegendeTemporaire}"
                                       Visibility="{x:Bind Vue.LegendeVisible, Mode=OneWay}" />

                            <ListView x:Name="Liste" Grid.Row="2" Margin="0,4,0,0"
                                      ItemsSource="{x:Bind Vue.Elements, Mode=OneWay}"
                                      SelectedItem="{x:Bind Vue.ElementSelectionne, Mode=TwoWay}">
                                <ListView.ItemContainerStyle>
                                    <Style TargetType="ListViewItem" BasedOn="{StaticResource DefaultListViewItemStyle}">
                                        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                                        <Setter Property="Padding" Value="8,6,8,6" />
                                    </Style>
                                </ListView.ItemContainerStyle>
                                <ListView.ItemTemplate>
                                    <DataTemplate x:DataType="local:ElementVue">
                                        <Grid ColumnSpacing="10">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="22" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <FontIcon Grid.Column="0" Glyph="{x:Bind Glyphe}" FontFamily="Segoe Fluent Icons"
                                                      FontSize="16" VerticalAlignment="Top" Margin="0,2,0,0"
                                                      Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                            <StackPanel Grid.Column="1" Spacing="2">
                                                <TextBlock Text="{x:Bind Nom}" FontWeight="SemiBold"
                                                           Visibility="{x:Bind ANom}" TextTrimming="CharacterEllipsis" />
                                                <TextBlock Text="{x:Bind Apercu}" FontFamily="{x:Bind Police}"
                                                           MaxLines="{x:Bind LignesApercu}" TextWrapping="Wrap"
                                                           TextTrimming="CharacterEllipsis" />
                                                <Grid ColumnSpacing="6">
                                                    <Grid.ColumnDefinitions>
                                                        <ColumnDefinition Width="Auto" />
                                                        <ColumnDefinition Width="*" />
                                                        <ColumnDefinition Width="Auto" />
                                                    </Grid.ColumnDefinitions>
                                                    <Border Grid.Column="0" Visibility="{x:Bind OngletVisible}" CornerRadius="4" Padding="5,0"
                                                            Background="{ThemeResource AccentFillColorDefaultBrush}">
                                                        <TextBlock Text="{x:Bind NomOnglet}" FontSize="11"
                                                                   Foreground="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
                                                    </Border>
                                                    <TextBlock Grid.Column="1" Text="{x:Bind Meta}" FontSize="11" TextTrimming="CharacterEllipsis"
                                                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                                    <TextBlock Grid.Column="2" Text="mise en forme" FontSize="11" Visibility="{x:Bind MiseEnFormeVisible}"
                                                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                                </Grid>
                                            </StackPanel>
                                            <Image Grid.Column="2" Width="64" Height="40" Stretch="UniformToFill"
                                                   VerticalAlignment="Center" Visibility="{x:Bind VignetteVisible}"
                                                   Source="{x:Bind Vignette}" />
                                        </Grid>
                                    </DataTemplate>
                                </ListView.ItemTemplate>
                            </ListView>

                            <TextBlock Grid.Row="2" Margin="12,24,24,0" TextWrapping="Wrap" HorizontalAlignment="Center"
                                       VerticalAlignment="Top" TextAlignment="Center"
                                       Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                       Text="{x:Bind Vue.MessageVide, Mode=OneWay}"
                                       Visibility="{x:Bind Vue.ListeVide, Mode=OneWay}" />
                        </Grid>

                        <Border Grid.Column="1" Margin="0,12,0,0" Background="{ThemeResource DividerStrokeColorDefaultBrush}" />

                        <!-- Colonne de droite : aperçu et rangement de l'élément choisi. -->
                        <local:VueApercu x:Name="PanneauApercu" Grid.Column="2" />
                    </Grid>

                    <!-- Page Paramètres (tâche 28), affichée par l'entrée Paramètres du volet. -->
                    <Grid x:Name="ZoneParametres" Visibility="Collapsed" />
                </Grid>
            </Grid>
        </NavigationView>
    </Grid>
</Window>
```

`src/PressePapiers/Interface/FenetrePrincipale.xaml.cs`

```csharp
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Modele;
using WinRT.Interop;

namespace PressePapiers.Interface;

/// <summary>
/// Fenêtre principale (proposition B de la maquette, rendu WinUI 3) : volet des onglets, liste des éléments et
/// recherche à gauche, aperçu à droite. Créée au démarrage et masquée ; la masquer ne termine pas l'application.
/// </summary>
public sealed partial class FenetrePrincipale : Window
{
    private static readonly TimeSpan _delaiMemoireTaille = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherQueueTimer _minuteurTaille;
    private bool _voletEnSynchronisation;

    /// <summary>Crée la fenêtre, masquée, sur le modèle de vue.</summary>
    public FenetrePrincipale(FenetrePrincipaleVue vue)
    {
        ArgumentNullException.ThrowIfNull(vue);
        Vue = vue;
        InitializeComponent();
        Handle = WindowNative.GetWindowHandle(this);

        AppliquerFond(MicaController.IsSupported());

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(ZoneTitre);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "icone-normale.ico"));
        AppliquerTailleMemorisee();

        _minuteurTaille = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _minuteurTaille.Interval = _delaiMemoireTaille;
        _minuteurTaille.IsRepeating = false;
        _minuteurTaille.Tick += (_, _) => MemoriserTaille();
        AppWindow.Changed += AppWindow_Changed;


        PanneauApercu.Lier(Vue);
        Vue.Dialogues = new GestionnaireDialogues(() => Content as FrameworkElement, () => EstVisible);
        Vue.Rafraichi += SynchroniserVolet;
        SynchroniserVolet();

        // Le NavigationView ignore une sélection posée avant son chargement : on la rejoue au premier affichage.
        Navigation.Loaded += (_, _) => SynchroniserVolet();
    }

    /// <summary>Modèle de vue lié aux contrôles.</summary>
    public FenetrePrincipaleVue Vue { get; }

    /// <summary>Handle Win32 de la fenêtre.</summary>
    public nint Handle { get; }

    /// <summary>Vrai tant que la fenêtre est affichée.</summary>
    public bool EstVisible => AppWindow.IsVisible;

    /// <summary>Affiche la fenêtre (AppWindow.Show puis PremierPlan.Amener) en notant la fenêtre d'origine du collage.</summary>
    public void AfficherPour(nint fenetreOrigine, OrigineOuverture origine) => throw new NotImplementedException("Tâche 27");

    /// <summary>Ouvre la fenêtre sur la page Paramètres.</summary>
    public void AfficherParametres() => throw new NotImplementedException("Tâche 28");

    /// <summary>Masque la fenêtre sans terminer l'application.</summary>
    public void Masquer() => throw new NotImplementedException("Tâche 27");

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange)
        {
            Vue.Affichee = sender.IsVisible;
        }

        // Pas de mémoire tant que la fenêtre est masquée : sa taille y est sans intérêt.
        if (args.DidSizeChange && sender.IsVisible)
        {
            _minuteurTaille.Stop();
            _minuteurTaille.Start();
        }
    }
}
```

`src/PressePapiers/Interface/FenetrePrincipale.Volet.cs`

```csharp
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
        _voletEnSynchronisation = true;
        try
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

            // L'entrée Paramètres garde la sélection tant que la page est ouverte.
            if (!ReferenceEquals(Navigation.SelectedItem, Navigation.SettingsItem))
            {
                Navigation.SelectedItem = entrees.FirstOrDefault(i => ((OngletVue)i.Tag).Id == Vue.OngletSelectionne?.Id);
            }
        }
        finally
        {
            _voletEnSynchronisation = false;
        }
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

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var parametres = args.IsSettingsSelected;
        ZoneHistorique.Visibility = parametres ? Visibility.Collapsed : Visibility.Visible;
        ZoneParametres.Visibility = parametres ? Visibility.Visible : Visibility.Collapsed;

        if (!_voletEnSynchronisation && args.SelectedItem is NavigationViewItem { Tag: OngletVue onglet })
        {
            Vue.ChoisirOnglet(onglet);
        }
    }

    // « Nouvel onglet » ne change pas la sélection : il ouvre la boîte de saisie du nom.
    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is "nouvel-onglet")
        {
            _ = Vue.NouvelOngletAsync();
        }
    }

    // Clic droit sur un onglet de l'utilisateur : renommer, supprimer, changer de place. Temporaire n'a pas de menu.
    private MenuFlyout CreerMenuOnglet(NavigationViewItem entree)
    {
        OngletVue Onglet() => (OngletVue)entree.Tag;
        var menu = new MenuFlyout();
        var renommer = new MenuFlyoutItem { Text = "Renommer" };
        renommer.Click += (_, _) => _ = Vue.RenommerOngletAsync(Onglet());
        var supprimer = new MenuFlyoutItem { Text = "Supprimer" };
        supprimer.Click += (_, _) => _ = Vue.SupprimerOngletAsync(Onglet());
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
}
```


- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, tous les tests existants verts, aucun test ajouté (521 réussis,
0 échec).

- [ ] **Étape 5 : Vérifier sur le poste**

```bash
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 26
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 25
```

Résultat attendu : pour le scénario 26 (environ 60 s), `=== Fin : 64 réussites, 0 échecs, 0 erreurs au journal` ; pour le
scénario 25, rejoué pour vérifier qu'il tient toujours, `=== Fin : 53 réussites, 0 échecs, 0 erreurs au journal`. Regardez
`captures/26-texte.png`, `26-rtf.png`, `26-fichiers.png`, `26-image.png`, `26-dialogue-suppression.png` et `26-dialogue-onglet.png`.

Ce que le banc prouve (mutations jouées puis annulées entre parenthèses) :
- texte : contenu complet, chasse fixe pour WindowsTerminal, méta « Copié depuis WindowsTerminal, HH:mm · 96 caractères » ; texte de
  300 004 caractères : aperçu limité à 100 000 avec sa mention, sélection en moins d'une seconde (80 ms mesurées) ;
- RTF : rendu avec gras, couleur et tableau (point 3 de la section 16 : confirmé sur un RTF réaliste écrit à la main), boîte remise
  en lecture seule dans tous les cas, y compris RTF vide, trop gros, sans texte, tronqué (mutation sans `finally` : 4 échecs) ;
  HTML seul : texte brut et mention ; bouton d'autre mode visible et libellé « Avec mise en forme » seulement pour un élément mis en forme ;
- fichiers : les trois chemins complets ; image : affichée, PNG non verrouillé, PNG manquant géré sans exception ;
- nommer : sans nom, sans onglet, ou avec des espaces seuls, rien n'est écrit et l'`InfoBar` avertit ; avec les deux, l'élément
  quitte Temporaire pour son onglet, les compteurs suivent, message de confirmation ; renommer un élément nommé garde son onglet ;
  une copie qui arrive ne remet pas à zéro un nom en cours de saisie (mutation sans la garde : échec) ; Entrée dans le champ Nom
  enregistre (frappe réelle) ;
- supprimer : un élément temporaire part sans boîte ; un élément nommé ouvre une boîte, Annuler le garde, Supprimer le supprime
  (mutation sans confirmation : échec) ;
- onglets : « Nouvel onglet » (focus puis Entrée) ouvre la boîte, Créer s'active quand le nom est saisi, la seconde boîte est refusée,
  la saisie réelle puis Entrée créent l'onglet ; fenêtre masquée aucune boîte ne s'ouvre ; renommer reprend le nom actuel ;
  Monter déplace d'une place sans toucher Temporaire ; supprimer un onglet non vide demande confirmation et supprime ses éléments
  et leurs PNG, un onglet vide part sans confirmation, Temporaire ni ne se renomme ni ne se supprime ;
- les boutons Coller et d'autre mode lèvent `CollageDemande` avec le bon mode.

Lignes `NON TESTE` : le banc écrit `NON TESTE` (jamais un faux `OK`) quand il ne peut pas frapper parce que sa fenêtre n'est pas au premier plan.

Non couvert ici : le RTF de vrais Word et Outlook (recette manuelle, spec section 14) ; une table plus large que la colonne (coupée,
défilement horizontal à vérifier à la main) ; le collage lui-même (tâche 27).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Interface/
git commit -m "Ajouter l'aperçu, le rangement des éléments et les boîtes de dialogue" -m "Colonne d'aperçu (texte, RTF par RichEditBox en lecture seule avec repli en texte brut, image lue en mémoire, liste de fichiers), commandes Coller, autre mode, Nommer et Supprimer, champs Nom et Onglet avec Enregistrer, InfoBar, et boîtes ContentDialog pour un nouvel onglet, la suppression d'un élément nommé et celle d'un onglet non vide (une à la fois, fenêtre affichée seulement). Menu contextuel des onglets : renommer, supprimer, monter, descendre."
```

