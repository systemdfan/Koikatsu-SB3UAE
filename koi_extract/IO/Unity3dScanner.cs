namespace IO
{
    public class Unity3dScanner
    {
        public IEnumerable<string> FindUnity3dFiles(string abdataRoot)
        {
            string scenarioPath = Path.Combine(abdataRoot, "adv", "scenario");
            if (!Directory.Exists(scenarioPath))
                yield break;

            foreach (var file in Directory.EnumerateFiles(scenarioPath, "*.unity3d", SearchOption.AllDirectories))
                yield return file;
        }
    }
}
