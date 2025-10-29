using HOS.Commands;
using HOS.ECS.Component;
using HOS.ECS.Entity;
using HOS.ECS.Meta;
using HOS.Machines;
using UnityEngine;
using Zenject;

namespace HOS.Installer
{
    public class PlayerComputerInitializer : MonoBehaviour
    {
        [Inject] private Computer playerComputer;
        
        private void Awake()
        {
            var scripts = Resources.LoadAll<TextAsset>("DefaultPlayerLua");
            var extComp = new ExtensionComponent("lua");
            
            for (var i = 0; i < scripts.Length; i++)
            {
                var script = scripts[i];

                var fileEntity = new FileEntity();
                var fileGuid = fileEntity.GUID;
                var binGuid = GlobalVars.GetDirectoryGuid(BasicDirectories.Bin);
                playerComputer.FileSystem.Entities.Add(fileEntity);

                var nameComp = new NameComponent(script.name);
                var contentComp = new FileContentComponent(playerComputer.FileSystem.Meta.AddMeta(fileGuid, new StringReference(script.text)));
                var childComp = new DirectoryChildComponent(binGuid);
                
                playerComputer.FileSystem.Components.AddComponent(fileGuid, nameComp);
                playerComputer.FileSystem.Components.AddComponent(fileGuid, contentComp);
                playerComputer.FileSystem.Components.AddComponent(fileGuid, childComp);
                playerComputer.FileSystem.Components.AddComponent(fileGuid, extComp);
            }
        }
    }
}