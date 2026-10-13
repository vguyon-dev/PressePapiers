### Tâche 28 : Page Paramètres (PageParametres)

**Fichiers :**
- Créer : `src/PressePapiers/Interface/FenetrePrincipaleVue.Parametres.cs`
- Créer : `src/PressePapiers/Interface/PageParametres.xaml`
- Créer : `src/PressePapiers/Interface/PageParametres.xaml.cs`
- Modifier : `src/PressePapiers/Interface/FenetrePrincipaleVue.Collage.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/VueApercu.xaml.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.xaml` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.xaml.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.Volet.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.Affichage.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : aucun test xUnit (interface WinUI). Vérification sur le poste par le banc d'essai : `$ESSAIS/Scenario28.cs` (démarrage avec Windows et ouverture de dossiers remplacés par des doublures : le banc ne touche ni au registre ni à l'Explorateur)

**Interfaces :**

- Utilise :
  - tâche 3 : `Raccourci.TryAnalyser(string texte, out Raccourci raccourci) : bool`, `Raccourci.ToString()` (forme canonique « Ctrl+Alt+Maj+V ») ;
  - tâche 10 : `Parametres` (tous les champs), `ThemeApplication { Systeme, Clair, Sombre }`, `ServiceParametres.Enregistrer(Parametres)` ;
  - tâche 16 : `DemarrageWindows.Appliquer(bool activer, string cheminExecutable)`
    (la page n'appelle que celle-ci, jamais `EstActive`) ;
  - tâche 2 : `DossierDonnees.ParDefaut()` (`Racine`, `Journal`) ;
  - tâches 25 à 27 : `FenetrePrincipaleVue` (`Reglages`, `EtatCrochet`, `Annoncer`, `Rafraichir`, `ModeAlternatif`, `LibelleAutreMode`),
    `FenetrePrincipale` (`Ouvrir`, `MontrerHistorique`, volet), `ResumeElement.EnsembleChasseFixe`.

- Fournit :
  - `public sealed partial class PageParametres : UserControl` : `void Lier(FenetrePrincipaleVue)`, `void Charger()` ;
  - dans la vue (fichier `FenetrePrincipaleVue.Parametres.cs`) : `event Action? ParametresModifies`,
    `Func<Raccourci, string?>? AppliquerRaccourci { get; set; }` (enregistre le raccourci auprès de Windows, renvoie le message de refus ou
    null ; branché par la tâche 29 sur `RelaisWindows.EnregistrerRaccourci`), `Action<bool> AppliquerDemarrage { get; set; }`
    (par défaut `DemarrageWindows.Appliquer`), `Action<string> OuvrirChemin { get; set; }` (par défaut `Process.Start` avec le shell),
    `bool EnregistrerParametres(Func<Parametres, Parametres> modifier)`, `string? ChangerRaccourci(string texte)`,
    `bool ChangerDemarrage(bool activer)`, `void OuvrirDossierDonnees()`, `void OuvrirJournal()` ;
  - dans la fenêtre : `AfficherParametres()` (contrat 4.3, plus de coquille : ouvre la fenêtre sans fenêtre d'origine, ou la remet devant,
    sur la page Paramètres), `internal void AppliquerTheme(ThemeApplication)` ;
  - changement de comportement du volet : il réagit désormais à l'**invocation** de l'utilisateur (`ItemInvoked`), plus à la sélection
    (voir les décisions ci-dessous) ;
  - `Vue.Reglages` est la source vivante des réglages : la tâche 29 doit donner `() => vue.Reglages` à `ServiceCapture` pour que le
    plafond de Temporaire réglé dans la page soit pris en compte à la copie suivante.
  - Consommateurs : tâche 29.

- [ ] **Étape 1 : Écrire la vérification sur le poste (scénario du banc)**

Créez dans `$ESSAIS`. Le scénario remplace `AppliquerDemarrage` et `OuvrirChemin` par des doublures dès le départ : **aucune valeur n'est
écrite dans le registre réel**, aucun programme n'est lancé. Le chemin d'échec du registre est joué par une doublure qui lève une exception.

`$ESSAIS/Scenario28.cs`

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Interface;
using PressePapiers.Systeme;

namespace PressePapiers.Banc;

/// <summary>
/// Tâche 28 : page Paramètres. Le démarrage avec Windows et l'ouverture de dossiers passent par des doublures : le banc ne
/// touche ni au registre ni à l'Explorateur.
/// </summary>
internal static class Scenario28
{
    public static async Task ExecuterAsync(ContexteBanc c)
    {
        var vue = c.Vue;
        var fenetre = c.Fenetre;
        var page = c.Controle<PageParametres>("ZoneParametres");
        T Sous<T>(string nom) where T : class => page.FindName(nom) as T ?? throw new InvalidOperationException(nom);
        var champRaccourci = Sous<TextBox>("ChampRaccourci");
        var messageRaccourci = Sous<TextBlock>("MessageRaccourci");
        var erreurRaccourci = Sous<TextBlock>("ErreurRaccourci");
        var demarrage = Sous<ToggleSwitch>("InterrupteurDemarrage");
        var miseEnForme = Sous<ComboBox>("ChoixMiseEnForme");
        var plafond = Sous<NumberBox>("ChampPlafond");
        var theme = Sous<ComboBox>("ChoixTheme");
        var chasseFixe = Sous<TextBox>("ChampChasseFixe");
        var etat = Sous<TextBlock>("EtatCrochet");
        var annonce = c.Controle<InfoBar>("Annonce");
        var racine = c.Controle<Grid>("Racine");
        var navigation = c.Controle<NavigationView>("Navigation");
        var zoneHistorique = c.Controle<Grid>("ZoneHistorique");
        var champRecherche = c.Controle<AutoSuggestBox>("ChampRecherche");

        // Doublures : aucune écriture dans le registre réel, aucun programme lancé.
        var demarrages = new List<bool>();
        Exception? echecDemarrage = null;
        vue.AppliquerDemarrage = activer =>
        {
            if (echecDemarrage is not null)
            {
                throw echecDemarrage;
            }

            demarrages.Add(activer);
        };
        var ouverts = new List<string>();
        vue.OuvrirChemin = chemin => ouverts.Add(chemin);
        var raccourcisRefuses = new List<Raccourci>();
        string? refusWindows = "Cette combinaison est déjà utilisée par une autre application.";
        vue.AppliquerRaccourci = raccourci =>
        {
            raccourcisRefuses.Add(raccourci);
            return refusWindows;
        };

        // --- Ouverture sur la page Paramètres (depuis l'icône), sans fenêtre d'origine.
        c.Verifier(!fenetre.EstVisible, "fenêtre masquée au départ");
        fenetre.AfficherParametres();
        await c.Attendre(900);
        c.Verifier(fenetre.EstVisible, "AfficherParametres : la fenêtre s'ouvre");
        c.Ecrire($"INFO   page {page.Visibility}, historique {zoneHistorique.Visibility}, sélection du volet : {navigation.SelectedItem?.GetType().Name} (paramètres : {ReferenceEquals(navigation.SelectedItem, navigation.SettingsItem)})");
        c.Verifier(page.Visibility == Visibility.Visible && zoneHistorique.Visibility == Visibility.Collapsed
                   && ReferenceEquals(navigation.SelectedItem, navigation.SettingsItem), "la page Paramètres est affichée, l'entrée du volet est sélectionnée");
        c.Verifier(!annonce.IsOpen, "pas de message « sans fenêtre d'origine » sur la page Paramètres");
        c.Verifier(vue.FenetreOrigine == 0, "aucune fenêtre d'origine");

        // --- Valeurs par défaut.
        c.Verifier(champRaccourci.Text == "Ctrl+Alt+Maj+V" && demarrage.IsOn && miseEnForme.SelectedIndex == 0 && plafond.Value == 5000
                   && theme.SelectedIndex == 0, "réglages par défaut affichés (raccourci, démarrage, mise en forme, plafond, thème)");
        c.Verifier(chasseFixe.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length == 9 && chasseFixe.Text.Contains("WindowsTerminal"), "la liste des 9 applications à chasse fixe est affichée");
        c.Verifier(etat.Text == "Inconnu", "état du hook : valeur initiale");
        vue.EtatCrochet = "Actif : Win+V est intercepté.";
        c.Verifier(etat.Text == "Actif : Win+V est intercepté.", "état du hook : suit FenetrePrincipaleVue.EtatCrochet");
        vue.EtatCrochet = "Inactif : la pose du hook a échoué (code 5).";
        c.Verifier(etat.Text.StartsWith("Inactif"), "état du hook inactif avec sa raison");
        await c.CapturerAsync("28-parametres.png");
        await c.Attendre(200);

        // --- Raccourci de secours : validation par Raccourci.TryAnalyser, puis acceptation par Windows.
        void Saisir(string texte)
        {
            champRaccourci.Text = texte;
            ContexteBanc.Cliquer(Sous<Button>("BoutonRaccourci"));
        }

        foreach (var invalide in new[] { "Win+V", "V", "Ctrl+Alt+", "Ctrl+Alt+Maj+Pause", "" })
        {
            Saisir(invalide);
            c.Verifier(erreurRaccourci.Visibility == Visibility.Visible && erreurRaccourci.Text.StartsWith("Raccourci non reconnu")
                       && vue.Reglages.RaccourciSecours == "Ctrl+Alt+Maj+V" && raccourcisRefuses.Count == 0,
                       $"raccourci « {invalide} » : refusé par TryAnalyser, Windows n'est pas interrogé, réglage inchangé");
        }

        Saisir("ctrl+shift+b");
        c.Verifier(raccourcisRefuses.Count == 1 && raccourcisRefuses[0] == new Raccourci(true, false, true, 0x42), "combinaison valide : transmise à Windows, forme analysée");
        c.Verifier(erreurRaccourci.Text == refusWindows && vue.Reglages.RaccourciSecours == "Ctrl+Alt+Maj+V",
                   "Windows refuse (déjà prise) : le message est dans la page, le réglage ne change pas");
        refusWindows = null;
        Saisir("ctrl+shift+b");
        c.Verifier(vue.Reglages.RaccourciSecours == "Ctrl+Maj+B" && champRaccourci.Text == "Ctrl+Maj+B"
                   && messageRaccourci.Text == "Raccourci de secours actif : Ctrl+Maj+B.", $"Windows accepte : enregistré sous sa forme canonique, « {messageRaccourci.Text} »");
        c.Verifier(c.Parametres.Charger().RaccourciSecours == "Ctrl+Maj+B", "le raccourci est écrit dans parametres.json (relu par ServiceParametres)");

        // Entrée dans le champ valide, frappe réelle.
        champRaccourci.Focus(FocusState.Programmatic);
        champRaccourci.Text = "Ctrl+Alt+F9";
        await c.Attendre(200);
        if (Touches.Taper(fenetre.Handle, Touches.Entree))
        {
            await c.Attendre(300);
            c.Verifier(vue.Reglages.RaccourciSecours == "Ctrl+Alt+F9", "Entrée dans le champ du raccourci l'applique (frappe réelle)");
        }
        else
        {
            c.Ecrire("NON TESTE  Entrée dans le champ du raccourci : fenêtre non au premier plan");
        }

        // --- Démarrage avec Windows.
        demarrage.IsOn = false;
        await c.Attendre(200);
        c.Verifier(demarrages.SequenceEqual([false]) && !vue.Reglages.DemarrageAvecWindows, "démarrage désactivé : DemarrageWindows appelé une fois (doublure), réglage écrit");
        demarrage.IsOn = true;
        await c.Attendre(200);
        c.Verifier(demarrages.SequenceEqual([false, true]) && vue.Reglages.DemarrageAvecWindows, "démarrage réactivé");
        echecDemarrage = new UnauthorizedAccessException("registre refusé");
        demarrage.IsOn = false;
        await c.Attendre(200);
        c.Verifier(demarrage.IsOn && vue.Reglages.DemarrageAvecWindows && annonce.IsOpen && annonce.Severity == InfoBarSeverity.Error,
                   "registre refusé : l'interrupteur revient, le réglage ne change pas, un message d'erreur s'affiche");
        echecDemarrage = null;
        annonce.IsOpen = false;

        // --- Mise en forme par défaut : change le collage et le libellé du bouton d'autre mode.
        miseEnForme.SelectedIndex = 1;
        await c.Attendre(200);
        c.Verifier(vue.Reglages.MiseEnFormeParDefaut == ModeCollage.AvecMiseEnForme && vue.ModeAlternatif == ModeCollage.TexteSeul, "mise en forme par défaut : avec mise en forme");
        var apercu = c.Controle<VueApercu>("PanneauApercu");
        c.Verifier(((AppBarButton)apercu.FindName("BoutonAutreMode")).Label == "Texte brut", "le bouton d'autre mode de l'aperçu passe à « Texte brut »");
        ModeCollage? demande = null;
        vue.CollageDemande += (mode, _) => demande = mode;
        miseEnForme.SelectedIndex = 0;
        await c.Attendre(150);

        // --- Plafond de Temporaire.
        plafond.Value = 100;
        await c.Attendre(150);
        c.Verifier(vue.Reglages.PlafondTemporaire == 100, "plafond : 100 enregistré");
        plafond.Value = 0;
        await c.Attendre(150);
        c.Verifier(vue.Reglages.PlafondTemporaire == 0, "plafond : 0 (désactivé) enregistré");
        plafond.Value = double.NaN;
        await c.Attendre(150);
        c.Verifier(plafond.Value == 0 && vue.Reglages.PlafondTemporaire == 0, "zone du plafond vidée : retour à la valeur enregistrée");
        plafond.Value = -5;
        await c.Attendre(150);
        c.Verifier(vue.Reglages.PlafondTemporaire >= 0, $"plafond négatif : jamais enregistré (valeur {vue.Reglages.PlafondTemporaire})");
        plafond.Value = 5000;
        await c.Attendre(150);

        // --- Thème : appliqué à la fenêtre entière, enregistré.
        theme.SelectedIndex = 1;
        await c.Attendre(400);
        c.Verifier(racine.RequestedTheme == ElementTheme.Light && racine.ActualTheme == ElementTheme.Light && vue.Reglages.Theme == ThemeApplication.Clair, "thème Clair appliqué à la fenêtre");
        c.Verifier(fenetre.AppWindow.TitleBar.ButtonForegroundColor == Microsoft.UI.Colors.Black, "thème Clair : boutons de la barre de titre noirs (sinon blancs sur blanc)");
        await c.CapturerAsync("28-clair.png");
        theme.SelectedIndex = 2;
        await c.Attendre(400);
        c.Verifier(racine.ActualTheme == ElementTheme.Dark && vue.Reglages.Theme == ThemeApplication.Sombre, "thème Sombre appliqué");
        c.Verifier(fenetre.AppWindow.TitleBar.ButtonForegroundColor == Microsoft.UI.Colors.White, "thème Sombre : boutons de la barre de titre blancs");
        await c.CapturerAsync("28-sombre.png");
        theme.SelectedIndex = 0;
        await c.Attendre(300);
        c.Verifier(racine.RequestedTheme == ElementTheme.Default && vue.Reglages.Theme == ThemeApplication.Systeme, "thème Système : suit Windows");
        c.Verifier(fenetre.AppWindow.TitleBar.ButtonForegroundColor is null, "thème Système : les boutons de la barre de titre reprennent les couleurs de Windows");

        // --- Applications en chasse fixe : enregistrées à la sortie du champ, la liste des éléments suit.
        var terminal = vue.Elements.First(e => e.Element.Application == "WindowsTerminal");
        var sql = vue.Elements.First(e => e.Element.Application == "Ssms");
        c.Verifier(terminal.Police.Source == "Cascadia Mono" && sql.Police.Source == "Cascadia Mono", "avant : terminal et Ssms en chasse fixe");
        chasseFixe.Focus(FocusState.Programmatic);
        await c.Attendre(150);
        chasseFixe.Text = "Ssms\nnotepad++.exe, Code;  \nssms\n";
        Sous<Button>("BoutonDossier").Focus(FocusState.Programmatic);
        await c.Attendre(400);
        c.Verifier(vue.Reglages.ApplicationsChasseFixe.SequenceEqual(["Ssms", "notepad++", "Code"]), $"liste enregistrée à la sortie du champ : {string.Join(", ", vue.Reglages.ApplicationsChasseFixe)}");
        c.Verifier(chasseFixe.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).SequenceEqual(["Ssms", "notepad++", "Code"]),"le champ montre la liste nettoyée (sans .exe, sans doublon)");
        c.Verifier(vue.Elements.First(e => e.Id == terminal.Id).Police.Source == "Segoe UI Variable" && vue.Elements.First(e => e.Id == sql.Id).Police.Source == "Cascadia Mono",
                   "la liste des éléments reflète la nouvelle liste : terminal en police normale, Ssms en chasse fixe");
        chasseFixe.Focus(FocusState.Programmatic);
        await c.Attendre(150);
        chasseFixe.Text = string.Empty;
        Sous<Button>("BoutonDossier").Focus(FocusState.Programmatic);
        await c.Attendre(300);
        c.Verifier(vue.Reglages.ApplicationsChasseFixe.Count == 0, "liste vidée : aucune application en chasse fixe");

        // --- Boutons : dossier des données et journal.
        ContexteBanc.Cliquer(Sous<Button>("BoutonDossier"));
        c.Verifier(ouverts.Count == 1 && ouverts[0] == c.Donnees.Racine, $"Ouvrir le dossier des données : {ouverts.LastOrDefault()}");
        ContexteBanc.Cliquer(Sous<Button>("BoutonJournal"));
        c.Verifier(ouverts.Count == 2 && ouverts[1] == c.Donnees.Journal, "journal absent : le dossier du journal est ouvert");
        File.WriteAllText(Path.Combine(c.Donnees.Journal, "journal-20261012.log"), "hier");
        File.SetLastWriteTimeUtc(Path.Combine(c.Donnees.Journal, "journal-20261012.log"), DateTime.UtcNow.AddDays(-1));
        File.WriteAllText(Path.Combine(c.Donnees.Journal, "journal-20261013.log"), "aujourd'hui");
        ContexteBanc.Cliquer(Sous<Button>("BoutonJournal"));
        c.Verifier(ouverts[2] == Path.Combine(c.Donnees.Journal, "journal-20261013.log"), "Ouvrir le journal : le fichier le plus récent");
        var essais = ouverts.Count;
        vue.OuvrirChemin = chemin =>
        {
            ouverts.Add(chemin);
            if (chemin.EndsWith(".log", StringComparison.Ordinal))
            {
                throw new System.ComponentModel.Win32Exception(1155, "aucune application associée");
            }
        };
        ContexteBanc.Cliquer(Sous<Button>("BoutonJournal"));
        c.Verifier(ouverts.Count == essais + 2 && ouverts[^1] == c.Donnees.Journal, "aucune application pour .log : repli sur le dossier du journal");

        // --- Persistance : un nouveau ServiceParametres relit tout.
        var relus = c.Parametres.Charger();
        c.Verifier(relus.RaccourciSecours == "Ctrl+Alt+F9" || relus.RaccourciSecours == "Ctrl+Maj+B", $"relecture : raccourci {relus.RaccourciSecours}");
        c.Verifier(relus.Theme == ThemeApplication.Systeme && relus.PlafondTemporaire == 5000 && relus.ApplicationsChasseFixe.Count == 0 && relus.DemarrageAvecWindows,
                   "relecture : thème, plafond, liste et démarrage tels qu'enregistrés");
        c.Verifier(relus.LargeurFenetre == 920, "relecture : la taille de la fenêtre n'a pas été écrasée par les autres réglages");

        // --- Navigation : les touches de l'historique n'agissent pas sur la page Paramètres.
        champRecherche.Focus(FocusState.Programmatic);
        demande = null;
        annonce.IsOpen = false;
        var avantTouches = c.Depot.CompterElements(c.Depot.OngletTemporaire.Id);
        if (Touches.Taper(fenetre.Handle, Touches.Entree) && Touches.Taper(fenetre.Handle, Touches.Suppr) && Touches.Taper(fenetre.Handle, Touches.F2))
        {
            await c.Attendre(400);
            c.Verifier(demande is null && !annonce.IsOpen, "Entrée, Suppr et F2 sur la page Paramètres : aucune demande de collage, aucun message");
            c.Verifier(c.Depot.CompterElements(c.Depot.OngletTemporaire.Id) == avantTouches, "Suppr sur la page Paramètres : aucun élément supprimé");
        }
        else
        {
            c.Ecrire("NON TESTE  touches de l'historique sur la page Paramètres : fenêtre non au premier plan");
        }

        Touches.Taper(fenetre.Handle, 0x32, Touches.Ctrl);
        await c.Attendre(400);
        c.Verifier(page.Visibility == Visibility.Collapsed && zoneHistorique.Visibility == Visibility.Visible && vue.OngletSelectionne?.Nom == "Code",
                   "Ctrl+2 depuis la page Paramètres : retour à l'historique, onglet Code");
        fenetre.AfficherParametres();
        await c.Attendre(500);
        c.Verifier(page.Visibility == Visibility.Visible && fenetre.EstVisible, "AfficherParametres, fenêtre déjà ouverte : bascule sur la page Paramètres");

        // Entrées du volet choisies comme l'utilisateur (focus puis Entrée).
        await c.InvoquerEntreeAsync(c.EntreeOnglet("Temporaire"));
        c.Verifier(page.Visibility == Visibility.Collapsed && zoneHistorique.Visibility == Visibility.Visible && vue.OngletSelectionne?.EstTemporaire == true,
                   "entrée Temporaire du volet : retour à la liste");
        await c.InvoquerEntreeAsync((NavigationViewItem)navigation.SettingsItem);
        c.Verifier(page.Visibility == Visibility.Visible && ReferenceEquals(navigation.SelectedItem, navigation.SettingsItem),
                   "entrée Paramètres du volet : la page s'affiche");
        await c.Attendre(500);
        c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte, Texte = "copie pendant les paramètres", Application = "msedge" });
        await c.Attendre(500);
        c.Verifier(page.Visibility == Visibility.Visible && ReferenceEquals(navigation.SelectedItem, navigation.SettingsItem),
                   "une copie qui arrive ne fait pas quitter la page Paramètres");
        Touches.Taper(fenetre.Handle, Touches.Echap);
        await c.Attendre(400);
        c.Verifier(!fenetre.EstVisible, "Échap sur la page Paramètres : la fenêtre se masque");
        fenetre.AfficherPour(0, OrigineOuverture.Icone);
        await c.Attendre(700);
        c.Verifier(zoneHistorique.Visibility == Visibility.Visible && page.Visibility == Visibility.Collapsed && vue.OngletSelectionne?.EstTemporaire == true,
                   "réouverture normale : on revient sur l'historique, Temporaire");
        fenetre.Masquer();
        await c.Attendre(200);

        c.Verifier(c.Journal.Erreurs.Count == 1 && c.Journal.Avertissements.Count == 1,
                   $"journal : une erreur (registre refusé) et un avertissement (application .log absente), sans autre : {c.Journal.Erreurs.Count} erreur(s), {c.Journal.Avertissements.Count} avertissement(s)");
    }
}
```

- [ ] **Étape 2 : Lancer le banc et constater l'échec**

```bash
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 28
```

Résultat attendu : la compilation échoue : `CS0246` (`PageParametres` introuvable), `CS1061` (`'FenetrePrincipaleVue' ne contient pas de
définition pour 'AppliquerDemarrage'`, `'OuvrirChemin'`, `'AppliquerRaccourci'`). À la rédaction : 9 erreurs.

- [ ] **Étape 3 : Implémenter**

Décisions et pièges à connaître :
- **Le volet réagit à `ItemInvoked`, plus à `SelectionChanged`.** Le `NavigationView` envoie `SelectionChanged` en différé, parfois
  après que le code a changé d'état : une sélection posée par le code (ouverture, Ctrl+Tab) était relue comme un choix de l'utilisateur
  et ramenait la fenêtre sur l'ancienne page (constaté : la page Paramètres restait masquée). `ItemInvoked` ne vient que de
  l'utilisateur (clic, ou Entrée au clavier sur l'entrée). La page affichée vient d'un seul état (`_parametresVoulus`) appliqué par
  `AfficherPage`. Les scénarios 25 et 26 du banc choisissent donc les onglets par focus puis Entrée (`InvoquerEntreeAsync`).
- Chaque réglage s'enregistre dès qu'il change, sauf le raccourci (bouton Appliquer ou Entrée) et la liste des applications (à la sortie
  du champ). `EnregistrerParametres` ne réécrit pas le fichier si rien n'a changé et prévient `ParametresModifies` (la fenêtre applique le thème).
- **Raccourci de secours** : `Raccourci.TryAnalyser` valide le texte ; si Windows refuse la combinaison (déjà prise : code 1409), le
  message reste sous le champ et le réglage ne change pas. L'enregistrement auprès de Windows est un point d'extension
  (`AppliquerRaccourci`), branché par la tâche 29.
- **Démarrage avec Windows** : si le registre refuse, l'interrupteur revient à son état et un message d'erreur s'affiche (mutation sans
  ce retour : échec). L'état affiché est celui des réglages, pas celui du registre (`EstActive` n'est pas appelée).
- **Thème** : `RequestedTheme` de la racine du contenu. Les boutons de la barre de titre suivent le thème de **Windows**, pas celui de la
  fenêtre : un thème Clair forcé sur un Windows sombre les laissait blancs sur blanc, d'où `AppWindow.TitleBar.Button*Color`
  (retour aux valeurs par défaut avec le thème Système).
- Un pinceau de thème posé par le code (`Application.Current.Resources[...]`) est celui du thème de l'application, pas de la fenêtre :
  le message d'erreur du raccourci et son message de succès sont donc deux zones de texte au pinceau fixé dans le XAML.
- `TextBox.Text` renvoie les sauts de ligne en `\r` seul : la liste des applications se coupe sur `\r`, `\n`, virgule et point-virgule,
  retire `.exe` et les doublons (casse ignorée) et se réécrit nettoyée.
- La page tient dans un `ScrollViewer` : à 560 px de haut les derniers réglages (thème, liste, boutons) se défilent.
- `AfficherParametres` sans fenêtre d'origine n'affiche pas le message « ouvert sans fenêtre d'origine » (`messageOrigine: false`).

`src/PressePapiers/Interface/FenetrePrincipaleVue.Collage.cs`

```csharp
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>Collage de l'élément choisi dans la fenêtre d'origine, et navigation au clavier dans les onglets et la liste.</summary>
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Fenêtre qui avait le focus quand la fenêtre a été ouverte ; zéro quand elle est inconnue (ouverture depuis l'icône).</summary>
    public nint FenetreOrigine { get; set; }

    /// <summary>Handle de la fenêtre principale, pour que le recollage n'y revienne jamais.</summary>
    public nint HandleOutil { get; set; }

    /// <summary>Masque la fenêtre ; appelée par le recollage au bon moment de sa séquence.</summary>
    public Action MasquerFenetre { get; set; } = () => { };

    /// <summary>Notification de l'icône (titre, message), pour un collage impossible dont la fenêtre est déjà masquée. Branchée par l'application.</summary>
    public Action<string, string>? Notifier { get; set; }

    /// <summary>
    /// Vrai pendant la séquence de recollage : la fenêtre cède le premier plan à la fenêtre d'origine, ce qui ne doit
    /// pas la faire se masquer comme une perte de focus ordinaire.
    /// </summary>
    public bool CollageEnCours { get; private set; }

    /// <summary>
    /// Colle l'élément choisi dans la fenêtre d'origine (ou le copie seulement), puis dit à l'utilisateur ce qui s'est passé
    /// quand la fenêtre reste affichée. À appeler sur le fil d'interface : le recollage y reprend après chaque attente.
    /// </summary>
    public async Task CollerAsync(ModeCollage mode, bool copieSeule)
    {
        if (ElementSelectionne is not { } ligne || CollageEnCours)
        {
            return;
        }

        CollageEnCours = true;
        try
        {
            var issue = await _recollage.CollerAsync(ligne.Element, mode, FenetreOrigine, copieSeule, HandleOutil, MasquerFenetre);
            Informer(issue);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Le collage a échoué.", exception);
            Annoncer(InfoBarSeverity.Error, "Le collage a échoué. Le détail est dans le journal.");
        }
        finally
        {
            CollageEnCours = false;
        }
    }

    /// <summary>Message qui accompagne l'issue d'un collage : rien après un collage réussi, la fenêtre s'est masquée.</summary>
    internal void Informer(IssueCollage issue)
    {
        switch (issue)
        {
            case IssueCollage.CopieDemandee:
                Annoncer(InfoBarSeverity.Success, "Copié dans le presse-papiers.");
                break;
            case IssueCollage.CopieSansFenetreOrigine:
                Annoncer(InfoBarSeverity.Informational,
                         "Copié. La fenêtre d'origine est inconnue (ouverture depuis l'icône) : colle avec Ctrl+V.");
                break;
            case IssueCollage.CopieFenetreFermee:
                Annoncer(InfoBarSeverity.Warning, "Copié. La fenêtre d'origine n'existe plus : colle avec Ctrl+V.");
                break;
            case IssueCollage.CopieFenetreElevee:
                // La fenêtre est déjà masquée : seule la notification de l'icône peut encore le dire.
                Notifier?.Invoke("Presse-papiers", "Copié, colle avec Ctrl+V");
                break;
            case IssueCollage.Echec:
                Annoncer(InfoBarSeverity.Error, "Le collage a échoué. Le détail est dans le journal.");
                break;
        }
    }

    /// <summary>Remet la fenêtre dans l'état d'une ouverture : onglet Temporaire, recherche vide, premier élément choisi.</summary>
    internal void PreparerOuverture(OrigineOuverture origine, bool messageOrigine = true)
    {
        AnnonceOuverte = false;
        ChoisirOnglet(Onglets.Count > 0 ? Onglets[0] : OngletSelectionne!);

        if (messageOrigine && origine is OrigineOuverture.Icone or OrigineOuverture.Relance)
        {
            Annoncer(InfoBarSeverity.Informational,
                     "Ouvert sans fenêtre d'origine : Entrée copie l'élément, colle-le ensuite avec Ctrl+V.");
        }
    }

    /// <summary>Affiche l'onglet dont le numéro est donné (1 pour Temporaire) ; sans effet si cet onglet n'existe pas.</summary>
    public void AllerAOnglet(int numero)
    {
        if (numero >= 1 && numero <= Onglets.Count)
        {
            ChoisirOnglet(Onglets[numero - 1]);
        }
    }

    /// <summary>Passe à l'onglet suivant (+1) ou précédent (-1), en bouclant.</summary>
    public void OngletRelatif(int decalage)
    {
        var courant = Onglets.ToList().FindIndex(o => o.Id == OngletSelectionne?.Id);
        if (courant >= 0)
        {
            var total = Onglets.Count;
            ChoisirOnglet(Onglets[((courant + decalage) % total + total) % total]);
        }
    }

    /// <summary>Déplace le choix dans la liste d'une ligne vers le haut (-1) ou le bas (+1), sans sortir de la liste.</summary>
    public void DeplacerSelection(int decalage)
    {
        if (Elements.Count == 0)
        {
            return;
        }

        var courant = ElementSelectionne is null ? -1 : Elements.ToList().FindIndex(e => e.Id == ElementSelectionne.Id);
        ElementSelectionne = Elements[Math.Clamp(courant + decalage, 0, Elements.Count - 1)];
    }
}
```

`src/PressePapiers/Interface/FenetrePrincipaleVue.Parametres.cs`

```csharp
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Systeme;

namespace PressePapiers.Interface;

/// <summary>Enregistrement des réglages et actions de la page Paramètres (spec, section 8).</summary>
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Levé après chaque enregistrement réussi des réglages : la fenêtre applique le thème, l'application les autres.</summary>
    public event Action? ParametresModifies;

    /// <summary>
    /// Enregistre le raccourci de secours auprès de Windows ; renvoie le message à montrer si la combinaison est refusée
    /// (déjà prise par une autre application), ou null si elle est enregistrée. Branché par l'application.
    /// </summary>
    public Func<Raccourci, string?>? AppliquerRaccourci { get; set; }

    /// <summary>Crée ou supprime le lancement avec Windows ; lève une exception si le registre refuse. Remplaçable pour l'essai.</summary>
    public Action<bool> AppliquerDemarrage { get; set; } =
        activer => DemarrageWindows.Appliquer(activer, Environment.ProcessPath ?? string.Empty);

    /// <summary>Ouvre un dossier ou un fichier avec l'application associée de Windows. Remplaçable pour l'essai.</summary>
    public Action<string> OuvrirChemin { get; set; } =
        chemin => Process.Start(new ProcessStartInfo(chemin) { UseShellExecute = true })?.Dispose();

    /// <summary>
    /// Enregistre de nouveaux réglages, tirés des réglages en vigueur par <paramref name="modifier"/>, puis prévient les abonnés.
    /// </summary>
    /// <returns>Vrai si le fichier a été écrit ; sinon un message d'erreur est affiché et les réglages restent ceux d'avant.</returns>
    public bool EnregistrerParametres(Func<Parametres, Parametres> modifier)
    {
        var nouveaux = modifier(Reglages);
        if (nouveaux == Reglages)
        {
            return true;
        }

        try
        {
            _parametres.Enregistrer(nouveaux);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible d'enregistrer les réglages.", exception);
            Annoncer(InfoBarSeverity.Error, "Les réglages n'ont pas pu être enregistrés. Le détail est dans le journal.");
            return false;
        }

        var chasseFixeChangee = !nouveaux.ApplicationsChasseFixe.SequenceEqual(Reglages.ApplicationsChasseFixe);
        Reglages = nouveaux;
        if (chasseFixeChangee)
        {
            _chasseFixe = ResumeElement.EnsembleChasseFixe(nouveaux.ApplicationsChasseFixe);
            Rafraichir();
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(LibelleAutreMode)));
        ParametresModifies?.Invoke();
        return true;
    }

    /// <summary>
    /// Valide puis enregistre le raccourci de secours : texte reconnu par <see cref="Raccourci.TryAnalyser"/>, puis accepté par Windows.
    /// </summary>
    /// <returns>Null si le raccourci est enregistré, sinon le message à montrer sous le champ.</returns>
    public string? ChangerRaccourci(string texte)
    {
        if (!Raccourci.TryAnalyser(texte, out var raccourci))
        {
            return "Raccourci non reconnu. Exemple : Ctrl+Alt+Maj+V (Ctrl ou Alt obligatoire, jamais la touche Windows ; "
                   + "touche : lettre, chiffre, F1 à F24 ou Espace).";
        }

        if (AppliquerRaccourci?.Invoke(raccourci) is { } refus)
        {
            return refus;
        }

        return EnregistrerParametres(p => p with { RaccourciSecours = raccourci.ToString() })
            ? null
            : "Le raccourci n'a pas pu être enregistré dans les réglages.";
    }

    /// <summary>Active ou désactive le lancement avec Windows. En cas d'échec, le réglage ne change pas et un message l'explique.</summary>
    public bool ChangerDemarrage(bool activer)
    {
        try
        {
            AppliquerDemarrage(activer);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de modifier le démarrage avec Windows.", exception);
            Annoncer(InfoBarSeverity.Error, "Le démarrage avec Windows n'a pas pu être modifié. Le détail est dans le journal.");
            return false;
        }

        return EnregistrerParametres(p => p with { DemarrageAvecWindows = activer });
    }

    /// <summary>Ouvre le dossier des données dans l'Explorateur.</summary>
    public void OuvrirDossierDonnees() => Ouvrir(DossierDonnees.ParDefaut().Racine);

    /// <summary>Ouvre le fichier du journal le plus récent, ou à défaut le dossier du journal.</summary>
    public void OuvrirJournal()
    {
        var dossier = DossierDonnees.ParDefaut().Journal;
        var recent = Directory.Exists(dossier)
            ? new DirectoryInfo(dossier).EnumerateFiles().OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
            : null;
        if (recent is null || !Ouvrir(recent.FullName))
        {
            Ouvrir(dossier);
        }
    }

    private bool Ouvrir(string chemin)
    {
        try
        {
            OuvrirChemin(chemin);
            return true;
        }
        catch (Exception exception)
        {
            _journal.Avertissement($"Impossible d'ouvrir {chemin}.", exception);
            Annoncer(InfoBarSeverity.Warning, "Impossible d'ouvrir ce dossier ou ce fichier. Le détail est dans le journal.");
            return false;
        }
    }
}
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

        // Changer le mode par défaut dans les Paramètres change le libellé du bouton d'autre mode.
        vue.ParametresModifies += () => BoutonAutreMode.Label = vue.LibelleAutreMode;
        SynchroniserOnglets();
        Afficher(vue.ElementSelectionne);
    }

    /// <summary>Vrai si ce nœud de l'arbre visuel est le champ Nom : ses touches (Entrée, flèches) lui appartiennent.</summary>
    internal bool ContientChampNom(DependencyObject noeud) => ReferenceEquals(noeud, ChampNom);

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

`src/PressePapiers/Interface/PageParametres.xaml`

```xml
<UserControl
    x:Class="PressePapiers.Interface.PageParametres"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <UserControl.Resources>
        <!-- Titre d'un réglage et sa description en dessous. -->
        <Style x:Key="TitreReglage" TargetType="TextBlock">
            <Setter Property="FontWeight" Value="SemiBold" />
        </Style>
        <Style x:Key="DescriptionReglage" TargetType="TextBlock" BasedOn="{StaticResource CaptionTextBlockStyle}">
            <Setter Property="TextWrapping" Value="Wrap" />
            <Setter Property="Foreground" Value="{ThemeResource TextFillColorSecondaryBrush}" />
        </Style>
    </UserControl.Resources>

    <ScrollViewer Padding="24,16,24,16">
        <StackPanel Spacing="20" MaxWidth="600" HorizontalAlignment="Left">
            <TextBlock Text="Paramètres" Style="{StaticResource SubtitleTextBlockStyle}" />

            <!-- Raccourci de secours -->
            <StackPanel Spacing="6">
                <TextBlock Text="Raccourci de secours" Style="{StaticResource TitreReglage}" />
                <TextBlock Style="{StaticResource DescriptionReglage}"
                           Text="Toujours actif, en plus de Win+V : utile devant une fenêtre administrateur, où Win+V n'est pas intercepté." />
                <Grid ColumnSpacing="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <TextBox x:Name="ChampRaccourci" Grid.Column="0" AutomationProperties.Name="Raccourci de secours"
                             PlaceholderText="Ctrl+Alt+Maj+V" KeyDown="ChampRaccourci_KeyDown" />
                    <Button x:Name="BoutonRaccourci" Grid.Column="1" Content="Appliquer" Click="Raccourci_Click" />
                </Grid>
                <TextBlock x:Name="MessageRaccourci" Style="{StaticResource DescriptionReglage}" Visibility="Collapsed" />
                <TextBlock x:Name="ErreurRaccourci" Style="{StaticResource DescriptionReglage}" Visibility="Collapsed"
                           Foreground="{ThemeResource SystemFillColorCriticalBrush}" />
            </StackPanel>

            <!-- Démarrage avec Windows -->
            <ToggleSwitch x:Name="InterrupteurDemarrage" Header="Démarrage avec Windows" OnContent="Activé" OffContent="Désactivé"
                          Toggled="Demarrage_Toggled" />

            <!-- Mise en forme par défaut -->
            <StackPanel Spacing="6">
                <ComboBox x:Name="ChoixMiseEnForme" Header="Mise en forme par défaut au collage" MinWidth="240"
                          SelectionChanged="MiseEnForme_SelectionChanged">
                    <ComboBoxItem Content="Texte brut" />
                    <ComboBoxItem Content="Avec mise en forme" />
                </ComboBox>
                <TextBlock Style="{StaticResource DescriptionReglage}"
                           Text="Maj+Entrée colle dans l'autre mode. Les images et les fichiers ne sont pas concernés." />
            </StackPanel>

            <!-- Plafond de Temporaire -->
            <StackPanel Spacing="6">
                <NumberBox x:Name="ChampPlafond" Header="Plafond d'éléments dans Temporaire" Minimum="0" Maximum="1000000"
                           SmallChange="100" LargeChange="1000" SpinButtonPlacementMode="Compact" MinWidth="240"
                           HorizontalAlignment="Left" ValueChanged="Plafond_ValueChanged" />
                <TextBlock Style="{StaticResource DescriptionReglage}"
                           Text="Au-delà, les plus anciens éléments sont supprimés. 0 désactive le plafond." />
            </StackPanel>

            <!-- Thème -->
            <ComboBox x:Name="ChoixTheme" Header="Thème" MinWidth="240" SelectionChanged="Theme_SelectionChanged">
                <ComboBoxItem Content="Système" />
                <ComboBoxItem Content="Clair" />
                <ComboBoxItem Content="Sombre" />
            </ComboBox>

            <!-- Applications en chasse fixe -->
            <StackPanel Spacing="6">
                <TextBox x:Name="ChampChasseFixe" Header="Applications affichées en police à chasse fixe"
                         AcceptsReturn="True" TextWrapping="Wrap" MinHeight="96" MaxHeight="160"
                         LostFocus="ChasseFixe_LostFocus" />
                <TextBlock Style="{StaticResource DescriptionReglage}"
                           Text="Un nom de processus par ligne (sans .exe), par exemple WindowsTerminal ou Code." />
            </StackPanel>

            <!-- État du hook Win+V -->
            <StackPanel Spacing="6">
                <TextBlock Text="Interception de Win+V" Style="{StaticResource TitreReglage}" />
                <TextBlock x:Name="EtatCrochet" TextWrapping="Wrap" IsTextSelectionEnabled="True" />
            </StackPanel>

            <!-- Dossiers -->
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button x:Name="BoutonDossier" Content="Ouvrir le dossier des données" Click="Dossier_Click" />
                <Button x:Name="BoutonJournal" Content="Ouvrir le journal" Click="Journal_Click" />
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`src/PressePapiers/Interface/PageParametres.xaml.cs`

```csharp
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

    <Grid x:Name="Racine" PreviewKeyDown="Racine_PreviewKeyDown">
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
                                      DoubleTapped="Liste_DoubleTapped" CharacterReceived="Liste_CharacterReceived"
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

                    <!-- Page Paramètres, affichée par l'entrée Paramètres du volet. -->
                    <local:PageParametres x:Name="ZoneParametres" Visibility="Collapsed" />
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
using PressePapiers.Systeme;
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
    private bool _parametresVoulus;
    private bool _ouvertureEnCours;
    private bool _fermetureAutorisee;

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

        // Cycle de vie : la croix masque la fenêtre au lieu de la fermer, la perte du focus la masque aussi.
        AppWindow.Closing += AppWindow_Closing;
        Activated += Fenetre_Activated;

        PanneauApercu.Lier(Vue);
        ZoneParametres.Lier(Vue);
        AppliquerTheme(Vue.Reglages.Theme);
        Vue.ParametresModifies += () => AppliquerTheme(Vue.Reglages.Theme);
        Vue.HandleOutil = Handle;
        Vue.MasquerFenetre = Masquer;
        Vue.CollageDemande += (mode, copieSeule) => _ = Vue.CollerAsync(mode, copieSeule);
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

    /// <summary>Levé quand Windows refuse de donner le premier plan à la fenêtre : l'application doit le dire par l'icône.</summary>
    public event Action? PremierPlanRefuse;

    /// <summary>
    /// Affiche la fenêtre au centre de l'écran de la fenêtre d'origine, sur Temporaire, premier élément choisi, curseur dans
    /// la recherche (AppWindow.Show puis PremierPlan.Amener). Ouverture idempotente : un signal reçu pendant que la fenêtre
    /// est affichée ou en cours d'ouverture est ignoré. Depuis l'icône ou une relance, la fenêtre d'origine n'est pas fiable :
    /// Entrée copie seulement.
    /// </summary>
    public void AfficherPour(nint fenetreOrigine, OrigineOuverture origine) => Ouvrir(fenetreOrigine, origine, messageOrigine: true);

    /// <summary>
    /// Ouvre la fenêtre sur la page Paramètres (menu de l'icône), sans fenêtre d'origine ; si elle est déjà affichée,
    /// elle est remise devant et passe sur la page Paramètres.
    /// </summary>
    public void AfficherParametres()
    {
        if (EstVisible)
        {
            PremierPlan.Amener(Handle);
        }
        else
        {
            Ouvrir(0, OrigineOuverture.Icone, messageOrigine: false);
        }

        _parametresVoulus = true;
        Navigation.SelectedItem = Navigation.SettingsItem;
        AfficherPage(parametres: true);
    }

    private void Ouvrir(nint fenetreOrigine, OrigineOuverture origine, bool messageOrigine)
    {
        if (EstVisible || _ouvertureEnCours)
        {
            // Une relance de l'application, ou l'icône, demandent la fenêtre : on la remet devant sans rien changer.
            if (EstVisible && origine is OrigineOuverture.Icone or OrigineOuverture.Relance)
            {
                PremierPlan.Amener(Handle);
            }

            return;
        }

        _ouvertureEnCours = true;
        var origineFiable = origine is OrigineOuverture.WinV or OrigineOuverture.RaccourciSecours
                            && PremierPlan.EstOrigineValide(fenetreOrigine, Handle);
        Vue.FenetreOrigine = origineFiable ? fenetreOrigine : 0;
        Vue.Affichee = true;
        Vue.PreparerOuverture(origine, messageOrigine);
        MontrerHistorique();
        PlacerAuCentre(origineFiable ? fenetreOrigine : 0);
        AppWindow.Show();
        var premierPlanObtenu = PremierPlan.Amener(Handle);
        if (!premierPlanObtenu)
        {
            PremierPlanRefuse?.Invoke();
        }

        ChampRecherche.Focus(FocusState.Programmatic);

        // Les pertes de focus survenues pendant l'ouverture elle-même sont ignorées, donc on les rattrape ici : une fenêtre
        // qui a eu le premier plan puis l'a perdu avant la fin de l'ouverture ne doit pas rester ouverte sans focus.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _ouvertureEnCours = false;
            if (premierPlanObtenu && EstVisible && PremierPlan.FenetreActive() != Handle)
            {
                Masquer();
            }
        });
    }

    /// <summary>Masque la fenêtre sans terminer l'application.</summary>
    public void Masquer() => AppWindow.Hide();

    /// <summary>Permet à la fenêtre de se fermer vraiment : à appeler avant de quitter l'application (menu de l'icône).</summary>
    public void AutoriserFermeture() => _fermetureAutorisee = true;

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

    // La croix, Alt+F4 et la fermeture par le système masquent la fenêtre : l'application vit dans la zone de notification.
    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_fermetureAutorisee)
        {
            return;
        }

        args.Cancel = true;
        Masquer();
    }

    // Perdre le focus ferme la fenêtre, sauf pendant une ouverture, un recollage (la fenêtre d'origine reprend le premier
    // plan) ou une boîte de dialogue.
    private void Fenetre_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && EstVisible && !_ouvertureEnCours
            && !Vue.CollageEnCours && Vue.Dialogues?.EnCours != true)
        {
            Masquer();
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
                _ = Vue.NouvelOngletAsync();
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

    // Revient à la liste des éléments si la page Paramètres était ouverte.
    private void MontrerHistorique()
    {
        _parametresVoulus = false;
        AfficherPage(parametres: false);
        Navigation.SelectedItem = EntreesOnglets().FirstOrDefault(i => ((OngletVue)i.Tag).Id == Vue.OngletSelectionne?.Id);
    }
}
```

`src/PressePapiers/Interface/FenetrePrincipale.Affichage.cs`

```csharp
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
```


- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, tous les tests verts, aucun test ajouté (576
réussis, 0 échec).

- [ ] **Étape 5 : Vérifier sur le poste**

```bash
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 28
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 27
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 26
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 25
```

Résultat attendu : scénario 28 (environ 50 s) `=== Fin : 57 réussites, 0 échecs, 1 erreurs au journal` (l'erreur notée est celle du registre
refusé, jouée exprès, et le scénario vérifie qu'elle est la seule, avec un seul avertissement) ; les scénarios 27, 26 et 25, rejoués pour
vérifier que le changement du volet n'a rien cassé : 65, 64 et 53 réussites, 0 échec. Regardez `captures/28-parametres.png`,
`28-clair.png` et `28-sombre.png`. Ce que le banc prouve :
- `AfficherParametres` sur une fenêtre jamais affichée : la page Paramètres est affichée, l'entrée du volet est sélectionnée, pas de
  message, aucune fenêtre d'origine ; réglages par défaut affichés ; état du hook mis à jour avec `EtatCrochet` ;
- raccourci : « Win+V », « V », « Ctrl+Alt+ », « Ctrl+Alt+Maj+Pause » et le vide sont refusés par `TryAnalyser` sans interroger Windows ;
  « ctrl+shift+b » est transmis à Windows sous la forme analysée, refusé (« déjà utilisée ») le message reste sous le champ et le réglage
  ne change pas, accepté il est enregistré sous sa forme canonique « Ctrl+Maj+B » (relu par `ServiceParametres`) ; Entrée dans le champ
  l'applique (frappe réelle) ;
- démarrage : la doublure est appelée une fois par changement, le réglage est écrit ; registre refusé : l'interrupteur revient et un
  message d'erreur apparaît ;
- mise en forme par défaut : change `ModeAlternatif` et le libellé du bouton d'autre mode de l'aperçu ; plafond : 100, 0 enregistrés,
  zone vidée (NaN) revient à la valeur enregistrée, négatif jamais enregistré ; thème : Clair, Sombre, Système appliqués à toute la
  fenêtre et boutons de la barre de titre noirs, blancs, puis par défaut ;
- applications à chasse fixe : « Ssms\nnotepad++.exe, Code;  \nssms » devient `Ssms, notepad++, Code` à la sortie du champ (mutation
  sans relecture : échec) ; la liste d'éléments suit (le terminal passe en police normale, Ssms reste à chasse fixe) ; liste vidée
  prise en compte ;
- boutons : le dossier des données et le journal reçoivent les bons chemins (le fichier le plus récent, sinon le dossier ; repli sur le
  dossier si aucune application ne gère `.log`) ;
- navigation : Entrée, Suppr et F2 sur la page Paramètres ne font rien d'une liste masquée ; Ctrl+2 ramène à l'historique ; les entrées
  Temporaire et Paramètres du volet, invoquées au clavier, changent bien de page ; une copie qui arrive ne fait pas quitter la page
  Paramètres ; Échap sur la page masque la fenêtre ; la réouverture revient sur Temporaire.

Écarts avec la maquette : aucune page Paramètres n'y figure ; elle suit les contrôles WinUI standard (`ToggleSwitch`, `ComboBox`,
`NumberBox`, `TextBox`).

Non couvert ici : l'écriture réelle dans `HKCU\...\Run` (tâche 16, à voir à la recette) ; l'enregistrement réel du raccourci par
`RegisterHotKey` (tâche 29 ; ici une doublure) ; l'ouverture réelle de l'Explorateur et du journal.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Interface/
git commit -m "Ajouter la page Paramètres" -m "Raccourci de secours validé par Raccourci.TryAnalyser puis accepté par Windows, démarrage avec Windows, mise en forme par défaut, plafond de Temporaire, thème appliqué à toute la fenêtre (boutons de la barre de titre compris), applications à chasse fixe, état du hook Win+V, accès au dossier des données et au journal. Chaque réglage s'enregistre dès qu'il change. Le volet réagit à l'invocation de l'utilisateur et non plus à la sélection, que le NavigationView envoie en différé."
```

