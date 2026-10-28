using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PressePapiers.Natif;

/// <summary>Écriture d'un dump du processus courant (SurveillanceInterface).</summary>
internal static partial class NatifDump
{
    // Mémoire complète (piles .NET lisibles par dotnet-dump), handles, état des fils, modules déchargés, carte mémoire.
    internal const uint TypeComplet = 0x2 | 0x4 | 0x20 | 0x800 | 0x1000;

    [LibraryImport("dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MiniDumpWriteDump(nint processus, uint idProcessus, SafeFileHandle fichier, uint type,
                                                   nint exception, nint fluxUtilisateur, nint rappel);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GetCurrentProcess();
}
