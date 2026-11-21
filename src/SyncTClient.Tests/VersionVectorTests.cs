using SyncTClient.Bep;
using SyncTClient.Bep.Proto;

namespace SyncTClient.Tests;

/// <summary>
/// Der Vergleich zweier Versionen entscheidet ueber Konflikt oder nicht --
/// und darueber, welche Fassung gewinnt. Zeitstempel taugen dafuer nicht.
/// </summary>
public class VersionVectorTests
{
    private const ulong A = 0x1122334455667788;
    private const ulong B = 0x99AABBCCDDEEFF00;

    private static Vector Vektor(params (ulong Geraet, ulong Stand)[] staende)
    {
        var vektor = new Vector();
        foreach (var (geraet, stand) in staende)
            vektor.Counters.Add(new Counter { Id = geraet, Value = stand });
        return vektor;
    }

    [Fact]
    public void Gleicher_Stand_ist_gleich()
    {
        var links = Vektor((A, 3), (B, 7));
        var rechts = Vektor((A, 3), (B, 7));

        Assert.Equal(VersionOrder.Gleich, VersionVectors.Compare(links, rechts));
    }

    [Fact]
    public void Enthaltene_Fassung_ist_neuer()
    {
        var links = Vektor((A, 4), (B, 7));
        var rechts = Vektor((A, 3), (B, 7));

        Assert.Equal(VersionOrder.Neuer, VersionVectors.Compare(links, rechts));
        Assert.Equal(VersionOrder.Aelter, VersionVectors.Compare(rechts, links));
    }

    [Fact]
    public void Zusaetzliches_Geraet_zaehlt_als_neuer()
    {
        var links = Vektor((A, 1), (B, 1));
        var rechts = Vektor((A, 1));

        Assert.Equal(VersionOrder.Neuer, VersionVectors.Compare(links, rechts));
    }

    [Fact]
    public void Zwei_unabhaengige_Aenderungen_stehen_nebeneinander()
    {
        var links = Vektor((A, 4), (B, 7));
        var rechts = Vektor((A, 3), (B, 8));

        Assert.Equal(VersionOrder.Nebeneinander, VersionVectors.Compare(links, rechts));
        Assert.Equal(VersionOrder.Nebeneinander, VersionVectors.Compare(rechts, links));
    }

    [Fact]
    public void Fehlender_Vektor_gilt_als_leer()
    {
        Assert.Equal(VersionOrder.Gleich, VersionVectors.Compare(null, null));
        Assert.Equal(VersionOrder.Gleich, VersionVectors.Compare(null, new Vector()));
        Assert.Equal(VersionOrder.Neuer, VersionVectors.Compare(Vektor((A, 1)), null));
        Assert.Equal(VersionOrder.Aelter, VersionVectors.Compare(null, Vektor((A, 1))));
    }

    [Fact]
    public void Doppelter_Eintrag_zaehlt_mit_dem_hoechsten_Stand()
    {
        // Sollte nicht vorkommen; Zaehler wachsen nur, also gilt der hoechste.
        var links = Vektor((A, 2), (A, 5));
        var rechts = Vektor((A, 5));

        Assert.Equal(VersionOrder.Gleich, VersionVectors.Compare(links, rechts));
    }
}
