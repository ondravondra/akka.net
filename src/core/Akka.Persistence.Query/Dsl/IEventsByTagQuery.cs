using Akka.Streams.Dsl;
using System.Reactive.Streams;

namespace Akka.Persistence.Query.Dsl
{
    public interface IEventsByTagQuery: IReadJournal
    {
        Source<EventEnvelope, Unit> EventsByTag(string tag, long offset);
    }
}
