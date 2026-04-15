using TorServices.Parser;
using TorServices.Network;
using TorServices.Core;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

namespace TorServices.Core;

public class TorrentController
{
    public async Task StartDownload(string filePath)
    {
        Console.WriteLine("🚀 STARTING TORRENT ENGINE\n");

        byte[] data = File.ReadAllBytes(filePath);

        var parser = new BencodeParser(data);
        var root = parser.Parse() as Dictionary<string, object>;

        if (root == null)
        {
            Console.WriteLine("❌ Invalid torrent");
            return;
        }

        var meta = ExtractMetadata(root);
        int totalPieces = meta.Pieces.Length / 20;

        byte[] infoHash = TorrentCrypto.ComputeInfoHash(parser.RawInfoBytes);
        string peerId = "-UT3530-" + Guid.NewGuid().ToString("N")[..12];

        Console.WriteLine($"📦 {meta.Name}");
        Console.WriteLine($"🔑 {BitConverter.ToString(infoHash).Replace("-", "")}");
        Console.WriteLine($"🧩 Pieces: {totalPieces}\n");

        var tracker = new TrackerClient();

        Console.WriteLine("🌐 Contacting tracker...\n");

        var peers = await tracker.GetPeers(
            meta.Announce,
            infoHash,
            meta.Length,
            peerId
        );

        Console.WriteLine($"👥 Peers found: {peers.Count}\n");

        if (peers.Count == 0)
        {
            Console.WriteLine("❌ No peers available");
            return;
        }

        var pieceManager = new PieceManager();
        var downloader = new PieceDownloader();

        // ============================
        // 🔥 PARALLEL SYSTEM
        // ============================

        int maxParallel = 10; // you can tune this
        var semaphore = new SemaphoreSlim(maxParallel);

        var tasks = new List<Task>();

        var pieceQueue = new ConcurrentQueue<int>(
            Enumerable.Range(0, totalPieces)
        );

        var inProgress = new ConcurrentDictionary<int, bool>();

        for (int i = 0; i < maxParallel; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                while (pieceQueue.TryDequeue(out int index))
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        if (pieceManager.HasPiece(index))
                            continue;

                        if (!inProgress.TryAdd(index, true))
                            continue;

                        bool success = false;

                        foreach (var peer in peers)
                        {
                            var parts = peer.Split(':');
                            string ip = parts[0];
                            int port = int.Parse(parts[1]);

                            try
                            {
                                Console.WriteLine($"📥 [{index}] -> {ip}:{port}");

                                using TcpClient client = new TcpClient();
                                await client.ConnectAsync(ip, port);

                                using var stream = client.GetStream();

                                var peerClient = new PeerClient();

                                bool handshake = await peerClient.HandshakeAsync(
                                    stream,
                                    infoHash,
                                    peerId
                                );

                                if (!handshake)
                                    continue;

                                int pieceLength =
                                    (index == totalPieces - 1)
                                        ? (int)(meta.Length - (long)index * meta.PieceLength)
                                        : meta.PieceLength;

                                byte[] piece = await downloader.DownloadPiece(
                                    stream,
                                    index,
                                    pieceLength
                                );

                                byte[] expected = new byte[20];
                                Buffer.BlockCopy(meta.Pieces, index * 20, expected, 0, 20);

                                if (PieceVerifier.Verify(piece, expected))
                                {
                                    pieceManager.StorePiece(index, piece);

                                    Console.WriteLine($"✅ [{index}] OK");

                                    success = true;
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine($"❌ [{index}] corrupt");
                                }
                            }
                            catch
                            {
                                // silent fail → try next peer
                            }
                        }

                        if (!success)
                        {
                            Console.WriteLine($"🔁 [{index}] requeue");
                            pieceQueue.Enqueue(index); // retry later
                        }
                    }
                    finally
                    {
                        inProgress.TryRemove(index, out _);
                        semaphore.Release();
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // ============================
        // BUILD FILE
        // ============================

        if (pieceManager.GetMissingPieces(totalPieces).Count == 0)
        {
            Console.WriteLine("\n💾 Building file...");

            byte[] file = pieceManager.BuildFile(totalPieces);

            string output = $"{meta.Name}.bin";
            File.WriteAllBytes(output, file);

            Console.WriteLine("🎉 DOWNLOAD COMPLETE");
        }
        else
        {
            Console.WriteLine("\n❌ Download incomplete");
        }
    }

    // ---------------- METADATA ----------------

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