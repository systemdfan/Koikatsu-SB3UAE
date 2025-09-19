using Config;
using IO;
using UnityPlugin;
using SB3Utility;

namespace Processing
{
    public class Unity3dProcessor
    {
        private readonly IniConfig config;
        private readonly DialogueExtractor extractor;
        private readonly OutputWriter writer;

        public Unity3dProcessor(IniConfig config, DialogueExtractor extractor, OutputWriter writer)
        {
            this.config = config;
            this.extractor = extractor;
            this.writer = writer;
        }

        public void Process(string unity3dPath)
        {
            var parser = UnityPlugin.Plugins.OpenUnity3d(unity3dPath);
            var editor = new Unity3dEditor(parser);
            var assetNames = editor.GetAssetNames(filter: true);

            for (int i = 0; i < parser.Cabinet.Components.Count; i++)
            {
                Console.WriteLine($"Asset: {assetNames[i]}");
                var component = parser.Cabinet.Components[i];
                if (component.classID2 == UnityClassID.MonoBehaviour)
                {
                    var name = assetNames[i];
                    var monobeh = (MonoBehaviour)editor.LoadWhenNeeded(i);
                    var lines = extractor.ExtractDialogueLines(monobeh);
                 
                    if (lines.Count > 0)
                    {
                        writer.WriteDialogueFile(unity3dPath, monobeh.m_Name, lines, config);
                    }
                }
            }
        }
    }
}
