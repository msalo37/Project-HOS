using HOS.Console;
using MoonSharp.Interpreter;
using Zenject;

namespace HOS.Lua
{
    public class HOSLuaVM
    {
        public HOSLuaVM(PlayerConsole console, DiContainer container)
        {
            UserData.RegisterAssembly();
            script = new(CoreModules.Preset_Complete); // https://www.moonsharp.org/sandbox.html
            script.Options.DebugPrint = console.AddLine;

            var osLib = new LuaOSLib(script);
            container.Inject(osLib);
            
            script.Globals["os"] = osLib;
            
            table = new Table(script);
        }

        private Script script;
        private Table table;
        
        public int RunLua(string luaCode, params string[] args)
        {
            table.Clear();
            if (args != null && args.Length > 0)
            {
                foreach (var arg in args)
                {
                    table.Append(DynValue.NewString(arg));
                }
            }
            
            script.DoString(luaCode);
            
            DynValue luaFunction = script.Globals.Get("execute");
            DynValue exitCode = script.Call(luaFunction, table.Length > 0 ? table : null);
            
            return exitCode.ToObject<int>();
        }
    }
}