using System;
using System.IO;
using System.Text;
using HOS.ECS;
using HOS.ECS.Component;
using HOS.ECS.Entity;
using HOS.Machines;
using HOS.Utils;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class ECSTest : MonoBehaviour
{
    [Zenject.Inject] private Computer playerComputer;
    [Zenject.Inject] private GlobalVars globalVars;

    private void Start()
    {
        (int, int) stats = playerComputer.FileSystem.ComponentRegistry.GetStats();
        Debug.Log($"Container count: {stats.Item1}");
        Debug.Log($"Component count: {stats.Item2}");

        var guid = globalVars.GetDirectoryGuid(GlobalVars.BasicDirectories.Bin);
        ref var comp = ref playerComputer.FileSystem.ComponentRegistry.GetComponent<NameComponent>(guid);
        Debug.Log(comp.ToString());
        Debug.Log(HOSUtils.GetPath(guid, playerComputer.FileSystem));

        var dirA = new DirectoryEntity();
        playerComputer.FileSystem.EntityContainer.Add(dirA);
        playerComputer.FileSystem.ComponentRegistry.AddComponent(dirA.GUID, new NameComponent("test"));
        playerComputer.FileSystem.ComponentRegistry.AddComponent(dirA.GUID, new DirectoryChildComponent(guid));

        Debug.Log(HOSUtils.GetPath(dirA.GUID, playerComputer.FileSystem));

        var list = ListPool<Guid>.Get();
        HOSUtils.GetFiles(globalVars.GetDirectoryGuid(GlobalVars.BasicDirectories.Main),
            playerComputer.FileSystem,
            list);

        StringBuilder sb = new();
        foreach (var g in list)
        {
            sb.Append("\n");
            sb.Append(HOSUtils.GetFileName(g, playerComputer.FileSystem));
        }
        ListPool<Guid>.Release(list);
        Debug.Log(sb.ToString());

    }
}