using System;
using System.Collections.Generic;
using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;

namespace HOS.Domain.Processes
{
    public readonly struct ProcessId : IEquatable<ProcessId>
    {
        public ProcessId(int value) => Value = value;
        public int Value { get; }
        public bool Equals(ProcessId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ProcessId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(ProcessId left, ProcessId right) => left.Equals(right);
        public static bool operator !=(ProcessId left, ProcessId right) => !left.Equals(right);
    }

    public enum ProcessState
    {
        Created,
        Running,
        Sleeping,
        Stopped,
        Exited,
        Failed
    }

    public enum ProcessError
    {
        ProcessNotFound,
        AccessDenied,
        AlreadyTerminated,
        InvalidExecutable
    }

    public sealed class Process
    {
        internal Process(
            ProcessId id,
            ProcessId? parentId,
            UserId owner,
            NodeId executableId,
            string name,
            GameTime startedAt)
        {
            Id = id;
            ParentId = parentId;
            Owner = owner;
            ExecutableId = executableId;
            Name = name;
            StartedAt = startedAt;
            State = ProcessState.Running;
        }

        public ProcessId Id { get; }
        public ProcessId? ParentId { get; }
        public UserId Owner { get; }
        public NodeId ExecutableId { get; }
        public string Name { get; }
        public GameTime StartedAt { get; }
        public ProcessState State { get; internal set; }
        public int? ExitCode { get; internal set; }
    }

    public sealed class ProcessTable
    {
        private readonly Dictionary<ProcessId, Process> processes =
            new Dictionary<ProcessId, Process>();

        private int nextProcessId = 1;

        internal Result<ProcessId, ProcessError> Start(
            NodeId executableId,
            string name,
            UserId owner,
            ProcessId? parentId,
            GameTime time)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<ProcessId, ProcessError>.Failure(
                    ProcessError.InvalidExecutable);
            if (parentId.HasValue && !processes.ContainsKey(parentId.Value))
                return Result<ProcessId, ProcessError>.Failure(
                    ProcessError.ProcessNotFound);

            var id = new ProcessId(nextProcessId++);
            processes.Add(
                id,
                new Process(id, parentId, owner, executableId, name, time));
            return Result<ProcessId, ProcessError>.Success(id);
        }

        public Result<Unit, ProcessError> Exit(
            ProcessId id,
            int exitCode,
            UserId caller,
            bool isKernel = false)
        {
            if (!processes.TryGetValue(id, out var process))
                return Result<Unit, ProcessError>.Failure(ProcessError.ProcessNotFound);
            if (!isKernel && process.Owner != caller)
                return Result<Unit, ProcessError>.Failure(ProcessError.AccessDenied);
            if (process.State == ProcessState.Exited || process.State == ProcessState.Failed)
                return Result<Unit, ProcessError>.Failure(ProcessError.AlreadyTerminated);

            process.ExitCode = exitCode;
            process.State = exitCode == 0 ? ProcessState.Exited : ProcessState.Failed;
            return Result<Unit, ProcessError>.Success(Unit.Value);
        }

        public bool TryGet(ProcessId id, out Process process) =>
            processes.TryGetValue(id, out process);

        public IReadOnlyList<Process> List()
        {
            var result = new List<Process>(processes.Values);
            result.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));
            return result;
        }
    }
}
