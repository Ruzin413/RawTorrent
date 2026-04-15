using System.Net.Sockets;
using System.Text;

namespace TorServices.Network;

public class PeerClient
{
    public async Task<bool> HandshakeAsync(NetworkStream stream, byte[] infoHash, string peerId)
    {
        try
        {
            byte[] handshake = new byte[68];

            handshake[0] = 19;
            Encoding.ASCII.GetBytes("BitTorrent protocol").CopyTo(handshake, 1);

            Buffer.BlockCopy(infoHash, 0, handshake, 28, 20);

            Encoding.ASCII.GetBytes(peerId).CopyTo(handshake, 48);

            await stream.WriteAsync(handshake);

            byte[] response = new byte[68];
            int read = 0;

            while (read < 68)
            {
                int r = await stream.ReadAsync(response, read, 68 - read);
                if (r == 0) return false;
                read += r;
            }

            return response[0] == 19;
        }
        catch
        {
            return false;
        }
    }
}