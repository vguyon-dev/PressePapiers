namespace PressePapiers.Capture;

/// <summary>Format privé que l'outil pose sur chacune de ses écritures pour ignorer ses propres copies.</summary>
public static class MarqueInterne
{
    /// <summary>Nom du format enregistré.</summary>
    public const string NomFormat = "PressePapiers.Interne";

    /// <summary>Identifiant du format (RegisterClipboardFormat), vérifié non nul.</summary>
    /// <exception cref="InvalidOperationException">Windows refuse d'enregistrer le format.</exception>
    public static uint Format => FormatsPressePapiers.Enregistrer(NomFormat);
}
