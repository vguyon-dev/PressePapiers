using Microsoft.Extensions.Time.Testing;
using PressePapiers.Coeur.Clavier;

namespace PressePapiers.Tests.Clavier;

public sealed class AntiRebondTests
{
    private static readonly TimeSpan Delai = TimeSpan.FromMilliseconds(300);

    [Fact]
    public void Le_premier_signal_est_accepte()
    {
        var antiRebond = new AntiRebond(new FakeTimeProvider(), Delai);

        Assert.True(antiRebond.Accepter());
    }

    [Fact]
    public void Un_second_signal_en_moins_du_delai_est_refuse()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);
        Assert.True(antiRebond.Accepter());

        horloge.Advance(TimeSpan.FromMilliseconds(299));

        Assert.False(antiRebond.Accepter());
    }

    [Fact]
    public void Un_signal_a_la_date_exacte_du_delai_est_accepte()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);
        Assert.True(antiRebond.Accepter());

        horloge.Advance(Delai);

        Assert.True(antiRebond.Accepter());
    }

    [Fact]
    public void Un_signal_apres_le_delai_est_accepte()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);
        Assert.True(antiRebond.Accepter());

        horloge.Advance(TimeSpan.FromMilliseconds(301));

        Assert.True(antiRebond.Accepter());
    }

    [Fact]
    public void Un_signal_refuse_ne_repousse_pas_la_fenetre()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);
        Assert.True(antiRebond.Accepter());           // t = 0
        horloge.Advance(TimeSpan.FromMilliseconds(200));
        Assert.False(antiRebond.Accepter());          // t = 200, refusé

        horloge.Advance(TimeSpan.FromMilliseconds(150));

        // t = 350 : 350 ms après le dernier signal ACCEPTÉ. Si le refus de t = 200 repoussait la fenêtre,
        // on ne serait qu'à 150 ms et ce signal serait refusé à tort.
        Assert.True(antiRebond.Accepter());
    }

    [Fact]
    public void Une_rafale_de_signaux_serres_ne_laisse_passer_que_le_premier()
    {
        var horloge = new FakeTimeProvider();
        var antiRebond = new AntiRebond(horloge, Delai);

        var acceptes = 0;
        for (var i = 0; i < 10; i++)
        {
            if (antiRebond.Accepter()) acceptes++;
            horloge.Advance(TimeSpan.FromMilliseconds(20));
        }

        Assert.Equal(1, acceptes);
    }

    [Fact]
    public void Le_delai_nul_accepte_tout()
    {
        var antiRebond = new AntiRebond(new FakeTimeProvider(), TimeSpan.Zero);

        Assert.True(antiRebond.Accepter());
        Assert.True(antiRebond.Accepter());
    }
}
