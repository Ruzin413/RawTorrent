using Microsoft.EntityFrameworkCore;
using TorServices.Models;

namespace TorServices.Data;

public class TorrentDbContext : DbContext
{
    public TorrentDbContext(DbContextOptions<TorrentDbContext> options) : base(options) { }

    public DbSet<TorrentRecord> Torrents => Set<TorrentRecord>();
    public DbSet<UserClient> UserClients => Set<UserClient>();
    public DbSet<TorrentProgress> TorrentProgresses => Set<TorrentProgress>();
}
