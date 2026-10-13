using PressePapiers.Coeur.Clavier;

namespace PressePapiers.Tests.Clavier;

public sealed class TableDesTouchesTests
{
    private const uint Entree = TableDesTouches.VkEntree;

    private static CommandeClavier Interpreter(uint touche, bool ctrl = false, bool maj = false, bool alt = false,
                                               ZoneFocus zone = ZoneFocus.Recherche, bool rechercheVide = true,
                                               bool dialogueOuvert = false) =>
        TableDesTouches.Interpreter(touche, ctrl, maj, alt, zone, rechercheVide, dialogueOuvert);

    [Theory]
    [InlineData(ZoneFocus.Recherche)]
    [InlineData(ZoneFocus.Liste)]
    public void Entree_colle_depuis_la_recherche_et_la_liste(ZoneFocus zone)
    {
        Assert.Equal(ActionTouche.Coller, Interpreter(Entree, zone: zone).Action);
    }

    [Theory]
    [InlineData(ZoneFocus.Recherche)]
    [InlineData(ZoneFocus.Liste)]
    public void Maj_Entree_colle_dans_l_autre_mode(ZoneFocus zone)
    {
        Assert.Equal(ActionTouche.CollerAutreMode, Interpreter(Entree, maj: true, zone: zone).Action);
    }

    [Theory]
    [InlineData(ZoneFocus.Recherche)]
    [InlineData(ZoneFocus.Liste)]
    public void Ctrl_Entree_copie_seulement(ZoneFocus zone)
    {
        Assert.Equal(ActionTouche.CopierSeulement, Interpreter(Entree, ctrl: true, zone: zone).Action);
    }

    [Fact]
    public void Ctrl_Maj_Entree_copie_seulement_et_ne_colle_jamais()
    {
        // La copie seule est la plus prudente : elle l'emporte sur Maj+Entrée.
        Assert.Equal(ActionTouche.CopierSeulement, Interpreter(Entree, ctrl: true, maj: true).Action);
    }

    [Theory]
    [InlineData(ZoneFocus.ChampNom)]
    [InlineData(ZoneFocus.Autre)]
    public void Entree_garde_son_role_dans_le_champ_nom_et_sur_les_autres_controles(ZoneFocus zone)
    {
        Assert.Equal(ActionTouche.Aucune, Interpreter(Entree, zone: zone).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(Entree, maj: true, zone: zone).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(Entree, ctrl: true, zone: zone).Action);
    }

    [Theory]
    [InlineData(ZoneFocus.Recherche)]
    [InlineData(ZoneFocus.Liste)]
    [InlineData(ZoneFocus.ChampNom)]
    [InlineData(ZoneFocus.Autre)]
    public void F2_va_au_champ_nom_depuis_partout(ZoneFocus zone)
    {
        Assert.Equal(ActionTouche.AllerAuNom, Interpreter(TableDesTouches.VkF2, zone: zone).Action);
    }

    [Fact]
    public void F2_avec_un_modificateur_garde_son_role()
    {
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkF2, ctrl: true).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkF2, maj: true).Action);
    }

    [Theory]
    [InlineData(ZoneFocus.Recherche)]
    [InlineData(ZoneFocus.Liste)]
    public void Suppr_supprime_l_element_quand_la_recherche_est_vide(ZoneFocus zone)
    {
        Assert.Equal(ActionTouche.SupprimerElement, Interpreter(TableDesTouches.VkSuppr, zone: zone, rechercheVide: true).Action);
    }

    [Theory]
    [InlineData(ZoneFocus.Recherche)]
    [InlineData(ZoneFocus.Liste)]
    public void Suppr_garde_son_role_quand_une_recherche_est_en_cours(ZoneFocus zone)
    {
        // Dans la zone de recherche, Suppr efface un caractère de la saisie.
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkSuppr, zone: zone, rechercheVide: false).Action);
    }

    [Fact]
    public void Suppr_ne_supprime_jamais_depuis_le_champ_nom()
    {
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkSuppr, zone: ZoneFocus.ChampNom, rechercheVide: true).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkSuppr, zone: ZoneFocus.Autre, rechercheVide: true).Action);
    }

    [Fact]
    public void Ctrl_Tab_va_a_l_onglet_suivant_et_Ctrl_Maj_Tab_au_precedent()
    {
        Assert.Equal(ActionTouche.OngletSuivant, Interpreter(TableDesTouches.VkTab, ctrl: true).Action);
        Assert.Equal(ActionTouche.OngletPrecedent, Interpreter(TableDesTouches.VkTab, ctrl: true, maj: true).Action);
    }

    [Theory]
    [InlineData(ZoneFocus.Recherche)]
    [InlineData(ZoneFocus.Liste)]
    [InlineData(ZoneFocus.ChampNom)]
    [InlineData(ZoneFocus.Autre)]
    public void Tab_seul_passe_d_un_controle_a_l_autre(ZoneFocus zone)
    {
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkTab, zone: zone).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkTab, maj: true, zone: zone).Action);
    }

    [Theory]
    [InlineData(0x31, 1)]
    [InlineData(0x35, 5)]
    [InlineData(0x39, 9)]
    [InlineData(0x61, 1)]
    [InlineData(0x65, 5)]
    [InlineData(0x69, 9)]
    public void Ctrl_chiffre_va_a_l_onglet_de_ce_numero(int touche, int numero)
    {
        var commande = Interpreter((uint)touche, ctrl: true);

        Assert.Equal(ActionTouche.AllerAOnglet, commande.Action);
        Assert.Equal(numero, commande.Numero);
    }

    [Theory]
    [InlineData(0x30)]
    [InlineData(0x60)]
    [InlineData(0x6A)]
    public void Ctrl_zero_et_les_touches_voisines_ne_font_rien(int touche)
    {
        Assert.Equal(ActionTouche.Aucune, Interpreter((uint)touche, ctrl: true).Action);
    }

    [Fact]
    public void Un_chiffre_sans_ctrl_est_de_la_saisie()
    {
        Assert.Equal(ActionTouche.Aucune, Interpreter(0x31).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(0x31, maj: true).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(0x31, ctrl: true, maj: true).Action);
    }

    [Fact]
    public void Echap_efface_d_abord_la_recherche()
    {
        Assert.Equal(ActionTouche.EffacerRecherche, Interpreter(TableDesTouches.VkEchap, rechercheVide: false).Action);
    }

    [Fact]
    public void Echap_masque_la_fenetre_quand_la_recherche_est_vide()
    {
        Assert.Equal(ActionTouche.Masquer, Interpreter(TableDesTouches.VkEchap, rechercheVide: true).Action);
    }

    [Theory]
    [InlineData(ZoneFocus.Recherche)]
    [InlineData(ZoneFocus.Liste)]
    [InlineData(ZoneFocus.ChampNom)]
    [InlineData(ZoneFocus.Autre)]
    public void Echap_a_les_deux_temps_depuis_n_importe_quelle_zone(ZoneFocus zone)
    {
        Assert.Equal(ActionTouche.EffacerRecherche, Interpreter(TableDesTouches.VkEchap, zone: zone, rechercheVide: false).Action);
        Assert.Equal(ActionTouche.Masquer, Interpreter(TableDesTouches.VkEchap, zone: zone, rechercheVide: true).Action);
    }

    [Fact]
    public void Les_fleches_pilotent_la_liste_depuis_la_recherche_seulement()
    {
        Assert.Equal(ActionTouche.SelectionPrecedente, Interpreter(TableDesTouches.VkHaut).Action);
        Assert.Equal(ActionTouche.SelectionSuivante, Interpreter(TableDesTouches.VkBas).Action);

        // Dans la liste, le ListView gère lui-même les flèches ; dans le champ Nom, elles déplacent le curseur.
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkHaut, zone: ZoneFocus.Liste).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkBas, zone: ZoneFocus.Liste).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkHaut, zone: ZoneFocus.ChampNom).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter(TableDesTouches.VkBas, zone: ZoneFocus.ChampNom).Action);
    }

    [Theory]
    [InlineData(0x0D)]
    [InlineData(0x1B)]
    [InlineData(0x71)]
    [InlineData(0x2E)]
    [InlineData(0x09)]
    [InlineData(0x31)]
    [InlineData(0x26)]
    public void Une_boite_de_dialogue_ouverte_garde_ses_propres_touches(int touche)
    {
        Assert.Equal(ActionTouche.Aucune, Interpreter((uint)touche, ctrl: touche is 0x09 or 0x31, dialogueOuvert: true).Action);
        Assert.Equal(ActionTouche.Aucune, Interpreter((uint)touche, dialogueOuvert: true, rechercheVide: false).Action);
    }

    [Theory]
    [InlineData(0x0D)]
    [InlineData(0x1B)]
    [InlineData(0x71)]
    [InlineData(0x2E)]
    [InlineData(0x09)]
    [InlineData(0x31)]
    public void Alt_laisse_passer_les_raccourcis_de_menu_de_windows(int touche)
    {
        Assert.Equal(ActionTouche.Aucune, Interpreter((uint)touche, ctrl: touche is 0x09 or 0x31, alt: true).Action);
    }

    [Fact]
    public void Une_touche_inconnue_ne_fait_rien()
    {
        Assert.Equal(new CommandeClavier(ActionTouche.Aucune), Interpreter(0x41));
    }
}
