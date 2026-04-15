namespace TorServices.CLI;

public class Command
{
    public string Action { get; set; }
    public string TargetFile { get; set; }
}

public static class CommandParser
{
    public static Command Parse(string[] args)
    {
        return new Command
        {
            Action = args[0],
            TargetFile = args.Length > 1 ? args[1] : null
        };
    }
}