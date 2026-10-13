using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows de la brique Premier plan (spec, section 10.1). Chaque brique native est autonome.</summary>
internal static unsafe partial class NatifPremierPlan
{
    internal const int SW_SHOW = 5;
    internal const int SW_RESTORE = 9;
    internal const uint INPUT_MOUSE = 0;

    /// <summary>GA_ROOTOWNER : fenêtre propriétaire de plus haut niveau.</summary>
    internal const uint GA_ROOTOWNER = 3;

    /// <summary>MOUSEINPUT (32 octets en x64, remplissage compris).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EntreeSouris
    {
        public int Dx;
        public int Dy;
        public uint DonneesSouris;
        public uint Drapeaux;
        public uint Heure;
        public nint InfoSupplementaire;
    }

    /// <summary>INPUT dimensionné sur sa plus grande variante, sinon SendInput refuse la taille.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct Entree
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public EntreeSouris Souris;
    }

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BringWindowToTop(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hwnd, int commande);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hwnd, out uint idProcessus);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AttachThreadInput(uint idFilSource, uint idFilCible, [MarshalAs(UnmanagedType.Bool)] bool attacher);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint nombre, in Entree entrees, int tailleEntree);

    [LibraryImport("user32.dll")]
    internal static partial nint GetDesktopWindow();

    [LibraryImport("user32.dll")]
    internal static partial nint GetShellWindow();

    [LibraryImport("user32.dll")]
    internal static partial nint GetAncestor(nint hwnd, uint drapeau);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    internal static partial int GetClassName(nint hwnd, char* nom, int tailleMax);

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentThreadId();
}
