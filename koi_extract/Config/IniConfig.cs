using logging;
namespace Config
{
    public class IniConfig
    {
        private readonly Dictionary<string, string> mappings = new();

        public void Load(string path)
        {
            if (!File.Exists(path)) return;

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.Trim();
                if (trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                    continue;

                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim().ToLower();
                    string value = parts[1].Trim();
                    mappings[key] = value;
                    log.DebugLog($"[DEBUG] INI loaded: '{key}' = '{value}'");
                }
            }
        }

        public string MapName(string characterFolder, string originalTag)
        {
            if (string.IsNullOrEmpty(characterFolder))
                return originalTag;

            string key = characterFolder.ToLower();

            if (mappings.TryGetValue(key, out var value))
                return value;

            return originalTag;
        }
    }
}
