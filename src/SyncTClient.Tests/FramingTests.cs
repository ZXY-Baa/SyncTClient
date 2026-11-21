using System.Buffers.Binary;
using Google.Protobuf;
using K4os.Compression.LZ4;
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
    /// <summary>Baut einen Rahmen von Hand, damit sich auch LZ4 pruefen laesst.</summary>
    private static MemoryStream Rahmen(MessageType typ, MessageCompression kompression, byte[] nutzlast)
    {
        var header = new Header { Type = typ, Compression = kompression }.ToByteArray();
        var strom = new MemoryStream();

        strom.WriteByte((byte)(header.Length >> 8));
        strom.WriteByte((byte)header.Length);
        strom.Write(header);

        var laenge = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(laenge, (uint)nutzlast.Length);
        strom.Write(laenge);
        strom.Write(nutzlast);

        strom.Position = 0;
        return strom;
    }

    /// <summary>Verpackt Rohdaten so, wie Syncthing sie komprimiert.</summary>
    private static byte[] Lz4(byte[] roh)
    {
        var puffer = new byte[LZ4Codec.MaximumOutputSize(roh.Length)];
        var geschrieben = LZ4Codec.Encode(roh, puffer);
        Assert.True(geschrieben > 0);

        var nutzlast = new byte[4 + geschrieben];
        BinaryPrimitives.WriteUInt32BigEndian(nutzlast, (uint)roh.Length);
        puffer.AsSpan(0, geschrieben).CopyTo(nutzlast.AsSpan(4));
        return nutzlast;
    }

    private static byte[] BeispielIndex()
    {
        var nachricht = new IndexMessage();
        nachricht.Files.Add(new ProtoFileInfo { Name = new string('x', 5000), Size = 1234 });
        return nachricht.ToByteArray();
    }

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
        var roh = BeispielIndex();

        using var strom = Rahmen(MessageType.Index, MessageCompression.None, roh);
        var (typ, nutzlast) = await BepFraming.ReadMessageAsync(strom, CancellationToken.None);

        Assert.Equal(MessageType.Index, typ);
        Assert.Equal(roh, nutzlast);

        var gelesen = IndexMessage.Parser.ParseFrom(nutzlast);
        Assert.Equal(5000, gelesen.Files[0].Name.Length);
        Assert.Equal(1234, gelesen.Files[0].Size);
    }

    [Fact]
    public async Task Komprimierte_Nachricht_wird_entpackt()
    {
        var roh = BeispielIndex();

        using var strom = Rahmen(MessageType.Index, MessageCompression.Lz4, Lz4(roh));
        var (typ, nutzlast) = await BepFraming.ReadMessageAsync(strom, CancellationToken.None);

        Assert.Equal(MessageType.Index, typ);
        Assert.Equal(roh, nutzlast);
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
    public async Task Entpackte_Groesse_ueber_dem_Erlaubten_wird_abgewiesen()
    {
        // Der Rahmen selbst ist klein; erst die vorangestellte Groesse behauptet
        // 500_000_001 Bytes. Auch das muss vor der Zuteilung auffallen.
        using var strom = Rahmen(MessageType.Index, MessageCompression.Lz4,
            [0x1D, 0xCD, 0x65, 0x01, 0xFF, 0xFF, 0xFF, 0xFF]);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await BepFraming.ReadMessageAsync(strom, CancellationToken.None));
    }

    [Fact]
    public async Task Zu_kurze_Kompression_wird_abgewiesen()
    {
        using var strom = Rahmen(MessageType.Index, MessageCompression.Lz4, [0x01, 0x02, 0x03]);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await BepFraming.ReadMessageAsync(strom, CancellationToken.None));
    }

    [Fact]
    public async Task Kaputte_Kompression_wird_als_Formatfehler_gemeldet()
    {
        // Gueltige Groesse, unsinniger Inhalt dahinter.
        using var strom = Rahmen(MessageType.Index, MessageCompression.Lz4, Lz4Kopf(64, 0x42, 0x43, 0x44));

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await BepFraming.ReadMessageAsync(strom, CancellationToken.None));
    }

    [Fact]
    public async Task Unbekanntes_Verfahren_wird_abgewiesen()
    {
        using var strom = Rahmen(MessageType.Index, (MessageCompression)99, [0x00]);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await BepFraming.ReadMessageAsync(strom, CancellationToken.None));
    }

    [Fact]
    public async Task Abgeschnittene_Nachricht_wird_erkannt()
    {
        using var vollstaendig = new MemoryStream();
        await BepFraming.WriteMessageAsync(vollstaendig, MessageType.Ping, new Ping(), CancellationToken.None);

        var alles = vollstaendig.ToArray();
        using var halb = new MemoryStream(alles[..^1]);

        await Assert.ThrowsAsync<EndOfStreamException>(
            async () => await BepFraming.ReadMessageAsync(halb, CancellationToken.None));
    }

    [Fact]
    public async Task Leerer_Strom_endet_mit_Ende_der_Daten()
    {
        using var strom = new MemoryStream();

        await Assert.ThrowsAsync<EndOfStreamException>(
            async () => await BepFraming.ReadMessageAsync(strom, CancellationToken.None));
    }

    /// <summary>Vorangestellte Groesse gefolgt von beliebigen Bytes.</summary>
    private static byte[] Lz4Kopf(uint groesse, params byte[] rest)
    {
        var nutzlast = new byte[4 + rest.Length];
        BinaryPrimitives.WriteUInt32BigEndian(nutzlast, groesse);
        rest.CopyTo(nutzlast.AsSpan(4));
        return nutzlast;
    }
}
