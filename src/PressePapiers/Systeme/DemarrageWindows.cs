using Microsoft.Win32;

namespace PressePapiers.Systeme;

/// <summary>
/// Lancement avec Windows par la clé Run de l'utilisateur (spec, section 12) : un lancement non élevé, sans droits
/// administrateur. La valeur s'appelle « PressePapiers » et contient le chemin de l'exécutable entre guillemets.
/// </summary>
public static class DemarrageWindows
{
    /// <summary>Clé Run de l'utilisateur, sous HKCU.</summary>
    internal const string CleRun = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Nom de la valeur de la clé Run.</summary>
    internal const string NomValeur = "PressePapiers";

    /// <summary>Vrai si la valeur « PressePapiers » existe dans HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
    public static bool EstActive() => EstActive(CleRun);

    /// <summary>Crée ou supprime la valeur « PressePapiers » de la clé Run.</summary>
    /// <param name="activer">Vrai pour lancer l'application avec Windows, faux pour retirer la valeur.</param>
    /// <param name="cheminExecutable">Chemin complet de l'exécutable ; ignoré quand <paramref name="activer"/> est faux.</param>
    public static void Appliquer(bool activer, string cheminExecutable) => Appliquer(activer, cheminExecutable, CleRun);

    /// <summary>
    /// Variante qui prend le chemin de la clé sous HKCU : les vérifications travaillent sous une clé d'essai
    /// et ne touchent jamais la vraie clé Run.
    /// </summary>
    internal static bool EstActive(string cheminCle)
    {
        using var cle = Registry.CurrentUser.OpenSubKey(cheminCle);
        return cle?.GetValue(NomValeur) is not null;
    }

    /// <summary>Variante de <see cref="Appliquer(bool, string)"/> sur la clé donnée (sous HKCU).</summary>
    internal static void Appliquer(bool activer, string cheminExecutable, string cheminCle)
    {
        if (activer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cheminExecutable);

            // Les guillemets protègent un chemin qui contient des espaces (le dossier de l'utilisateur, par exemple).
            using var cle = Registry.CurrentUser.CreateSubKey(cheminCle);
            cle.SetValue(NomValeur, $"\"{cheminExecutable}\"", RegistryValueKind.String);
            return;
        }

        // Retirer ce qui n'existe pas n'est pas une erreur : la clé peut ne jamais avoir été créée.
        using var existante = Registry.CurrentUser.OpenSubKey(cheminCle, writable: true);
        existante?.DeleteValue(NomValeur, throwOnMissingValue: false);
    }
}
