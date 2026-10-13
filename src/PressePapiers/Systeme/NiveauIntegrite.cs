namespace PressePapiers.Systeme;

/// <summary>Niveau d'intégrité des processus (spec, section 10.3). Coquille : remplacée par la tâche 19.</summary>
public static class NiveauIntegrite
{
    /// <summary>RID du niveau d'intégrité du processus d'une fenêtre ; null si une étape de la lecture échoue.</summary>
    public static int? DeFenetre(nint hwnd) => throw new NotImplementedException("Tâche 19");

    /// <summary>RID du niveau d'intégrité du processus courant.</summary>
    public static int DuProcessusCourant() => throw new NotImplementedException("Tâche 19");
}
