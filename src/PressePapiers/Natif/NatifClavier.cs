using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows du hook clavier bas niveau (spec, section 11). Chaque brique native est autonome.</summary>
internal static partial class NatifClavier
{
    internal const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;
    internal const uint WM_QUIT = 0x0012;
    internal const uint WM_TIMER = 0x0113;
    internal const uint WM_APP = 0x8000;
    internal const uint PM_NOREMOVE = 0x0000;
    internal const uint WM_USER = 0x0400;

    /// <summary>Drapeau LLKHF_INJECTED de KBDLLHOOKSTRUCT.flags : frappe produite par SendInput.</summary>
    internal const uint LLKHF_INJECTED = 0x10;

    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;

    internal const int VK_SHIFT = 0x10;
    internal const int VK_CONTROL = 0x11;
    internal const int VK_MENU = 0x12;
    internal const int VK_LWIN = 0x5B;
    internal const int VK_RWIN = 0x5C;

    /// <summary>Touche « sans effet » envoyée avant le relâchement de Win pour que le menu Démarrer reste fermé.</summary>
    internal const ushort VK_MASQUE = 0xE8;

    /// <summary>Décalages des champs de KBDLLHOOKSTRUCT (vkCode, scanCode, flags, time).</summary>
    internal const int DecalageCodeTouche = 0;
    internal const int DecalageDrapeaux = 8;
    internal const int DecalageHeure = 12;

    /// <summary>Action de SystemParametersInfo qui lit le délai de répétition du clavier (0 à 3, soit (n + 1) x 250 ms).</summary>
    internal const uint SPI_GETKEYBOARDDELAY = 0x0016;

    /// <summary>MSG avec son champ de fin : la structure fait 48 octets en x64.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        public nint Fenetre;
        public uint Identifiant;
        public nint WParam;
        public nint LParam;
        public uint Heure;
        public int X;
        public int Y;
        public uint Prive;
    }

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

    /// <summary>Signature de LowLevelKeyboardProc ; le délégué doit être gardé vivant par l'appelant.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint ProcedureHook(int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static partial nint SetWindowsHookEx(int idHook, nint procedure, nint module, uint idFil);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(nint crochet);

    [LibraryImport("user32.dll")]
    internal static partial nint CallNextHookEx(nint crochet, int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    internal static partial int GetMessage(out Message message, nint fenetre, uint min, uint max);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PeekMessage(out Message message, nint fenetre, uint min, uint max, uint retrait);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostThreadMessage(uint idFil, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int touche);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfo(uint action, uint param, out int valeur, uint winIni);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint nombre, Entree[] entrees, int tailleEntree);

    [LibraryImport("user32.dll")]
    internal static partial nuint SetTimer(nint fenetre, nuint idTimer, uint delaiMs, nint rappel);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool KillTimer(nint fenetre, nuint idTimer);

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? nom);
}
