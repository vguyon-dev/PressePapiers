using PressePapiers.Coeur.Session;

namespace PressePapiers.Tests.Session;

public sealed class SecondeInstanceTests
{
    private static readonly DateTime Maintenant = new(2026, 10, 28, 8, 47, 3);

    [Fact]
    public void DoitPrevenir_SilencieuxALOuvertureDeSession()
    {
        // Cas du 28/10/2026 : tâche planifiée à 08:45:31, démarrage ordinaire à 08:47:03.
        Assert.False(SecondeInstance.DoitPrevenir(new DateTime(2026, 10, 28, 8, 45, 31), Maintenant));
    }

    [Fact]
    public void DoitPrevenir_LancementTardifPrevenu()
    {
        Assert.True(SecondeInstance.DoitPrevenir(Maintenant.AddMinutes(-30), Maintenant));
    }

    [Fact]
    public void DoitPrevenir_DebutIllisiblePrevenu()
    {
        Assert.True(SecondeInstance.DoitPrevenir(null, Maintenant));
    }
}
