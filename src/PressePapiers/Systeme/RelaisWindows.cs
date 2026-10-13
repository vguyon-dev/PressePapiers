using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Fenêtre Win32 de premier niveau invisible, sur le fil d'interface : reçoit les copies, le raccourci de secours,
/// les messages de l'icône et les changements de session. Ce n'est pas une fenêtre « message-only » : celles-ci ne
/// reçoivent pas les messages diffusés, donc pas « TaskbarCreated ». Elle doit être créée et libérée sur le fil
/// d'interface, qui pompe ses messages.
/// </summary>
public sealed class RelaisWindows : IDisposable
{
    /// <summary>Message de rappel de l'icône de notification (WM_APP + 1).</summary>
    public const uint MessageIcone = 0x8001;

    // Identifiant du raccourci de secours auprès de RegisterHotKey.
    private const int IdRaccourci = 1;

    // Minuterie des nouveaux essais d'inscription aux notifications de session : au démarrage de Windows, les services
    // Bureau à distance peuvent ne pas être prêts (WTSRegisterSessionNotification échoue alors, souvent avec 1702).
    private const nuint IdMinuterieSession = 1;
    private const uint DelaiEssaiSessionMs = 10_000;
    private const int EssaisSessionMax = 30;

    // Procédures des fenêtres que DestroyWindow n'a pas pu détruire : Windows peut encore les appeler, le délégué doit
    // donc survivre à l'objet, jusqu'à la fin du processus.
    private static readonly List<NatifRelais.ProcedureFenetre> _proceduresOrphelines = [];

    private readonly IJournal _journal;

    // La procédure est gardée dans un champ : le pointeur donné à Windows ne retient pas le délégué, et le ramasse-miettes
    // le libérerait pendant que la fenêtre vit encore.
    private readonly NatifRelais.ProcedureFenetre _procedure;
    private readonly string _nomClasse;
    private readonly nint _instance;
    private readonly uint _messageBarreDesTaches;

    private nint _hwnd;
    private bool _libere;
    private bool _ecoutePressePapiers;
    private bool _sessionInscrite;
    private bool _minuterieSession;
    private int _essaisSession;
    private Raccourci? _raccourci;

    /// <summary>Crée la fenêtre sur le fil courant, qui doit être le fil d'interface.</summary>
    /// <param name="journal">Journal des échecs de configuration.</param>
    /// <exception cref="Win32Exception">La classe ou la fenêtre n'a pas pu être créée.</exception>
    public RelaisWindows(IJournal journal)
    {
        _journal = journal;
        _procedure = Traiter;
        _instance = NatifRelais.ObtenirModule(0);

        // Un nom de classe propre à l'instance : deux relais dans un même processus ne se gênent pas.
        _nomClasse = $"PressePapiers.Relais.{Guid.NewGuid():N}";

        var nomNatif = Marshal.StringToHGlobalUni(_nomClasse);
        try
        {
            var classe = new NatifRelais.ClasseFenetre
            {
                Taille = (uint)Unsafe.SizeOf<NatifRelais.ClasseFenetre>(),
                Procedure = Marshal.GetFunctionPointerForDelegate(_procedure),
                Instance = _instance,
                NomClasse = nomNatif,
            };
            if (NatifRelais.EnregistrerClasse(classe) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Enregistrement de la classe du relais impossible.");
            }
        }
        finally
        {
            // Windows garde sa propre copie du nom.
            Marshal.FreeHGlobal(nomNatif);
        }

        var hwnd = NatifRelais.CreerFenetre(
            NatifRelais.WsExToolWindow | NatifRelais.WsExNoActivate, _nomClasse, "PressePapiers relais",
            NatifRelais.WsPopup, 0, 0, 0, 0, 0, 0, _instance, 0);
        if (hwnd == 0)
        {
            var code = Marshal.GetLastPInvokeError();
            NatifRelais.RetirerClasse(_nomClasse, _instance);
            throw new Win32Exception(code, "Création de la fenêtre du relais impossible.");
        }

        _hwnd = hwnd;

        // Les échecs suivants ne sont pas fatals : l'outil reste utilisable, sans la fonction concernée.
        _messageBarreDesTaches = NatifRelais.EnregistrerMessage("TaskbarCreated");
        if (_messageBarreDesTaches == 0)
        {
            _journal.Avertissement($"Message TaskbarCreated non enregistré (code {Marshal.GetLastPInvokeError()}).");
        }
        else if (!NatifRelais.AutoriserMessage(_hwnd, _messageBarreDesTaches, NatifRelais.MsgfltAllow, 0))
        {
            _journal.Avertissement($"TaskbarCreated non autorisé par le filtre de messages (code {Marshal.GetLastPInvokeError()}).");
        }

        // Lancé en administrateur (tâche planifiée), le processus filtre les messages envoyés par l'Explorateur, de niveau
        // inférieur (UIPI) : le rappel de l'icône doit être autorisé comme TaskbarCreated, sinon un clic sur l'icône
        // pourrait rester sans effet. Sans objet, mais sans danger, pour un processus ordinaire.
        if (!NatifRelais.AutoriserMessage(_hwnd, MessageIcone, NatifRelais.MsgfltAllow, 0))
        {
            _journal.Avertissement($"Le message de l'icône n'est pas autorisé par le filtre de messages (code {Marshal.GetLastPInvokeError()}).");
        }

        InscrireSession();
    }

    /// <summary>Handle de la fenêtre relais ; zéro une fois le relais libéré.</summary>
    public nint Handle => _hwnd;

    /// <summary>WM_CLIPBOARDUPDATE.</summary>
    public event Action? CopieDetectee;

    /// <summary>WM_HOTKEY, identifiant 1.</summary>
    public event Action? RaccourciSecoursPresse;

    /// <summary>Message de l'icône ; l'argument est le LOWORD de lParam.</summary>
    public event Action<uint>? MessageIconeRecu;

    /// <summary>Message enregistré « TaskbarCreated » : l'Explorateur a redémarré.</summary>
    public event Action? BarreDesTachesRecreee;

    /// <summary>WM_WTSSESSION_CHANGE avec WTS_SESSION_UNLOCK.</summary>
    public event Action? SessionDeverrouillee;

    /// <summary>WM_POWERBROADCAST avec PBT_APMRESUMEAUTOMATIC.</summary>
    public event Action? SortieDeVeille;

    /// <summary>Commence à écouter le presse-papiers (AddClipboardFormatListener) ; faux en cas d'échec.</summary>
    /// <returns>Vrai si l'écoute est en place (y compris si elle l'était déjà).</returns>
    public bool EcouterPressePapiers()
    {
        ControlerVivant();
        if (_ecoutePressePapiers)
        {
            return true;
        }

        _ecoutePressePapiers = NatifRelais.AjouterEcouteurPressePapiers(_hwnd);
        if (!_ecoutePressePapiers)
        {
            _journal.Erreur($"Écoute du presse-papiers impossible (code {Marshal.GetLastPInvokeError()}).");
        }

        return _ecoutePressePapiers;
    }

    /// <summary>
    /// Enregistre le raccourci de secours en remplaçant le précédent. Si le nouveau est refusé (déjà pris par une autre
    /// application, par exemple), le précédent est rétabli : une saisie malheureuse ne laisse pas l'outil sans secours.
    /// </summary>
    /// <param name="raccourci">Combinaison à enregistrer.</param>
    /// <param name="codeErreur">Code d'erreur Windows en cas d'échec (1409 : combinaison déjà prise), sinon zéro.</param>
    /// <returns>Vrai si le raccourci est enregistré.</returns>
    public bool EnregistrerRaccourci(Raccourci raccourci, out int codeErreur)
    {
        ControlerVivant();

        var precedent = _raccourci;
        if (precedent is not null)
        {
            NatifRelais.RetirerRaccourciGlobal(_hwnd, IdRaccourci);
            _raccourci = null;
        }

        if (NatifRelais.EnregistrerRaccourciGlobal(_hwnd, IdRaccourci, raccourci.Modificateurs, raccourci.ToucheVirtuelle))
        {
            _raccourci = raccourci;
            codeErreur = 0;
            return true;
        }

        codeErreur = Marshal.GetLastPInvokeError();
        _journal.Avertissement($"Raccourci de secours {raccourci} refusé (code {codeErreur}).");

        if (precedent is { } ancien)
        {
            if (NatifRelais.EnregistrerRaccourciGlobal(_hwnd, IdRaccourci, ancien.Modificateurs, ancien.ToucheVirtuelle))
            {
                _raccourci = ancien;
            }
            else
            {
                _journal.Erreur($"L'ancien raccourci de secours {ancien} n'a pas pu être rétabli (code {Marshal.GetLastPInvokeError()}) : "
                                + "l'outil n'a plus de raccourci de secours.");
            }
        }

        return false;
    }

    /// <summary>Retire le raccourci de secours ; sans effet s'il n'est pas enregistré.</summary>
    public void RetirerRaccourci()
    {
        if (_libere || _raccourci is null)
        {
            return;
        }

        NatifRelais.RetirerRaccourciGlobal(_hwnd, IdRaccourci);
        _raccourci = null;
    }

    /// <summary>Retire le raccourci, l'écoute et les inscriptions, puis détruit la fenêtre. Sans effet au second appel.</summary>
    public void Dispose()
    {
        if (_libere)
        {
            return;
        }

        RetirerRaccourci();
        ArreterMinuterieSession();
        if (_ecoutePressePapiers)
        {
            NatifRelais.RetirerEcouteurPressePapiers(_hwnd);
            _ecoutePressePapiers = false;
        }

        if (_sessionInscrite)
        {
            NatifRelais.DesinscrireNotificationsSession(_hwnd);
            _sessionInscrite = false;
        }

        // Le drapeau passe avant la destruction : les messages de fin de vie ne lèvent plus d'événement.
        _libere = true;
        var hwnd = _hwnd;
        _hwnd = 0;

        // DestroyWindow n'agit que sur le fil créateur ; ailleurs, la fenêtre reste et l'échec est noté. Sa procédure
        // reste alors joignable par Windows : le délégué est retenu, et la classe, encore utilisée, n'est pas retirée.
        if (!NatifRelais.DetruireFenetre(hwnd))
        {
            _journal.Avertissement($"Destruction du relais impossible (code {Marshal.GetLastPInvokeError()}), appelée depuis le mauvais fil ?");
            lock (_proceduresOrphelines)
            {
                _proceduresOrphelines.Add(_procedure);
            }

            return;
        }

        NatifRelais.RetirerClasse(_nomClasse, _instance);
    }

    private void ControlerVivant() => ObjectDisposedException.ThrowIf(_libere, this);

    // Inscription aux notifications de session (déverrouillage) ; en cas d'échec, nouvel essai toutes les 10 secondes
    // pendant cinq minutes. Un seul avertissement au premier échec, puis une ligne à l'issue.
    private void InscrireSession()
    {
        _essaisSession++;
        _sessionInscrite = NatifRelais.InscrireNotificationsSession(_hwnd, NatifRelais.NotifyForThisSession);
        if (_sessionInscrite)
        {
            ArreterMinuterieSession();
            if (_essaisSession > 1)
            {
                _journal.Info($"Notifications de session inscrites au {_essaisSession}e essai.");
            }

            return;
        }

        var code = Marshal.GetLastPInvokeError();
        if (_essaisSession >= EssaisSessionMax)
        {
            ArreterMinuterieSession();
            _journal.Avertissement($"Notifications de session toujours refusées après {EssaisSessionMax} essais (code {code}) : "
                                   + "le hook Win+V ne sera pas réinstallé au déverrouillage.");
            return;
        }

        if (_essaisSession == 1)
        {
            _journal.Avertissement($"Notifications de session non inscrites (code {code}) : nouvel essai toutes les "
                                   + $"{DelaiEssaiSessionMs / 1000} s.");
        }

        if (!_minuterieSession)
        {
            _minuterieSession = NatifRelais.PoserMinuterie(_hwnd, IdMinuterieSession, DelaiEssaiSessionMs, 0) != 0;
            if (!_minuterieSession)
            {
                _journal.Avertissement($"Minuterie des nouveaux essais impossible (code {Marshal.GetLastPInvokeError()}).");
            }
        }
    }

    private void ArreterMinuterieSession()
    {
        if (!_minuterieSession)
        {
            return;
        }

        NatifRelais.RetirerMinuterie(_hwnd, IdMinuterieSession);
        _minuterieSession = false;
    }

    // Procédure de fenêtre : une exception qui la traverserait remonterait dans du code natif et tuerait le processus.
    private nint Traiter(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (_libere || _hwnd == 0)
        {
            return NatifRelais.ProcedureParDefaut(hwnd, message, wParam, lParam);
        }

        try
        {
            if (_messageBarreDesTaches != 0 && message == _messageBarreDesTaches)
            {
                BarreDesTachesRecreee?.Invoke();
                return 0;
            }

            switch (message)
            {
                case NatifRelais.WmClipboardUpdate:
                    CopieDetectee?.Invoke();
                    return 0;

                case NatifRelais.WmHotkey when wParam == IdRaccourci:
                    RaccourciSecoursPresse?.Invoke();
                    return 0;

                case MessageIcone:
                    MessageIconeRecu?.Invoke((uint)(lParam & 0xFFFF));
                    return 0;

                case NatifRelais.WmWtsSessionChange when wParam == NatifRelais.WtsSessionUnlock:
                    SessionDeverrouillee?.Invoke();
                    return 0;

                case NatifRelais.WmTimer when (nuint)wParam == IdMinuterieSession:
                    InscrireSession();
                    return 0;

                case NatifRelais.WmPowerBroadcast when wParam == NatifRelais.PbtApmResumeAutomatic:
                    SortieDeVeille?.Invoke();
                    return 1; // TRUE : message pris en compte
            }
        }
        catch (Exception exception)
        {
            _journal.Erreur($"Exception dans un abonné du relais (message 0x{message:X4}).", exception);
        }

        return NatifRelais.ProcedureParDefaut(hwnd, message, wParam, lParam);
    }
}
