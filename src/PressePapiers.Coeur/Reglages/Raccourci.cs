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
