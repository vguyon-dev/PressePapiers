using System.Diagnostics;
using PressePapiers.Coeur.Formats;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>
/// Lecture du presse-papiers à chaque copie. Une lecture qui dépasse <see cref="DelaiGlobal"/> n'est pas perdue : le texte
/// déjà lu est rendu tout de suite, et le résultat complet est livré à la capture suivante quand le fil abandonné finit
/// (voir <see cref="RelectureDemandee"/>). Tant que ce fil court, il tient le presse-papiers ouvert : aucune autre
/// lecture ne part.
/// </summary>
public sealed class LecteurPressePapiers
{
    /// <summary>
    /// Durée d'attente de la lecture native. Au-delà, l'appelant reçoit le texte déjà lu (ou rien) et la lecture continue
    /// sur son fil : elle protège de l'application propriétaire d'un format en rendu différé qui tarde à répondre.
    /// </summary>
    internal static readonly TimeSpan DelaiGlobal = TimeSpan.FromSeconds(2);

    private readonly IJournal _journal;
    private readonly object _verrou = new();

    // Un fil abandonné au délai lit encore : il tient le presse-papiers ouvert, aucun autre fil ne part avant sa fin.
    private bool _enRetard;

    // Une copie signalée pendant le retard n'a pas été lue : une relecture suivra la fin de la lecture en retard.
    private bool _copieManquee;

    // Résultat de la lecture en retard, rendu par le prochain appel de LireAsync au lieu d'une nouvelle lecture.
    private ResultatTardif? _resultatTardif;

    /// <summary>Crée le lecteur.</summary>
    /// <param name="hwndRelais">
    /// Fenêtre relais de l'outil. La lecture ne s'y accroche pas (elle ouvre le presse-papiers avec une fenêtre nulle, sur son
    /// propre fil) ; le paramètre est conservé pour que l'écrivain et le lecteur se construisent de la même façon.
    /// </param>
    /// <param name="journal">Journal des copies ignorées.</param>
    public LecteurPressePapiers(nint hwndRelais, IJournal journal)
    {
        _ = hwndRelais;
        ArgumentNullException.ThrowIfNull(journal);
        _journal = journal;
    }

    /// <summary>
    /// Levé, sur un fil quelconque, quand le lecteur a quelque chose à livrer sans nouvelle notification : le résultat d'une
    /// lecture en retard, ou une copie signalée pendant ce retard. L'abonné relance une capture (<see cref="LireAsync"/>).
    /// </summary>
    public event Action? RelectureDemandee;

    /// <summary>
    /// Lit le contenu : 10 essais d'ouverture espacés de 25 ms ; null si la marque interne est présente, si le contenu est
    /// illisible ou vide, ou si une lecture en retard tient encore le presse-papiers. Les formats sont lus dans l'ordre de
    /// la spec, section 4. Les images sont converties en PNG avec leur miniature.
    /// </summary>
    /// <param name="application">Application d'origine relevée à la réception de la notification, ou null.</param>
    /// <returns>Le contenu à enregistrer, ou null s'il n'y a rien à garder (jamais d'exception).</returns>
    public async Task<ContenuCopie?> LireAsync(string? application)
    {
        try
        {
            var tardif = PrendreResultatTardif(out var lectureBloquee);
            if (tardif is not null)
            {
                return await ConvertirAsync(tardif.Brut, tardif.Application);
            }

            if (lectureBloquee)
            {
                return null;
            }

            var brut = await LireSurFilDedieAsync(application);
            return brut is null ? null : await ConvertirAsync(brut, application);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Lecture du presse-papiers impossible : la copie est ignorée.", exception);
            return null;
        }
    }

    // Rend le résultat en attente, s'il y en a un ; sinon dit si une lecture en retard interdit d'en lancer une autre.
    private ResultatTardif? PrendreResultatTardif(out bool lectureBloquee)
    {
        ResultatTardif? tardif;
        var relire = false;
        lock (_verrou)
        {
            tardif = _resultatTardif;
            _resultatTardif = null;
            lectureBloquee = tardif is null && _enRetard;
            if (lectureBloquee)
            {
                // Pas de nouveau fil ni de nouvel avertissement : la copie sera relue à la fin du retard.
                _copieManquee = true;
            }
            else if (tardif is not null && _copieManquee)
            {
                _copieManquee = false;
                relire = true;
            }
        }

        if (relire)
        {
            LeverRelecture();
        }

        return tardif;
    }

    // Un fil par lecture : OpenClipboard et GetClipboardData peuvent bloquer sans limite, donc jamais sur le fil d'interface
    // ni sur le pool. Si le délai passe, le fil continue (fil d'arrière-plan) et livre son résultat quand il finit.
    private async Task<ContenuBrut?> LireSurFilDedieAsync(string? application)
    {
        var lecture = new LectureEnCours(application);
        var fil = new Thread(() => ExecuterLecture(lecture))
        {
            IsBackground = true,
            Name = "Lecture du presse-papiers",
        };
        fil.Start();

        var fini = await Task.WhenAny(lecture.Resultat.Task, Task.Delay(DelaiGlobal));
        if (fini == lecture.Resultat.Task)
        {
            return await lecture.Resultat.Task;
        }

        ContenuBrut? partiel;
        lock (_verrou)
        {
            // Le fil a pu finir entre la fin du délai et la prise du verrou : son résultat est alors complet.
            if (lecture.Terminee)
            {
                return lecture.Final;
            }

            lecture.Abandonnee = true;
            _enRetard = true;
            partiel = lecture.Partiel;
            lecture.PartielRendu = partiel;
        }

        _journal.Avertissement(partiel is null
            ? $"Le presse-papiers n'a pas répondu en {DelaiGlobal.TotalSeconds:0} s (propriétaire en rendu différé ?) : "
              + "la copie sera enregistrée quand il répondra ; aucune autre lecture ne part d'ici là."
            : $"Le presse-papiers n'a pas répondu en {DelaiGlobal.TotalSeconds:0} s (propriétaire en rendu différé ?) : "
              + "le texte est enregistré sans attendre sa mise en forme ; aucune autre lecture ne part avant la fin de celle-ci.");
        return partiel;
    }

    // Corps du fil de lecture. Ne lève jamais : une exception ici tuerait le processus.
    private void ExecuterLecture(LectureEnCours lecture)
    {
        ContenuBrut? brut = null;
        try
        {
            brut = LectureBrute.Lire(_journal, partiel =>
            {
                lock (_verrou) lecture.Partiel = partiel;
            });
        }
        catch (Exception exception)
        {
            _journal.Erreur("Lecture native du presse-papiers en échec.", exception);
        }

        bool abandonnee;
        lock (_verrou)
        {
            lecture.Terminee = true;
            lecture.Final = brut;
            abandonnee = lecture.Abandonnee;
        }

        if (abandonnee)
        {
            TerminerRetard(lecture, brut);
        }

        lecture.Resultat.TrySetResult(brut);
    }

    // La lecture abandonnée a fini : son résultat attend la prochaine capture, s'il apporte plus que le partiel déjà rendu.
    private void TerminerRetard(LectureEnCours lecture, ContenuBrut? brut)
    {
        var apporte = brut is not null && (lecture.PartielRendu is null || brut.Type != lecture.PartielRendu.Type);
        bool lever;
        lock (_verrou)
        {
            _enRetard = false;
            if (apporte)
            {
                _resultatTardif = new ResultatTardif(brut!, lecture.Application);
            }

            // Sans résultat à livrer, la copie manquée est relue directement par la capture relancée.
            lever = apporte || _copieManquee;
            if (!apporte)
            {
                _copieManquee = false;
            }
        }

        var duree = lecture.Chrono.Elapsed.TotalSeconds;
        _journal.Info(apporte
            ? $"Lecture du presse-papiers terminée après {duree:0.#} s : son résultat est livré à la capture."
            : $"Lecture du presse-papiers terminée après {duree:0.#} s : rien de plus à enregistrer.");

        if (lever)
        {
            LeverRelecture();
        }
    }

    private void LeverRelecture()
    {
        try
        {
            RelectureDemandee?.Invoke();
        }
        catch (Exception exception)
        {
            // Appelé depuis le fil de lecture : une exception d'abonné ne doit pas tuer le processus.
            _journal.Erreur("Un abonné de RelectureDemandee a levé une exception.", exception);
        }
    }

    // L'image arrive en DIB : conversion en PNG et miniature après la fermeture du presse-papiers, hors du délai de lecture.
    private async Task<ContenuCopie?> ConvertirAsync(ContenuBrut brut, string? application)
    {
        switch (brut.Type)
        {
            case TypeElement.Image:
                try
                {
                    var png = await ConvertisseurImage.DibVersPngAsync(brut.Dib!);
                    var miniature = await ConvertisseurImage.MiniatureAsync(png);
                    return new ContenuCopie
                    {
                        Type = TypeElement.Image,
                        ImagePng = png,
                        Miniature = miniature,
                        Application = application,
                    };
                }
                catch (Exception exception)
                {
                    _journal.Avertissement("Copie ignorée : l'image n'a pas pu être convertie en PNG.", exception);
                    return null;
                }

            case TypeElement.Fichiers:
                return new ContenuCopie { Type = TypeElement.Fichiers, Fichiers = brut.Fichiers, Application = application };

            default:
                return new ContenuCopie
                {
                    Type = brut.Type,
                    Texte = brut.Texte,
                    Rtf = brut.Rtf,
                    Html = brut.Html,
                    Application = application,
                };
        }
    }

    /// <summary>Résultat d'une lecture en retard et application relevée pour la copie qu'elle concerne.</summary>
    private sealed record ResultatTardif(ContenuBrut Brut, string? Application);

    /// <summary>État partagé entre le fil de lecture et l'attente ; ses champs mutables ne se touchent que sous le verrou.</summary>
    private sealed class LectureEnCours(string? application)
    {
        public string? Application { get; } = application;
        public Stopwatch Chrono { get; } = Stopwatch.StartNew();
        public TaskCompletionSource<ContenuBrut?> Resultat { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ContenuBrut? Partiel { get; set; }
        public ContenuBrut? PartielRendu { get; set; }
        public ContenuBrut? Final { get; set; }
        public bool Terminee { get; set; }
        public bool Abandonnee { get; set; }
    }
}
