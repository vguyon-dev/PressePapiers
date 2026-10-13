using System.Globalization;
using System.Text;
using PressePapiers.Coeur.Journalisation;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PressePapiers.Systeme;

/// <summary>
/// Journal fichier par Serilog (spec, section 13) : un fichier par jour, <c>journal-AAAAMMJJ.log</c>, les 14 plus récents
/// gardés. Les messages sont écrits tels quels (jamais lus comme un modèle Serilog). Après <see cref="Dispose"/>, les
/// appels sont ignorés : un message tardif pendant l'arrêt ne doit rien casser.
/// </summary>
public sealed class JournalSerilog : IJournal, IDisposable
{
    // Une ligne par événement : heure locale avec décalage, niveau sur trois lettres, message, puis l'exception s'il y en a une.
    private const string Modele = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    // Nombre de fichiers de jour gardés.
    private const int FichiersGardes = 14;

    private readonly Logger _logger;
    private int _libere;

    /// <summary>Crée le journal dans un dossier, créé au besoin.</summary>
    /// <param name="dossierJournal">Dossier des fichiers du journal.</param>
    public JournalSerilog(string dossierJournal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dossierJournal);
        Directory.CreateDirectory(dossierJournal);

        // « shared » : une seconde instance, qui note sa redirection avant de s'arrêter, écrit dans le même fichier.
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(dossierJournal, "journal-.log"),
                outputTemplate: Modele,
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: FichiersGardes,
                shared: true,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            .CreateLogger();
    }

    /// <inheritdoc />
    public void Info(string message) => Ecrire(LogEventLevel.Information, message, null);

    /// <inheritdoc />
    public void Avertissement(string message, Exception? exception = null) => Ecrire(LogEventLevel.Warning, message, exception);

    /// <inheritdoc />
    public void Erreur(string message, Exception? exception = null) => Ecrire(LogEventLevel.Error, message, exception);

    /// <summary>Vide les tampons et ferme le fichier ; sans effet la seconde fois.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _libere, 1) == 0)
        {
            _logger.Dispose();
        }
    }

    private void Ecrire(LogEventLevel niveau, string message, Exception? exception)
    {
        if (Volatile.Read(ref _libere) != 0)
        {
            return;
        }

        // Le texte passe en argument et non dans le modèle : des accolades dans un message restent des accolades.
        _logger.Write(niveau, exception, "{Texte:l}", message);
    }
}
