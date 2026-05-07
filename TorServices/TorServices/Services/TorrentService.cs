using System.Collections.Concurrent;
using TorServices.Core;
using TorServices.Models;
using TorServices.DTOs;
using Microsoft.EntityFrameworkCore;
using TorServices.Data;
using TorServices.Network;
using TorServices.DHT;
using Npgsql;


namespace TorServices.Services;

public class TorrentService
{
    private readonly ConcurrentDictionary<string, TorrentController> _controllers = new();
    private readonly List<string> _queueOrder = new();
    private const int MaxActiveDownloads = 2;
    private readonly object _lock = new();
    private readonly IServiceProvider _serviceProvider;

    public TorrentService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        LoadFromDatabase();
        StartBackgroundMonitor();
    }

    private void StartBackgroundMonitor()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(2000);
                var activeIds = _controllers.Keys.ToList();
                foreach (var id in activeIds)
                {
                    if (_controllers.TryGetValue(id, out var controller))
                    {
                        await SaveToDatabase(controller);
                        
                        // If it's completed, remove from active controllers
                        if (controller.Status == "Completed")
                        {
                            _controllers.TryRemove(id, out _);
                            lock (_lock) _queueOrder.Remove(id);
                        }
                    }
                }
            }
        });
    }

    private void LoadFromDatabase()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TorrentDbContext>();
        var records = context.Torrents.Include(r => r.ProgressInfo).ToList();
        
        foreach (var record in records)
        {
            var status = record.ProgressInfo?.Status ?? "Stopped";
            if (status != "Completed")
            {
                var controller = new TorrentController 
                { 
                    Name = record.Name,
                    OutputDir = record.OutputDir,
                    MagnetUri = record.MagnetUri,
                    TorrentPath = record.TorrentPath,
                    InitialBitfield = record.ProgressInfo?.Bitfield,
                    TotalPieces = record.ProgressInfo?.TotalPieces ?? 0,
                    Status = "Stopped",
                    ClientId = record.ClientId

                };


                
                controller.SetId(record.Id);
                _controllers[record.Id] = controller;
            }
        }
    }

    public IEnumerable<TorrentStatus> GetAllTorrents(string? clientId = null)
    {
        var controllers = _controllers.Values.AsEnumerable();
        if (!string.IsNullOrEmpty(clientId))
        {
            controllers = controllers.Where(c => c.ClientId == clientId);
        }

        return controllers.Select(c => new TorrentStatus
        {
            Id = c.Id,
            Name = c.Name,
            Progress = c.TotalPieces > 0 ? (double)c.CompletedPieces / c.TotalPieces * 100 : 0,
            ActivePeers = c.ActivePeersCount,
            Status = c.Status,
            TotalSize = c.TotalSize,
            CompletedPieces = c.CompletedPieces,
            TotalPieces = c.TotalPieces,
            ClientId = c.ClientId,
            OutputDir = c.OutputDir
        });
    }

    public List<TorrentStatus> GetHistory(string? clientId = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TorrentDbContext>();
        var query = context.Torrents
            .Include(r => r.ProgressInfo)
            .Where(r => r.ProgressInfo != null && r.ProgressInfo.Status == "Completed");

        if (!string.IsNullOrEmpty(clientId))
        {
            query = query.Where(r => r.ClientId == clientId);
        }

        return query
            .OrderByDescending(r => r.ProgressInfo!.LastUpdatedAt)
            .Select(r => new TorrentStatus
            {
                Id = r.Id,
                Name = r.Name,
                Status = r.ProgressInfo!.Status,
                Progress = r.ProgressInfo.Progress,
                TotalPieces = r.ProgressInfo.TotalPieces,
                CompletedPieces = r.ProgressInfo.CompletedPieces,
                ClientId = r.ClientId,
                OutputDir = r.OutputDir
            })
            .ToList();
    }

    public async Task<string> StartTorrent(string path, string? outputDir = null, string? clientId = null)
    {
        var controller = new TorrentController { TorrentPath = path, OutputDir = outputDir, ClientId = clientId };
        _controllers[controller.Id] = controller;
        
        await SaveToDatabase(controller);

        lock (_lock)
        {
            _queueOrder.Add(controller.Id);
        }
        
        ProcessQueue();
        Console.WriteLine($"[Service] Added new torrent to queue: {path} (Client: {clientId})");
        return controller.Id;

    }

    public async Task<string> StartMagnet(string uri, string? outputDir = null, string? clientId = null)
    {
        var controller = new TorrentController { MagnetUri = uri, OutputDir = outputDir, ClientId = clientId };
        _controllers[controller.Id] = controller;
        
        await SaveToDatabase(controller);

        lock (_lock)
        {
            _queueOrder.Add(controller.Id);
        }

        ProcessQueue();
        Console.WriteLine($"[Service] Added new magnet to queue: {uri} (Client: {clientId})");
        return controller.Id;

    }

    private async Task SaveToDatabase(TorrentController controller)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TorrentDbContext>();

        if (!string.IsNullOrEmpty(controller.ClientId))
        {
            var user = await context.UserClients.FindAsync(controller.ClientId);
            if (user == null)
            {
                user = new UserClient { ClientId = controller.ClientId };
                context.UserClients.Add(user);
                try { await context.SaveChangesAsync(); }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505") { }
            }
        }

        var record = await context.Torrents.FindAsync(controller.Id);
        if (record == null)
        {
            record = new TorrentRecord
            {
                Id = controller.Id,
                Name = controller.Name,
                OutputDir = controller.OutputDir,
                MagnetUri = controller.MagnetUri,
                TorrentPath = controller.TorrentPath,
                ClientId = controller.ClientId
            };

            context.Torrents.Add(record);
        }
        else if (controller.Name != "Initializing..." && record.Name == "Initializing...")
        {
            record.Name = controller.Name;
        }

        var progress = await context.TorrentProgresses.FindAsync(controller.Id);
        if (progress == null)
        {
            progress = new TorrentProgress
            {
                TorrentId = controller.Id,
                Status = controller.Status,
                TotalPieces = controller.TotalPieces,
                CompletedPieces = controller.CompletedPieces,
                Bitfield = controller.GetBitfield() ?? controller.InitialBitfield,

                Progress = controller.TotalPieces > 0 ? (double)controller.CompletedPieces / controller.TotalPieces * 100 : 0
            };
            context.TorrentProgresses.Add(progress);
        }
        else
        {
            progress.Status = controller.Status;
            progress.TotalPieces = controller.TotalPieces;
            progress.CompletedPieces = controller.CompletedPieces;
            
            var currentBitfield = controller.GetBitfield();
            if (currentBitfield != null) progress.Bitfield = currentBitfield;

            progress.Progress = controller.TotalPieces > 0 ? (double)controller.CompletedPieces / controller.TotalPieces * 100 : 0;

            progress.LastUpdatedAt = DateTime.UtcNow;
        }


        try
        {
            await context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) 
            when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Ignore duplicate key errors - the record already exists which is fine
        }
    }


    public bool ResumeTorrent(string id)
    {
        if (_controllers.TryGetValue(id, out var controller))
        {
            if (controller.Status == "Stopped" || controller.Status == "Error")
            {
                controller.Status = "Queued";
                lock (_lock)
                {
                    if (!_queueOrder.Contains(id)) _queueOrder.Add(id);
                }
                ProcessQueue();
                return true;
            }
        }
        return false;
    }

    private void ProcessQueue()
    {
        lock (_lock)
        {
            int activeCount = _controllers.Values.Count(c => c.Status == "Downloading" || c.Status == "Starting...");
            
            if (activeCount < MaxActiveDownloads)
            {
                var nextId = _queueOrder.FirstOrDefault(id => 
                    _controllers.TryGetValue(id, out var c) && c.Status == "Queued");

                if (nextId != null && _controllers.TryGetValue(nextId, out var controller))
                {
                    controller.Status = "Starting..."; // Temporary status to avoid double-pick
                    
                    _ = Task.Run(async () => {
                        try {
                            if (!string.IsNullOrEmpty(controller.TorrentPath))
                                await controller.StartDownload(controller.TorrentPath, controller.OutputDir);
                            else if (!string.IsNullOrEmpty(controller.MagnetUri))
                                await controller.StartMagnetDownload(controller.MagnetUri, controller.OutputDir);
                        } catch (Exception ex) {
                            controller.Status = $"Error: {ex.Message}";
                        } finally {

                            ProcessQueue(); // Check for next when this one ends
                        }
                    });

                    // Check if we can start another one immediately
                    Task.Run(() => ProcessQueue());
                }
            }
        }
    }

    public bool StopTorrent(string id)
    {
        if (_controllers.TryGetValue(id, out var controller))
        {
            controller.Stop();
            controller.Status = "Stopped";
            ProcessQueue();
            return true;
        }
        return false;
    }

    public async Task<bool> RemoveTorrent(string id)
    {
        bool found = false;
        if (_controllers.TryRemove(id, out var controller))
        {
            controller.Stop();
            lock (_lock)
            {
                _queueOrder.Remove(id);
            }
            ProcessQueue();
            found = true;
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TorrentDbContext>();
        
        var record = await context.Torrents.FindAsync(id);
        if (record != null)
        {
            context.Torrents.Remove(record);
            await context.SaveChangesAsync();
            found = true;
        }

        return found;
    }

    public async Task ClearAllData(string? clientId = null)
    {
        var idsToRemove = _controllers
            .Where(kvp => string.IsNullOrEmpty(clientId) || kvp.Value.ClientId == clientId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in idsToRemove)
        {
            if (_controllers.TryRemove(id, out var controller))
            {
                controller.Stop();
                lock (_lock) _queueOrder.Remove(id);
            }
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TorrentDbContext>();
        
        var torrents = await context.Torrents
            .Where(t => string.IsNullOrEmpty(clientId) || t.ClientId == clientId)
            .ToListAsync();
            
        var torrentIds = torrents.Select(t => t.Id).ToList();
        var progresses = await context.TorrentProgresses
            .Where(p => torrentIds.Contains(p.TorrentId))
            .ToListAsync();

        context.TorrentProgresses.RemoveRange(progresses);
        context.Torrents.RemoveRange(torrents);

        if (string.IsNullOrEmpty(clientId))
        {
            context.UserClients.RemoveRange(context.UserClients);
        }
        
        await context.SaveChangesAsync();
    }

    public async Task<List<string>> DiscoverPeersForMetadata(byte[] infoHash, List<string> trackers, string peerId)
    {
        var discoveredPeers = new ConcurrentBag<string>();
        var trackerClient = new TrackerClient();
        var dhtClient = new DhtClient();
        
        var tasks = new List<Task>();
        
        foreach (var t in trackers)
        {
            tasks.Add(Task.Run(async () => {
                try {
                    var ps = await trackerClient.GetPeers(t, infoHash, 0, peerId);
                    foreach (var p in ps) discoveredPeers.Add(p);
                } catch { }
            }));
        }
        
        tasks.Add(Task.Run(async () => {
            try {
                var ps = await dhtClient.GetPeersAsync(infoHash);
                foreach (var p in ps) discoveredPeers.Add(p);
            } catch { }
        }));
        
        await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(5000));
        
        return discoveredPeers.Distinct().Take(10).ToList();
    }
}
