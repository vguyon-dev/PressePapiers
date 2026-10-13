# PressePapiers : contrat d'implémentation

> **Statut au 13/10/2026, après la construction du plan.** Le contrat ci-dessous est celui que les auteurs ont reçu. Le code
> du plan s'en écarte sur les points suivants, sans changer aucune signature publique listée ici ; le détail par tâche est
> dans `integration.md`, section 5, et le code de chaque tâche fait foi.
>
> - **Verrou** : `DepotHistorique` et `MoteurRecherche` verrouillent le même objet, la connexion SQLite (et non un verrou
>   interne propre au dépôt) ; la recherche garde le verrou pendant toute la vie de sa commande.
> - **Schéma** (tâche 5, `Coeur/Stockage/SchemaBase.cs`) : déclencheur `element_au` limité à `AFTER UPDATE OF
>   texte_recherche`, deux index (onglet et empreinte ; onglet et date d'usage).
> - **Fichiers ajoutés** : `global.json` ; `Coeur/Stockage/SchemaBase.cs`, `OuvertureBase.cs`, `IDialogueDemarrage.cs` ;
>   `Coeur/Capture/ExecutionUnique.cs` ; `Coeur/Clavier/ZoneFocus.cs`, `ActionTouche.cs`, `CommandeClavier.cs`,
>   `TableDesTouches.cs` ; `Natif/NatifDemarrage.cs`, `NatifOuverture.cs` ; `Systeme/DialoguesWindows.cs` ;
>   `Capture/ContenuBrut.cs`, `FormatsPressePapiers.cs`, `LectureBrute.cs` ; `App.Composition.cs` ;
>   `scripts/generer-icones.ps1` et les icônes `Assets/icone-normale.ico`, `Assets/icone-alerte.ico` ; la vue et la fenêtre
>   en fichiers partiels (`FenetrePrincipaleVue.*.cs`, `FenetrePrincipale.xaml`, `.xaml.cs`, `.Volet.cs`, `.Affichage.cs`,
>   `.Clavier.cs`) avec `OngletVue`, `ElementVue`, `ResumeElement`, `IDialogues`, `GestionnaireDialogues`, `RenduRtf`,
>   `VueApercu`, `PageParametres` ; assistants de test partagés.
> - **Membres publics ajoutés** : `FenetrePrincipale.PremierPlanRefuse`, `AutoriserFermeture()` ;
>   `FenetrePrincipaleVue.AppliquerRaccourci`, `AppliquerDemarrage`, `OuvrirChemin`, `Notifier`, `Dialogues`,
>   `ParametresModifies` (`EtatCrochet` vaut « Inconnu » au départ). Membres internes de test : `BaseHistorique.ControlerVersion`,
>   `DossierDonnees.ParDefaut(Func<string,string?>, string)`, surcharges de `DemarrageWindows` avec chemin de clé.
> - **`AllowUnsafeBlocks`** posé dans le projet de l'application à la tâche 16 (exigé par `LibraryImport`).
> - **Réinstallation périodique du hook** : faite par `CrochetWinV` sur son propre fil (toutes les 15 minutes) ; `App`
>   relit seulement l'état du hook chaque minute et relance un hook inactif.
>
> **Statut au 13/10/2026, après la revue de code** (`revue.md`). Ajouts publics : `Coeur.Modele.ElementResume`
> (`LongueurMaxExtrait` = 500), `DepotHistorique.ListerResumes(long)`, `DepotHistorique.NettoyerImagesOrphelines()`,
> `MoteurRecherche.ChercherResumes(string, int = 500)`, paramètre optionnel `IJournal? journal` de `Sauvegarde.Creer`,
> `DialoguesWindows.Signaler(string)`, `LecteurPressePapiers.RelectureDemandee`. Nouveaux fichiers :
> `Coeur/Stockage/FichiersImages.cs`, `ExtraitTexte.cs`, `MigrationSchema.cs` ; `Natif/NatifProcessus.cs` ;
> `Interface/ExecutionProtegee.cs`, `Interface/GestionDemarrage.cs` (internes). Comportements : schéma en version 2 ; `Raccourci.TryAnalyser`
> exige deux modificateurs ; `AjouterCopie` renvoie un élément partiel pour un doublon (`Lire` donne l'élément complet) ;
> `EcrireMeta` ne lève plus `Modifie`.

Annexe du plan `docs/superpowers/plans/2026-10-13-pressepapiers.md`, tirée de la spec
`docs/superpowers/specs/2026-10-13-pressepapiers-design.md`. Chaque tâche du plan respecte ces noms, ces signatures et cette
répartition des fichiers. Un écart nécessaire se signale et se reporte ici ; il ne s'invente pas dans une seule tâche.

## 1. Solution et projets

```
PressePapiers/
  PressePapiers.slnx
  Directory.Build.props        Nullable enable, ImplicitUsings enable, LangVersion latest, TargetFramework commun
  Directory.Packages.props     gestion centralisée des versions NuGet
  src/PressePapiers.Coeur/     bibliothèque, net10.0-windows10.0.19041.0, sans WinUI
  src/PressePapiers/           application WinUI 3, net10.0-windows10.0.19041.0, x64, non empaquetée, autonome
  tests/PressePapiers.Tests/   xUnit, net10.0-windows10.0.19041.0, référence Coeur seulement
  scripts/publier.ps1
  README.md
```

- `PressePapiers.Coeur` : `InternalsVisibleTo("PressePapiers.Tests")`.
- Application : `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `Platforms=x64`, `RuntimeIdentifier=win-x64`,
  symbole `DISABLE_XAML_GENERATED_MAIN` (Main personnalisé), pas de `PublishTrimmed` ni NativeAOT.
- Paquets (versions exactes fixées dans `Directory.Packages.props` par la tâche 1, versions stables publiées depuis au moins
  deux semaines) : Microsoft.WindowsAppSDK 2.5.1, Microsoft.Windows.SDK.BuildTools 10.0.28000.2705, Microsoft.Data.Sqlite,
  CommunityToolkit.Mvvm, Serilog, Serilog.Sinks.File, xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk,
  Microsoft.Extensions.TimeProvider.Testing. Assertions xUnit seules (pas de FluentAssertions, licence commerciale).

## 2. Règles communes

- Noms en français ; commentaires en français centrés sur l'intention ; documentation XML sur les types et membres publics.
- Fichiers courts, une responsabilité par fichier ; un type public par fichier.
- Dates : `TimeProvider` injecté (jamais `DateTime.Now` dans Coeur) ; stockage en texte ISO 8601 aller-retour (`"O"`).
- Fils : la fenêtre relais, l'icône, la fenêtre B et tout accès au presse-papiers vivent sur le fil d'interface. Le fil du
  hook ne touche jamais l'interface : il lève `WinVPresse`, le consommateur passe par `DispatcherQueue`.
- `DepotHistorique` est sûr entre fils (verrou interne sur toutes ses opérations) ; son événement `Modifie` peut être levé
  sur n'importe quel fil.
- Aucun `INSERT OR REPLACE` sur `element`.
- À la fin de chaque tâche : `dotnet build PressePapiers.slnx -c Debug` sans erreur et `dotnet test` sur toute la solution,
  tous les tests verts (pas seulement ceux de la tâche).
- Chaque commit se construit seul : une tâche n'appelle que des types déjà présents (les coquilles de la tâche 1 servent
  à cela).
- Pas de tiret cadratin ni demi-cadratin d'incise, dans le code comme dans les textes.

## 3. Coeur : types publics

### 3.1 `PressePapiers.Coeur.Modele`

```csharp
public enum TypeElement { Texte = 1, MisEnForme = 2, Image = 3, Fichiers = 4 }

public enum ModeCollage { TexteSeul = 0, AvecMiseEnForme = 1 }

public enum OrigineOuverture { WinV, RaccourciSecours, Icone, Relance }

public sealed record Onglet(long Id, string Nom, int Ordre, bool EstTemporaire);

public sealed record Element
{
    public required long Id { get; init; }
    public required long OngletId { get; init; }
    public string? Nom { get; init; }
    public required TypeElement Type { get; init; }
    public string? Texte { get; init; }            // texte brut ; pour Fichiers : chemins séparés par '\n'
    public byte[]? Rtf { get; init; }
    public byte[]? Html { get; init; }             // octets bruts « HTML Format »
    public string? ImageFichier { get; init; }     // nom du PNG dans DossierDonnees.Images
    public byte[]? Miniature { get; init; }        // PNG, 160 px de large au plus
    public required byte[] Empreinte { get; init; }
    public string? Application { get; init; }
    public required DateTimeOffset DateCopie { get; init; }
    public required DateTimeOffset DateUsage { get; init; }
    public required long Taille { get; init; }     // octets du contenu principal
    public IReadOnlyList<string> Fichiers { get; }  // découpe de Texte si Type == Fichiers, sinon vide
}

public sealed record ContenuCopie
{
    public required TypeElement Type { get; init; }
    public string? Texte { get; init; }
    public byte[]? Rtf { get; init; }
    public byte[]? Html { get; init; }
    public byte[]? ImagePng { get; init; }
    public byte[]? Miniature { get; init; }
    public IReadOnlyList<string>? Fichiers { get; init; }
    public string? Application { get; init; }
}
```

### 3.2 `PressePapiers.Coeur.Journalisation`

```csharp
public interface IJournal
{
    void Info(string message);
    void Avertissement(string message, Exception? exception = null);
    void Erreur(string message, Exception? exception = null);
}

public sealed class JournalNul : IJournal { public static readonly JournalNul Instance; }
```

### 3.3 `PressePapiers.Coeur.Stockage`

```csharp
public sealed record DossierDonnees(string Racine)
{
    public string Base { get; }          // Racine\historique.db
    public string Images { get; }        // Racine\images
    public string Sauvegardes { get; }   // Racine\sauvegardes
    public string Journal { get; }       // Racine\journal
    public string Parametres { get; }    // Racine\parametres.json
    // %LOCALAPPDATA%\PressePapiers, ou le dossier de la variable d'environnement PRESSEPAPIERS_DONNEES si elle est définie
    // (essais et recette sans toucher aux données réelles).
    public static DossierDonnees ParDefaut();
    public void CreerDossiers();
}

public sealed class BaseIncompatibleException(string message) : Exception(message);

public sealed class BaseHistorique : IDisposable
{
    // Ouvre ou crée la base : schéma de la spec (section 5, tables, index FTS5 trigram, trois déclencheurs),
    // journal_mode=WAL, foreign_keys=ON ; contrôle sqlite_version() >= 3.34.0 et FTS5, sinon BaseIncompatibleException.
    public static BaseHistorique Ouvrir(string cheminFichier);
    public SqliteConnection Connexion { get; }
    public string VersionSqlite { get; }
    public void Dispose();
}

internal static class LecteurElement
{
    public const string Colonnes = "id, onglet_id, nom, type, texte, rtf, html, image_fichier, miniature, empreinte, application, date_copie, date_usage, taille";
    public static Element Lire(SqliteDataReader lecteur);   // colonnes dans l'ordre de Colonnes
}

public static class Empreinte
{
    // SHA-256 : texte en UTF-8 (Texte, MisEnForme), octets du PNG (Image), chemins joints par '\n' (Fichiers).
    public static byte[] Calculer(ContenuCopie contenu);
}

public sealed class DepotHistorique
{
    public DepotHistorique(BaseHistorique baseHistorique, DossierDonnees dossiers, TimeProvider horloge, IJournal journal);
    public Onglet OngletTemporaire { get; }                 // créé à la première ouverture (nom « Temporaire », ordre 0)
    public IReadOnlyList<Onglet> ListerOnglets();           // par Ordre, Temporaire en premier
    public Onglet CreerOnglet(string nom);                  // nom non vide, ajouté en dernier
    public void RenommerOnglet(long ongletId, string nom);  // Temporaire : InvalidOperationException
    public void DeplacerOnglet(long ongletId, int nouvelOrdre);
    public void SupprimerOnglet(long ongletId);             // supprime ses éléments et leurs PNG ; Temporaire : InvalidOperationException
    public int CompterElements(long ongletId);
    public IReadOnlyList<Element> ListerElements(long ongletId);   // DateUsage décroissante, puis Id décroissant (ordre stable à date égale)
    public Element? Lire(long elementId);
    public Element AjouterCopie(ContenuCopie contenu);      // dédoublonnage dans Temporaire ; écrit le PNG ; renvoie l'élément créé ou remonté
    public Element Nommer(long elementId, string nom, long ongletId);   // nom non vide, ongletId différent de Temporaire
    public void MarquerUtilise(long elementId);
    public void Supprimer(long elementId);                  // supprime aussi le PNG
    public int PurgerTemporaire();                          // éléments de Temporaire et leurs PNG ; renvoie le nombre
    public int AppliquerPlafond(int plafond);               // 0 = désactivé ; renvoie le nombre supprimé
    public void ReconstruireIndex();                        // INSERT INTO element_fts(element_fts) VALUES ('rebuild')
    public string? LireMeta(string cle);
    public void EcrireMeta(string cle, string valeur);
    public string CheminImage(Element element);             // chemin complet du PNG ; InvalidOperationException si pas d'image
    public event Action? Modifie;                           // après toute écriture réussie
}

public static class Sauvegarde
{
    // Copie par SqliteConnection.BackupDatabase dans dossierSauvegardes (historique-AAAAMMJJ-HHmmss.db),
    // garde les nombreGarde plus récentes ; renvoie le chemin créé, ou null s'il n'y a pas de base.
    public static string? Creer(string cheminBase, string dossierSauvegardes, TimeProvider horloge, int nombreGarde = 3);
    public static string? PlusRecente(string dossierSauvegardes);
    public static void Restaurer(string cheminSauvegarde, string cheminBase);   // base fermée ; supprime -wal et -shm
}
```

### 3.4 `PressePapiers.Coeur.Recherche`

```csharp
public static class Normalisation
{
    // Minuscules, accents retirés (décomposition Unicode), ligatures ramenées (œ en oe, æ en ae), espaces réduits.
    public static string Normaliser(string? texte);
    public static string TexteRecherche(string? nom, string? texte, IReadOnlyList<string>? fichiers, string? application);
}

public sealed class MoteurRecherche
{
    public MoteurRecherche(BaseHistorique baseHistorique);
    // Tous les mots requis ; mots de 3 caractères et plus par MATCH (chaque mot entre guillemets doubles),
    // mots de 1 ou 2 caractères par LIKE avec ESCAPE ; liste vide si la requête est vide ; DateUsage décroissante.
    public IReadOnlyList<Element> Chercher(string requete, int limite = 500);
}
```

### 3.5 `PressePapiers.Coeur.Session`

```csharp
public interface ISourceSession
{
    long? LireHeureOuverture();   // FILETIME brut de WTSINFOW.LogonTime ; null si lecture impossible
}

public enum EtatSession { PremierLancement, MemeSession, NouvelleSession, Indetermine }

public sealed record ResultatSession(EtatSession Etat, int ElementsPurges);

public static class DemarrageSession
{
    public const string CleRepere = "session.heure_ouverture";
    // Règles de la spec, section 6 : purge avant mise à jour du repère ; valeur nulle ou 0 = Indetermine, aucune purge.
    public static ResultatSession Appliquer(ISourceSession source, DepotHistorique depot, IJournal journal);
}
```

### 3.6 `PressePapiers.Coeur.Reglages`

```csharp
public readonly record struct Raccourci(bool Ctrl, bool Alt, bool Maj, uint ToucheVirtuelle)
{
    // Texte du type « Ctrl+Alt+Maj+V » (ordre libre, casse libre, « Shift » accepté pour Maj) ; au moins Ctrl ou Alt ;
    // jamais la touche Windows ; touche : lettre A-Z, chiffre 0-9, F1-F24, Espace.
    public static bool TryAnalyser(string texte, out Raccourci raccourci);
    public uint Modificateurs { get; }   // MOD_CONTROL | MOD_ALT | MOD_SHIFT selon les drapeaux, plus MOD_NOREPEAT (0x4000)
    public override string ToString();   // forme canonique « Ctrl+Alt+Maj+V »
}

public enum ThemeApplication { Systeme, Clair, Sombre }

public sealed record Parametres
{
    public string RaccourciSecours { get; init; } = "Ctrl+Alt+Maj+V";
    public bool DemarrageAvecWindows { get; init; } = true;
    public ModeCollage MiseEnFormeParDefaut { get; init; } = ModeCollage.TexteSeul;
    public int PlafondTemporaire { get; init; } = 5000;          // 0 = désactivé
    public ThemeApplication Theme { get; init; } = ThemeApplication.Systeme;
    public IReadOnlyList<string> ApplicationsChasseFixe { get; init; } =
        ["WindowsTerminal", "Code", "devenv", "powershell", "pwsh", "cmd", "Ssms", "rider64", "notepad++"];
    public int LargeurFenetre { get; init; } = 920;
    public int HauteurFenetre { get; init; } = 560;
}

public sealed class ServiceParametres
{
    public ServiceParametres(string cheminFichier, IJournal journal);
    public Parametres Charger();                     // défauts si absent ou illisible (avertissement au journal)
    public void Enregistrer(Parametres parametres);  // écriture atomique : fichier temporaire puis File.Move(..., true)
}
```

### 3.7 `PressePapiers.Coeur.Clavier`

```csharp
public enum ActionCrochet { Laisser, Avaler, AvalerEtOuvrir, MasquerPuisLaisser }

// Logique pure du hook Win+V (spec section 11), testable sans Windows.
public sealed class AutomateWinV
{
    public const uint VkWinGauche = 0x5B, VkWinDroite = 0x5C, VkV = 0x56;
    // injectee : frappe portant LLKHF_INJECTED (toujours Laisser) ; winReellementEnfoncee : GetAsyncKeyState au moment de V.
    public ActionCrochet Traiter(uint touche, bool enfoncee, bool injectee, bool winReellementEnfoncee);
    public void Reinitialiser();   // déverrouillage, sortie de veille, réinstallation
}

public sealed class AntiRebond
{
    public AntiRebond(TimeProvider horloge, TimeSpan delai);
    public bool Accepter();        // faux si le dernier signal accepté date de moins de delai
}
```

### 3.8 `PressePapiers.Coeur.Collage`

```csharp
public enum IssueCollage { Colle, CopieDemandee, CopieSansFenetreOrigine, CopieFenetreFermee, CopieFenetreElevee, Echec }

public static class DecisionCollage
{
    // niveauCible null = lecture impossible = traité comme plus élevé (spec section 10.3).
    public static IssueCollage Decider(bool copieSeuleDemandee, bool fenetreOrigineConnue, bool fenetreExiste,
                                       int? niveauCible, int niveauOutil);
}
```

### 3.9 `PressePapiers.Coeur.Formats`

```csharp
public static class FormatHtml
{
    public static byte[] Encoder(string fragmentHtml);              // en-tête Version:1.0, offsets en octets UTF-8, CRLF
    public static string? ExtraireFragment(ReadOnlySpan<byte> donnees);
}

public static class ListeFichiers
{
    public static byte[] EncoderDropFiles(IReadOnlyList<string> chemins);       // structure DROPFILES, chemins en UTF-16
    public static IReadOnlyList<string> DecoderDropFiles(ReadOnlySpan<byte> donnees);
}

public static class ConvertisseurImage
{
    public static Task<byte[]> DibVersPngAsync(byte[] dib);        // CF_DIB ou CF_DIBV5 ; alpha conservé pour un V5 avec masque alpha
    public static Task<byte[]> PngVersDibAsync(byte[] png);        // pour écrire CF_DIB
    public static Task<byte[]> MiniatureAsync(byte[] png, uint largeurMax = 160);
}
```

## 4. Application : types publics

### 4.1 `PressePapiers.Systeme`

```csharp
public sealed class RelaisWindows : IDisposable
{
    public const uint MessageIcone = 0x8001;                 // WM_APP + 1
    public RelaisWindows(IJournal journal);                  // fenêtre de premier niveau invisible, sur le fil courant (interface)
    public nint Handle { get; }
    public event Action? CopieDetectee;                      // WM_CLIPBOARDUPDATE
    public event Action? RaccourciSecoursPresse;             // WM_HOTKEY, identifiant 1
    public event Action<uint>? MessageIconeRecu;             // LOWORD(lParam)
    public event Action? BarreDesTachesRecreee;              // message enregistré « TaskbarCreated »
    public event Action? SessionDeverrouillee;               // WM_WTSSESSION_CHANGE, WTS_SESSION_UNLOCK
    public event Action? SortieDeVeille;                     // WM_POWERBROADCAST, PBT_APMRESUMEAUTOMATIC
    public bool EcouterPressePapiers();                      // AddClipboardFormatListener
    public bool EnregistrerRaccourci(Raccourci raccourci, out int codeErreur);
    public void RetirerRaccourci();
    public void Dispose();
}

public sealed class IconeNotification : IDisposable
{
    public IconeNotification(RelaisWindows relais, IJournal journal);
    public void Afficher(bool alerte, string infobulle);     // NIM_ADD la première fois, NIM_MODIFY ensuite ; recréée sur BarreDesTachesRecreee
    public void Notifier(string titre, string message);      // bulle NIF_INFO
    public event Action? OuvrirDemande;
    public event Action? ParametresDemandes;
    public event Action? QuitterDemande;
    public void Dispose();                                   // NIM_DELETE
}

public interface ICrochetWinV : IDisposable
{
    bool EstActif { get; }
    string? RaisonInactif { get; }
    event Action<nint>? WinVPresse;                          // levé sur le fil du hook ; argument : fenêtre au premier plan
    void Demarrer();
    void Reinstaller();                                      // spec section 11 : nouveau posé avant retrait de l'ancien
}

public sealed class CrochetWinV : ICrochetWinV { public CrochetWinV(IJournal journal); }

public static class PremierPlan
{
    public static bool Amener(nint hwnd);                    // spec section 10.1
    public static nint FenetreActive();
    public static bool EstOrigineValide(nint hwnd, nint hwndOutil);   // ni l'outil, ni le bureau, ni la barre des tâches
}

public static class NiveauIntegrite
{
    public static int? DeFenetre(nint hwnd);                 // RID du niveau ; null si une étape échoue
    public static int DuProcessusCourant();
}

public sealed class SourceSessionWts : ISourceSession { }

public static class DemarrageWindows
{
    public static bool EstActive();
    public static void Appliquer(bool activer, string cheminExecutable);   // HKCU\...\Run, valeur « PressePapiers »
}

public sealed class JournalSerilog : IJournal, IDisposable { public JournalSerilog(string dossierJournal); }   // un fichier par jour, 14 gardés
```

### 4.2 `PressePapiers.Capture`

```csharp
public static class MarqueInterne
{
    public const string NomFormat = "PressePapiers.Interne";
    public static uint Format { get; }                       // RegisterClipboardFormat, vérifié non nul
}

public static class ApplicationOrigine
{
    public static string? Determiner(nint fenetre);          // nom du processus ; ApplicationFrameHost résolu ; null si inconnu
}

public sealed class LecteurPressePapiers
{
    public LecteurPressePapiers(nint hwndRelais, IJournal journal);
    // 10 essais espacés de 25 ms ; null si marque interne présente, illisible ou vide ; formats dans l'ordre de la spec, section 4.
    public Task<ContenuCopie?> LireAsync(string? application);
}

public sealed class EcrivainPressePapiers
{
    public EcrivainPressePapiers(nint hwndRelais, IJournal journal);
    public Task<bool> EcrireAsync(Element element, ModeCollage mode, string? cheminImage);   // pose toujours la marque interne
}

public sealed class ServiceCapture : IDisposable
{
    public ServiceCapture(RelaisWindows relais, LecteurPressePapiers lecteur, DepotHistorique depot,
                          Func<Parametres> parametres, IJournal journal);
    public event Action<Element>? ElementAjoute;
    public void Dispose();
}

public sealed class ServiceRecollage
{
    public ServiceRecollage(EcrivainPressePapiers ecrivain, DepotHistorique depot, IJournal journal);
    // Spec section 10.2 : écriture, contrôle de la fenêtre et de son niveau, Premier plan, attente des modificateurs, Ctrl+V.
    public Task<IssueCollage> CollerAsync(Element element, ModeCollage mode, nint fenetreOrigine, bool copieSeule,
                                          nint hwndOutil, Action masquerFenetre);
}
```

### 4.3 `PressePapiers.Interface`

```csharp
public sealed partial class FenetrePrincipaleVue : ObservableObject
{
    public FenetrePrincipaleVue(DepotHistorique depot, MoteurRecherche recherche, ServiceRecollage recollage,
                                ServiceParametres parametres, IJournal journal);
    public string EtatCrochet { get; set; }      // texte d'état du hook Win+V affiché dans les Paramètres, fourni par App
    // Le reste (collections, commandes, propriétés liées) appartient aux tâches de l'interface.
}

public sealed partial class FenetrePrincipale : Window
{
    public FenetrePrincipale(FenetrePrincipaleVue vue);
    public nint Handle { get; }
    public bool EstVisible { get; }
    public void AfficherPour(nint fenetreOrigine, OrigineOuverture origine);   // spec section 8, ouverture ; AppWindow.Show puis PremierPlan.Amener
    public void AfficherParametres();                                          // ouvre la fenêtre sur la page Paramètres
    public void Masquer();
}
```

`App` (fichier `App.xaml.cs`) et `Program` (fichier `Program.cs`) composent le tout ; ordre de démarrage de la spec, section 12.

## 5. Répartition des fichiers et des tâches

L'ordre des tâches garantit qu'une tâche ne s'appuie que sur des tâches précédentes : toute la logique de Coeur (1 à 15)
précède l'application (16 à 29). Les coquilles de la tâche 1 lèvent `NotImplementedException("Tâche NN")` jusqu'à ce que
leur tâche les remplace.

| Tâche | Contenu | Fichiers (sous `src/` et `tests/`) |
|---|---|---|
| 1 | Socle : solution, paquets, Directory.*.props, modèle, journal, `BaseHistorique` complet (schéma), `LecteurElement`, coquilles compilables de tous les autres types publics (y compris une `FenetrePrincipale` vide et une `App` minimale qui compile), premier test vert | tous les fichiers de projet ; `Coeur/Modele/*`, `Coeur/Journalisation/*`, `Coeur/Stockage/BaseHistorique.cs`, `BaseIncompatibleException.cs`, `LecteurElement.cs` ; coquilles |
| 2 | Utilitaires partagés : `DossierDonnees`, `Empreinte`, `Normalisation` | `Coeur/Stockage/DossierDonnees.cs`, `Empreinte.cs`, `Coeur/Recherche/Normalisation.cs` |
| 3 | `Raccourci` | `Coeur/Reglages/Raccourci.cs` |
| 4 à 7 | Stockage : onglets ; éléments ; purge, plafond, index, méta ; `Sauvegarde` | `Coeur/Stockage/DepotHistorique.cs`, `Sauvegarde.cs` |
| 8 | `DemarrageSession` | `Coeur/Session/*` |
| 9 | `MoteurRecherche` | `Coeur/Recherche/MoteurRecherche.cs` |
| 10 | `Parametres`, `ThemeApplication`, `ServiceParametres` | `Coeur/Reglages/*` sauf `Raccourci.cs` |
| 11, 12 | Logique du clavier et du collage : `AutomateWinV`, `AntiRebond` ; `DecisionCollage` | `Coeur/Clavier/*`, `Coeur/Collage/*` |
| 13 à 15 | Formats : `FormatHtml` ; `ListeFichiers` ; `ConvertisseurImage` | `Coeur/Formats/*` |
| 16 | Démarrage de l'application : `Program` (instance unique), `App` (cycle de vie, journal, base, copie de sécurité, session et purge, fenêtre B créée masquée, quitter), `JournalSerilog`, `SourceSessionWts`, `DemarrageWindows` | `PressePapiers/Program.cs`, `App.xaml(.cs)`, `Systeme/JournalSerilog.cs`, `SourceSessionWts.cs`, `DemarrageWindows.cs`, `Natif/NatifSession.cs` |
| 17, 18 | `RelaisWindows` ; `IconeNotification` | `Systeme/RelaisWindows.cs`, `IconeNotification.cs`, `Natif/NatifRelais.cs`, `NatifIcone.cs` |
| 19, 20 | `PremierPlan`, `NiveauIntegrite` ; `ICrochetWinV`, `CrochetWinV` | `Systeme/PremierPlan.cs`, `NiveauIntegrite.cs`, `ICrochetWinV.cs`, `CrochetWinV.cs`, `Natif/NatifPremierPlan.cs`, `NatifIntegrite.cs`, `NatifClavier.cs` |
| 21 à 23 | `MarqueInterne`, `ApplicationOrigine`, `LecteurPressePapiers` ; `EcrivainPressePapiers` ; `ServiceCapture` | `Capture/*` sauf `ServiceRecollage.cs`, `Natif/NatifPressePapiers.cs` |
| 24 | `ServiceRecollage` | `Capture/ServiceRecollage.cs`, `Natif/NatifSaisie.cs` |
| 25 à 28 | Interface : fenêtre et vue (liste, onglets, recherche) ; aperçu et dialogues ; clavier, ouverture et fermeture ; page Paramètres | `Interface/*` |
| 29 | Assemblage : composition complète dans `App` (relais, icône, hook, capture, recollage, fenêtre, réinstallation du hook, raccourci de secours, démarrage avec Windows), `scripts/publier.ps1`, `README.md` (recette manuelle de la spec, section 14) | `App.xaml.cs`, `scripts/*`, `README.md` |

Déclarations P/Invoke : dossier `src/PressePapiers/Natif/`, **un fichier par brique** (classe `internal static partial`
avec `LibraryImport`), comme indiqué dans le tableau. Une même fonction Windows peut être déclarée dans deux classes :
chaque brique reste autonome, aucune tâche ne modifie le fichier natif d'une autre.
