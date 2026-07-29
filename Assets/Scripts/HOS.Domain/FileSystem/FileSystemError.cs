namespace HOS.Domain.FileSystem
{
    public enum FileSystemError
    {
        NodeNotFound,
        ParentNotFound,
        NotAFile,
        NotADirectory,
        NameAlreadyExists,
        InvalidName,
        AccessDenied,
        DirectoryNotEmpty,
        CannotDeleteRoot,
        CannotMoveRoot,
        CannotMoveDirectoryIntoItself,
        SymbolicLinkLoop,
        InvalidWorkingDirectory
    }

    public readonly struct DeleteOptions
    {
        public DeleteOptions(bool recursive) => Recursive = recursive;
        public bool Recursive { get; }
        public static DeleteOptions Single => new DeleteOptions(false);
        public static DeleteOptions RecursiveDelete => new DeleteOptions(true);
    }
}
