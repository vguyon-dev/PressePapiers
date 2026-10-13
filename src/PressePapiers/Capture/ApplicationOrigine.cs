using System.ComponentModel;
using System.Diagnostics;
using PressePapiers.Natif;

namespace PressePapiers.Capture;

/// <summary>Nom du processus d'où vient une copie : une indication au mieux, jamais une certitude.</summary>
public static class ApplicationOrigine
{
    private const string HoteApplicationsUwp = "ApplicationFrameHost";

    /// <summary>
    /// Nom du processus de la fenêtre, sans extension. Pour une application UWP, la fenêtre appartient à
    /// ApplicationFrameHost : on cherche alors, parmi ses fenêtres filles, celle d'un autre processus, qui est l'application.
    /// </summary>
    /// <param name="fenetre">Fenêtre au premier plan relevée à la copie, ou 0.</param>
    /// <returns>Le nom du processus (par exemple « Code », « WindowsTerminal »), ou null si rien n'est déterminé.</returns>
    public static string? Determiner(nint fenetre)
    {
        if (fenetre == 0)
        {
            return null;
        }

        var idHote = IdProcessus(fenetre);
        var nom = NomProcessus(idHote);
        if (!string.Equals(nom, HoteApplicationsUwp, StringComparison.OrdinalIgnoreCase))
        {
            return nom;
        }

        // Le nom ApplicationFrameHost n'apprend rien : sans fenêtre fille d'un autre processus, l'origine reste inconnue.
        nint fille = 0;
        while ((fille = NatifPressePapiers.FindWindowEx(fenetre, fille, null, null)) != 0)
        {
            var idFille = IdProcessus(fille);
            if (idFille != 0 && idFille != idHote)
            {
                return NomProcessus(idFille);
            }
        }

        return null;
    }

    /// <summary>
    /// Origine de la copie qui vient d'être signalée : processus de la fenêtre au premier plan ; seulement à défaut,
    /// processus propriétaire du presse-papiers (il peut être un service sans rapport avec la fenêtre que l'utilisateur voit).
    /// À appeler dès la réception de la notification, avant que l'utilisateur ne change de fenêtre.
    /// </summary>
    internal static string? DeterminerPourCopie() =>
        Determiner(NatifPressePapiers.GetForegroundWindow()) ?? Determiner(NatifPressePapiers.GetClipboardOwner());

    private static uint IdProcessus(nint fenetre)
    {
        NatifPressePapiers.GetWindowThreadProcessId(fenetre, out var id);
        return id;
    }

    // Le processus peut avoir disparu entre la copie et la lecture, ou refuser l'accès : l'origine est alors inconnue.
    private static string? NomProcessus(uint idProcessus)
    {
        if (idProcessus == 0)
        {
            return null;
        }

        try
        {
            using var processus = Process.GetProcessById((int)idProcessus);
            return processus.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }
}
