using System;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;

namespace HOS.Unity.Bootstrap
{
    public sealed class GameBootstrapResult
    {
        public GameBootstrapResult(
            GameWorld world,
            Machine playerMachine,
            Machine developmentServer,
            PlayerShellContext shell,
            UserId playerUserId,
            NodeId binDirectoryId,
            NodeId playerHomeDirectoryId)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            PlayerMachine = playerMachine ?? throw new ArgumentNullException(nameof(playerMachine));
            DevelopmentServer = developmentServer ?? throw new ArgumentNullException(nameof(developmentServer));
            Shell = shell ?? throw new ArgumentNullException(nameof(shell));
            PlayerUserId = playerUserId;
            BinDirectoryId = binDirectoryId;
            PlayerHomeDirectoryId = playerHomeDirectoryId;
        }

        public GameWorld World { get; }
        public Machine PlayerMachine { get; }
        public Machine DevelopmentServer { get; }
        public PlayerShellContext Shell { get; }
        public UserId PlayerUserId { get; }
        public NodeId BinDirectoryId { get; }
        public NodeId PlayerHomeDirectoryId { get; }
    }
}
