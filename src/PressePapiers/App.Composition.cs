using PressePapiers.Capture;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Interface;
using PressePapiers.Systeme;

namespace PressePapiers;

// Composition des briques : fenêtre et modèle de vue, relais (copies, raccourci de secours, icône), hook Win+V,
// capture, état du hook affiché sur l'icône et dans les Paramètres, démarrage avec Windows.
public sealed partial class App
{
    private const string TitreNotification = "Presse-papiers";
    private const int ErreurRaccourciDejaPris = 1409;

    // Le hook se réinstalle seul toutes les 15 minutes sur son propre fil (CrochetWinV) ; cette minuterie ne fait que
    // relire son état pour l'icône et les Paramètres, et relance l'installation d'un hook inactif.
    private static readonly TimeSpan _periodeEtatCrochet = TimeSpan.FromMinutes(1);

    private (bool Actif, string? Raison)? _etatAffiche;

    // La fenêtre est créée sans être affichée : ni Show(false) préalable ni Activate, l'essai du point 2 de la spec
    // (section 16) a montré que AppWindow.Show puis PremierPlan.Amener suffisent à la première ouverture. Le relais est
    // créé avant elle : le collage écrit dans le presse-papiers au nom de la fenêtre du relais.
    private void CreerFenetrePrincipale(BaseHistorique baseHistorique, DepotHistorique depot, DossierDonnees dossiers)
    {
        var parametres = new ServiceParametres(dossiers.Parametres, _journal);
        _relais = new RelaisWindows(_journal);
        var recollage = new ServiceRecollage(new EcrivainPressePapiers(_relais.Handle, _journal), depot, _journal);
        _vue = new FenetrePrincipaleVue(depot, new MoteurRecherche(baseHistorique), recollage, parametres, _journal);
        _fenetre = new FenetrePrincipale(_vue);

        _vue.Notifier = (titre, message) => _icone?.Notifier(titre, message);
        _vue.AppliquerRaccourci = EnregistrerRaccourci;
        _vue.ParametresModifies += () => SurParametresModifies(depot);
        _fenetre.PremierPlanRefuse += () => _icone?.Notifier(
            TitreNotification, "Windows a refusé d'afficher la fenêtre au premier plan : clique sur l'icône pour l'ouvrir.");
        AppliquerPlafond(depot);
    }

    // Relais : écoute du presse-papiers et capture, raccourci de secours, icône de notification et son menu.
    private void DemarrerRelais(DepotHistorique depot)
    {
        var relais = _relais!;
        var vue = _vue!;

        _capture = new ServiceCapture(relais, new LecteurPressePapiers(relais.Handle, _journal), depot, () => vue.Reglages, _journal);
        if (!relais.EcouterPressePapiers())
        {
            _journal.Erreur("Les copies ne seront pas enregistrées : écoute du presse-papiers impossible.");
        }

        // Levé pendant le traitement de WM_HOTKEY : Windows accorde alors le premier plan au processus, la fenêtre
        // active est relevée avant que quoi que ce soit ne la change.
        relais.RaccourciSecoursPresse += () => OuvrirDepuisRaccourci(PremierPlan.FenetreActive(), OrigineOuverture.RaccourciSecours);
        relais.SessionDeverrouillee += () => ReinstallerCrochet("déverrouillage de la session");
        relais.SortieDeVeille += () => ReinstallerCrochet("sortie de veille");

        _icone = new IconeNotification(relais, _journal);
        _icone.OuvrirDemande += () => AfficherFenetre(OrigineOuverture.Icone);
        _icone.ParametresDemandes += () => _fenetre?.AfficherParametres();

        // Hors de la procédure du relais : Quitter détruit la fenêtre du relais, qui ne doit plus être dans sa pile d'appels.
        _icone.QuitterDemande += () => PosterSurInterface(Quitter, "l'arrêt demandé par l'icône");

        EnregistrerRaccourciAuDemarrage(vue.Reglages.RaccourciSecours);
    }

    // Le hook lève WinVPresse sur son propre fil : l'ouverture est ramenée sur le fil d'interface.
    private void DemarrerCrochet()
    {
        var crochet = new CrochetWinV(_journal);
        _crochet = crochet;
        crochet.WinVPresse += fenetre => PosterSurInterface(
            () => OuvrirDepuisRaccourci(fenetre, OrigineOuverture.WinV), "l'ouverture par Win+V");
        crochet.Demarrer();
        ActualiserEtatCrochet();

        _minuterieCrochet = _fil.CreateTimer();
        _minuterieCrochet.Interval = _periodeEtatCrochet;
        _minuterieCrochet.IsRepeating = true;
        _minuterieCrochet.Tick += (_, _) => ExecuterProtege(SurveillerCrochet, "la surveillance du hook");
        _minuterieCrochet.Start();
    }

    // Win+V ou raccourci de secours : ignoré si la fenêtre est déjà affichée, ou moins de 300 ms après une ouverture.
    private void OuvrirDepuisRaccourci(nint fenetreOrigine, OrigineOuverture origine)
    {
        if (ArretEnCours || _fenetre is null || _fenetre.EstVisible || !_antiRebond.Accepter())
        {
            return;
        }

        _fenetre.AfficherPour(fenetreOrigine, origine);
    }

    private void ReinstallerCrochet(string raison)
    {
        _journal.Info($"Réinstallation du hook Win+V demandée ({raison}).");
        _crochet?.Reinstaller();
    }

    private void SurveillerCrochet()
    {
        if (_crochet is { EstActif: false } crochet)
        {
            crochet.Reinstaller();
        }

        ActualiserEtatCrochet();
    }

    // Aspect d'alerte de l'icône et texte des Paramètres, seulement quand l'état change.
    private void ActualiserEtatCrochet()
    {
        if (_crochet is null || _icone is null || _vue is null)
        {
            return;
        }

        var etat = (_crochet.EstActif, _crochet.RaisonInactif);
        if (_etatAffiche == etat)
        {
            return;
        }

        _etatAffiche = etat;
        var raccourci = _vue.Reglages.RaccourciSecours;
        if (etat.EstActif)
        {
            _vue.EtatCrochet = "Actif : Win+V ouvre PressePapiers.";
            _icone.Afficher(alerte: false, TitreNotification);
        }
        else
        {
            _vue.EtatCrochet = $"Inactif : {etat.RaisonInactif} Le raccourci de secours {raccourci} reste disponible.";
            _icone.Afficher(alerte: true, $"{TitreNotification} : Win+V inactif, utiliser {raccourci}");
            _journal.Avertissement($"Hook Win+V inactif : {etat.RaisonInactif}");
        }
    }

    // Raccourci des réglages ; un texte illisible (fichier modifié à la main) laisse la place au défaut.
    private void EnregistrerRaccourciAuDemarrage(string texte)
    {
        if (!Raccourci.TryAnalyser(texte, out var raccourci))
        {
            _journal.Avertissement($"Raccourci de secours « {texte} » illisible : le défaut est utilisé.");
            Raccourci.TryAnalyser(new Parametres().RaccourciSecours, out raccourci);
        }

        if (EnregistrerRaccourci(raccourci) is { } refus)
        {
            _icone?.Notifier(TitreNotification, refus);
        }
    }

    // Branché sur les Paramètres : null si Windows accepte la combinaison, sinon le message à montrer.
    private string? EnregistrerRaccourci(Raccourci raccourci)
    {
        if (_relais is null)
        {
            return "Le raccourci ne peut pas être enregistré pendant l'arrêt.";
        }

        if (_relais.EnregistrerRaccourci(raccourci, out var code))
        {
            _journal.Info($"Raccourci de secours {raccourci} enregistré.");
            return null;
        }

        return code == ErreurRaccourciDejaPris
            ? $"Le raccourci {raccourci} est déjà utilisé par une autre application : choisis une autre combinaison dans les Paramètres."
            : $"Windows a refusé le raccourci {raccourci} (code {code}).";
    }

    // Le thème, le mode de collage et la chasse fixe sont appliqués par la fenêtre et la vue, le raccourci par
    // EnregistrerRaccourci, le démarrage avec Windows par la vue elle-même. Restent le plafond et le texte d'état du hook,
    // qui cite le raccourci de secours.
    private void SurParametresModifies(DepotHistorique depot)
    {
        AppliquerPlafond(depot);
        _etatAffiche = null;
        ActualiserEtatCrochet();
    }

    // Un plafond abaissé dans les Paramètres s'applique tout de suite, sans attendre la prochaine copie.
    private void AppliquerPlafond(DepotHistorique depot)
    {
        if (_vue is null)
        {
            return;
        }

        try
        {
            var supprimes = depot.AppliquerPlafond(_vue.Reglages.PlafondTemporaire);
            if (supprimes > 0)
            {
                _journal.Info($"Plafond de Temporaire : {supprimes} élément(s) ancien(s) supprimé(s).");
            }
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Plafond de Temporaire non appliqué.", exception);
        }
    }

    // Aligne la clé Run sur les réglages, avec le chemin de l'exécutable en cours (il change après une publication).
    // Un refus du registre est noté sans empêcher le démarrage.
    private void AppliquerDemarrageWindows()
    {
        var activer = _vue?.Reglages.DemarrageAvecWindows ?? false;
        var chemin = Environment.ProcessPath;
        if (string.IsNullOrEmpty(chemin))
        {
            _journal.Avertissement("Chemin de l'exécutable inconnu : démarrage avec Windows non modifié.");
            return;
        }

        try
        {
            DemarrageWindows.Appliquer(activer, chemin);
            _journal.Info($"Démarrage avec Windows : {(activer ? "activé" : "désactivé")}.");
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Démarrage avec Windows non modifié.", exception);
        }
    }
}
