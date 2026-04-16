using TorServices.CLI;
using TorServices.Core;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Check if user gave any command
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        // 2. Convert raw args into structured command
        var command = CommandParser.Parse(args);
        if (command == null || string.IsNullOrEmpty(command.TargetFile))
        {
            PrintUsage();
            return;
        }

        // 3. Route command to correct handler
        if (command.Action == CommandAction.Download)
        {
            var controller = new TorrentController();
            
            if (command.TargetFile.StartsWith("magnet:?"))
            {
                await controller.StartMagnetDownload(command.TargetFile);
            }
            else
            {
                await controller.StartDownload(command.TargetFile);
            }
        }
        else
        {
            Console.WriteLine($"Unknown action: {args[0]}");
            PrintUsage();
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  torrent download <file.torrent>");
        Console.WriteLine("  torrent download \"magnet:?xt=urn:...\"");
        Console.WriteLine("\nOptions:");
        Console.WriteLine("  -o, --output <dir>    Specify output directory");
        Console.WriteLine("  -v, --verbose         Enable verbose logging");
    }
}