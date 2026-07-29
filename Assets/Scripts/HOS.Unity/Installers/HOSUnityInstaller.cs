using HOS.Application.Execution;
using HOS.Application.Shell;
using HOS.Scripting;
using HOS.Unity.Bootstrap;
using Zenject;

namespace HOS.Unity.Installers
{
    public sealed class HOSUnityInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            var bootstrap = new DefaultGameBootstrap().Create();

            Container.BindInstance(bootstrap).AsSingle();
            Container.BindInstance(bootstrap.World).AsSingle();
            Container.BindInstance(bootstrap.PlayerMachine).AsSingle();
            Container.BindInstance(bootstrap.Shell).AsSingle();

            Container.Bind<CommandLineParser>().AsSingle();
            Container.Bind<ExecutableResolver>().AsSingle();
            Container.Bind<LuaRuntimeOptions>().AsSingle();
            Container.Bind<IProgramRuntime>().To<LuaProgramRuntime>().AsSingle();
            Container.Bind<ShellEngine>().AsSingle();
        }
    }
}
