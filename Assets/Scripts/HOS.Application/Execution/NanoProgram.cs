using System;
using System.Collections.Generic;
using System.Text;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;

namespace HOS.Application.Execution
{
    public sealed class NanoProgram : IBuiltinProgram
    {
        public const string ProgramId = "nano";

        public string Id => ProgramId;

        public ProgramStartResult Start(
            ProgramExecutionContext context,
            IReadOnlyList<string> arguments)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (arguments == null || arguments.Count != 1)
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(2, "Usage: nano <file>"));
            }

            var path = arguments[0];
            if (string.IsNullOrWhiteSpace(path))
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(2, "nano: invalid file path"));
            }
            if (path.EndsWith(HosBinaryFormat.Extension, StringComparison.OrdinalIgnoreCase))
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(1, $"nano: {path}: binary file is not supported"));
            }

            var parsed = VirtualPath.Parse(path);
            if (parsed.IsFailure)
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(2, $"nano: {path}: invalid file path"));
            }

            var machine = context.TargetMachine;
            var shell = context.Shell;
            var access = machine.Users.CreateAccessContext(shell.CurrentUserId);
            var resolved = machine.FileSystem.Resolve(
                shell.WorkingDirectory,
                parsed.Value,
                access);

            if (resolved.IsSuccess)
            {
                var entry = machine.FileSystem.Stat(resolved.Value);
                if (entry.IsFailure || entry.Value.Type != FileNodeType.RegularFile)
                {
                    return ProgramStartResult.Completed(
                        CommandResult.Failure(1, $"nano: {path}: not a file"));
                }

                var content = machine.FileSystem.ReadFile(resolved.Value, access);
                if (content.IsFailure)
                {
                    return ProgramStartResult.Completed(
                        CommandResult.Failure(
                            1,
                            $"nano: {path}: {ShellEngine.FormatFileSystemError(content.Error)}"));
                }
                if (HosBinaryFormat.IsBinary(content.Value))
                {
                    return ProgramStartResult.Completed(
                        CommandResult.Failure(1, $"nano: {path}: binary file is not supported"));
                }

                return ProgramStartResult.Running(
                    new NanoSession(
                        machine.FileSystem,
                        access,
                        path,
                        content.Value.ReadUtf8(),
                        resolved.Value,
                        default,
                        null,
                        default));
            }

            if (resolved.Error != FileSystemError.NodeNotFound)
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(
                        1,
                        $"nano: {path}: {ShellEngine.FormatFileSystemError(resolved.Error)}"));
            }

            if (!TryResolveNewFile(
                    machine.FileSystem,
                    shell.WorkingDirectory,
                    path,
                    access,
                    out var parentId,
                    out var name,
                    out var error))
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(1, $"nano: {path}: {error}"));
            }

            if (!machine.Users.TryGetUser(shell.CurrentUserId, out var user))
            {
                return ProgramStartResult.Completed(
                    CommandResult.Failure(1, "nano: current user is unavailable"));
            }

            return ProgramStartResult.Running(
                new NanoSession(
                    machine.FileSystem,
                    access,
                    path,
                    string.Empty,
                    null,
                    parentId,
                    name,
                    new FileOwnership(user.Id, user.PrimaryGroup)));
        }

        private static bool TryResolveNewFile(
            VirtualFileSystem fileSystem,
            NodeId workingDirectory,
            string path,
            AccessContext access,
            out NodeId parentId,
            out string name,
            out string error)
        {
            parentId = default;
            name = null;
            error = "invalid file path";

            var trimmed = path.TrimEnd('/');
            var separator = trimmed.LastIndexOf('/');
            var parentPath = separator < 0
                ? "."
                : separator == 0 ? "/" : trimmed.Substring(0, separator);
            name = separator < 0 ? trimmed : trimmed.Substring(separator + 1);
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var parsedParent = VirtualPath.Parse(parentPath);
            if (parsedParent.IsFailure)
                return false;
            var resolvedParent = fileSystem.Resolve(workingDirectory, parsedParent.Value, access);
            if (resolvedParent.IsFailure)
            {
                error = ShellEngine.FormatFileSystemError(resolvedParent.Error);
                return false;
            }

            parentId = resolvedParent.Value;
            return true;
        }

        private sealed class NanoSession : IProgramSession
        {
            private const int VisibleRows = 14;

            private readonly VirtualFileSystem fileSystem;
            private readonly AccessContext access;
            private readonly string path;
            private readonly StringBuilder buffer;
            private readonly NodeId destinationParentId;
            private readonly string destinationName;
            private readonly FileOwnership newFileOwnership;

            private NodeId? fileId;
            private int caretIndex;
            private int preferredColumn = -1;
            private int firstVisibleLine;
            private bool dirty;
            private bool discardArmed;
            private string status;

            public NanoSession(
                VirtualFileSystem fileSystem,
                AccessContext access,
                string path,
                string content,
                NodeId? fileId,
                NodeId destinationParentId,
                string destinationName,
                FileOwnership newFileOwnership)
            {
                this.fileSystem = fileSystem;
                this.access = access;
                this.path = path;
                this.fileId = fileId;
                this.destinationParentId = destinationParentId;
                this.destinationName = destinationName;
                this.newFileOwnership = newFileOwnership;
                buffer = new StringBuilder(content ?? string.Empty);
                status = fileId.HasValue ? "Ready" : "New file";
            }

            public TerminalFrame CurrentFrame => BuildFrame();

            public ProgramSessionUpdate HandleInput(ProgramInput input)
            {
                if (input.Control && input.Key == ProgramInputKey.O)
                {
                    Save();
                    return ProgramSessionUpdate.Running();
                }
                if (input.Control && input.Key == ProgramInputKey.X)
                {
                    if (!dirty || discardArmed)
                        return ProgramSessionUpdate.Exited(CommandResult.Success());

                    discardArmed = true;
                    status = "Unsaved changes: Ctrl+O save, Ctrl+X discard";
                    return ProgramSessionUpdate.Running();
                }

                switch (input.Key)
                {
                    case ProgramInputKey.Character:
                        if (!char.IsControl(input.Character))
                        {
                            buffer.Insert(caretIndex++, input.Character);
                            MarkChanged();
                        }
                        break;
                    case ProgramInputKey.Enter:
                        buffer.Insert(caretIndex++, '\n');
                        MarkChanged();
                        break;
                    case ProgramInputKey.Backspace:
                        if (caretIndex > 0)
                        {
                            buffer.Remove(caretIndex - 1, 1);
                            caretIndex--;
                            MarkChanged();
                        }
                        break;
                    case ProgramInputKey.Delete:
                        if (caretIndex < buffer.Length)
                        {
                            buffer.Remove(caretIndex, 1);
                            MarkChanged();
                        }
                        break;
                    case ProgramInputKey.LeftArrow:
                        if (caretIndex > 0)
                            caretIndex--;
                        preferredColumn = -1;
                        break;
                    case ProgramInputKey.RightArrow:
                        if (caretIndex < buffer.Length)
                            caretIndex++;
                        preferredColumn = -1;
                        break;
                    case ProgramInputKey.UpArrow:
                        MoveVertical(-1);
                        break;
                    case ProgramInputKey.DownArrow:
                        MoveVertical(1);
                        break;
                    case ProgramInputKey.Home:
                        caretIndex = FindLineStart(caretIndex);
                        preferredColumn = -1;
                        break;
                    case ProgramInputKey.End:
                        caretIndex = FindLineEnd(caretIndex);
                        preferredColumn = -1;
                        break;
                }

                return ProgramSessionUpdate.Running();
            }

            public ProgramSessionUpdate HandleSignal(ProgramSignal signal)
            {
                var exitCode = signal == ProgramSignal.Interrupt ? 130 : 143;
                var name = signal == ProgramSignal.Interrupt ? "interrupted" : "terminated";
                return ProgramSessionUpdate.Exited(
                    CommandResult.Failure(exitCode, "nano: " + name));
            }

            private void Save()
            {
                var content = FileContent.FromUtf8(buffer.ToString());
                if (fileId.HasValue)
                {
                    var written = fileSystem.WriteFile(fileId.Value, content, access);
                    if (written.IsFailure)
                    {
                        status = "Save failed: " + ShellEngine.FormatFileSystemError(written.Error);
                        return;
                    }
                }
                else
                {
                    var created = fileSystem.CreateFile(
                        destinationParentId,
                        destinationName,
                        newFileOwnership,
                        FilePermissions.DefaultFile,
                        content,
                        access);
                    if (created.IsFailure)
                    {
                        status = "Save failed: " + ShellEngine.FormatFileSystemError(created.Error);
                        return;
                    }
                    fileId = created.Value;
                }

                dirty = false;
                discardArmed = false;
                status = "Wrote " + buffer.Length + " characters";
            }

            private void MarkChanged()
            {
                dirty = true;
                discardArmed = false;
                preferredColumn = -1;
                status = "Modified";
            }

            private void MoveVertical(int direction)
            {
                var currentStart = FindLineStart(caretIndex);
                var column = caretIndex - currentStart;
                if (preferredColumn < 0)
                    preferredColumn = column;

                if (direction < 0)
                {
                    if (currentStart == 0)
                        return;
                    var targetStart = FindLineStart(currentStart - 1);
                    caretIndex = Math.Min(targetStart + preferredColumn, FindLineEnd(targetStart));
                }
                else
                {
                    var currentEnd = FindLineEnd(caretIndex);
                    if (currentEnd == buffer.Length)
                        return;
                    var targetStart = currentEnd + 1;
                    caretIndex = Math.Min(targetStart + preferredColumn, FindLineEnd(targetStart));
                }
            }

            private int FindLineStart(int index)
            {
                for (var position = Math.Min(index, buffer.Length) - 1; position >= 0; position--)
                {
                    if (buffer[position] == '\n')
                        return position + 1;
                }
                return 0;
            }

            private int FindLineEnd(int index)
            {
                for (var position = Math.Max(0, index); position < buffer.Length; position++)
                {
                    if (buffer[position] == '\n')
                        return position;
                }
                return buffer.Length;
            }

            private TerminalFrame BuildFrame()
            {
                var lineStarts = CollectLineStarts();
                var cursorLine = FindCursorLine(lineStarts);
                if (cursorLine < firstVisibleLine)
                    firstVisibleLine = cursorLine;
                else if (cursorLine >= firstVisibleLine + VisibleRows)
                    firstVisibleLine = cursorLine - VisibleRows + 1;

                var frame = new StringBuilder();
                frame.Append("HOS nano  ").Append(path);
                if (dirty)
                    frame.Append(" *");
                frame.Append('\n');

                var frameCaret = frame.Length;
                for (var row = 0; row < VisibleRows; row++)
                {
                    var lineIndex = firstVisibleLine + row;
                    if (lineIndex < lineStarts.Count)
                    {
                        var start = lineStarts[lineIndex];
                        var end = FindLineEnd(start);
                        if (lineIndex == cursorLine)
                            frameCaret = frame.Length + Math.Min(caretIndex - start, end - start);
                        frame.Append(buffer.ToString(start, end - start));
                    }
                    frame.Append('\n');
                }

                frame.Append(status ?? string.Empty).Append('\n');
                frame.Append("^O Save    ^X Exit    ^C Interrupt");
                return new TerminalFrame(frame.ToString(), frameCaret);
            }

            private List<int> CollectLineStarts()
            {
                var result = new List<int> { 0 };
                for (var index = 0; index < buffer.Length; index++)
                {
                    if (buffer[index] == '\n')
                        result.Add(index + 1);
                }
                return result;
            }

            private int FindCursorLine(IReadOnlyList<int> lineStarts)
            {
                for (var index = lineStarts.Count - 1; index >= 0; index--)
                {
                    if (caretIndex >= lineStarts[index])
                        return index;
                }
                return 0;
            }
        }
    }
}
