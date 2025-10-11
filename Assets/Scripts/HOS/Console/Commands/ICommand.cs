using System;
using Unity.Collections;
using UnityEngine;

namespace HOS.Commands
{
    public interface ICommand
    {
        public void Execute(string[] args);
    }

    public class FunctionCommand : ICommand
    {
        public FunctionCommand(Action<string[]> func)
        {
            this.func = func;
        }

        private Action<string[]> func;

        public void Execute(string[] args)
        {
            func.Invoke(args);
        }
    }

    public interface IFileContent
    {
        public string GetContent();
        public void EditContent(string newVal);
    }

    public struct FileContentComponent : IFileContent
    {
        public FileContentComponent(string val)
        {
            content = new(val);
        }

        public FixedString4096Bytes content;

        public void EditContent(string newVal)
        {
            content = new(newVal);
        }

        public string GetContent()
        {
            return content.ToString();
        }
    }

    public struct FileContentReferenceComponent : IFileContent
    {
        private string resourcePath;

        public void EditContent(string newVal) { }

        public string GetContent()
        {
            return Resources.Load<TextAsset>(resourcePath).text;
        }
    }

    public struct FileContentResourcesReferenceComponent : IFileContent
    {
        public int metaKey; // TODO create MetaDataStorage

        public void EditContent(string newVal)
        {
            // edit
        }

        public string GetContent()
        {
            // get
        }
    }
}