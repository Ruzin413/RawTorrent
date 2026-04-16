using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TorServices.Parser;

namespace TorServices.Network;

public class MetadataFetcher
{
    // Fetches metadata using BEP 09
    public async Task<byte[]> FetchMetadataAsync(string ip, int port, byte[] infoHash, string peerId, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(ip, port, cts.Token);

        var stream = tcp.GetStream();
        var peerClient = new PeerClient();

        bool hsOk = await peerClient.HandshakeAsync(stream, infoHash, peerId);
        if (!hsOk) throw new Exception("Handshake failed");

        // Send Extended Handshake (ID=20, Subtype=0)
        int myMetadataId = 1;
        var hsDict = new Dictionary<string, object>
        {
            { "m", new Dictionary<string, object> { { "ut_metadata", myMetadataId } } }
        };
        
        byte[] payload = BencodeEncoder.EncodeDictionary(hsDict);
        byte[] extMsg = new byte[6 + payload.Length];
        
        WriteInt(extMsg, 0, 2 + payload.Length);
        extMsg[4] = 20; // Extended message
        extMsg[5] = 0;  // Subtype 0: Handshake
        Buffer.BlockCopy(payload, 0, extMsg, 6, payload.Length);

        await stream.WriteAsync(extMsg, 0, extMsg.Length, cts.Token);

        int metadataSize = -1;
        int utMetadataId = -1;

        // Wait for peer extended handshake
        while (true)
        {
            var (id, block) = await ReadMessageAsync(stream, cts.Token);

            if (id == 20 && block.Length > 0 && block[0] == 0) // Extended Handshake
            {
                byte[] dictBytes = new byte[block.Length - 1];
                Buffer.BlockCopy(block, 1, dictBytes, 0, dictBytes.Length);

                var parser = new BencodeParser(dictBytes);
                var dict = parser.Parse() as Dictionary<string, object>;
                
                if (dict != null)
                {
                    if (dict.ContainsKey("metadata_size"))
                        metadataSize = Convert.ToInt32(dict["metadata_size"]);
                    
                    if (dict.ContainsKey("m"))
                    {
                        var m = dict["m"] as Dictionary<string, object>;
                        if (m != null && m.ContainsKey("ut_metadata"))
                            utMetadataId = Convert.ToInt32(m["ut_metadata"]);
                    }
                }
                
                break;
            }
        }

        if (metadataSize <= 0 || utMetadataId <= 0)
            throw new Exception("Peer doesn't support ut_metadata");

        Console.WriteLine($"\n📦 Peer advertised metadata: {metadataSize} bytes. Downloading...");

        // Request piece 0
        var reqDict = new Dictionary<string, object>
        {
            { "msg_type", 0 },
            { "piece", 0 }
        };

        byte[] reqPayload = BencodeEncoder.EncodeDictionary(reqDict);
        byte[] reqMsg = new byte[6 + reqPayload.Length];
        WriteInt(reqMsg, 0, 2 + reqPayload.Length);
        reqMsg[4] = 20;
        reqMsg[5] = (byte)utMetadataId;
        Buffer.BlockCopy(reqPayload, 0, reqMsg, 6, reqPayload.Length);

        await stream.WriteAsync(reqMsg, 0, reqMsg.Length, cts.Token);

        // Receive piece 0
        while (true)
        {
            var (id, block) = await ReadMessageAsync(stream, cts.Token);

            if (id == 20 && block.Length > 0 && block[0] == myMetadataId)
            {
                // Decode dict
                byte[] dictBytes = new byte[block.Length - 1];
                Buffer.BlockCopy(block, 1, dictBytes, 0, dictBytes.Length);

                var parser = new BencodeParser(dictBytes);
                var dict = parser.Parse() as Dictionary<string, object>;
                
                if (dict != null && dict.ContainsKey("msg_type") && Convert.ToInt32(dict["msg_type"]) == 1)
                {
                    int bencodeEnd = parser.CurrentIndex;
                    int rawSize = block.Length - 1 - bencodeEnd;

                    if (rawSize == metadataSize)
                    {
                        byte[] meta = new byte[rawSize];
                        Buffer.BlockCopy(block, 1 + bencodeEnd, meta, 0, rawSize);
                        return meta;
                    }
                }
            }
        }
    }

    // DetectBencodeEnd was removed as we use parser.CurrentIndex now.

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

        int length = (lenBuf[0] << 24) | (lenBuf[1] << 16) | (lenBuf[2] << 8) | lenBuf[3];
        if (length == 0) return (99, Array.Empty<byte>());

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
        if (length > 1) Buffer.BlockCopy(body, 1, payload, 0, length - 1);
        return (id, payload);
    }

    private void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
