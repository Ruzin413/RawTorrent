using System;
using System.IO;

namespace TorServices.Parser;

public static class TorrentFileReader
{
    public static byte[] Read(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path is empty");

            if (!File.Exists(path))
                throw new FileNotFoundException($"Torrent file not found: {path}");

            byte[] data = File.ReadAllBytes(path);

            if (data.Length == 0)
                throw new Exception("Torrent file is empty");

            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to read torrent file: {ex.Message}");
            throw;
        }
    }

    // Optional: async version (useful later for UI apps / large files)
    public static async Task<byte[]> ReadAsync(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path is empty");

            if (!File.Exists(path))
                throw new FileNotFoundException($"Torrent file not found: {path}");

            byte[] data = await File.ReadAllBytesAsync(path);

            if (data.Length == 0)
                throw new Exception("Torrent file is empty");

            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to read torrent file: {ex.Message}");
            throw;
        }
    }
}