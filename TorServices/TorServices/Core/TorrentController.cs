using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TorServices.Core;
using TorServices.Network;
using TorServices.Parser;
using TorServices.DHT;

namespace TorServices.Core;

public class TorrentController
{
    private readonly TrackerClient _tracker = new();
    private readonly PieceDownloader _downloader = new();

    public async Task StartDownload(string torrentPath)
    {
        Console.WriteLine("🚀 STARTING TORRENT ENGINE\n");

        byte[] torrentData = TorrentFileReader.Read(torrentPath);
        var parser = new BencodeParser(torrentData);
        var meta = parser.Parse() as Dictionary<string, object>;

        if (meta == null)
            return;

        var announce = meta["announce"].ToString();
        var info = meta["info"] as Dictionary<string, object>;

        byte[] infoHash = TorrentCrypto.ComputeInfoHash(parser.RawInfoBytes);
        string peerId = "-TS0001-" + Guid.NewGuid().ToString("N")[..12];

        List<string> trackers = new() { announce };

        await ExecuteDownload(infoHash, peerId, info, trackers);
    }

    public async Task StartMagnetDownload(string magnetUri)
    {
        Console.WriteLine("🧲 STARTING MAGNET ENGINE\n");

        var magnet = MagnetParser.Parse(magnetUri);
        string peerId = "-TS0001-" + Guid.NewGuid().ToString("N")[..12];

        // 1. Find some peers first to get metadata
        Console.WriteLine("🌐 Searching DHT and Trackers for magnet peers...");
        List<string> initialPeers = new();
        try
        {
            var dht = new DhtClient();
            initialPeers.AddRange(await dht.GetPeersAsync(magnet.InfoHash));
        }
        catch { }

        // We must also ping trackers because our DHT logic is just a placeholder!
        foreach (var url in magnet.Trackers)
        {
            try
            {
                var p = await _tracker.GetPeers(url, magnet.InfoHash, 0, peerId);
                initialPeers.AddRange(p);
                Console.WriteLine($"🌐 Tracker {url} found {p.Count} peers.");
                if (initialPeers.Count > 10) break; // Don't query infinite trackers just for metadata
            }
            catch { }
        }

        var uniquePeers = initialPeers.Distinct().ToList();

        if (uniquePeers.Count == 0)
        {
            Console.WriteLine("❌ Could not find peers to fetch metadata.");
            return;
        }

        // 2. Fetch metadata from peers concurrently
        var fetcher = new MetadataFetcher();
        byte[] infoData = null;

        var fetchTasks = new List<Task<byte[]>>();
        using var cts = new CancellationTokenSource();

        // Start fetching from the first 50 peers simultaneously
        foreach (var peer in uniquePeers.Take(50))
        {
            var (ip, port) = ParsePeer(peer);
            var task = Task.Run(async () =>
            {
                try
                {
                    // Each fetcher internally times out after 30s
                    return await fetcher.FetchMetadataAsync(ip, port, magnet.InfoHash, peerId, cts.Token);
                }
                catch
                {
                    return null;
                }
            }, cts.Token);
            fetchTasks.Add(task);
        }

        Console.WriteLine($"🚀 Attempting to connect to {fetchTasks.Count} peers concurrently to fetch metadata...");

        // Wait for ANY task to succeed
        while (fetchTasks.Count > 0)
        {
            var finishedTask = await Task.WhenAny(fetchTasks);
            fetchTasks.Remove(finishedTask);

            var result = await finishedTask;
            if (result != null)
            {
                infoData = result;
                cts.Cancel(); // Force stop all other metadata download threads
                Console.WriteLine($"✅ Metadata downloaded successfully from a peer!");
                break;
            }
        }

        if (infoData == null)
        {
            Console.WriteLine("❌ Failed to fetch metadata from any peer. They might all be dead or offline.");
            return;
        }

        var parser = new BencodeParser(infoData);
        var info = parser.Parse() as Dictionary<string, object>;

        if (info == null) return;

        await ExecuteDownload(magnet.InfoHash, peerId, info, magnet.Trackers, uniquePeers);
    }

    private async Task ExecuteDownload(byte[] infoHash, string peerId, Dictionary<string, object> info, List<string> trackers, List<string>? initialDhtPeers = null)
    {
        string name = Encoding.UTF8.GetString((byte[])info["name"]);
        
        long length = 0;
        if (info.ContainsKey("length"))
        {
            length = Convert.ToInt64(info["length"]);
        }
        else if (info.ContainsKey("files"))
        {
            if (info["files"] is List<object> files)
            {
                foreach (var f in files)
                {
                    if (f is Dictionary<string, object> file && file.ContainsKey("length"))
                    {
                        length += Convert.ToInt64(file["length"]);
                    }
                }
            }
        }

        int pieceLength = Convert.ToInt32(info["piece length"]);
        byte[] pieces = (byte[])info["pieces"];

        int pieceCount = pieces.Length / 20;

        Console.WriteLine($"\n📦 {name}");
        Console.WriteLine($"🧩 Pieces: {pieceCount}");
        Console.WriteLine($"🔑 InfoHash: {BitConverter.ToString(infoHash).Replace("-", "")}\n");

        // ---------------- TRACKER ----------------
        Console.WriteLine("🌐 Contacting trackers...\n");

        List<string> allPeers = initialDhtPeers ?? new List<string>();

        foreach (var t in trackers)
        {
            try
            {
                var p = await _tracker.GetPeers(t, infoHash, length, peerId);
                allPeers.AddRange(p);
            }
            catch { }
        }

        if (initialDhtPeers == null)
        {
            try
            {
                var dht = new DhtClient();
                allPeers.AddRange(await dht.GetPeersAsync(infoHash));
            }
            catch { }
        }

        var peers = allPeers.Distinct().ToList();
        Console.WriteLine($"\n👥 Total unique peers: {peers.Count}");

        if (peers.Count == 0)
        {
            Console.WriteLine("❌ No peers available");
            return;
        }

        // ---------------- PIECE MANAGER ----------------
        using var pieceManager = new PieceManager(name, pieceLength, length);

        // ---------------- DOWNLOAD ----------------
        Console.WriteLine("\n📥 Starting download...\n");

        int peerIndex = 0;
        var pieceQueue = new ConcurrentQueue<int>(Enumerable.Range(0, pieceCount));
        
        // Sequentially instead of in parallel for now per request
        int maxConcurrency = 1; // Math.Min(50, pieceCount);

        var tasks = new List<Task>();

        for (int i = 0; i < maxConcurrency; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                while (pieceQueue.TryDequeue(out int id))
                {
                    bool pieceDownloaded = false;
                    while (!pieceDownloaded)
                    {
                        string peer = GetNextPeer(peers, ref peerIndex);

                        try
                        {
                            if (!pieceManager.TryClaimPiece(id))
                            {
                                pieceDownloaded = true;
                                continue;
                            }

                            Console.WriteLine($"📥 [{id}] -> {peer}");

                            var (ip, port) = ParsePeer(peer);

                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                            using var tcp = new TcpClient();
                            await tcp.ConnectAsync(ip, port, cts.Token);

                            var stream = tcp.GetStream();
                            var peerClient = new PeerClient();

                            bool ok = await peerClient.HandshakeAsync(stream, infoHash, peerId);

                            if (!ok)
                            {
                                throw new Exception("Handshake failed");
                            }

                            var data = await _downloader.DownloadPiece(
                                stream,
                                id,
                                GetPieceLength(id, pieceCount, length, pieceLength),
                                cts.Token
                            );

                            byte[] expectedHash = new byte[20];
                            Buffer.BlockCopy(pieces, id * 20, expectedHash, 0, 20);

                            if (!PieceVerifier.Verify(data, expectedHash))
                            {
                                throw new Exception("SHA1 hash mismatch");
                            }

                            pieceManager.Store(id, data);
                            Console.WriteLine($"✅ [{id}] OK");
                            pieceDownloaded = true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠ [{id}] error: {ex.Message}");
                            pieceManager.ReleasePiece(id);
                            // Try the next peer for the same piece
                        }
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("\n💾 Building file...");
        pieceManager.BuildFile(pieceCount, name);
        Console.WriteLine("🎉 DOWNLOAD COMPLETE");
    }

    // ---------------- HELPERS ----------------

    private string GetNextPeer(List<string> peers, ref int index)
    {
        int current = Interlocked.Increment(ref index);
        return peers[Math.Abs(current) % peers.Count];
    }

    private (string ip, int port) ParsePeer(string peer)
    {
        var parts = peer.Split(':');
        return (parts[0], int.Parse(parts[1]));
    }

    private int GetPieceLength(int index, int totalPieces, long fileSize, int pieceLength)
    {
        long last = fileSize % pieceLength;

        if (index == totalPieces - 1 && last != 0)
            return (int)last;

        return pieceLength;
    }
}