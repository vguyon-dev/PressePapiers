namespace PressePapiers.Coeur.Clavier;

/// <summary>
/// Ignore un signal trop proche du dernier signal accepté (double Win+V, rafale du raccourci de secours).
/// Sûr entre fils : les signaux arrivent du fil d'interface et du fil du hook.
/// </summary>
public sealed class AntiRebond
{
    private readonly TimeProvider _horloge;
    private readonly TimeSpan _delai;
    private readonly object _verrou = new();

    // Instant (horloge monotone) du dernier signal ACCEPTÉ ; un signal refusé ne repousse pas la fenêtre.
    private long? _dernierAccepte;

    /// <summary>Crée l'anti-rebond avec son horloge et son délai.</summary>
    public AntiRebond(TimeProvider horloge, TimeSpan delai)
    {
        ArgumentNullException.ThrowIfNull(horloge);
        _horloge = horloge;
        _delai = delai;
    }

    /// <summary>Faux si le dernier signal accepté date de moins que le délai.</summary>
    public bool Accepter()
    {
        lock (_verrou)
        {
            var maintenant = _horloge.GetTimestamp();
            if (_dernierAccepte is long precedent && _horloge.GetElapsedTime(precedent, maintenant) < _delai)
                return false;

            _dernierAccepte = maintenant;
            return true;
        }
    }
}
