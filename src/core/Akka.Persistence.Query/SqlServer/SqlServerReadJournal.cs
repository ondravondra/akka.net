using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Streams;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Query.Dsl;
using Akka.Streams.Dsl;

namespace Akka.Persistence.Query.SqlServer
{
    public class SqlServerReadJournal : IReadJournal, 
        IAllPersistenceIdsQuery,
        ICurrentPersistenceIdsQuery,
        IEventsByPersistenceIdQuery,
        ICurrentEventsByPersistenceIdQuery,
        IEventsByTagQuery,
        ICurrentEventsByTagQuery
    {
        public const string Identifier = "akka.persistence.query.journal.sql-server";

        private readonly ExtendedActorSystem _system;
        private readonly Config _config;

        private readonly string _writeJournalPluginId;
        private readonly int _maxBufSize;
        private readonly TimeSpan _refreshInterval;

        public SqlServerReadJournal(ExtendedActorSystem system, Config config)
        {
            _system = system;
            _config = config;

            _writeJournalPluginId = config.GetString("write-plugin");
            _maxBufSize = config.GetInt("max-buffer-size");
            _refreshInterval = config.GetTimeSpan("refresh-interval");
        }

        public Source<string, Unit> AllPersistenceIds()
        {
            var graph = Source.ActorPublisher<string>(AllPersistenceIdsPublisher.Props(true, _maxBufSize, _writeJournalPluginId))
                .MapMaterializedValue(_ => Unit.Instance)
                .Named("AllPersistenceIds");

            return Source.FromGraph(graph);
        }

        public Source<string, Unit> CurrentPersistenceIds()
        {
            var graph = Source.ActorPublisher<string>(AllPersistenceIdsPublisher.Props(false, _maxBufSize, _writeJournalPluginId))
                .MapMaterializedValue(_ => Unit.Instance)
                .Named("CurrentPersistenceIds");

            return Source.FromGraph(graph);
        }

        public Source<EventEnvelope, Unit> EventsByPersistenceId(string persistenceId, long fromSequenceNr = 0, long toSequenceNr = long.MaxValue)
        {
            var graph = Source.ActorPublisher<EventEnvelope>(EventsByPersistenceIdPublisher.Props(persistenceId, fromSequenceNr,
                toSequenceNr, _refreshInterval, _maxBufSize, _writeJournalPluginId))
                .MapMaterializedValue(_ => Unit.Instance)
                .Named("EventsByPersistenceId");

            return Source.FromGraph(graph);
        }

        public Source<EventEnvelope, Unit> CurrentEventsByPersistenceId(string persistenceId, long fromSequenceNr = 0, long toSequenceNr = long.MaxValue)
        {
            var graph = Source.ActorPublisher<EventEnvelope>(EventsByPersistenceIdPublisher.Props(persistenceId, fromSequenceNr,
                toSequenceNr, TimeSpan.Zero, _maxBufSize, _writeJournalPluginId))
                .MapMaterializedValue(_ => Unit.Instance)
                .Named("CurrentEventsByPersistenceId");

            return Source.FromGraph(graph);
        }

        public Source<EventEnvelope, Unit> EventsByTag(string tag, long offset = 0)
        {
            var graph = Source.ActorPublisher<EventEnvelope>(EventsByTagPublisher.Props(tag, offset, long.MaxValue, _refreshInterval,
                _maxBufSize, _writeJournalPluginId))
                .MapMaterializedValue(_ => Unit.Instance)
                .Named("EventsByTag-" + tag);

            return Source.FromGraph(graph);
        }

        public Source<EventEnvelope, Unit> CurrentEventsByTag(string tag, long offset = 0)
        {
            var graph = Source.ActorPublisher<EventEnvelope>(EventsByTagPublisher.Props(tag, offset, long.MaxValue, TimeSpan.Zero,
                _maxBufSize, _writeJournalPluginId))
                .MapMaterializedValue(_ => Unit.Instance)
                .Named("CurrentEventsByTag-" + tag);

            return Source.FromGraph(graph);
        }
    }
}
