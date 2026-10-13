using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows de l'ouverture de la fenêtre : échelle d'un écran et position du curseur (spec, section 8).</summary>
internal static partial class NatifOuverture
{
    /// <summary>MDT_EFFECTIVE_DPI : la résolution réellement appliquée sur l'écran, mise à l'échelle comprise.</summary>
    internal const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>POINT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    /// <summary>Résolution d'un écran ; renvoie 0 (S_OK) quand elle est connue.</summary>
    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(nint ecran, int type, out uint dpiX, out uint dpiY);

    /// <summary>Position du curseur, en pixels de l'écran virtuel.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point position);
}
