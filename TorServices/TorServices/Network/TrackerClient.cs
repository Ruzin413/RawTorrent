using System.Net.Http;
using System.Text;
using TorServices.Parser;

namespace TorServices.Network;

public class TrackerClient
{
    private readonly HttpClient _client = new();

    public async Task<List<string>> GetPeers(
        string announceUrl,
        byte[] infoHash,
        long fileSize,
        string peerId)
    {
        string url =
            $"{announceUrl}?info_hash={ToPercent(infoHash)}" +
            $"&peer_id={peerId}" +
            $"&port=6881&uploaded=0&downloaded=0&left={fileSize}&compact=1";

        try
        {
            var data = await _client.GetByteArrayAsync(url);

            var parser = new BencodeParser(data);
            var result = parser.Parse() as Dictionary<string, object>;

            if (result != null && result.ContainsKey("peers"))
            {
                return ParsePeers((byte[])result["peers"]);
            }
        }
        catch { }

        return new List<string>();
    }

    private string ToPercent(byte[] data)
        => string.Concat(data.Select(b => $"%{b:x2}"));

    private List<string> ParsePeers(byte[] data)
    {
        var peers = new List<string>();

        for (int i = 0; i < data.Length; i += 6)
        {
            string ip = $"{data[i]}.{data[i + 1]}.{data[i + 2]}.{data[i + 3]}";
            int port = (data[i + 4] << 8) | data[i + 5];
            peers.Add($"{ip}:{port}");
        }

        return peers;
    }
}