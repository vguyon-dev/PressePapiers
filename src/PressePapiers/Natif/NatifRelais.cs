using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>
/// Déclarations Win32 de la fenêtre relais : classe de fenêtre, écoute du presse-papiers, raccourci global, messages
/// enregistrés et notifications de session. Une même fonction peut être déclarée ailleurs : chaque brique reste autonome.
/// </summary>
internal static partial class NatifRelais
{
    // Messages traités par la procédure du relais.
    internal const uint WmHotkey = 0x0312;
    internal const uint WmClipboardUpdate = 0x031D;
    internal const uint WmPowerBroadcast = 0x0218;
    internal const uint WmWtsSessionChange = 0x02B1;
    internal const uint WmTimer = 0x0113;

    // Paramètres de ces messages.
    internal const int PbtApmResumeAutomatic = 0x0012;
    internal const int WtsSessionUnlock = 0x0008;

    // WS_POPUP : fenêtre de premier niveau, sans bordure ; jamais affichée. WS_EX_TOOLWINDOW et WS_EX_NOACTIVATE
    // l'écartent de la barre des tâches et d'Alt+Tab au cas où elle serait un jour montrée.
    internal const uint WsPopup = 0x80000000;
    internal const uint WsExToolWindow = 0x00000080;
    internal const uint WsExNoActivate = 0x08000000;

    // MSGFLT_ALLOW : laisse passer le message même si le processus tourne à un niveau d'intégrité plus élevé.
    internal const uint MsgfltAllow = 1;

    // NOTIFY_FOR_THIS_SESSION : seules les notifications de la session de l'utilisateur.
    internal const uint NotifyForThisSession = 0;

    /// <summary>Procédure de fenêtre (WNDPROC).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint ProcedureFenetre(nint hwnd, uint message, nint wParam, nint lParam);

    /// <summary>WNDCLASSEXW ; les pointeurs de texte sont alloués par l'appelant.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ClasseFenetre
    {
        public uint Taille;
        public uint Style;
        public nint Procedure;
        public int OctetsSupplementairesClasse;
        public int OctetsSupplementairesFenetre;
        public nint Instance;
        public nint Icone;
        public nint Curseur;
        public nint Fond;
        public nint NomMenu;
        public nint NomClasse;
        public nint IconePetite;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")]
    internal static partial nint ObtenirModule(nint nomModule);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort EnregistrerClasse(in ClasseFenetre classe);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RetirerClasse(string nomClasse, nint instance);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreerFenetre(uint styleEtendu, string nomClasse, string? titre, uint style,
        int x, int y, int largeur, int hauteur, nint parent, nint menu, nint instance, nint parametre);

    [LibraryImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DetruireFenetre(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint ProcedureParDefaut(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "AddClipboardFormatListener", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AjouterEcouteurPressePapiers(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "RemoveClipboardFormatListener", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RetirerEcouteurPressePapiers(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnregistrerRaccourciGlobal(nint hwnd, int identifiant, uint modificateurs, uint touche);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RetirerRaccourciGlobal(nint hwnd, int identifiant);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint EnregistrerMessage(string nom);

    [LibraryImport("user32.dll", EntryPoint = "ChangeWindowMessageFilterEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AutoriserMessage(nint hwnd, uint message, uint action, nint structureResultat);

    [LibraryImport("user32.dll", EntryPoint = "SetTimer", SetLastError = true)]
    internal static partial nuint PoserMinuterie(nint hwnd, nuint identifiant, uint delaiMs, nint rappel);

    [LibraryImport("user32.dll", EntryPoint = "KillTimer")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RetirerMinuterie(nint hwnd, nuint identifiant);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSRegisterSessionNotification", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InscrireNotificationsSession(nint hwnd, uint portee);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSUnRegisterSessionNotification", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DesinscrireNotificationsSession(nint hwnd);
}
