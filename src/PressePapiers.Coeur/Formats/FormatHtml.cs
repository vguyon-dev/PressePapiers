namespace PressePapiers.Coeur.Formats;

/// <summary>Format « HTML Format » du presse-papiers. Coquille : remplacée par la tâche 13.</summary>
public static class FormatHtml
{
    /// <summary>Encode un fragment : en-tête <c>Version:1.0</c>, offsets en octets UTF-8, lignes terminées par CRLF.</summary>
    public static byte[] Encoder(string fragmentHtml) => throw new NotImplementedException("Tâche 13");

    /// <summary>Extrait le fragment d'un « HTML Format » ; null si les données sont invalides.</summary>
    public static string? ExtraireFragment(ReadOnlySpan<byte> donnees) => throw new NotImplementedException("Tâche 13");
}
