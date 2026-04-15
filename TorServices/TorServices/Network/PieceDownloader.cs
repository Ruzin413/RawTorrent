using System.Net.Sockets;
using TorServices.Network;

namespace TorServices.Network;

public class PieceDownloader
{
    private const int BlockSize = 16384;

    // ================================
    // WAIT FOR UNCHOKE
    // ================================
    public async Task WaitForUnchokeAsync(NetworkStream stream)
    {
        while (true)
        {
            var (id, _) = await ReadMessageAsync(stream);

            if (id == PeerMessage.Unchoke)
                return;
        }
    }

    // ================================
    // DOWNLOAD FULL PIECE (FIXED)
    // ================================
    public async Task<byte[]> DownloadPiece(
        NetworkStream stream,
        int index,
        int pieceLength)
    {
        List<byte> piece = new();
        int downloaded = 0;

        // IMPORTANT: keep requesting blocks until full piece is built
        while (downloaded < pieceLength)
        {
            int requestLength = Math.Min(BlockSize, pieceLength - downloaded);

            // send request
            byte[] request = BuildRequest(index, downloaded, requestLength);
            await stream.WriteAsync(request, 0, request.Length);

            // read response
            var (id, payload) = await ReadMessageAsync(stream);

            if (id == PeerMessage.Piece)
            {
                // payload:
                // index (4 bytes) + begin (4 bytes) + block

                if (payload.Length < 8)
                    continue;

                int begin =
                    (payload[0] << 24) |
                    (payload[1] << 16) |
                    (payload[2] << 8) |
                    payload[3];

                int blockSize = payload.Length - 8;

                byte[] block = new byte[blockSize];
                Buffer.BlockCopy(payload, 8, block, 0, blockSize);

                piece.AddRange(block);
                downloaded += blockSize;
            }
            else if (id == PeerMessage.Choke)
            {
                await WaitForUnchokeAsync(stream);
            }
            else if (id == 99)
            {
                throw new Exception("Connection lost");
            }
        }

        return piece.ToArray();
    }

    // ================================
    // READ MESSAGE (SAFE)
    // ================================
    private async Task<(byte id, byte[] payload)> ReadMessageAsync(NetworkStream stream)
    {
        byte[] lenBuf = new byte[4];
        int readTotal = 0;

        while (readTotal < 4)
        {
            int read = await stream.ReadAsync(lenBuf, readTotal, 4 - readTotal);
            if (read == 0)
                return (99, Array.Empty<byte>());

            readTotal += read;
        }

        int length =
            (lenBuf[0] << 24) |
            (lenBuf[1] << 16) |
            (lenBuf[2] << 8) |
            lenBuf[3];

        if (length == 0)
            return (99, Array.Empty<byte>()); // keep-alive

        byte[] body = new byte[length];
        int bodyRead = 0;

        while (bodyRead < length)
        {
            int read = await stream.ReadAsync(body, bodyRead, length - bodyRead);
            if (read == 0)
                throw new Exception("Connection closed");

            bodyRead += read;
        }

        byte id = body[0];

        byte[] payload = new byte[length - 1];
        if (length > 1)
            Buffer.BlockCopy(body, 1, payload, 0, length - 1);

        return (id, payload);
    }

    // ================================
    // BUILD REQUEST MESSAGE
    // ================================
    private byte[] BuildRequest(int index, int begin, int length)
    {
        byte[] msg = new byte[17];

        // length = 13 bytes after this field
        msg[0] = 0;
        msg[1] = 0;
        msg[2] = 0;
        msg[3] = 13;

        msg[4] = PeerMessage.Request;

        WriteInt(msg, 5, index);
        WriteInt(msg, 9, begin);
        WriteInt(msg, 13, length);

        return msg;
    }

    // ================================
    // BIG ENDIAN INT WRITER
    // ================================
    private void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}