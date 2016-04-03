using System.Reactive.Streams;
using Akka.Streams.Dsl;

namespace Akka.Persistence.Query.Dsl
{
    public interface IEventsByPersistenceIdQuery : IReadJournal
    {
        Source<EventEnvelope, Unit> EventsByPersistenceId(string persistenceId, long fromSequenceNr, long toSequenceNr);
    }
}
