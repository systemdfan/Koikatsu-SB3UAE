using System.Text;
using Config;
using IO;
using Processing;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length < 2)
        {
            Console.WriteLine($"Usage: {System.AppDomain.CurrentDomain.FriendlyName} <abdata path> <output path> [config.ini]");
            return;
        }

        string abdataPath = args[0];
        string outputPath = args[1];
        string? iniPath = args.Length > 2 ? args[2] : null;

        if (!Directory.Exists(abdataPath))
        {
            Console.WriteLine($"[ERROR] abdata path not found: {abdataPath}");
            return;
        }

            var config = new IniConfig();
        if (iniPath != null && File.Exists(iniPath))
            config.Load(iniPath);

        var scanner = new Unity3dScanner();
        var files = scanner.FindUnity3dFiles(abdataPath);

        var writer = new OutputWriter(outputPath, abdataPath);
        var processor = new Unity3dProcessor(config, new DialogueExtractor(), writer);


        foreach (var file in files)
        {
            Console.WriteLine($"Opened {file}");
            processor.Process(file);
        }

        Console.WriteLine("Done.");
    }
}
