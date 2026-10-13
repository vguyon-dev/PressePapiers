using PressePapiers.Coeur.Formats;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>Lecture du presse-papiers à chaque copie.</summary>
public sealed class LecteurPressePapiers
{
    /// <summary>
    /// Durée maximale de la lecture native : au-delà, la copie est abandonnée. Elle protège de l'application propriétaire
    /// d'un format en rendu différé qui ne répond pas (la lecture reste alors bloquée sur son fil, que l'on laisse derrière).
    /// </summary>
    internal static readonly TimeSpan DelaiGlobal = TimeSpan.FromSeconds(2);

    private readonly IJournal _journal;

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
    /// Lit le contenu : 10 essais espacés de 25 ms ; null si la marque interne est présente, si le contenu est illisible ou vide.
    /// Les formats sont lus dans l'ordre de la spec, section 4. Les images sont converties en PNG avec leur miniature.
    /// </summary>
    /// <param name="application">Application d'origine relevée à la réception de la notification, ou null.</param>
    /// <returns>Le contenu à enregistrer, ou null s'il n'y a rien à garder (jamais d'exception).</returns>
    public async Task<ContenuCopie?> LireAsync(string? application)
    {
        try
        {
            var brut = await LireSurFilDedieAsync();
            return brut is null ? null : await ConvertirAsync(brut, application);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Lecture du presse-papiers impossible : la copie est ignorée.", exception);
            return null;
        }
    }

    // Un fil par lecture : OpenClipboard et GetClipboardData peuvent bloquer sans limite, donc jamais sur le fil d'interface
    // ni sur le pool. Si le délai passe, le fil reste derrière (fil d'arrière-plan) et finit quand le propriétaire répond.
    private async Task<ContenuBrut?> LireSurFilDedieAsync()
    {
        var resultat = new TaskCompletionSource<ContenuBrut?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fil = new Thread(() =>
        {
            try
            {
                resultat.TrySetResult(LectureBrute.Lire(_journal));
            }
            catch (Exception exception)
            {
                _journal.Erreur("Lecture native du presse-papiers en échec.", exception);
                resultat.TrySetResult(null);
            }
        })
        {
            IsBackground = true,
            Name = "Lecture du presse-papiers",
        };
        fil.Start();

        var fini = await Task.WhenAny(resultat.Task, Task.Delay(DelaiGlobal));
        if (fini != resultat.Task)
        {
            _journal.Avertissement(
                $"Copie ignorée : le presse-papiers n'a pas répondu en {DelaiGlobal.TotalSeconds:0} s (propriétaire en rendu différé ?).");
            return null;
        }

        return await resultat.Task;
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
}
