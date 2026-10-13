using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows qui donnent le chemin de l'exécutable d'un processus (application d'origine d'une copie).</summary>
internal static partial class NatifProcessus
{
    /// <summary>PROCESS_QUERY_LIMITED_INFORMATION : droit minimal, accordé même pour un processus élevé ou protégé.</summary>
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint OpenProcess(uint acces, [MarshalAs(UnmanagedType.Bool)] bool heriter, uint idProcessus);

    /// <summary>Chemin Win32 de l'exécutable ; <paramref name="taille"/> donne la capacité en caractères, puis la longueur.</summary>
    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool QueryFullProcessImageName(nint processus, uint drapeaux, char* tampon, ref uint taille);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);
}
