using System;
using System.Text;
using HOS.ECS.Systems;
using MoonSharp.Interpreter;
using UnityEngine.Pool;
using Context = HOS.Machines.Context;

[MoonSharpUserData]
public class LuaOSLib
{
    public LuaOSLib(Script script)
    {
        this.script = script;
    }
    
    [Zenject.Inject] private Context context;
    private Script script;
    private StringBuilder stringBuilder = new();
    
    public string get_current_folder()
    {
        return context.CurrentComputer.FileSystem.Systems.Get<FilePathSystem>().GetPath(context.CurrentDirectory);
    }

    public Table get_files(string path)
    {
        var table = new Table(script);

        var navSystem = context.CurrentComputer.FileSystem.Systems.Get<FileNavigationSystem>();
        Guid folderGuid = navSystem.ResolvePath(path, context.CurrentDirectory);
        if (folderGuid == Guid.Empty) 
            return null;
    
        var pathSystem = context.CurrentComputer.FileSystem.Systems.Get<FilePathSystem>();
        var listGuid = ListPool<Guid>.Get();
        pathSystem.GetChildren(folderGuid, listGuid);

        var namingSystem = context.CurrentComputer.FileSystem.Systems.Get<FileNamingSystem>();
        var listString = ListPool<string>.Get();
    
        foreach (var fileGuid in listGuid)
        {
            listString.Add(namingSystem.GetFullName(fileGuid));
        }

        foreach (var str in listString)
        {
            table.Append(DynValue.NewString(str));
        }
    
        ListPool<Guid>.Release(listGuid);
        ListPool<string>.Release(listString);

        return table;
    }

    public bool open_folder(string path)
    {
        Guid folderGuid = context.CurrentComputer.FileSystem.Systems.Get<FileNavigationSystem>().ResolvePath(path, context.CurrentDirectory);
        if (folderGuid == Guid.Empty) return false;
        context.OpenDirectory(folderGuid);
        return true;
    }

    public string get_file_content(string path)
    {
        // cat [file]
        // todo
        return string.Empty;
    }

    public int create_folder(string path)
    {
        return (int)context.CurrentComputer.FileSystem.Systems.Get<FileCreationSystem>().CreateDirectoryRecursive(path, context.CurrentDirectory);
    }

    public void create_file(string path)
    {
        // todo
    }

    public int delete(string path)
    {
        return (int)context.CurrentComputer.FileSystem.Systems.Get<FileDeletionSystem>().DeletePath(path, context.CurrentDirectory);
    }
}