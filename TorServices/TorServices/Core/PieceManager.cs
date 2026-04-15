using System.Collections.Concurrent;

namespace TorServices.Core;

public class PieceManager
{
    private readonly ConcurrentDictionary<int, byte[]> _pieces = new();

    public void StorePiece(int index, byte[] data)
    {
        _pieces[index] = data;
    }

    public bool HasPiece(int index)
        => _pieces.ContainsKey(index);

    public List<int> GetMissingPieces(int totalPieces)
    {
        var missing = new List<int>();

        for (int i = 0; i < totalPieces; i++)
        {
            if (!_pieces.ContainsKey(i))
                missing.Add(i);
        }

        return missing;
    }

    // =========================
    // SAFE BUILD (NO CRASH)
    // =========================
    public byte[] BuildFile(int totalPieces)
    {
        var file = new List<byte>();

        for (int i = 0; i < totalPieces; i++)
        {
            if (_pieces.TryGetValue(i, out var piece))
            {
                file.AddRange(piece);
            }
            else
            {
                Console.WriteLine($"⚠ Missing piece {i} - build aborted safely");
                throw new Exception($"Cannot build file: missing piece {i}");
            }
        }

        return file.ToArray();
    }

    // =========================
    // SAFE VERSION (NO THROW)
    // =========================
    public byte[] BuildFileSafe(int totalPieces)
    {
        var file = new List<byte>();

        for (int i = 0; i < totalPieces; i++)
        {
            if (_pieces.TryGetValue(i, out var piece))
            {
                file.AddRange(piece);
            }
            else
            {
                Console.WriteLine($"⚠ Skipping missing piece {i}");
            }
        }

        return file.ToArray();
    }

    public int CompletedPieces => _pieces.Count;
}