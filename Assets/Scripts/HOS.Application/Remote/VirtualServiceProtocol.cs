using System;
using System.Collections.Generic;
using HOS.Domain.Identity;
using HOS.Domain.Machines;
using HOS.Domain.Network;
using HOS.Domain.Services;

namespace HOS.Application.Remote
{
    public sealed class ServiceRequest
    {
        private readonly IReadOnlyDictionary<string, string> fields;

        public ServiceRequest(string operation, IReadOnlyDictionary<string, string> fields = null)
        {
            if (string.IsNullOrWhiteSpace(operation))
                throw new ArgumentException("Operation cannot be empty.", nameof(operation));
            Operation = operation;
            this.fields = fields ?? new Dictionary<string, string>();
        }

        public string Operation { get; }
        public IReadOnlyDictionary<string, string> Fields => fields;
        public string Get(string name) => name != null && fields.TryGetValue(name, out var value) ? value : null;
    }

    public sealed class ServiceResponse
    {
        private readonly Dictionary<string, string> fields =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public ServiceResponse(bool success, string message = "")
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }
        public IReadOnlyDictionary<string, string> Fields => fields;
        public UserId? GrantedUserId { get; private set; }
        public string Payload { get; private set; }

        public ServiceResponse With(string name, string value)
        {
            fields[name] = value ?? string.Empty;
            return this;
        }

        public ServiceResponse Grant(UserId userId, string payload)
        {
            GrantedUserId = userId;
            Payload = payload ?? string.Empty;
            return this;
        }
    }

    public sealed class VirtualServiceContext
    {
        public VirtualServiceContext(
            NetworkConnection connection,
            Machine machine,
            MachineService service)
        {
            Connection = connection;
            Machine = machine;
            Service = service;
        }

        public NetworkConnection Connection { get; }
        public Machine Machine { get; }
        public MachineService Service { get; }
    }

    public interface IVirtualServiceHandler
    {
        ServiceProtocol Protocol { get; }
        ServiceResponse Handle(VirtualServiceContext context, ServiceRequest request);
    }
}
