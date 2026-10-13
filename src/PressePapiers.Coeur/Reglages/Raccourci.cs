namespace PressePapiers.Coeur.Reglages;

/// <summary>Raccourci clavier global. Coquille : remplacée par la tâche 3.</summary>
/// <param name="Ctrl">Touche Ctrl.</param>
/// <param name="Alt">Touche Alt.</param>
/// <param name="Maj">Touche Maj.</param>
/// <param name="ToucheVirtuelle">Code de touche virtuelle de la touche principale.</param>
public readonly record struct Raccourci(bool Ctrl, bool Alt, bool Maj, uint ToucheVirtuelle)
{
    /// <summary>
    /// Analyse un texte du type « Ctrl+Alt+Maj+V » (ordre et casse libres, « Shift » accepté pour Maj) : au moins Ctrl ou Alt,
    /// jamais la touche Windows, touche principale parmi A-Z, 0-9, F1-F24 et Espace.
    /// </summary>
    public static bool TryAnalyser(string texte, out Raccourci raccourci) => throw new NotImplementedException("Tâche 3");

    /// <summary>MOD_CONTROL, MOD_ALT et MOD_SHIFT selon les drapeaux, plus MOD_NOREPEAT (0x4000).</summary>
    public uint Modificateurs => throw new NotImplementedException("Tâche 3");

    /// <summary>Forme canonique « Ctrl+Alt+Maj+V ».</summary>
    public override string ToString() => throw new NotImplementedException("Tâche 3");
}
