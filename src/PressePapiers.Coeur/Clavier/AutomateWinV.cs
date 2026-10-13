namespace PressePapiers.Coeur.Clavier;

/// <summary>Logique pure du hook Win+V (spec, section 11), testable sans Windows. Coquille : remplacée par la tâche 11.</summary>
public sealed class AutomateWinV
{
    /// <summary>Touche Windows gauche.</summary>
    public const uint VkWinGauche = 0x5B;

    /// <summary>Touche Windows droite.</summary>
    public const uint VkWinDroite = 0x5C;

    /// <summary>Touche V.</summary>
    public const uint VkV = 0x56;

    /// <summary>
    /// Décide du sort d'une frappe. <paramref name="injectee"/> : frappe portant LLKHF_INJECTED (toujours laissée) ;
    /// <paramref name="winReellementEnfoncee"/> : résultat de GetAsyncKeyState au moment de V.
    /// </summary>
    public ActionCrochet Traiter(uint touche, bool enfoncee, bool injectee, bool winReellementEnfoncee) =>
        throw new NotImplementedException("Tâche 11");

    /// <summary>Oublie l'état « Win enfoncée » : déverrouillage, sortie de veille, réinstallation du hook.</summary>
    public void Reinitialiser() => throw new NotImplementedException("Tâche 11");
}
