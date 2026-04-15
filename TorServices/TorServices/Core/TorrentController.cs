using TorServices.Parser;
using TorServices.Network;
using TorServices.Core;
using System.Net.Sockets;
using System.Text;

namespace TorServices.Core;

public class TorrentController
{
    public async Task StartDownload(string filePath)
    {
        Console.WriteLine("🚀 STARTING TORRENT ENGINE\n");

        // 1. READ FILE
        byte[] data = File.ReadAllBytes(filePath);

        // 2. PARSE
        var parser = new BencodeParser(data);
        var result = parser.Parse();

        var root = result as Dictionary<string, object>;
        if (root == null)
        {
            Console.WriteLine("❌ Invalid torrent");
            return;
        }

        // 3. METADATA
        var meta = ExtractMetadata(root);

        int totalPieces = meta.Pieces.Length / 20;

        Console.WriteLine($"\n📦 TORRENT INFO:");
        Console.WriteLine($"Tracker: {meta.Announce}");
        Console.WriteLine($"Name: {meta.Name}");
        Console.WriteLine($"Size: {meta.Length}");
        Console.WriteLine($"Pieces: {totalPieces}");

        // 4. INFO HASH
        byte[] infoHash = TorrentCrypto.ComputeInfoHash(parser.RawInfoBytes);
        string peerId = "-UT3530-" + Guid.NewGuid().ToString("N")[..12];

        Console.WriteLine($"\n🔑 InfoHash: {BitConverter.ToString(infoHash).Replace("-", "")}");

        // 5. TRACKER
        var tracker = new TrackerClient();

        Console.WriteLine("\n🌐 Contacting tracker...");

        var peers = await tracker.GetPeers(
            meta.Announce,
            infoHash,
            meta.Length,
            peerId
        );

        Console.WriteLine($"\n👥 Peers found: {peers.Count}");

        if (peers.Count == 0)
        {
            Console.WriteLine("❌ No peers found");
            return;
        }

        // 6. PIECE STORAGE
        var pieceManager = new PieceManager();
        var downloader = new PieceDownloader();

        // 7. DOWNLOAD LOOP
        foreach (var peer in peers)
        {
            var parts = peer.Split(':');
            string ip = parts[0];
            int port = int.Parse(parts[1]);

            try
            {
                Console.WriteLine($"\n📡 Connecting {ip}:{port}...");

                using TcpClient client = new TcpClient();
                await client.ConnectAsync(ip, port);

                using var stream = client.GetStream();

                var peerClient = new PeerClient();

                bool handshake = await peerClient.HandshakeAsync(stream, infoHash, peerId);

                if (!handshake)
                {
                    Console.WriteLine($"❌ Handshake failed {peer}");
                    continue;
                }

                Console.WriteLine($"🎉 Handshake OK {peer}");

                var missing = pieceManager.GetMissingPieces(totalPieces);

                foreach (int index in missing)
                {
                    try
                    {
                        Console.WriteLine($"📥 Piece {index}");

                        int pieceLength =
                            (index == totalPieces - 1)
                                ? (int)(meta.Length - (long)index * meta.PieceLength)
                                : meta.PieceLength;

                        byte[] piece = await downloader.DownloadPiece(stream, index, pieceLength);

                        byte[] expected = new byte[20];
                        Buffer.BlockCopy(meta.Pieces, index * 20, expected, 0, 20);

                        if (PieceVerifier.Verify(piece, expected))
                        {
                            pieceManager.StorePiece(index, piece);
                            Console.WriteLine($"✅ Piece {index} OK");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Piece {index} corrupted");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠ Piece {index} error: {ex.Message}");
                    }
                }

                if (pieceManager.GetMissingPieces(totalPieces).Count == 0)
                {
                    Console.WriteLine("\n💾 Building file...");

                    byte[] file = pieceManager.BuildFile(totalPieces);

                    File.WriteAllBytes($"{meta.Name}.bin", file);

                    Console.WriteLine("🎉 DOWNLOAD COMPLETE");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Peer error: {ex.Message}");
            }
        }

        Console.WriteLine("\n❌ Download incomplete (missing pieces remain)");
    }

    private TorrentMetadata ExtractMetadata(Dictionary<string, object> root)
    {
        var info = (Dictionary<string, object>)root["info"];

        return new TorrentMetadata
        {
            Announce = Encoding.UTF8.GetString((byte[])root["announce"]),
            Name = Encoding.UTF8.GetString((byte[])info["name"]),
            Length = (long)info["length"],
            PieceLength = Convert.ToInt32(info["piece length"]),
            Pieces = (byte[])info["pieces"]
        };
    }
}