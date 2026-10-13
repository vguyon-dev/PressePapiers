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
