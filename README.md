# PressePapiers

Gestionnaire de presse-papiers pour Windows 11, qui remplace l'historique de Win+V par un outil personnel plus complet :

- un historique bien plus grand que les 25 éléments de Windows (5 000 éléments dans l'onglet Temporaire par défaut, réglable) ;
- la recherche dans tout l'historique (casse et accents ignorés) ;
- des éléments gardés sous un nom, rangés dans des onglets ;
- un onglet Temporaire pour le reste, vidé à chaque nouvelle session Windows (arrêt, redémarrage, déconnexion), mais pas quand
  l'outil est relancé ;
- l'ouverture par Win+V et le recollage dans la fenêtre d'origine.

Contenus capturés : texte, texte mis en forme (RTF et `HTML Format`), images, listes de fichiers. **Tout est enregistré,
sans exception** : les contenus qu'une application demande à exclure de l'historique (gestionnaires de mots de passe, par
exemple) le sont aussi. C'est un choix assumé ; la base n'est pas chiffrée.

Application WinUI 3 (Windows App SDK 2.5.1) en C# sur .NET 10, non empaquetée et autonome, qui vit dans la zone de
notification. Conception : `docs/superpowers/specs/2026-10-13-pressepapiers-design.md`. Licence MIT.

## Installation

Prérequis pour compiler : Windows 11 x64 et le SDK .NET 10. Aucun droit administrateur n'est nécessaire, et la machine
n'a pas besoin du runtime .NET ni du Windows App SDK : la publication les embarque.

```powershell
pwsh -NoProfile -File scripts\publier.ps1 -Lancer
```

Le script publie en Release (win-x64, autonome) dans `publish\PressePapiers` (environ 231 Mo), arrête l'instance installée
s'il y en a une, copie la publication dans `%LOCALAPPDATA%\Programs\PressePapiers`, puis lance la version installée
(`-Lancer`). `-Destination <dossier>` choisit un autre dossier d'installation (un chemin relatif se résout par rapport au
dossier courant de PowerShell). Pour mettre à jour, relancer le même script.

La copie miroir supprime ce qui n'est pas dans la publication : le script contrôle donc la destination avant de publier ou
d'arrêter quoi que ce soit, et la refuse si :

- elle contient ou recouvre le dossier des données (`%LOCALAPPDATA%\PressePapiers`, ou celui de `PRESSEPAPIERS_DONNEES`), le dépôt
  ou le dossier `publish` ;
- c'est la racine d'un disque, un dossier de profil connu (profil, Bureau, Documents, Téléchargements, `AppData`, `Programs`,
  Program Files, Windows...) ou l'un de leurs parents ;
- c'est un fichier, ou un dossier existant qui n'est ni vide ni déjà une installation (sans `PressePapiers.exe`).

Seul le processus dont l'exécutable est celui de la destination est arrêté (une version Debug en essai n'est pas touchée) :
demande de fermeture d'abord, arrêt forcé au bout de 5 secondes. La fenêtre de l'application étant le plus souvent masquée,
la demande de fermeture reste en général sans effet et l'arrêt est forcé.

Au lancement d'une version Release, l'application inscrit son chemin dans
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (valeur `PressePapiers`) si le démarrage avec Windows est activé dans ses
réglages, ce qui est le cas par défaut, et ne la réécrit que si le chemin a changé ; sinon elle retire cette valeur. Une version
Debug n'écrit jamais cette clé (elle le note au journal).

Windows 11 range une nouvelle icône dans le menu de débordement de la zone de notification (la flèche vers le haut).
L'épingler dans la barre des tâches permet de voir son aspect d'alerte (pastille rouge) quand Win+V n'est pas repris.

**Désinstaller :** désactiver « Démarrer avec Windows » dans les Paramètres, quitter par le menu de l'icône, puis supprimer
`%LOCALAPPDATA%\Programs\PressePapiers` (et `%LOCALAPPDATA%\PressePapiers` pour effacer aussi l'historique).

## Utilisation

Copier comme d'habitude (Ctrl+C, menu Copier) : chaque copie arrive en tête de l'onglet Temporaire. Un contenu déjà
présent dans Temporaire n'est pas dupliqué, il remonte en tête.

Win+V ouvre la fenêtre au centre de l'écran de la fenêtre active, sur Temporaire, premier élément choisi, curseur dans la
recherche. Taper pour chercher, choisir avec les flèches, Entrée pour coller dans la fenêtre d'origine. La fenêtre se masque
après un collage, avec Échap ou quand elle perd le focus. Un raccourci de secours fait la même chose (Ctrl+Alt+Maj+V par
défaut) ; il sert aussi quand Win+V n'est pas repris, par exemple devant une fenêtre lancée en administrateur. Win+V ou le
raccourci de secours pendant que la fenêtre est déjà affichée mais passée derrière une autre (après une boîte de dialogue, par
exemple) la ramène au premier plan. La fenêtre n'est ni agrandissable ni réductible et n'apparaît pas dans Alt+Tab.

Ouverte depuis l'icône de notification ou par un second lancement de l'application, la fenêtre n'a pas de fenêtre d'origine
fiable : Entrée copie alors l'élément, à coller ensuite avec Ctrl+V. Devant une fenêtre lancée en administrateur, Windows
bloque le collage simulé : l'élément est copié et une notification dit de coller avec Ctrl+V. De même, quand le collage
échoue ou n'est pas envoyé alors que la fenêtre est déjà masquée (premier plan changé, touches de modification restées
enfoncées), une notification de l'icône l'annonce : « Collage impossible, l'élément est copié : colle avec Ctrl+V ».

**Garder un élément :** F2 (ou le bouton Nommer), saisir un nom, choisir un onglet autre que Temporaire, Entrée ou
Enregistrer (le curseur revient alors à la recherche). Un élément nommé est gardé sans limite de durée ; il peut être renommé, déplacé ou supprimé. Les onglets se
créent par « Nouvel onglet » dans le volet de gauche ; un onglet non vide demande confirmation avant d'être supprimé, avec
ses éléments. L'onglet Temporaire ne se renomme pas et ne se supprime pas.

**Icône de notification :** clic pour ouvrir ; clic droit pour le menu Ouvrir, Paramètres, Quitter.

### Raccourcis de la fenêtre

| Touche | Action |
|---|---|
| saisie | chercher dans tout l'historique (tous les onglets) |
| Haut, Bas | choisir un élément |
| Entrée, double-clic | coller dans la fenêtre d'origine |
| Maj+Entrée | coller dans l'autre mode de mise en forme (texte seul ou avec mise en forme) |
| Ctrl+Entrée (avec ou sans Maj) | copier seulement, sans coller |
| F2 | aller au champ Nom |
| Entrée dans le champ Nom | enregistrer (nom et onglet requis) |
| Suppr | supprimer l'élément, seulement quand la recherche est vide (sinon Suppr efface un caractère) ; un élément nommé demande confirmation |
| Ctrl+Tab, Ctrl+Maj+Tab | onglet suivant, précédent |
| Ctrl+1 à Ctrl+9 | aller à l'onglet n (Ctrl+1 : Temporaire) |
| Échap | effacer la recherche, puis masquer la fenêtre |

Tab garde son rôle normal (passer d'un contrôle à l'autre).

## Réglages

Page Paramètres de la fenêtre (entrée en bas du volet, ou menu de l'icône) :

| Réglage | Défaut |
|---|---|
| Raccourci de secours | Ctrl+Alt+Maj+V (au moins deux modificateurs parmi Ctrl, Alt et Maj, jamais la touche Windows ; touche : lettre, chiffre, F1 à F24 ou Espace). Un message s'affiche si Windows le refuse parce qu'une autre application l'a déjà pris ; si le fichier des réglages refuse ensuite l'écriture, l'ancien raccourci est remis. Sur un clavier AZERTY, Ctrl+Alt+lettre est aussi AltGr+lettre (€, @, #...) : préférer une combinaison avec Maj ou une touche de fonction. |
| Démarrer avec Windows | activé (jamais écrit dans le registre par une version Debug) |
| Mise en forme par défaut au collage | texte seul |
| Plafond de Temporaire | 5 000 éléments (0 = sans plafond) ; au-delà, les plus anciens sont supprimés. Un plafond qui supprimerait des éléments demande une confirmation, avec leur nombre ; la valeur n'est proposée qu'une seconde après le dernier clic sur les flèches du champ |
| Thème | Système (ou Clair, Sombre) |
| Applications affichées en chasse fixe | terminaux et éditeurs de code courants ; enregistrées 500 ms après la dernière frappe, à la perte du focus et au masquage de la fenêtre |

La page montre aussi l'état du hook Win+V (actif, ou inactif avec la raison) et deux boutons, « Ouvrir le dossier des
données » et « Ouvrir le journal ». Les réglages sont dans `parametres.json` ; si le fichier est illisible, l'application
prend les défauts et le note au journal.

## Dossier des données

`%LOCALAPPDATA%\PressePapiers` :

| Élément | Contenu |
|---|---|
| `historique.db` | base SQLite (mode WAL) : onglets, éléments, index de recherche plein texte |
| `images\` | une image PNG par élément de type image |
| `sauvegardes\` | copie de la base faite à chaque démarrage, les 3 dernières gardées ; une base illisible au démarrage propose de restaurer la plus récente |
| `journal\` | journal de l'application, un fichier par jour, 14 jours gardés |
| `parametres.json` | réglages |

La variable d'environnement `PRESSEPAPIERS_DONNEES` remplace ce dossier par un autre : pour essayer l'application sans
toucher aux vraies données. Pour un essai, y créer d'abord un `parametres.json` contenant `{ "DemarrageAvecWindows": false }`,
sinon le premier lancement d'une version Release inscrit l'exécutable d'essai dans la clé Run (une version Debug n'écrit jamais
cette clé). Dans une version Debug, `PRESSEPAPIERS_ARRET_APRES_MS` demande un arrêt propre au bout de ce délai.

## Développement

```powershell
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Projets : `src\PressePapiers.Coeur` (logique sans WinUI : modèle, stockage, recherche, session, réglages, formats),
`src\PressePapiers` (application WinUI 3), `tests\PressePapiers.Tests` (xUnit v3, sur Coeur seulement).
`scripts\generer-icones.ps1` régénère les icônes de la zone de notification (Windows PowerShell 5.1).

Comportements à connaître :

- Le hook Win+V (`WH_KEYBOARD_LL`) vit sur un fil dédié et se réinstalle seul toutes les 15 minutes ; l'application le
  réinstalle aussi au déverrouillage de la session et à la sortie de veille. Arrêté ou planté, l'outil rend la main à
  l'historique natif de Windows, laissé activé.
- La liste et la recherche ne lisent que des résumés (`ElementResume` : début du texte, miniature des images) ; l'élément
  complet n'est lu qu'à la sélection (aperçu) et au collage. Fenêtre masquée, rien n'est relu et aucune ligne n'est gardée en
  mémoire : la liste est lue une seule fois par ouverture.
- La capture lit le presse-papiers sur le fil d'interface et y enregistre l'élément : copier un texte de plusieurs
  mégaoctets gèle la fenêtre une à deux secondes (indexation plein texte). Le dépôt et la recherche partagent un seul
  verrou, la connexion SQLite : ils restent sûrs si cette écriture passe un jour sur un autre fil.
- Une exception qui sort d'un rappel de `DispatcherQueue` ou d'une minuterie arrête le processus sans passer par
  `UnhandledException` : tout travail posté sur le fil d'interface, tout rappel de minuterie et toute méthode asynchrone lancée
  sans attente passent par `ExecutionProtegee` (`App.PosterSurInterface` pour l'application), qui la note au journal.

## Recette manuelle

À jouer sur le poste après installation (spec, section 14). Une case par vérification.

### Win+V et raccourci de secours

- [ ] Win+V dans plusieurs applications (terminal, éditeur de code, navigateur, Office, Explorateur) : la fenêtre s'ouvre au premier plan, au centre de l'écran de la fenêtre active.
- [ ] Le menu Démarrer ne s'ouvre pas au relâchement de Win après Win+V.
- [ ] Win seul, Win+E et Win+Maj+S fonctionnent toujours.
- [ ] Win+V en gardant Win enfoncée après V : une seule ouverture, fenêtre au premier plan.
- [ ] Win+V tenu (répétition automatique) et double Win+V rapide : une seule ouverture.
- [ ] Raccourci de secours (Ctrl+Alt+Maj+V) depuis une application ordinaire : même ouverture.
- [ ] Changer le raccourci de secours dans les Paramètres, puis essayer une combinaison déjà prise par une autre application : message sous le champ, l'ancien raccourci reste actif.
- [ ] Collage spécial d'Excel (Ctrl+Alt+V) toujours disponible pendant que l'outil tourne.
- [ ] Win+Ctrl+V (sélecteur de sortie audio de Windows) n'est pas intercepté : Windows réagit comme sans l'outil.
- [ ] Win+V pendant qu'une boîte de dialogue de l'outil est ouverte (Nouvel onglet, confirmation) et que la fenêtre est passée derrière une autre application : la fenêtre revient au premier plan, boîte comprise.

### Fenêtre administrateur

- [ ] Devant une fenêtre lancée en administrateur (Gestionnaire des tâches, terminal administrateur) : Win+V ouvre l'historique natif de Windows.
- [ ] Devant cette même fenêtre : le raccourci de secours ouvre-t-il la fenêtre ? (non établi par la documentation ; noter le résultat) ; puis Entrée : pas de collage, notification « Copié, colle avec Ctrl+V ».
- [ ] Essayer aussi une combinaison Ctrl+Maj+lettre comme raccourci de secours devant cette fenêtre.
- [ ] La fenêtre de l'outil s'affiche-t-elle au premier plan quand une application élevée a le focus ?

### Copie et recollage de chaque type

- [ ] Texte depuis un éditeur, recollé dans un autre.
- [ ] Texte mis en forme copié depuis Word : aperçu fidèle (gras, couleurs, tableau) ; recollé en texte seul par Entrée et avec la mise en forme par Maj+Entrée.
- [ ] Texte mis en forme copié depuis Outlook : même contrôle. Un tableau plus large que la colonne d'aperçu est coupé à droite (limite connue).
- [ ] Texte copié depuis une page web (HTML seul) : aperçu en texte brut avec la mention « mise en forme conservée pour le collage », recollé avec la mise en forme dans Word.
- [ ] Image copiée (capture Win+Maj+S, image d'une page web) et recollée dans Word, Excel, PowerPoint, Outlook, un navigateur et Paint.
- [ ] Image avec transparence (PNG copié depuis un navigateur ou un éditeur d'images) : transparence conservée au recollage dans ces mêmes applications ?
- [ ] Fichiers copiés dans l'Explorateur, recollés dans un autre dossier : les fichiers sont copiés.
- [ ] Copie depuis une application du Store (Calculatrice, Photos) : application d'origine reconnue dans la liste.
- [ ] Ctrl+Entrée : copie seulement, message « Copié dans le presse-papiers ».
- [ ] Collage impossible alors que la fenêtre est déjà masquée (par exemple en changeant de fenêtre juste après Entrée, ou en gardant Maj enfoncée) : notification de l'icône « Collage impossible, l'élément est copié : colle avec Ctrl+V », jamais un échec muet.

### Fenêtre d'origine

- [ ] Fenêtre d'origine réduite entre l'ouverture et Entrée : elle est restaurée et reçoit le collage.
- [ ] Fenêtre d'origine fermée entre-temps : copie seulement, message dans la fenêtre.
- [ ] Ouverture depuis l'icône : Entrée copie seulement, message dans la fenêtre.

### Fenêtre et affichage

- [ ] Première ouverture après le démarrage : pas de cadre vide gênant (la fenêtre peint son contenu quelques dizaines de millisecondes après son apparition).
- [ ] Perte du focus : la fenêtre se masque, sauf pendant une boîte de dialogue.
- [ ] La croix masque la fenêtre sans quitter l'application.
- [ ] Taille de la fenêtre changée puis rouverte : taille gardée ; pas de bouton Agrandir ni Réduire, et la fenêtre n'apparaît pas dans Alt+Tab.
- [ ] Plafond abaissé sous le nombre d'éléments de Temporaire (Paramètres) : une confirmation indique combien d'éléments seront supprimés ; Annuler remet l'ancien plafond sans rien supprimer. Les flèches du champ ne proposent la confirmation qu'une fois la valeur choisie.
- [ ] Applications en chasse fixe : taper un nom puis masquer la fenêtre sans quitter la zone : le nom est gardé à la réouverture.
- [ ] Un message de réussite (Copié, Enregistré) se ferme seul après environ 5 secondes ; un message d'erreur reste.
- [ ] Écran à 150 %, puis deux écrans d'échelles différentes : taille et centrage justes sur chaque écran.
- [ ] Thèmes Clair, Sombre et Système : toute la fenêtre suit, boutons de la barre de titre compris.

### Sessions et onglet Temporaire

- [ ] Arrêt puis rallumage avec démarrage rapide : Temporaire vidé.
- [ ] Redémarrage : Temporaire vidé.
- [ ] Fermeture de session puis reconnexion : Temporaire vidé.
- [ ] Win+L puis déverrouillage : Temporaire conservé, Win+V toujours repris.
- [ ] Veille puis reprise : Temporaire conservé, Win+V toujours repris.
- [ ] Reprise d'une connexion Bureau à distance : Temporaire conservé.
- [ ] Relance de l'outil (Quitter puis relancer) : Temporaire conservé.
- [ ] Outil lancé une fois en administrateur : Temporaire conservé.
- [ ] Les éléments nommés survivent à tous ces cas.

### Icône, instance unique, erreurs

- [ ] Redémarrage de l'Explorateur (Gestionnaire des tâches, Redémarrer l'Explorateur Windows) : l'icône réapparaît.
- [ ] Clic sur l'icône : la fenêtre s'ouvre ; clic droit : menu Ouvrir, Paramètres, Quitter, chacun fonctionne.
- [ ] Hook inactif (rare ; sinon à constater si l'icône prend l'aspect d'alerte) : pastille rouge sur l'icône épinglée et raison dans les Paramètres.
- [ ] Double lancement pendant que la fenêtre est masquée : la fenêtre s'ouvre au premier plan, pas de seconde instance.
- [ ] Démarrage avec Windows : après une ouverture de session, l'icône est là (Windows peut retarder les programmes de démarrage de quelques secondes).
- [ ] Une erreur de l'interface (si l'occasion se présente) est notée au journal et l'outil continue.

## Points de la conception encore à vérifier sur le poste

État au 13/10/2026 des points de la section 16 de la spec, après les essais du développement. Ceux-ci restent à vérifier
à la main :

- [ ] **Point 1, heure d'ouverture de session (`LogonTime`)** : identique pour un processus normal et un processus élevé (terminal administrateur) ; inchangée après Win+L, veille et reprise Bureau à distance ; changée après « Arrêter » en démarrage rapide puis rallumage. Mesuré seulement dans un processus normal (deux lectures identiques, même valeur dans un processus neuf).
- [ ] **Point 3, RTF dans l'aperçu** : rendu d'un RTF réellement copié depuis Word et Outlook (essayé seulement sur un RTF écrit à la main).
- [ ] **Point 4, icône** : vrais clics de souris sur l'icône (le menu et l'ouverture n'ont été essayés que par messages postés) ; redémarrage réel de l'Explorateur.
- [ ] **Point 5, raccourci de secours devant une fenêtre administrateur** (Ctrl+Alt+Maj+V et une combinaison Ctrl+Maj+lettre, devant le Gestionnaire des tâches).
- [ ] **Point 6, Win+V devant une fenêtre administrateur** : l'historique natif reprend la main.
- [ ] **Point 7, format PNG à l'écriture** : accepté par Word, Excel, PowerPoint, Outlook, un navigateur et Paint, transparence comprise (la lecture d'un `CF_DIBV5` avec transparence est vérifiée).
- [ ] **Hook Win+V réel** : l'avalement de Win+V par la vraie touche Windows n'a pas pu être simulé (seule l'injection l'a été) ; si Win+V n'est pas repris, c'est le premier point à examiner (lecture de la touche Windows par `GetAsyncKeyState`).
