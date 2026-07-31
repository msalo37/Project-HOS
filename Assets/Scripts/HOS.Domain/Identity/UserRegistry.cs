using System;
using System.Collections.Generic;
using HOS.Domain.Common;

namespace HOS.Domain.Identity
{
    public enum IdentityError
    {
        UserNotFound,
        GroupNotFound,
        UserNameAlreadyExists,
        GroupNameAlreadyExists,
        InvalidName
    }

    public sealed class UserAccount
    {
        public UserAccount(UserId id, string name, GroupId primaryGroup)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            PrimaryGroup = primaryGroup;
        }

        public UserId Id { get; }
        public string Name { get; }
        public GroupId PrimaryGroup { get; }
    }

    public sealed class UserGroup
    {
        private readonly HashSet<UserId> members = new HashSet<UserId>();

        public UserGroup(GroupId id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public GroupId Id { get; }
        public string Name { get; }
        public IReadOnlyCollection<UserId> Members => members;
        internal void Add(UserId userId) => members.Add(userId);
        internal bool Contains(UserId userId) => members.Contains(userId);
    }

    public sealed class UserRegistry
    {
        private readonly Dictionary<UserId, UserAccount> users = new Dictionary<UserId, UserAccount>();
        private readonly Dictionary<GroupId, UserGroup> groups = new Dictionary<GroupId, UserGroup>();
        private readonly Dictionary<string, UserId> usersByName =
            new Dictionary<string, UserId>(StringComparer.Ordinal);
        private readonly Dictionary<string, GroupId> groupsByName =
            new Dictionary<string, GroupId>(StringComparer.Ordinal);

        public Result<GroupId, IdentityError> CreateGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<GroupId, IdentityError>.Failure(IdentityError.InvalidName);
            if (groupsByName.ContainsKey(name))
                return Result<GroupId, IdentityError>.Failure(IdentityError.GroupNameAlreadyExists);

            var id = GroupId.New();
            groups.Add(id, new UserGroup(id, name));
            groupsByName.Add(name, id);
            return Result<GroupId, IdentityError>.Success(id);
        }

        public Result<UserId, IdentityError> CreateUser(string name, GroupId primaryGroup)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<UserId, IdentityError>.Failure(IdentityError.InvalidName);
            if (!groups.TryGetValue(primaryGroup, out var group))
                return Result<UserId, IdentityError>.Failure(IdentityError.GroupNotFound);
            if (usersByName.ContainsKey(name))
                return Result<UserId, IdentityError>.Failure(IdentityError.UserNameAlreadyExists);

            var id = UserId.New();
            users.Add(id, new UserAccount(id, name, primaryGroup));
            usersByName.Add(name, id);
            group.Add(id);
            return Result<UserId, IdentityError>.Success(id);
        }

        public Result<Unit, IdentityError> AddToGroup(UserId userId, GroupId groupId)
        {
            if (!users.ContainsKey(userId))
                return Result<Unit, IdentityError>.Failure(IdentityError.UserNotFound);
            if (!groups.TryGetValue(groupId, out var group))
                return Result<Unit, IdentityError>.Failure(IdentityError.GroupNotFound);

            group.Add(userId);
            return Result<Unit, IdentityError>.Success(Unit.Value);
        }

        public bool TryGetUser(UserId id, out UserAccount user) => users.TryGetValue(id, out user);
        public bool TryGetUser(string name, out UserAccount user)
        {
            if (name != null && usersByName.TryGetValue(name, out var id))
                return users.TryGetValue(id, out user);
            user = null;
            return false;
        }
        public bool TryGetGroup(GroupId id, out UserGroup group) => groups.TryGetValue(id, out group);

        public AccessContext CreateAccessContext(UserId userId, bool isKernel = false)
        {
            if (!users.ContainsKey(userId))
                throw new KeyNotFoundException($"User {userId} does not exist.");

            var memberships = new List<GroupId>();
            foreach (var pair in groups)
            {
                if (pair.Value.Contains(userId))
                    memberships.Add(pair.Key);
            }

            return new AccessContext(userId, memberships, isKernel);
        }
    }
}
