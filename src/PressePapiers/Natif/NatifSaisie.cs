using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows du recollage : frappe de Ctrl+V et état réel des modificateurs (spec, section 10.2).</summary>
internal static partial class NatifSaisie
{
    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>MAPVK_VK_TO_VSC : code de touche virtuelle vers code de balayage.</summary>
    internal const uint MAPVK_VK_TO_VSC = 0;

    internal const ushort VK_CONTROL = 0x11;
    internal const ushort VK_V = 0x56;

    /// <summary>Win gauche, Win droite, Maj, Ctrl, Alt : ce que l'utilisateur peut encore tenir après Entrée ou Maj+Entrée.</summary>
    internal static readonly int[] ModificateursASurveiller = [0x5B, 0x5C, 0x10, 0x11, 0x12];

    /// <summary>KEYBDINPUT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EntreeClavier
    {
        public ushort Touche;
        public ushort CodeBalayage;
        public uint Drapeaux;
        public uint Heure;
        public nint InfoSupplementaire;
    }

    /// <summary>INPUT dimensionné sur sa plus grande variante (souris), sinon SendInput refuse la taille.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct Entree
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public EntreeClavier Clavier;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint nombre, Entree[] entrees, int tailleEntree);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int touche);

    /// <summary>Traduit une touche selon <paramref name="type"/> (MAPVK_*) ; 0 si la traduction est impossible.</summary>
    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    internal static partial uint MapVirtualKey(uint code, uint type);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hwnd);
}
