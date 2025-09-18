using Config;
using Models;

namespace IO
{
    public class OutputWriter
    {
        private readonly string outputRoot;
        private readonly string abdataRoot;

        public OutputWriter(string outputRoot, string abdataRoot)
        {
            this.outputRoot = outputRoot;
            this.abdataRoot = abdataRoot;
        }

        public void WriteDialogueFile(string unity3dPath, string monoName, List<DialogueLine> lines, IniConfig config)
        {
            string scenarioRoot = Path.Combine(abdataRoot, "adv", "scenario");
            string relativePath = Path.GetRelativePath(scenarioRoot, unity3dPath)
                                        .Replace('\\', '/');

            string[] parts = relativePath.Split('/');
            string characterFolder = parts.Length > 1 ? parts[0] : "Unknown";

            string unity3dName = Path.GetFileNameWithoutExtension(unity3dPath);
            string outDir = Path.Combine(outputRoot, characterFolder, unity3dName);
            Directory.CreateDirectory(outDir);

            string filePath = Path.Combine(outDir, $"{monoName}.txt");

            using var sw = new StreamWriter(filePath);
            foreach (var line in lines)
            {
                string tag = line.Tag?.Trim() ?? "";

                Console.WriteLine($"[DBG] OriginalTag='{tag}', characterFolder='{characterFolder}'");

                if ((tag == "[H名]" ) && config != null)
                {
                    string mapped = config.MapName(characterFolder, tag);
                    Console.WriteLine($"[DBG] Mapped='{mapped}'");

                    if (!string.IsNullOrEmpty(mapped))
                        tag = mapped;
                }

                sw.WriteLine($"{tag}:{line.Text}");
            }

            Console.WriteLine($"[DBG] Wrote {lines.Count} lines -> {filePath}");
        }
    }
}
