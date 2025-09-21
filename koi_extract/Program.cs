using System.Diagnostics;
using System.Text;
using Config;
using IO;
using Processing;
using SB3Utility;
using UnityPlugin;

class Program
{
    public static bool IsDebugMode = false;
    public static bool IsExtractinglanguage = false;
    public static string Language;
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length < 2)
        {
            Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} <abdata path> <output path> [config.ini] [--lang=] [--debug]\n\t--lang - extract specified language text (works only with kk party(doesn't work with most of charachters))\n\t\ten-US\n\tcn-TW\n\tcn-CN\n\t--debug - debug log");
            return;
        }

        // Парсим флаги
        IsDebugMode = args.Contains("--debug");
        if (IsDebugMode)
            Console.WriteLine("DEBUG MODE: Включен расширенный вывод отладки");

        // Парсим --lang=
        string? langCode = null;
        var langArg = args.FirstOrDefault(a => a.StartsWith("--lang="));
        if (langArg != null)
        {
            langCode = langArg.Substring("--lang=".Length);
            var validLanguages = new[] { "en-US", "cn-TW", "cn-CN" };
            if (!validLanguages.Contains(langCode))
            {
                Console.WriteLine($"[ERROR] Invalid language code: {langCode}. Valid codes: {string.Join(", ", validLanguages)}");
                return;
            }
            Language = langCode;
            Console.WriteLine($"Language mode: {langCode}");
        }

        // Удаляем флаги из массива аргументов
        args = args.Where(a => !a.StartsWith("--")).ToArray();

        string abdataPath = NormalizePath(args[0]);
        string outputPath = NormalizePath(args[1]);
        string? iniPath = args.Length > 2 ? NormalizePath(args[2]) : null;

        if (!Directory.Exists(abdataPath))
        {
            Console.WriteLine($"[ERROR] abdata path not found: {abdataPath}");
            return;
        }


        Directory.CreateDirectory(outputPath); 

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

    static string NormalizePath(string path)
    {
        return Path.GetFullPath(path
            .Trim('\'', '"')
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
