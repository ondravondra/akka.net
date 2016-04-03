using System.Reactive.Streams;
using Akka.Streams.Dsl;

namespace Akka.Persistence.Query.Dsl
{
    interface ICurrentPersistenceIdsQuery
    {
        Source<string, Unit> CurrentPersistenceIds();
    }
}
