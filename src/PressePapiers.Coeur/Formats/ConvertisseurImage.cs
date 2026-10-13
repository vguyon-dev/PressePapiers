namespace PressePapiers.Coeur.Formats;

/// <summary>Conversions entre DIB du presse-papiers et PNG, par WIC. Coquille : remplacée par la tâche 15.</summary>
public static class ConvertisseurImage
{
    /// <summary>Convertit un CF_DIB ou CF_DIBV5 en PNG ; l'alpha est conservé pour un V5 avec masque alpha.</summary>
    public static Task<byte[]> DibVersPngAsync(byte[] dib) => throw new NotImplementedException("Tâche 15");

    /// <summary>Convertit un PNG en DIB, pour écrire CF_DIB.</summary>
    public static Task<byte[]> PngVersDibAsync(byte[] png) => throw new NotImplementedException("Tâche 15");

    /// <summary>Réduit un PNG à une miniature de <paramref name="largeurMax"/> pixels de large au plus.</summary>
    public static Task<byte[]> MiniatureAsync(byte[] png, uint largeurMax = 160) => throw new NotImplementedException("Tâche 15");
}
