using SyncTClient.Bep;

namespace SyncTClient.Tests;

/// <summary>
/// Die Blockliste. Sie wird jedem anderen Knoten angekuendigt: eine abweichende
/// Zerlegung gilt dort als andere Datei.
/// </summary>
public class BlockListTests
{
    [Fact]
    public void Kleinste_Datei_bekommt_128_KiB_Bloecke()
    {
        Assert.Equal(128 << 10, BlockList.BlockSizeFor(0));
        Assert.Equal(128 << 10, BlockList.BlockSizeFor(1));
        Assert.Equal(128 << 10, BlockList.BlockSizeFor(262_143_999));
    }

    [Fact]
    public void Grenze_liegt_bei_2000_mal_der_Blockgroesse()
    {
        // Nach lib/protocol/blocksize.go gilt die erste Groesse, bei der
        // fileSize < 2000 * blockSize ist -- echt kleiner, nicht kleiner-gleich.
        Assert.Equal(256 << 10, BlockList.BlockSizeFor(262_144_000));
        Assert.Equal(256 << 10, BlockList.BlockSizeFor((2000L * (256 << 10)) - 1));
        Assert.Equal(512 << 10, BlockList.BlockSizeFor(2000L * (256 << 10)));
    }

    [Fact]
    public void Riesige_Datei_bekommt_hoechstens_16_MiB()
    {
        Assert.Equal(16 << 20, BlockList.BlockSizeFor(long.MaxValue));
    }

    [Fact]
    public void Negative_Groesse_wird_abgelehnt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BlockList.BlockSizeFor(-1));
    }

    [Fact]
    public void Kleine_Datei_hat_genau_einen_Block()
    {
        var daten = new byte[1000];
        Random.Shared.NextBytes(daten);

        var (blockGroesse, bloecke, hash) = BlockList.For(new MemoryStream(daten), daten.Length);

        Assert.Equal(128 << 10, blockGroesse);

        var block = Assert.Single(bloecke);
        Assert.Equal(0L, block.Offset);
        Assert.Equal(1000, block.Size);

        // SHA-256 ueber den Block-Hash, also 32 Bytes.
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void Datei_ueber_der_Blockgroesse_hat_zwei_Bloecke()
    {
        var daten = new byte[(128 << 10) + 1];
        Random.Shared.NextBytes(daten);

        var (_, bloecke, _) = BlockList.For(new MemoryStream(daten), daten.Length);

        Assert.Equal(2, bloecke.Count);
        Assert.Equal(0L, bloecke[0].Offset);
        Assert.Equal(128 << 10, bloecke[0].Size);
        Assert.Equal(128 << 10, bloecke[1].Offset);
        Assert.Equal(1, bloecke[1].Size);
    }

    [Fact]
    public void Derselbe_Inhalt_ergibt_dieselbe_Liste()
    {
        var daten = new byte[300_000];
        Random.Shared.NextBytes(daten);

        var a = BlockList.For(new MemoryStream(daten), daten.Length);
        var b = BlockList.For(new MemoryStream(daten), daten.Length);

        Assert.Equal(a.BlockSize, b.BlockSize);
        Assert.Equal(a.BlocksHash, b.BlocksHash);
        Assert.Equal(a.Blocks.Count, b.Blocks.Count);
    }

    [Fact]
    public void Ein_geaendertes_Byte_aendert_den_Gesamthash()
    {
        var daten = new byte[300_000];
        Random.Shared.NextBytes(daten);

        var vorher = BlockList.For(new MemoryStream(daten), daten.Length);

        daten[150_000] ^= 0xFF;
        var nachher = BlockList.For(new MemoryStream(daten), daten.Length);

        Assert.NotEqual(vorher.BlocksHash, nachher.BlocksHash);
    }
}
