using HOS.Commands;
using HOS.Console;
using Zenject;
using HOS.Machines;
using HOS.Lua;
using Context = HOS.Machines.Context;

namespace HOS.Installer
{
    public class HOSInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<GlobalVars>().AsSingle();
            Container.Bind<Computer>().AsSingle(); // player computer
            Container.Bind<PlayerConsole>().AsSingle();
            Container.Bind<CommandController>().AsSingle();
            Container.Bind<ConsoleInput>().AsSingle();
            Container.Bind<Context>().AsSingle();
            Container.Bind<HOSLuaVM>().AsSingle();
        }
    }
}