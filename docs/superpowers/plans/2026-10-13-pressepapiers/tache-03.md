### Tâche 3 : Raccourci

**Fichiers :**
- Modifier : `src/PressePapiers.Coeur/Reglages/Raccourci.cs`
- Créer : aucun
- Tests : `tests/PressePapiers.Tests/Reglages/RaccourciTests.cs`

**Interfaces :**
- Utilise (tâche 1) : la coquille `Raccourci` ; `Parametres.RaccourciSecours` (valeur par défaut `"Ctrl+Alt+Maj+V"`).
- Fournit :
  - `public readonly record struct Raccourci(bool Ctrl, bool Alt, bool Maj, uint ToucheVirtuelle)` ;
  - `public static bool TryAnalyser(string texte, out Raccourci raccourci)` : refuse (et rend `default`) un texte vide, sans
    Ctrl ni Alt, sans touche principale, avec deux touches principales, un modificateur répété, la touche Windows (« Win »,
    « Windows », comme tout mot inconnu) ou une touche hors A-Z, 0-9, F1-F24, Espace ;
  - `public uint Modificateurs { get; }` : MOD_ALT 0x1, MOD_CONTROL 0x2, MOD_SHIFT 0x4 selon les drapeaux, plus
    MOD_NOREPEAT 0x4000 ; directement utilisable comme argument `fsModifiers` de `RegisterHotKey` ;
  - `public override string ToString()` : forme canonique, modificateurs toujours dans l'ordre Ctrl, Alt, Maj, puis la touche
    (lettre en majuscule, `F5`, `Espace` ; une touche hors liste donne `0x..` au lieu de lever une exception).
  - Consommateurs : tâche 17 (`RelaisWindows.EnregistrerRaccourci` passe `Modificateurs` et `ToucheVirtuelle`), tâche 28
    (page Paramètres : analyse de la saisie, affichage de `ToString()`), tâche 29.

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Reglages/RaccourciTests.cs`

```csharp
using PressePapiers.Coeur.Reglages;

namespace PressePapiers.Tests.Reglages;

public sealed class RaccourciTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Maj+V", true, true, true, 0x56u)]
    [InlineData("ctrl+alt+maj+v", true, true, true, 0x56u)]
    [InlineData("CTRL+ALT+MAJ+V", true, true, true, 0x56u)]
    [InlineData("Maj+Alt+Ctrl+V", true, true, true, 0x56u)]
    [InlineData("Shift+Ctrl+V", true, false, true, 0x56u)]
    [InlineData("Ctrl+Alt+z", true, true, false, 0x5Au)]
    [InlineData("Ctrl+1", true, false, false, 0x31u)]
    [InlineData("Alt+0", false, true, false, 0x30u)]
    [InlineData("Alt+F5", false, true, false, 0x74u)]
    [InlineData("Ctrl+F1", true, false, false, 0x70u)]
    [InlineData("Ctrl+F24", true, false, false, 0x87u)]
    [InlineData("Ctrl+Espace", true, false, false, 0x20u)]
    [InlineData("Ctrl+Alt+espace", true, true, false, 0x20u)]
    [InlineData("Ctrl+F", true, false, false, 0x46u)]
    [InlineData(" Ctrl + Alt + V ", true, true, false, 0x56u)]
    public void TryAnalyser_accepte_les_formes_valides(string texte, bool ctrl, bool alt, bool maj, uint touche)
    {
        Assert.True(Raccourci.TryAnalyser(texte, out var raccourci));

        Assert.Equal(new Raccourci(ctrl, alt, maj, touche), raccourci);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("V")]                // ni Ctrl ni Alt
    [InlineData("Maj+V")]            // Maj seule ne suffit pas : elle gênerait la frappe ordinaire
    [InlineData("Ctrl")]             // pas de touche principale
    [InlineData("Ctrl+Alt+Maj")]
    [InlineData("Win+V")]            // la touche Windows est réservée au système
    [InlineData("Windows+V")]
    [InlineData("Ctrl+Win+V")]
    [InlineData("Ctrl+Windows+Maj+V")]
    [InlineData("Ctrl+Meta+V")]
    [InlineData("Ctrl+V+C")]         // deux touches principales
    [InlineData("Ctrl+Ctrl+V")]      // modificateur répété
    [InlineData("Maj+Shift+Ctrl+V")]
    [InlineData("Ctrl++V")]
    [InlineData("Ctrl+")]
    [InlineData("+V")]
    [InlineData("Ctrl+F0")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+F05")]
    [InlineData("Ctrl+AB")]
    [InlineData("Ctrl+é")]
    [InlineData("Ctrl+Tab")]
    [InlineData("Ctrl+@")]
    public void TryAnalyser_refuse_les_formes_invalides_et_rend_un_raccourci_vide(string? texte)
    {
        Assert.False(Raccourci.TryAnalyser(texte!, out var raccourci));

        Assert.Equal(default, raccourci);
    }

    [Fact]
    public void Le_raccourci_de_secours_par_defaut_des_parametres_est_valide()
    {
        Assert.True(Raccourci.TryAnalyser(new Parametres().RaccourciSecours, out var raccourci));

        Assert.Equal(new Raccourci(true, true, true, 0x56), raccourci);
    }

    [Theory]
    [InlineData(false, false, false, 0x4000u)]
    [InlineData(false, true, false, 0x4001u)]   // MOD_ALT
    [InlineData(true, false, false, 0x4002u)]   // MOD_CONTROL
    [InlineData(false, false, true, 0x4004u)]   // MOD_SHIFT
    [InlineData(true, false, true, 0x4006u)]
    [InlineData(true, true, true, 0x4007u)]
    public void Modificateurs_combine_les_drapeaux_avec_NOREPEAT(bool ctrl, bool alt, bool maj, uint attendu)
    {
        Assert.Equal(attendu, new Raccourci(ctrl, alt, maj, 0x56).Modificateurs);
    }

    [Theory]
    [InlineData(true, true, true, 0x56u, "Ctrl+Alt+Maj+V")]
    [InlineData(true, false, false, 0x31u, "Ctrl+1")]
    [InlineData(false, true, false, 0x74u, "Alt+F5")]
    [InlineData(true, true, false, 0x87u, "Ctrl+Alt+F24")]
    [InlineData(true, false, true, 0x20u, "Ctrl+Maj+Espace")]
    [InlineData(true, false, false, 0x0Du, "Ctrl+0x0D")]   // touche hors liste : forme lisible plutôt qu'une exception
    public void ToString_donne_la_forme_canonique(bool ctrl, bool alt, bool maj, uint touche, string attendu)
    {
        Assert.Equal(attendu, new Raccourci(ctrl, alt, maj, touche).ToString());
    }

    [Fact]
    public void La_forme_canonique_range_toujours_les_modificateurs_dans_le_meme_ordre()
    {
        Assert.True(Raccourci.TryAnalyser("Maj+Alt+Ctrl+V", out var raccourci));

        // Si l'ordre était inversé (Maj en premier), ce texte sortirait « Maj+Alt+Ctrl+V ».
        Assert.Equal("Ctrl+Alt+Maj+V", raccourci.ToString());
    }

    [Fact]
    public void Deux_ecritures_du_meme_raccourci_sont_egales()
    {
        Assert.True(Raccourci.TryAnalyser("ctrl+alt+v", out var un));
        Assert.True(Raccourci.TryAnalyser("Alt+Ctrl+V", out var deux));

        Assert.Equal(un, deux);
    }

    [Fact]
    public void Chaque_touche_acceptee_survit_a_l_aller_retour_texte()
    {
        var touches = new List<uint>();
        touches.AddRange(Enumerable.Range('A', 26).Select(c => (uint)c));
        touches.AddRange(Enumerable.Range('0', 10).Select(c => (uint)c));
        touches.AddRange(Enumerable.Range(0x70, 24).Select(c => (uint)c));
        touches.Add(0x20);

        var essais = 0;
        foreach (var touche in touches)
        {
            // Les sept combinaisons non vides de modificateurs, dont celles sans Ctrl ni Alt (refusées au retour).
            for (var masque = 1; masque < 8; masque++)
            {
                var original = new Raccourci((masque & 1) != 0, (masque & 2) != 0, (masque & 4) != 0, touche);
                var valide = original.Ctrl || original.Alt;

                var accepte = Raccourci.TryAnalyser(original.ToString(), out var relu);

                Assert.Equal(valide, accepte);
                if (valide)
                {
                    Assert.Equal(original, relu);
                }

                essais++;
            }
        }

        Assert.Equal(61 * 7, essais);
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : les 56 nouveaux tests échouent avec `System.NotImplementedException : Tâche 3` ; les 72 tests des
tâches 1 et 2 restent verts :

```
Résumé de série de tests : Échec!
  total : 128
  échec : 56
  réussie : 72
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Reglages/Raccourci.cs`

```csharp
namespace PressePapiers.Coeur.Reglages;

/// <summary>Raccourci clavier global : modificateurs et touche principale, sans la touche Windows.</summary>
/// <param name="Ctrl">Touche Ctrl.</param>
/// <param name="Alt">Touche Alt.</param>
/// <param name="Maj">Touche Maj.</param>
/// <param name="ToucheVirtuelle">Code de touche virtuelle de la touche principale.</param>
public readonly record struct Raccourci(bool Ctrl, bool Alt, bool Maj, uint ToucheVirtuelle)
{
    // Drapeaux de RegisterHotKey.
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    private const uint VkEspace = 0x20;
    private const uint VkF1 = 0x70;

    /// <summary>
    /// Analyse un texte du type « Ctrl+Alt+Maj+V » : ordre et casse libres, « Shift » accepté pour Maj, espaces autour des
    /// signes tolérés. Il faut Ctrl ou Alt (Maj seule gênerait la frappe ordinaire) ; la touche Windows est refusée, comme tout
    /// mot inconnu, parce que Windows se réserve ces combinaisons ; la touche principale est une lettre A-Z, un chiffre 0-9,
    /// F1 à F24 ou Espace.
    /// </summary>
    /// <param name="texte">Texte saisi par l'utilisateur.</param>
    /// <param name="raccourci">Le raccourci analysé, ou la valeur vide si le texte est refusé.</param>
    /// <returns>Vrai si le texte est un raccourci valide.</returns>
    public static bool TryAnalyser(string texte, out Raccourci raccourci)
    {
        raccourci = default;
        if (string.IsNullOrWhiteSpace(texte))
        {
            return false;
        }

        bool ctrl = false, alt = false, maj = false;
        uint? touche = null;

        foreach (var morceau in texte.Split('+'))
        {
            var jeton = morceau.Trim();
            if (jeton.Length == 0)
            {
                return false;
            }

            switch (jeton.ToLowerInvariant())
            {
                case "ctrl":
                    if (ctrl) return false;
                    ctrl = true;
                    continue;
                case "alt":
                    if (alt) return false;
                    alt = true;
                    continue;
                case "maj" or "shift":
                    if (maj) return false;
                    maj = true;
                    continue;
            }

            // Tout autre jeton doit être l'unique touche principale.
            if (touche is not null || !TryToucheVirtuelle(jeton, out var code))
            {
                return false;
            }

            touche = code;
        }

        if (touche is null || !(ctrl || alt))
        {
            return false;
        }

        raccourci = new Raccourci(ctrl, alt, maj, touche.Value);
        return true;
    }

    /// <summary>MOD_CONTROL, MOD_ALT et MOD_SHIFT selon les drapeaux, plus MOD_NOREPEAT (0x4000).</summary>
    public uint Modificateurs =>
        ModNoRepeat | (Ctrl ? ModControl : 0) | (Alt ? ModAlt : 0) | (Maj ? ModShift : 0);

    /// <summary>Forme canonique « Ctrl+Alt+Maj+V » : toujours Ctrl, Alt, Maj puis la touche.</summary>
    public override string ToString()
    {
        var parties = new List<string>(4);
        if (Ctrl) parties.Add("Ctrl");
        if (Alt) parties.Add("Alt");
        if (Maj) parties.Add("Maj");
        parties.Add(NomTouche(ToucheVirtuelle));
        return string.Join('+', parties);
    }

    private static bool TryToucheVirtuelle(string jeton, out uint code)
    {
        code = 0;

        if (jeton.Length == 1)
        {
            var c = char.ToUpperInvariant(jeton[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                code = c; // pour les lettres majuscules et les chiffres, le code virtuel est le code ASCII
                return true;
            }

            return false;
        }

        if (jeton.Equals("Espace", StringComparison.OrdinalIgnoreCase))
        {
            code = VkEspace;
            return true;
        }

        // F1 à F24 : un ou deux chiffres, sans zéro de tête.
        if ((jeton[0] is 'F' or 'f') && jeton.Length <= 3 && jeton[1] != '0'
            && jeton[1..].All(char.IsAsciiDigit)
            && int.Parse(jeton[1..]) is >= 1 and <= 24 and var numero)
        {
            code = VkF1 + (uint)(numero - 1);
            return true;
        }

        return false;
    }

    private static string NomTouche(uint code) => code switch
    {
        >= 'A' and <= 'Z' or >= '0' and <= '9' => ((char)code).ToString(),
        >= VkF1 and <= VkF1 + 23 => "F" + (code - VkF1 + 1),
        VkEspace => "Espace",
        _ => $"0x{code:X2}",
    };
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
  total : 128
  échec : 0
  réussie : 128
  ignoré : 0
```

Les 56 nouveaux tests : `RaccourciTests` 56 (15 formes valides, 25 formes refusées, 1 défaut des paramètres,
6 Modificateurs, 6 ToString, 1 ordre canonique, 1 égalité, 1 aller-retour sur 61 touches et 7 combinaisons).

Contrôle de l'ordre : `La_forme_canonique_range_toujours_les_modificateurs_dans_le_meme_ordre` échoue si `ToString` range
les modificateurs dans un autre ordre que Ctrl, Alt, Maj (par exemple Maj en premier).

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : le code est portable et entièrement couvert par les tests. (Le comportement réel de `RegisterHotKey` avec ces
`Modificateurs` se vérifie à la tâche 17, puis à la recette, point 5 de la section 16 de la spec.)

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Reglages/Raccourci.cs tests/PressePapiers.Tests/Reglages/RaccourciTests.cs
git commit -m "Ajouter l'analyse du raccourci clavier"
```
