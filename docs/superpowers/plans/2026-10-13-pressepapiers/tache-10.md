### Tâche 10 : Parametres, ThemeApplication et ServiceParametres

**Fichiers :**
- Modifier : `src/PressePapiers.Coeur/Reglages/ServiceParametres.cs` (le plan donne le contenu complet du fichier après modification)
- Créer : aucun (`Parametres.cs` et `ThemeApplication.cs` sont déjà complets depuis la tâche 1 et ne changent pas)
- Tests : `tests/PressePapiers.Tests/Reglages/ServiceParametresTests.cs` (créé)

**Interfaces :**
- Utilise (tâche 1) :
  - `public enum ThemeApplication { Systeme, Clair, Sombre }` ;
  - `public sealed record Parametres` : `string RaccourciSecours = "Ctrl+Alt+Maj+V"`, `bool DemarrageAvecWindows = true`,
    `ModeCollage MiseEnFormeParDefaut = ModeCollage.TexteSeul`, `int PlafondTemporaire = 5000`,
    `ThemeApplication Theme = Systeme`, `IReadOnlyList<string> ApplicationsChasseFixe` (neuf applications par défaut),
    `int LargeurFenetre = 920`, `int HauteurFenetre = 560` ;
  - `IJournal` (`Info`, `Avertissement`, `Erreur`), `ModeCollage`.
- Fournit :
  - `public ServiceParametres(string cheminFichier, IJournal journal)` (lève `ArgumentException` si le chemin est vide,
    `ArgumentNullException` si le journal est nul) ;
  - `public Parametres Charger()` : défauts si le fichier est absent (message `Info`, premier lancement normal) ou illisible
    (JSON invalide, vide, `null`, type ou énumération erronés, erreur d'entrée-sortie : `Avertissement` avec l'exception) ;
    une valeur lisible mais inutilisable (texte nul, liste nulle, plafond négatif, fenêtre sous 400 x 300) est remplacée par son
    défaut, avec un `Avertissement`, sans perdre le reste ; le fichier toléré à la main : casse des noms libre, commentaires,
    virgule finale, propriétés inconnues, marque d'ordre des octets ;
  - `public void Enregistrer(Parametres parametres)` : JSON indenté, énumérations en texte, UTF-8 sans marque, écrit dans
    `<fichier>.tmp` puis `File.Move(..., overwrite: true)` ; crée le dossier manquant ; supprime le `.tmp` si l'écriture échoue
    puis relance l'exception.
  - Consommateurs : tâche 16 et 29 (`App` charge au démarrage), tâche 25 (taille de la fenêtre), tâche 28 (page Paramètres).

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Reglages/ServiceParametresTests.cs`

```csharp
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Reglages;

public sealed class ServiceParametresTests : IDisposable
{
    private readonly DossierTemporaire _dossier = new();
    private readonly JournalEspion _journal = new();

    public void Dispose() => _dossier.Dispose();

    [Fact]
    public void Un_fichier_absent_donne_les_valeurs_par_defaut_sans_avertissement()
    {
        var service = Creer();

        var parametres = service.Charger();

        AssertDefauts(parametres);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Les_valeurs_par_defaut_sont_celles_de_la_spec()
    {
        var parametres = new Parametres();

        Assert.Equal("Ctrl+Alt+Maj+V", parametres.RaccourciSecours);
        Assert.True(parametres.DemarrageAvecWindows);
        Assert.Equal(ModeCollage.TexteSeul, parametres.MiseEnFormeParDefaut);
        Assert.Equal(5000, parametres.PlafondTemporaire);
        Assert.Equal(ThemeApplication.Systeme, parametres.Theme);
        Assert.Equal(["WindowsTerminal", "Code", "devenv", "powershell", "pwsh", "cmd", "Ssms", "rider64", "notepad++"], parametres.ApplicationsChasseFixe);
        Assert.Equal(920, parametres.LargeurFenetre);
        Assert.Equal(560, parametres.HauteurFenetre);
    }

    [Fact]
    public void Un_aller_retour_conserve_tous_les_reglages()
    {
        var service = Creer();
        var ecrits = new Parametres
        {
            RaccourciSecours = "Ctrl+Alt+F9",
            DemarrageAvecWindows = false,
            MiseEnFormeParDefaut = ModeCollage.AvecMiseEnForme,
            PlafondTemporaire = 0,
            Theme = ThemeApplication.Sombre,
            ApplicationsChasseFixe = ["Code", "Mon éditeur", "x\"y"],
            LargeurFenetre = 1111,
            HauteurFenetre = 777,
        };

        service.Enregistrer(ecrits);
        var lus = Creer().Charger();

        Assert.Equal(ecrits.RaccourciSecours, lus.RaccourciSecours);
        Assert.Equal(ecrits.DemarrageAvecWindows, lus.DemarrageAvecWindows);
        Assert.Equal(ecrits.MiseEnFormeParDefaut, lus.MiseEnFormeParDefaut);
        Assert.Equal(ecrits.PlafondTemporaire, lus.PlafondTemporaire);
        Assert.Equal(ecrits.Theme, lus.Theme);
        Assert.Equal(ecrits.ApplicationsChasseFixe, lus.ApplicationsChasseFixe);
        Assert.Equal(ecrits.LargeurFenetre, lus.LargeurFenetre);
        Assert.Equal(ecrits.HauteurFenetre, lus.HauteurFenetre);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Le_fichier_est_indente_et_ecrit_les_enumerations_en_texte()
    {
        Creer().Enregistrer(new Parametres { Theme = ThemeApplication.Clair, MiseEnFormeParDefaut = ModeCollage.AvecMiseEnForme });

        var contenu = File.ReadAllText(Chemin);

        Assert.Contains("\"Theme\": \"Clair\"", contenu);
        Assert.Contains("\"MiseEnFormeParDefaut\": \"AvecMiseEnForme\"", contenu);
        Assert.Contains("\n", contenu);
        Assert.Contains("  \"RaccourciSecours\"", contenu);
        Assert.False(File.ReadAllBytes(Chemin).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "pas de marque d'ordre des octets");
    }

    [Fact]
    public void Enregistrer_cree_le_dossier_manquant_et_ne_laisse_aucun_fichier_temporaire()
    {
        var chemin = _dossier.Fichier(Path.Combine("sous", "dossier", "parametres.json"));
        var service = new ServiceParametres(chemin, _journal);

        service.Enregistrer(new Parametres { PlafondTemporaire = 42 });

        Assert.True(File.Exists(chemin));
        Assert.Equal([chemin], Directory.GetFiles(Path.GetDirectoryName(chemin)!));
        Assert.Equal(42, service.Charger().PlafondTemporaire);
    }

    [Fact]
    public void Enregistrer_remplace_le_fichier_existant_et_un_ancien_temporaire_est_ecrase()
    {
        var service = Creer();
        service.Enregistrer(new Parametres { PlafondTemporaire = 1 });
        File.WriteAllText(Chemin + ".tmp", "reste d'une coupure");

        service.Enregistrer(new Parametres { PlafondTemporaire = 2 });

        Assert.Equal(2, service.Charger().PlafondTemporaire);
        Assert.False(File.Exists(Chemin + ".tmp"));
    }

    [Fact]
    public void Un_echec_d_ecriture_laisse_l_ancien_fichier_intact_et_supprime_le_temporaire()
    {
        var service = Creer();
        service.Enregistrer(new Parametres { PlafondTemporaire = 7 });

        // Un lecteur qui interdit le remplacement du fichier fait échouer le déplacement final.
        using (new FileStream(Chemin, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<Exception>(() => service.Enregistrer(new Parametres { PlafondTemporaire = 8 }));
        }

        Assert.False(File.Exists(Chemin + ".tmp"));
        Assert.Equal(7, service.Charger().PlafondTemporaire);
    }

    [Theory]
    [InlineData("{ pas du json")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"texte\"")]
    [InlineData("{\"Theme\": \"Rose\"}")]
    [InlineData("{\"Theme\": 1}")]
    [InlineData("{\"MiseEnFormeParDefaut\": 9}")]
    [InlineData("{\"PlafondTemporaire\": \"beaucoup\"}")]
    [InlineData("{\"LargeurFenetre\": 1e99}")]
    [InlineData("{\"ApplicationsChasseFixe\": \"Code\"}")]
    [InlineData("{\"DemarrageAvecWindows\": \"oui\"}")]
    [InlineData("{\"Theme\": \"Clair\"")]
    public void Un_fichier_corrompu_donne_les_valeurs_par_defaut_avec_un_avertissement(string contenu)
    {
        File.WriteAllText(Chemin, contenu);

        var parametres = Creer().Charger();

        AssertDefauts(parametres);
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_binaire_donne_les_valeurs_par_defaut_avec_un_avertissement()
    {
        File.WriteAllBytes(Chemin, [0xFF, 0xFE, 0x00, 0x01, 0x80, 0x81, 0x00, 0x00, 0xC3, 0x28]);

        var parametres = Creer().Charger();

        AssertDefauts(parametres);
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_partiel_garde_les_valeurs_manquantes_par_defaut()
    {
        File.WriteAllText(Chemin, "{ \"Theme\": \"Clair\", \"PlafondTemporaire\": 100 }");

        var parametres = Creer().Charger();

        Assert.Equal(ThemeApplication.Clair, parametres.Theme);
        Assert.Equal(100, parametres.PlafondTemporaire);
        Assert.Equal("Ctrl+Alt+Maj+V", parametres.RaccourciSecours);
        Assert.Equal(920, parametres.LargeurFenetre);
        Assert.Equal(new Parametres().ApplicationsChasseFixe, parametres.ApplicationsChasseFixe);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_modifie_a_la_main_est_tolere()
    {
        // Casse libre, commentaires, virgule finale, propriété inconnue, texte d'énumération en minuscules.
        File.WriteAllText(Chemin, """
            {
              // thème choisi à la main
              "theme": "sombre",
              "inconnue": 12,
              "plafondTemporaire": 250,
            }
            """);

        var parametres = Creer().Charger();

        Assert.Equal(ThemeApplication.Sombre, parametres.Theme);
        Assert.Equal(250, parametres.PlafondTemporaire);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_avec_marque_d_ordre_des_octets_est_lu()
    {
        File.WriteAllText(Chemin, "{ \"PlafondTemporaire\": 33 }", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Equal(33, Creer().Charger().PlafondTemporaire);
    }

    [Fact]
    public void Des_valeurs_lisibles_mais_inutilisables_sont_remplacees_une_a_une_avec_un_avertissement()
    {
        File.WriteAllText(Chemin, """
            {
              "Theme": "Sombre",
              "RaccourciSecours": null,
              "ApplicationsChasseFixe": null,
              "PlafondTemporaire": -5,
              "LargeurFenetre": 10,
              "HauteurFenetre": -1
            }
            """);

        var parametres = Creer().Charger();

        Assert.Equal(ThemeApplication.Sombre, parametres.Theme);
        Assert.Equal("Ctrl+Alt+Maj+V", parametres.RaccourciSecours);
        Assert.Equal(new Parametres().ApplicationsChasseFixe, parametres.ApplicationsChasseFixe);
        Assert.Equal(5000, parametres.PlafondTemporaire);
        Assert.Equal(920, parametres.LargeurFenetre);
        Assert.Equal(560, parametres.HauteurFenetre);
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Un_plafond_a_zero_et_une_liste_vide_sont_des_choix_valides()
    {
        File.WriteAllText(Chemin, "{ \"PlafondTemporaire\": 0, \"ApplicationsChasseFixe\": [] }");

        var parametres = Creer().Charger();

        Assert.Equal(0, parametres.PlafondTemporaire);
        Assert.Empty(parametres.ApplicationsChasseFixe);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_corrompu_est_remplace_par_un_fichier_valide_a_l_enregistrement()
    {
        File.WriteAllText(Chemin, "{ abîmé");
        var service = Creer();
        service.Charger();
        Assert.Single(_journal.Avertissements);

        service.Enregistrer(new Parametres { Theme = ThemeApplication.Clair });

        Assert.Equal(ThemeApplication.Clair, service.Charger().Theme);
    }

    [Fact]
    public void Le_constructeur_et_Enregistrer_refusent_les_arguments_nuls()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceParametres(Chemin, null!));
        Assert.ThrowsAny<ArgumentException>(() => new ServiceParametres("  ", _journal));
        Assert.Throws<ArgumentNullException>(() => Creer().Enregistrer(null!));
    }

    private string Chemin => _dossier.Fichier("parametres.json");

    private ServiceParametres Creer() => new(Chemin, _journal);

    private static void AssertDefauts(Parametres parametres)
    {
        var defauts = new Parametres();
        Assert.Equal(defauts.RaccourciSecours, parametres.RaccourciSecours);
        Assert.Equal(defauts.DemarrageAvecWindows, parametres.DemarrageAvecWindows);
        Assert.Equal(defauts.MiseEnFormeParDefaut, parametres.MiseEnFormeParDefaut);
        Assert.Equal(defauts.PlafondTemporaire, parametres.PlafondTemporaire);
        Assert.Equal(defauts.Theme, parametres.Theme);
        Assert.Equal(defauts.ApplicationsChasseFixe, parametres.ApplicationsChasseFixe);
        Assert.Equal(defauts.LargeurFenetre, parametres.LargeurFenetre);
        Assert.Equal(defauts.HauteurFenetre, parametres.HauteurFenetre);
    }

    // Journal qui retient les avertissements, propre à ce fichier de tests.
    private sealed class JournalEspion : IJournal
    {
        public List<string> Avertissements { get; } = [];

        public void Info(string message)
        {
        }

        public void Avertissement(string message, Exception? exception = null) => Avertissements.Add(message);

        public void Erreur(string message, Exception? exception = null)
        {
        }
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Attendu avant l'implémentation (la coquille lève `NotImplementedException("Tâche 10")`) : la solution compile, puis

```
Résumé de série de tests : Échec!
  total : 380
  échec : 29
  réussie : 351
  ignoré : 0
```

Un seul test du fichier réussit déjà (`Les_valeurs_par_defaut_sont_celles_de_la_spec`, qui n'utilise pas le service).

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Reglages/ServiceParametres.cs` (remplace la coquille, contenu complet)

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Coeur.Reglages;

/// <summary>
/// Lecture et écriture du fichier <c>parametres.json</c>. Un fichier absent, illisible ou aux valeurs absurdes ne bloque
/// jamais le démarrage : on retombe sur les valeurs par défaut.
/// </summary>
public sealed class ServiceParametres
{
    // Le fichier se modifie à la main : énumérations en texte (refus des nombres), casse libre, commentaires tolérés.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private static readonly UTF8Encoding Utf8SansMarque = new(encoderShouldEmitUTF8Identifier: false);

    private const int LargeurMinimale = 400;
    private const int HauteurMinimale = 300;

    private readonly string _cheminFichier;
    private readonly IJournal _journal;

    /// <summary>Crée le service sur un fichier de réglages.</summary>
    public ServiceParametres(string cheminFichier, IJournal journal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminFichier);
        ArgumentNullException.ThrowIfNull(journal);
        _cheminFichier = cheminFichier;
        _journal = journal;
    }

    /// <summary>Lit les réglages ; les valeurs par défaut si le fichier est absent ou illisible (avertissement au journal).</summary>
    public Parametres Charger()
    {
        if (!File.Exists(_cheminFichier))
        {
            // Cas normal au premier lancement : pas un problème à signaler.
            _journal.Info($"Aucun fichier de réglages ({_cheminFichier}) : valeurs par défaut.");
            return new Parametres();
        }

        try
        {
            var json = File.ReadAllText(_cheminFichier, Encoding.UTF8);
            var lus = JsonSerializer.Deserialize<Parametres>(json, Options);
            if (lus is null)
            {
                _journal.Avertissement($"Fichier de réglages vide ({_cheminFichier}) : valeurs par défaut.");
                return new Parametres();
            }

            return Corriger(lus);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _journal.Avertissement($"Fichier de réglages illisible ({_cheminFichier}) : valeurs par défaut.", ex);
            return new Parametres();
        }
    }

    /// <summary>Écrit les réglages de façon atomique : fichier temporaire, puis <c>File.Move(..., true)</c>.</summary>
    public void Enregistrer(Parametres parametres)
    {
        ArgumentNullException.ThrowIfNull(parametres);

        var dossier = Path.GetDirectoryName(Path.GetFullPath(_cheminFichier));
        if (!string.IsNullOrEmpty(dossier))
        {
            Directory.CreateDirectory(dossier);
        }

        // Le fichier définitif n'est remplacé que par un fichier complet : une coupure en cours d'écriture
        // laisse l'ancien contenu intact, jamais un fichier tronqué.
        var temporaire = _cheminFichier + ".tmp";
        try
        {
            File.WriteAllText(temporaire, JsonSerializer.Serialize(parametres, Options), Utf8SansMarque);
            File.Move(temporaire, _cheminFichier, overwrite: true);
        }
        catch
        {
            TenterSupprimer(temporaire);
            throw;
        }
    }

    // Une valeur lisible mais inutilisable (null écrit à la main, plafond négatif, fenêtre minuscule)
    // est remplacée par son défaut, sans perdre le reste des réglages.
    private Parametres Corriger(Parametres lus)
    {
        var defauts = new Parametres();
        var corriges = lus with
        {
            RaccourciSecours = string.IsNullOrWhiteSpace(lus.RaccourciSecours) ? defauts.RaccourciSecours : lus.RaccourciSecours,
            ApplicationsChasseFixe = lus.ApplicationsChasseFixe is null ? defauts.ApplicationsChasseFixe : lus.ApplicationsChasseFixe,
            PlafondTemporaire = lus.PlafondTemporaire < 0 ? defauts.PlafondTemporaire : lus.PlafondTemporaire,
            LargeurFenetre = lus.LargeurFenetre < LargeurMinimale ? defauts.LargeurFenetre : lus.LargeurFenetre,
            HauteurFenetre = lus.HauteurFenetre < HauteurMinimale ? defauts.HauteurFenetre : lus.HauteurFenetre,
        };

        if (!ReferenceEquals(corriges.ApplicationsChasseFixe, lus.ApplicationsChasseFixe)
            || corriges.PlafondTemporaire != lus.PlafondTemporaire
            || corriges.LargeurFenetre != lus.LargeurFenetre
            || corriges.HauteurFenetre != lus.HauteurFenetre
            || corriges.RaccourciSecours != lus.RaccourciSecours)
        {
            _journal.Avertissement($"Valeurs de réglages inutilisables dans {_cheminFichier} : remplacées par les valeurs par défaut.");
        }

        return corriges;
    }

    private static void TenterSupprimer(string chemin)
    {
        try
        {
            File.Delete(chemin);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 380
  échec : 0
  réussie : 380
  ignoré : 0
```

Les 30 nouveaux tests de `ServiceParametresTests` (15 fichiers corrompus en `[Theory]`, plus un fichier binaire, un fichier
partiel, un fichier modifié à la main, la marque d'ordre des octets, les valeurs inutilisables corrigées une à une, l'aller-retour
complet, le format du fichier, la création du dossier, le remplacement et l'échec d'écriture). Les 350 tests des tâches 1 à 9 restent verts.

Contrôle (point de vigilance 4) : chaque fichier abîmé donne exactement un avertissement et les défauts ; le test d'échec
d'écriture verrouille le fichier cible avec un `FileStream` en `FileShare.Read`, constate que `Enregistrer` lève, que l'ancien
contenu est intact (plafond 7) et qu'aucun `.tmp` ne traîne. Sous Windows l'exception levée est `UnauthorizedAccessException`
(déplacement par-dessus un fichier ouvert) : le test accepte toute exception.

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : le service ne touche que le fichier qu'on lui donne, couvert par des tests sur un dossier temporaire. (La lecture
effective au démarrage est contrôlée à la tâche 16, la page Paramètres à la tâche 28.)

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Reglages/ServiceParametres.cs tests/PressePapiers.Tests/Reglages/ServiceParametresTests.cs
git commit -m "Ajouter la lecture et l'ecriture des parametres"
```
