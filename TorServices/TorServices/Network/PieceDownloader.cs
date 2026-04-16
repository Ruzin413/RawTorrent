using System.Net.Sockets;

namespace TorServices.Network;

public class PieceDownloader
{
    private const int BlockSize = 16384;

    public async Task WaitForUnchokeAsync(NetworkStream stream, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var (id, _) = await ReadMessageAsync(stream, token);
            if (id == PeerMessage.Unchoke)
                return;
        }
    }

    public async Task<byte[]> DownloadPiece(NetworkStream stream, int index, int length, CancellationToken token)
    {
        byte[] data = new byte[length];
        int receivedBytes = 0;
        int requestedOffset = 0;
        int pendingRequests = 0;
        const int MaxPending = 8; // Standard BitTorrent pipeline size

        byte[] interestedMsg = { 0, 0, 0, 1, 2 };
        await stream.WriteAsync(interestedMsg, 0, 5, token);

        await WaitForUnchokeAsync(stream, token);

        while (receivedBytes < length)
        {
            token.ThrowIfCancellationRequested();

            // 1. Refill the pipeline with multiple "Request" messages
            while (pendingRequests < MaxPending && requestedOffset < length)
            {
                int blockLen = Math.Min(BlockSize, length - requestedOffset);
                byte[] request = BuildRequest(index, requestedOffset, blockLen);
                await stream.WriteAsync(request, 0, request.Length, token);
                
                requestedOffset += blockLen;
                pendingRequests++;
            }

            // 2. Wait for and process the next message from the peer
            var (id, payload) = await ReadMessageAsync(stream, token);

            if (id == PeerMessage.Choke)
            {
                await WaitForUnchokeAsync(stream, token);
                // Reset the requested offset to total received to re-pull the "tail" of the piece
                requestedOffset = receivedBytes;
                pendingRequests = 0;
                continue;
            }

            if (id == PeerMessage.Piece)
            {
                if (payload.Length <= 8)
                    continue;

                int pIndex = (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3];
                int pBegin = (payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7];

                if (pIndex != index)
                    continue;

                int dataLen = payload.Length - 8;
                if (dataLen <= 0 || pBegin + dataLen > length)
                    continue;

                // Store the block and update our counters
                Buffer.BlockCopy(payload, 8, data, pBegin, dataLen);
                receivedBytes += dataLen;
                pendingRequests--;
            }
        }

        return data;
    }

    private async Task<(byte id, byte[] payload)> ReadMessageAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] lenBuf = new byte[4];

        int headerOffset = 0;
        while (headerOffset < 4)
        {
            int r = await stream.ReadAsync(lenBuf, headerOffset, 4 - headerOffset, token);
            if (r == 0) return (99, Array.Empty<byte>());
            headerOffset += r;
        }

        int length =
            (lenBuf[0] << 24) |
            (lenBuf[1] << 16) |
            (lenBuf[2] << 8) |
            lenBuf[3];

        if (length == 0)
            return (99, Array.Empty<byte>()); // keep-alive

        byte[] body = new byte[length];
        int offset = 0;

        while (offset < length)
        {
            int r = await stream.ReadAsync(body, offset, length - offset, token);
            if (r == 0) throw new Exception("Disconnected");
            offset += r;
        }

        byte id = body[0];
        byte[] payload = new byte[length - 1];

        if (length > 1)
            Buffer.BlockCopy(body, 1, payload, 0, length - 1);

        return (id, payload);
    }

    private byte[] BuildRequest(int index, int begin, int length)
    {
        byte[] msg = new byte[17];

        msg[3] = 13;
        msg[4] = PeerMessage.Request;

        WriteInt(msg, 5, index);
        WriteInt(msg, 9, begin);
        WriteInt(msg, 13, length);

        return msg;
    }

    private void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}