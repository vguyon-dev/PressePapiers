### Tâche 11 : AutomateWinV et AntiRebond

**Fichiers :**
- Modifier : `src/PressePapiers.Coeur/Clavier/AutomateWinV.cs` et `src/PressePapiers.Coeur/Clavier/AntiRebond.cs` (le plan donne le contenu complet de chaque fichier après modification)
- Créer : aucun
- Tests : `tests/PressePapiers.Tests/Clavier/AutomateWinVTests.cs` (créé, 21 tests), `tests/PressePapiers.Tests/Clavier/AntiRebondTests.cs` (créé, 7 tests)

**Interfaces :**
- Utilise (tâche 1) :
  - `enum ActionCrochet { Laisser, Avaler, AvalerEtOuvrir, MasquerPuisLaisser }` (espace de noms `PressePapiers.Coeur.Clavier`) ;
  - les coquilles `AutomateWinV` (constantes `VkWinGauche = 0x5B`, `VkWinDroite = 0x5C`, `VkV = 0x56`) et `AntiRebond` ;
  - tests : `Microsoft.Extensions.Time.Testing.FakeTimeProvider` (paquet déjà référencé par le projet de tests).
- Fournit (signatures du contrat, inchangées) :
  - `public ActionCrochet AutomateWinV.Traiter(uint touche, bool enfoncee, bool injectee, bool winReellementEnfoncee)` ;
  - `public void AutomateWinV.Reinitialiser()` ;
  - `public AntiRebond(TimeProvider horloge, TimeSpan delai)` et `public bool Accepter()`.
  - Règles de l'automate, que la tâche 20 (`CrochetWinV`) applique frappe par frappe et que la recette vérifie :

| Frappe vue par le hook | État de l'automate | Résultat |
|---|---|---|
| toute frappe injectée (`LLKHF_INJECTED`) | quel qu'il soit | `Laisser`, état intact |
| Win (gauche ou droite) enfoncée | n'importe lequel | `Laisser` ; si aucune Win n'était tenue, repart d'une séquence propre (V non avalée, pas de masque) |
| V enfoncée | Win tenue et `winReellementEnfoncee` | première fois `AvalerEtOuvrir`, répétition `Avaler` ; un masque est dû au relâchement de Win |
| V enfoncée | Win tenue mais `winReellementEnfoncee` faux | `Laisser` et remise à zéro (état périmé : relâchement de Win manqué) |
| V relâchée | Win tenue | `Avaler` (V n'est plus avalée) |
| V enfoncée ou relâchée | Win déjà relâchée mais V encore avalée | `Avaler` ; le relâchement de V éteint l'état (sinon la répétition écrirait des « v ») |
| Win relâchée | un masque est dû (V a été avalée) | `MasquerPuisLaisser`, une seule fois |
| Win relâchée | aucun masque | `Laisser` (Win seule ouvre le menu Démarrer normalement) |
| toute autre touche | | `Laisser` |

  - `AntiRebond.Accepter()` : vrai pour le premier signal ; faux tant que le dernier signal ACCEPTÉ date de moins que `delai`
    (un refus ne repousse pas la fenêtre) ; un signal exactement à `delai` est accepté. Sûr entre fils (verrou interne).
    Consommateurs : tâche 20 (`CrochetWinV` pose l'automate statique), tâches 27 et 29 (ouverture idempotente : un second
    signal d'ouverture à moins de 300 ms du premier est ignoré).

Deux écarts volontaires par rapport à l'essai jetable de la spec (section 11), tous deux couverts par un test :
1. si Win est relâchée avant V, V (tenue) reste avalée jusqu'à son relâchement ; l'essai la laissait passer et sa répétition
   aurait écrit des « v » ;
2. un nouvel appui sur Win remet `vAvalee` à faux ; dans l'essai, un relâchement de V manqué faisait traiter tous les Win+V
   suivants comme des répétitions, donc sans ouverture.

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Clavier/AutomateWinVTests.cs`

```csharp
using PressePapiers.Coeur.Clavier;

namespace PressePapiers.Tests.Clavier;

public sealed class AutomateWinVTests
{
    private const uint Win = AutomateWinV.VkWinGauche;
    private const uint WinDroite = AutomateWinV.VkWinDroite;
    private const uint V = AutomateWinV.VkV;
    private const uint Autre = 0x41; // la lettre A

    // Raccourcis de lecture : une frappe réelle (non injectée) avec la touche Windows réellement enfoncée ou non.
    private static ActionCrochet Appuyer(AutomateWinV a, uint touche, bool winReelle = true) =>
        a.Traiter(touche, enfoncee: true, injectee: false, winReellementEnfoncee: winReelle);

    private static ActionCrochet Relacher(AutomateWinV a, uint touche, bool winReelle = true) =>
        a.Traiter(touche, enfoncee: false, injectee: false, winReellementEnfoncee: winReelle);

    [Fact]
    public void Win_puis_V_avale_et_ouvre()
    {
        var automate = new AutomateWinV();

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Win));
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void Win_droite_puis_V_avale_et_ouvre()
    {
        var automate = new AutomateWinV();

        Appuyer(automate, WinDroite);

        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void La_repetition_de_V_est_avalee_sans_rouvrir()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));

        // Le clavier répète V tant que la touche est tenue : une seule ouverture au total.
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V));
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V));
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V));
    }

    [Fact]
    public void Le_relachement_de_V_est_avale()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        Assert.Equal(ActionCrochet.Avaler, Relacher(automate, V));
    }

    [Fact]
    public void Un_second_Win_V_apres_relachement_de_V_rouvre()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, V);

        // Win reste tenue : un nouvel appui sur V est une nouvelle demande, pas une répétition.
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void Le_relachement_de_Win_apres_un_V_avale_masque_le_menu_Demarrer()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, V);

        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win));
    }

    [Fact]
    public void Le_masque_n_est_envoye_qu_une_fois()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win));

        // Le second relâchement (autre touche Windows, ou répétition) ne doit plus rien envoyer.
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void Win_relachee_avant_V_masque_puis_la_fin_de_V_est_avalee()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        // L'utilisateur lâche Win avant V : le masque part, et V, toujours tenue, ne doit ni s'écrire ni répéter.
        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win, winReelle: false));
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V, winReelle: false));
        Assert.Equal(ActionCrochet.Avaler, Relacher(automate, V, winReelle: false));
    }

    [Fact]
    public void Un_nouveau_Win_V_apres_une_sequence_interrompue_ouvre_encore()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, Win, winReelle: false); // V jamais relâchée vue par le hook (fenêtre élevée par exemple)

        Appuyer(automate, Win);

        // Une nouvelle pression de Win recommence la séquence : V n'est plus traitée comme une répétition.
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void Win_seule_est_laissee_aux_deux_transitions()
    {
        var automate = new AutomateWinV();

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Win));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void Win_seule_ne_demande_aucun_masque()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Relacher(automate, Win);

        // Win, Win : le menu Démarrer doit s'ouvrir normalement, aucun masque ne traîne.
        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void V_sans_Win_est_laissee()
    {
        var automate = new AutomateWinV();

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, V, winReelle: false));
    }

    [Fact]
    public void Win_plus_une_autre_touche_est_laissee_et_le_relachement_de_Win_aussi()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);

        // Win+E, Win+Maj+S, etc. restent aux mains de Windows.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Autre));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Autre));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void Une_frappe_injectee_est_toujours_laissee_meme_pour_Win_V()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);

        // La priorité de l'injection sur le reste : sans elle, ce V injecté serait avalé et ouvrirait la fenêtre.
        var action = automate.Traiter(V, enfoncee: true, injectee: true, winReellementEnfoncee: true);

        Assert.Equal(ActionCrochet.Laisser, action);
    }

    [Fact]
    public void Une_frappe_injectee_de_Win_ne_change_pas_l_etat()
    {
        var automate = new AutomateWinV();

        automate.Traiter(Win, enfoncee: true, injectee: true, winReellementEnfoncee: true);

        // Win injectée n'a pas armé l'automate : un V réel qui suit n'est pas une demande, même si GetAsyncKeyState
        // dit que Win est enfoncée (c'est alors la frappe injectée elle-même qui l'enfonce).
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: true));
    }

    [Fact]
    public void Le_masque_injecte_du_hook_lui_meme_est_laisse()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        // Le hook reçoit aussi la touche 0xE8 qu'il vient d'injecter : elle ne doit rien déclencher.
        var action = automate.Traiter(0xE8, enfoncee: true, injectee: true, winReellementEnfoncee: true);

        Assert.Equal(ActionCrochet.Laisser, action);
        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win));
    }

    [Fact]
    public void V_quand_Win_n_est_plus_reellement_enfoncee_est_laissee_et_remet_a_zero()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);

        // Le relâchement de Win est passé inaperçu (écran de verrouillage, fenêtre élevée) : l'état est périmé.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false));

        // Remise à zéro : même si Win est de nouveau signalée enfoncée, l'automate n'a pas vu son appui.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: true));
    }

    [Fact]
    public void La_remise_a_zero_par_V_perimee_ne_laisse_aucun_masque_en_attente()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Appuyer(automate, V, winReelle: false); // état périmé détecté : tout est oublié

        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void Reinitialiser_oublie_Win_enfoncee()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);

        automate.Reinitialiser();

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V));
    }

    [Fact]
    public void Reinitialiser_oublie_le_V_avale_et_le_masque()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        automate.Reinitialiser();

        // Plus de masque en attente : le relâchement de Win est laissé tel quel.
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));

        // Et V n'est plus considérée comme déjà avalée : une nouvelle séquence ouvre.
        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void Deux_touches_Windows_tenues_gardent_la_sequence_active_apres_le_relachement_de_l_une()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, WinDroite);
        Relacher(automate, Win);

        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }
}
```

`tests/PressePapiers.Tests/Clavier/AntiRebondTests.cs`

```csharp
using Microsoft.Extensions.Time.Testing;
using PressePapiers.Coeur.Clavier;

namespace PressePapiers.Tests.Clavier;

public sealed class AntiRebondTests
{
    private static readonly TimeSpan Delai = TimeSpan.FromMilliseconds(300);

    [Fact]
    public void Le_premier_signal_est_accepte()
    {
        var antiRebond = new AntiRebond(new FakeTimeProvider(), Delai);

        Assert.True(antiRebond.Accepter());
    }

    [Fact]
    public void Un_second_signal_en_moins_du_delai_est_refuse()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);
        Assert.True(antiRebond.Accepter());

        horloge.Advance(TimeSpan.FromMilliseconds(299));

        Assert.False(antiRebond.Accepter());
    }

    [Fact]
    public void Un_signal_a_la_date_exacte_du_delai_est_accepte()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);
        Assert.True(antiRebond.Accepter());

        horloge.Advance(Delai);

        Assert.True(antiRebond.Accepter());
    }

    [Fact]
    public void Un_signal_apres_le_delai_est_accepte()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);
        Assert.True(antiRebond.Accepter());

        horloge.Advance(TimeSpan.FromMilliseconds(301));

        Assert.True(antiRebond.Accepter());
    }

    [Fact]
    public void Un_signal_refuse_ne_repousse_pas_la_fenetre()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);
        Assert.True(antiRebond.Accepter());           // t = 0
        horloge.Advance(TimeSpan.FromMilliseconds(200));
        Assert.False(antiRebond.Accepter());          // t = 200, refusé

        horloge.Advance(TimeSpan.FromMilliseconds(150));

        // t = 350 : 350 ms après le dernier signal ACCEPTÉ. Si le refus de t = 200 repoussait la fenêtre,
        // on ne serait qu'à 150 ms et ce signal serait refusé à tort.
        Assert.True(antiRebond.Accepter());
    }

    [Fact]
    public void Une_rafale_de_signaux_serres_ne_laisse_passer_que_le_premier()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);

        var acceptes = 0;
        for (var i = 0; i < 10; i++)
        {
            if (antiRebond.Accepter()) acceptes++;
            horloge.Advance(TimeSpan.FromMilliseconds(20));
        }

        Assert.Equal(1, acceptes);
    }

    [Fact]
    public void Le_delai_nul_accepte_tout()
    {
        var antiRebond = new AntiRebond(new FakeTimeProvider(), TimeSpan.Zero);

        Assert.True(antiRebond.Accepter());
        Assert.True(antiRebond.Accepter());
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : les 28 nouveaux tests échouent avec `System.NotImplementedException : Tâche 11` ; les 380 tests des tâches
1 à 10 restent verts :

```
Résumé de série de tests : Échec!
  total : 408
  échec : 28
  réussie : 380
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Clavier/AutomateWinV.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>
/// Logique pure du hook Win+V (spec, section 11), testable sans Windows. L'automate ne fait que décider :
/// l'envoi de la touche sans effet et le signal d'ouverture appartiennent à l'appelant.
/// </summary>
public sealed class AutomateWinV
{
    /// <summary>Touche Windows gauche.</summary>
    public const uint VkWinGauche = 0x5B;

    /// <summary>Touche Windows droite.</summary>
    public const uint VkWinDroite = 0x5C;

    /// <summary>Touche V.</summary>
    public const uint VkV = 0x56;

    // Le hook décide sur son fil, mais la remise à zéro peut venir d'ailleurs (déverrouillage, sortie de veille).
    private readonly object _verrou = new();

    private bool _winGaucheEnfoncee;
    private bool _winDroiteEnfoncee;

    // V a été avalée et n'a pas encore été relâchée : toute répétition suivante n'est qu'avalée.
    private bool _vAvalee;

    // Win+V a eu lieu : au relâchement de Win, une touche sans effet doit précéder pour que le menu Démarrer reste fermé.
    private bool _masqueAEnvoyer;

    private bool WinEnfoncee => _winGaucheEnfoncee || _winDroiteEnfoncee;

    /// <summary>
    /// Décide du sort d'une frappe. <paramref name="injectee"/> : frappe portant LLKHF_INJECTED (toujours laissée) ;
    /// <paramref name="winReellementEnfoncee"/> : résultat de GetAsyncKeyState au moment de V.
    /// </summary>
    public ActionCrochet Traiter(uint touche, bool enfoncee, bool injectee, bool winReellementEnfoncee)
    {
        // Nos propres frappes (touche sans effet, Ctrl+V du recollage) ne doivent jamais armer ni déclencher l'automate.
        if (injectee) return ActionCrochet.Laisser;

        lock (_verrou)
        {
            if (touche is VkWinGauche or VkWinDroite) return TraiterWin(touche, enfoncee);
            if (touche == VkV) return TraiterV(enfoncee, winReellementEnfoncee);
            return ActionCrochet.Laisser;
        }
    }

    /// <summary>Oublie tout l'état : déverrouillage, sortie de veille, réinstallation du hook.</summary>
    public void Reinitialiser()
    {
        lock (_verrou) RemettreAZero();
    }

    private ActionCrochet TraiterWin(uint touche, bool enfoncee)
    {
        if (enfoncee)
        {
            // Un nouvel appui sur Win (et non la répétition d'un appui déjà vu) recommence une séquence propre.
            if (!WinEnfoncee)
            {
                _vAvalee = false;
                _masqueAEnvoyer = false;
            }

            if (touche == VkWinGauche) _winGaucheEnfoncee = true; else _winDroiteEnfoncee = true;
            return ActionCrochet.Laisser;
        }

        if (touche == VkWinGauche) _winGaucheEnfoncee = false; else _winDroiteEnfoncee = false;

        if (!_masqueAEnvoyer) return ActionCrochet.Laisser;

        _masqueAEnvoyer = false;
        return ActionCrochet.MasquerPuisLaisser;
    }

    private ActionCrochet TraiterV(bool enfoncee, bool winReellementEnfoncee)
    {
        if (WinEnfoncee)
        {
            // L'état dit « Win enfoncée » mais la touche ne l'est plus : un relâchement a échappé au hook
            // (fenêtre élevée, écran de verrouillage). On ne vole pas une frappe sur un état périmé.
            if (!winReellementEnfoncee)
            {
                RemettreAZero();
                return ActionCrochet.Laisser;
            }

            if (enfoncee)
            {
                // La répétition automatique de la touche ne compte qu'une fois.
                var premiere = !_vAvalee;
                _vAvalee = true;
                _masqueAEnvoyer = true;
                return premiere ? ActionCrochet.AvalerEtOuvrir : ActionCrochet.Avaler;
            }

            _vAvalee = false;
            return ActionCrochet.Avaler;
        }

        // Win a été lâchée avant V : V, déjà avalée à l'enfoncement, l'est encore jusqu'à son relâchement,
        // sinon la répétition écrirait des « v » dans l'application.
        if (_vAvalee)
        {
            if (!enfoncee) _vAvalee = false;
            return ActionCrochet.Avaler;
        }

        return ActionCrochet.Laisser;
    }

    private void RemettreAZero()
    {
        _winGaucheEnfoncee = false;
        _winDroiteEnfoncee = false;
        _vAvalee = false;
        _masqueAEnvoyer = false;
    }
}
```

`src/PressePapiers.Coeur/Clavier/AntiRebond.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>
/// Ignore un signal trop proche du dernier signal accepté (double Win+V, rafale du raccourci de secours).
/// Sûr entre fils : les signaux arrivent du fil d'interface et du fil du hook.
/// </summary>
public sealed class AntiRebond
{
    private readonly TimeProvider _horloge;
    private readonly TimeSpan _delai;
    private readonly object _verrou = new();

    // Instant (horloge monotone) du dernier signal ACCEPTÉ ; un signal refusé ne repousse pas la fenêtre.
    private long? _dernierAccepte;

    /// <summary>Crée l'anti-rebond avec son horloge et son délai.</summary>
    public AntiRebond(TimeProvider horloge, TimeSpan delai)
    {
        ArgumentNullException.ThrowIfNull(horloge);
        _horloge = horloge;
        _delai = delai;
    }

    /// <summary>Faux si le dernier signal accepté date de moins que le délai.</summary>
    public bool Accepter()
    {
        lock (_verrou)
        {
            var maintenant = _horloge.GetTimestamp();
            if (_dernierAccepte is long precedent && _horloge.GetElapsedTime(precedent, maintenant) < _delai)
                return false;

            _dernierAccepte = maintenant;
            return true;
        }
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis tous les tests verts :

```
Résumé de série de tests : Réussite!
  total : 408
  échec : 0
  réussie : 408
  ignoré : 0
```

Les 28 nouveaux tests : `AutomateWinVTests` 21, `AntiRebondTests` 7.

Point de vigilance 5 (signaux répétés) : la répétition de V est couverte par `La_repetition_de_V_est_avalee_sans_rouvrir`,
le double signal en moins de 300 ms par `Une_rafale_de_signaux_serres_ne_laisse_passer_que_le_premier`.

Contrôle de l'ordre et des priorités, vérifié en inversant réellement l'implémentation (chaque mutation fait échouer au moins
un test, puis le code est restauré) :
- tester l'injection après le traitement de Win et de V : `Une_frappe_injectee_est_toujours_laissee_meme_pour_Win_V` et
  `Une_frappe_injectee_de_Win_ne_change_pas_l_etat` échouent (ce dernier utilise `winReelle: true` exprès : avec `false` il
  passerait même avec la mutation) ;
- supprimer le contrôle de `winReellementEnfoncee` : `V_quand_Win_n_est_plus_reellement_enfoncee_est_laissee_et_remet_a_zero`
  et `La_remise_a_zero_par_V_perimee_ne_laisse_aucun_masque_en_attente` échouent ;
- ne plus remettre `vAvalee` à faux à un nouvel appui sur Win : `Un_nouveau_Win_V_apres_une_sequence_interrompue_ouvre_encore`
  échoue ;
- faire repousser la fenêtre par un signal refusé : `Un_signal_refuse_ne_repousse_pas_la_fenetre` échoue.

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : la logique est pure et entièrement couverte par les tests. Le comportement réel du hook se vérifie à la tâche 20.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Clavier/AutomateWinV.cs src/PressePapiers.Coeur/Clavier/AntiRebond.cs tests/PressePapiers.Tests/Clavier/AutomateWinVTests.cs tests/PressePapiers.Tests/Clavier/AntiRebondTests.cs
git commit -m "Ajouter l'automate Win+V et l'anti-rebond" -m "L'automate decide du sort de chaque frappe du hook (ouvrir, avaler, masquer le menu Demarrer, laisser) sans toucher a Windows. Il corrige deux points de l'essai jetable : V tenue apres le relachement de Win reste avalee, et un nouvel appui sur Win recommence une sequence propre au lieu de traiter V comme une repetition. L'anti-rebond refuse un signal a moins du delai du dernier signal accepte."
```
