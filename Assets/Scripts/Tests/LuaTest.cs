using UnityEngine;
using MoonSharp;
using HOS.Lua;
using Zenject;

public class LuaTest : MonoBehaviour
{
    [Inject] private HOSLuaVM luaVM;

    private void Start()
    {
        luaVM.RunLua("print(\"Hello!\")");
        luaVM.RunLua("print(\"World!\")");
    }
}