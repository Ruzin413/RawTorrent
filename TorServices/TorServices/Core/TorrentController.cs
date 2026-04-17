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
    private readonly DhtClient _dht = new();
    private readonly ConcurrentQueue<string> _peerDiscoveryQueue = new();
    private readonly ConcurrentDictionary<string, bool> _triedPeers = new();
    private readonly List<PeerSession> _activeSessions = new();
    private const int MaxActiveSessions = 30;

    public async Task StartDownload(string torrentPath, string outputDir = null)
    {
        Console.WriteLine("🚀 STARTING TORRENT ENGINE\n");

        byte[] torrentData = TorrentFileReader.Read(torrentPath);
        var parser = new BencodeParser(torrentData);
        var meta = parser.Parse() as Dictionary<string, object>;

        if (meta == null) return;

        var infoDict = meta["info"] as Dictionary<string, object>;
        var metadata = new TorrentMetadata(infoDict);

        byte[] infoHash = TorrentCrypto.ComputeInfoHash(parser.RawInfoBytes);
        string peerId = "-TS0001-" + Guid.NewGuid().ToString("N")[..12];

        List<string> trackers = GetTrackers(meta);
        await ExecuteDownload(infoHash, peerId, metadata, trackers, outputDir);
    }

    public async Task StartMagnetDownload(string magnetUri, string outputDir = null)
    {
        Console.WriteLine("🧲 STARTING MAGNET ENGINE\n");

        var magnet = MagnetParser.Parse(magnetUri);
        string peerId = "-TS0001-" + Guid.NewGuid().ToString("N")[..12];

        Console.WriteLine("🌐 Searching DHT and Trackers for magnet peers...");
        
        // 1. Initial concurrent discovery for metadata fetching
        _ = Task.Run(async () => {
            var ps = await _dht.GetPeersAsync(magnet.InfoHash);
            foreach (var p in ps) if (_triedPeers.TryAdd(p, true)) _peerDiscoveryQueue.Enqueue(p);
        });

        foreach (var url in GetDiscoveryTrackers(magnet.Trackers))
        {
            _ = Task.Run(async () => {
                try {
                    var ps = await _tracker.GetPeers(url, magnet.InfoHash, 0, peerId);
                    foreach (var p in ps) if (_triedPeers.TryAdd(p, true)) _peerDiscoveryQueue.Enqueue(p);
                } catch { }
            });
        }

        // Wait for at least some peers to be discovered
        DateTime start = DateTime.Now;
        while (_peerDiscoveryQueue.IsEmpty && (DateTime.Now - start).TotalSeconds < 10) await Task.Delay(500);

        // 2. Fetch metadata from peers concurrently
        var fetcher = new MetadataFetcher();
        byte[]? infoData = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var fetchTasks = new List<Task>();
        const int MaxMetadataConcurrent = 20;

        Console.WriteLine("🚀 Searching metadata from discovered peers concurrently...");

        while (infoData == null && !cts.IsCancellationRequested)
        {
            while (fetchTasks.Count < MaxMetadataConcurrent && _peerDiscoveryQueue.TryDequeue(out string peerAddr))
            {
                var pAddr = peerAddr;
                fetchTasks.Add(Task.Run(async () => {
                    try {
                        var (ip, port) = ParsePeer(pAddr);
                        var data = await fetcher.FetchMetadataAsync(ip, port, magnet.InfoHash, peerId, cts.Token);
                        if (data != null) {
                            Interlocked.CompareExchange(ref infoData, data, null);
                            cts.Cancel(); // Success!
                        }
                    } catch { }
                }));
            }

            if (fetchTasks.Count == 0 && _peerDiscoveryQueue.IsEmpty) break;

            var finished = await Task.WhenAny(fetchTasks.Concat(new[] { Task.Delay(2000, cts.Token) }));
            fetchTasks.RemoveAll(t => t.IsCompleted);

            if (infoData != null) break;
        }

        if (infoData == null) { Console.WriteLine("❌ Failed to fetch metadata."); return; }

        var parser = new BencodeParser(infoData);
        var infoDict = parser.Parse() as Dictionary<string, object>;
        var metadata = new TorrentMetadata(infoDict);

        await ExecuteDownload(magnet.InfoHash, peerId, metadata, magnet.Trackers, outputDir);
    }

    private async Task ExecuteDownload(byte[] infoHash, string peerId, TorrentMetadata metadata, List<string> trackers, string outputDir, List<string>? initialPeers = null)
    {
        _triedPeers.Clear(); // Critical: Reset so we can connect to peers used for metadata
        int pieceCount = metadata.Pieces.Length / 20;
        using var pieceManager = new PieceManager(metadata, outputDir);
        using var cts = new CancellationTokenSource();

        Console.WriteLine($"\n📦 {metadata.Name} ({pieceCount} pieces)");

        // 1. Start background discovery
        _ = Task.Run(async () => {
            while (!cts.IsCancellationRequested) {
                foreach (var t in GetDiscoveryTrackers(trackers)) {
                    try {
                        var ps = await _tracker.GetPeers(t, infoHash, metadata.TotalLength, peerId);
                        var newPeers = ps.Where(p => !_triedPeers.ContainsKey(p)).ToList();
                        if (newPeers.Count > 0) {
                            Console.WriteLine($"🌐 Tracker {t} found {newPeers.Count} new peers.");
                            foreach (var p in newPeers) _peerDiscoveryQueue.Enqueue(p);
                        }
                    } catch { }
                }
                await Task.Delay(TimeSpan.FromMinutes(15), cts.Token);
            }
        }, cts.Token);

        _ = Task.Run(async () => {
            while (!cts.IsCancellationRequested) {
                var ps = await _dht.GetPeersAsync(infoHash);
                var newPeers = ps.Where(p => !_triedPeers.ContainsKey(p)).ToList();
                if (newPeers.Count > 0) {
                    Console.WriteLine($"🌐 DHT found {newPeers.Count} new peers.");
                    foreach (var p in newPeers) _peerDiscoveryQueue.Enqueue(p);
                }
                await Task.Delay(TimeSpan.FromMinutes(5), cts.Token);
            }
        }, cts.Token);

        if (initialPeers != null) {
            Console.WriteLine($"🌐 Using {initialPeers.Count} initial peers.");
            foreach (var p in initialPeers) _peerDiscoveryQueue.Enqueue(p);
        }

        // 2. Main download loop
        var downloadTasks = new List<Task>();
        for (int i = 0; i < MaxActiveSessions; i++)
        {
            downloadTasks.Add(Task.Run(async () => {
                while (!cts.IsCancellationRequested && pieceManager.CompletedCount < pieceCount)
                {
                    if (_peerDiscoveryQueue.TryDequeue(out string peerAddr))
                    {
                        if (!_triedPeers.TryAdd(peerAddr, true)) continue;

                        try {
                            using var session = new PeerSession(peerAddr, infoHash, peerId, pieceCount);
                            session.OnPeersDiscovered += (parent, found) => {
                                foreach (var f in found) _peerDiscoveryQueue.Enqueue(f);
                            };

                            if (await session.StartAsync(cts.Token))
                            {
                                Console.WriteLine($"🔌 Connected to {peerAddr}");
                                lock (_activeSessions) _activeSessions.Add(session);
                                
                                try {
                                    while (session.Connected && pieceManager.CompletedCount < pieceCount)
                                    {
                                        int pieceIndex = PickPieceRarestFirst(session, pieceManager, pieceCount, out bool isEndgame);
                                        if (pieceIndex == -1) { await Task.Delay(1000); continue; }

                                        if (pieceManager.TryClaimPiece(pieceIndex) || isEndgame)
                                        {
                                            try {
                                                var data = await session.RequestPieceAsync(pieceIndex, GetPieceLength(pieceIndex, pieceCount, metadata.TotalLength, metadata.PieceLength), cts.Token);
                                                
                                                byte[] expectedHash = new byte[20];
                                                Buffer.BlockCopy(metadata.Pieces, pieceIndex * 20, expectedHash, 0, 20);

                                                if (PieceVerifier.Verify(data, expectedHash)) {
                                                    pieceManager.Store(pieceIndex, data);
                                                    Console.Write(".");
                                                } else {
                                                    pieceManager.ReleasePiece(pieceIndex);
                                                }
                                            } catch {
                                                pieceManager.ReleasePiece(pieceIndex);
                                                throw; // Drop connection if download fails
                                            }
                                        }
                                    }
                                } finally {
                                    lock (_activeSessions) _activeSessions.Remove(session);
                                    Console.WriteLine($"🔌 Disconnected from {peerAddr}");
                                }
                            }
                        } catch { }
                    }
                    else
                    {
                        // Wait for more peers
                        await Task.Delay(500);
                    }
                }
            }));
        }

        while (pieceManager.CompletedCount < pieceCount) {
            string status = $"[⚡] Progress: {pieceManager.CompletedCount}/{pieceCount} ({((double)pieceManager.CompletedCount/pieceCount*100):0.0}%) | Active Peers: {_activeSessions.Count}     ";
            Console.Title = status;
            
            // Also print to console directly for visibility
            Console.Write($"\r{status}");
            
            await Task.Delay(1000);
        }

        cts.Cancel();
        await Task.WhenAll(downloadTasks);
        Console.WriteLine("\n🎉 DOWNLOAD COMPLETE");
    }

    private int PickPieceRarestFirst(PeerSession session, PieceManager pieceManager, int totalPieces, out bool isEndgame)
    {
        isEndgame = false;
        var candidates = new List<int>();
        var endgameCandidates = new List<int>();

        for (int i = 0; i < totalPieces; i++)
        {
            if (session.Bitfield.HasPiece(i) && !pieceManager.IsPieceCompleted(i))
            {
                if (!pieceManager.IsClaimed(i)) candidates.Add(i);
                else endgameCandidates.Add(i);
            }
        }

        if (candidates.Count > 0)
        {
            var availability = new int[totalPieces];
            lock (_activeSessions)
            {
                foreach (var s in _activeSessions)
                {
                    foreach (var c in candidates)
                        if (s.Bitfield.HasPiece(c)) availability[c]++;
                }
            }

            int minAvailability = candidates.Min(c => availability[c]);
            var rarestCandidates = candidates.Where(c => availability[c] == minAvailability).ToList();
            
            return rarestCandidates[Random.Shared.Next(rarestCandidates.Count)];
        }

        if (endgameCandidates.Count > 0)
        {
            isEndgame = true;
            return endgameCandidates[Random.Shared.Next(endgameCandidates.Count)];
        }

        return -1;
    }

    // [Rest of helpers: GetTrackers, GetDiscoveryTrackers, ParsePeer, GetPieceLength]
    private List<string> GetDiscoveryTrackers(List<string> original)
    {
        var list = original.Distinct().ToList();
        var fallbacks = new[] {
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://tracker.openbittorrent.com:80/announce",
            "udp://9.rarbg.to:2710/announce",
            "udp://exodus.desync.com:6969/announce"
        };
        foreach (var f in fallbacks) if (!list.Contains(f)) list.Add(f);
        return list;
    }

    private (string ip, int port) ParsePeer(string peer)
    {
        var parts = peer.Split(':');
        return (parts[0], int.Parse(parts[1]));
    }

    private int GetPieceLength(int index, int totalPieces, long fileSize, int pieceLength)
    {
        if (index == totalPieces - 1) {
            long last = fileSize % pieceLength;
            return last == 0 ? pieceLength : (int)last;
        }
        return pieceLength;
    }

    private List<string> GetTrackers(Dictionary<string, object> meta)
    {
        var trackers = new List<string>();
        if (meta.ContainsKey("announce")) trackers.Add(Encoding.UTF8.GetString(meta["announce"] as byte[]));
        if (meta.ContainsKey("announce-list") && meta["announce-list"] is List<object> list)
            foreach (var tier in list) if (tier is List<object> tl) foreach (var t in tl) trackers.Add(Encoding.UTF8.GetString(t as byte[]));
        return trackers.Distinct().ToList();
    }
}