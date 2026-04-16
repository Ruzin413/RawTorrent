using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TorServices.DHT;

public class DhtClient
{
    private readonly UdpClient _udp;

    // bootstrap nodes (VERY important)
    private readonly List<DhtNode> _bootstrapNodes = new()
    {
        new DhtNode("router.bittorrent.com", 6881),
        new DhtNode("dht.transmissionbt.com", 6881),
        new DhtNode("router.utorrent.com", 6881)
    };

    public DhtClient(int port = 6881)
    {
        _udp = new UdpClient(port);
    }

    // MAIN: get peers from DHT
    public async Task<List<string>> GetPeersAsync(byte[] infoHash)
    {
        var peers = new List<string>();

        foreach (var node in _bootstrapNodes)
        {
            try
            {
                var results = await QueryNodeForPeers(node, infoHash);
                peers.AddRange(results);
            }
            catch
            {
                // ignore dead nodes
            }
        }

        return peers.Distinct().ToList();
    }

    private async Task<List<string>> QueryNodeForPeers(DhtNode node, byte[] infoHash)
    {
        var peers = new List<string>();
        var request = BuildGetPeersQuery(infoHash);
        var bytes = Encoding.ASCII.GetBytes(request);

        await _udp.SendAsync(bytes, bytes.Length, node.Ip, node.Port);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var result = await _udp.ReceiveAsync(cts.Token);
            string response = Encoding.ASCII.GetString(result.Buffer);
            ExtractPeers(response, peers);
        }
        catch
        {
            // Timeout or failure
        }
        return peers;
    }

    private string BuildGetPeersQuery(byte[] infoHash)
    {
        string hash = Convert.ToHexString(infoHash);

        return $"d1:ad2:id20:{RandomId()}9:info_hash20:{hash}e";
    }

    private string RandomId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 20);
    }

    private void ExtractPeers(string response, List<string> peers)
    {
        // SIMPLE fallback parsing (real DHT = binary compact format)

        var parts = response.Split(':');

        foreach (var part in parts)
        {
            if (part.Contains(".") && part.Contains(" "))
            {
                // ignore for now (placeholder)
            }
        }
    }
}