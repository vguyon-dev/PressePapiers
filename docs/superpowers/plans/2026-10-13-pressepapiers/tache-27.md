### Tâche 27 : Clavier, ouverture et fermeture (AfficherPour, TableDesTouches, collage)

**Fichiers :**
- Créer : `src/PressePapiers.Coeur/Clavier/ZoneFocus.cs`
- Créer : `src/PressePapiers.Coeur/Clavier/ActionTouche.cs`
- Créer : `src/PressePapiers.Coeur/Clavier/CommandeClavier.cs`
- Créer : `src/PressePapiers.Coeur/Clavier/TableDesTouches.cs`
- Créer : `src/PressePapiers/Natif/NatifOuverture.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipaleVue.Collage.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipale.Clavier.cs`
- Modifier : `src/PressePapiers/Interface/VueApercu.xaml.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.xaml` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.xaml.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.Volet.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/Interface/FenetrePrincipale.Affichage.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : `tests/PressePapiers.Tests/Clavier/TableDesTouchesTests.cs` (55 tests, logique pure de la table des touches). Le reste (fenêtre, focus, frappes réelles, collage) se vérifie sur le poste par le banc d'essai : `$ESSAIS/Scenario27.cs`, `$ESSAIS/CibleCollage.cs` et `$ESSAIS/Touches.cs` modifié

**Interfaces :**

- Utilise :
  - tâche 24 : `ServiceRecollage.CollerAsync(Element element, ModeCollage mode, nint fenetreOrigine, bool copieSeule, nint hwndOutil,
    Action masquerFenetre) : Task<IssueCollage>` (écrit, marque utilisé, décide, amène la fenêtre d'origine, masque, attend les
    modificateurs, Ctrl+V ; la fenêtre n'est PAS masquée pour `CopieDemandee`, `CopieSansFenetreOrigine`, `CopieFenetreFermee`,
    `Echec`) ; `IssueCollage` (tâche 12) ;
  - tâche 19 : `PremierPlan.Amener(nint) : bool`, `PremierPlan.FenetreActive() : nint`, `PremierPlan.EstOrigineValide(nint hwnd, nint hwndOutil)` ;
  - tâche 1 : `OrigineOuverture` (`WinV`, `RaccourciSecours`, `Icone`, `Relance`), `ModeCollage` ;
  - tâche 10 : `Parametres.MiseEnFormeParDefaut`, `LargeurFenetre`, `HauteurFenetre` ;
  - tâches 25 et 26 : `FenetrePrincipaleVue` (`Reglages`, `Elements`, `ElementSelectionne`, `Onglets`, `OngletSelectionne`, `ChoisirOnglet`,
    `EffacerRecherche`, `Annoncer`, `DemanderCollage`, `CollageDemande`, `ModeAlternatif`, `SupprimerSelectionAsync`, `Dialogues`),
    `VueApercu.FocaliserNom`, `IDialogues.EnCours`.

- Fournit :
  - dans Coeur (`PressePapiers.Coeur.Clavier`) : `enum ZoneFocus { Recherche, Liste, ChampNom, Autre }`,
    `enum ActionTouche { Aucune, Coller, CollerAutreMode, CopierSeulement, AllerAuNom, SupprimerElement, OngletSuivant,
    OngletPrecedent, AllerAOnglet, EffacerRecherche, Masquer, SelectionPrecedente, SelectionSuivante }`,
    `readonly record struct CommandeClavier(ActionTouche Action, int Numero = 0)`,
    `static class TableDesTouches` avec `CommandeClavier Interpreter(uint touche, bool ctrl, bool maj, bool alt, ZoneFocus zone,
    bool rechercheVide, bool dialogueOuvert)` (logique pure de la table de la spec, section 8) ;
  - `internal static partial class NatifOuverture` (`GetDpiForMonitor`, `GetCursorPos`) ;
  - dans la vue (fichier `FenetrePrincipaleVue.Collage.cs`) : `nint FenetreOrigine { get; set; }`, `nint HandleOutil { get; set; }`,
    `Action MasquerFenetre { get; set; }`, `Action<string, string>? Notifier { get; set; }` (titre, message : notification de l'icône),
    `bool CollageEnCours { get; }`, `Task CollerAsync(ModeCollage mode, bool copieSeule)`, `void AllerAOnglet(int numero)` (1 = Temporaire),
    `void OngletRelatif(int decalage)` (boucle), `void DeplacerSelection(int decalage)`, en interne `Informer(IssueCollage)` et
    `PreparerOuverture(OrigineOuverture, bool messageOrigine = true)` ;
  - dans la fenêtre : `AfficherPour(nint fenetreOrigine, OrigineOuverture origine)` et `Masquer()` (contrat 4.3, plus de coquilles),
    `event Action? PremierPlanRefuse` (Windows a refusé le premier plan : l'application le dit par l'icône),
    `void AutoriserFermeture()` (à appeler avant de quitter : sinon la croix, Alt+F4 et la fermeture système ne font que masquer) ;
  - dans `VueApercu` : `internal bool ContientChampNom(DependencyObject)`.
  - Consommateurs : tâche 28 (`AfficherPour`, `AfficherParametres`), tâche 29 (`AfficherPour` depuis le hook, le raccourci de secours
    et l'icône ; `Notifier`, `PremierPlanRefuse`, `AutoriserFermeture`).

- [ ] **Étape 1 : Écrire les tests, puis la vérification sur le poste**

La table des touches (priorités de Ctrl, Maj, Entrée ; Échap en deux temps ; Suppr selon la recherche) est de la logique pure : elle est
placée dans Coeur pour être testée sans interface. Un test qui prouve une priorité échoue si l'ordre est inversé (trois mutations
jouées : voir l'étape 5).

`tests/PressePapiers.Tests/Clavier/TableDesTouchesTests.cs`

```csharp
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
```

Puis la vérification sur le poste. Le banc reçoit une fenêtre d'origine **réelle** : un contrôle EDIT Win32 de premier niveau, créé sur
son propre fil (`CibleCollage`), dans laquelle l'essai colle pour de vrai (vrai presse-papiers, vrai `SendInput`) et dont il relit le
texte. Aucune application de l'utilisateur n'est touchée. Attention : le banc modifie le presse-papiers réel, et le laisse avec un
texte neutre (« banc d'essai terminé »).

`Touches.cs` gagne le double-clic réel (avec retour du curseur) et la fermeture par la croix (`SC_CLOSE`) : remplacez-le.

`$ESSAIS/Touches.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Banc;

/// <summary>
/// Frappes réelles (SendInput) pour exercer les raccourcis de la fenêtre. Garde-fou : rien n'est envoyé si la fenêtre
/// attendue n'est pas au premier plan, pour ne jamais frapper dans une autre application.
/// </summary>
internal static class Touches
{
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const uint KeyUnicode = 0x0004;

    public const ushort Entree = 0x0D, Echap = 0x1B, Tab = 0x09, Suppr = 0x2E, F2 = 0x71, Haut = 0x26, Bas = 0x28;
    public const ushort Maj = 0x10, Ctrl = 0x11, Alt = 0x12;

    /// <summary>Appuie sur la touche en maintenant les modificateurs, puis relâche tout. Faux si la fenêtre n'est pas au premier plan.</summary>
    public static bool Taper(nint fenetreAttendue, ushort touche, params ushort[] modificateurs)
    {
        if (GetForegroundWindow() != fenetreAttendue)
        {
            return false;
        }

        var entrees = new List<Frappe>();
        foreach (var m in modificateurs)
        {
            entrees.Add(Clavier(m, 0, 0));
        }

        entrees.Add(Clavier(touche, 0, 0));
        entrees.Add(Clavier(touche, 0, KeyUp));
        foreach (var m in modificateurs.Reverse())
        {
            entrees.Add(Clavier(m, 0, KeyUp));
        }

        return Envoyer(entrees);
    }

    /// <summary>Saisit un texte caractère par caractère (frappes Unicode). Faux si la fenêtre n'est pas au premier plan.</summary>
    public static bool Ecrire(nint fenetreAttendue, string texte)
    {
        if (GetForegroundWindow() != fenetreAttendue)
        {
            return false;
        }

        var entrees = new List<Frappe>();
        foreach (var c in texte)
        {
            entrees.Add(Clavier(0, c, KeyUnicode));
            entrees.Add(Clavier(0, c, KeyUnicode | KeyUp));
        }

        return Envoyer(entrees);
    }

    /// <summary>
    /// Double-clic gauche à un point de l'écran, puis retour du curseur là où il était. Faux si la fenêtre n'est pas
    /// au premier plan : aucun clic n'est envoyé.
    /// </summary>
    public static bool DoubleClic(nint fenetreAttendue, int x, int y)
    {
        if (GetForegroundWindow() != fenetreAttendue || !GetCursorPos(out var avant))
        {
            return false;
        }

        SetCursorPos(x, y);
        var bas = new Frappe { Type = 0, IndicSouris = 0x0002 };
        var haut = new Frappe { Type = 0, IndicSouris = 0x0004 };
        var ok = Envoyer([bas, haut, bas, haut]);
        Thread.Sleep(150);
        SetCursorPos(avant.X, avant.Y);
        return ok;
    }

    /// <summary>Envoie SC_CLOSE à la fenêtre : ce que fait un clic sur la croix ou Alt+F4.</summary>
    public static void FermerParLaCroix(nint fenetre) => PostMessage(fenetre, 0x0112, 0xF060, 0);

    private static Frappe Clavier(ushort touche, ushort caractere, uint indicateurs) =>
        new() { Type = InputKeyboard, Touche = touche, Caractere = caractere, Indicateurs = indicateurs };

    private static bool Envoyer(List<Frappe> entrees)
    {
        var tableau = entrees.ToArray();
        return SendInput((uint)tableau.Length, tableau, Marshal.SizeOf<Frappe>()) == tableau.Length;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Frappe
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Touche;
        [FieldOffset(10)] public ushort Caractere;
        [FieldOffset(12)] public uint Indicateurs;
        [FieldOffset(20)] public uint IndicSouris;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nombre, Frappe[] entrees, int taille);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint fenetre, uint message, nint w, nint l);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point position);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
}
```

`$ESSAIS/CibleCollage.cs`

```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace PressePapiers.Banc;

/// <summary>
/// Fenêtre Win32 ordinaire (un contrôle EDIT de premier niveau, sur son propre fil) qui sert d'application d'origine :
/// l'essai y colle pour de vrai et relit son texte. Aucune application de l'utilisateur n'est touchée.
/// </summary>
internal sealed class CibleCollage : IDisposable
{
    private const uint WsOverlappedWindow = 0x00CF0000, WsVisible = 0x10000000, EsMultiline = 0x0004, EsAutoVScroll = 0x0040;
    private const uint WmGetText = 0x000D, WmGetTextLength = 0x000E, WmSetText = 0x000C, WmClose = 0x0010, WmQuit = 0x0012;

    private readonly Thread _fil;
    private readonly ManualResetEventSlim _prete = new();
    private uint _idFil;

    public CibleCollage(int x, int y)
    {
        _fil = new Thread(() =>
        {
            _idFil = GetCurrentThreadId();
            Fenetre = CreateWindowEx(0, "EDIT", "Cible de collage du banc", WsOverlappedWindow | WsVisible | EsMultiline | EsAutoVScroll,
                                     x, y, 520, 320, 0, 0, 0, 0);
            _prete.Set();
            while (GetMessage(out var message, 0, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        })
        { IsBackground = true, Name = "CibleCollage" };
        _fil.Start();
        _prete.Wait();
    }

    public nint Fenetre { get; private set; }

    public bool Existe => IsWindow(Fenetre);

    public string Texte()
    {
        var longueur = (int)SendMessage(Fenetre, WmGetTextLength, 0, 0);
        var tampon = new StringBuilder(longueur + 1);
        SendMessage(Fenetre, WmGetText, longueur + 1, tampon);
        return tampon.ToString();
    }

    public void Vider() => SendMessage(Fenetre, WmSetText, 0, "");

    /// <summary>Ferme la fenêtre, comme si l'utilisateur l'avait fermée entre l'ouverture et le collage.</summary>
    public void Fermer() => PostMessage(Fenetre, WmClose, 0, 0);

    public void Dispose()
    {
        Fermer();
        PostThreadMessage(_idFil, WmQuit, 0, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Fenetre;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Heure;
        public int X, Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint styleEtendu, string classe, string titre, uint style, int x, int y, int l, int h,
                                              nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")] private static extern int GetMessage(out Msg message, nint fenetre, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Msg message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Msg message);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint fenetre);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint fenetre, uint message, nint w, nint l);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint fil, uint message, nint w, nint l);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint fenetre, uint message, nint w, nint l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessage(nint fenetre, uint message, nint w, StringBuilder l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessage(nint fenetre, uint message, nint w, string l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}

/// <summary>Lecture du presse-papiers réel, sans passer par le code de l'application : formats présents et texte.</summary>
internal static class PressePapiersBanc
{
    private const uint CfUnicodeText = 13;

    public static bool FormatPresent(string nom) => IsClipboardFormatAvailable(RegisterClipboardFormat(nom));

    public static string? Texte(nint fenetre)
    {
        for (var essai = 0; essai < 20; essai++)
        {
            if (OpenClipboard(fenetre))
            {
                try
                {
                    var poignee = GetClipboardData(CfUnicodeText);
                    if (poignee == 0)
                    {
                        return null;
                    }

                    var pointeur = GlobalLock(poignee);
                    try
                    {
                        return Marshal.PtrToStringUni(pointeur);
                    }
                    finally
                    {
                        GlobalUnlock(poignee);
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }

            Thread.Sleep(25);
        }

        return null;
    }

    [DllImport("user32.dll")] private static extern bool OpenClipboard(nint fenetre);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern nint GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string nom);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint poignee);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint poignee);
}
```

`$ESSAIS/Scenario27.cs`

```csharp
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Modele;
using PressePapiers.Interface;
using PressePapiers.Systeme;

namespace PressePapiers.Banc;

/// <summary>
/// Tâche 27 : ouverture (centrage, premier plan, onglet, focus), table des touches avec de vraies frappes, collage réel dans
/// une fenêtre d'essai, copie seule, ouverture depuis l'icône, fermeture sur perte du focus, croix.
/// </summary>
internal static class Scenario27
{
    public static async Task ExecuterAsync(ContexteBanc c)
    {
        var vue = c.Vue;
        var fenetre = c.Fenetre;
        var handle = fenetre.Handle;
        var annonce = c.Controle<InfoBar>("Annonce");
        var liste = c.Controle<ListView>("Liste");
        var champ = c.Controle<AutoSuggestBox>("ChampRecherche");
        var navigation = c.Controle<NavigationView>("Navigation");
        var apercu = c.Controle<VueApercu>("PanneauApercu");
        var champNom = (TextBox)apercu.FindName("ChampNom");

        string Focus()
        {
            var courant = FocusManager.GetFocusedElement(fenetre.Content.XamlRoot) as DependencyObject;
            for (var n = courant; n is not null; n = VisualTreeHelper.GetParent(n))
            {
                if (ReferenceEquals(n, champ)) return "recherche";
                if (ReferenceEquals(n, liste)) return "liste";
                if (ReferenceEquals(n, champNom)) return "nom";
            }

            return courant?.GetType().Name ?? "(aucun)";
        }

        async Task<bool> Frappe(ushort touche, params ushort[] modificateurs)
        {
            var envoyee = Touches.Taper(handle, touche, modificateurs);
            if (!envoyee)
            {
                c.Ecrire($"ECHEC  frappe {touche:X2} non envoyée : premier plan {PremierPlan.FenetreActive():X}, fenêtre {handle:X}, visible {fenetre.EstVisible}");
                c.Echecs++;
            }

            await c.Attendre(300);
            return envoyee;
        }

        // Une fenêtre ordinaire sert d'application d'origine, à gauche de l'écran principal.
        using var cible = new CibleCollage(40, 80);
        var neutre = PremierPlan.Amener(cible.Fenetre);
        await c.Attendre(400);
        c.Verifier(neutre && PremierPlan.FenetreActive() == cible.Fenetre, "la fenêtre d'essai est au premier plan avant l'ouverture");

        // --- Première ouverture : fenêtre jamais activée, affichée sans Activate.
        c.Verifier(!fenetre.EstVisible, "avant l'ouverture : fenêtre masquée");
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
        await c.Attendre(40);

        // Capture sans rien demander à la fenêtre : on voit ce que l'utilisateur voit juste après Win+V.
        await c.CapturerAsync("27-premiere-ouverture.png", amener: false);
        await c.Attendre(300);
        c.Ecrire($"INFO   EstVisible {fenetre.EstVisible}, Affichee {vue.Affichee}, premier plan {PremierPlan.FenetreActive():X} (fenêtre {handle:X}, cible {cible.Fenetre:X})");
        c.Verifier(fenetre.EstVisible && vue.Affichee, "AfficherPour : fenêtre visible");
        c.Verifier(PremierPlan.FenetreActive() == handle, "AfficherPour : la fenêtre est au premier plan, depuis une autre application");
        c.Verifier(vue.FenetreOrigine == cible.Fenetre, "la fenêtre d'origine est notée");
        c.Verifier(navigation.IsLoaded && liste.ActualHeight > 100, "le contenu est affiché dès la première ouverture (liste mise en page)");
        c.Verifier(vue.OngletSelectionne?.EstTemporaire == true && vue.Recherche.Length == 0 && vue.ElementSelectionne?.Id == vue.Elements[0].Id,
                   "ouverture : onglet Temporaire, recherche vide, premier élément choisi");
        c.Verifier(Focus() == "recherche", $"le curseur est dans la recherche (focus : {Focus()})");
        var ecran = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(cible.Fenetre), DisplayAreaFallback.Nearest);
        var t = fenetre.AppWindow.Size;
        var p = fenetre.AppWindow.Position;
        var centreX = p.X + t.Width / 2;
        var centreY = p.Y + t.Height / 2;
        var travail = ecran.WorkArea;
        c.Verifier(Math.Abs(centreX - (travail.X + travail.Width / 2)) <= 2 && Math.Abs(centreY - (travail.Y + travail.Height / 2)) <= 2,
                   $"centrée sur l'écran de la fenêtre d'origine (centre {centreX},{centreY} ; écran {travail.X + travail.Width / 2},{travail.Y + travail.Height / 2})");
        c.Verifier(t.Width == (int)Math.Round(920 * c.Echelle) && t.Height == (int)Math.Round(560 * c.Echelle), $"taille mémorisée appliquée ({t.Width} x {t.Height})");
        await c.CapturerAsync("27-ouverture.png");

        // --- Ouverture idempotente : un second signal, fenêtre affichée, est ignoré.
        var origineAvant = vue.FenetreOrigine;
        fenetre.AfficherPour(0, OrigineOuverture.RaccourciSecours);
        await c.Attendre(200);
        c.Verifier(fenetre.EstVisible && vue.FenetreOrigine == origineAvant && !annonce.IsOpen, "second signal pendant l'affichage : ignoré, rien ne change");

        // --- Plusieurs écrans : centrée sur l'écran de la fenêtre d'origine.
        var ecrans = DisplayArea.FindAll();
        c.Ecrire($"INFO   écrans détectés : {ecrans.Count}");
        if (ecrans.Count > 1)
        {
            for (var i = 0; i < ecrans.Count; i++)
            {
                var autre = ecrans[i];
                if (autre.DisplayId.Value == ecran.DisplayId.Value)
                {
                    continue;
                }

                fenetre.Masquer();
                await c.Attendre(200);
                using var cibleLointaine = new CibleCollage(autre.WorkArea.X + 60, autre.WorkArea.Y + 60);
                fenetre.AfficherPour(cibleLointaine.Fenetre, OrigineOuverture.WinV);
                await c.Attendre(700);
                var q = fenetre.AppWindow.Position;
                var s = fenetre.AppWindow.Size;
                c.Verifier(Math.Abs(q.X + s.Width / 2 - (autre.WorkArea.X + autre.WorkArea.Width / 2)) <= 3, "autre écran : fenêtre centrée sur cet écran");
            }

            fenetre.Masquer();
            await c.Attendre(200);
            PremierPlan.Amener(cible.Fenetre);
            await c.Attendre(300);
            fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
            await c.Attendre(600);
        }

        // --- Saisie réelle dans la recherche, flèches, Échap en deux temps.
        c.Verifier(Touches.Ecrire(handle, "quai"), "frappes envoyées (fenêtre au premier plan)");
        await c.Attendre(500);
        c.Verifier(vue.Recherche == "quai" && vue.EnRecherche && vue.Elements.Count > 0, $"saisie : « {vue.Recherche} » lance la recherche ({vue.Elements.Count} résultats)");
        await c.Attendre(100);
        await Frappe(Touches.Echap);
        c.Verifier(fenetre.EstVisible && vue.Recherche.Length == 0 && vue.Elements.Count == vue.Onglets[0].Compteur, "Échap, premier temps : la recherche est effacée, la fenêtre reste");
        await Frappe(Touches.Bas);
        c.Verifier(vue.ElementSelectionne?.Id == vue.Elements[1].Id, "flèche bas depuis la recherche : élément suivant");
        await Frappe(Touches.Bas);
        await Frappe(Touches.Haut);
        c.Verifier(vue.ElementSelectionne?.Id == vue.Elements[1].Id, "flèche haut : élément précédent");
        await Frappe(Touches.Haut);
        await Frappe(Touches.Haut);
        c.Verifier(vue.ElementSelectionne?.Id == vue.Elements[0].Id, "flèche haut au premier élément : la sélection s'arrête");
        c.Verifier(Focus() == "recherche", "les flèches ne sortent pas le focus de la recherche");

        // --- Onglets au clavier.
        await Frappe(Touches.Tab, Touches.Ctrl);
        c.Verifier(vue.OngletSelectionne?.Nom == "Code", "Ctrl+Tab : onglet suivant (Code)");
        await Frappe(Touches.Tab, Touches.Ctrl, Touches.Maj);
        c.Verifier(vue.OngletSelectionne?.EstTemporaire == true, "Ctrl+Maj+Tab : onglet précédent (Temporaire)");
        await Frappe(Touches.Tab, Touches.Ctrl, Touches.Maj);
        c.Verifier(vue.OngletSelectionne?.Nom == vue.Onglets[^1].Nom, "Ctrl+Maj+Tab depuis Temporaire : retour au dernier onglet");
        await Frappe(Touches.Tab, Touches.Ctrl);
        c.Verifier(vue.OngletSelectionne?.EstTemporaire == true, "Ctrl+Tab depuis le dernier : retour à Temporaire");
        await Frappe(0x33, Touches.Ctrl);
        c.Verifier(vue.OngletSelectionne?.Nom == "Adresses", "Ctrl+3 : troisième onglet (Adresses)");
        c.Verifier(ReferenceEquals(navigation.SelectedItem, navigation.MenuItems.OfType<NavigationViewItem>().First(i => i.Tag is OngletVue { Nom: "Adresses" })), "le volet suit l'onglet choisi au clavier");
        await Frappe(0x39, Touches.Ctrl);
        c.Verifier(vue.OngletSelectionne?.Nom == "Adresses", "Ctrl+9 sans neuvième onglet : rien ne change");
        await Frappe(0x31, Touches.Ctrl);
        c.Verifier(vue.OngletSelectionne?.EstTemporaire == true, "Ctrl+1 : Temporaire");

        // --- F2, Entrée dans le champ Nom (n'enregistre pas sans onglet, ne colle jamais), Échap depuis le champ Nom.
        await Frappe(Touches.F2);
        c.Verifier(Focus() == "nom", $"F2 : le curseur va au champ Nom (focus : {Focus()})");
        cible.Vider();
        await Frappe(Touches.Entree);
        c.Verifier(cible.Texte().Length == 0 && annonce.IsOpen && annonce.Severity == InfoBarSeverity.Warning,
                   "Entrée dans le champ Nom : avertissement d'enregistrement, aucun collage");
        champ.Focus(FocusState.Programmatic);
        await c.Attendre(200);

        // --- Suppr : supprime l'élément recherche vide, efface un caractère sinon.
        var supprime = c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte, Texte = "élément à supprimer au clavier", Application = "msedge" });
        await c.Attendre(300);
        c.Verifier(vue.Elements[0].Id == supprime.Id, "la copie la plus récente est en tête de la liste");
        vue.ElementSelectionne = vue.Elements.First(e => e.Id == supprime.Id);
        await c.Attendre(200);
        Touches.Ecrire(handle, "z");
        await c.Attendre(400);
        var nombreAvant = c.Depot.CompterElements(c.Depot.OngletTemporaire.Id);
        await Frappe(Touches.Suppr);
        c.Verifier(c.Depot.Lire(supprime.Id) is not null && vue.Recherche == "z", "Suppr avec une recherche en cours : l'élément reste (le caractère est édité)");
        await Frappe(Touches.Echap);
        vue.ElementSelectionne = vue.Elements.First(e => e.Id == supprime.Id);
        await c.Attendre(200);
        await Frappe(Touches.Suppr);
        await c.Attendre(300);
        c.Verifier(c.Depot.Lire(supprime.Id) is null && c.Depot.CompterElements(c.Depot.OngletTemporaire.Id) == nombreAvant - 1,
                   "Suppr, recherche vide : l'élément temporaire est supprimé sans confirmation");

        // --- Saisie depuis la liste : le caractère part dans la recherche, qui reprend le focus.
        liste.Focus(FocusState.Programmatic);
        await c.Attendre(200);
        c.Verifier(Focus() == "liste", $"le focus est dans la liste (focus : {Focus()})");
        Touches.Ecrire(handle, "q");
        await c.Attendre(500);
        c.Verifier(vue.Recherche == "q" && Focus() == "recherche", $"une touche tapée depuis la liste part dans la recherche (recherche « {vue.Recherche} », focus : {Focus()})");
        await Frappe(Touches.Echap);
        c.Verifier(vue.Recherche.Length == 0 && fenetre.EstVisible, "Échap efface cette saisie");

        // --- Collage réel : Entrée colle dans la fenêtre d'origine, la fenêtre se masque, l'élément remonte en tête.
        var texteA = c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte, Texte = "COLLAGE-A-1234", Application = "notepad" });
        await c.Attendre(150);
        var texteB = c.Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte, Texte = "COLLAGE-B-5678", Application = "notepad" });
        await c.Attendre(300);
        vue.ElementSelectionne = vue.Elements.First(e => e.Id == texteA.Id);
        await c.Attendre(200);
        cible.Vider();
        await Frappe(Touches.Entree);
        await c.Attendre(1500);
        c.Verifier(cible.Texte() == "COLLAGE-A-1234", $"Entrée : le texte est collé dans la fenêtre d'origine (reçu : « {cible.Texte()} »)");
        c.Verifier(!fenetre.EstVisible, "après le collage, la fenêtre est masquée");
        c.Verifier(PremierPlan.FenetreActive() == cible.Fenetre, "après le collage, la fenêtre d'origine a le premier plan");
        c.Verifier(c.Depot.ListerElements(c.Depot.OngletTemporaire.Id)[0].Id == texteA.Id, "l'élément collé remonte en tête de son onglet");
        c.Verifier(!PressePapiersBanc.FormatPresent("Rich Text Format"), "texte seul : pas de RTF dans le presse-papiers");

        // --- Ré-ouverture : le plus récent est en tête, la recherche est vide, Temporaire est affiché.
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.RaccourciSecours);
        await c.Attendre(700);
        c.Verifier(fenetre.EstVisible && PremierPlan.FenetreActive() == handle, "réouverture par le raccourci de secours : au premier plan");
        c.Verifier(vue.ElementSelectionne?.Id == texteA.Id && vue.OngletSelectionne?.EstTemporaire == true, "réouverture : l'élément collé est le premier, choisi");

        // --- Maj+Entrée : l'autre mode de mise en forme (avec RTF).
        var riche = vue.Elements.First(e => e.Element.Type == TypeElement.MisEnForme && e.Element.Application == "OUTLOOK");
        vue.ElementSelectionne = riche;
        await c.Attendre(200);
        cible.Vider();
        await Frappe(Touches.Entree, Touches.Maj);
        await c.Attendre(1500);
        c.Verifier(cible.Texte().Contains("Lefebvre-Dumontier") && PressePapiersBanc.FormatPresent("Rich Text Format"),
                   "Maj+Entrée sur un élément mis en forme : mode alterné, RTF dans le presse-papiers");
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
        await c.Attendre(700);
        vue.ElementSelectionne = vue.Elements.First(e => e.Id == riche.Id);
        await c.Attendre(200);
        cible.Vider();
        await Frappe(Touches.Entree);
        await c.Attendre(1500);
        c.Verifier(cible.Texte().Contains("Lefebvre-Dumontier") && !PressePapiersBanc.FormatPresent("Rich Text Format"),
                   "Entrée sur le même élément : mode par défaut, texte brut seulement");

        // --- Ctrl+Entrée : copie seule, la fenêtre reste, message de réussite.
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
        await c.Attendre(700);
        vue.ElementSelectionne = vue.Elements.First(e => e.Id == texteB.Id);
        await c.Attendre(200);
        cible.Vider();
        await Frappe(Touches.Entree, Touches.Ctrl);
        await c.Attendre(800);
        c.Verifier(fenetre.EstVisible && cible.Texte().Length == 0, "Ctrl+Entrée : rien n'est collé, la fenêtre reste affichée");
        c.Verifier(PressePapiersBanc.Texte(c.HandleRelais) == "COLLAGE-B-5678", "Ctrl+Entrée : l'élément est dans le presse-papiers");
        c.Verifier(annonce.IsOpen && annonce.Severity == InfoBarSeverity.Success && annonce.Message == "Copié dans le presse-papiers.", $"InfoBar : « {annonce.Message} »");

        // --- Double-clic sur un élément : colle.
        PremierPlan.Amener(handle);
        await c.Attendre(300);
        var ligneCible = vue.Elements.First(e => e.Id == texteA.Id);
        var conteneur = (FrameworkElement)liste.ContainerFromItem(ligneCible);
        var point = conteneur.TransformToVisual(fenetre.Content).TransformPoint(new Windows.Foundation.Point(60, 12));
        var fen = fenetre.AppWindow.Position;
        var echelle = c.Echelle;
        // Position écran = coin de la fenêtre + décalage du contenu (barre de titre étendue : le contenu commence au coin) + point.
        var x = (int)(fen.X + point.X * echelle);
        var y = (int)(fen.Y + point.Y * echelle);
        cible.Vider();
        if (Touches.DoubleClic(handle, x, y))
        {
            await c.Attendre(1800);
            c.Verifier(cible.Texte() == "COLLAGE-A-1234" && !fenetre.EstVisible, $"double-clic sur un élément : collé (reçu : « {cible.Texte()} »)");
        }
        else
        {
            c.Ecrire("NON TESTE  double-clic : fenêtre non au premier plan");
        }

        // --- Ouverture depuis l'icône : pas de fenêtre d'origine, copie seulement, l'InfoBar le dit.
        fenetre.AfficherPour(0, OrigineOuverture.Icone);
        await c.Attendre(700);
        c.Verifier(fenetre.EstVisible && vue.FenetreOrigine == 0 && annonce.IsOpen && annonce.Message.StartsWith("Ouvert sans fenêtre d'origine"),
                   $"ouverture depuis l'icône : message d'information « {annonce.Message} »");
        cible.Vider();
        await Frappe(Touches.Entree);
        await c.Attendre(800);
        c.Verifier(fenetre.EstVisible && cible.Texte().Length == 0 && annonce.Message.StartsWith("Copié. La fenêtre d'origine est inconnue"),
                   $"Entrée depuis l'icône : copie seule, fenêtre affichée, « {annonce.Message} »");
        var origineIcone = vue.FenetreOrigine;
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.Relance);
        await c.Attendre(400);
        c.Verifier(fenetre.EstVisible && vue.FenetreOrigine == origineIcone && PremierPlan.FenetreActive() == handle, "relance pendant l'affichage : la fenêtre est remise devant, l'état est conservé");

        // --- Fenêtre d'origine fermée entre-temps.
        fenetre.Masquer();
        await c.Attendre(300);
        using (var ephemere = new CibleCollage(560, 120))
        {
            PremierPlan.Amener(ephemere.Fenetre);
            await c.Attendre(300);
            fenetre.AfficherPour(ephemere.Fenetre, OrigineOuverture.WinV);
            await c.Attendre(700);
            ephemere.Fermer();
            await c.Attendre(400);
            PremierPlan.Amener(handle);
            await c.Attendre(300);
            await Frappe(Touches.Entree);
            await c.Attendre(800);
            c.Verifier(fenetre.EstVisible && annonce.Severity == InfoBarSeverity.Warning && annonce.Message.Contains("n'existe plus"),
                       $"fenêtre d'origine fermée : copie seule, « {annonce.Message} »");
        }

        // --- Cible élevée : le message passe par la notification de l'icône (le Gestionnaire des tâches ne se pilote pas ici).
        (string Titre, string Message)? notification = null;
        vue.Notifier = (titre, message) => notification = (titre, message);
        vue.Informer(IssueCollage.CopieFenetreElevee);
        c.Verifier(notification == ("Presse-papiers", "Copié, colle avec Ctrl+V"), "cible élevée : notification « Copié, colle avec Ctrl+V »");

        // --- Focus perdu pendant l'ouverture même : la fenêtre ne reste pas ouverte sans focus.
        fenetre.Masquer();
        await c.Attendre(300);
        PremierPlan.Amener(cible.Fenetre);
        await c.Attendre(300);
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
        PremierPlan.Amener(cible.Fenetre);
        await c.Attendre(800);
        c.Verifier(!fenetre.EstVisible, "premier plan repris par une autre fenêtre pendant l'ouverture : la fenêtre se masque");

        // --- Perte du focus : la fenêtre se masque, sauf pendant une boîte de dialogue.
        fenetre.Masquer();
        await c.Attendre(300);
        PremierPlan.Amener(cible.Fenetre);
        await c.Attendre(300);
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
        await c.Attendre(800);
        c.Verifier(fenetre.EstVisible, "affichée, la fenêtre reste ouverte tant que rien ne change");
        PremierPlan.Amener(cible.Fenetre);
        await c.Attendre(600);
        c.Verifier(!fenetre.EstVisible, "perte du focus : la fenêtre se masque");

        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
        await c.Attendre(800);
        _ = vue.NouvelOngletAsync();
        await c.Attendre(500);
        c.Verifier(vue.Dialogues!.EnCours, "boîte « Nouvel onglet » ouverte");
        PremierPlan.Amener(cible.Fenetre);
        await c.Attendre(700);
        c.Verifier(fenetre.EstVisible && vue.Dialogues.EnCours, "perte du focus pendant une boîte de dialogue : la fenêtre reste ouverte");
        PremierPlan.Amener(handle);
        await c.Attendre(300);
        ContexteBanc.Cliquer(c.BoutonBoite("CloseButton")!);
        await c.Attendre(400);
        c.Verifier(!vue.Dialogues.EnCours && fenetre.EstVisible, "boîte fermée par Annuler : la fenêtre reste ouverte");

        // --- Échap : deuxième temps, la fenêtre se masque ; Échap dans une boîte ne masque pas la fenêtre.
        _ = vue.NouvelOngletAsync();
        await c.Attendre(500);
        if (Touches.Taper(handle, Touches.Echap))
        {
            await c.Attendre(500);
            c.Verifier(!vue.Dialogues.EnCours && fenetre.EstVisible, "Échap dans une boîte de dialogue : la boîte se ferme, la fenêtre reste");
        }

        champ.Focus(FocusState.Programmatic);
        await c.Attendre(200);
        await Frappe(Touches.Echap);
        c.Verifier(!fenetre.EstVisible, "Échap recherche vide : la fenêtre se masque");

        // --- La croix : la fenêtre se masque, l'application continue.
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
        await c.Attendre(800);
        Touches.FermerParLaCroix(handle);
        await c.Attendre(400);
        c.Verifier(!fenetre.EstVisible, "croix (SC_CLOSE, comme un clic sur la croix ou Alt+F4) : Closing est annulé, la fenêtre se masque");
        fenetre.AfficherPour(cible.Fenetre, OrigineOuverture.WinV);
        await c.Attendre(800);
        c.Verifier(fenetre.EstVisible && PremierPlan.FenetreActive() == handle, "après la croix, la fenêtre se rouvre normalement");
        fenetre.Masquer();
        await c.Attendre(300);

        // Texte neutre dans le presse-papiers : les essais l'ont modifié.
        await new PressePapiers.Capture.EcrivainPressePapiers(c.HandleRelais, c.Journal)
            .EcrireAsync(c.Depot.Lire(texteB.Id)! with { Texte = "banc d'essai terminé" }, ModeCollage.TexteSeul, null);
        c.Verifier(c.Journal.Erreurs.Count == 0, $"aucune erreur au journal ({c.Journal.Erreurs.Count}) ; avertissements : {c.Journal.Avertissements.Count}");
    }
}
```

- [ ] **Étape 2 : Lancer les tests et le banc, et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 27
```

Résultat attendu : la compilation des tests échoue (`CS0246` : `ZoneFocus` introuvable, `TableDesTouches` introuvable) ; le banc échoue
aussi à la compilation (`CS1061` : `'FenetrePrincipaleVue' ne contient pas de définition pour 'FenetreOrigine'`).

- [ ] **Étape 3 : Implémenter**

Décisions et pièges à connaître :
- **Ouverture** (`AfficherPour`) : `AppWindow.Show()` suffit, même pour une fenêtre jamais activée : le contenu est complet dès la
  première capture (spec, section 16, point 2 : un premier `Show(false)` puis `Hide` n'est PAS nécessaire ; le banc le vérifie par une
  capture 400 ms après l'appel, et par les réouvertures suivantes depuis une autre application, toujours au premier plan). Ouverture
  idempotente : fenêtre affichée ou en cours d'ouverture, un signal est ignoré ; seuls l'icône et la relance remettent la fenêtre
  devant. Le centrage prend l'écran de la fenêtre d'origine (`DisplayArea.GetFromWindowId`), sinon l'écran du curseur, sinon l'écran
  principal ; la taille mémorisée est en pixels logiques et se multiplie par l'échelle **de cet écran** (`GetDpiForMonitor`), pas par
  celle de l'écran où la fenêtre se trouvait avant (banc : trois écrans détectés, fenêtre centrée sur chacun).
- **Perte du focus** : l'événement `Activated` (`Deactivated`) masque la fenêtre, sauf pendant l'ouverture, un recollage
  (`CollageEnCours`) ou une boîte de dialogue. Les pertes de focus survenues pendant l'ouverture sont ignorées, donc la fin de
  l'ouverture (rappel de priorité basse) vérifie que la fenêtre est toujours au premier plan et la masque sinon (mutation sans ce
  rattrapage : échec). Si Windows refuse le premier plan, `PremierPlanRefuse` est levé et la fenêtre reste affichée.
- **Clavier** : `PreviewKeyDown` de la racine (tunnel : il passe avant les contrôles) consulte `TableDesTouches` avec les modificateurs
  lus par `InputKeyboardSource` et la zone du focus (recherche, liste, champ Nom, autre). Entrée dans le champ Nom enregistre (tâche 26)
  et sur un bouton l'active : la table ne la prend que depuis la recherche et la liste. Sur la page Paramètres (tâche 28), seuls Échap
  et le changement d'onglet agissent. Une touche tapée depuis la liste part dans la recherche (`CharacterReceived`).
- **Collage** : le message dépend de l'issue (`Colle` : la fenêtre s'est masquée, rien à dire ; `CopieDemandee` : « Copié dans le
  presse-papiers. » ; `CopieSansFenetreOrigine`, `CopieFenetreFermee` : `InfoBar` ; `CopieFenetreElevee` : la fenêtre est déjà masquée,
  donc `Notifier`, la notification de l'icône, branchée à la tâche 29 ; `Echec` : `InfoBar` d'erreur). Ouverte depuis l'icône ou par
  une relance, la fenêtre n'a pas de fenêtre d'origine fiable : un message l'annonce dès l'ouverture et Entrée copie seulement.
- **Croix** : `AppWindow.Closing` est annulé puis la fenêtre est masquée. `Window.Close()` ne passe PAS par `AppWindow.Closing` et
  met fin à l'application quand `DispatcherShutdownMode` vaut son défaut (`OnLastWindowClose`) : la tâche 29 doit régler
  `OnExplicitShutdown` (spec, section 8) et appeler `AutoriserFermeture()` avant `Application.Exit`. Le banc simule la croix par
  `SC_CLOSE`.
- `NatifOuverture` est un fichier natif ajouté par cette tâche (une brique, un fichier) : `GetDpiForMonitor` (`shcore.dll`) et
  `GetCursorPos`.

`src/PressePapiers.Coeur/Clavier/ZoneFocus.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>Où se trouve le focus clavier dans la fenêtre : de lui dépend le sens d'une touche comme Entrée ou Suppr.</summary>
public enum ZoneFocus
{
    /// <summary>La zone de recherche.</summary>
    Recherche,

    /// <summary>La liste des éléments.</summary>
    Liste,

    /// <summary>Le champ Nom de l'aperçu, qui a ses propres touches (Entrée enregistre).</summary>
    ChampNom,

    /// <summary>Tout autre contrôle (boutons, liste d'onglets, volet) : la touche garde son rôle normal.</summary>
    Autre,
}
```

`src/PressePapiers.Coeur/Clavier/ActionTouche.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>Ce que la fenêtre fait d'une touche (spec, section 8).</summary>
public enum ActionTouche
{
    /// <summary>La touche garde son rôle normal.</summary>
    Aucune,

    /// <summary>Entrée : coller dans la fenêtre d'origine, avec le mode de mise en forme des réglages.</summary>
    Coller,

    /// <summary>Maj+Entrée : coller dans l'autre mode de mise en forme.</summary>
    CollerAutreMode,

    /// <summary>Ctrl+Entrée : copier seulement, sans coller.</summary>
    CopierSeulement,

    /// <summary>F2 : aller au champ Nom.</summary>
    AllerAuNom,

    /// <summary>Suppr : supprimer l'élément choisi.</summary>
    SupprimerElement,

    /// <summary>Ctrl+Tab : onglet suivant.</summary>
    OngletSuivant,

    /// <summary>Ctrl+Maj+Tab : onglet précédent.</summary>
    OngletPrecedent,

    /// <summary>Ctrl+1 à Ctrl+9 : aller à l'onglet dont le numéro est dans <see cref="CommandeClavier.Numero"/>.</summary>
    AllerAOnglet,

    /// <summary>Échap avec une recherche en cours : effacer la recherche.</summary>
    EffacerRecherche,

    /// <summary>Échap sans recherche : masquer la fenêtre.</summary>
    Masquer,

    /// <summary>Flèche haut depuis la zone de recherche : élément précédent de la liste.</summary>
    SelectionPrecedente,

    /// <summary>Flèche bas depuis la zone de recherche : élément suivant de la liste.</summary>
    SelectionSuivante,
}
```

`src/PressePapiers.Coeur/Clavier/CommandeClavier.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>Résultat de <see cref="TableDesTouches.Interpreter"/> : l'action, et pour <see cref="ActionTouche.AllerAOnglet"/> le numéro d'onglet (1 à 9).</summary>
/// <param name="Action">Action à faire.</param>
/// <param name="Numero">Numéro d'onglet, de 1 à 9 ; zéro pour toute autre action.</param>
public readonly record struct CommandeClavier(ActionTouche Action, int Numero = 0);
```

`src/PressePapiers.Coeur/Clavier/TableDesTouches.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>
/// Table des touches de la fenêtre principale (spec, section 8), sans aucune dépendance à WinUI : la fenêtre lui donne
/// la touche, les modificateurs et l'endroit du focus, elle répond par l'action à faire.
/// </summary>
public static class TableDesTouches
{
    /// <summary>Code virtuel d'Entrée.</summary>
    public const uint VkEntree = 0x0D;

    /// <summary>Code virtuel de Tab.</summary>
    public const uint VkTab = 0x09;

    /// <summary>Code virtuel d'Échap.</summary>
    public const uint VkEchap = 0x1B;

    /// <summary>Code virtuel de la flèche haut.</summary>
    public const uint VkHaut = 0x26;

    /// <summary>Code virtuel de la flèche bas.</summary>
    public const uint VkBas = 0x28;

    /// <summary>Code virtuel de Suppr.</summary>
    public const uint VkSuppr = 0x2E;

    /// <summary>Code virtuel de F2.</summary>
    public const uint VkF2 = 0x71;

    /// <summary>
    /// Dit ce que fait une touche. Rien ne se passe pendant une boîte de dialogue, qui a ses propres touches, ni avec Alt.
    /// Entrée ne colle que depuis la recherche ou la liste : dans le champ Nom elle enregistre, sur un bouton elle l'active.
    /// </summary>
    /// <param name="touche">Code virtuel de la touche.</param>
    /// <param name="ctrl">Ctrl est enfoncée.</param>
    /// <param name="maj">Maj est enfoncée.</param>
    /// <param name="alt">Alt est enfoncée.</param>
    /// <param name="zone">Où se trouve le focus.</param>
    /// <param name="rechercheVide">La zone de recherche ne contient rien.</param>
    /// <param name="dialogueOuvert">Une boîte de dialogue est ouverte.</param>
    public static CommandeClavier Interpreter(uint touche, bool ctrl, bool maj, bool alt, ZoneFocus zone,
                                              bool rechercheVide, bool dialogueOuvert)
    {
        if (dialogueOuvert || alt)
        {
            return new(ActionTouche.Aucune);
        }

        var depuisRechercheOuListe = zone is ZoneFocus.Recherche or ZoneFocus.Liste;
        var sansModificateur = !ctrl && !maj;

        // Les chiffres du clavier principal (0x31 à 0x39) et du pavé numérique (0x61 à 0x69).
        if (ctrl && !maj && touche is >= 0x31 and <= 0x39)
        {
            return new(ActionTouche.AllerAOnglet, (int)(touche - 0x30));
        }

        if (ctrl && !maj && touche is >= 0x61 and <= 0x69)
        {
            return new(ActionTouche.AllerAOnglet, (int)(touche - 0x60));
        }

        return touche switch
        {
            // Ctrl l'emporte sur Maj : Ctrl+Maj+Entrée copie seulement, il ne colle jamais.
            VkEntree when depuisRechercheOuListe && ctrl => new(ActionTouche.CopierSeulement),
            VkEntree when depuisRechercheOuListe && maj => new(ActionTouche.CollerAutreMode),
            VkEntree when depuisRechercheOuListe => new(ActionTouche.Coller),
            VkF2 when sansModificateur => new(ActionTouche.AllerAuNom),
            // Suppr ne supprime l'élément que recherche vide : sinon il efface un caractère de la saisie.
            VkSuppr when sansModificateur && rechercheVide && depuisRechercheOuListe => new(ActionTouche.SupprimerElement),
            VkTab when ctrl && maj => new(ActionTouche.OngletPrecedent),
            VkTab when ctrl => new(ActionTouche.OngletSuivant),

            // Échap en deux temps : la recherche d'abord, la fenêtre ensuite.
            VkEchap when sansModificateur => new(rechercheVide ? ActionTouche.Masquer : ActionTouche.EffacerRecherche),
            VkHaut when sansModificateur && zone == ZoneFocus.Recherche => new(ActionTouche.SelectionPrecedente),
            VkBas when sansModificateur && zone == ZoneFocus.Recherche => new(ActionTouche.SelectionSuivante),
            _ => new(ActionTouche.Aucune),
        };
    }
}
```

`src/PressePapiers/Natif/NatifOuverture.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows de l'ouverture de la fenêtre : échelle d'un écran et position du curseur (spec, section 8).</summary>
internal static partial class NatifOuverture
{
    /// <summary>MDT_EFFECTIVE_DPI : la résolution réellement appliquée sur l'écran, mise à l'échelle comprise.</summary>
    internal const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>POINT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    /// <summary>Résolution d'un écran ; renvoie 0 (S_OK) quand elle est connue.</summary>
    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(nint ecran, int type, out uint dpiX, out uint dpiY);

    /// <summary>Position du curseur, en pixels de l'écran virtuel.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point position);
}
```

`src/PressePapiers/Interface/FenetrePrincipaleVue.Collage.cs`

```csharp
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>Collage de l'élément choisi dans la fenêtre d'origine, et navigation au clavier dans les onglets et la liste.</summary>
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Fenêtre qui avait le focus quand la fenêtre a été ouverte ; zéro quand elle est inconnue (ouverture depuis l'icône).</summary>
    public nint FenetreOrigine { get; set; }

    /// <summary>Handle de la fenêtre principale, pour que le recollage n'y revienne jamais.</summary>
    public nint HandleOutil { get; set; }

    /// <summary>Masque la fenêtre ; appelée par le recollage au bon moment de sa séquence.</summary>
    public Action MasquerFenetre { get; set; } = () => { };

    /// <summary>Notification de l'icône (titre, message), pour un collage impossible dont la fenêtre est déjà masquée. Branchée par l'application.</summary>
    public Action<string, string>? Notifier { get; set; }

    /// <summary>
    /// Vrai pendant la séquence de recollage : la fenêtre cède le premier plan à la fenêtre d'origine, ce qui ne doit
    /// pas la faire se masquer comme une perte de focus ordinaire.
    /// </summary>
    public bool CollageEnCours { get; private set; }

    /// <summary>
    /// Colle l'élément choisi dans la fenêtre d'origine (ou le copie seulement), puis dit à l'utilisateur ce qui s'est passé
    /// quand la fenêtre reste affichée. À appeler sur le fil d'interface : le recollage y reprend après chaque attente.
    /// </summary>
    public async Task CollerAsync(ModeCollage mode, bool copieSeule)
    {
        if (ElementSelectionne is not { } ligne || CollageEnCours)
        {
            return;
        }

        CollageEnCours = true;
        try
        {
            var issue = await _recollage.CollerAsync(ligne.Element, mode, FenetreOrigine, copieSeule, HandleOutil, MasquerFenetre);
            Informer(issue);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Le collage a échoué.", exception);
            Annoncer(InfoBarSeverity.Error, "Le collage a échoué. Le détail est dans le journal.");
        }
        finally
        {
            CollageEnCours = false;
        }
    }

    /// <summary>Message qui accompagne l'issue d'un collage : rien après un collage réussi, la fenêtre s'est masquée.</summary>
    internal void Informer(IssueCollage issue)
    {
        switch (issue)
        {
            case IssueCollage.CopieDemandee:
                Annoncer(InfoBarSeverity.Success, "Copié dans le presse-papiers.");
                break;
            case IssueCollage.CopieSansFenetreOrigine:
                Annoncer(InfoBarSeverity.Informational,
                         "Copié. La fenêtre d'origine est inconnue (ouverture depuis l'icône) : colle avec Ctrl+V.");
                break;
            case IssueCollage.CopieFenetreFermee:
                Annoncer(InfoBarSeverity.Warning, "Copié. La fenêtre d'origine n'existe plus : colle avec Ctrl+V.");
                break;
            case IssueCollage.CopieFenetreElevee:
                // La fenêtre est déjà masquée : seule la notification de l'icône peut encore le dire.
                Notifier?.Invoke("Presse-papiers", "Copié, colle avec Ctrl+V");
                break;
            case IssueCollage.Echec:
                Annoncer(InfoBarSeverity.Error, "Le collage a échoué. Le détail est dans le journal.");
                break;
        }
    }

    /// <summary>Remet la fenêtre dans l'état d'une ouverture : onglet Temporaire, recherche vide, premier élément choisi.</summary>
    internal void PreparerOuverture(OrigineOuverture origine)
    {
        AnnonceOuverte = false;
        ChoisirOnglet(Onglets.Count > 0 ? Onglets[0] : OngletSelectionne!);

        if (origine is OrigineOuverture.Icone or OrigineOuverture.Relance)
        {
            Annoncer(InfoBarSeverity.Informational,
                     "Ouvert sans fenêtre d'origine : Entrée copie l'élément, colle-le ensuite avec Ctrl+V.");
        }
    }

    /// <summary>Affiche l'onglet dont le numéro est donné (1 pour Temporaire) ; sans effet si cet onglet n'existe pas.</summary>
    public void AllerAOnglet(int numero)
    {
        if (numero >= 1 && numero <= Onglets.Count)
        {
            ChoisirOnglet(Onglets[numero - 1]);
        }
    }

    /// <summary>Passe à l'onglet suivant (+1) ou précédent (-1), en bouclant.</summary>
    public void OngletRelatif(int decalage)
    {
        var courant = Onglets.ToList().FindIndex(o => o.Id == OngletSelectionne?.Id);
        if (courant >= 0)
        {
            var total = Onglets.Count;
            ChoisirOnglet(Onglets[((courant + decalage) % total + total) % total]);
        }
    }

    /// <summary>Déplace le choix dans la liste d'une ligne vers le haut (-1) ou le bas (+1), sans sortir de la liste.</summary>
    public void DeplacerSelection(int decalage)
    {
        if (Elements.Count == 0)
        {
            return;
        }

        var courant = ElementSelectionne is null ? -1 : Elements.ToList().FindIndex(e => e.Id == ElementSelectionne.Id);
        ElementSelectionne = Elements[Math.Clamp(courant + decalage, 0, Elements.Count - 1)];
    }
}
```

`src/PressePapiers/Interface/VueApercu.xaml.cs`

```csharp
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using PressePapiers.Coeur.Modele;
using Windows.Storage.Streams;
using Windows.System;

namespace PressePapiers.Interface;

/// <summary>
/// Colonne de droite de la fenêtre : commandes, nom et onglet de rangement, contenu complet de l'élément choisi
/// (texte, RTF, image, liste de fichiers), application d'origine et date.
/// </summary>
public sealed partial class VueApercu : UserControl
{
    // Un TextBlock ne digère pas des mégaoctets : au-delà, l'aperçu est tronqué (le collage reste complet).
    private const int CaracteresAffiches = 100_000;

    private FenetrePrincipaleVue? _vue;
    private (long Id, string? Nom, long OngletId)? _affiche;
    private List<OngletVue> _ongletsProposes = [];

    /// <summary>Crée l'aperçu, vide : <see cref="Lier"/> le branche sur le modèle de vue.</summary>
    public VueApercu()
    {
        InitializeComponent();
        Afficher(null);
    }

    /// <summary>Branche l'aperçu sur le modèle de vue : il suit l'élément choisi et la liste des onglets.</summary>
    public void Lier(FenetrePrincipaleVue vue)
    {
        _vue = vue;
        vue.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FenetrePrincipaleVue.ElementSelectionne))
            {
                Afficher(vue.ElementSelectionne);
            }
        };
        vue.Rafraichi += SynchroniserOnglets;
        SynchroniserOnglets();
        Afficher(vue.ElementSelectionne);
    }

    /// <summary>Vrai si ce nœud de l'arbre visuel est le champ Nom : ses touches (Entrée, flèches) lui appartiennent.</summary>
    internal bool ContientChampNom(DependencyObject noeud) => ReferenceEquals(noeud, ChampNom);

    /// <summary>Place le curseur dans le champ Nom (F2 et bouton Nommer).</summary>
    public void FocaliserNom()
    {
        ChampNom.Focus(FocusState.Programmatic);
        ChampNom.SelectAll();
    }

    private void Afficher(ElementVue? ligne)
    {
        var element = ligne?.Element;

        // Une relecture du dépôt recrée les lignes : si c'est le même élément, rien ne change, et un nom en cours
        // de saisie n'est pas effacé.
        (long, string?, long)? cle = element is null ? null : (element.Id, element.Nom, element.OngletId);
        if (cle is not null && cle == _affiche)
        {
            return;
        }

        _affiche = cle;
        ApercuTexte.Visibility = Visibility.Collapsed;
        DefilementTexte.Visibility = Visibility.Collapsed;
        ApercuRiche.Visibility = Visibility.Collapsed;
        ZoneImage.Visibility = Visibility.Collapsed;
        DefilementFichiers.Visibility = Visibility.Collapsed;
        Mention.Text = string.Empty;

        var present = element is not null;
        Vide.Visibility = present ? Visibility.Collapsed : Visibility.Visible;
        foreach (var bouton in new[] { BoutonColler, BoutonAutreMode, BoutonNommer, BoutonSupprimer, BoutonEnregistrer })
        {
            bouton.IsEnabled = present;
        }

        ChampNom.IsEnabled = ChoixOnglet.IsEnabled = present;
        BoutonAutreMode.Visibility = element?.Type == TypeElement.MisEnForme ? Visibility.Visible : Visibility.Collapsed;
        BoutonAutreMode.Label = _vue?.LibelleAutreMode ?? "Texte brut";
        ChampNom.Text = element?.Nom ?? string.Empty;
        ChoixOnglet.SelectedItem = _ongletsProposes.FirstOrDefault(o => o.Id == element?.OngletId);
        Meta.Text = element is null ? string.Empty : Description(element);

        if (element is null || ligne is null)
        {
            return;
        }

        switch (element.Type)
        {
            case TypeElement.Image:
                _ = AfficherImageAsync(element);
                break;
            case TypeElement.Fichiers:
                ListeFichiers.ItemsSource = element.Fichiers;
                DefilementFichiers.Visibility = Visibility.Visible;
                break;
            case TypeElement.MisEnForme when element.Rtf is { Length: > 0 } rtf && RenduRtf.Charger(ApercuRiche, rtf, _vue?.Journal):
                ApercuRiche.Visibility = Visibility.Visible;
                break;
            case TypeElement.MisEnForme:
                // Pas de RTF exploitable (page web en HTML seul, RTF refusé) : texte brut, la mise en forme reste pour le collage.
                AfficherTexte(element, ligne, "Mise en forme conservée pour le collage.");
                break;
            default:
                AfficherTexte(element, ligne, null);
                break;
        }
    }

    private void AfficherTexte(Element element, ElementVue ligne, string? mention)
    {
        var texte = element.Texte ?? string.Empty;
        if (texte.Length > CaracteresAffiches)
        {
            mention = $"Aperçu limité aux {Nombre(CaracteresAffiches)} premiers caractères ({Nombre(texte.Length)} au total) ; "
                      + "le collage porte sur le texte entier.";
            texte = texte[..CaracteresAffiches];
        }

        ApercuTexte.Text = texte;
        ApercuTexte.FontFamily = ligne.Police;
        Mention.Text = mention ?? string.Empty;
        ApercuTexte.Visibility = Visibility.Visible;
        DefilementTexte.Visibility = Visibility.Visible;
    }

    // L'image est lue en mémoire : un BitmapImage sur le fichier le garderait ouvert et empêcherait de le supprimer.
    private async Task AfficherImageAsync(Element element)
    {
        var cle = _affiche;
        ZoneImage.Visibility = Visibility.Visible;
        ApercuImage.Source = null;
        ImageAbsente.Visibility = Visibility.Collapsed;
        try
        {
            var octets = await File.ReadAllBytesAsync(_vue!.CheminImage(element));
            using var flux = new InMemoryRandomAccessStream();
            await flux.WriteAsync(octets.AsBuffer());
            flux.Seek(0);
            var image = new BitmapImage { DecodePixelWidth = 1000 };
            await image.SetSourceAsync(flux);
            if (cle == _affiche)
            {
                ApercuImage.Source = image;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                                       or System.Runtime.InteropServices.COMException)
        {
            if (cle == _affiche)
            {
                ImageAbsente.Text = "L'image est introuvable ou illisible sur le disque.";
                ImageAbsente.Visibility = Visibility.Visible;
            }
        }
    }

    // Nombre à la française, groupes de chiffres séparés par une espace insécable.
    private static string Nombre(int valeur) =>
        valeur.ToString("N0", CultureInfo.GetCultureInfo("fr-FR")).Replace('\u202F', '\u00A0');

    private string Description(Element element)
    {
        var origine = string.IsNullOrWhiteSpace(element.Application) ? "une application inconnue" : element.Application;
        var taille = element.Type switch
        {
            TypeElement.Image => ResumeElement.Taille(element.Taille),
            TypeElement.Fichiers => $"{element.Fichiers.Count} fichier(s)",
            _ => Nombre(element.Texte?.Length ?? 0) + " caractères",
        };
        return $"Copié depuis {origine}, {ResumeElement.Heure(element.DateCopie, _vue?.Horloge ?? TimeProvider.System)} · {taille}";
    }

    // Les onglets proposés sont ceux de l'utilisateur : un élément nommé ne reste pas dans Temporaire.
    private void SynchroniserOnglets()
    {
        if (_vue is null)
        {
            return;
        }

        BoutonAutreMode.Label = _vue.LibelleAutreMode;
        var utilisateur = _vue.Onglets.Where(o => !o.EstTemporaire).ToList();
        if (utilisateur.Select(o => (o.Id, o.Nom)).SequenceEqual(_ongletsProposes.Select(o => (o.Id, o.Nom))))
        {
            return;
        }

        var choisi = (ChoixOnglet.SelectedItem as OngletVue)?.Id;
        _ongletsProposes = utilisateur;
        ChoixOnglet.ItemsSource = utilisateur;
        ChoixOnglet.SelectedItem = utilisateur.FirstOrDefault(o => o.Id == choisi);
    }

    private void Coller_Click(object sender, RoutedEventArgs e) => _vue?.DemanderCollage(autreMode: false, copieSeule: false);

    private void AutreMode_Click(object sender, RoutedEventArgs e) => _vue?.DemanderCollage(autreMode: true, copieSeule: false);

    private void Nommer_Click(object sender, RoutedEventArgs e) => FocaliserNom();

    private void Supprimer_Click(object sender, RoutedEventArgs e) => _ = _vue?.SupprimerSelectionAsync();

    private void Enregistrer_Click(object sender, RoutedEventArgs e) => Enregistrer();

    private void ChampNom_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Enregistrer();
        }
    }

    private void Enregistrer() => _vue?.Enregistrer(ChampNom.Text, (ChoixOnglet.SelectedItem as OngletVue)?.Id);
}
```

`src/PressePapiers/Interface/FenetrePrincipale.xaml`

```xml
<Window
    x:Class="PressePapiers.Interface.FenetrePrincipale"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:PressePapiers.Interface"
    Title="Presse-papiers">

    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>

    <Grid x:Name="Racine" PreviewKeyDown="Racine_PreviewKeyDown">
        <Grid.RowDefinitions>
            <RowDefinition Height="32" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- Barre de titre fondue : icône, nom de l'application, badge du raccourci. -->
        <Grid x:Name="ZoneTitre" Grid.Row="0">
            <StackPanel Orientation="Horizontal" Spacing="8" Margin="12,0,0,0" VerticalAlignment="Center">
                <FontIcon Glyph="&#xE77F;" FontFamily="Segoe Fluent Icons" FontSize="14" />
                <TextBlock Text="Presse-papiers" Style="{StaticResource CaptionTextBlockStyle}" VerticalAlignment="Center" />
                <Border Background="{ThemeResource ControlFillColorSecondaryBrush}"
                        BorderBrush="{ThemeResource ControlStrokeColorDefaultBrush}" BorderThickness="1"
                        CornerRadius="4" Padding="6,1" VerticalAlignment="Center">
                    <TextBlock Text="Win+V" FontSize="11" Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                </Border>
            </StackPanel>
        </Grid>

        <!-- Volet de gauche de 200 px : les onglets sont ajoutés par le code (compteurs, Nouvel onglet). -->
        <NavigationView x:Name="Navigation" Grid.Row="1"
                        PaneDisplayMode="Left" OpenPaneLength="200"
                        IsBackButtonVisible="Collapsed" IsPaneToggleButtonVisible="False"
                        IsSettingsVisible="True"
                        SelectionChanged="Navigation_SelectionChanged"
                        ItemInvoked="Navigation_ItemInvoked">

            <!-- Zone de contenu : le NavigationView lui donne le fond en couche arrondie. -->
            <Grid x:Name="Contenu">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>

                <!-- Messages (confirmations, problèmes) en haut de la zone de contenu. -->
                <InfoBar x:Name="Annonce" Grid.Row="0" Margin="12,12,12,0" IsClosable="True"
                         Severity="{x:Bind Vue.AnnonceGravite, Mode=OneWay}"
                         Message="{x:Bind Vue.AnnonceMessage, Mode=OneWay}"
                         IsOpen="{x:Bind Vue.AnnonceOuverte, Mode=TwoWay}" />

                <Grid Grid.Row="1">
                    <Grid x:Name="ZoneHistorique">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="300" />
                            <ColumnDefinition Width="1" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>

                        <!-- Colonne du milieu : recherche, légende de Temporaire, liste. -->
                        <Grid Grid.Column="0" Padding="12,12,0,0">
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="*" />
                            </Grid.RowDefinitions>

                            <AutoSuggestBox x:Name="ChampRecherche" Grid.Row="0" Margin="0,0,12,0"
                                            PlaceholderText="Rechercher dans tout l'historique"
                                            QueryIcon="Find"
                                            Text="{x:Bind Vue.Recherche, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

                            <TextBlock Grid.Row="1" Margin="2,8,12,4" TextWrapping="Wrap"
                                       Style="{StaticResource CaptionTextBlockStyle}"
                                       Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                       Text="{x:Bind Vue.LegendeTemporaire}"
                                       Visibility="{x:Bind Vue.LegendeVisible, Mode=OneWay}" />

                            <ListView x:Name="Liste" Grid.Row="2" Margin="0,4,0,0"
                                      DoubleTapped="Liste_DoubleTapped" CharacterReceived="Liste_CharacterReceived"
                                  ItemsSource="{x:Bind Vue.Elements, Mode=OneWay}"
                                      SelectedItem="{x:Bind Vue.ElementSelectionne, Mode=TwoWay}">
                                <ListView.ItemContainerStyle>
                                    <Style TargetType="ListViewItem" BasedOn="{StaticResource DefaultListViewItemStyle}">
                                        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                                        <Setter Property="Padding" Value="8,6,8,6" />
                                    </Style>
                                </ListView.ItemContainerStyle>
                                <ListView.ItemTemplate>
                                    <DataTemplate x:DataType="local:ElementVue">
                                        <Grid ColumnSpacing="10">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="22" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <FontIcon Grid.Column="0" Glyph="{x:Bind Glyphe}" FontFamily="Segoe Fluent Icons"
                                                      FontSize="16" VerticalAlignment="Top" Margin="0,2,0,0"
                                                      Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                            <StackPanel Grid.Column="1" Spacing="2">
                                                <TextBlock Text="{x:Bind Nom}" FontWeight="SemiBold"
                                                           Visibility="{x:Bind ANom}" TextTrimming="CharacterEllipsis" />
                                                <TextBlock Text="{x:Bind Apercu}" FontFamily="{x:Bind Police}"
                                                           MaxLines="{x:Bind LignesApercu}" TextWrapping="Wrap"
                                                           TextTrimming="CharacterEllipsis" />
                                                <Grid ColumnSpacing="6">
                                                    <Grid.ColumnDefinitions>
                                                        <ColumnDefinition Width="Auto" />
                                                        <ColumnDefinition Width="*" />
                                                        <ColumnDefinition Width="Auto" />
                                                    </Grid.ColumnDefinitions>
                                                    <Border Grid.Column="0" Visibility="{x:Bind OngletVisible}" CornerRadius="4" Padding="5,0"
                                                            Background="{ThemeResource AccentFillColorDefaultBrush}">
                                                        <TextBlock Text="{x:Bind NomOnglet}" FontSize="11"
                                                                   Foreground="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
                                                    </Border>
                                                    <TextBlock Grid.Column="1" Text="{x:Bind Meta}" FontSize="11" TextTrimming="CharacterEllipsis"
                                                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                                    <TextBlock Grid.Column="2" Text="mise en forme" FontSize="11" Visibility="{x:Bind MiseEnFormeVisible}"
                                                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                                </Grid>
                                            </StackPanel>
                                            <Image Grid.Column="2" Width="64" Height="40" Stretch="UniformToFill"
                                                   VerticalAlignment="Center" Visibility="{x:Bind VignetteVisible}"
                                                   Source="{x:Bind Vignette}" />
                                        </Grid>
                                    </DataTemplate>
                                </ListView.ItemTemplate>
                            </ListView>

                            <TextBlock Grid.Row="2" Margin="12,24,24,0" TextWrapping="Wrap" HorizontalAlignment="Center"
                                       VerticalAlignment="Top" TextAlignment="Center"
                                       Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                       Text="{x:Bind Vue.MessageVide, Mode=OneWay}"
                                       Visibility="{x:Bind Vue.ListeVide, Mode=OneWay}" />
                        </Grid>

                        <Border Grid.Column="1" Margin="0,12,0,0" Background="{ThemeResource DividerStrokeColorDefaultBrush}" />

                        <!-- Colonne de droite : aperçu et rangement de l'élément choisi. -->
                        <local:VueApercu x:Name="PanneauApercu" Grid.Column="2" />
                    </Grid>

                    <!-- Page Paramètres (tâche 28), affichée par l'entrée Paramètres du volet. -->
                    <Grid x:Name="ZoneParametres" Visibility="Collapsed" />
                </Grid>
            </Grid>
        </NavigationView>
    </Grid>
</Window>
```

`src/PressePapiers/Interface/FenetrePrincipale.xaml.cs`

```csharp
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Modele;
using PressePapiers.Systeme;
using WinRT.Interop;

namespace PressePapiers.Interface;

/// <summary>
/// Fenêtre principale (proposition B de la maquette, rendu WinUI 3) : volet des onglets, liste des éléments et
/// recherche à gauche, aperçu à droite. Créée au démarrage et masquée ; la masquer ne termine pas l'application.
/// </summary>
public sealed partial class FenetrePrincipale : Window
{
    private static readonly TimeSpan _delaiMemoireTaille = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherQueueTimer _minuteurTaille;
    private bool _voletEnSynchronisation;
    private bool _ouvertureEnCours;
    private bool _fermetureAutorisee;

    /// <summary>Crée la fenêtre, masquée, sur le modèle de vue.</summary>
    public FenetrePrincipale(FenetrePrincipaleVue vue)
    {
        ArgumentNullException.ThrowIfNull(vue);
        Vue = vue;
        InitializeComponent();
        Handle = WindowNative.GetWindowHandle(this);

        AppliquerFond(MicaController.IsSupported());

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(ZoneTitre);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "icone-normale.ico"));
        AppliquerTailleMemorisee();

        _minuteurTaille = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _minuteurTaille.Interval = _delaiMemoireTaille;
        _minuteurTaille.IsRepeating = false;
        _minuteurTaille.Tick += (_, _) => MemoriserTaille();
        AppWindow.Changed += AppWindow_Changed;

        // Cycle de vie : la croix masque la fenêtre au lieu de la fermer, la perte du focus la masque aussi.
        AppWindow.Closing += AppWindow_Closing;
        Activated += Fenetre_Activated;

        PanneauApercu.Lier(Vue);
        Vue.HandleOutil = Handle;
        Vue.MasquerFenetre = Masquer;
        Vue.CollageDemande += (mode, copieSeule) => _ = Vue.CollerAsync(mode, copieSeule);
        Vue.Dialogues = new GestionnaireDialogues(() => Content as FrameworkElement, () => EstVisible);
        Vue.Rafraichi += SynchroniserVolet;
        SynchroniserVolet();

        // Le NavigationView ignore une sélection posée avant son chargement : on la rejoue au premier affichage.
        Navigation.Loaded += (_, _) => SynchroniserVolet();
    }

    /// <summary>Modèle de vue lié aux contrôles.</summary>
    public FenetrePrincipaleVue Vue { get; }

    /// <summary>Handle Win32 de la fenêtre.</summary>
    public nint Handle { get; }

    /// <summary>Vrai tant que la fenêtre est affichée.</summary>
    public bool EstVisible => AppWindow.IsVisible;

    /// <summary>Levé quand Windows refuse de donner le premier plan à la fenêtre : l'application doit le dire par l'icône.</summary>
    public event Action? PremierPlanRefuse;

    /// <summary>
    /// Affiche la fenêtre au centre de l'écran de la fenêtre d'origine, sur Temporaire, premier élément choisi, curseur dans
    /// la recherche (AppWindow.Show puis PremierPlan.Amener). Ouverture idempotente : un signal reçu pendant que la fenêtre
    /// est affichée ou en cours d'ouverture est ignoré. Depuis l'icône ou une relance, la fenêtre d'origine n'est pas fiable :
    /// Entrée copie seulement.
    /// </summary>
    public void AfficherPour(nint fenetreOrigine, OrigineOuverture origine)
    {
        if (EstVisible || _ouvertureEnCours)
        {
            // Une relance de l'application, ou l'icône, demandent la fenêtre : on la remet devant sans rien changer.
            if (EstVisible && origine is OrigineOuverture.Icone or OrigineOuverture.Relance)
            {
                PremierPlan.Amener(Handle);
            }

            return;
        }

        _ouvertureEnCours = true;
        var origineFiable = origine is OrigineOuverture.WinV or OrigineOuverture.RaccourciSecours
                            && PremierPlan.EstOrigineValide(fenetreOrigine, Handle);
        Vue.FenetreOrigine = origineFiable ? fenetreOrigine : 0;
        Vue.Affichee = true;
        Vue.PreparerOuverture(origine);
        MontrerHistorique();
        PlacerAuCentre(origineFiable ? fenetreOrigine : 0);
        AppWindow.Show();
        var premierPlanObtenu = PremierPlan.Amener(Handle);
        if (!premierPlanObtenu)
        {
            PremierPlanRefuse?.Invoke();
        }

        ChampRecherche.Focus(FocusState.Programmatic);

        // Les pertes de focus survenues pendant l'ouverture elle-même sont ignorées, donc on les rattrape ici : une fenêtre
        // qui a eu le premier plan puis l'a perdu avant la fin de l'ouverture ne doit pas rester ouverte sans focus.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _ouvertureEnCours = false;
            if (premierPlanObtenu && EstVisible && PremierPlan.FenetreActive() != Handle)
            {
                Masquer();
            }
        });
    }

    /// <summary>Masque la fenêtre sans terminer l'application.</summary>
    public void Masquer() => AppWindow.Hide();

    /// <summary>Permet à la fenêtre de se fermer vraiment : à appeler avant de quitter l'application (menu de l'icône).</summary>
    public void AutoriserFermeture() => _fermetureAutorisee = true;

    /// <summary>Ouvre la fenêtre sur la page Paramètres.</summary>
    public void AfficherParametres() => throw new NotImplementedException("Tâche 28");

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange)
        {
            Vue.Affichee = sender.IsVisible;
        }

        // Pas de mémoire tant que la fenêtre est masquée : sa taille y est sans intérêt.
        if (args.DidSizeChange && sender.IsVisible)
        {
            _minuteurTaille.Stop();
            _minuteurTaille.Start();
        }
    }

    // La croix, Alt+F4 et la fermeture par le système masquent la fenêtre : l'application vit dans la zone de notification.
    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_fermetureAutorisee)
        {
            return;
        }

        args.Cancel = true;
        Masquer();
    }

    // Perdre le focus ferme la fenêtre, sauf pendant une ouverture, un recollage (la fenêtre d'origine reprend le premier
    // plan) ou une boîte de dialogue.
    private void Fenetre_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && EstVisible && !_ouvertureEnCours
            && !Vue.CollageEnCours && Vue.Dialogues?.EnCours != true)
        {
            Masquer();
        }
    }
}
```

`src/PressePapiers/Interface/FenetrePrincipale.Volet.cs`

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PressePapiers.Interface;

// Volet des onglets : synchronisation avec le modèle de vue, invocation par l'utilisateur, menu contextuel.
public sealed partial class FenetrePrincipale
{
    // Aligne les entrées du volet sur les onglets du modèle de vue : reconstruction seulement si la liste a changé.
    private void SynchroniserVolet()
    {
        _voletEnSynchronisation = true;
        try
        {
            var onglets = Vue.Onglets;
            var anciens = EntreesOnglets().Select(i => (OngletVue)i.Tag).ToList();
            if (OngletVue.StructureDifferente(anciens, onglets))
            {
                ReconstruireVolet(onglets);
            }

            var entrees = EntreesOnglets();
            for (var i = 0; i < entrees.Count; i++)
            {
                entrees[i].Tag = onglets[i];
                ((InfoBadge)entrees[i].InfoBadge).Value = onglets[i].Compteur;
            }

            // L'entrée Paramètres garde la sélection tant que la page est ouverte.
            if (!ReferenceEquals(Navigation.SelectedItem, Navigation.SettingsItem))
            {
                Navigation.SelectedItem = entrees.FirstOrDefault(i => ((OngletVue)i.Tag).Id == Vue.OngletSelectionne?.Id);
            }
        }
        finally
        {
            _voletEnSynchronisation = false;
        }
    }

    private List<NavigationViewItem> EntreesOnglets() =>
        Navigation.MenuItems.OfType<NavigationViewItem>().Where(i => i.Tag is OngletVue).ToList();

    private void ReconstruireVolet(IReadOnlyList<OngletVue> onglets)
    {
        var policeIcones = new FontFamily("Segoe Fluent Icons");
        Navigation.MenuItems.Clear();
        foreach (var onglet in onglets)
        {
            var entree = new NavigationViewItem
            {
                Content = onglet.Nom,
                Tag = onglet,
                Icon = new FontIcon { Glyph = onglet.EstTemporaire ? "\uE121" : "\uE8B7", FontFamily = policeIcones },
                InfoBadge = new InfoBadge
                {
                    Value = onglet.Compteur,
                    Style = (Style)Application.Current.Resources["InformationalValueInfoBadgeStyle"],
                },
            };
            if (!onglet.EstTemporaire)
            {
                entree.ContextFlyout = CreerMenuOnglet(entree);
            }

            Navigation.MenuItems.Add(entree);
        }

        Navigation.MenuItems.Add(new NavigationViewItemSeparator());
        Navigation.MenuItems.Add(new NavigationViewItem
        {
            Content = "Nouvel onglet",
            Tag = "nouvel-onglet",
            SelectsOnInvoked = false,
            Icon = new FontIcon { Glyph = "\uE710", FontFamily = policeIcones },
        });
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var parametres = args.IsSettingsSelected;
        ZoneHistorique.Visibility = parametres ? Visibility.Collapsed : Visibility.Visible;
        ZoneParametres.Visibility = parametres ? Visibility.Visible : Visibility.Collapsed;

        if (!_voletEnSynchronisation && args.SelectedItem is NavigationViewItem { Tag: OngletVue onglet })
        {
            Vue.ChoisirOnglet(onglet);
        }
    }

    // « Nouvel onglet » ne change pas la sélection : il ouvre la boîte de saisie du nom.
    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is "nouvel-onglet")
        {
            _ = Vue.NouvelOngletAsync();
        }
    }

    // Clic droit sur un onglet de l'utilisateur : renommer, supprimer, changer de place. Temporaire n'a pas de menu.
    private MenuFlyout CreerMenuOnglet(NavigationViewItem entree)
    {
        OngletVue Onglet() => (OngletVue)entree.Tag;
        var menu = new MenuFlyout();
        var renommer = new MenuFlyoutItem { Text = "Renommer" };
        renommer.Click += (_, _) => _ = Vue.RenommerOngletAsync(Onglet());
        var supprimer = new MenuFlyoutItem { Text = "Supprimer" };
        supprimer.Click += (_, _) => _ = Vue.SupprimerOngletAsync(Onglet());
        var monter = new MenuFlyoutItem { Text = "Monter" };
        monter.Click += (_, _) => Vue.DeplacerOnglet(Onglet(), -1);
        var descendre = new MenuFlyoutItem { Text = "Descendre" };
        descendre.Click += (_, _) => Vue.DeplacerOnglet(Onglet(), 1);
        menu.Items.Add(renommer);
        menu.Items.Add(supprimer);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(monter);
        menu.Items.Add(descendre);
        return menu;
    }

    // Revient à la liste des éléments si la page Paramètres était ouverte.
    private void MontrerHistorique()
    {
        _voletEnSynchronisation = true;
        try
        {
            Navigation.SelectedItem = EntreesOnglets().FirstOrDefault(i => ((OngletVue)i.Tag).Id == Vue.OngletSelectionne?.Id);
        }
        finally
        {
            _voletEnSynchronisation = false;
        }
    }
}
```

`src/PressePapiers/Interface/FenetrePrincipale.Affichage.cs`

```csharp
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PressePapiers.Natif;
using Windows.Graphics;

namespace PressePapiers.Interface;

// Aspect et position : fond Mica, thème, taille mémorisée, centrage sur l'écran de la fenêtre d'origine.
public sealed partial class FenetrePrincipale
{
    /// <summary>Fond Mica, ou fond uni quand le matériau n'est pas pris en charge (Windows 10, mode économie d'énergie).</summary>
    internal void AppliquerFond(bool mica)
    {
        if (mica)
        {
            SystemBackdrop = new MicaBackdrop();
            Racine.Background = null;
        }
        else
        {
            SystemBackdrop = null;
            Racine.Background = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"];
        }
    }

    // Taille mémorisée en pixels logiques : la même largeur de 920 doit donner la même fenêtre à 100 % et à 150 %.
    private double EchelleAffichage => Content?.XamlRoot?.RasterizationScale ?? 1.0;

    private void AppliquerTailleMemorisee()
    {
        var echelle = EchelleAffichage;
        AppWindow.Resize(new SizeInt32((int)Math.Round(Vue.Reglages.LargeurFenetre * echelle),
                                       (int)Math.Round(Vue.Reglages.HauteurFenetre * echelle)));
    }

    private void MemoriserTaille()
    {
        var echelle = EchelleAffichage;
        Vue.MemoriserTaille((int)Math.Round(AppWindow.Size.Width / echelle), (int)Math.Round(AppWindow.Size.Height / echelle));
    }

    // Au centre de la zone de travail de l'écran de la fenêtre d'origine, à la taille mémorisée de cet écran.
    private void PlacerAuCentre(nint fenetreOrigine)
    {
        var ecran = EcranDe(fenetreOrigine);
        var travail = ecran.WorkArea;
        var echelle = EchelleDe(ecran) ?? EchelleAffichage;
        var largeur = Math.Min((int)Math.Round(Vue.Reglages.LargeurFenetre * echelle), travail.Width);
        var hauteur = Math.Min((int)Math.Round(Vue.Reglages.HauteurFenetre * echelle), travail.Height);
        AppWindow.MoveAndResize(new RectInt32(travail.X + (travail.Width - largeur) / 2,
                                              travail.Y + (travail.Height - hauteur) / 2, largeur, hauteur));
    }

    // Écran de la fenêtre d'origine ; à défaut, celui du curseur (ouverture depuis l'icône), puis l'écran principal.
    private static DisplayArea EcranDe(nint fenetreOrigine)
    {
        try
        {
            if (fenetreOrigine != 0)
            {
                return DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(fenetreOrigine), DisplayAreaFallback.Nearest);
            }

            if (NatifOuverture.GetCursorPos(out var curseur))
            {
                return DisplayArea.GetFromPoint(new PointInt32(curseur.X, curseur.Y), DisplayAreaFallback.Nearest);
            }
        }
        catch (Exception)
        {
            // Une fenêtre détruite entre-temps : l'écran principal fait l'affaire.
        }

        return DisplayArea.Primary;
    }

    // Échelle d'affichage de cet écran (1,5 pour 150 %) : la taille mémorisée est en pixels logiques.
    private static double? EchelleDe(DisplayArea ecran)
    {
        var moniteur = Win32Interop.GetMonitorFromDisplayId(ecran.DisplayId);
        return NatifOuverture.GetDpiForMonitor(moniteur, NatifOuverture.MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 ? dpi / 96.0 : null;
    }
}
```

`src/PressePapiers/Interface/FenetrePrincipale.Clavier.cs`

```csharp
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PressePapiers.Coeur.Clavier;
using CoreVirtualKeyStates = Windows.UI.Core.CoreVirtualKeyStates;
using VirtualKey = Windows.System.VirtualKey;

namespace PressePapiers.Interface;

// Clavier : la table des touches de la spec (section 8) appliquée à la fenêtre, double-clic, saisie depuis la liste.
public sealed partial class FenetrePrincipale
{
    private void Racine_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var commande = TableDesTouches.Interpreter(
            (uint)e.Key, Enfoncee(VirtualKey.Control), Enfoncee(VirtualKey.Shift), Enfoncee(VirtualKey.Menu),
            ZoneDuFocus(), string.IsNullOrEmpty(Vue.Recherche), Vue.Dialogues?.EnCours == true);

        // Sur la page Paramètres, la liste est masquée : ses touches n'ont rien à faire.
        var surParametres = ZoneParametres.Visibility == Visibility.Visible;
        if (commande.Action == ActionTouche.Aucune
            || (surParametres && commande.Action is not (ActionTouche.Masquer or ActionTouche.OngletSuivant
                                                          or ActionTouche.OngletPrecedent or ActionTouche.AllerAOnglet)))
        {
            return;
        }

        e.Handled = true;
        switch (commande.Action)
        {
            case ActionTouche.Coller:
                Vue.DemanderCollage(autreMode: false, copieSeule: false);
                break;
            case ActionTouche.CollerAutreMode:
                Vue.DemanderCollage(autreMode: true, copieSeule: false);
                break;
            case ActionTouche.CopierSeulement:
                Vue.DemanderCollage(autreMode: false, copieSeule: true);
                break;
            case ActionTouche.AllerAuNom:
                PanneauApercu.FocaliserNom();
                break;
            case ActionTouche.SupprimerElement:
                _ = Vue.SupprimerSelectionAsync();
                break;
            case ActionTouche.OngletSuivant:
                Vue.OngletRelatif(1);
                MontrerHistorique();
                break;
            case ActionTouche.OngletPrecedent:
                Vue.OngletRelatif(-1);
                MontrerHistorique();
                break;
            case ActionTouche.AllerAOnglet:
                Vue.AllerAOnglet(commande.Numero);
                MontrerHistorique();
                break;
            case ActionTouche.EffacerRecherche:
                Vue.EffacerRecherche();
                break;
            case ActionTouche.Masquer:
                Masquer();
                break;
            case ActionTouche.SelectionPrecedente:
            case ActionTouche.SelectionSuivante:
                Vue.DeplacerSelection(commande.Action == ActionTouche.SelectionPrecedente ? -1 : 1);
                if (Vue.ElementSelectionne is not null)
                {
                    Liste.ScrollIntoView(Vue.ElementSelectionne);
                }

                break;
        }
    }

    // Double-clic sur un élément : coller, comme Entrée.
    private void Liste_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (Vue.ElementSelectionne is not null)
        {
            Vue.DemanderCollage(autreMode: false, copieSeule: false);
        }
    }

    // Saisie depuis la liste : le caractère part dans la zone de recherche, qui reprend le focus.
    private void Liste_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (char.IsControl(args.Character))
        {
            return;
        }

        args.Handled = true;
        Vue.Recherche += args.Character;
        ChampRecherche.Focus(FocusState.Programmatic);
        if (Racine.XamlRoot is not null)
        {
            var champ = ContenuDe<TextBox>(ChampRecherche);
            if (champ is not null)
            {
                champ.SelectionStart = champ.Text.Length;
            }
        }
    }

    private static bool Enfoncee(VirtualKey touche) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(touche).HasFlag(CoreVirtualKeyStates.Down);

    // Où est le focus : le sens d'Entrée, de Suppr et des flèches en dépend.
    private ZoneFocus ZoneDuFocus()
    {
        if (Content?.XamlRoot is null || FocusManager.GetFocusedElement(Content.XamlRoot) is not DependencyObject courant)
        {
            return ZoneFocus.Autre;
        }

        for (DependencyObject? noeud = courant; noeud is not null; noeud = VisualTreeHelper.GetParent(noeud))
        {
            if (ReferenceEquals(noeud, ChampRecherche))
            {
                return ZoneFocus.Recherche;
            }

            if (ReferenceEquals(noeud, Liste))
            {
                return ZoneFocus.Liste;
            }

            if (PanneauApercu.ContientChampNom(noeud))
            {
                return ZoneFocus.ChampNom;
            }
        }

        return ZoneFocus.Autre;
    }

    private static T? ContenuDe<T>(DependencyObject racine) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
        {
            var enfant = VisualTreeHelper.GetChild(racine, i);
            if (enfant is T direct)
            {
                return direct;
            }

            if (ContenuDe<T>(enfant) is { } plusBas)
            {
                return plusBas;
            }
        }

        return default;
    }
}
```


- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, tous les tests verts, 55 tests de plus que les tâches précédentes (576 réussis,
0 échec, 0 ignoré).

- [ ] **Étape 5 : Vérifier sur le poste**

```bash
bash "$ESSAIS/lancer.sh" "$(git rev-parse --show-toplevel)" 27
```

Résultat attendu (environ 60 s ; ne touchez à rien) : `=== Fin : 65 réussites, 0 échecs, 0 erreurs au journal`, avec une ligne `INFO` sur
les écrans détectés. Regardez `captures/27-premiere-ouverture.png` et `27-ouverture.png`. Ce que le banc prouve :
- ouverture : fenêtre jamais activée affichée au premier plan depuis une autre application, contenu complet à la première capture,
  Temporaire, recherche vide, premier élément choisi, curseur dans la recherche, centrée sur l'écran de la fenêtre d'origine (et sur
  chacun des deux autres écrans de la machine d'essai), taille mémorisée appliquée ; second signal ignoré ;
- frappes réelles : saisie dans la recherche (délai de 150 ms), touche tapée depuis la liste qui part dans la recherche, Échap en deux temps, flèches haut et bas depuis la recherche (le focus
  ne bouge pas, la sélection s'arrête aux bornes), Ctrl+Tab, Ctrl+Maj+Tab (avec retour à la boucle), Ctrl+1, Ctrl+3, Ctrl+9 sans
  neuvième onglet, F2 (focus dans le champ Nom), Entrée dans le champ Nom (n'enregistre pas sans onglet, ne colle jamais), Suppr (n'agit
  pas avec une recherche en cours, supprime l'élément temporaire sans confirmation recherche vide) ;
- collage réel : Entrée colle dans la fenêtre d'origine (texte relu par `WM_GETTEXT`), la fenêtre se masque, la fenêtre d'origine a le
  premier plan, l'élément remonte en tête ; texte seul : pas de RTF dans le presse-papiers ; Maj+Entrée sur un élément mis en forme :
  RTF présent ; Ctrl+Entrée : rien n'est collé, la fenêtre reste, l'élément est dans le presse-papiers, message de réussite ;
  double-clic réel sur un élément : collé ;
- depuis l'icône : message d'information, Entrée copie seulement avec son message ; relance pendant l'affichage : remise devant, état
  conservé ; fenêtre d'origine fermée entre-temps : copie seule et message ; cible élevée : notification « Copié, colle avec Ctrl+V »
  (appel de `Informer`, la lecture du niveau d'une vraie fenêtre élevée relève de la tâche 19) ;
- focus : perte du focus masque la fenêtre ; pas pendant une boîte de dialogue (mutation sans ce test : échec) ; premier plan repris
  pendant l'ouverture : la fenêtre se masque (mutation sans rattrapage : échec) ; Échap dans une boîte ferme la boîte seulement ;
- croix (`SC_CLOSE`) : la fenêtre se masque et se rouvre normalement.

Mutations de la table des touches (étape 1) : priorité de Maj sur Ctrl pour Entrée : 1 test échoue ; Échap inversé : 6 échouent ;
Suppr sans condition sur la recherche : 2 échouent.

Non couvert ici (recette manuelle, spec section 14) : Win+V réel (tâche 29), une fenêtre administrateur réelle (la cible est plus
élevée), la mise à l'échelle 150 % sur un écran secondaire de résolution différente, le collage dans Word, Excel, un navigateur ;
la suspension de la fermeture pendant un recollage n'est pas distinguable à l'essai (la fenêtre se masque de toute façon à
l'instant où `ServiceRecollage` appelle `masquerFenetre`).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Clavier tests/PressePapiers.Tests/Clavier/TableDesTouchesTests.cs src/PressePapiers/Natif/NatifOuverture.cs src/PressePapiers/Interface/
git commit -m "Ajouter l'ouverture, le clavier, le collage et la fermeture de la fenêtre" -m "AfficherPour centre la fenêtre sur l'écran de la fenêtre d'origine, amène le premier plan et place le curseur dans la recherche sur l'onglet Temporaire ; ouverture idempotente. La table des touches de la spec vit dans Coeur (TableDesTouches, testée) et la fenêtre l'applique : Entrée, Maj+Entrée, Ctrl+Entrée, F2, Suppr, Ctrl+Tab, Ctrl+1 à 9, Échap en deux temps, double-clic. Collage par ServiceRecollage avec message selon l'issue, copie seule depuis l'icône, fermeture sur perte du focus sauf recollage et boîte de dialogue, croix interceptée."
```

