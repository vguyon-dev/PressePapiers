using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Icône de la zone de notification (WinUI 3 n'en fournit pas) et son menu natif. Les messages de l'icône arrivent par
/// le relais ; le menu est un menu Win32 (Ouvrir, Paramètres, Quitter). L'icône est recréée quand l'Explorateur
/// redémarre. À créer et libérer sur le fil d'interface, comme le relais.
/// </summary>
public sealed class IconeNotification : IDisposable
{
    // Identifiant de l'icône auprès du Shell : une seule icône par processus.
    internal const uint IdIcone = 1;

    // Commandes du menu contextuel.
    private const uint CommandeOuvrir = 1;
    private const uint CommandeParametres = 2;
    private const uint CommandeQuitter = 3;

    private const string FichierNormal = "icone-normale.ico";
    private const string FichierAlerte = "icone-alerte.ico";

    private readonly RelaisWindows _relais;
    private readonly IJournal _journal;
    private readonly nint _hwnd;

    private nint _iconeNormale;
    private nint _iconeAlerte;
    private bool _iconeNormalePossedee;
    private bool _iconeAlertePossedee;

    // Dernier état demandé : il sert à recréer l'icône à l'identique.
    private bool _demandee;
    private bool _alerte;
    private string _infobulle = "Presse-papiers";

    private bool _ajoutee;
    private bool _versionQuatre;
    private bool _menuOuvert;
    private bool _libere;

    /// <summary>Crée l'icône, qui reçoit ses messages par le relais ; rien n'apparaît avant <see cref="Afficher"/>.</summary>
    /// <param name="relais">Relais dont la fenêtre reçoit les messages de l'icône.</param>
    /// <param name="journal">Journal des échecs du Shell.</param>
    public IconeNotification(RelaisWindows relais, IJournal journal)
    {
        _relais = relais;
        _journal = journal;
        _hwnd = relais.Handle;
        _relais.MessageIconeRecu += SurMessageIcone;
        _relais.BarreDesTachesRecreee += SurBarreDesTachesRecreee;
    }

    /// <summary>Clic sur l'icône ou commande « Ouvrir ».</summary>
    public event Action? OuvrirDemande;

    /// <summary>Commande « Paramètres ».</summary>
    public event Action? ParametresDemandes;

    /// <summary>Commande « Quitter ».</summary>
    public event Action? QuitterDemande;

    /// <summary>
    /// Affiche l'icône (NIM_ADD la première fois, NIM_MODIFY ensuite), en aspect d'alerte si demandé, avec cette infobulle.
    /// </summary>
    /// <param name="alerte">Vrai pour l'aspect d'alerte (le hook Win+V n'est pas actif).</param>
    /// <param name="infobulle">Texte de l'infobulle, tronqué à 127 caractères.</param>
    public void Afficher(bool alerte, string infobulle)
    {
        ObjectDisposedException.ThrowIf(_libere, this);
        _demandee = true;
        _alerte = alerte;
        _infobulle = infobulle;
        Appliquer();
    }

    /// <summary>Affiche une bulle de notification (NIF_INFO) ; fait apparaître l'icône si elle ne l'était pas encore.</summary>
    /// <param name="titre">Titre de la bulle, tronqué à 63 caractères.</param>
    /// <param name="message">Message de la bulle, tronqué à 255 caractères.</param>
    public void Notifier(string titre, string message)
    {
        ObjectDisposedException.ThrowIf(_libere, this);
        if (!_ajoutee)
        {
            Appliquer();
        }

        var donnees = Creer(NatifIcone.NifInfo);
        NatifIcone.DefinirTitreBulle(ref donnees, titre);
        NatifIcone.DefinirBulle(ref donnees, message);
        donnees.DrapeauxBulle = NatifIcone.NiifInfo;
        if (!NatifIcone.NotifierIcone(NatifIcone.NimModify, ref donnees))
        {
            _journal.Avertissement("Bulle de notification refusée par le Shell.");
        }
    }

    /// <summary>Retire l'icône de la zone de notification (NIM_DELETE) et libère les icônes chargées.</summary>
    public void Dispose()
    {
        if (_libere)
        {
            return;
        }

        _libere = true;
        _relais.MessageIconeRecu -= SurMessageIcone;
        _relais.BarreDesTachesRecreee -= SurBarreDesTachesRecreee;

        if (_ajoutee)
        {
            var donnees = Creer(0);
            NatifIcone.NotifierIcone(NatifIcone.NimDelete, ref donnees);
            _ajoutee = false;
        }

        if (_iconeNormalePossedee)
        {
            NatifIcone.DetruireIcone(_iconeNormale);
        }

        if (_iconeAlertePossedee)
        {
            NatifIcone.DetruireIcone(_iconeAlerte);
        }
    }

    // Ajoute l'icône ou met à jour celle qui existe ; si le Shell ne la connaît plus, elle est ajoutée de nouveau.
    private void Appliquer()
    {
        var donnees = Creer(NatifIcone.NifMessage | NatifIcone.NifIcon | NatifIcone.NifTip | NatifIcone.NifShowTip);
        donnees.MessageRappel = RelaisWindows.MessageIcone;
        donnees.Icone = IconePour(_alerte);
        NatifIcone.DefinirInfobulle(ref donnees, _infobulle);

        if (_ajoutee)
        {
            if (NatifIcone.NotifierIcone(NatifIcone.NimModify, ref donnees))
            {
                return;
            }

            _journal.Avertissement("Mise à jour de l'icône refusée : elle est ajoutée de nouveau.");
            _ajoutee = false;
        }

        if (!NatifIcone.NotifierIcone(NatifIcone.NimAdd, ref donnees))
        {
            _journal.Avertissement("Ajout de l'icône de notification refusé par le Shell.");
            return;
        }

        _ajoutee = true;

        // Version 4 : messages précis (NIN_SELECT, WM_CONTEXTMENU) au lieu des seuls messages de souris.
        donnees.DelaiOuVersion = NatifIcone.VersionIcone4;
        _versionQuatre = NatifIcone.NotifierIcone(NatifIcone.NimSetVersion, ref donnees);
        if (!_versionQuatre)
        {
            _journal.Avertissement("NOTIFYICON_VERSION_4 refusée : l'icône réagit aux messages de souris classiques.");
        }
    }

    private NatifIcone.DonneesIcone Creer(uint drapeaux) => new()
    {
        Taille = (uint)Unsafe.SizeOf<NatifIcone.DonneesIcone>(),
        Hwnd = _hwnd,
        Id = IdIcone,
        Drapeaux = drapeaux,
    };

    // L'Explorateur a redémarré : ses icônes ont disparu. Le retrait préalable évite un doublon si l'avis arrive
    // alors que l'ancienne icône existe encore.
    private void SurBarreDesTachesRecreee()
    {
        if (_libere || !_demandee)
        {
            return;
        }

        var donnees = Creer(0);
        NatifIcone.NotifierIcone(NatifIcone.NimDelete, ref donnees);
        _ajoutee = false;
        Appliquer();
        _journal.Info("Icône de notification recréée après le redémarrage de l'Explorateur.");
    }

    private void SurMessageIcone(uint evenement)
    {
        if (_libere)
        {
            return;
        }

        // En version 4 : NIN_SELECT et WM_CONTEXTMENU ; en repli (version 0) : relâchement du bouton gauche ou droit.
        var selection = _versionQuatre
            ? evenement is NatifIcone.NinSelect or NatifIcone.NinKeySelect
            : evenement == NatifIcone.WmLButtonUp;
        var menu = _versionQuatre
            ? evenement == NatifIcone.WmContextMenu
            : evenement == NatifIcone.WmRButtonUp;

        if (selection)
        {
            OuvrirDemande?.Invoke();
        }
        else if (menu)
        {
            AfficherMenu();
        }
    }

    // Menu natif : SetForegroundWindow avant, pour qu'il se ferme au clic ailleurs ; WM_NULL après, pour que la
    // boucle de messages ne reste pas endormie (recette décrite par Microsoft pour TrackPopupMenu). Chaque commande
    // porte un accélérateur (O, P, Q) pour le clavier ; « Ouvrir » est la commande par défaut, en gras.
    private void AfficherMenu()
    {
        if (_menuOuvert)
        {
            return;
        }

        var menu = NatifIcone.CreerMenu();
        if (menu == 0)
        {
            _journal.Avertissement($"Création du menu de l'icône impossible (code {Marshal.GetLastPInvokeError()}).");
            return;
        }

        var commande = 0;
        _menuOuvert = true;
        try
        {
            NatifIcone.AjouterAuMenu(menu, NatifIcone.MfString, CommandeOuvrir, "&Ouvrir");
            NatifIcone.AjouterAuMenu(menu, NatifIcone.MfString, CommandeParametres, "&Paramètres");
            NatifIcone.AjouterAuMenu(menu, NatifIcone.MfSeparator, 0, null);
            NatifIcone.AjouterAuMenu(menu, NatifIcone.MfString, CommandeQuitter, "&Quitter");
            NatifIcone.DefinirCommandeParDefaut(menu, CommandeOuvrir, 0);

            NatifIcone.LirePositionCurseur(out var position);
            NatifIcone.AmenerAuPremierPlan(_hwnd);
            commande = NatifIcone.AfficherMenu(
                menu, NatifIcone.TpmRightButton | NatifIcone.TpmBottomAlign | NatifIcone.TpmReturnCmd,
                position.X, position.Y, _hwnd, 0);
            NatifIcone.PosterMessage(_hwnd, NatifIcone.WmNull, 0, 0);
        }
        finally
        {
            _menuOuvert = false;
            NatifIcone.DetruireMenu(menu);
        }

        // Les événements sont levés une fois le menu refermé, hors de sa boucle de messages.
        switch ((uint)commande)
        {
            case CommandeOuvrir:
                OuvrirDemande?.Invoke();
                break;
            case CommandeParametres:
                ParametresDemandes?.Invoke();
                break;
            case CommandeQuitter:
                QuitterDemande?.Invoke();
                break;
        }
    }

    private nint IconePour(bool alerte)
    {
        if (alerte)
        {
            if (_iconeAlerte == 0)
            {
                (_iconeAlerte, _iconeAlertePossedee) = Charger(FichierAlerte);
            }

            return _iconeAlerte;
        }

        if (_iconeNormale == 0)
        {
            (_iconeNormale, _iconeNormalePossedee) = Charger(FichierNormal);
        }

        return _iconeNormale;
    }

    // Charge la taille d'icône que la zone de notification affiche à la résolution du système ; à défaut de fichier,
    // l'icône d'application de Windows (partagée, donc non détruite) évite une icône invisible.
    private (nint Icone, bool Possedee) Charger(string fichier)
    {
        var chemin = Path.Combine(AppContext.BaseDirectory, "Assets", fichier);
        var taille = NatifIcone.LireMetrique(NatifIcone.SmCxSmIcon, NatifIcone.LireDpiSysteme());
        var icone = File.Exists(chemin)
            ? NatifIcone.ChargerImage(0, chemin, NatifIcone.ImageIcon, taille, taille, NatifIcone.LrLoadFromFile)
            : 0;
        if (icone != 0)
        {
            return (icone, true);
        }

        _journal.Avertissement($"Icône {chemin} introuvable ou illisible : icône générique utilisée.");
        return (NatifIcone.ChargerIconeSysteme(0, NatifIcone.IdiApplication), false);
    }
}
