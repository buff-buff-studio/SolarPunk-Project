
#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor.AssetImporters;
#endif

namespace Solis.i18n
{
    #if UNITY_EDITOR
    [ScriptedImporter(1, "lang")]
    public class LanguageAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = File.ReadAllText(ctx.assetPath);
            var language = ScriptableObject.CreateInstance<Language>();
            
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("#"))
                    continue;

                var parts = line.Split('=', 2);

                if (parts.Length == 2)
                    language.entries.Add(Language.Hash(parts[0]), parts[1]);
            }

            language.internalName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            language.displayName = language.Localize("name");
            
            ctx.AddObjectToAsset("language", language);
            ctx.SetMainObject(language);
        }
    }
    #endif
}