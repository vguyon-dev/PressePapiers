# PressePapiers : plan d'implémentation

> **Pour les agents qui exécutent ce plan :** SOUS-COMPÉTENCE REQUISE : superpowers:subagent-driven-development
> (recommandé) ou superpowers:executing-plans, tâche par tâche. Les étapes utilisent des cases à cocher (`- [ ]`).
> Chaque tâche est dans son propre fichier, `2026-10-13-pressepapiers/tache-NN.md` : un exécutant lit ce document,
> le contrat, puis le seul fichier de sa tâche.

**Objectif :** un gestionnaire de presse-papiers WinUI 3 qui remplace l'historique Win+V de Windows 11 : historique long,
recherche, éléments nommés rangés dans des onglets, onglet Temporaire vidé à chaque nouvelle session Windows.

**Architecture :** une seule application en zone de notification. La logique sans interface (modèle, stockage SQLite avec
index plein texte, recherche, session, réglages, formats du presse-papiers, automate du hook, décision de collage) vit dans
`PressePapiers.Coeur`, testée par xUnit ; l'application WinUI 3 ajoute la fenêtre relais Win32, l'icône, le hook Win+V, le
premier plan, la capture, le recollage et la fenêtre B ; `App` compose le tout.

**Pile :** C# sur .NET 10 (SDK 10.0.401), `net10.0-windows10.0.19041.0` ; WinUI 3 par Microsoft.WindowsAppSDK 2.5.1
(non empaquetée, autonome, x64) et Microsoft.Windows.SDK.BuildTools 10.0.28000.2705 ; Microsoft.Data.Sqlite 10.0.12
(SQLite 3.53.3, FTS5 trigram) ; CommunityToolkit.Mvvm 8.4.2 ; Serilog 4.4.0 et Serilog.Sinks.File 7.0.0 ; xunit.v3 4.0.1,
xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1, Microsoft.Extensions.TimeProvider.Testing 10.10.0.

**Spec :** [`docs/superpowers/specs/2026-10-13-pressepapiers-design.md`](../specs/2026-10-13-pressepapiers-design.md)
**Contrat (noms, signatures, répartition des fichiers) :** [`2026-10-13-pressepapiers/contrat.md`](2026-10-13-pressepapiers/contrat.md)
**Journal de construction du plan :** [`2026-10-13-pressepapiers/integration.md`](2026-10-13-pressepapiers/integration.md)

> **Statut au 13/10/2026, après l'exécution.** Le plan a été exécuté en reprenant ses 29 commits validés, puis relu par
> cinq relecteurs indépendants (un par phase) : aucun défaut critique, des corrections importantes faites et fusionnées
> ensuite (lecture de résumés et schéma en version 2, images hostiles, sauvegardes, lecture en retard du presse-papiers,
> collages jamais muets, ouverture fiable, plafond confirmé, Win+V seul, garde-fous de `publier.ps1`). Le texte des tâches
> ci-dessous décrit l'état **avant** la revue ; le code de `main` fait foi. Détail des constats, des corrections et de ce qui
> est en réserve : [`2026-10-13-pressepapiers/revue.md`](2026-10-13-pressepapiers/revue.md).

## Comment ce plan a été construit

Le 13/10/2026, chaque tâche a été codée, compilée et testée sur le poste avant d'être rédigée : socle, puis deux vagues
d'auteurs en parallèle sur des branches séparées, fusion, puis assemblage. Le plan entier a ensuite été **rejoué depuis son
seul texte** dans un dépôt neuf : pour chaque tâche dans l'ordre, écriture des fichiers des blocs, compilation de toute la
solution, tous les tests, puis le commit de l'étape 6, avec contrôle d'un arbre propre. Résultat : chaque tâche compile avec
les seules tâches précédentes, 0 avertissement, 0 erreur, tous les tests verts (576 à la fin), et le code reconstitué est
identique au code assemblé. L'application assemblée a passé un essai de fumée de 29 contrôles sur 29, en Debug comme en
version publiée.

## Contraintes globales

Chaque tâche les respecte implicitement.

- Cible : `net10.0-windows10.0.19041.0` partout ; SDK .NET 10.0.401 ; `global.json` impose Microsoft.Testing.Platform
  (sans lui, `dotnet test` refuse xunit.v3 sous le SDK .NET 10).
- Commandes, à la racine du dépôt : compilation `dotnet build PressePapiers.slnx -c Debug` ; tests
  `dotnet test --solution PressePapiers.slnx -c Debug`. Pas de `-p:Platform` : l'application déclare `Platforms=x64`.
- Fin de chaque tâche : compilation de toute la solution sans erreur ni avertissement, et TOUS les tests verts, pas
  seulement ceux de la tâche. Chaque commit se construit seul.
- Application non empaquetée (`WindowsPackageType=None`) et autonome (`WindowsAppSDKSelfContained=true`), x64, `Main`
  personnalisé (`DISABLE_XAML_GENERATED_MAIN`) ; pas de `PublishTrimmed`, pas de NativeAOT, pas de fichier unique.
- Versions NuGet fixées dans `Directory.Packages.props` uniquement (gestion centralisée) ; Microsoft.WindowsAppSDK 2.5.1.
- Données : `%LOCALAPPDATA%\PressePapiers` (`historique.db`, `images\`, `sauvegardes\`, `journal\`, `parametres.json`).
  **Tout lancement d'essai** pose `PRESSEPAPIERS_DONNEES` vers un dossier temporaire et un `parametres.json` dont
  `DemarrageAvecWindows` vaut `false` : jamais les données réelles, jamais la vraie clé `Run`.
- SQLite >= 3.34.0 (tokenizer trigram), contrôlé au démarrage ; jamais `INSERT OR REPLACE` sur `element`.
- Valeurs de la spec : raccourci de secours par défaut Ctrl+Alt+Maj+V ; plafond de Temporaire 5 000 (0 = désactivé) ;
  lecture du presse-papiers : 10 essais espacés de 25 ms, abandon à 2 s ; texte de plus de 10 Mio ignoré ; miniature de
  160 px ; 3 copies de sécurité ; journal d'un fichier par jour, 14 gardés ; anti-rebond de 300 ms.
- Code : noms en français, commentaires en français centrés sur l'intention, documentation XML sur le public (CS1591 actif),
  fichiers courts ; dates par `TimeProvider` ; assertions xUnit seules ; P/Invoke par `LibraryImport`, un fichier natif par
  brique (`AllowUnsafeBlocks` posé à la tâche 16).
- Fils : relais, icône, fenêtre et presse-papiers sur le fil d'interface ; le fil du hook passe par `DispatcherQueue` ; un
  rappel posté au fil d'interface passe par `App.PosterSurInterface` (une exception sortie d'un rappel `DispatcherQueue` ou
  d'une minuterie arrête le processus sans passer par `UnhandledException`).
- Messages de commit en français, à l'impératif, sans ligne Co-Authored-By ; aucun tiret cadratin ni demi-cadratin
  d'incise, dans le code comme dans les textes.

## Points de vigilance (Review Focus)

Cas que la spec implique sans les nommer, et qui piqueraient en premier un utilisateur. Chacun a son test dans la tâche
indiquée.

1. **Rafale de copies** (même contenu recopié, contenus différents à la même milliseconde) : un seul élément par contenu
   dans Temporaire, ordre stable `DateUsage` puis `Id` décroissants. Tâche 5 ; tâche 23 pour une rafale de notifications
   pendant une capture (une seule lecture de plus).
2. **Gros contenus** (texte de 9 Mo, image 3840 x 2160) : empreinte et normalisation sous une seconde (tâche 2), conversion
   d'une image 4K (tâche 15), texte de plus de 10 Mio ignoré (tâche 21).
3. **Saisie de recherche hostile** (guillemets, `*`, `:`, AND, OR, NOT, `%`, `_`, emoji, caractère nul, vide, espaces) :
   jamais d'exception. Tâche 9 (40 saisies ; le caractère nul faisait planter SQLite avant correction).
4. **Données abîmées ou absentes** (fichier qui n'est pas une base, `parametres.json` corrompu, PNG manquant, base
   illisible au démarrage) : exception typée, défauts, suppression sans planter, proposition de restauration. Tâches 1, 6,
   7, 10 et 16.
5. **Signaux répétés** (Win+V qui se répète, double signal en moins de 300 ms, fenêtre déjà visible) : une seule
   ouverture. Tâche 11 (automate, anti-rebond) ; branchement dans la tâche 29.

## Tâches

Les nombres de tests sont ceux du rejeu linéaire, après la tâche.

| Phase | Tâche | Fichier | Tests |
|---|---|---|---|
| 1. Fondations | 1. Socle : solution, paquets, modèle, base SQLite et coquilles | [tache-01.md](2026-10-13-pressepapiers/tache-01.md) | 25 |
| | 2. DossierDonnees, Empreinte, Normalisation | [tache-02.md](2026-10-13-pressepapiers/tache-02.md) | 72 |
| | 3. Raccourci | [tache-03.md](2026-10-13-pressepapiers/tache-03.md) | 128 |
| 2. Stockage et session | 4. DepotHistorique : onglets | [tache-04.md](2026-10-13-pressepapiers/tache-04.md) | 162 |
| | 5. DepotHistorique : éléments | [tache-05.md](2026-10-13-pressepapiers/tache-05.md) | 202 |
| | 6. DepotHistorique : suppression, purge, plafond, index, méta | [tache-06.md](2026-10-13-pressepapiers/tache-06.md) | 250 |
| | 7. Sauvegarde de la base | [tache-07.md](2026-10-13-pressepapiers/tache-07.md) | 274 |
| | 8. DemarrageSession | [tache-08.md](2026-10-13-pressepapiers/tache-08.md) | 287 |
| 3. Recherche et réglages | 9. MoteurRecherche | [tache-09.md](2026-10-13-pressepapiers/tache-09.md) | 350 |
| | 10. Parametres, ThemeApplication et ServiceParametres | [tache-10.md](2026-10-13-pressepapiers/tache-10.md) | 380 |
| 4. Logique du clavier et du collage | 11. AutomateWinV et AntiRebond | [tache-11.md](2026-10-13-pressepapiers/tache-11.md) | 408 |
| | 12. DecisionCollage | [tache-12.md](2026-10-13-pressepapiers/tache-12.md) | 446 |
| 5. Formats | 13. FormatHtml | [tache-13.md](2026-10-13-pressepapiers/tache-13.md) | 467 |
| | 14. ListeFichiers | [tache-14.md](2026-10-13-pressepapiers/tache-14.md) | 482 |
| | 15. ConvertisseurImage | [tache-15.md](2026-10-13-pressepapiers/tache-15.md) | 504 |
| 6. Application et système | 16. Démarrage de l'application (instance unique, journal, base, session, fenêtre masquée) | [tache-16.md](2026-10-13-pressepapiers/tache-16.md) | 513 |
| | 17. RelaisWindows, la fenêtre relais Win32 | [tache-17.md](2026-10-13-pressepapiers/tache-17.md) | 513 |
| | 18. IconeNotification, icône et menu natif | [tache-18.md](2026-10-13-pressepapiers/tache-18.md) | 513 |
| 7. Premier plan et Win+V | 19. PremierPlan et NiveauIntegrite | [tache-19.md](2026-10-13-pressepapiers/tache-19.md) | 513 |
| | 20. ICrochetWinV et CrochetWinV | [tache-20.md](2026-10-13-pressepapiers/tache-20.md) | 513 |
| 8. Capture et recollage | 21. MarqueInterne, ApplicationOrigine, LecteurPressePapiers | [tache-21.md](2026-10-13-pressepapiers/tache-21.md) | 513 |
| | 22. EcrivainPressePapiers | [tache-22.md](2026-10-13-pressepapiers/tache-22.md) | 513 |
| | 23. ServiceCapture | [tache-23.md](2026-10-13-pressepapiers/tache-23.md) | 521 |
| | 24. ServiceRecollage | [tache-24.md](2026-10-13-pressepapiers/tache-24.md) | 521 |
| 9. Interface | 25. Fenêtre principale : onglets, liste, recherche | [tache-25.md](2026-10-13-pressepapiers/tache-25.md) | 521 |
| | 26. Aperçu, rangement des éléments et boîtes de dialogue | [tache-26.md](2026-10-13-pressepapiers/tache-26.md) | 521 |
| | 27. Clavier, ouverture et fermeture | [tache-27.md](2026-10-13-pressepapiers/tache-27.md) | 576 |
| | 28. Page Paramètres | [tache-28.md](2026-10-13-pressepapiers/tache-28.md) | 576 |
| 10. Assemblage | 29. Assemblage, publication et README | [tache-29.md](2026-10-13-pressepapiers/tache-29.md) | 576 |

Les tâches 16 à 29 touchent du code Windows natif ou d'interface : leur étape 5 décrit une vérification sur le poste
(programme jetable hors du dépôt ou banc d'essai non committé), dont le code complet figure dans la tâche.

**Revues :** une revue de code à la fin de chaque phase, et une revue finale de toute la branche.

## État des points à valider (spec, section 16)

| Point | État au 13/10/2026 |
|---|---|
| 1. `LogonTime` stable et identique en administrateur | partiel : deux lectures identiques et même valeur dans un processus neuf ; élévation, Win+L, veille, Bureau à distance, démarrage rapide à tester en recette |
| 2. Premier affichage au premier plan | confirmé : `AppWindow.Show()` puis `PremierPlan.Amener` suffisent, sans première activation ; clignotement à juger à l'œil |
| 3. RTF dans `RichEditBox` | partiel : RTF écrit à la main bien rendu, tableau plus large que la colonne coupé ; vrai RTF de Word et d'Outlook à tester |
| 4. Icône, menu natif, `TaskbarCreated` | confirmé, sauf les vrais clics de souris et un vrai redémarrage de l'Explorateur |
| 5. Raccourci de secours devant une fenêtre administrateur | non testé |
| 6. Win+V devant une fenêtre administrateur | non testé |
| 7. Transparence `CF_DIBV5` vers PNG, format `PNG` à l'écriture | partiel : lecture avec alpha et aller-retour réel confirmés ; acceptation par Office, un navigateur et Paint à tester |
| 8. SQLite >= 3.34.0 et FTS5 trigram | confirmé : SQLite 3.53.3 |

La recette manuelle complète est dans le `README.md` produit par la tâche 29 (sections « Recette manuelle » et « Points de
la conception encore à vérifier sur le poste »). Premier point à examiner si Win+V n'est pas repris : la lecture de la vraie
touche Windows par `GetAsyncKeyState` dans `CrochetWinV` (un vrai Win+V ne se simule pas : les frappes injectées sont
ignorées exprès).

## Limite connue

La capture enregistre l'élément sur le fil d'interface : un texte de plusieurs mégaoctets gèle la fenêtre environ deux
secondes (indexation trigram). Le verrou commun du dépôt et de la recherche rend sûr un passage futur de cette écriture sur
un autre fil.
