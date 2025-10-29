using UnityEngine;
using MoonSharp;
using HOS.Lua;
using Zenject;

public class LuaTest : MonoBehaviour
{
    [Inject] private HOSLuaVM luaVM;
    [SerializeField] private TextAsset textAsset;

    private void Start()
    {
        var file = Resources.Load<TextAsset>("DefaultPlayerLua/test");
        string lua = file.ToString();
        luaVM.RunLua(lua, "Hello", "World!");
    }
}