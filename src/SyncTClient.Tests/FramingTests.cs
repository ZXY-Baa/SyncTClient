using Google.Protobuf;
using SyncTClient.Bep;
using SyncTClient.Bep.Proto;
using IndexMessage = SyncTClient.Bep.Proto.Index;
using ProtoFileInfo = SyncTClient.Bep.Proto.FileInfo;

namespace SyncTClient.Tests;

/// <summary>
/// Die Rahmung auf dem Draht. Hier zaehlt vor allem, was <em>nicht</em>
/// passieren darf: eine Ankuendigung ueber 500 MB darf keinen Puffer dieser
/// Groesse anlegen, bevor die Laenge geprueft ist.
/// </summary>
public class FramingTests
{
    [Fact]
    public async Task Nachricht_geht_durch_und_kommt_unveraendert_zurueck()
    {
        using var strom = new MemoryStream();

        await BepFraming.WriteMessageAsync(strom, MessageType.Ping, new Ping(), CancellationToken.None);
        strom.Position = 0;

        var (typ, nutzlast) = await BepFraming.ReadMessageAsync(strom, CancellationToken.None);

        Assert.Equal(MessageType.Ping, typ);
        Assert.Equal(new Ping().ToByteArray(), nutzlast);
    }

    [Fact]
    public async Task Laengere_Nutzlast_bleibt_vollstaendig()
    {
        var nachricht = new IndexMessage();
        nachricht.Files.Add(new ProtoFileInfo { Name = new string('x', 5000), Size = 1234 });

        using var strom = new MemoryStream();
        await BepFraming.WriteMessageAsync(strom, MessageType.Index, nachricht, CancellationToken.None);
        strom.Position = 0;

        var (typ, nutzlast) = await BepFraming.ReadMessageAsync(strom, CancellationToken.None);

        Assert.Equal(MessageType.Index, typ);

        var gelesen = IndexMessage.Parser.ParseFrom(nutzlast);
        Assert.Equal(nachricht.Files[0].Name, gelesen.Files[0].Name);
        Assert.Equal(1234, gelesen.Files[0].Size);
    }

    [Fact]
    public async Task Angekuendigte_Laenge_ueber_dem_Erlaubten_wird_abgewiesen()
    {
        // Header-Laenge 0, dann eine Nachrichtenlaenge von 500_000_001 Bytes.
        // Der Puffer dafuer darf nicht angelegt werden -- geprueft wird vor der
        // Zuteilung.
        using var strom = new MemoryStream();
        strom.Write([0x00, 0x00]);
        strom.Write([0x1D, 0xCD, 0x65, 0x01]);
        strom.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await BepFraming.ReadMessageAsync(strom, CancellationToken.None));
    }

    [Fact]
    public async Task Abgeschnittene_Nachricht_wird_erkannt()
    {
        using var strom = new MemoryStream();
        await BepFraming.WriteMessageAsync(strom, MessageType.Ping, new Ping(), CancellationToken.None);

        var alles = strom.ToArray();
        using var halb = new MemoryStream(alles[..^1]);

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await BepFraming.ReadMessageAsync(halb, CancellationToken.None));
    }

    [Fact]
    public async Task Leerer_Strom_endet_mit_einer_Ausnahme()
    {
        using var strom = new MemoryStream();

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await BepFraming.ReadMessageAsync(strom, CancellationToken.None));
    }
}
