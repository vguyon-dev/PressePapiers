# Intégration du plan PressePapiers (tâche 29 et mise en cohérence)

Statut au 13/10/2026. Dépôt d'assemblage : `%LOCALAPPDATA%/Temp/pp/assemblage`, branche
`plan/assemblage` (base `plan/vague1`, fusion de `plan/demarrage` et `plan/interface`). Rien n'a été poussé : le clone n'a
pour distants que des dossiers temporaires d'autres auteurs, et le dépôt réel est en lecture seule pour cet assemblage. La
demande « pousse » de l'utilisateur vise le dépôt réel : elle revient à la session qui reporte le plan dans
`C:/Mes Projets/Autres/PressePapiers`.

## 1. Résultat

- Fusion sans conflit des branches `plan/demarrage` (tâche 16) et `plan/interface` (tâches 25 à 28).
- Commits ajoutés au-dessus des fusions :
  - `a2dc0d8 Intégration : partager le verrou de la connexion entre le dépôt et la recherche` ;
  - `ba2a4b2 Assembler l'application : relais, icône, hook Win+V, capture, recollage et démarrage avec Windows` (tâche 29) ;
  - `0d36092 Intégration : préciser l'unité de la taille mémorisée de la fenêtre`.
- Compilation de toute la solution : 0 avertissement, 0 erreur. Tests : 576 sur 576 réussis
  (`dotnet test --solution PressePapiers.slnx -c Debug`).
- Section écrite : `sections/tache-29.md` (code recopié des fichiers validés par `outils-assemblage/generer_tache29.py`).

## 2. Mise en cohérence des sections 1 à 29

Méthode (outils dans `%LOCALAPPDATA%/Temp/pp/outils-assemblage`) :

1. `plan.py comparer` : chaque bloc de code des sections 1 à 28 comparé au fichier du commit de sa tâche. Avant correction,
   seuls les projets `PressePapiers.csproj` des tâches 17, 18, 19 et 21 différaient (alignés à la main sur le fichier fusionné
   par la vague 1).
2. `rejouer.sh` : **rejeu linéaire depuis le seul texte des sections**, dans un dépôt neuf (`pp/lin`) parti du commit de la
   spec : pour chaque tâche dans l'ordre 1 à 29, écriture des fichiers des blocs, icônes produites par le script de la tâche
   18, `dotnet build` de toute la solution, `dotnet test` de toute la solution, puis exécution du bloc `git add` et
   `git commit` de l'étape 6 de la section et contrôle qu'aucun fichier ne reste hors du commit.
3. `plan.py final` : reconstitution textuelle des tâches 1 à 29 dans un dossier neuf (`pp/lin-texte`), comparée à l'arbre
   de `HEAD` du dépôt d'assemblage : **0 écart** (fins de ligne et marque d'ordre UTF-8 ignorées).

Résultats du rejeu linéaire (après corrections) : chaque tâche compile avec les seules tâches précédentes, 0 avertissement,
0 erreur, tous les tests verts, aucun fichier hors commit. Nombre de tests après chaque tâche :

| Tâche | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 à 22 | 23 à 26 | 27 à 29 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Tests | 25 | 72 | 128 | 162 | 202 | 250 | 274 | 287 | 350 | 380 | 408 | 446 | 467 | 482 | 504 | 513 | 521 | 576 |

### Corrections faites dans les sections

| Sections | Correction | Cause |
|---|---|---|
| 16, 17, 18, 19, 20, 21 | `AllowUnsafeBlocks` passe dans la tâche 16 (bloc complet du projet, ligne « Modifier », `git add`) ; le bloc du projet est retiré des tâches 17, 19 et 21, qui ne le changeaient plus ; renvois des tâches 18 et 20 corrigés. | Défaut trouvé par le seul rejeu linéaire : la tâche 16, écrite sur une base qui contenait déjà les tâches 17 à 24, ne compilait pas appliquée après la 15 (`CS0227`, `SYSLIB1062` sur `NatifSession` et `NatifDemarrage`). Le dépôt fusionné, lui, compilait. |
| 4, 5, 6, 9 | Verrou partagé (voir section 3) : `DepotHistorique` dans les trois états des tâches 4 à 6, `MoteurRecherche` et le nouveau test `RechercheConcurrenteTests` dans la tâche 9 (fichiers, interfaces, étape 1, contrôles, `git add`). Remarques de la tâche 4 sur le verrou réécrites. | Défaut signalé par l'auteur de l'interface, confirmé par un test. |
| 1 | Commentaire de `Parametres.LargeurFenetre` et `HauteurFenetre` : pixels logiques. | Signalé par l'auteur de l'interface. |
| 9 à 28 | Nombres de tests des étapes 2 et 4 remplacés par ceux du rejeu linéaire ; phrases « dans le clone de rédaction », « le total dépend des tâches écrites en parallèle » retirées. | Les sections donnaient les totaux de leur branche (par exemple 190 au lieu de 350 pour la tâche 9). |
| 17, 18 | Mention d'un avertissement WMC1509 « déjà présent » retirée. | Le rejeu ne montre aucun avertissement. |
| 21, 25, 26, 28 | Mentions des clones parallèles reformulées (« À la rédaction : N erreurs », renvoi à la tâche 16). | Plan linéaire. |
| 29 | Section écrite. | Tâche de cet assemblage. |

Non vérifié par le rejeu : les nombres d'échecs attendus aux étapes 2 (ils découlent des tests ajoutés, sans exécution de
chaque état rouge, sauf l'étape 2 de la tâche 29, jouée) et les programmes de vérification hors dépôt des tâches 16 à 28
(joués par leurs auteurs sur leurs branches, pas rejoués ici, sauf celui de la tâche 29).

## 3. Corrections de code de l'intégration

**Verrou commun du dépôt et de la recherche** (commit `a2dc0d8`, sections 4, 5, 6 et 9). `MoteurRecherche` verrouillait
`BaseHistorique.Connexion`, `DepotHistorique` un `Lock` privé : les deux ne s'excluaient pas. Un test de charge sur deux fils
(ajouts plus plafond de Temporaire, qui supprime dans une transaction, contre une recherche en boucle) a échoué 3 fois sur 3
(`InvalidOperationException` : commande hors de la transaction en cours). Avec le seul verrou commun, il échouait encore
de temps en temps : `NullReferenceException` à la fermeture de la connexion, car `MoteurRecherche` créait et libérait sa
commande hors du verrou et la connexion tient la liste de ses commandes sans protection. Correction : le dépôt verrouille la
connexion elle-même ; la recherche garde le verrou pendant toute la vie de sa commande. Après correction : 12 passages sur
12 verts ; mutation (verrou privé rétabli dans le dépôt) : 5 échecs sur 6. Le test réduit fortement la probabilité de manquer
la course, sans la prouver absente. Dans l'application assemblée, la capture écrit sur le fil d'interface (pas de
`ConfigureAwait(false)` dans la chaîne de capture, contexte de synchronisation posé par `Program`), donc la course n'était
pas encore déclenchée ; elle l'aurait été dès que l'écriture serait passée sur un autre fil.

## 4. Tâche 29 : choix et écarts à la consigne

- `App.xaml.cs` part du fichier de la tâche 16 ; la composition est dans un nouveau fichier partiel `App.Composition.cs`
  (fichiers courts). Le `catch (NotImplementedException)` provisoire est retiré.
- **Réinstallation périodique du hook** : la consigne demandait une réinstallation toutes les 15 minutes par
  `DispatcherQueueTimer`. `CrochetWinV` (tâche 20) le fait déjà sur son fil (`SetTimer`, 15 minutes), comme la spec
  (section 11). Pour ne pas doubler, la minuterie d'`App` (une minute) relit l'état du hook pour l'icône et les Paramètres
  et relance l'installation d'un hook inactif ; la réinstallation au déverrouillage et à la sortie de veille est branchée.
- Premier affichage (point 2) : fenêtre créée sans `Show(false)` ni `Activate`, `AppWindow.Show()` puis `PremierPlan.Amener`
  à chaque ouverture (déjà dans `FenetrePrincipale`).
- Quitter est posté sur le fil d'interface (le relais ne doit pas être détruit dans sa propre procédure) ; ordre : capture,
  hook, icône (`NIM_DELETE`), `FenetrePrincipale.AutoriserFermeture()`, relais, base, journal, `Application.Exit`.
- `DemarrageWindows.Appliquer(Reglages.DemarrageAvecWindows, Environment.ProcessPath)` à chaque démarrage, refus du registre
  noté sans bloquer.
- Taille de la publication : 231,2 Mo (Release, win-x64, autonome), relevée le 13/10/2026.

## 5. Écarts au contrat (à reporter dans `contrat.md`)

Fichiers et membres ajoutés hors du tableau de la section 5 du contrat, par tâche :

- Tâche 5 : `Coeur/Stockage/SchemaBase.cs` modifié (déclencheur `element_au` limité à `AFTER UPDATE OF texte_recherche`,
  deux index : onglet et empreinte, onglet et date d'usage). À reporter aussi dans le bloc SQL de la section 5 de la spec.
- Tâche 4 et 9 (intégration) : un seul verrou, la connexion, pour `DepotHistorique` et `MoteurRecherche` (le contrat dit
  « verrou interne » du dépôt).
- Tâche 16 : `Coeur/Stockage/IDialogueDemarrage.cs`, `Coeur/Stockage/OuvertureBase.cs` (à ajouter à la section 3.3),
  `Natif/NatifDemarrage.cs`, `Systeme/DialoguesWindows.cs`, `tests/.../Stockage/OuvertureBaseTests.cs` ; membres internes
  `App.ArretEnCours`, `App.SignalerActivation`, `App.AfficherFenetre(OrigineOuverture)`, `App.Quitter`,
  `App.PosterSurInterface(Action, string)` ; surcharges internes `DemarrageWindows.EstActive(string)` et
  `Appliquer(bool, string, string)` ; `PressePapiers.csproj` modifié (`AllowUnsafeBlocks`, déplacé de la tâche 17 par cette
  intégration). Le relais est créé avant la fenêtre principale.
- Tâche 18 : `scripts/generer-icones.ps1`, `Assets/icone-normale.ico`, `Assets/icone-alerte.ico`, `ItemGroup` des icônes dans
  le projet.
- Tâches 21 à 23 : `Capture/ContenuBrut.cs`, `FormatsPressePapiers.cs`, `LectureBrute.cs` ; `Coeur/Capture/ExecutionUnique.cs`
  et ses tests (testable sans WinUI).
- Tâches 25 à 28 : `Coeur/Clavier/ZoneFocus.cs`, `ActionTouche.cs`, `CommandeClavier.cs`, `TableDesTouches.cs` et
  `TableDesTouchesTests.cs` ; `Natif/NatifOuverture.cs` ; vue et fenêtre réparties en fichiers partiels
  (`FenetrePrincipaleVue.*.cs`, `FenetrePrincipale.xaml`, `.xaml.cs`, `.Volet.cs`, `.Affichage.cs`, `.Clavier.cs`) ; types
  `OngletVue`, `ElementVue`, `ResumeElement`, `IDialogues`, `GestionnaireDialogues`, `RenduRtf`, `VueApercu`, `PageParametres` ;
  coquille `FenetrePrincipale.cs` supprimée ; ajouts publics `FenetrePrincipale.PremierPlanRefuse`, `AutoriserFermeture()`,
  `FenetrePrincipaleVue.AppliquerRaccourci`, `AppliquerDemarrage`, `OuvrirChemin`, `Notifier`, `Dialogues`,
  `ParametresModifies` ; `EtatCrochet` vaut « Inconnu » au départ.
- Tâche 29 : `App.Composition.cs` (partie de `App`) ; minuterie de surveillance du hook au lieu d'une seconde réinstallation
  périodique (section 4).
- Assistants de test partagés : `DepotDeTest`, `ContenusDeTest`, `JournalCollecte`, `ImagesSynthetiques`, `InsertionsSql`,
  `DossierTemporaire`.

## 6. Essai de fumée de l'application assemblée

Script `pp/essais-assemblage/fumee.ps1` (code complet dans la tâche 29, étape 1), dossier de données jetable par
`PRESSEPAPIERS_DONNEES`, `parametres.json` avec `"DemarrageAvecWindows": false` écrit avant le premier lancement.

- Version Debug du dépôt d'assemblage : 29 contrôles OK, 0 échec, 0 non testé (sortie dans `fumee-sortie-1.txt`) :
  démarrage sans fenêtre visible, relais trouvé, fenêtre principale créée masquée, icône présente (`Shell_NotifyIconGetRect`),
  hook posé, raccourci de secours enregistré, démarrage avec Windows désactivé ; `Set-Clipboard` d'un texte puis ligne de
  type 1 dans Temporaire ; image copiée par Windows Forms puis ligne de type 3 et PNG présent dans `images\` ; Ctrl+Alt+Maj+V
  par `SendInput` depuis une fenêtre Windows Forms au premier plan : fenêtre visible et au premier plan ; Échap : fenêtre
  masquée, application vivante ; second lancement : la seconde instance s'arrête (code 0), la fenêtre de la première s'ouvre
  au premier plan, un seul processus ; Quitter par le menu de l'icône (`WM_CONTEXTMENU` posté au relais puis Q) : arrêt en
  code 0, icône retirée, aucune erreur au journal ; clé Run identique avant et après ; aucun processus restant ; texte neutre
  remis dans le presse-papiers.
- Version publiée par `scripts/publier.ps1 -Destination <dossier d'essai>` (Release) : mêmes 29 contrôles OK
  (`fumee-sortie-publiee.txt`). Dossier d'essai et publication supprimés ensuite.
- Version Debug reconstruite depuis les sections (`pp/lin`, tâches 1 à 28) : la vérification rouge de l'étape 2 de la tâche
  29 donne 9 échecs attendus (`fumee-sortie-rouge.txt`).
- Version Debug reconstruite depuis les sections (`pp/lin`, tâche 29 comprise) : passage interrompu par l'activité de
  l'utilisateur sur le poste (une copie dans un autre éditeur pendant l'essai, Visual Studio Code au premier plan) : 17 OK,
  1 échec (le dernier élément était la copie de l'utilisateur, l'image avait bien été enregistrée juste avant), 2 non testés
  par sécurité (aucune frappe envoyée hors de la fenêtre cible). Non relancé pour ne plus toucher au poste pendant qu'il est
  utilisé ; le code est identique à celui du dépôt d'assemblage (comparaison textuelle, 0 écart). Sortie dans
  `fumee-sortie-lin-interrompue.txt` (le texte copié par l'utilisateur en a été retiré).
- Effets sur le poste pendant les essais : le contenu du presse-papiers de l'utilisateur a été remplacé à chaque passage (texte
  d'essai, image, puis « texte neutre », qui y est resté) ; le hook Win+V d'une instance d'essai a été actif quelques minutes
  à chaque passage ; lors du passage interrompu, le menu de l'icône a pu rester affiché jusqu'à l'arrêt de l'instance. Aucun
  réglage Windows ni la clé Run n'ont changé (contrôlé), aucun processus n'est resté, les serveurs de compilation sont arrêtés.
- SQLite embarqué : version 3.53.3 (journal de démarrage), FTS5 trigram créé.

## 7. Validations (points de vigilance du plan et section 16 de la spec)

Points de vigilance :

1. Rafale de copies : confirmé (tâche 5 : un élément par contenu, ordre `DateUsage` puis `Id` décroissants, mutations ;
   tâche 23 : une rafale de notifications ne donne qu'une lecture de plus).
2. Gros contenus : confirmé (tâche 2 sous une seconde, tâche 15 conversion 4K, tâche 21 texte de plus de 10 Mio ignoré) ;
   limite connue : un texte de 9 Mo coûte environ 2 s d'enregistrement sur le fil d'interface.
3. Saisie de recherche hostile : confirmé (tâche 9, 40 saisies, défaut du caractère nul corrigé).
4. Données abîmées : confirmé (tâches 1, 6, 7, 10 et 16).
5. Signaux répétés : confirmé en logique (tâche 11 : automate, anti-rebond de 300 ms) ; l'anti-rebond commun et le refus
   quand la fenêtre est visible sont branchés par la tâche 29 ; un vrai Win+V n'est pas simulable.

Section 16 de la spec :

| Point | État | Source |
|---|---|---|
| 1. `LogonTime` | partiel | Tâche 16 : deux lectures identiques et même valeur dans un processus neuf, 142 ms après le début de session de `Win32_LogonSession`. Non testé : processus élevé, Win+L, veille, Bureau à distance, démarrage rapide. |
| 2. Premier plan d'une fenêtre jamais activée | confirmé | Tâches 16 (essai WinUI 3 jetable, Bloc-notes au premier plan) et 27 (vraie fenêtre) : `Show()` puis `Amener` suffisent, sans `Show(false)` ; fumée de la tâche 29 : fenêtre au premier plan par le raccourci de secours et par la relance. Clignotement à juger à la recette. |
| 3. RTF dans `RichEditBox` | partiel | Tâche 26 : RTF écrit à la main (gras, couleur, tableau) rendu correctement ; tableau plus large que la colonne coupé à droite ; aucun vrai RTF de Word ou d'Outlook. |
| 4. Icône `Shell_NotifyIcon`, menu natif, `TaskbarCreated` | confirmé sauf vrais clics | Tâches 17 et 18 (ajout, modification, bulle, recréation après retrait forcé, menu par messages postés) ; fumée de la tâche 29 (icône posée, Quitter par le menu, icône retirée). Non testé : vrais clics de souris, vrai redémarrage de l'Explorateur. |
| 5. `RegisterHotKey` devant une fenêtre administrateur | non testé | Tâche 17 : raccourci déclenché par `SendInput` devant des fenêtres ordinaires, conflit 1409 et rétablissement ; fumée de la tâche 29 : raccourci de secours opérationnel. Aucune fenêtre élevée essayée. |
| 6. Win+V devant une fenêtre administrateur | non testé | Aucune source ; recette manuelle. |
| 7. `CF_DIBV5` vers PNG avec transparence, format `PNG` à l'écriture | partiel | Tâches 15, 21 et 22 : lecture d'un V5 avec alpha confirmée, aller-retour réel ; acceptation du format `PNG` par Office, un navigateur et Paint non testée. |
| 8. SQLite >= 3.34.0 et FTS5 trigram | confirmé | Tâche 1 (tests) ; fumée de la tâche 29 : SQLite 3.53.3. |

## 8. Reste à tester par l'utilisateur

La recette manuelle complète est dans le `README.md` (sections « Recette manuelle » et « Points de la conception encore à
vérifier sur le poste »). Points les plus importants :

- vrai Win+V dans plusieurs applications (avalement par la vraie touche Windows, menu Démarrer non ouvert) ; c'est le premier
  point à examiner si Win+V n'est pas repris (lecture de la touche Windows par `GetAsyncKeyState`) ;
- fenêtre administrateur : Win+V natif, raccourci de secours, collage refusé avec notification ;
- RTF copié depuis Word et Outlook ; collage dans Word, Excel, PowerPoint, Outlook, un navigateur, Paint, transparence ;
- sessions : démarrage rapide, redémarrage, fermeture de session, Win+L, veille, Bureau à distance, lancement en administrateur ;
- vrais clics sur l'icône (à épingler hors du menu de débordement de Windows 11), redémarrage de l'Explorateur ;
- mise à l'échelle 150 % et écrans d'échelles différentes ; impression visuelle du premier affichage ;
- une erreur réelle de l'interface notée au journal (`Application.UnhandledException`).

## 9. Spec et contrat à mettre à jour dans le dépôt réel

- Spec section 5 : bloc SQL (déclencheur `AFTER UPDATE OF texte_recherche`, deux index).
- Spec section 8 : Suppr ne supprime que recherche vide, y compris depuis la liste ; Ctrl+Maj+Entrée copie seulement ; une
  perte du focus pendant l'ouverture est rattrapée ; taille mémorisée en pixels logiques ; le volet réagit à `ItemInvoked` ;
  la croix est interceptée par `AppWindow.Closing`, `Window.Close()` ne passe pas par là (d'où `AutoriserFermeture`).
- Spec section 12 : relais créé avant la fenêtre principale ; arrêt de recette `PRESSEPAPIERS_ARRET_APRES_MS` (Debug) ;
  taille de la publication 231 Mo.
- Spec section 13 : une exception sortie d'un rappel `DispatcherQueue` ou d'une minuterie arrête le processus sans passer par
  `UnhandledException` (d'où `App.PosterSurInterface`) ; la capture enregistre sur le fil d'interface.
- Spec section 11 : la réinstallation périodique vit dans le hook ; `App` surveille l'état chaque minute.
- Spec section 16 : états du tableau de la section 7 ci-dessus.
- Contrat : écarts de la section 5 ci-dessus ; « verrou interne » du dépôt remplacé par le verrou de la connexion partagé
  avec `MoteurRecherche`.
