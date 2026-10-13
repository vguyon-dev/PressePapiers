using PressePapiers.Coeur.Recherche;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Recherche;

/// <summary>
/// Le dépôt et le moteur de recherche partagent la même connexion SQLite, qui n'est pas sûre entre fils : ils doivent
/// prendre le même verrou. Dans l'application, la capture écrit pendant que la fenêtre cherche.
/// </summary>
public sealed class RechercheConcurrenteTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Une_recherche_pendant_des_ajouts_sur_un_autre_fil_ne_leve_jamais()
    {
        var moteur = new MoteurRecherche(_banc.Base);
        var erreurs = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var ecritureFinie = false;

        // Comme la capture : un ajout puis le plafond de Temporaire, qui supprime dans une transaction dès qu'il est
        // dépassé. Sans verrou commun, une recherche lancée pendant cette transaction sur la même connexion échoue.
        var ecrivain = new Thread(() =>
        {
            try
            {
                for (var i = 0; i < 400; i++)
                {
                    _banc.Depot.AjouterCopie(ContenusDeTest.Texte($"copie numéro {i} du texte cherché"));
                    _banc.Depot.AppliquerPlafond(50);
                }
            }
            catch (Exception exception)
            {
                erreurs.Enqueue(exception);
            }
            finally
            {
                Volatile.Write(ref ecritureFinie, true);
            }
        });

        var lecteur = new Thread(() =>
        {
            try
            {
                while (!Volatile.Read(ref ecritureFinie))
                {
                    moteur.Chercher("texte cherché");
                }
            }
            catch (Exception exception)
            {
                erreurs.Enqueue(exception);
            }
        });

        ecrivain.Start();
        lecteur.Start();
        ecrivain.Join();
        lecteur.Join();

        Assert.Empty(erreurs);
        Assert.Equal(50, moteur.Chercher("texte cherché").Count);
    }
}
