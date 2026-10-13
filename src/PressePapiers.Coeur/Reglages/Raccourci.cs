namespace PressePapiers.Coeur.Reglages;

/// <summary>Raccourci clavier global : modificateurs (dont la touche Windows) et touche principale.</summary>
/// <param name="Win">Touche Windows.</param>
/// <param name="Ctrl">Touche Ctrl.</param>
/// <param name="Alt">Touche Alt.</param>
/// <param name="Maj">Touche Maj.</param>
/// <param name="ToucheVirtuelle">Code de touche virtuelle de la touche principale.</param>
public readonly record struct Raccourci(bool Win, bool Ctrl, bool Alt, bool Maj, uint ToucheVirtuelle)
{
    // Drapeaux de RegisterHotKey.
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private const uint VkEspace = 0x20;
    private const uint VkV = 0x56;
    private const uint VkF1 = 0x70;

    /// <summary>
    /// Analyse un texte du type « Win+Alt+V » ou « Ctrl+Alt+Maj+V » : ordre et casse libres, « Shift » accepté pour Maj,
    /// « Windows » pour Win, espaces autour des signes tolérés. Il faut au moins deux modificateurs parmi Win, Ctrl, Alt et Maj :
    /// un raccourci global à un seul modificateur (Ctrl+C, Ctrl+V, Ctrl+Z, Alt+F4...) détournerait le clavier dans tout Windows.
    /// Les combinaisons que Windows se réserve et que <c>RegisterHotKey</c> ne pourrait jamais enregistrer (Win+Maj+V et Win+Ctrl+V,
    /// qui ouvrent l'historique du presse-papiers et le choix de saisie) sont refusées d'avance. La touche principale est une
    /// lettre A-Z, un chiffre 0-9, F1 à F24 ou Espace.
    /// </summary>
    /// <param name="texte">Texte saisi par l'utilisateur.</param>
    /// <param name="raccourci">Le raccourci analysé, ou la valeur vide si le texte est refusé.</param>
    /// <returns>Vrai si le texte est un raccourci valide.</returns>
    public static bool TryAnalyser(string texte, out Raccourci raccourci) =>
        Analyser(texte, out raccourci) == ResultatAnalyse.Valide;

    /// <summary>
    /// Comme <see cref="TryAnalyser"/>, mais distingue une combinaison que Windows se réserve d'un texte simplement invalide :
    /// l'interface peut alors dire pourquoi elle refuse.
    /// </summary>
    /// <param name="texte">Texte saisi par l'utilisateur.</param>
    /// <param name="raccourci">Le raccourci analysé, ou la valeur vide si le texte est refusé.</param>
    /// <returns>Le verdict de l'analyse.</returns>
    public static ResultatAnalyse Analyser(string texte, out Raccourci raccourci)
    {
        raccourci = default;
        if (string.IsNullOrWhiteSpace(texte))
        {
            return ResultatAnalyse.Invalide;
        }

        bool win = false, ctrl = false, alt = false, maj = false;
        uint? touche = null;

        foreach (var morceau in texte.Split('+'))
        {
            var jeton = morceau.Trim();
            if (jeton.Length == 0)
            {
                return ResultatAnalyse.Invalide;
            }

            switch (jeton.ToLowerInvariant())
            {
                case "win" or "windows":
                    if (win) return ResultatAnalyse.Invalide;
                    win = true;
                    continue;
                case "ctrl":
                    if (ctrl) return ResultatAnalyse.Invalide;
                    ctrl = true;
                    continue;
                case "alt":
                    if (alt) return ResultatAnalyse.Invalide;
                    alt = true;
                    continue;
                case "maj" or "shift":
                    if (maj) return ResultatAnalyse.Invalide;
                    maj = true;
                    continue;
            }

            // Tout autre jeton doit être l'unique touche principale.
            if (touche is not null || !TryToucheVirtuelle(jeton, out var code))
            {
                return ResultatAnalyse.Invalide;
            }

            touche = code;
        }

        // Un seul modificateur ferait d'un raccourci de secours un détournement de frappes courantes (Ctrl+C, Alt+F4...).
        var nombreModificateurs = (win ? 1 : 0) + (ctrl ? 1 : 0) + (alt ? 1 : 0) + (maj ? 1 : 0);
        if (touche is null || nombreModificateurs < 2)
        {
            return ResultatAnalyse.Invalide;
        }

        var analyse = new Raccourci(win, ctrl, alt, maj, touche.Value);
        if (analyse.EstReserveParWindows)
        {
            return ResultatAnalyse.ReserveParWindows;
        }

        raccourci = analyse;
        return ResultatAnalyse.Valide;
    }

    /// <summary>
    /// Vrai pour une combinaison que Windows traite lui-même avant tout programme : <c>RegisterHotKey</c> la refuse (erreur 1409,
    /// constatée à l'essai pour Win+Maj+V et Win+Ctrl+V). Win+V seul est déjà exclu par la règle des deux modificateurs ; il est
    /// compris ici pour qu'un raccourci construit à la main ne passe pas non plus. Win+Alt+V, lui, s'enregistre.
    /// </summary>
    public bool EstReserveParWindows =>
        Win && ToucheVirtuelle == VkV && !Alt && !(Ctrl && Maj);

    /// <summary>MOD_WIN, MOD_CONTROL, MOD_ALT et MOD_SHIFT selon les drapeaux, plus MOD_NOREPEAT (0x4000).</summary>
    public uint Modificateurs =>
        ModNoRepeat | (Win ? ModWin : 0) | (Ctrl ? ModControl : 0) | (Alt ? ModAlt : 0) | (Maj ? ModShift : 0);

    /// <summary>Forme canonique « Win+Alt+V » : toujours Win, Ctrl, Alt, Maj puis la touche.</summary>
    public override string ToString()
    {
        var parties = new List<string>(5);
        if (Win) parties.Add("Win");
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

/// <summary>Verdict de <see cref="Raccourci.Analyser"/>.</summary>
public enum ResultatAnalyse
{
    /// <summary>Le texte est un raccourci utilisable.</summary>
    Valide,

    /// <summary>Le texte n'est pas un raccourci (mot inconnu, touche absente, un seul modificateur, doublon...).</summary>
    Invalide,

    /// <summary>Le texte est bien formé, mais Windows se réserve la combinaison.</summary>
    ReserveParWindows,
}
