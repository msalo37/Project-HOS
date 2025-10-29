using System.IO;
using UnityEngine;
using UnityEditor.AssetImporters;

namespace HOS.Lua.Editor
{
    [ScriptedImporter(1, "lua")]
    public class LuaScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);
            TextAsset textAsset = new TextAsset(text);
            ctx.AddObjectToAsset("lua", textAsset);
            ctx.SetMainObject(textAsset);
        }
    }
}