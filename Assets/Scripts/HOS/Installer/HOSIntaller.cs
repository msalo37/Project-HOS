using Zenject;
using HOS.Machines;
using HOS.Lua;

namespace HOS.Installer
{
    public class HOSInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<HOSLuaVM>().AsSingle();
            Container.Bind<GlobalVars>().AsSingle();
            Container.Bind<Computer>().AsSingle(); // player computer
        }
    }
}