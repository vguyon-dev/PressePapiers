namespace PressePapiers.Coeur.Clavier;

/// <summary>
/// Logique pure du hook Win+V (spec, section 11), testable sans Windows. L'automate ne fait que décider :
/// l'envoi de la touche sans effet et le signal d'ouverture appartiennent à l'appelant.
/// </summary>
public sealed class AutomateWinV
{
    /// <summary>Touche Windows gauche.</summary>
    public const uint VkWinGauche = 0x5B;

    /// <summary>Touche Windows droite.</summary>
    public const uint VkWinDroite = 0x5C;

    /// <summary>Touche V.</summary>
    public const uint VkV = 0x56;

    /// <summary>Délai de répétition du clavier supposé quand l'appelant ne le connaît pas (réglage Windows par défaut, 500 ms).</summary>
    public const uint DelaiRepetitionParDefautMs = 500;

    // Marge ajoutée au délai de répétition du clavier : un enfoncement de V qui suit le précédent de moins que
    // (délai + marge) est la répétition automatique de la touche tenue, pas une frappe neuve.
    private const uint MargeRepetitionMs = 150;

    // Le hook décide sur son fil, mais la remise à zéro peut venir d'ailleurs (déverrouillage, sortie de veille).
    private readonly object _verrou = new();

    private bool _winGaucheEnfoncee;
    private bool _winDroiteEnfoncee;

    // V a été avalée et n'a pas encore été relâchée : toute répétition suivante n'est qu'avalée.
    private bool _vAvalee;

    // Win+V a eu lieu : au relâchement de Win, une touche sans effet doit précéder pour que le menu Démarrer reste fermé.
    private bool _masqueAEnvoyer;

    // Heure (ms, champ time de KBDLLHOOKSTRUCT) du dernier enfoncement de V avalé ou reconnu comme sa répétition.
    private uint _heureDerniereV;

    // Écart maximal entre deux enfoncements de V pour que le second passe pour une répétition.
    private readonly uint _ecartRepetitionMaxMs;

    /// <summary>Crée l'automate.</summary>
    /// <param name="delaiRepetitionClavierMs">
    /// Délai avant la première répétition d'une touche tenue, en millisecondes (réglage du clavier de Windows) : il sert
    /// à distinguer la répétition de V, avalée, d'une frappe neuve après un relâchement que le hook n'a pas vu.
    /// </param>
    public AutomateWinV(uint delaiRepetitionClavierMs = DelaiRepetitionParDefautMs)
    {
        _ecartRepetitionMaxMs = delaiRepetitionClavierMs + MargeRepetitionMs;
    }

    private bool WinEnfoncee => _winGaucheEnfoncee || _winDroiteEnfoncee;

    /// <summary>
    /// Décide du sort d'une frappe. <paramref name="injectee"/> : frappe portant LLKHF_INJECTED (toujours laissée) ;
    /// <paramref name="winReellementEnfoncee"/> : résultat de GetAsyncKeyState au moment de V ;
    /// <paramref name="temps"/> : heure de la frappe en millisecondes (champ time de KBDLLHOOKSTRUCT, compteur 32 bits
    /// qui peut repasser par zéro) ; <paramref name="autreModificateurEnfonce"/> : Ctrl, Alt ou Maj tenue avec V, ce qui
    /// fait de Win+V un raccourci de Windows (Win+Ctrl+V par exemple), laissé passer sans oublier que Win est tenue.
    /// </summary>
    public ActionCrochet Traiter(uint touche, bool enfoncee, bool injectee, bool winReellementEnfoncee, uint temps,
                                 bool autreModificateurEnfonce = false)
    {
        // Nos propres frappes (touche sans effet, Ctrl+V du recollage) ne doivent jamais armer ni déclencher l'automate.
        if (injectee) return ActionCrochet.Laisser;

        lock (_verrou)
        {
            if (touche is VkWinGauche or VkWinDroite) return TraiterWin(touche, enfoncee);
            if (touche == VkV) return TraiterV(enfoncee, winReellementEnfoncee, temps, autreModificateurEnfonce);
            return ActionCrochet.Laisser;
        }
    }

    /// <summary>Oublie tout l'état : déverrouillage, sortie de veille, réinstallation du hook.</summary>
    public void Reinitialiser()
    {
        lock (_verrou) RemettreAZero();
    }

    private ActionCrochet TraiterWin(uint touche, bool enfoncee)
    {
        if (enfoncee)
        {
            // Un nouvel appui sur Win (et non la répétition d'un appui déjà vu) recommence une séquence propre.
            if (!WinEnfoncee)
            {
                _vAvalee = false;
                _masqueAEnvoyer = false;
            }

            if (touche == VkWinGauche) _winGaucheEnfoncee = true; else _winDroiteEnfoncee = true;
            return ActionCrochet.Laisser;
        }

        if (touche == VkWinGauche) _winGaucheEnfoncee = false; else _winDroiteEnfoncee = false;

        if (!_masqueAEnvoyer) return ActionCrochet.Laisser;

        _masqueAEnvoyer = false;
        return ActionCrochet.MasquerPuisLaisser;
    }

    private ActionCrochet TraiterV(bool enfoncee, bool winReellementEnfoncee, uint temps, bool autreModificateurEnfonce)
    {
        if (WinEnfoncee)
        {
            // L'état dit « Win enfoncée » mais la touche ne l'est plus : un relâchement a échappé au hook
            // (fenêtre élevée, écran de verrouillage). On ne vole pas une frappe sur un état périmé.
            if (!winReellementEnfoncee)
            {
                RemettreAZero();
                return ActionCrochet.Laisser;
            }

            // Win est bien tenue mais accompagnée de Ctrl, Alt ou Maj : raccourci de Windows, laissé passer. Seuls V et
            // le masque sont oubliés ; Win reste tenue, et un V suivant (Ctrl lâchée) doit pouvoir ouvrir la fenêtre.
            if (enfoncee && autreModificateurEnfonce)
            {
                _vAvalee = false;
                _masqueAEnvoyer = false;
                return ActionCrochet.Laisser;
            }

            if (enfoncee)
            {
                // La répétition automatique de la touche ne compte qu'une fois.
                var premiere = !_vAvalee;
                _vAvalee = true;
                _masqueAEnvoyer = true;
                _heureDerniereV = temps;
                return premiere ? ActionCrochet.AvalerEtOuvrir : ActionCrochet.Avaler;
            }

            // Seul le relâchement d'un V dont l'enfoncement a été avalé l'est aussi : si V était déjà tenue avant Win,
            // l'application a vu l'enfoncement et doit voir le relâchement, sinon elle croirait la touche coincée.
            if (!_vAvalee) return ActionCrochet.Laisser;

            _vAvalee = false;
            return ActionCrochet.Avaler;
        }

        // Win a été lâchée avant V : V, déjà avalée à l'enfoncement, l'est encore jusqu'à son relâchement.
        if (_vAvalee)
        {
            if (!enfoncee)
            {
                _vAvalee = false;
                return ActionCrochet.Avaler;
            }

            // Win a été lâchée avant V, qui est encore tenue : le clavier répète V, et ces répétitions suivent de près
            // le précédent V vu. On les avale aussi, sinon des « v » s'écriraient dans la fenêtre ouverte.
            if (EstRepetition(temps))
            {
                _heureDerniereV = temps;
                return ActionCrochet.Avaler;
            }

            // Trop tard pour une répétition : le hook n'a pas vu le relâchement de la précédente (fenêtre élevée, écran
            // de verrouillage). C'est une frappe neuve, qu'on ne vole pas : on oublie l'état périmé et on la laisse passer.
            RemettreAZero();
            return ActionCrochet.Laisser;
        }

        return ActionCrochet.Laisser;
    }

    // Soustraction non contrôlée : l'heure du système est un compteur 32 bits qui repasse par zéro environ tous les 49 jours.
    private bool EstRepetition(uint temps) => unchecked(temps - _heureDerniereV) < _ecartRepetitionMaxMs;

    private void RemettreAZero()
    {
        _winGaucheEnfoncee = false;
        _winDroiteEnfoncee = false;
        _vAvalee = false;
        _masqueAEnvoyer = false;
        _heureDerniereV = 0;
    }
}
