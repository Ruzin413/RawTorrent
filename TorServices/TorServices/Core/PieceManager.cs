using System;
using System.Collections.Concurrent;
using System.IO;

namespace TorServices.Core;

public class PieceManager : IDisposable
{
    private readonly FileStream _fileStream;
    private readonly int _pieceLength;
    private readonly ConcurrentDictionary<int, bool> _claimed = new();
    private readonly object _lock = new();

    public PieceManager(string fileName, int pieceLength, long totalLength)
    {
        _pieceLength = pieceLength;
        _fileStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        _fileStream.SetLength(totalLength);
    }

    public bool TryClaimPiece(int index)
    {
        return _claimed.TryAdd(index, true);
    }

    public void ReleasePiece(int index)
    {
        _claimed.TryRemove(index, out _);
    }

    public void Store(int index, byte[] data)
    {
        lock (_lock)
        {
            long offset = (long)index * _pieceLength;
            _fileStream.Seek(offset, SeekOrigin.Begin);
            _fileStream.Write(data, 0, data.Length);
            _fileStream.Flush();
        }
    }

    public void BuildFile(int totalPieces, string fileName)
    {
        // Data is written in-place real-time. No memory merge required.
        Console.WriteLine($"\n✅ All pieces streamed directly to {fileName}");
    }

    public void Dispose()
    {
        _fileStream?.Dispose();
    }
}