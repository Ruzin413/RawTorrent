namespace TorServices.Core;

public static class TorrentDiagnostics
{
    public static void PrintStatus(
        string announce,
        byte[] infoHash,
        List<string> peers,
        bool handshakeSuccess)
    {
        Console.WriteLine("\n==============================");
        Console.WriteLine("🧪 TORRENT SYSTEM STATUS CHECK");
        Console.WriteLine("==============================");

        Console.WriteLine($"\n🌐 Tracker:");
        Console.WriteLine(!string.IsNullOrEmpty(announce)
            ? "✔ OK"
            : "❌ FAIL");

        Console.WriteLine($"\n🔑 InfoHash:");
        Console.WriteLine(infoHash != null && infoHash.Length == 20
            ? "✔ VALID (20 bytes)"
            : "❌ INVALID");

        Console.WriteLine($"\n👥 Peers:");
        Console.WriteLine(peers != null && peers.Count > 0
            ? $"✔ FOUND ({peers.Count})"
            : "❌ NO PEERS");

        Console.WriteLine($"\n🤝 Handshake:");
        Console.WriteLine(handshakeSuccess
            ? "✔ CONNECTED"
            : "❌ FAILED");

        Console.WriteLine("\n==============================");

        if (handshakeSuccess && peers.Count > 0)
            Console.WriteLine("🚀 SYSTEM STATUS: READY FOR DOWNLOAD");
        else
            Console.WriteLine("⚠ SYSTEM STATUS: NOT READY");
    }
}