using System;
using System.Collections.Generic;
using HOS.Domain.Network;
using HOS.Domain.Services;

namespace HOS.Application.Remote
{
    public sealed class DevSyncServiceHandler : IVirtualServiceHandler
    {
        private readonly Dictionary<ConnectionId, ConnectionState> states =
            new Dictionary<ConnectionId, ConnectionState>();

        public ServiceProtocol Protocol => ServiceProtocol.DevSync;

        public ServiceResponse Handle(VirtualServiceContext context, ServiceRequest request)
        {
            if (!states.TryGetValue(context.Connection.Id, out var state))
            {
                state = new ConnectionState();
                states.Add(context.Connection.Id, state);
            }

            switch (request.Operation)
            {
                case "challenge":
                    state.Nonce = Guid.NewGuid().ToString("N").Substring(0, 12);
                    state.Token = null;
                    return new ServiceResponse(true, "challenge issued")
                        .With("nonce", state.Nonce)
                        .With("algorithm", "legacy-v1");

                case "authenticate":
                    if (state.Nonce == null)
                        return new ServiceResponse(false, "challenge required");
                    if (!StringComparer.Ordinal.Equals(
                            request.Get("proof"),
                            ComputeLegacyProof(state.Nonce)))
                        return new ServiceResponse(false, "invalid proof");
                    state.Token = Guid.NewGuid().ToString("N").Substring(0, 16);
                    return new ServiceResponse(true, "legacy client accepted")
                        .With("token", state.Token);

                case "execute_job":
                    if (state.Token == null ||
                        !StringComparer.Ordinal.Equals(request.Get("token"), state.Token))
                        return new ServiceResponse(false, "authentication required");
                    var payload = request.Get("payload");
                    if (string.IsNullOrWhiteSpace(payload))
                        return new ServiceResponse(false, "payload required");
                    state.Token = null;
                    return new ServiceResponse(true, "debug shell opened")
                        .With("user", context.Service.Owner.ToString())
                        .Grant(context.Service.Owner, payload);

                default:
                    return new ServiceResponse(false, "unknown operation");
            }
        }

        public static string ComputeLegacyProof(string nonce)
        {
            if (nonce == null) return string.Empty;
            var checksum = 0;
            for (var index = 0; index < nonce.Length; index++)
                checksum = (checksum + nonce[index] * (index + 1)) % 65535;
            return checksum.ToString();
        }

        private sealed class ConnectionState
        {
            public string Nonce { get; set; }
            public string Token { get; set; }
        }
    }
}
