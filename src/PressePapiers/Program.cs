using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Natif;
using PressePapiers.Systeme;

namespace PressePapiers;

/// <summary>
/// Point d'entrée personnalisé (symbole DISABLE_XAML_GENERATED_MAIN) : instance unique avant toute création de fenêtre,
/// selon la documentation du Windows App SDK sur l'instanciation d'une application (spec, section 12).
/// </summary>
public static class Program
{
    // Clé d'instance : deux lancements qui la partagent sont la même application.
    private const string CleInstance = "PressePapiers";

    // Au-delà, l'instance principale est jugée muette.
    private const uint DelaiRedirectionMs = 15_000;

    /// <summary>Démarre l'application WinUI 3 sur un fil à appartement unique, ou redirige vers l'instance déjà lancée.</summary>
    [STAThread]
    public static void Main()
    {
        // Comme le Main généré par WinUI : contrôle que le processus remplit les conditions de XAML (manifeste, version
        // de Windows) avant tout autre appel.
        NatifDemarrage.XamlCheckProcessRequirements();
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Avant toute fenêtre : la décision se prend tant que le processus n'a rien créé.
        var principale = AppInstance.FindOrRegisterForKey(CleInstance);
        if (!principale.IsCurrent)
        {
            if (RedirigerVers(principale))
            {
                return;
            }

            // L'instance principale a pu s'arrêter pendant la redirection : cette instance prend alors sa place.
            principale = AppInstance.FindOrRegisterForKey(CleInstance);
            if (!principale.IsCurrent)
            {
                Noter(journal => journal.Avertissement(
                    $"Seconde instance (processus {Environment.ProcessId}) : l'instance principale ne répond pas, arrêt."));
                NatifDemarrage.AfficherMessage(0,
                    "PressePapiers est déjà lancé mais ne répond pas. Ferme-le depuis le Gestionnaire des tâches, puis relance-le.",
                    "PressePapiers", NatifDemarrage.MB_OK | NatifDemarrage.MB_ICONWARNING | NatifDemarrage.MB_SETFOREGROUND);
                return;
            }

            Noter(journal => journal.Info(
                $"Seconde instance (processus {Environment.ProcessId}) : l'instance principale a disparu, celle-ci démarre à sa place."));
        }

        // L'événement arrive sur un fil quelconque : App le ramène sur le fil d'interface.
        principale.Activated += (_, _) => App.SignalerActivation();

        Application.Start(parametres =>
        {
            // Sans contexte de synchronisation, les await de l'interface reprendraient sur un fil du pool.
            var contexte = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(contexte);
            _ = new App();
        });
    }

    // Transmet l'activation à l'instance principale ; renvoie vrai si elle l'a reçue. L'appel est lancé sur une tâche du
    // pool et ce fil l'attend par CoWaitForMultipleObjects : ce fil STA doit continuer à pomper ses messages COM pendant
    // l'attente (un Wait() bloquant pourrait empêcher l'appel inter-processus d'aboutir).
    private static bool RedirigerVers(AppInstance principale)
    {
        // L'instance principale va vouloir afficher sa fenêtre : c'est ce processus, au premier plan, qui peut le lui permettre.
        NatifDemarrage.AllowSetForegroundWindow(principale.ProcessId);

        var arguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var termine = NatifDemarrage.CreerEvenement(0, manuel: true, signale: false, 0);
        if (termine == 0)
        {
            Noter(journal => journal.Avertissement("Seconde instance : événement de redirection impossible à créer."));
            return false;
        }

        Exception? echec = null;
        _ = Task.Run(async () =>
        {
            try
            {
                await principale.RedirectActivationToAsync(arguments);
            }
            catch (Exception exception)
            {
                echec = exception;
            }
            finally
            {
                NatifDemarrage.SetEvent(termine);
            }
        });

        var resultat = NatifDemarrage.AttendreSansBloquer(termine, DelaiRedirectionMs);

        // Le handle reste ouvert en cas de délai dépassé : la tâche peut encore le signaler.
        if (resultat != 0)
        {
            echec = new TimeoutException("L'instance principale n'a pas répondu.");
        }
        else
        {
            NatifDemarrage.CloseHandle(termine);
        }

        if (echec is null)
        {
            Noter(journal => journal.Info(
                $"Seconde instance (processus {Environment.ProcessId}) : activation redirigée vers l'instance principale, arrêt."));
            return true;
        }

        Noter(journal => journal.Avertissement($"Seconde instance (processus {Environment.ProcessId}) : redirection impossible.", echec));
        return false;
    }

    // Une trace au journal de l'instance principale (fichier partagé). Jamais d'exception : rien ne doit empêcher la suite.
    private static void Noter(Action<JournalSerilog> ecrire)
    {
        try
        {
            using var journal = new JournalSerilog(DossierDonnees.ParDefaut().Journal);
            ecrire(journal);
        }
        catch (Exception)
        {
            // Pas de journal possible (dossier inaccessible) : la seconde instance continue sans rien dire.
        }
    }
}
