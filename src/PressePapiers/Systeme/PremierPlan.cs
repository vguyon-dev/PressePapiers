namespace PressePapiers.Systeme;

/// <summary>Amène une fenêtre au premier plan (spec, section 10.1). Coquille : remplacée par la tâche 19.</summary>
public static class PremierPlan
{
    /// <summary>Restaure la fenêtre si besoin puis la met au premier plan ; renvoie faux si Windows refuse.</summary>
    public static bool Amener(nint hwnd) => throw new NotImplementedException("Tâche 19");

    /// <summary>Fenêtre actuellement au premier plan.</summary>
    public static nint FenetreActive() => throw new NotImplementedException("Tâche 19");

    /// <summary>Vrai si la fenêtre peut servir de fenêtre d'origine : ni l'outil, ni le bureau, ni la barre des tâches.</summary>
    public static bool EstOrigineValide(nint hwnd, nint hwndOutil) => throw new NotImplementedException("Tâche 19");
}
