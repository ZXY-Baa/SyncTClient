using SyncTClient.Bep;

namespace SyncTClient.Tests;

/// <summary>
/// Die Geraete-ID: Kuerzel der Zertifikatspruefsumme, in der Form, die
/// Syncthing anzeigt und die ein Mensch abtippt.
/// </summary>
public class DeviceIdTests
{
    private static DeviceId Zufaellig()
    {
        var roh = new byte[DeviceId.Length];
        Random.Shared.NextBytes(roh);
        return DeviceId.FromBytes(roh);
    }

    [Fact]
    public void Text_und_Wiedererkennung_ergeben_dieselbe_Kennung()
    {
        var id = Zufaellig();

        Assert.Equal(id, DeviceId.Parse(id.ToString()));
    }

    [Fact]
    public void Kleinschreibung_und_Bindestriche_stoeren_nicht()
    {
        var id = Zufaellig();
        var text = id.ToString();

        Assert.Equal(id, DeviceId.Parse(text.ToLowerInvariant()));
        Assert.Equal(id, DeviceId.Parse("  " + text + "  "));
    }

    [Fact]
    public void Leerer_Text_ist_die_leere_Kennung()
    {
        Assert.True(DeviceId.TryParse(string.Empty, out var id, out var fehler));
        Assert.Null(fehler);
        Assert.Equal(DeviceId.Empty, id);
    }

    [Fact]
    public void Unsinn_wird_abgelehnt()
    {
        Assert.False(DeviceId.TryParse("nicht-base32-###", out _, out var fehler));
        Assert.NotNull(fehler);

        Assert.Throws<FormatException>(() => DeviceId.Parse("nicht-base32-###"));
    }

    [Fact]
    public void Kuerzel_hat_sieben_Zeichen()
    {
        var id = Zufaellig();

        Assert.Equal(7, id.Short().Length);
        Assert.Equal(id.Short(), DeviceId.Parse(id.ToString()).Short());
    }

    [Fact]
    public void Gleiche_Bytes_ergeben_gleiche_Kennung()
    {
        var roh = new byte[DeviceId.Length];
        Random.Shared.NextBytes(roh);

        Assert.Equal(DeviceId.FromBytes(roh), DeviceId.FromBytes(roh));
        Assert.NotEqual(DeviceId.FromBytes(roh), DeviceId.FromBytes(roh.Select(b => (byte)(b ^ 0xFF)).ToArray()));
    }

    [Fact]
    public void Die_leere_Kennung_hat_keinen_Text()
    {
        Assert.Equal(string.Empty, DeviceId.Empty.ToString());
        Assert.Equal(0UL, DeviceId.Empty.ShortId());
    }
}
