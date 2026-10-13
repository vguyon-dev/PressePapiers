namespace PressePapiers.Coeur.Formats;

/// <summary>Format CF_HDROP (structure DROPFILES). Coquille : remplacée par la tâche 14.</summary>
public static class ListeFichiers
{
    /// <summary>Encode des chemins en structure DROPFILES, chemins en UTF-16.</summary>
    public static byte[] EncoderDropFiles(IReadOnlyList<string> chemins) => throw new NotImplementedException("Tâche 14");

    /// <summary>Décode une structure DROPFILES en liste de chemins.</summary>
    public static IReadOnlyList<string> DecoderDropFiles(ReadOnlySpan<byte> donnees) => throw new NotImplementedException("Tâche 14");
}
