namespace PressePapiers.Capture;

/// <summary>Format privé que l'outil pose sur chacune de ses écritures pour ignorer ses propres copies. Coquille : tâche 21.</summary>
public static class MarqueInterne
{
    /// <summary>Nom du format enregistré.</summary>
    public const string NomFormat = "PressePapiers.Interne";

    /// <summary>Identifiant du format (RegisterClipboardFormat), vérifié non nul.</summary>
    public static uint Format => throw new NotImplementedException("Tâche 21");
}
