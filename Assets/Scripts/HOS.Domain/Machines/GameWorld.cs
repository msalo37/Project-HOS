using System;
using System.Collections.Generic;
using HOS.Domain.Common;
using HOS.Domain.Network;

namespace HOS.Domain.Machines
{
    public enum WorldError
    {
        MachineNotFound,
        MachineAlreadyExists,
        AddressAlreadyRegistered
    }

    public sealed class GameWorld
    {
        private readonly Dictionary<MachineId, Machine> machines =
            new Dictionary<MachineId, Machine>();

        public GameWorld()
        {
            Network = new VirtualNetwork();
        }

        public GameTime Time { get; private set; }
        public VirtualNetwork Network { get; }
        public IReadOnlyCollection<Machine> Machines => machines.Values;

        public Result<Unit, WorldError> AddMachine(Machine machine)
        {
            if (machine == null)
                throw new ArgumentNullException(nameof(machine));
            if (machines.ContainsKey(machine.Id))
                return Result<Unit, WorldError>.Failure(WorldError.MachineAlreadyExists);

            var networkResult = Network.Register(machine);
            if (networkResult.IsFailure)
                return Result<Unit, WorldError>.Failure(WorldError.AddressAlreadyRegistered);

            machines.Add(machine.Id, machine);
            return Result<Unit, WorldError>.Success(Unit.Value);
        }

        public bool TryGetMachine(MachineId id, out Machine machine) =>
            machines.TryGetValue(id, out machine);

        public void Advance(GameDuration duration)
        {
            Time = Time.Advance(duration);
            foreach (var machine in machines.Values)
                machine.FileSystem.AdvanceTime(duration);
        }
    }
}
