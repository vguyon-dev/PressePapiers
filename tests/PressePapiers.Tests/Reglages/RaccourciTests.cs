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
    [InlineData("Ctrl+Alt+1", true, true, false, 0x31u)]
    [InlineData("Alt+Maj+0", false, true, true, 0x30u)]
    [InlineData("Alt+Maj+F5", false, true, true, 0x74u)]
    [InlineData("Ctrl+Maj+F1", true, false, true, 0x70u)]
    [InlineData("Ctrl+Alt+F24", true, true, false, 0x87u)]
    [InlineData("Ctrl+Maj+Espace", true, false, true, 0x20u)]
    [InlineData("Ctrl+Alt+espace", true, true, false, 0x20u)]
    [InlineData("Ctrl+Alt+F", true, true, false, 0x46u)]
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
    [InlineData("V")]                // aucun modificateur
    [InlineData("Maj+V")]            // un seul modificateur : il gênerait la frappe ordinaire
    [InlineData("Ctrl+C")]           // copier, coller, annuler et fermer ne se détournent pas dans tout Windows
    [InlineData("Ctrl+V")]
    [InlineData("Ctrl+Z")]
    [InlineData("ctrl+x")]
    [InlineData("Alt+F4")]
    [InlineData("Alt+0")]
    [InlineData("Ctrl+1")]
    [InlineData("Ctrl+Espace")]
    [InlineData("Ctrl+F24")]
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

    [Theory]
    [InlineData("Ctrl+Alt+V")]
    [InlineData("Ctrl+Maj+V")]
    [InlineData("Alt+Maj+V")]
    [InlineData("Ctrl+Alt+Maj+V")]
    public void Deux_modificateurs_au_moins_suffisent(string texte)
    {
        Assert.True(Raccourci.TryAnalyser(texte, out _));
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
            // Les sept combinaisons non vides de modificateurs, dont celles à un seul modificateur (refusées au retour).
            for (var masque = 1; masque < 8; masque++)
            {
                var original = new Raccourci((masque & 1) != 0, (masque & 2) != 0, (masque & 4) != 0, touche);
                var valide = (original.Ctrl ? 1 : 0) + (original.Alt ? 1 : 0) + (original.Maj ? 1 : 0) >= 2;

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
