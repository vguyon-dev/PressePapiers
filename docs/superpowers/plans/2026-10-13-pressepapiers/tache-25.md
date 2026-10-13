### Tâche 25 : Fenêtre principale : onglets, liste, recherche (FenetrePrincipale, FenetrePrincipaleVue)

**Fichiers :**
- Créer : `src/PressePapiers/Interface/ResumeElement.cs`
- Créer : `src/PressePapiers/Interface/OngletVue.cs`
- Créer : `src/PressePapiers/Interface/ElementVue.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipaleVue.Liste.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipale.xaml`
- Créer : `src/PressePapiers/Interface/FenetrePrincipale.xaml.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipale.Volet.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipale.Affichage.cs`
- Modifier : `src/PressePapiers/Interface/FenetrePrincipaleVue.cs` (le plan donne le contenu complet du fichier après modification)
- Supprimer : `src/PressePapiers/Interface/FenetrePrincipale.cs` (coquille de la tâche 1 : la classe est définie par le XAML et son code)
- Tests : aucun test xUnit (interface WinUI : le projet de tests ne référence que Coeur). Vérification sur le poste par un banc d'essai jetable hors du dépôt, dans `%LOCALAPPDATA%\Temp\essais-interface\` : `App.xaml.cs`, `LanceurBanc.cs`, `ContexteBanc.cs`, `DonneesBanc.cs`, `Touches.cs`, `capture.ps1`, `lancer.sh`, `Scenario25.cs`

**Interfaces :**

- Utilise :
  - tâche 1 : `Element` (`Id`, `OngletId`, `Nom`, `Type`, `Texte`, `Miniature`, `Application`, `DateCopie`, `DateUsage`, `Taille`,
    `Fichiers`), `Onglet(long Id, string Nom, int Ordre, bool EstTemporaire)`, `TypeElement`, `IJournal` ;
  - tâches 4 à 7 : `DepotHistorique` : `OngletTemporaire`, `ListerOnglets() : IReadOnlyList<Onglet>`,
    `CompterElements(long ongletId) : int`, `ListerElements(long ongletId) : IReadOnlyList<Element>` (`DateUsage` puis `Id`
    décroissants), `event Action? Modifie` (peut être levé sur n'importe quel fil, après libération du verrou du dépôt) ;
  - tâche 9 : `MoteurRecherche.Chercher(string requete, int limite = 500) : IReadOnlyList<Element>` ;
  - tâche 10 : `ServiceParametres.Charger() : Parametres`, `ServiceParametres.Enregistrer(Parametres)` ;
    `Parametres.LargeurFenetre`, `HauteurFenetre` (pixels logiques), `ApplicationsChasseFixe` ;
  - tâche 24 : `ServiceRecollage` (seulement conservé par la vue ; l'appel vient de la tâche 27) ;
  - tâche 14 (`ConvertisseurImage.MiniatureAsync`) et tâche 19 (`PremierPlan.Amener`) : utilisées par le banc seulement.

- Fournit :
  - `public sealed partial class FenetrePrincipaleVue : ObservableObject` (contrat 4.3) avec le constructeur
    `FenetrePrincipaleVue(DepotHistorique, MoteurRecherche, ServiceRecollage, ServiceParametres, IJournal)` (à créer sur le fil
    d'interface : il capture son `DispatcherQueue`) et `string EtatCrochet { get; set; }` (valeur initiale « Inconnu ») ;
  - dans la vue : `TimeProvider Horloge { get; set; }`, `Parametres Reglages { get; }`, `IReadOnlyList<OngletVue> Onglets { get; }`,
    `OngletVue? OngletSelectionne { get; }`, `IReadOnlyList<ElementVue> Elements { get; }`, `ElementVue? ElementSelectionne { get; set; }`,
    `string Recherche { get; set; }` (la liste suit 150 ms après la dernière frappe ; vidée, elle suit tout de suite),
    `bool EnRecherche`, `bool LegendeVisible`, `string LegendeTemporaire`, `bool ListeVide`, `string MessageVide`,
    `bool Affichee { get; set; }` (fenêtre affichée : masquée, la vue ne relit rien), `event Action? Rafraichi`,
    `void ChoisirOnglet(OngletVue)`, `void EffacerRecherche()`, `void Rafraichir(bool premierElement = false)`,
    `void MemoriserTaille(int largeur, int hauteur)` ;
  - `public sealed record OngletVue(long Id, string Nom, bool EstTemporaire, int Compteur)` ;
  - `public sealed class ElementVue` (`Element`, `Id`, `Glyphe`, `Nom`, `ANom`, `LignesApercu`, `Apercu`, `Police`, `Meta`, `NomOnglet`,
    `OngletVisible`, `MiseEnFormeVisible`, `VignetteVisible`, `Vignette`) ;
  - `internal static class ResumeElement` (`Glyphe`, `ApercuTexte`, `Apercu`, `Taille`, `Heure`, `EstChasseFixe`, `EnsembleChasseFixe`) ;
  - `public sealed partial class FenetrePrincipale : Window` (contrat 4.3) : constructeur `FenetrePrincipale(FenetrePrincipaleVue)`,
    `FenetrePrincipaleVue Vue`, `nint Handle`, `bool EstVisible` ; `AfficherPour`, `AfficherParametres` et `Masquer` restent des
    coquilles (tâches 27 et 28) ; `internal void AppliquerFond(bool mica)` ;
  - le fichier `FenetrePrincipale.cs` (coquille) disparaît : la classe est définie par `FenetrePrincipale.xaml` et son code, répartis
    en fichiers partiels par responsabilité (fichiers courts) : `FenetrePrincipale.xaml.cs` (construction, ouverture, fermeture),
    `FenetrePrincipale.Volet.cs` (volet des onglets), `FenetrePrincipale.Affichage.cs` (fond, thème, taille, position) ; la tâche 27
    y ajoute `FenetrePrincipale.Clavier.cs`. De même la vue : `FenetrePrincipaleVue.cs` (état, constructeur) et
    `FenetrePrincipaleVue.Liste.cs` (relecture du dépôt, recherche différée).
  - Consommateurs : tâches 26 à 28 (aperçu, clavier, Paramètres), tâche 29 (composition).

- [ ] **Étape 1 : Écrire la vérification sur le poste (banc d'essai jetable, hors du dépôt)**

Le banc crée les vrais services (base SQLite, relais, recollage, réglages) sur un dossier de données jetable, le remplit d'éléments
réalistes et longs (code, SQL, URL, courriel avec RTF, liste de fichiers, image 1920 x 1080, trois onglets nommés), puis exerce la
vraie fenêtre : mesures, frappes réelles, captures d'écran. Il sert aux tâches 25 à 28 ; chaque tâche y ajoute un scénario.

```bash
# À lancer depuis la racine du dépôt. Le banc d'essai vit hors du dépôt, dans un dossier à chemin court.
ESSAIS="$LOCALAPPDATA/Temp/essais-interface"
mkdir -p "$ESSAIS"
```

Consignes du banc (valables pour toutes les tâches de l'interface) :
- `lancer.sh` recopie le banc dans le dépôt (dossier `src/PressePapiers/Banc`, et un `App.xaml.cs` de remplacement),
  compile la solution, lance l'application avec `PRESSEPAPIERS_DONNEES` pointant vers un dossier jetable
  (`%LOCALAPPDATA%\Temp\pp-banc-donnees`, jamais les données réelles), puis remet le dépôt dans son état committé (`git checkout`
  de `App.xaml.cs`, suppression du dossier `Banc`). Rien du banc n'est committé : si `git status` montre un fichier du banc, supprimez-le.
- Le banc exige Git Bash (`cygpath`, `timeout`) et Windows PowerShell 5.1 (`powershell.exe`, pour les captures d'écran).
  Il affiche la vraie fenêtre : **ne touchez ni au clavier ni à la souris pendant l'essai** (une minute environ), et fermez ce qui
  pourrait voler le premier plan. Les frappes réelles ne sont envoyées que si la fenêtre du banc est au premier plan ; sinon le
  contrôle est noté `ECHEC` ou `NON TESTE` plutôt que de frapper dans une autre application. Un contrôle qui échoue parce qu'une
  autre fenêtre a pris le premier plan réussit en général au second passage : relancez avant de chercher un défaut.
- Le banc remplace `App.xaml.cs` en bloc par une application qui joue les scénarios. Si `Program.cs` (tâche 16) ne fait pas
  `new App()` dans `Application.Start`, adaptez la ligne correspondante du banc.

Créez les fichiers suivants dans `$ESSAIS`.

`$ESSAIS/App.xaml.cs`

```csharp
using Microsoft.UI.Xaml;
using PressePapiers.Banc;

namespace PressePapiers;

/// <summary>Application du banc d'essai (jetable, jamais committée) : exécute les scénarios au lieu de la vraie composition.</summary>
public sealed partial class App : Application
{
    /// <summary>Charge les ressources XAML.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = LanceurBanc.ExecuterAsync();
    }
}
```

`$ESSAIS/LanceurBanc.cs`

```csharp
using System.Reflection;
using Microsoft.UI.Xaml;

namespace PressePapiers.Banc;

/// <summary>
/// Point d'entrée du banc : crée le contexte (services réels sur un dossier de données jetable), joue les scénarios
/// demandés par la variable PRESSEPAPIERS_BANC (liste de numéros de tâche, par exemple « 25,26 »), écrit le verdict
/// dans banc-resultats.txt puis quitte avec un code de sortie non nul s'il y a un échec.
/// </summary>
internal static class LanceurBanc
{
    public static async Task ExecuterAsync()
    {
        var code = 1;
        ContexteBanc? contexte = null;
        try
        {
            contexte = await ContexteBanc.CreerAsync();
            var demandes = (Environment.GetEnvironmentVariable("PRESSEPAPIERS_BANC") ?? "25")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var numero in demandes)
            {
                var type = Assembly.GetExecutingAssembly().GetType($"PressePapiers.Banc.Scenario{numero}")
                           ?? throw new InvalidOperationException($"Scénario {numero} introuvable.");
                contexte.Ecrire($"=== Scénario {numero}");
                var tache = (Task)type.GetMethod("ExecuterAsync")!.Invoke(null, [contexte])!;
                await tache;
            }

            code = contexte.Echecs == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            contexte?.Ecrire("EXCEPTION " + exception);
            if (contexte is null)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "banc-exception.txt"), exception.ToString());
            }
        }
        finally
        {
            contexte?.Ecrire($"=== Fin : {contexte.Reussites} réussites, {contexte.Echecs} échecs, {contexte.Journal.Erreurs.Count} erreurs au journal");
            contexte?.Liberer();
            Environment.Exit(code);
        }
    }
}
```

`$ESSAIS/ContexteBanc.cs`

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PressePapiers.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Interface;
using PressePapiers.Systeme;
using Windows.Graphics;

namespace PressePapiers.Banc;

/// <summary>Journal du banc : tout est écrit dans journal-banc.txt ; les erreurs sont aussi gardées pour le verdict.</summary>
internal sealed class JournalBanc(string fichier) : IJournal
{
    public List<string> Erreurs { get; } = [];
    public List<string> Avertissements { get; } = [];
    public List<string> Lignes { get; } = [];

    public void Info(string message) => Noter("INFO", message, null);

    public void Avertissement(string message, Exception? exception = null)
    {
        Avertissements.Add(message);
        Noter("AVERT", message, exception);
    }

    public void Erreur(string message, Exception? exception = null)
    {
        Erreurs.Add(message);
        Noter("ERREUR", message, exception);
    }

    private void Noter(string niveau, string message, Exception? exception)
    {
        var ligne = $"{niveau} {message}{(exception is null ? "" : " | " + exception)}";
        lock (Lignes)
        {
            Lignes.Add(ligne);
            File.AppendAllText(fichier, ligne + Environment.NewLine);
        }
    }
}

/// <summary>Horloge décalable : le banc date ses éléments d'exemple dans le passé, puis revient à l'heure réelle.</summary>
internal sealed class HorlogeBanc : TimeProvider
{
    public TimeSpan Decalage { get; set; }

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Decalage;
}

/// <summary>Services réels sur un dossier de données jetable (PRESSEPAPIERS_DONNEES), plus les aides des scénarios.</summary>
internal sealed class ContexteBanc
{
    private readonly string _fichierResultats;
    private readonly BaseHistorique _base;
    private readonly RelaisWindows _relais;

    private ContexteBanc(DossierDonnees donnees, JournalBanc journal, HorlogeBanc horloge, BaseHistorique baseHistorique,
                         DepotHistorique depot, MoteurRecherche moteur, RelaisWindows relais, ServiceRecollage recollage,
                         ServiceParametres parametres, FenetrePrincipaleVue vue, FenetrePrincipale fenetre)
    {
        Donnees = donnees;
        Journal = journal;
        Horloge = horloge;
        _base = baseHistorique;
        Depot = depot;
        Moteur = moteur;
        _relais = relais;
        Recollage = recollage;
        Parametres = parametres;
        Vue = vue;
        Fenetre = fenetre;
        _fichierResultats = Path.Combine(donnees.Racine, "banc-resultats.txt");
    }

    public DossierDonnees Donnees { get; }
    public JournalBanc Journal { get; }
    public HorlogeBanc Horloge { get; }
    public DepotHistorique Depot { get; }
    public MoteurRecherche Moteur { get; }
    public ServiceRecollage Recollage { get; }
    public ServiceParametres Parametres { get; }
    public FenetrePrincipaleVue Vue { get; }
    public FenetrePrincipale Fenetre { get; }
    public nint HandleRelais => _relais.Handle;
    public int Reussites { get; private set; }
    public int Echecs { get; set; }

    /// <summary>Répertoire des captures d'écran (variable PRESSEPAPIERS_CAPTURES, sinon le dossier de données).</summary>
    public string DossierCaptures => Environment.GetEnvironmentVariable("PRESSEPAPIERS_CAPTURES") ?? Donnees.Racine;

    public static async Task<ContexteBanc> CreerAsync()
    {
        var donnees = DossierDonnees.ParDefaut();
        if (Environment.GetEnvironmentVariable("PRESSEPAPIERS_DONNEES") is not { Length: > 0 })
        {
            throw new InvalidOperationException("Le banc exige PRESSEPAPIERS_DONNEES : jamais les données réelles.");
        }

        donnees.CreerDossiers();
        var journal = new JournalBanc(Path.Combine(donnees.Racine, "journal-banc.txt"));
        var horloge = new HorlogeBanc();
        var baseHistorique = BaseHistorique.Ouvrir(donnees.Base);
        var depot = new DepotHistorique(baseHistorique, donnees, horloge, journal);
        var moteur = new MoteurRecherche(baseHistorique);
        var relais = new RelaisWindows(journal);
        var recollage = new ServiceRecollage(new EcrivainPressePapiers(relais.Handle, journal), depot, journal);
        var parametres = new ServiceParametres(donnees.Parametres, journal);

        if (depot.CompterElements(depot.OngletTemporaire.Id) == 0 && depot.ListerOnglets().Count == 1)
        {
            await DonneesBanc.RemplirAsync(depot, horloge);
        }

        var vue = new FenetrePrincipaleVue(depot, moteur, recollage, parametres, journal);
        var fenetre = new FenetrePrincipale(vue);
        return new ContexteBanc(donnees, journal, horloge, baseHistorique, depot, moteur, relais, recollage, parametres, vue, fenetre);
    }

    public void Liberer()
    {
        _relais.Dispose();
        _base.Dispose();
    }

    public void Ecrire(string ligne)
    {
        File.AppendAllText(_fichierResultats, ligne + Environment.NewLine);
    }

    /// <summary>Note un contrôle : « OK » ou « ECHEC », avec le libellé.</summary>
    public void Verifier(bool condition, string libelle)
    {
        if (condition)
        {
            Reussites++;
        }
        else
        {
            Echecs++;
        }

        Ecrire((condition ? "OK     " : "ECHEC  ") + libelle);
    }

    public Task Attendre(int millisecondes) => Task.Delay(millisecondes);

    /// <summary>Affiche la fenêtre au premier plan, à une position fixe, et laisse la mise en page se faire.</summary>
    public async Task AfficherAsync()
    {
        Fenetre.Activate();
        Fenetre.AppWindow.Move(new PointInt32(60, 30));
        PremierPlan.Amener(Fenetre.Handle);
        await Attendre(900);
    }

    /// <summary>Échelle d'affichage de la fenêtre (100 % = 1,0).</summary>
    public double Echelle => Fenetre.Content.XamlRoot.RasterizationScale;

    /// <summary>Retrouve un contrôle nommé dans le contenu de la fenêtre.</summary>
    public T Controle<T>(string nom) where T : class =>
        ((FrameworkElement)Fenetre.Content).FindName(nom) as T ?? throw new InvalidOperationException($"Contrôle {nom} introuvable.");

    /// <summary>
    /// Choisit une entrée du volet comme l'utilisateur : focus clavier puis Entrée (ItemInvoked). Faux, avec un échec noté,
    /// si la fenêtre n'est pas au premier plan : aucune frappe n'est alors envoyée.
    /// </summary>
    public async Task<bool> InvoquerEntreeAsync(NavigationViewItem entree)
    {
        entree.Focus(FocusState.Keyboard);
        await Attendre(150);
        var envoyee = Touches.Taper(Fenetre.Handle, Touches.Entree);
        if (!envoyee)
        {
            Ecrire("ECHEC  Entrée sur une entrée du volet non envoyée : fenêtre non au premier plan");
            Echecs++;
        }

        await Attendre(300);
        return envoyee;
    }

    /// <summary>Entrée du volet pour un onglet, par son nom (Temporaire, Code, ...).</summary>
    public NavigationViewItem EntreeOnglet(string nom) =>
        Controle<NavigationView>("Navigation").MenuItems.OfType<NavigationViewItem>().First(i => i.Tag is OngletVue o && o.Nom == nom);

    /// <summary>Tous les descendants d'un type donné dans l'arbre visuel.</summary>
    public static IEnumerable<T> Descendants<T>(DependencyObject racine) where T : DependencyObject
    {
        var nombre = VisualTreeHelper.GetChildrenCount(racine);
        for (var i = 0; i < nombre; i++)
        {
            var enfant = VisualTreeHelper.GetChild(racine, i);
            if (enfant is T trouve)
            {
                yield return trouve;
            }

            foreach (var plus in Descendants<T>(enfant))
            {
                yield return plus;
            }
        }
    }

    /// <summary>Active un bouton comme le ferait un lecteur d'écran (modèle d'automatisation) : sans souris ni clavier réels.</summary>
    public static void Cliquer(Microsoft.UI.Xaml.Controls.Primitives.ButtonBase bouton)
    {
        var pair = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(bouton);
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)pair.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
    }

    /// <summary>Contenu des boîtes de dialogue ouvertes sur la fenêtre (les ContentDialog vivent dans une fenêtre flottante de la racine).</summary>
    public IEnumerable<DependencyObject> BoitesOuvertes() =>
        VisualTreeHelper.GetOpenPopupsForXamlRoot(Fenetre.Content.XamlRoot).Select(p => (DependencyObject)p.Child);

    /// <summary>Bouton de la boîte ouverte (PrimaryButton, CloseButton), ou null s'il n'y a pas de boîte.</summary>
    public Microsoft.UI.Xaml.Controls.Button? BoutonBoite(string nom) =>
        BoitesOuvertes().SelectMany(Descendants<Microsoft.UI.Xaml.Controls.Button>).FirstOrDefault(b => b.Name == nom);

    /// <summary>Zone de saisie de la boîte ouverte, ou null.</summary>
    public Microsoft.UI.Xaml.Controls.TextBox? ChampBoite() =>
        BoitesOuvertes().SelectMany(Descendants<Microsoft.UI.Xaml.Controls.TextBox>).FirstOrDefault();


    /// <summary>Capture la fenêtre (CopyFromScreen, par PowerShell) dans un PNG du dossier des captures ; renvoie le chemin.</summary>
    public async Task<string> CapturerAsync(string nom, bool amener = true)
    {
        Directory.CreateDirectory(DossierCaptures);
        if (amener)
        {
            PremierPlan.Amener(Fenetre.Handle);
            await Attendre(300);
        }

        const int DwmwaExtendedFrameBounds = 9;
        _ = DwmGetWindowAttribute(Fenetre.Handle, DwmwaExtendedFrameBounds, out var cadre, Marshal.SizeOf<Rect>());
        var png = Path.Combine(DossierCaptures, nom);
        var script = Environment.GetEnvironmentVariable("PRESSEPAPIERS_CAPTURE_PS1") ?? throw new InvalidOperationException("PRESSEPAPIERS_CAPTURE_PS1 manque.");
        var info = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Png \"{png}\" -X {cadre.Gauche} -Y {cadre.Haut} -Largeur {cadre.Droite - cadre.Gauche} -Hauteur {cadre.Bas - cadre.Haut}")
        { UseShellExecute = false, CreateNoWindow = true };
        using var processus = Process.Start(info)!;
        await processus.WaitForExitAsync();
        return png;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Gauche, Haut, Droite, Bas;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribut, out Rect valeur, int taille);
}
```

`$ESSAIS/DonneesBanc.cs`

```csharp
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using PressePapiers.Coeur.Formats;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PressePapiers.Banc;

/// <summary>Éléments d'exemple réalistes : textes longs, code, RTF d'Outlook avec tableau, image, fichiers, onglets nommés.</summary>
internal static class DonneesBanc
{
    /// <summary>RTF tel que Word ou Outlook en produisent : gras, italique, couleur, tableau bordé avec en-tête grisé.</summary>
    public const string RtfOutlook = """
        {\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1036{\fonttbl{\f0\fswiss\fcharset0 Calibri;}{\f1\fnil\fcharset0 Segoe UI;}}
        {\colortbl ;\red192\green0\blue0;\red31\green78\blue121;\red217\green217\blue217;}
        \viewkind4\uc1\pard\sa160\sl259\slmult1\f0\fs22 Bonjour Madame \b Lefebvre-Dumontier\b0 ,\par
        Suite \'e0 notre \'e9change t\'e9l\'e9phonique de ce matin, je vous confirme la livraison de la commande \b\cf1 n\'b0 2026-10-0457\cf0\b0  pr\'e9vue \i jeudi 16 octobre\i0  entre 8 h et 10 h, quai 4.\par
        Le d\'e9tail de la livraison est le suivant :\par
        \trowd\trgaph108\trleft0\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\clcbpat3\cellx4200\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\clcbpat3\cellx6200\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\clcbpat3\cellx8200
        \pard\intbl\b D\'e9signation\cell Quantit\'e9\cell Quai\cell\b0\row
        \trowd\trgaph108\trleft0\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\cellx4200\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\cellx6200\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\cellx8200
        \pard\intbl Palettes de film \'e9tirable\cell 24\cell 4\cell\row
        \trowd\trgaph108\trleft0\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\cellx4200\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\cellx6200\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10\cellx8200
        \pard\intbl Cartons de 12 bouteilles\cell 180\cell 4\cell\row
        \pard\sa160\sl259\slmult1\par
        Nous restons \cf2\ul \'e0 votre disposition\ulnone\cf0  pour tout compl\'e9ment.\par
        Bien cordialement,\par
        \b Service exp\'e9ditions\b0\par
        }
        """;

    public const string TexteOutlook =
        "Bonjour Madame Lefebvre-Dumontier,\r\nSuite à notre échange téléphonique de ce matin, je vous confirme la livraison de la commande "
        + "n° 2026-10-0457 prévue jeudi 16 octobre entre 8 h et 10 h, quai 4.\r\nLe détail de la livraison est le suivant :\r\n"
        + "Désignation\tQuantité\tQuai\r\nPalettes de film étirable\t24\t4\r\nCartons de 12 bouteilles\t180\t4\r\n\r\n"
        + "Nous restons à votre disposition pour tout complément.\r\nBien cordialement,\r\nService expéditions";

    private const string RelanceFacture =
        "Madame, Monsieur,\r\nSauf erreur de notre part, la facture ci-jointe reste impayée à ce jour. Nous vous remercions de bien vouloir "
        + "procéder à son règlement dans les meilleurs délais.\r\nBien cordialement";

    private const string RequeteClients = """
        SELECT c.IDClient, c.RaisonSociale, SUM(f.MontantHT) AS CA2026
        FROM Client c
        JOIN Facture f ON f.IDClient = c.IDClient
        WHERE f.DateFacture >= '2026-01-01'
        GROUP BY c.IDClient, c.RaisonSociale
        ORDER BY CA2026 DESC;
        """;

    private const string TexteLong =
        "Compte rendu de la réunion du comité de pilotage du 9 octobre 2026. Présents : la direction industrielle, le responsable "
        + "de l'atelier de conditionnement, la responsable qualité et deux représentants du service informatique. Premier point : "
        + "le déploiement des nouveaux postes de l'atelier est reporté de deux semaines, faute de lecteurs de codes-barres compatibles. "
        + "Deuxième point : la bascule de la base de production vers MariaDB se fera en deux temps, d'abord la lecture seule le "
        + "lundi 19 octobre, puis l'écriture le lundi suivant, après validation des sauvegardes. Troisième point : le planning des "
        + "congés de fin d'année sera publié au plus tard le 31 octobre. Prochaine réunion le jeudi 23 octobre à 14 h.";

    /// <summary>Remplit la base : trois onglets nommés et neuf éléments temporaires, datés dans le passé, du plus ancien au plus récent.</summary>
    public static async Task RemplirAsync(DepotHistorique depot, HorlogeBanc horloge)
    {
        var code = depot.CreerOnglet("Code");
        var adresses = depot.CreerOnglet("Adresses");
        var modeles = depot.CreerOnglet("Modèles de mails");

        horloge.Decalage = -TimeSpan.FromDays(11);
        Nommer(depot, Texte("Ssms", """
            SELECT c.RaisonSociale, SUM(f.MontantHT) AS CA
            FROM Client c JOIN Facture f ON f.IDClient = c.IDClient
            WHERE c.Actif = 1 AND YEAR(f.DateFacture) = YEAR(GETDATE())
            GROUP BY c.RaisonSociale;
            """), "Clients actifs avec chiffre d'affaires de l'année", code);
        horloge.Decalage = -TimeSpan.FromDays(8);
        Nommer(depot, Texte("WindowsTerminal", "dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true"),
               "Publier la version Release autonome", code);
        horloge.Decalage = -TimeSpan.FromDays(14);
        Nommer(depot, Texte("WINWORD", "3 place de la Mairie, 27000 Évreux"), "Siège", adresses);
        horloge.Decalage = -TimeSpan.FromDays(13);
        Nommer(depot, Texte("OUTLOOK", "Entrepôt Nord, quai 4\n12 rue des Tanneurs\n76800 Saint-Étienne-du-Rouvray"),
               "Adresse de livraison de l'entrepôt de Saint-Étienne-du-Rouvray (quai 4, accès poids lourds)", adresses);
        horloge.Decalage = -TimeSpan.FromDays(21);
        Nommer(depot, new ContenuCopie
        {
            Type = TypeElement.MisEnForme, Texte = RelanceFacture, Application = "OUTLOOK",
            Rtf = Encoding.ASCII.GetBytes(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Calibri;}}\f0\fs22 Madame, Monsieur,\par Sauf erreur de notre part, la facture ci-jointe reste \b impay\'e9e\b0  \'e0 ce jour.\par}"),
        }, "Relance de facture impayée (formule polie)", modeles);

        // Temporaire, du plus ancien au plus récent (la date d'usage suit l'ordre d'ajout).
        horloge.Decalage = -TimeSpan.FromMinutes(75);
        depot.AjouterCopie(Texte("Ssms", RequeteClients));
        horloge.Decalage = -TimeSpan.FromMinutes(73);
        depot.AjouterCopie(Texte("ms-teams", "06 12 34 56 78"));
        horloge.Decalage = -TimeSpan.FromMinutes(68);
        depot.AjouterCopie(Texte("msedge",
            "https://learn.microsoft.com/fr-fr/windows/win32/dataxchg/using-the-clipboard#monitoring-clipboard-contents"));
        horloge.Decalage = -TimeSpan.FromMinutes(64);
        depot.AjouterCopie(Texte("WINWORD", TexteLong));
        horloge.Decalage = -TimeSpan.FromMinutes(61);
        depot.AjouterCopie(new ContenuCopie
        {
            Type = TypeElement.MisEnForme, Texte = TexteOutlook, Application = "OUTLOOK", Rtf = Encoding.ASCII.GetBytes(RtfOutlook),
        });
        horloge.Decalage = -TimeSpan.FromMinutes(52);
        depot.AjouterCopie(new ContenuCopie
        {
            Type = TypeElement.Fichiers, Application = "explorer",
            Fichiers =
            [
                @"C:\Mes Projets\Autres\IndicateurVerrouTouche\docs\superpowers\specs\2026-06-10-indicateur-verrou-touche-design.md",
                @"C:\Mes Projets\Autres\IndicateurVerrouTouche\README.md",
                @"D:\Partage\Procédures\Procédure de déploiement des postes de l'atelier (version 3).docx",
            ],
        });
        horloge.Decalage = -TimeSpan.FromMinutes(48);
        var png = await PngDegradeAsync(1920, 1080);
        depot.AjouterCopie(new ContenuCopie
        {
            Type = TypeElement.Image, ImagePng = png, Miniature = await ConvertisseurImage.MiniatureAsync(png, 160), Application = "SnippingTool",
        });
        horloge.Decalage = -TimeSpan.FromMinutes(40);
        depot.AjouterCopie(Texte("WindowsTerminal",
            """git log --oneline --graph --decorate --all -- "src/Services/Presse papiers/HistoriqueService.cs" """.Trim()));
        horloge.Decalage = TimeSpan.Zero;
    }

    private static ContenuCopie Texte(string application, string texte) =>
        new() { Type = TypeElement.Texte, Texte = texte, Application = application };

    private static void Nommer(DepotHistorique depot, ContenuCopie contenu, string nom, Onglet onglet)
    {
        var element = depot.AjouterCopie(contenu);
        depot.Nommer(element.Id, nom, onglet.Id);
    }

    /// <summary>PNG d'un dégradé bleu, turquoise, orange avec un cadre clair, comme une capture d'écran stylisée.</summary>
    public static async Task<byte[]> PngDegradeAsync(int largeur, int hauteur)
    {
        var pixels = new byte[largeur * hauteur * 4];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                var i = (y * largeur + x) * 4;
                var t = (x / (double)largeur + y / (double)hauteur) / 2;
                var cadre = x is > 120 and < 1800 && y is > 100 and < 980 && (x < 140 || x > 1780 || y < 120 || y > 960);
                pixels[i] = cadre ? (byte)240 : (byte)(180 - 120 * t);
                pixels[i + 1] = cadre ? (byte)240 : (byte)(108 + 70 * t);
                pixels[i + 2] = cadre ? (byte)240 : (byte)(43 + 200 * t);
                pixels[i + 3] = 255;
            }
        }

        using var flux = new InMemoryRandomAccessStream();
        var encodeur = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, flux);
        encodeur.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)largeur, (uint)hauteur, 96, 96, pixels);
        await encodeur.FlushAsync();
        var png = new byte[flux.Size];
        flux.Seek(0);
        await flux.ReadAsync(png.AsBuffer(), (uint)flux.Size, InputStreamOptions.None);
        return png;
    }
}
```

`$ESSAIS/Touches.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Banc;

/// <summary>
/// Frappes réelles (SendInput) pour exercer les raccourcis de la fenêtre. Garde-fou : rien n'est envoyé si la fenêtre
/// attendue n'est pas au premier plan, pour ne jamais frapper dans une autre application.
/// </summary>
internal static class Touches
{
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const uint KeyUnicode = 0x0004;

    public const ushort Entree = 0x0D, Echap = 0x1B, Tab = 0x09, Suppr = 0x2E, F2 = 0x71, Haut = 0x26, Bas = 0x28;
    public const ushort Maj = 0x10, Ctrl = 0x11, Alt = 0x12;

    /// <summary>Appuie sur la touche en maintenant les modificateurs, puis relâche tout. Faux si la fenêtre n'est pas au premier plan.</summary>
    public static bool Taper(nint fenetreAttendue, ushort touche, params ushort[] modificateurs)
    {
        if (GetForegroundWindow() != fenetreAttendue)
        {
            return false;
        }

        var entrees = new List<Frappe>();
        foreach (var m in modificateurs)
        {
            entrees.Add(Clavier(m, 0, 0));
        }

        entrees.Add(Clavier(touche, 0, 0));
        entrees.Add(Clavier(touche, 0, KeyUp));
        foreach (var m in modificateurs.Reverse())
        {
            entrees.Add(Clavier(m, 0, KeyUp));
        }

        return Envoyer(entrees);
    }

    /// <summary>Saisit un texte caractère par caractère (frappes Unicode). Faux si la fenêtre n'est pas au premier plan.</summary>
    public static bool Ecrire(nint fenetreAttendue, string texte)
    {
        if (GetForegroundWindow() != fenetreAttendue)
        {
            return false;
        }

        var entrees = new List<Frappe>();
        foreach (var c in texte)
        {
            entrees.Add(Clavier(0, c, KeyUnicode));
            entrees.Add(Clavier(0, c, KeyUnicode | KeyUp));
        }

        return Envoyer(entrees);
    }

    private static Frappe Clavier(ushort touche, ushort caractere, uint indicateurs) =>
        new() { Type = InputKeyboard, Touche = touche, Caractere = caractere, Indicateurs = indicateurs };

    private static bool Envoyer(List<Frappe> entrees)
    {
        var tableau = entrees.ToArray();
        return SendInput((uint)tableau.Length, tableau, Marshal.SizeOf<Frappe>()) == tableau.Length;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Frappe
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Touche;
        [FieldOffset(10)] public ushort Caractere;
        [FieldOffset(12)] public uint Indicateurs;
    }

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nombre, Frappe[] entrees, int taille);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
```

`$ESSAIS/capture.ps1`

```powershell
<#
    Capture un rectangle de l'écran (en pixels réels) dans un PNG.
    Usage : powershell -NoProfile -ExecutionPolicy Bypass -File capture.ps1 -Png chemin.png -X 60 -Y 30 -Largeur 920 -Hauteur 560
#>
param([string]$Png, [int]$X, [int]$Y, [int]$Largeur, [int]$Hauteur)
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace W -Name D -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(System.IntPtr c);'
# Coordonnées réelles : sans cela, un écran mis à l'échelle donnerait une zone décalée.
[void][W.D]::SetProcessDpiAwarenessContext([IntPtr]-4)
$image = New-Object System.Drawing.Bitmap($Largeur, $Hauteur)
$g = [System.Drawing.Graphics]::FromImage($image)
$g.CopyFromScreen($X, $Y, 0, 0, $image.Size)
$g.Dispose()
$image.Save($Png, [System.Drawing.Imaging.ImageFormat]::Png)
$image.Dispose()
```

`$ESSAIS/lancer.sh`

```bash
#!/bin/bash
# Joue des scénarios du banc d'essai sur le dépôt donné, puis remet le dépôt dans son état committé.
# Usage : bash lancer.sh <racine du dépôt> <scénarios>   (par exemple : bash lancer.sh "$PWD" 25)
set -u
depot="${1:?Usage : bash lancer.sh <racine du dépôt> <scénarios>}"
scenarios="${2:?Usage : bash lancer.sh <racine du dépôt> <scénarios>}"
ici="$(cd "$(dirname "$0")" && pwd)"
donnees="$LOCALAPPDATA/Temp/pp-banc-donnees"
exe="$depot/src/PressePapiers/bin/Debug/net10.0-windows10.0.19041.0/win-x64/PressePapiers.exe"

nettoyer() {
  git -C "$depot" checkout -- src/PressePapiers/App.xaml.cs
  rm -rf "$depot/src/PressePapiers/Banc"
}
trap nettoyer EXIT

# Le banc remplace App.xaml.cs et ajoute le dossier Banc ; tout est retiré en sortie.
mkdir -p "$depot/src/PressePapiers/Banc"
cp "$ici/App.xaml.cs" "$depot/src/PressePapiers/App.xaml.cs"
for fichier in "$ici"/*.cs; do
  case "$(basename "$fichier")" in
    App.xaml.cs|Scenario*) ;;
    *) cp "$fichier" "$depot/src/PressePapiers/Banc/" ;;
  esac
done
# Seuls les scénarios demandés sont copiés : ceux des tâches suivantes ne compilent pas encore.
for numero in ${scenarios//,/ }; do
  cp "$ici/Scenario$numero.cs" "$depot/src/PressePapiers/Banc/"
done
dotnet build "$depot/PressePapiers.slnx" -c Debug 2>&1 | grep -E "error|Erreur\(s\)" | sort -u
[ "${PIPESTATUS[0]}" -eq 0 ] || { echo "Compilation en échec : banc non lancé."; exit 1; }

# Données jetables neuves à chaque passage : jamais les données réelles.
rm -rf "$donnees"
mkdir -p "$donnees"
PRESSEPAPIERS_DONNEES="$(cygpath -w "$donnees")" \
PRESSEPAPIERS_BANC="$scenarios" \
PRESSEPAPIERS_CAPTURE_PS1="$(cygpath -w "$ici/capture.ps1")" \
PRESSEPAPIERS_CAPTURES="$(cygpath -w "$ici/captures")" \
  timeout 240 "$exe"
echo "code de sortie : $?"
cat "$donnees/banc-resultats.txt"
```

`$ESSAIS/Scenario25.cs`

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Modele;
using Windows.Graphics;

namespace PressePapiers.Banc;

/// <summary>Tâche 25 : fenêtre principale, onglets et compteurs, liste, recherche différée, légende, rafraîchissement, taille, fond.</summary>
internal static class Scenario25
{
    public static async Task ExecuterAsync(ContexteBanc c)
    {
        var vue = c.Vue;
        var fenetre = c.Fenetre;

        // --- Création : masquée, onglets et compteurs lus dans le dépôt.
        c.Verifier(!fenetre.EstVisible, "la fenêtre est créée masquée");
        c.Verifier(fenetre.Handle != 0, "Handle renseigné");
        c.Verifier(vue.Onglets.Select(o => o.Nom).SequenceEqual(["Temporaire", "Code", "Adresses", "Modèles de mails"]),
                   "onglets : Temporaire puis ceux de l'utilisateur, dans l'ordre");
        c.Verifier(vue.Onglets.Select(o => o.Compteur).SequenceEqual([8, 2, 2, 1]), "compteurs 8, 2, 2, 1");
        c.Verifier(vue.OngletSelectionne?.EstTemporaire == true, "Temporaire est l'onglet affiché au départ");
        c.Verifier(vue.Elements.Count == 8, "la liste de Temporaire montre ses 8 éléments");
        c.Verifier(vue.ElementSelectionne?.Id == vue.Elements[0].Id, "le premier élément est choisi");
        c.Verifier(vue.Elements[0].Element.Texte!.StartsWith("git log"), "ordre : le plus récemment utilisé en tête (git log)");
        c.Verifier(vue.Elements[0].Police.Source == "Cascadia Mono", "WindowsTerminal est listé en police à chasse fixe");
        c.Verifier(vue.Elements.Single(e => e.Element.Application == "msedge").Police.Source == "Segoe UI Variable",
                   "msedge reste en police normale");
        c.Verifier(vue.LegendeVisible, "la légende de Temporaire est visible");
        c.Verifier(!vue.Affichee, "le modèle de vue sait que la fenêtre est masquée");

        // --- Fond sans Mica : un fond uni remplace le matériau.
        var racine = c.Controle<Grid>("Racine");
        c.Verifier(fenetre.SystemBackdrop is not null, "Mica pris en charge sur ce poste : fond Mica en place");
        fenetre.AppliquerFond(mica: false);
        c.Verifier(fenetre.SystemBackdrop is null && racine.Background is not null, "sans Mica : plus de matériau, fond uni");
        fenetre.AppliquerFond(mica: true);
        c.Verifier(fenetre.SystemBackdrop is not null && racine.Background is null, "retour à Mica : le fond uni disparaît");

        // --- Affichage : taille mémorisée en pixels logiques.
        await c.AfficherAsync();
        c.Verifier(fenetre.EstVisible && vue.Affichee, "après Activate, EstVisible et Affichee sont vrais");
        var echelle = c.Echelle;
        var taille = fenetre.AppWindow.Size;
        c.Verifier(taille.Width == (int)Math.Round(920 * echelle) && taille.Height == (int)Math.Round(560 * echelle),
                   $"taille de départ 920 x 560 pixels logiques (échelle {echelle:0.##}, mesuré {taille.Width} x {taille.Height})");

        // --- Volet : entrées, badges, sélection.
        var navigation = c.Controle<NavigationView>("Navigation");
        var entrees = navigation.MenuItems.OfType<NavigationViewItem>().ToList();
        c.Verifier(entrees.Count == 5 && navigation.MenuItems.OfType<NavigationViewItemSeparator>().Count() == 1,
                   "volet : 4 onglets, un séparateur, Nouvel onglet");
        c.Verifier(entrees[^1].Content is "Nouvel onglet" && !entrees[^1].SelectsOnInvoked, "Nouvel onglet ne se sélectionne pas");
        c.Verifier(entrees.Take(4).Select(e => ((InfoBadge)e.InfoBadge).Value).SequenceEqual([8, 2, 2, 1]), "badges : 8, 2, 2, 1");
        c.Ecrire($"INFO   élément sélectionné du volet : {(navigation.SelectedItem as NavigationViewItem)?.Content ?? navigation.SelectedItem ?? "(aucun)"}");
        c.Verifier(ReferenceEquals(navigation.SelectedItem, entrees[0]), "Temporaire est sélectionné dans le volet");
        c.Verifier(navigation.IsSettingsVisible, "l'entrée Paramètres est en bas du volet");
        c.Verifier(Math.Abs(navigation.OpenPaneLength - 200) < 0.1, "volet de 200 px");

        // --- Liste réellement affichée.
        var liste = c.Controle<ListView>("Liste");
        var textes = ContexteBanc.Descendants<TextBlock>(liste).Select(t => t.Text).ToList();
        c.Verifier(liste.SelectedIndex == 0, "premier élément sélectionné dans le ListView");
        c.Verifier(textes.Any(t => t.StartsWith("git log")), "l'aperçu du premier élément est affiché");
        c.Verifier(textes.Any(t => t.StartsWith("WindowsTerminal · ")), "ligne « application · heure » affichée");
        c.Verifier(textes.Any(t => t.Contains('\n')), "un aperçu sur deux lignes (code) garde son retour à la ligne");
        var largeurs = (Liste: liste.ActualWidth, Recherche: c.Controle<AutoSuggestBox>("ChampRecherche").ActualWidth,
                        Apercu: c.Controle<Grid>("ZoneHistorique").ActualWidth - 301);
        c.Ecrire($"INFO   largeurs : liste {largeurs.Liste:0}, recherche {largeurs.Recherche:0}, zone d'aperçu {largeurs.Apercu:0}");
        c.Verifier(largeurs.Apercu >= 360, "les trois colonnes tiennent à 920 px : la zone d'aperçu garde au moins 360 px");
        await c.CapturerAsync("25-temporaire.png");

        // --- Recherche : délai de 150 ms, un seul relevé par rafale de frappes, tous onglets.
        var champ = c.Controle<AutoSuggestBox>("ChampRecherche");
        var releves = 0;
        void Compter() => releves++;
        vue.Rafraichi += Compter;
        champ.Text = "f";
        await c.Attendre(40);
        champ.Text = "fa";
        await c.Attendre(40);
        champ.Text = "factu";
        c.Verifier(vue.Recherche == "factu", "la saisie atteint le modèle de vue sans attendre (liaison à double sens)");
        await c.Attendre(90);
        c.Verifier(releves == 0 && vue.Elements.Count == 8, "90 ms après la dernière frappe : la liste n'a pas encore changé");
        await c.Attendre(300);
        c.Verifier(releves == 1, $"une rafale de trois frappes donne une seule recherche (mesuré {releves})");
        var noms = vue.Elements.Select(e => e.NomOnglet).OrderBy(n => n).ToList();
        c.Verifier(vue.Elements.Count == 3 && noms.SequenceEqual(["Code", "Modèles de mails", "Temporaire"]),
                   $"« factu » trouve 3 éléments dans 3 onglets : {string.Join(", ", noms)}");
        c.Verifier(vue.Elements.All(e => e.OngletVisible) && !vue.LegendeVisible, "résultats : nom d'onglet affiché, légende masquée");
        c.Verifier(vue.ElementSelectionne?.Id == vue.Elements[0].Id, "résultat : le premier est choisi");
        var legende = ContexteBanc.Descendants<TextBlock>((DependencyObject)fenetre.Content)
            .First(t => t.Text.StartsWith("Vidé à la prochaine"));
        c.Verifier(legende.Visibility == Visibility.Collapsed, "la légende est réellement masquée pendant la recherche");
        await c.CapturerAsync("25-recherche.png");

        champ.Text = "zzzintrouvable";
        await c.Attendre(400);
        c.Verifier(vue.ListeVide && vue.MessageVide.Contains("zzzintrouvable"), "aucun résultat : message qui reprend la saisie");
        champ.Text = "  ";
        await c.Attendre(60);
        c.Verifier(vue.Elements.Count == 8 && vue.LegendeVisible, "saisie d'espaces seuls : liste de l'onglet, sans délai");
        vue.Rafraichi -= Compter;

        // --- Changement d'onglet : la recherche est effacée, la légende disparaît hors de Temporaire.
        champ.Text = "quai";
        await c.Attendre(400);
        await c.InvoquerEntreeAsync(entrees[1]);
        c.Verifier(vue.OngletSelectionne?.Nom == "Code" && vue.Elements.Count == 2, "onglet Code : 2 éléments");
        c.Verifier(vue.Recherche.Length == 0 && champ.Text.Length == 0, "changer d'onglet efface la recherche");
        c.Verifier(!vue.LegendeVisible, "pas de légende hors de Temporaire");
        c.Verifier(vue.Elements.All(e => e.ANom && e.LignesApercu == 1), "éléments nommés : nom en gras, aperçu sur une ligne");
        c.Verifier(!vue.Elements.Any(e => e.OngletVisible), "hors recherche, pas de nom d'onglet sur les lignes");
        await c.InvoquerEntreeAsync(entrees[0]);

        // --- Écriture depuis un autre fil : un seul rafraîchissement par rafale, sur le fil d'interface.
        var avant = vue.Elements.Count;
        await Task.Run(() => c.Depot.AjouterCopie(new ContenuCopie
        {
            Type = TypeElement.Texte, Texte = "copie venue du fil de capture", Application = "notepad++",
        }));
        await c.Attendre(250);
        c.Verifier(vue.Elements.Count == avant + 1 && vue.Elements[0].Element.Texte == "copie venue du fil de capture",
                   "une copie écrite sur un autre fil apparaît en tête de la liste");
        c.Verifier(((InfoBadge)entrees[0].InfoBadge).Value == avant + 1, "le badge de Temporaire suit");
        c.Verifier(vue.Elements[0].Police.Source == "Cascadia Mono", "notepad++ : police à chasse fixe (liste réglable)");

        releves = 0;
        var selectionsNulles = 0;
        void SurveillerSelection(object? expediteur, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(vue.ElementSelectionne) && vue.ElementSelectionne is null)
            {
                selectionsNulles++;
            }
        }

        vue.PropertyChanged += SurveillerSelection;
        vue.Rafraichi += Compter;
        for (var i = 0; i < 20; i++)
        {
            c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte, Texte = "rafale " + i, Application = "msedge" });
        }

        await c.Attendre(300);
        vue.Rafraichi -= Compter;
        vue.PropertyChanged -= SurveillerSelection;
        c.Verifier(releves == 1 && vue.Elements.Count == avant + 21, $"20 écritures de suite donnent un seul rafraîchissement (mesuré {releves})");
        c.Verifier(selectionsNulles == 0, "la relecture ne laisse jamais passer une sélection nulle (l'aperçu garderait sa saisie)");

        // --- Fenêtre masquée : aucune relecture, rattrapage à l'affichage.
        fenetre.AppWindow.Hide();
        await c.Attendre(200);
        c.Verifier(!vue.Affichee && !fenetre.EstVisible, "masquée : Affichee est faux");
        releves = 0;
        vue.Rafraichi += Compter;
        c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte, Texte = "copie pendant le masquage", Application = "msedge" });
        await c.Attendre(300);
        c.Verifier(releves == 0, "masquée, la fenêtre ne relit rien");
        fenetre.AppWindow.Show();
        await c.Attendre(300);
        vue.Rafraichi -= Compter;
        c.Verifier(releves == 1 && vue.Elements[0].Element.Texte == "copie pendant le masquage", "à l'affichage, la liste rattrape l'écriture manquée");

        // --- Taille mémorisée après un redimensionnement.
        fenetre.AppWindow.Resize(new SizeInt32((int)Math.Round(1000 * echelle), (int)Math.Round(610 * echelle)));
        await c.Attendre(1200);
        var json = File.ReadAllText(c.Donnees.Parametres);
        c.Verifier(c.Vue.Reglages.LargeurFenetre == 1000 && c.Vue.Reglages.HauteurFenetre == 610 && json.Contains("1000") && json.Contains("610"),
                   $"taille mémorisée dans parametres.json : {c.Vue.Reglages.LargeurFenetre} x {c.Vue.Reglages.HauteurFenetre}");
        c.Verifier(c.Parametres.Charger().LargeurFenetre == 1000, "relue par ServiceParametres");
        fenetre.AppWindow.Resize(new SizeInt32((int)Math.Round(920 * echelle), (int)Math.Round(560 * echelle)));
        await c.Attendre(1200);

        c.Verifier(c.Journal.Erreurs.Count == 0, "aucune erreur au journal");
    }
}
```

- [ ] **Étape 2 : Lancer le banc et constater l'échec**

```bash
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 25
```

Résultat attendu : la compilation échoue (`Compilation en échec : banc non lancé.`), faute des types de la tâche : `CS0246`
(`OngletVue` introuvable) et `CS1061` (`'FenetrePrincipaleVue' ne contient pas de définition pour 'Elements'`, `'Recherche'`,
`'Rafraichi'`, ...). À la rédaction : 52 erreurs.

- [ ] **Étape 3 : Implémenter**

Supprimez la coquille : `git rm src/PressePapiers/Interface/FenetrePrincipale.cs`. Puis écrivez les fichiers suivants.

Décisions et pièges à connaître :
- `ListView.ItemsSource` est lié à une **nouvelle liste** à chaque relecture (jamais `Clear` puis `Add` sur 5 000 éléments). Le
  ListView se vide alors un instant et renvoie `null` par la liaison à double sens : `ElementSelectionne` ignore ce `null` pendant la
  relecture (sinon l'aperçu de la tâche 26 se remettrait à zéro et effacerait une saisie en cours). Le banc le vérifie.
- La recherche attend 150 ms par un `DispatcherQueueTimer` (une frappe repousse l'échéance) ; une saisie vidée relit tout de suite.
- Les écritures du dépôt arrivent de n'importe quel fil (la capture) : `Modifie` est ramené sur le fil d'interface et regroupé
  (vingt écritures de suite donnent une seule relecture). Fenêtre masquée, rien n'est relu : la liste se rattrape à l'affichage
  (`Affichee`, mis à jour par `AppWindow.Changed`).
- Le `NavigationView` ignore une sélection posée avant son premier chargement : le volet se resynchronise sur `Loaded`.
- Un `x:Name` de la page qui porte le même nom qu'une propriété liée dans un `DataTemplate` (par exemple `Apercu`) fait échouer le
  compilateur XAML avec `WMC9999` (message d'erreur illisible, même avec `VSLANG=1033`). Les contrôles de la page portent donc des
  noms distincts des propriétés de `ElementVue`.
- Un `x:Name` inutile n'est jamais posé sur un type qui existe déjà dans l'espace de noms (ici `VueApercu`, `PageParametres` : tâches 26 et 28).
- La taille est mémorisée en **pixels logiques** (920 x 560 donne la même fenêtre à 100 % et à 150 %) ; elle n'est mémorisée que
  fenêtre affichée, 500 ms après le dernier redimensionnement.
- Une image se liste avec sa miniature, décodée à la première lecture seulement (lignes réellement affichées).

`src/PressePapiers/Interface/ResumeElement.cs`

```csharp
using System.Globalization;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>
/// Textes courts qui décrivent un élément dans la liste : aperçu sur deux lignes, heure, taille, icône de type.
/// Logique pure, sans contrôle WinUI, pour rester lisible et vérifiable à part.
/// </summary>
internal static class ResumeElement
{
    /// <summary>Glyphe Segoe Fluent Icons du type d'élément.</summary>
    public static string Glyphe(TypeElement type) => type switch
    {
        TypeElement.MisEnForme => "\uE8D2",
        TypeElement.Image => "\uEB9F",
        TypeElement.Fichiers => "\uE8B7",
        _ => "\uE8A5",
    };

    /// <summary>
    /// Deux premières lignes non vides du texte, débarrassées de leur indentation ; une ellipse signale la suite.
    /// Un texte de plusieurs mégaoctets n'est lu que sur ses premiers caractères.
    /// </summary>
    public static string ApercuTexte(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return string.Empty;
        }

        const int caracteresLus = 2000;
        var debut = texte.Length > caracteresLus ? texte[..caracteresLus] : texte;
        var lignes = debut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var resume = lignes.Length == 1 ? lignes[0] : lignes[0] + "\n" + lignes[1];
        var suite = lignes.Length > 2 || texte.Length > caracteresLus;
        return suite ? resume + " …" : resume;
    }

    /// <summary>Texte d'aperçu d'un élément selon son type.</summary>
    public static string Apercu(Element element)
    {
        switch (element.Type)
        {
            case TypeElement.Image:
                return "Image, " + Taille(element.Taille);
            case TypeElement.Fichiers:
                var chemins = element.Fichiers;
                var noms = chemins.Take(3).Select(c => Path.GetFileName(c.TrimEnd('\\', '/')) is { Length: > 0 } nom ? nom : c);
                var reste = chemins.Count > 3 ? ", …" : string.Empty;
                var pluriel = chemins.Count > 1 ? "fichiers" : "fichier";
                return $"{chemins.Count} {pluriel} : {string.Join(", ", noms)}{reste}";
            default:
                var apercu = ApercuTexte(element.Texte);
                return apercu.Length > 0 ? apercu : "(contenu vide)";
        }
    }

    /// <summary>Taille lisible en français : « 812 o », « 12 Ko », « 3,4 Mo ».</summary>
    public static string Taille(long octets)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        return octets switch
        {
            < 1024 => $"{octets} o",
            < 1024 * 1024 => (octets / 1024.0).ToString("0.#", fr) + " Ko",
            _ => (octets / 1024.0 / 1024.0).ToString("0.#", fr) + " Mo",
        };
    }

    /// <summary>Heure du jour, « hier 18:03 », « 08/10 » ou « 08/10/2025 » selon l'ancienneté, dans le fuseau de l'horloge.</summary>
    public static string Heure(DateTimeOffset date, TimeProvider horloge)
    {
        var local = TimeZoneInfo.ConvertTime(date, horloge.LocalTimeZone);
        var maintenant = TimeZoneInfo.ConvertTime(horloge.GetUtcNow(), horloge.LocalTimeZone);
        var invariant = CultureInfo.InvariantCulture;

        if (local.Date == maintenant.Date)
        {
            return local.ToString("HH:mm", invariant);
        }

        if (local.Date == maintenant.Date.AddDays(-1))
        {
            return "hier " + local.ToString("HH:mm", invariant);
        }

        return local.Year == maintenant.Year ? local.ToString("dd/MM", invariant) : local.ToString("dd/MM/yyyy", invariant);
    }

    /// <summary>Vrai si le nom d'application figure dans la liste des applications à chasse fixe (casse et « .exe » ignorés).</summary>
    public static bool EstChasseFixe(string? application, IReadOnlySet<string> applicationsChasseFixe) =>
        !string.IsNullOrWhiteSpace(application) && applicationsChasseFixe.Contains(NomSansExtension(application));

    /// <summary>Ensemble comparable (insensible à la casse, sans « .exe ») des noms de la liste réglable.</summary>
    public static HashSet<string> EnsembleChasseFixe(IEnumerable<string> noms) =>
        new(noms.Where(n => !string.IsNullOrWhiteSpace(n)).Select(NomSansExtension), StringComparer.OrdinalIgnoreCase);

    private static string NomSansExtension(string nom)
    {
        var propre = nom.Trim();
        return propre.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? propre[..^4] : propre;
    }
}
```

`src/PressePapiers/Interface/OngletVue.cs`

```csharp
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>Un onglet tel que le volet de navigation l'affiche : son identité, son nom et le nombre d'éléments qu'il contient.</summary>
public sealed record OngletVue(long Id, string Nom, bool EstTemporaire, int Compteur)
{
    /// <summary>Construit la ligne du volet à partir de l'onglet du dépôt et de son compteur.</summary>
    public static OngletVue De(Onglet onglet, int compteur) => new(onglet.Id, onglet.Nom, onglet.EstTemporaire, compteur);

    /// <summary>Vrai si le volet doit être reconstruit : un onglet est apparu, a disparu, a changé de place ou de nom.</summary>
    public static bool StructureDifferente(IReadOnlyList<OngletVue> a, IReadOnlyList<OngletVue> b) =>
        a.Count != b.Count || a.Zip(b).Any(p => p.First.Id != p.Second.Id || p.First.Nom != p.Second.Nom);
}
```

`src/PressePapiers/Interface/ElementVue.cs`

```csharp
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
```

`src/PressePapiers/Interface/FenetrePrincipaleVue.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using PressePapiers.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Interface;

/// <summary>
/// Modèle de vue de la fenêtre principale : onglets avec compteurs, liste des éléments de l'onglet ou résultats de
/// recherche, légende de Temporaire. Vit sur le fil d'interface ; les écritures du dépôt faites depuis un autre fil
/// (la capture) y reviennent par le <see cref="DispatcherQueue"/> et sont regroupées en un seul rafraîchissement.
/// </summary>
public sealed partial class FenetrePrincipaleVue : ObservableObject
{
    /// <summary>Délai de frappe avant de lancer la recherche (spec, section 8).</summary>
    internal static readonly TimeSpan DelaiRecherche = TimeSpan.FromMilliseconds(150);

    private readonly DepotHistorique _depot;
    private readonly MoteurRecherche _recherche;
    private readonly ServiceRecollage _recollage;
    private readonly ServiceParametres _parametres;
    private readonly IJournal _journal;
    private readonly DispatcherQueue _file;
    private readonly DispatcherQueueTimer _minuteurRecherche;

    private HashSet<string> _chasseFixe = [];
    private bool _rafraichissementPlanifie;
    private bool _rafraichissementEnAttente;
    private bool _affichee;
    private bool _rechercheInterne;
    private bool _relecture;
    private ElementVue? _elementSelectionne;

    /// <summary>Crée le modèle de vue sur le fil d'interface et lit une première fois les onglets et la liste.</summary>
    public FenetrePrincipaleVue(DepotHistorique depot, MoteurRecherche recherche, ServiceRecollage recollage,
                                ServiceParametres parametres, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(recherche);
        ArgumentNullException.ThrowIfNull(recollage);
        ArgumentNullException.ThrowIfNull(parametres);
        ArgumentNullException.ThrowIfNull(journal);

        _depot = depot;
        _recherche = recherche;
        _recollage = recollage;
        _parametres = parametres;
        _journal = journal;
        _file = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("Le modèle de vue doit être créé sur le fil d'interface.");

        _minuteurRecherche = _file.CreateTimer();
        _minuteurRecherche.Interval = DelaiRecherche;
        _minuteurRecherche.IsRepeating = false;
        _minuteurRecherche.Tick += (_, _) => Rafraichir(premierElement: true);

        Reglages = parametres.Charger();
        _chasseFixe = ResumeElement.EnsembleChasseFixe(Reglages.ApplicationsChasseFixe);

        _depot.Modifie += OnDepotModifie;
        Rafraichir(premierElement: true);
    }

    /// <summary>Horloge de l'affichage des heures ; remplaçable pour l'essai.</summary>
    public TimeProvider Horloge { get; set; } = TimeProvider.System;

    /// <summary>Réglages en vigueur (taille de la fenêtre, applications à chasse fixe, mode de collage).</summary>
    public Parametres Reglages { get; private set; }

    /// <summary>Texte d'état du hook Win+V affiché dans les Paramètres, fourni par l'application.</summary>
    [ObservableProperty]
    public partial string EtatCrochet { get; set; } = "Inconnu";

    /// <summary>Onglets du volet de navigation, Temporaire en premier.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<OngletVue> Onglets { get; private set; } = [];

    /// <summary>Onglet dont la liste est affichée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LegendeVisible))]
    public partial OngletVue? OngletSelectionne { get; private set; }

    /// <summary>Lignes de la liste : éléments de l'onglet, ou résultats de la recherche dans tous les onglets.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListeVide))]
    [NotifyPropertyChangedFor(nameof(MessageVide))]
    public partial IReadOnlyList<ElementVue> Elements { get; private set; } = [];

    /// <summary>
    /// Ligne choisie dans la liste. Quand la liste est remplacée, le ListView se vide un instant et renvoie null par la
    /// liaison : pendant une relecture ce null est ignoré, pour que l'aperçu et ses champs ne soient pas remis à zéro.
    /// </summary>
    public ElementVue? ElementSelectionne
    {
        get => _elementSelectionne;
        set
        {
            if (_relecture && value is null)
            {
                return;
            }

            SetProperty(ref _elementSelectionne, value);
        }
    }

    /// <summary>Texte saisi dans la zone de recherche ; la liste suit 150 ms après la dernière frappe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnRecherche))]
    [NotifyPropertyChangedFor(nameof(LegendeVisible))]
    [NotifyPropertyChangedFor(nameof(MessageVide))]
    public partial string Recherche { get; set; } = string.Empty;

    /// <summary>Vrai si la saisie contient autre chose que des espaces : la liste montre alors des résultats.</summary>
    public bool EnRecherche => !string.IsNullOrWhiteSpace(Recherche);

    /// <summary>Vrai pour la légende de Temporaire, qui disparaît pendant une recherche.</summary>
    public bool LegendeVisible => !EnRecherche && OngletSelectionne?.EstTemporaire == true;

    /// <summary>Texte de la légende de l'onglet Temporaire.</summary>
    public string LegendeTemporaire =>
        "Vidé à la prochaine ouverture de session Windows. F2 pour garder un élément sous un nom.";

    /// <summary>Vrai si la liste n'a aucune ligne.</summary>
    public bool ListeVide => Elements.Count == 0;

    /// <summary>Message affiché à la place d'une liste vide.</summary>
    public string MessageVide => EnRecherche
        ? $"Aucun élément ne contient « {Recherche.Trim()} »."
        : "Aucun élément dans cet onglet.";

    /// <summary>
    /// Vrai tant que la fenêtre est affichée. Masquée, elle ne relit rien : une écriture du dépôt est notée et la liste
    /// n'est reconstruite qu'au passage à vrai (une copie par seconde ne doit pas coûter une lecture de la base).
    /// </summary>
    public bool Affichee
    {
        get => _affichee;
        set
        {
            _affichee = value;
            if (value && _rafraichissementEnAttente)
            {
                Rafraichir();
            }
        }
    }

    /// <summary>Événement levé après chaque rafraîchissement, pour que la fenêtre aligne le volet de navigation.</summary>
    public event Action? Rafraichi;
}
```

`src/PressePapiers/Interface/FenetrePrincipaleVue.Liste.cs`

```csharp
namespace PressePapiers.Interface;

// Relecture du dépôt : onglets, éléments, recherche différée, regroupement des écritures venues d'un autre fil.
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Choisit l'onglet à afficher : la recherche est effacée et le premier élément est choisi.</summary>
    public void ChoisirOnglet(OngletVue onglet)
    {
        ArgumentNullException.ThrowIfNull(onglet);
        ChangerRecherche(string.Empty);
        OngletSelectionne = onglet;
        Rafraichir(premierElement: true);
    }

    /// <summary>Efface la recherche sans attendre le délai de frappe.</summary>
    public void EffacerRecherche()
    {
        ChangerRecherche(string.Empty);
        Rafraichir(premierElement: true);
    }

    /// <summary>Relit les onglets, leurs compteurs et la liste. À appeler sur le fil d'interface.</summary>
    /// <param name="premierElement">Vrai pour choisir le premier élément ; faux pour garder l'élément courant s'il existe encore.</param>
    public void Rafraichir(bool premierElement = false)
    {
        _rafraichissementEnAttente = false;
        try
        {
            var onglets = _depot.ListerOnglets().Select(o => OngletVue.De(o, _depot.CompterElements(o.Id))).ToList();
            var courant = onglets.FirstOrDefault(o => o.Id == OngletSelectionne?.Id) ?? onglets[0];
            Onglets = onglets;
            OngletSelectionne = courant;
            ReconstruireElements(onglets, courant, premierElement);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de relire l'historique pour l'afficher.", exception);
        }

        Rafraichi?.Invoke();
    }

    /// <summary>Mémorise la taille de la fenêtre (pixels logiques) dans les réglages, si elle a changé.</summary>
    public void MemoriserTaille(int largeur, int hauteur)
    {
        if (largeur <= 0 || hauteur <= 0 || (largeur == Reglages.LargeurFenetre && hauteur == Reglages.HauteurFenetre))
        {
            return;
        }

        var nouveaux = Reglages with { LargeurFenetre = largeur, HauteurFenetre = hauteur };
        try
        {
            _parametres.Enregistrer(nouveaux);
            Reglages = nouveaux;
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Impossible de mémoriser la taille de la fenêtre.", exception);
        }
    }

    // Change la saisie sans déclencher la relecture différée : l'appelant relit lui-même.
    private void ChangerRecherche(string valeur)
    {
        _minuteurRecherche.Stop();
        _rechercheInterne = true;
        try
        {
            Recherche = valeur;
        }
        finally
        {
            _rechercheInterne = false;
        }
    }

    partial void OnRechercheChanged(string value)
    {
        if (_rechercheInterne)
        {
            return;
        }

        // Chaque frappe repousse l'échéance ; une saisie vidée met la liste à jour tout de suite.
        _minuteurRecherche.Stop();
        if (string.IsNullOrWhiteSpace(value))
        {
            Rafraichir(premierElement: true);
        }
        else
        {
            _minuteurRecherche.Start();
        }
    }

    private void ReconstruireElements(List<OngletVue> onglets, OngletVue courant, bool premierElement)
    {
        var idAvant = ElementSelectionne?.Id;
        var indexAvant = -1;
        for (var i = 0; i < Elements.Count; i++)
        {
            if (Elements[i].Id == idAvant)
            {
                indexAvant = i;
                break;
            }
        }

        IReadOnlyList<PressePapiers.Coeur.Modele.Element> source;
        Dictionary<long, string>? noms = null;
        if (EnRecherche)
        {
            source = _recherche.Chercher(Recherche);
            noms = onglets.ToDictionary(o => o.Id, o => o.Nom);
        }
        else
        {
            source = _depot.ListerElements(courant.Id);
        }

        var lignes = source
            .Select(e => new ElementVue(e, noms?.GetValueOrDefault(e.OngletId),
                                        ResumeElement.EstChasseFixe(e.Application, _chasseFixe), Horloge))
            .ToList();

        _relecture = true;
        try
        {
            Elements = lignes;
        }
        finally
        {
            _relecture = false;
        }

        if (lignes.Count == 0)
        {
            SetProperty(ref _elementSelectionne, null, nameof(ElementSelectionne));
        }
        else if (premierElement || idAvant is null)
        {
            ElementSelectionne = lignes[0];
        }
        else
        {
            // L'élément courant reste choisi ; s'il a disparu, le suivant prend sa place (le dernier si on était en bas).
            ElementSelectionne = lignes.FirstOrDefault(l => l.Id == idAvant) ?? lignes[Math.Clamp(indexAvant, 0, lignes.Count - 1)];
        }
    }

    private void OnDepotModifie()
    {
        // Peut arriver de n'importe quel fil (la capture écrit hors du fil d'interface).
        if (_file.HasThreadAccess)
        {
            PlanifierRafraichissement();
        }
        else
        {
            _file.TryEnqueue(PlanifierRafraichissement);
        }
    }

    private void PlanifierRafraichissement()
    {
        if (!Affichee)
        {
            _rafraichissementEnAttente = true;
            return;
        }

        if (_rafraichissementPlanifie)
        {
            return;
        }

        // Plusieurs écritures de suite donnent une seule relecture.
        _rafraichissementPlanifie = true;
        _file.TryEnqueue(() =>
        {
            _rafraichissementPlanifie = false;
            Rafraichir();
        });
    }
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

                    <!-- Colonne de droite : aperçu (tâche 26). -->
                    <Grid x:Name="ZoneApercu" Grid.Column="2" />
                </Grid>

                <!-- Page Paramètres (tâche 28), affichée par l'entrée Paramètres du volet. -->
                <Grid x:Name="ZoneParametres" Visibility="Collapsed" />
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
            Navigation.MenuItems.Add(new NavigationViewItem
            {
                Content = onglet.Nom,
                Tag = onglet,
                Icon = new FontIcon { Glyph = onglet.EstTemporaire ? "\uE121" : "\uE8B7", FontFamily = policeIcones },
                InfoBadge = new InfoBadge
                {
                    Value = onglet.Compteur,
                    Style = (Style)Application.Current.Resources["InformationalValueInfoBadgeStyle"],
                },
            });
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

    // « Nouvel onglet » ne change pas la sélection : la boîte de saisie vient avec la tâche 26.
    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
    }
}
```

`src/PressePapiers/Interface/FenetrePrincipale.Affichage.cs`

```csharp
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
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 25
```

Résultat attendu (environ 40 s) : `0 Erreur(s)`, `code de sortie : 0`, 53 lignes `OK`, aucune `ECHEC`, puis
`=== Fin : 53 réussites, 0 échecs, 0 erreurs au journal`. Deux lignes `INFO` : `élément sélectionné du volet : Temporaire` et
`largeurs : liste 288, recherche 276, zone d'aperçu 402`. Les captures sont dans `$ESSAIS/captures/` : ouvrez `25-temporaire.png`
et `25-recherche.png` et regardez-les.

Ce que le banc prouve (chaque point a été mis à l'épreuve par une mutation du code, jouée puis annulée) :
- création : fenêtre masquée, onglets Temporaire, Code, Adresses, Modèles de mails avec les compteurs 8, 2, 2, 1 ; ordre
  `DateUsage` décroissant (la copie la plus récente en tête) ; police à chasse fixe pour WindowsTerminal (et pour notepad++ une fois
  la copie arrivée), police normale pour msedge ;
- recherche : trois frappes à 40 ms d'intervalle donnent **une seule** relecture, 150 ms après la dernière ; 90 ms après, la liste n'a
  pas encore changé (mutation « sans délai » : 2 échecs) ; « factu » trouve 3 éléments dans 3 onglets, avec le nom de l'onglet sur
  chaque ligne et sans la légende ; une saisie sans résultat affiche un message qui la reprend ; des espaces seuls ramènent la liste de
  l'onglet sans délai ;
- changer d'onglet efface la recherche, masque la légende de Temporaire et montre les éléments nommés (nom en gras, aperçu d'une ligne) ;
- une copie écrite depuis un autre fil apparaît en tête, le compteur suit ; vingt écritures de suite donnent **une** relecture
  (mutation « sans regroupement » : 20 relectures) et aucune notification de sélection nulle (mutation « sans garde » : échec) ;
- fenêtre masquée : zéro relecture, puis rattrapage à l'affichage ;
- taille : 920 x 560 pixels logiques à l'ouverture, 1000 x 610 écrits dans `parametres.json` après un redimensionnement, relus par
  `ServiceParametres` ;
- fond : `AppliquerFond(false)` retire Mica et pose un fond uni, `AppliquerFond(true)` le rétablit (le chemin sans Mica n'est exercé
  que par cet appel, aucune machine sans Mica n'était disponible).

Mesure à 920 x 560 : volet 200 px, liste 288 px, zone de recherche 276 px, aperçu 402 px ; rien n'est tronqué de façon gênante : les
aperçus longs se terminent par une ellipse sur deux lignes, les noms par une ellipse sur une ligne.

Écarts avec la maquette (proposition B, rendu WinUI 3) à connaître :
- la ligne d'aide du bas (« Entrée coller, Maj+Entrée ... ») de la maquette n'existe pas : la spec section 8 ne la demande pas ; elle
  coûterait 30 px sur 560 ;
- les boutons Nommer et Supprimer au survol de chaque ligne de la maquette HTML n'existent pas (la barre de commandes de l'aperçu les porte) ;
- la maquette dit « Tab : onglet suivant » ; la spec (et ce plan) dit Ctrl+Tab, Tab gardant son rôle normal ;
- les légendes Réduire, Agrandir, Fermer sont celles du système (barre de titre fondue) et non un dessin ;
- le badge « Win+V » de la barre de titre et la couche arrondie du contenu sont rendus par WinUI comme dans l'essai `RenduWinUI`.

Non couvert ici : le fond sans Mica sur un vrai Windows 10 ; la mise à l'échelle 150 % (le banc tourne à 100 %) ; la reprise sur un
écran de résolution différente (tâche 27).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Interface/
git commit -m "Ajouter la fenêtre principale et son modèle de vue : onglets, liste, recherche différée" -m "Volet des onglets avec compteurs, liste des éléments (icône de type, nom, aperçu sur deux lignes, application et heure, chasse fixe selon les réglages), recherche tous onglets lancée 150 ms après la dernière frappe, légende de Temporaire, relecture regroupée sur le fil d'interface quand le dépôt change, aucune relecture fenêtre masquée, taille mémorisée en pixels logiques, fond uni sans Mica."
```

