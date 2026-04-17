using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TorServices.Core;

public class PieceManager : IDisposable
{
    private readonly TorrentMetadata _metadata;
    private readonly string _outputDir;
    private readonly ConcurrentDictionary<int, bool> _claimed = new();
    private readonly ConcurrentDictionary<int, bool> _completed = new();
    private readonly Dictionary<string, FileStream> _fileHandles = new();
    private readonly object _lock = new();

    public PieceManager(TorrentMetadata metadata, string outputDir)
    {
        _metadata = metadata;
        _outputDir = string.IsNullOrWhiteSpace(outputDir) ? Directory.GetCurrentDirectory() : outputDir;
        
        EnsureDirectoriesAndFiles();
        OpenHandles();
    }

    private void EnsureDirectoriesAndFiles()
    {
        try
        {
            foreach (var file in _metadata.Files)
            {
                string fullPath = Path.Combine(_outputDir, file.Path);
                string? dir = Path.GetDirectoryName(fullPath);
                
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (!File.Exists(fullPath))
                {
                    using var fs = File.Create(fullPath);
                    fs.SetLength(file.Length);
                }
                else
                {
                    using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Write);
                    if (fs.Length < file.Length)
                        fs.SetLength(file.Length);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ File initialization error: {ex.Message}");
            throw;
        }
    }

    private void OpenHandles()
    {
        try
        {
            foreach (var file in _metadata.Files)
            {
                string fullPath = Path.Combine(_outputDir, file.Path);
                var fs = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                _fileHandles[file.Path] = fs;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Failed to open file handles: {ex.Message}");
            throw;
        }
    }

    public bool TryClaimPiece(int index)
    {
        if (_completed.ContainsKey(index)) return false;
        return _claimed.TryAdd(index, true);
    }

    public void ReleasePiece(int index)
    {
        _claimed.TryRemove(index, out _);
    }

    public bool IsClaimed(int index) => _claimed.ContainsKey(index);

    public void MarkCompleted(int index)
    {
        _completed.TryAdd(index, true);
        _claimed.TryRemove(index, out _);
    }

    public bool IsPieceCompleted(int index) => _completed.ContainsKey(index);

    public int CompletedCount => _completed.Count;

    public void Store(int index, byte[] data)
    {
        lock (_lock)
        {
            long pieceGlobalOffset = (long)index * _metadata.PieceLength;
            int bytesToProcess = data.Length;

            foreach (var file in _metadata.Files)
            {
                long fileEnd = file.Offset + file.Length;

                if (pieceGlobalOffset < fileEnd && pieceGlobalOffset + bytesToProcess > file.Offset)
                {
                    long writeOffsetInFile = Math.Max(0, pieceGlobalOffset - file.Offset);
                    int readOffsetInData = (int)Math.Max(0, file.Offset - pieceGlobalOffset);
                    int bytesToWrite = (int)Math.Min(
                        file.Length - writeOffsetInFile,
                        bytesToProcess - readOffsetInData
                    );

                    if (_fileHandles.TryGetValue(file.Path, out var fs))
                    {
                        fs.Seek(writeOffsetInFile, SeekOrigin.Begin);
                        fs.Write(data, readOffsetInData, bytesToWrite);
                    }
                }
            }
            MarkCompleted(index);
        }
    }

    public void BuildFile(int totalPieces, string fileName)
    {
        Console.WriteLine($"\n✅ Download progress: {_completed.Count}/{totalPieces} pieces.");
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var handle in _fileHandles.Values)
            {
                handle.Dispose();
            }
            _fileHandles.Clear();
        }
    }
}