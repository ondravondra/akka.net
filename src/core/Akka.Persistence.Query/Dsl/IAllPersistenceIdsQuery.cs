using System.Reactive.Streams;
using Akka.Streams.Dsl;

namespace Akka.Persistence.Query.Dsl
{
    public interface IAllPersistenceIdsQuery : IReadJournal
    {
        Source<string, Unit> AllPersistenceIds();
    }
}
