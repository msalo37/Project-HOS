using System;
using System.Collections.Generic;

namespace HOS.Domain.Common
{
    public interface IDomainEvent
    {
        GameTime OccurredAt { get; }
    }

    public sealed class DomainEventBuffer
    {
        private readonly List<IDomainEvent> events = new List<IDomainEvent>();

        public void Add(IDomainEvent domainEvent)
        {
            if (domainEvent == null)
                throw new ArgumentNullException(nameof(domainEvent));

            events.Add(domainEvent);
        }

        public IReadOnlyList<IDomainEvent> Drain()
        {
            if (events.Count == 0)
                return Array.Empty<IDomainEvent>();

            var result = events.ToArray();
            events.Clear();
            return result;
        }
    }
}
