namespace PressePapiers.Systeme;

/// <summary>Lancement avec Windows par la clé Run de l'utilisateur. Coquille : remplacée par la tâche 16.</summary>
public static class DemarrageWindows
{
    /// <summary>Vrai si la valeur « PressePapiers » existe dans HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
    public static bool EstActive() => throw new NotImplementedException("Tâche 16");

    /// <summary>Crée ou supprime la valeur « PressePapiers » de la clé Run.</summary>
    public static void Appliquer(bool activer, string cheminExecutable) => throw new NotImplementedException("Tâche 16");
}
