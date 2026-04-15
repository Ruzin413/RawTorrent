using TorServices.CLI;
using TorServices.Core;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Check if user gave any command
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("torrent download <file.torrent>");
            return;
        }

        // 2. Convert raw args into structured command
        var command = CommandParser.Parse(args);

        // 3. Route command to correct handler
        if (command.Action == "download")
        {
            var controller = new TorrentController();
            await controller.StartDownload(command.TargetFile);
        }
        else
        {
            Console.WriteLine("Unknown command");
        }
    }
}