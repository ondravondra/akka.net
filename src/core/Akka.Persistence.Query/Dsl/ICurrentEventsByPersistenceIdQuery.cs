using System.Reactive.Streams;
using Akka.Streams.Dsl;

namespace Akka.Persistence.Query.Dsl
{
    public interface ICurrentEventsByPersistenceIdQuery : IReadJournal
    {
        Source<EventEnvelope, Unit> CurrentEventsByPersistenceId(string persistenceId, long fromSequenceNr, long toSequenceNr);
    }
}
