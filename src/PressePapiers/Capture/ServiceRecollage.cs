using System.Diagnostics;
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Natif;
using PressePapiers.Systeme;

namespace PressePapiers.Capture;

/// <summary>
/// Remet un élément dans le presse-papiers et le colle dans la fenêtre d'origine (spec, section 10.2).
/// À appeler depuis le fil d'interface : les continuations reviennent sur lui, là où la fenêtre se masque.
/// </summary>
public sealed class ServiceRecollage
{
    private static readonly TimeSpan _pasAttente = TimeSpan.FromMilliseconds(10);

    private readonly EcrivainPressePapiers _ecrivain;
    private readonly DepotHistorique _depot;
    private readonly IJournal _journal;

    /// <summary>Crée le service de recollage.</summary>
    public ServiceRecollage(EcrivainPressePapiers ecrivain, DepotHistorique depot, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(ecrivain);
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(journal);
        _ecrivain = ecrivain;
        _depot = depot;
        _journal = journal;
    }

    /// <summary>Durée maximale d'attente du relâchement des modificateurs (une seconde dans la spec).</summary>
    internal TimeSpan AttenteMaximaleModificateurs { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Dit si un modificateur est encore enfoncé. Remplaçable pour l'essai avec de fausses frappes.</summary>
    internal Func<bool> ModificateursEnfonces { get; set; } = ModificateursReelsEnfonces;

    /// <summary>
    /// Spec, section 10.2 : écriture, contrôle de la fenêtre et de son niveau, premier plan, attente des modificateurs, Ctrl+V.
    /// </summary>
    /// <remarks>
    /// L'élément est toujours écrit en premier et remonte en tête de son onglet dès que l'écriture réussit, collé ou non.
    /// <paramref name="masquerFenetre"/> n'est appelée que lorsque la fenêtre doit disparaître : après un collage, et
    /// quand la cible est plus élevée (notification « Copié, colle avec Ctrl+V »). Pour les autres copies seules, la
    /// fenêtre reste ouverte et l'appelant affiche son message. <see cref="IssueCollage.Echec"/> : écriture
    /// impossible, premier plan refusé, ou frappe non envoyée ; la fenêtre n'est alors pas masquée.
    /// </remarks>
    public async Task<IssueCollage> CollerAsync(Element element, ModeCollage mode, nint fenetreOrigine, bool copieSeule,
                                                nint hwndOutil, Action masquerFenetre)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(masquerFenetre);

        try
        {
            var chemin = element.Type == TypeElement.Image ? _depot.CheminImage(element) : null;
            if (!await _ecrivain.EcrireAsync(element, mode, chemin))
            {
                _journal.Erreur($"Écriture de l'élément {element.Id} dans le presse-papiers impossible.");
                return IssueCollage.Echec;
            }

            _depot.MarquerUtilise(element.Id);

            var origineConnue = fenetreOrigine != 0 && PremierPlan.EstOrigineValide(fenetreOrigine, hwndOutil);
            var existe = origineConnue && NatifSaisie.IsWindow(fenetreOrigine);

            // Le niveau n'est lu que s'il peut changer la décision.
            int? niveauCible = existe && !copieSeule ? NiveauIntegrite.DeFenetre(fenetreOrigine) : null;
            var issue = DecisionCollage.Decider(copieSeule, origineConnue, existe, niveauCible, NiveauIntegrite.DuProcessusCourant());

            switch (issue)
            {
                case IssueCollage.Colle:
                    return await ColleAsync(fenetreOrigine, masquerFenetre);

                case IssueCollage.CopieFenetreElevee:
                    // Pas de Ctrl+V (Windows le bloquerait sans le dire) : on rend la main à la fenêtre d'origine,
                    // au mieux, pour que l'utilisateur colle lui-même.
                    PremierPlan.Amener(fenetreOrigine);
                    masquerFenetre();
                    return issue;

                default:
                    return issue;
            }
        }
        catch (Exception exception)
        {
            _journal.Erreur($"Recollage de l'élément {element.Id} en échec.", exception);
            return IssueCollage.Echec;
        }
    }

    private async Task<IssueCollage> ColleAsync(nint fenetreOrigine, Action masquerFenetre)
    {
        // Le premier plan se rend tant que la fenêtre de l'outil est encore active, puis elle se masque.
        if (!PremierPlan.Amener(fenetreOrigine))
        {
            _journal.Avertissement("Le retour au premier plan de la fenêtre d'origine a été refusé par Windows.");
            return IssueCollage.Echec;
        }

        masquerFenetre();
        await AttendreModificateursRelachesAsync();

        // Coller dans une autre fenêtre que celle prévue serait pire que ne pas coller.
        if (PremierPlan.FenetreActive() != fenetreOrigine)
        {
            _journal.Avertissement("Le premier plan a changé avant l'envoi de Ctrl+V : collage abandonné, l'élément reste copié.");
            return IssueCollage.Echec;
        }

        if (!EnvoyerCtrlV())
        {
            _journal.Erreur($"SendInput n'a pas inséré Ctrl+V (erreur {Marshal.GetLastWin32Error()}).");
            return IssueCollage.Echec;
        }

        return IssueCollage.Colle;
    }

    // Win, Maj, Ctrl ou Alt encore tenus (Entrée avec Maj, ou Win+V pas encore relâchée) fausseraient le raccourci :
    // on attend leur relâchement, une seconde au plus, puis on colle quand même.
    private async Task AttendreModificateursRelachesAsync()
    {
        var chrono = Stopwatch.StartNew();
        while (ModificateursEnfonces())
        {
            if (chrono.Elapsed >= AttenteMaximaleModificateurs)
            {
                _journal.Avertissement("Des modificateurs sont restés enfoncés plus d'une seconde ; Ctrl+V est envoyé quand même.");
                return;
            }

            await Task.Delay(_pasAttente);
        }
    }

    private static bool ModificateursReelsEnfonces()
    {
        foreach (var touche in NatifSaisie.ModificateursASurveiller)
            if ((NatifSaisie.GetAsyncKeyState(touche) & 0x8000) != 0) return true;

        return false;
    }

    // Ctrl enfoncée, V enfoncée et relâchée, Ctrl relâchée : une seule frappe atomique pour Windows.
    private static bool EnvoyerCtrlV()
    {
        var entrees = new NatifSaisie.Entree[4];
        Remplir(ref entrees[0], NatifSaisie.VK_CONTROL, relache: false);
        Remplir(ref entrees[1], NatifSaisie.VK_V, relache: false);
        Remplir(ref entrees[2], NatifSaisie.VK_V, relache: true);
        Remplir(ref entrees[3], NatifSaisie.VK_CONTROL, relache: true);

        return NatifSaisie.SendInput((uint)entrees.Length, entrees, Marshal.SizeOf<NatifSaisie.Entree>()) == entrees.Length;
    }

    private static void Remplir(ref NatifSaisie.Entree entree, ushort touche, bool relache)
    {
        entree.Type = NatifSaisie.INPUT_KEYBOARD;
        entree.Clavier.Touche = touche;
        entree.Clavier.Drapeaux = relache ? NatifSaisie.KEYEVENTF_KEYUP : 0;
    }
}
