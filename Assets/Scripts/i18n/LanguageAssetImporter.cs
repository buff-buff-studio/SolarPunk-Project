
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
            
            //split new lines with carriage return or not
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);    
            foreach (var line in lines)
            {
                if (line.StartsWith("#"))
                    continue;

                var parts = line.Split('=', 2);

                if (parts.Length == 2)
                {
                    language.entries.Add(Language.Hash(parts[0]), parts[1].Replace("\r",""));
                }
            }
            
            if(!language.entries.ContainsKey(Language.Hash("")))
                language.entries.Add(Language.Hash(""), "");

            language.internalName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            language.displayName = language.Localize("name");
            
            ctx.AddObjectToAsset("language", language);
            ctx.SetMainObject(language);
        }
    }
    #endif
}