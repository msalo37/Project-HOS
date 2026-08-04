using HOS.Application.Execution;
using HOS.Application.Shell;
using HOS.Scripting;
using HOS.Unity.Bootstrap;
using HOS.Application.Remote;
using Zenject;

namespace HOS.Unity.Installers
{
    public sealed class HOSUnityInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            var bootstrap = new DefaultGameBootstrap().Create();
            var luaOptions = new LuaRuntimeOptions();
            var builtinPrograms = new BuiltinProgramRegistry(
                new IBuiltinProgram[]
                {
                    new NanoProgram(),
                    new SystemInfoProgram()
                });
            var runtime = new ProgramRuntimeRouter(
                new LuaProgramRuntime(luaOptions),
                new BinaryProgramRuntime(builtinPrograms));

            Container.BindInstance(bootstrap).AsSingle();
            Container.BindInstance(bootstrap.World).AsSingle();
            Container.BindInstance(bootstrap.PlayerMachine).AsSingle();
            Container.BindInstance(bootstrap.Shell).AsSingle();

            Container.Bind<CommandLineParser>().AsSingle();
            Container.Bind<ExecutableResolver>().AsSingle();
            Container.Bind<RemoteAccessService>().AsSingle();
            Container.BindInstance(luaOptions).AsSingle();
            Container.BindInstance(builtinPrograms).AsSingle();
            Container.BindInstance<IProgramRuntime>(runtime).AsSingle();
            Container.Bind<ShellEngine>().AsSingle();
        }
    }
}
