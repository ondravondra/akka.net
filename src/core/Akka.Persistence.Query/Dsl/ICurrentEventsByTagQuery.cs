using Akka.Streams.Dsl;
using System.Reactive.Streams;

namespace Akka.Persistence.Query.Dsl
{
    public interface ICurrentEventsByTagQuery : IReadJournal
    {
        Source<EventEnvelope, Unit> CurrentEventsByTag(string tag, long offset);
    }
}
