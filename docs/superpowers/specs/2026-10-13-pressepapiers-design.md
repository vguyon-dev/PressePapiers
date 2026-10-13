# PressePapiers : spécification de conception

**Date :** 13/10/2026
**Statut :** conception validée section par section en conversation le 13/10/2026 ; affirmations techniques contre-vérifiées sur sources primaires le même jour ; spec en attente de relecture.
**Maquette de référence :** `maquettes/2026-10-13-propositions-fenetre.html` (proposition B, rendu WinUI 3).

> **Statut au 13/10/2026, après la construction du plan** (`docs/superpowers/plans/2026-10-13-pressepapiers.md`). La
> conception reste valable ; le code validé du plan la précise ou s'en écarte sur ces points, et c'est lui qui fait foi :
>
> - **Section 5** : le déclencheur `element_au` est `AFTER UPDATE OF texte_recherche` (un changement de date d'usage ne
>   réindexe plus le texte) et la table `element` a deux index (onglet et empreinte ; onglet et date d'usage). Le stockage et
>   la recherche partagent un seul verrou, la connexion. Schéma exact : tâche 5 du plan, `SchemaBase.cs`.
> - **Section 8** : Suppr ne supprime que si la recherche est vide, y compris depuis la liste ; Ctrl+Maj+Entrée copie
>   seulement ; une perte du focus pendant l'ouverture est rattrapée ; la taille mémorisée est en pixels logiques ; le volet
>   réagit à `ItemInvoked` ; la croix passe par `AppWindow.Closing`, et `Window.Close()` n'y passant pas, la fenêtre a une
>   méthode `AutoriserFermeture` pour l'arrêt.
> - **Section 11** : la réinstallation toutes les 15 minutes vit dans le hook lui-même ; l'application relit son état chaque
>   minute (icône, Paramètres) et relance un hook inactif.
> - **Section 12** : le relais est créé avant la fenêtre principale ; en Debug, la variable `PRESSEPAPIERS_ARRET_APRES_MS`
>   arrête l'application pour la recette automatisée ; la publication Release fait 231 Mo.
> - **Section 13** : une exception sortie d'un rappel `DispatcherQueue` ou d'une minuterie arrête le processus sans passer
>   par `UnhandledException` : tout rappel posté au fil d'interface passe par `App.PosterSurInterface`, qui l'attrape et la
>   note. La capture enregistre sur le fil d'interface : un texte de plusieurs mégaoctets gèle la fenêtre environ 2 s
>   (limite connue).
> - **Section 16** : point 2 (premier plan d'une fenêtre jamais activée) et point 8 (SQLite 3.53.3, FTS5 trigram)
>   confirmés ; points 1, 3, 4 et 7 partiellement confirmés ; points 5 et 6 non testés. État détaillé : plan, section
>   « État des points à valider ».

> **Statut au 13/10/2026, après la revue de code** (`docs/superpowers/plans/2026-10-13-pressepapiers/revue.md`). Les
> corrections précisent encore la conception :
>
> - **Section 4** : la lecture rend le texte tout de suite ; au-delà de 2 s, ce texte sert de résultat partiel, aucune
>   nouvelle lecture ne part tant que la lecture en retard tient le presse-papiers, et son résultat est livré par une
>   relecture quand elle finit. L'ordre de décision du type est : `CF_HDROP` (fichiers), puis texte (mis en forme si RTF ou
>   HTML), puis image. Un DIB est refusé au-delà de 64 Mpx, en compression JPEG ou PNG, ou s'il est incohérent.
> - **Section 5** : schéma en version 2 (petites colonnes d'abord ; colonnes calculées `extrait` de 500 caractères,
>   `a_mise_en_forme`, `nombre_fichiers` ; gros contenus en dernier), migré automatiquement depuis la version 1 ; une base
>   d'une version supérieure est refusée. La liste et la recherche lisent des résumés. Les PNG orphelins sont supprimés au
>   démarrage. Une copie de sécurité n'est gardée qu'après `PRAGMA quick_check`. La restauration garde le `-wal` de la base
>   abîmée. Une recopie avec mise en forme enrichit l'élément existant.
> - **Section 8** : changer le plafond demande confirmation avant toute suppression ; la fenêtre n'est ni agrandissable ni
>   réductible et n'apparaît pas dans Alt+Tab ; les messages de réussite se ferment après 5 s ; le raccourci de secours exige
>   au moins deux modificateurs parmi Ctrl, Alt et Maj (avertissement AltGr sur AZERTY) ; Win+V ramène une fenêtre visible
>   restée derrière une autre.
> - **Section 10** : si une touche de modification reste enfoncée plus d'une seconde, Ctrl+V n'est pas envoyé (copie seule,
>   annoncée) ; tout échec survenu fenêtre masquée passe par la notification de l'icône ; 40 ms d'attente et codes de
>   balayage avant Ctrl+V.
> - **Section 11** : seul Win+V sans Ctrl, Alt ni Maj est repris ; les relâchements perdus remettent l'automate à zéro.
> - **Section 12** : la version Debug n'écrit jamais la clé Run ; la version Release ne la réécrit que si le chemin a changé ;
>   une seconde instance qui ne peut pas rediriger démarre à la place de la première si celle-ci a disparu, sinon affiche un
>   message ; `publier.ps1` refuse les destinations dangereuses.

> **Statut au 13/10/2026, après la recette** (`docs/superpowers/plans/2026-10-13-pressepapiers/revue.md`, section 6). Deux
> changements décidés sur le poste, qui l'emportent sur le texte plus bas :
>
> - **Raccourci de secours (sections 8, 11, 14 et 16)** : le défaut est **Win+Alt+V** et non plus Ctrl+Alt+Maj+V, trop long à
>   presser devant une fenêtre administrateur. La touche Windows est acceptée comme modificateur (au moins deux modificateurs
>   parmi Win, Ctrl, Alt et Maj) ; `RegisterHotKey` refusant Win+Maj+V et Win+Ctrl+V (erreur 1409, réservés par Windows),
>   ces combinaisons et Win+V sont refusées d'avance. Un `parametres.json` qui contient exactement l'ancien défaut passe au
>   nouveau au chargement. Le hook laisse déjà passer Win+Alt+V (Alt est un autre modificateur).
> - **Mode administrateur (sections 10.3, 12 et 14)** : en plus du mode normal, `scripts\publier.ps1 -Administrateur`
>   installe l'outil dans `C:\Program Files\PressePapiers` et crée une tâche planifiée qui le lance en administrateur à
>   l'ouverture de session ; Win+V et le collage automatique fonctionnent alors devant les fenêtres administrateur. L'outil
>   reconnaît ce mode à son jeton élevé et à son dossier protégé, ne gère plus la clé Run et remplace l'interrupteur de
>   démarrage par un texte dans les Paramètres. Voir la section « Mode administrateur » du README.

## 1. Objet

Remplacer l'historique du presse-papiers de Windows 11 (Win+V) par un outil personnel plus complet.

| N° | Besoin |
|---|---|
| R1 | Un historique bien plus grand que Win+V (limité à 25 éléments par Windows). |
| R2 | Fiable : l'historique natif finit régulièrement par buguer. |
| R3 | Rechercher dans l'historique. |
| R4 | Enregistrer un élément copié sous un nom choisi, et le garder. |
| R5 | Les éléments sans nom vont dans un onglet temporaire, vidé au redémarrage. |
| R6 | S'ouvrir avec Win+V et recoller dans la fenêtre d'origine. |

Usage : outil pour une seule personne, sur son poste, sans synchronisation entre machines.

## 2. Décisions prises

| Sujet | Décision | Raison |
|---|---|---|
| Coder ou adopter | Coder un outil maison. | Choix de l'utilisateur après comparaison (CopyQ, Ditto, ClipboardFusion) : aucun ne couvre R4 et R5 tels que voulus. |
| Contenus capturés | Texte brut, texte mis en forme (RTF, HTML), images, listes de fichiers. | Réponse de l'utilisateur ; la mise en forme est « en option » au collage. |
| Onglet temporaire | Vidé à chaque nouvelle session Windows (arrêt, redémarrage, déconnexion). Relancer l'outil ne le vide pas. | Réponse de l'utilisateur. |
| Rangement | Un onglet « Temporaire » fixe et des onglets créés par l'utilisateur ; nommer un élément = lui donner un nom et choisir son onglet. | Réponse de l'utilisateur. |
| Raccourci | Win+V obligatoire, plus un raccourci de secours toujours actif (défaut Ctrl+Alt+Maj+V). | Réponse de l'utilisateur. Ctrl+Alt+V, d'abord envisagé, est le Collage spécial d'Excel (et selon des sources tierces de Word et PowerPoint) : un raccourci global l'aurait capté. |
| Protection | Tout enregistrer, sans exclusion. Les formats par lesquels une application demande à rester hors de l'historique (`ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory`, `CanUploadToCloudClipboard`) existent et sont ignorés : l'outil gardera des contenus que Win+V natif exclut, mots de passe compris. | Choix explicite de l'utilisateur, averti que c'est déconseillé. |
| Interface | Proposition B de la maquette : onglets à gauche, liste au milieu, aperçu éditable à droite. A et C en réserve. | Choix de l'utilisateur sur maquette. |
| Architecture | Une seule application, en zone de notification. | Choix de l'utilisateur (approche 1 sur 3). |
| Framework | WinUI 3 (Windows App SDK 2.5.1), application non empaquetée et autonome, C# sur .NET 10. | Choix de l'utilisateur après comparaison de deux vraies fenêtres WPF et WinUI 3 ; la mini-fenêtre WinUI 3 de l'essai a compilé et tourné avec ce couple (net10.0-windows10.0.19041.0, Windows App SDK 2.5.1, non empaquetée, autonome). .NET 8 et 9 sortent du support le 10/11/2026 ; .NET 10 est LTS jusqu'au 14/11/2028. |
| Reprise de Win+V | Hook clavier bas niveau (WH_KEYBOARD_LL), historique natif laissé activé. | Essai jetable concluant sur le poste le 13/10/2026 (section 11). `RegisterHotKey(Win+V)` échoue sur ce poste (erreur 1409) : la documentation réserve les combinaisons avec la touche Windows au système. |
| Dépôt | Git, dépôt GitHub `vguyon-dev/PressePapiers` (public, licence MIT). | Réponse de l'utilisateur. |

## 3. Architecture

Une application WinUI 3 qui démarre avec Windows et vit dans la zone de notification. Trois projets dans une solution :

| Projet | Contenu | Dépend de |
|---|---|---|
| `PressePapiers.Coeur` (bibliothèque, `net10.0-windows10.0.19041.0`) | Modèle, stockage SQLite, recherche, dédoublonnage, détection de session, lecture et écriture des formats du presse-papiers (texte, RTF, `HTML Format`, DIB vers PNG par WIC, liste de fichiers). Aucune dépendance à WinUI. | Microsoft.Data.Sqlite |
| `PressePapiers` (application WinUI 3) | Relais Windows, hook Win+V, premier plan, capture, recollage, fenêtre, icône de notification, paramètres, démarrage. | Coeur, Windows App SDK |
| `PressePapiers.Tests` (xUnit) | Tests de la logique de Coeur. | Coeur |

Séparer `Coeur` permet de tester toute la logique sans interface ni WinUI.

### Briques et responsabilités

| Brique | Rôle | Ce qu'elle expose |
|---|---|---|
| **Relais Windows** | Une fenêtre Win32 de premier niveau, jamais affichée, créée sur le fil d'interface. Pas une fenêtre « message-only » : celles-ci ne reçoivent pas les messages diffusés, donc pas `TaskbarCreated`. Elle reçoit chaque copie (`AddClipboardFormatListener`, `WM_CLIPBOARDUPDATE`), le raccourci de secours (`RegisterHotKey`, `WM_HOTKEY`), les messages de l'icône de notification (`Shell_NotifyIcon`, WinUI 3 n'ayant pas d'icône intégrée) et `TaskbarCreated`, à la réception duquel l'icône est recréée. Le menu de l'icône est un menu Win32 natif (`TrackPopupMenu`, précédé de `SetForegroundWindow` sur le relais). | Événements `CopieDetectee`, `RaccourciSecours`, `ClicIcone`, `CommandeMenu`. |
| **Hook Win+V** | Fil d'exécution dédié avec boucle de messages ; avale Win+V, note la fenêtre active et poste un message vers le `DispatcherQueue` de l'interface. Isolé derrière une interface pour pouvoir être remplacé. | Événement `WinVPresse(fenetreActive)` ; propriété `EstActif` et raison d'échec. |
| **Premier plan** | Une classe unique qui amène une fenêtre au premier plan (section 10.1), utilisée pour la fenêtre B et pour le retour à la fenêtre d'origine. | `Amener(hwnd)` : réussi ou non. |
| **Capture** | Lit le presse-papiers à chaque copie, avec réessais ; extrait les formats ; ignore les écritures de l'outil lui-même. | `Element` prêt à enregistrer, ou rien. |
| **Stockage** (Coeur) | Base SQLite et dossier d'images ; ajout avec dédoublonnage, nommer, déplacer, supprimer, onglets, purge. | Dépôt d'éléments et d'onglets. |
| **Recherche** (Coeur) | Normalisation (casse, accents) et requête plein texte. | Liste d'éléments correspondants. |
| **Détecteur de session** (Coeur) | Dit si l'on est dans une nouvelle session Windows depuis le dernier lancement (section 6). | Nouvelle session, même session, ou indéterminé. |
| **Fenêtre B** | Interface de la proposition B ; créée au démarrage puis masquée. | Afficher, masquer, élément choisi. |
| **Recollage** | Remet l'élément dans le presse-papiers et le colle dans la fenêtre d'origine. | `Coller(element, mode)`. |
| **Paramètres** | Lecture et écriture des réglages (`parametres.json` dans le dossier des données). | Réglages typés. |
| **Journal** | Journal fichier des erreurs et événements notables. | Interface de journalisation. |

Règle de fils : la fenêtre relais vit sur le fil d'interface, qui pompe les messages ; le fil du hook ne touche jamais l'interface et passe par le `DispatcherQueue`.

### Trajet d'une copie

1. Ctrl+C dans une application : Windows envoie `WM_CLIPBOARDUPDATE` au relais ; la fenêtre au premier plan est relevée aussitôt (application d'origine, section 4).
2. La capture ouvre le presse-papiers (réessais), vérifie l'absence de la marque interne, extrait les formats.
3. Le stockage calcule l'empreinte : si un élément identique existe déjà dans Temporaire, il remonte en tête ; sinon un nouvel élément est créé en tête de Temporaire.
4. Si la fenêtre B est ouverte, sa liste se met à jour.

### Trajet d'un collage

1. Win+V : le hook avale la touche, note la fenêtre active (`GetForegroundWindow`) dans son rappel et poste `WinVPresse`.
2. L'interface affiche la fenêtre B sur l'écran de la fenêtre notée et l'amène au premier plan (brique Premier plan), recherche prête.
3. Entrée : le recollage s'exécute (section 10), la fenêtre se masque.

## 4. Capture et formats

| Format lu (dans cet ordre de préférence) | Ce qui est stocké | Recollé comme |
|---|---|---|
| `CF_UNICODETEXT` | Le texte. | `CF_UNICODETEXT`. |
| `Rich Text Format` (nom exact du format enregistré) | Le RTF brut, plus le texte brut. | Texte seul par défaut ; avec RTF selon le réglage ou Maj+Entrée. |
| `HTML Format` | Les octets bruts, plus le texte brut. | Comme le RTF. |
| `CF_DIBV5`, sinon `CF_DIB` | Une image PNG dans `images\`, une miniature (160 px de large) en base. | `CF_DIB` et le format enregistré `PNG`. |
| `CF_HDROP` | La liste des chemins. | `CF_HDROP` : l'Explorateur copie les fichiers. |

Règles :

- **Lecture** : jusqu'à 10 essais espacés de 25 ms si une autre application tient le presse-papiers ; au-delà, la copie est ignorée et notée au journal. Les données sont copiées immédiatement puis le presse-papiers est refermé ; les tailles sont validées. Un propriétaire en rendu différé qui ne répond pas ne doit pas bloquer l'outil : délai maximal par lecture, puis abandon noté.
- **Formats enregistrés** : le retour de `RegisterClipboardFormat` est vérifié (zéro = échec, noté au journal).
- **Images** : `CF_DIBV5` est lu avant `CF_DIB`, car Windows fournit `CF_DIB` par conversion et l'alpha y est perdu. Conversion en PNG par WIC (`BitmapDecoder` de `Windows.Graphics.Imaging`) après ajout d'un en-tête `BITMAPFILEHEADER` de 14 octets devant le DIB : construction du format BMP, à valider par des tests. La transparence n'est conservée que pour un `CF_DIBV5` avec masque alpha ; l'alpha peut y être prémultiplié (à vérifier par test). À l'écriture, le nom de format `PNG` n'est pas documenté par Microsoft (usage constaté par des sources anciennes) : sa prise en charge se vérifie en recette dans Word, Excel, PowerPoint, Outlook, un navigateur et Paint ; aucune transparence n'est promise avant ce test.
- **HTML Format** : texte UTF-8 ; en-tête `Version:1.0`, `StartHTML`, `EndHTML`, `StartFragment`, `EndFragment` (lignes terminées par CRLF) ; offsets en octets UTF-8 depuis le début du texte, en-tête compris ; fragment encadré par `<!--StartFragment-->` et `<!--EndFragment-->`. Tests avec accents et emojis (4 octets).
- **Marque interne** : chaque écriture de l'outil porte un format privé enregistré ; une copie qui le porte est ignorée.
- **Pas d'exclusion de contenu** (décision de l'utilisateur, section 2).
- **Application d'origine** : indication au mieux, jamais une certitude. Processus de la fenêtre au premier plan relevée à la réception de `WM_CLIPBOARDUPDATE` ; pour `ApplicationFrameHost.exe` (applications UWP), chercher la fenêtre enfant de l'application ; `GetClipboardOwner` seulement en repli ; « inconnue » si rien n'est déterminé.
- **Texte très volumineux** : au-delà de 10 Mo, la copie est ignorée et notée.

**Dédoublonnage :** empreinte SHA-256 du contenu principal (texte, octets de l'image, liste des chemins). Un élément identique déjà présent dans Temporaire remonte en tête (date de dernier usage et application d'origine mises à jour) ; le dédoublonnage ne s'applique qu'à Temporaire.

## 5. Stockage

Emplacement : `%LOCALAPPDATA%\PressePapiers\` contenant `historique.db`, `images\`, `sauvegardes\`, `journal\`, `parametres.json`.

Schéma (SQLite, mode WAL, une seule connexion d'écriture) :

```sql
CREATE TABLE onglet (
  id              INTEGER PRIMARY KEY,
  nom             TEXT NOT NULL,
  ordre           INTEGER NOT NULL,
  est_temporaire  INTEGER NOT NULL DEFAULT 0   -- un seul onglet temporaire, non supprimable
);

CREATE TABLE element (
  id              INTEGER PRIMARY KEY,
  onglet_id       INTEGER NOT NULL REFERENCES onglet(id),
  nom             TEXT,                        -- NULL tant que l'élément est temporaire
  type            INTEGER NOT NULL,            -- 1 texte, 2 mis en forme, 3 image, 4 fichiers
  texte           TEXT,                        -- texte brut, ou chemins séparés par des retours à la ligne
  texte_recherche TEXT NOT NULL,               -- nom + texte + chemins + application, normalisés (section 7)
  rtf             BLOB,
  html            BLOB,
  image_fichier   TEXT,                        -- nom du PNG dans images\
  miniature       BLOB,
  empreinte       BLOB NOT NULL,
  application     TEXT,
  date_copie      TEXT NOT NULL,
  date_usage      TEXT NOT NULL,
  taille          INTEGER NOT NULL
);

CREATE VIRTUAL TABLE element_fts USING fts5(
  texte_recherche, content='element', content_rowid='id', tokenize='trigram'
);

CREATE TRIGGER element_ai AFTER INSERT ON element BEGIN
  INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
END;
CREATE TRIGGER element_ad AFTER DELETE ON element BEGIN
  INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
END;
CREATE TRIGGER element_au AFTER UPDATE ON element BEGIN
  INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
  INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
END;

CREATE TABLE meta (cle TEXT PRIMARY KEY, valeur TEXT);  -- repère de session, version du schéma
```

- `INSERT OR REPLACE` est interdit sur `element` : ses suppressions implicites ne déclenchent pas `element_ad` (sauf `recursive_triggers`) et désynchroniseraient l'index. Une commande de réparation exécute `INSERT INTO element_fts(element_fts) VALUES ('rebuild')`.
- Au démarrage : contrôle `sqlite_version()` >= 3.34.0 (tokenizer `trigram`) et présence de FTS5 ; sinon message clair et arrêt propre.
- Ordre dans un onglet : `date_usage` décroissante (le dernier utilisé en haut).
- Supprimer un élément supprime aussi son PNG.
- **Copie de sécurité** : au démarrage, avant d'ouvrir la connexion d'écriture, copie par `SqliteConnection.BackupDatabase` dans `sauvegardes\`, les 3 dernières gardées. Jamais de copie manuelle du fichier : le fichier `-wal` fait partie de l'état de la base. Une base illisible au démarrage déclenche une proposition de restauration depuis la plus récente ; une base restaurée contient un ancien repère de session, donc Temporaire est purgé au lancement suivant (acceptable).
- Version du schéma dans `meta` pour les migrations futures.

## 6. Onglet temporaire et sessions

**Détection d'une nouvelle session**, au démarrage de l'outil, par un repère unique enregistré dans `meta` : l'**heure d'ouverture de la session Windows en cours**.

- Lecture : `WTSQuerySessionInformationW(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTSSessionInfo)`, champ `LogonTime` de `WTSINFOW`, gardé tel quel (entier 64 bits de type FILETIME), comparé en égalité exacte.
- Ce repère appartient à la session de terminal, pas au jeton du processus : il est le même que l'outil soit lancé normalement ou en administrateur. L'identifiant de session de connexion (LUID `AuthenticationId`) a été écarté : avec le contrôle de compte d'utilisateur, un administrateur a deux sessions de connexion liées aux LUID différents (mesuré sur le poste), et un lancement en administrateur aurait vidé Temporaire à tort.
- Il change à chaque ouverture de session : après un « Arrêter » en démarrage rapide (qui ferme les sessions utilisateur, selon Microsoft), un redémarrage, une fermeture de session. Il ne doit pas changer au verrouillage, à la veille, ni à la reprise d'une connexion Bureau à distance (non écrit par Microsoft, vérifié en recette).
- Aucune heure de démarrage du système n'est utilisée : inchangée par le démarrage rapide, sensible aux pas d'horloge, redondante.

Règles :

| Situation | Conduite |
|---|---|
| Aucun repère enregistré (premier lancement) | Rien n'est supprimé ; le repère est enregistré. |
| Repère différent | Nouvelle session : les éléments de Temporaire et leurs images sont supprimés, puis le repère est mis à jour. |
| Repère identique | Même session : rien n'est supprimé. |
| Lecture impossible ou valeur nulle (par exemple service Bureau à distance arrêté) | Aucune purge ; avertissement au journal ; nouvel essai au lancement suivant. |

La purge a lieu avant le démarrage du relais Windows, pour ne jamais effacer une copie faite juste après l'ouverture de session.

**Plafond de sécurité** : au-delà de 5 000 éléments dans Temporaire, les plus anciens (par `date_usage`) sont supprimés. Réglable, désactivable.

**Éléments nommés :** nommer = donner un nom et choisir un onglet autre que Temporaire ; l'élément quitte Temporaire et est conservé sans limite de durée. Renommer, changer d'onglet, supprimer à tout moment.

**Onglets :** créer, renommer, réordonner, supprimer. Supprimer un onglet non vide demande confirmation et supprime ses éléments. L'onglet Temporaire ne se renomme pas et ne se supprime pas.

## 7. Recherche

- Porte sur le nom, le texte, les chemins de fichiers et l'application d'origine, via `texte_recherche`.
- **Normalisation** : une fonction unique, partagée entre l'écriture de `texte_recherche` et la requête : minuscules, accents retirés par décomposition Unicode, ligatures ramenées (œ en oe, æ en ae). L'option `remove_diacritics` du tokenizer n'est pas utilisée.
- La requête normalisée est découpée en mots ; tous les mots doivent être présents.
- Mots de 3 caractères et plus : `MATCH` sur l'index `trigram`, chaque mot entre guillemets doubles (guillemets internes doublés), pour que AND, OR, NOT, `*` ou `:` tapés par l'utilisateur ne soient jamais lus comme syntaxe FTS5.
- Mots de 1 ou 2 caractères (l'index n'en trouve aucun) : repli sur `LIKE` avec clause `ESCAPE` et échappement de `%` et `_` ; ce repli parcourt la table, acceptable pour des mots aussi courts.
- La recherche parcourt tous les onglets ; les résultats indiquent leur onglet.
- Une image se retrouve par son nom, son application d'origine et sa date ; lire le texte des images est en réserve.

## 8. Fenêtre B (WinUI 3)

Disposition (référence : maquette, rendu WinUI 3) :

- Fond `MicaBackdrop` (fond uni si `MicaController.IsSupported()` est faux), barre de titre fondue (`ExtendsContentIntoTitleBar`) avec icône, « Presse-papiers » et badge « Win+V ».
- `NavigationView` à gauche (volet de 200 px) : Temporaire, onglets de l'utilisateur avec compteurs, « Nouvel onglet », entrée Paramètres en bas.
- Zone de contenu en couche arrondie : à gauche `AutoSuggestBox` de recherche, légende de l'onglet Temporaire, `ListView` des éléments (icône de type, nom en gras s'il existe, aperçu sur deux lignes, application et heure) ; à droite l'aperçu.
- Aperçu : `CommandBar` (Coller, Texte brut ou Avec mise en forme, Nommer, Supprimer), champs Nom et Onglet avec bouton Enregistrer, contenu complet, application et date de copie.
  - Texte : police à chasse fixe pour les applications d'une liste réglable (terminaux, éditeurs de code).
  - Mis en forme avec RTF : rendu aussi fidèle que le permet `RichEditBox` en lecture seule (à valider, section 16). Chargement : `IsReadOnly` passé à faux, `Document.SetText(TextSetOptions.FormatRtf, ...)`, puis `IsReadOnly` remis à vrai (try/finally). Si le rendu est mauvais, ou s'il n'y a que du HTML (pages web) : aperçu en texte brut avec la mention « mise en forme conservée pour le collage ».
  - Image : en grand ; fichiers : liste des chemins.
- Messages (confirmations, problèmes) : `InfoBar` en haut de la zone de contenu.

**Cycle de vie de la fenêtre :**

- `Application.DispatcherShutdownMode` réglé sur `OnExplicitShutdown` : masquer la fenêtre ne termine pas l'application.
- La croix et `AppWindow.Closing` sont interceptés (`Cancel = true`, puis masquage). Quitter (menu de l'icône) lève un drapeau, retire l'icône (`NIM_DELETE`) puis appelle `Application.Exit`.
- Affichage : `AppWindow.Show()` puis brique Premier plan ; masquage : `AppWindow.Hide()`. La fenêtre est créée au démarrage sans être affichée ; si WinUI 3 exige une première activation, elle est affichée sans activation (`Show(false)`) puis masquée aussitôt (section 16).

**Ouverture :** sur Win+V ou le raccourci de secours, au centre de l'écran de la fenêtre active ; onglet Temporaire, premier élément sélectionné, curseur dans la recherche. Taille réglable et mémorisée. Un second signal reçu moins de 300 ms après le premier, ou pendant que la fenêtre est visible ou en cours d'ouverture, est ignoré (ouverture idempotente).

**Fermeture :** après un collage ; Échap (efface d'abord la recherche, puis masque) ; perte du focus ; la croix masque aussi. La perte du focus est ignorée pendant une séquence de recollage et tant qu'un `ContentDialog` est ouvert. Quitter se fait par l'icône de notification.

**Clavier :**

| Touche | Action |
|---|---|
| saisie | rechercher dans tout l'historique |
| ↑ ↓ | choisir un élément |
| Entrée, double-clic | coller dans la fenêtre d'origine |
| Maj+Entrée | coller dans l'autre mode de mise en forme |
| Ctrl+Entrée | copier seulement, sans coller |
| F2 | aller au champ Nom |
| Entrée dans le champ Nom | enregistrer (nom et onglet requis) |
| Suppr (recherche vide) | supprimer l'élément |
| Ctrl+Tab, Ctrl+Maj+Tab | onglet suivant, précédent |
| Ctrl+1 à Ctrl+9 | aller à l'onglet n |
| Échap | effacer la recherche, puis masquer |

Tab garde son rôle normal (passer d'un contrôle à l'autre).

**Paramètres :** raccourci de secours (défaut Ctrl+Alt+Maj+V ; si l'enregistrement échoue parce que la combinaison est prise, message dans les Paramètres) ; démarrage avec Windows ; mise en forme par défaut au collage (défaut : texte brut) ; plafond de Temporaire ; thème (Système, Clair, Sombre) ; applications affichées en chasse fixe ; état du hook Win+V ; boutons « Ouvrir le dossier des données » et « Ouvrir le journal ».

**Icône de notification :** clic pour ouvrir ; clic droit : menu natif Ouvrir, Paramètres, Quitter. Aspect d'alerte quand le hook Win+V n'est pas actif.

## 9. Interactions avec les éléments

- **Nommer** : F2 ou bouton Nommer, saisie du nom, choix de l'onglet, Enregistrer ; un élément temporaire quitte Temporaire ; un élément nommé peut être renommé ou déplacé.
- **Supprimer** : Suppr ou bouton ; sans confirmation pour un élément temporaire, avec confirmation pour un élément nommé.
- **Nouvel onglet** : `ContentDialog` avec le nom.
- Tout `ContentDialog` prend pour `XamlRoot` la racine du contenu de la fenêtre B, n'est ouvert que fenêtre affichée, et un seul à la fois.
- Après un collage, l'élément remonte en tête de son onglet (`date_usage`).

## 10. Premier plan et recollage

### 10.1 Amener une fenêtre au premier plan

Windows ne laisse pas un processus d'arrière-plan prendre le premier plan : un hook clavier ne donne pas ce droit (aucune documentation ne le dit, et PowerToys, CopyQ et Ditto ne s'y fient pas). La brique Premier plan applique, dans l'ordre :

1. Si la fenêtre est réduite, la restaurer ; l'afficher.
2. `SendInput` d'une entrée souris entièrement à zéro (événement vide), qui fait de l'outil le dernier fournisseur d'entrée, puis `SetForegroundWindow(hwnd)`, puis contrôle `GetForegroundWindow() == hwnd`. Technique non documentée par Microsoft, utilisée en production par PowerToys (`WindowHelpers.BringToForeground`, licence MIT, y compris dans son outil WinUI 3 Advanced Paste).
3. En cas d'échec : `AttachThreadInput` entre le fil de la fenêtre et celui du premier plan, `SetForegroundWindow`, puis détachement (même recette dans PowerToys, CopyQ et Ditto).
4. Si l'échec persiste : notification de l'icône, jamais un échec muet.

Le réglage global `ForegroundLockTimeout` de l'utilisateur n'est jamais modifié. Après un `WM_HOTKEY` (raccourci de secours), Windows accorde en principe le droit de premier plan au processus qui reçoit le raccourci (billet de Raymond Chen, Microsoft) : l'appel se fait pendant le traitement du message, la même séquence restant le filet.

### 10.2 Recollage

1. La fenêtre active a été notée au moment de Win+V (dans le rappel du hook) ou du raccourci de secours. Elle est écartée si c'est la fenêtre de l'outil lui-même (double Win+V), le bureau ou la barre des tâches. Ouverte depuis l'icône de notification, la fenêtre B n'a pas de fenêtre d'origine fiable (la barre des tâches a pris le premier plan) : Entrée y copie seulement, et l'`InfoBar` le dit.
2. Au choix d'un élément, un drapeau « recollage en cours » suspend la fermeture sur perte du focus, puis :
   - l'élément est écrit dans le presse-papiers avec la marque interne, dans le mode choisi (texte seul, ou avec RTF et HTML) ;
   - si la fenêtre notée n'existe plus (`IsWindow` faux), l'outil s'arrête là : copie seulement, message dans l'`InfoBar` ;
   - le niveau d'intégrité de la fenêtre notée est contrôlé (section 10.3) ;
   - la brique Premier plan rend le premier plan à la fenêtre notée (restaurée si elle est réduite), tant que la fenêtre B est encore active, puis la fenêtre B se masque ;
   - l'outil attend le relâchement de Win, Maj, Ctrl et Alt (`GetAsyncKeyState`, une seconde au plus) ;
   - `SendInput` envoie Ctrl+V.
3. **Ctrl+Entrée :** seulement l'écriture dans le presse-papiers, sans collage.

### 10.3 Fenêtre administrateur

Windows bloque `SendInput` vers une fenêtre de niveau d'intégrité supérieur, sans le signaler (ni valeur de retour, ni `GetLastError`). Avant d'envoyer Ctrl+V : `GetWindowThreadProcessId`, `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`, `OpenProcessToken(TOKEN_QUERY)`, `GetTokenInformation(TokenIntegrityLevel)`, puis comparaison du dernier sous-identifiant du SID avec celui de l'outil, lu de la même façon. Essai sur le poste : la lecture réussit sur un processus élevé du même utilisateur (niveau 0x3000 contre 0x2000). Si l'une des étapes échoue, la cible est traitée comme plus élevée, jamais comme de même niveau. Cible plus élevée : pas de Ctrl+V, l'élément reste dans le presse-papiers, notification « Copié, colle avec Ctrl+V » (la fenêtre étant masquée).

## 11. Win+V et raccourci de secours

**Essai du 13/10/2026 (programme jetable, poste de l'utilisateur, Windows 11 build 26300, historique natif activé) :** 36 Win+V interceptés en une minute dans cinq applications (Windows Terminal, Claude, Visual Studio Code, deux fenêtres WPF et WinUI 3) ; l'historique natif ne s'est jamais ouvert ; le menu Démarrer ne s'est jamais ouvert au relâchement de Win. Non testé : une fenêtre lancée en administrateur.

Technique retenue (celle de l'essai, complétée par la contre-vérification) :

- `SetWindowsHookEx(WH_KEYBOARD_LL)` sur un fil dédié avec boucle de messages. Le délégué du rappel est gardé dans un champ (sinon le ramasse-miettes le libère).
- La procédure est une méthode statique unique, avec un état statique partagé (touche Windows enfoncée, V avalé).
- V enfoncée pendant que Win est enfoncée : contrôle de la touche réelle par `GetAsyncKeyState(VK_LWIN)` et `GetAsyncKeyState(VK_RWIN)` ; si Win est bien enfoncée, la frappe est avalée (valeur de retour non nulle, sans `CallNextHookEx`), à l'enfoncement comme au relâchement ; la répétition automatique ne déclenche qu'une ouverture. L'état « Win enfoncée » est remis à zéro au déverrouillage, à la sortie de veille et à chaque réinstallation : le hook ne voit pas les relâchements survenus devant une fenêtre élevée, une invite de contrôle de compte ou l'écran de verrouillage.
- Au relâchement de Win après un Win+V avalé : envoi immédiat d'une touche sans effet (code virtuel `0xE8`) avant de laisser passer le relâchement, pour que Windows n'ouvre pas le menu Démarrer.
- Les frappes envoyées par l'outil lui-même (drapeau `LLKHF_INJECTED` et `dwExtraInfo` égal à la signature de l'outil) sont ignorées. Les autres frappes injectées sont traitées comme de vraies frappes : certains logiciels de clavier (Razer Synapse) réinjectent toutes les touches réelles.
- Le rappel ne fait que décider, envoyer au besoin la touche sans effet et poster un message : Windows retire silencieusement un hook qui dépasse `LowLevelHooksTimeout` (une seconde au plus depuis Windows 10 1709), sans aucun moyen de le savoir.
- **Réinstallation** à la sortie de veille, au déverrouillage de session et toutes les 15 minutes, sur le fil du hook, seulement quand aucune touche Windows, Ctrl, Alt ou Maj n'est enfoncée (sinon report de quelques centaines de millisecondes). Le nouveau hook est posé avant le retrait de l'ancien ; posé en tête de chaîne, il avale la frappe avant l'ancien, qui ne la reçoit donc pas.
- **Fenêtres administrateur :** un processus non élevé ne reçoit pas leurs frappes par un hook bas niveau (documentation Microsoft sur UIAccess, cas décrit pour PowerToys). L'historique natif, laissé activé, devrait alors reprendre la main (à vérifier, section 16). La seule parade complète serait UIAccess ou l'élévation, incompatibles avec une installation sans droits administrateur : limite assumée. L'historique natif reprend aussi la main si l'outil est arrêté ; l'utilisateur peut le désactiver lui-même.
- **Raccourci de secours** toujours enregistré (`RegisterHotKey`, `MOD_NOREPEAT`, défaut Ctrl+Alt+Maj+V, réglable, sans touche Windows). Son fonctionnement devant une fenêtre administrateur n'est établi par aucune source primaire : à tester (section 16).
- **Diagnostic** dans les Paramètres et sur l'icône : actif, ou inactif avec la raison.

## 12. Démarrage, installation, instance unique

- Application non empaquetée (`WindowsPackageType` `None`) et autonome (`WindowsAppSDKSelfContained`), x64. La publication produit un dossier, copié par `scripts\publier.ps1` dans `%LOCALAPPDATA%\Programs\PressePapiers`, sans droits administrateur. Le fichier unique (`PublishSingleFile`) est possible dans cette configuration mais s'extrait dans un dossier temporaire au premier lancement : écarté. Pas de rognage (`PublishTrimmed`) ni de NativeAOT (plantages connus avec WinUI). Taille du dossier à relever au premier build.
- Démarrage avec Windows : valeur dans `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (réglable), donc lancement non élevé. Windows peut retarder les programmes de démarrage de quelques secondes : une copie faite juste après l'ouverture de session peut échapper à l'outil.
- **Instance unique** : `Main` personnalisé (symbole `DISABLE_XAML_GENERATED_MAIN`, `[STAThread]`, `WinRT.ComWrappersSupport.InitializeComWrappers()`) qui appelle `AppInstance.FindOrRegisterForKey` avant toute création de fenêtre, après l'initialisation automatique du Windows App SDK. Si l'instance n'est pas la principale, `RedirectActivationToAsync` est lancé dans une tâche et attendu sans bloquer le fil STA, puis le processus se termine. L'instance principale reçoit l'événement `Activated`, revient sur le fil d'interface, affiche sa fenêtre B et l'amène elle-même au premier plan.
- Ordre au démarrage : instance unique ; copie de sécurité de la base ; ouverture de la base et contrôle de version SQLite ; détection de session et purge éventuelle ; création de la fenêtre B masquée ; relais Windows (écoute du presse-papiers, raccourci de secours, icône) ; hook.

## 13. Erreurs et journal

- Journal fichier dans `journal\` (un fichier par jour, 14 jours gardés).
- Exceptions non gérées (WinUI `UnhandledException`, `AppDomain`, `TaskScheduler.UnobservedTaskException`) : notées ; l'outil continue chaque fois que c'est possible.
- Une copie illisible est ignorée et notée, sans gêner les suivantes.
- En cas de plantage, le hook disparaît avec le processus : Win+V natif reprend la main. L'icône peut rester affichée jusqu'au survol de la souris (comportement de Windows).

## 14. Tests

**Automatiques (xUnit, sur `PressePapiers.Coeur`) :**

- Stockage : ajout, dédoublonnage dans Temporaire, nommer et déplacer, supprimer (avec le PNG), suppression d'onglet, ordre par `date_usage`, plafond ; synchronisation de l'index FTS après insertion, mise à jour (renommage) et suppression ; commande `rebuild`.
- Recherche : casse, accents et ligatures (é, É, e, œ), plusieurs mots, mots de 1 et 2 caractères, caractères spéciaux (`"`, `*`, `:`, AND, OR, NOT, `%`, `_`), résultats sur tous les onglets.
- Détection de session : repère identique (pas de purge), différent (purge puis mise à jour), absent (premier lancement, rien supprimé, repère enregistré), lecture impossible ou valeur nulle (aucune purge, avertissement).
- Purge de Temporaire avec ses images.
- Formats : lecture et écriture de `HTML Format` (offsets en octets, UTF-8, accents, emojis), conversion DIB et DIBV5 vers PNG (avec et sans alpha), liste de fichiers.
- Copie de sécurité : rotation des 3 dernières.

**Recette manuelle (liste de cases dans le README) :**

- Win+V dans plusieurs applications ; menu Démarrer non ouvert ; Win seul, Win+E et Win+Maj+S fonctionnent ; fenêtre B ouverte au premier plan depuis une application tierce, y compris en gardant Win enfoncée après V.
- Fenêtre administrateur (par exemple Gestionnaire des tâches) : Win+V natif reprend la main ; raccourci de secours puis Entrée : notification « Copié, colle avec Ctrl+V ».
- Copie puis recollage de chaque type : texte, mis en forme (Word, Outlook, page web), image (Word, Excel, PowerPoint, Outlook, navigateur, Paint, transparence comprise), fichiers dans l'Explorateur.
- Fenêtre d'origine réduite, fermée entre-temps, élevée : comportement prévu à la section 10.
- Collage spécial d'Excel (Ctrl+Alt+V) toujours disponible pendant que l'outil tourne.
- Sessions : arrêt puis rallumage avec démarrage rapide, Temporaire vidé ; redémarrage, vidé ; fermeture de session, vidé ; Win+L, conservé ; veille, conservé ; reprise d'une connexion Bureau à distance, conservé ; relance de l'outil, conservé ; outil lancé une fois en administrateur, conservé.
- Redémarrage de l'Explorateur : l'icône réapparaît.
- Double lancement pendant que la fenêtre est masquée : la fenêtre s'ouvre au premier plan, pas de seconde instance.

Revue de code à la fin de chaque phase du plan, et revue finale.

## 15. Conventions de code

- Commentaires en français, centrés sur l'intention ; documentation XML sur les API publiques.
- Fichiers courts et à responsabilité unique ; noms en français.
- Optimisation équilibrée : pas de sur-ingénierie, mais libérer les ressources (handles du presse-papiers, hook, icône) et ne pas refaire de travail inutile.

## 16. Points à valider en début de développement

Chacun se vérifie par un test court sur le poste, avant de construire dessus :

1. `LogonTime` (section 6) : identique pour un processus normal et un processus élevé de la même session ; inchangé après Win+L, veille et reprise d'une connexion Bureau à distance ; changé après « Arrêter » en démarrage rapide puis rallumage.
2. Premier plan (section 10.1) : la fenêtre B, jamais activée auparavant, s'affiche au premier plan sans clignotement depuis une application tierce ; besoin éventuel d'un premier `Show(false)` puis `Hide`.
3. `RichEditBox` affiche correctement un RTF copié depuis Word et Outlook.
4. Icône de notification par `Shell_NotifyIcon` depuis le processus WinUI 3, menu natif et recréation sur `TaskbarCreated`.
5. `RegisterHotKey` déclenché devant une fenêtre administrateur (Ctrl+Alt+Maj+V, une combinaison Ctrl+Maj+lettre, devant le Gestionnaire des tâches).
6. Win+V dans une fenêtre administrateur : l'historique natif reprend la main.
7. Conversion `CF_DIBV5` vers PNG avec transparence, et acceptation du format `PNG` à l'écriture par Office, un navigateur et Paint.
8. Version de SQLite du bundle de Microsoft.Data.Sqlite >= 3.34.0 et création d'une table FTS5 `trigram`.

## 17. Hors périmètre, en réserve

- Propositions A (popup compacte) et C (recherche d'abord) de la maquette.
- Lecture du texte des images (OCR de Windows) pour la recherche.
- Suspendre la capture depuis l'icône.
- Exclusion d'applications ou des contenus marqués « ne pas historiser ».
- Chiffrement de la base.
- Collage par Maj+Inser pour certaines applications (consoles).
- Export et import des éléments nommés.
- Synchronisation entre postes.
- UIAccess (interception de Win+V devant les fenêtres administrateur) : exige une signature et une installation sous Program Files.
