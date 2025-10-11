using MoonSharp;
using MoonSharp.Interpreter;
using UnityEngine;

namespace HOS.Lua
{
    public class HOSLuaVM
    {
        public HOSLuaVM()
        {
            UserData.RegisterAssembly();
            script = new(CoreModules.Preset_Complete); // https://www.moonsharp.org/sandbox.html
            script.Options.DebugPrint = s => Debug.Log(s);
        }

        private Script script;

        public void RunLua(string luaCode)
        {
            script.DoString(luaCode);
        }
    }
}