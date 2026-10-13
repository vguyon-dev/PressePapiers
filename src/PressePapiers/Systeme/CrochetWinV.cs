using System.Runtime.InteropServices;
using PressePapiers.Coeur.Clavier;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Hook clavier bas niveau (WH_KEYBOARD_LL) qui reprend Win+V (spec, section 11). Le hook vit sur un fil dédié avec
/// sa boucle de messages. Son rappel ne fait que décider (<see cref="AutomateWinV"/>), envoyer au besoin la touche
/// sans effet et poster un message : Windows retire sans prévenir un hook trop lent. Le signal <see cref="WinVPresse"/>
/// est levé par la boucle du fil du hook, hors du rappel, et ne touche jamais à l'interface.
/// </summary>
public sealed class CrochetWinV : ICrochetWinV
{
    // Messages postés au fil du hook.
    private const uint MessageWinV = NatifClavier.WM_APP + 1;
    private const uint MessageReinstaller = NatifClavier.WM_APP + 2;

    private static readonly TimeSpan _delaiDemarrage = TimeSpan.FromSeconds(5);
    private const uint DelaiReportMs = 300;
    private const int ReportsMaximum = 100;
    private const uint DelaiPeriodiqueMs = 15 * 60 * 1000;

    // Le rappel est une méthode statique unique, son délégué est gardé dans un champ statique : sans cela le
    // ramasse-miettes le libérerait et Windows appellerait une adresse morte. L'état de l'automate est partagé
    // par le hook courant et le nouveau hook posé pendant une réinstallation.
    private static readonly NatifClavier.ProcedureHook _rappel = Rappel;
    private static readonly nint _pointeurRappel = Marshal.GetFunctionPointerForDelegate(_rappel);
    private static readonly AutomateWinV _automate = new();

    // Un seul crochet vivant à la fois : la procédure statique ne sait parler qu'à lui.
    private static CrochetWinV? _actuel;
    private static nint _crochetCourant;
    private static uint _idFilCrochet;

    private static long _frappesVues;

    private readonly IJournal _journal;
    private readonly ManualResetEventSlim _pret = new(false);
    private readonly object _verrou = new();
    private Thread? _fil;
    private volatile bool _actif;
    private volatile string? _raison = "Le hook n'a pas été démarré.";
    private bool _arrete;

    // État touché uniquement par le fil du hook.
    private nuint _minuterieReport;
    private int _reports;

    /// <summary>Crée le hook, sans le démarrer.</summary>
    public CrochetWinV(IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        _journal = journal;
    }

    /// <summary>
    /// Dit si Win, Ctrl, Alt ou Maj est enfoncée. Remplaçable pour l'essai : GetAsyncKeyState ne suit pas les
    /// frappes injectées dans certaines sessions, donc le report de réinstallation ne s'exerce pas avec de fausses frappes.
    /// </summary>
    internal Func<bool> ModificateursEnfonces { get; set; } = ModificateursReelsEnfonces;

    /// <summary>Nombre de frappes vues par le rappel depuis le début du processus (diagnostic, aucune touche conservée).</summary>
    internal static long FrappesVues => Interlocked.Read(ref _frappesVues);

    /// <inheritdoc />
    public bool EstActif => _actif;

    /// <inheritdoc />
    public string? RaisonInactif => _actif ? null : _raison;

    /// <inheritdoc />
    public event Action<nint>? WinVPresse;

    /// <inheritdoc />
    public void Demarrer()
    {
        lock (_verrou)
        {
            ObjectDisposedException.ThrowIf(_arrete, this);
            if (_fil is not null) return;

            if (Interlocked.CompareExchange(ref _actuel, this, null) is not null)
                throw new InvalidOperationException("Un autre crochet Win+V est déjà en service dans ce processus.");

            _fil = new Thread(BoucleDuFil) { Name = "CrochetWinV", IsBackground = true };
            _fil.Start();
        }

        // Le fil dit quand l'installation est jouée, réussie ou non ; on n'attend que quelques secondes.
        if (!_pret.Wait(_delaiDemarrage))
        {
            _raison = "Le fil du hook n'a pas répondu à temps.";
            _journal.Erreur(_raison);
        }
    }

    /// <inheritdoc />
    public void Reinstaller()
    {
        uint idFil;
        lock (_verrou)
        {
            if (_fil is null || _arrete) return;
            idFil = _idFilCrochet;
        }

        // Le remplacement se joue sur le fil du hook, à l'abri des frappes en cours de traitement.
        if (!NatifClavier.PostThreadMessage(idFil, MessageReinstaller, 0, 0))
            _journal.Avertissement("Réinstallation du hook impossible : le message n'a pas pu être posté.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Thread? fil;
        uint idFil;
        lock (_verrou)
        {
            if (_arrete) return;
            _arrete = true;
            fil = _fil;
            idFil = _idFilCrochet;
        }

        if (fil is not null)
        {
            NatifClavier.PostThreadMessage(idFil, NatifClavier.WM_QUIT, 0, 0);
            if (!fil.Join(TimeSpan.FromSeconds(2))) _journal.Avertissement("Le fil du hook ne s'est pas arrêté à temps.");
        }

        _actif = false;
        Interlocked.CompareExchange(ref _actuel, null, this);
        _pret.Dispose();
    }

    private void BoucleDuFil()
    {
        // Un premier PeekMessage crée la file de messages du fil : sans elle PostThreadMessage échouerait.
        NatifClavier.PeekMessage(out _, 0, NatifClavier.WM_USER, NatifClavier.WM_USER, NatifClavier.PM_NOREMOVE);
        _idFilCrochet = NatifClavier.GetCurrentThreadId();

        var minuteriePeriodique = NatifClavier.SetTimer(0, 0, DelaiPeriodiqueMs, 0);

        InstallerPremierCrochet();
        _pret.Set();

        // Boucle de messages obligatoire : sans elle Windows n'appelle jamais le hook bas niveau.
        while (NatifClavier.GetMessage(out var message, 0, 0, 0) > 0)
        {
            switch (message.Identifiant)
            {
                case MessageWinV:
                    LeverWinVPresse(message.WParam);
                    break;
                case MessageReinstaller:
                    TenterReinstallation(depuisReport: false);
                    break;
                case NatifClavier.WM_TIMER when (nuint)message.WParam == minuteriePeriodique:
                    TenterReinstallation(depuisReport: false);
                    break;
                case NatifClavier.WM_TIMER when _minuterieReport != 0 && (nuint)message.WParam == _minuterieReport:
                    TenterReinstallation(depuisReport: true);
                    break;
            }
        }

        if (minuteriePeriodique != 0) NatifClavier.KillTimer(0, minuteriePeriodique);
        ArreterReport();
        RetirerCrochet();
    }

    private void InstallerPremierCrochet()
    {
        var crochet = PoserCrochet();
        if (crochet == 0)
        {
            var erreur = Marshal.GetLastWin32Error();
            _raison = $"SetWindowsHookEx a échoué (erreur {erreur}).";
            _journal.Erreur(_raison);
            return;
        }

        _automate.Reinitialiser();
        _crochetCourant = crochet;
        _raison = null;
        _actif = true;
        _journal.Info("Hook Win+V posé.");
    }

    // Remplace le hook. Le neuf est posé avant le retrait de l'ancien : posé en tête de chaîne, le neuf avale
    // la frappe avant l'ancien, qui ne la reçoit donc pas ; il n'y a jamais de fenêtre sans hook.
    private void TenterReinstallation(bool depuisReport)
    {
        if (ModificateursEnfonces())
        {
            // Remplacer le hook pendant qu'une touche est tenue ferait perdre un relâchement à l'automate.
            if (!depuisReport) _reports = 0;
            if (++_reports > ReportsMaximum)
            {
                ArreterReport();
                _journal.Avertissement("Réinstallation du hook abandonnée : des modificateurs sont restés enfoncés.");
                return;
            }

            if (_minuterieReport == 0) _minuterieReport = NatifClavier.SetTimer(0, 0, DelaiReportMs, 0);
            return;
        }

        ArreterReport();

        var neuf = PoserCrochet();
        if (neuf == 0)
        {
            _journal.Avertissement($"Réinstallation du hook impossible (erreur {Marshal.GetLastWin32Error()}) ; l'ancien reste en place.");
            return;
        }

        var ancien = _crochetCourant;
        _crochetCourant = neuf;
        if (ancien != 0) NatifClavier.UnhookWindowsHookEx(ancien);

        // Le hook ne voit pas les relâchements survenus devant une fenêtre élevée ou l'écran de verrouillage.
        _automate.Reinitialiser();
        _reports = 0;
        _raison = null;
        _actif = true;
        _journal.Info("Hook Win+V réinstallé.");
    }

    private void ArreterReport()
    {
        if (_minuterieReport == 0) return;
        NatifClavier.KillTimer(0, _minuterieReport);
        _minuterieReport = 0;
    }

    private void RetirerCrochet()
    {
        var crochet = _crochetCourant;
        _crochetCourant = 0;
        _actif = false;
        if (crochet != 0) NatifClavier.UnhookWindowsHookEx(crochet);
    }

    private void LeverWinVPresse(nint fenetre)
    {
        try
        {
            WinVPresse?.Invoke(fenetre);
        }
        catch (Exception exception)
        {
            // Un abonné défaillant ne doit pas arrêter la boucle du hook.
            _journal.Erreur("Un abonné de WinVPresse a levé une exception.", exception);
        }
    }

    private static nint PoserCrochet() =>
        NatifClavier.SetWindowsHookEx(NatifClavier.WH_KEYBOARD_LL, _pointeurRappel, NatifClavier.GetModuleHandle(null), 0);

    private static bool EstEnfoncee(int touche) => (NatifClavier.GetAsyncKeyState(touche) & 0x8000) != 0;

    private static bool ModificateursReelsEnfonces() =>
        EstEnfoncee(NatifClavier.VK_LWIN) || EstEnfoncee(NatifClavier.VK_RWIN) || EstEnfoncee(NatifClavier.VK_CONTROL)
        || EstEnfoncee(NatifClavier.VK_MENU) || EstEnfoncee(NatifClavier.VK_SHIFT);

    // Procédure unique du hook. Elle doit rendre la main très vite et ne jamais laisser filer une exception.
    private static nint Rappel(int code, nint wParam, nint lParam)
    {
        if (code < 0) return NatifClavier.CallNextHookEx(_crochetCourant, code, wParam, lParam);

        try
        {
            Interlocked.Increment(ref _frappesVues);

            var touche = (uint)Marshal.ReadInt32(lParam, NatifClavier.DecalageCodeTouche);
            var drapeaux = (uint)Marshal.ReadInt32(lParam, NatifClavier.DecalageDrapeaux);
            var message = (int)wParam;
            var enfoncee = message is NatifClavier.WM_KEYDOWN or NatifClavier.WM_SYSKEYDOWN;
            var relachee = message is NatifClavier.WM_KEYUP or NatifClavier.WM_SYSKEYUP;
            if (!enfoncee && !relachee) return NatifClavier.CallNextHookEx(_crochetCourant, code, wParam, lParam);

            var injectee = (drapeaux & NatifClavier.LLKHF_INJECTED) != 0;

            // L'état réel de Win n'est utile (et lu) que pour V : les autres touches ne l'interrogent pas.
            var winReelle = touche == AutomateWinV.VkV && !injectee
                            && (EstEnfoncee(NatifClavier.VK_LWIN) || EstEnfoncee(NatifClavier.VK_RWIN));

            switch (_automate.Traiter(touche, enfoncee, injectee, winReelle))
            {
                case ActionCrochet.Avaler:
                    return 1;

                case ActionCrochet.AvalerEtOuvrir:
                    // La fenêtre active est relevée ici, dans le rappel, avant que quoi que ce soit ne la change.
                    NatifClavier.PostThreadMessage(_idFilCrochet, MessageWinV, NatifClavier.GetForegroundWindow(), 0);
                    return 1;

                case ActionCrochet.MasquerPuisLaisser:
                    EnvoyerToucheSansEffet();
                    break;
            }
        }
        catch
        {
            // Rien ne doit remonter dans Windows ; la frappe suit son chemin.
        }

        return NatifClavier.CallNextHookEx(_crochetCourant, code, wParam, lParam);
    }

    // Une touche sans effet (code 0xE8) glissée avant le relâchement de Win : Windows ne voit plus « Win seule »
    // et n'ouvre pas le menu Démarrer.
    private static void EnvoyerToucheSansEffet()
    {
        var entrees = new NatifClavier.Entree[2];
        entrees[0].Type = NatifClavier.INPUT_KEYBOARD;
        entrees[0].Clavier.Touche = NatifClavier.VK_MASQUE;
        entrees[1].Type = NatifClavier.INPUT_KEYBOARD;
        entrees[1].Clavier.Touche = NatifClavier.VK_MASQUE;
        entrees[1].Clavier.Drapeaux = NatifClavier.KEYEVENTF_KEYUP;
        NatifClavier.SendInput(2, entrees, Marshal.SizeOf<NatifClavier.Entree>());
    }
}
