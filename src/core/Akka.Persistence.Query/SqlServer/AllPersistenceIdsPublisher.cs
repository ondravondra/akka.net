using Akka.Actor;
using Akka.Event;
using Akka.Streams.Actors;

namespace Akka.Persistence.Query.SqlServer
{
    internal class AllPersistenceIdsPublisher : ActorPublisher<string>
    {
        private readonly bool _liveQuery;
        private readonly int _maxBufSize;
        private readonly string _writeJournalPluginId;
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly DeliverBuffer<string> _deliveryBuffer;

        // TODO: fix this
        private IActorRef _journal = null;

        public AllPersistenceIdsPublisher(bool liveQuery, int maxBufSize, string writeJournalPluginId)
        {
            _liveQuery = liveQuery;
            _maxBufSize = maxBufSize;
            _writeJournalPluginId = writeJournalPluginId;
            _deliveryBuffer = new DeliverBuffer<string>(this);
        }

        protected override bool Receive(object message)
        {
            if (message is Cancel)
            {
                Context.Stop(Self);
            }
            else
            {
                _journal.Tell(new SqlServerJournal.SubscribeAllPersistenceIds());
                Become(Active);
            }

            return true;
        }

        private bool Active(object message)
        {
            if (message is SqlServerJournal.CurrentPersistenceIds)
            {
                var allPersistenceIds = ((SqlServerJournal.CurrentPersistenceIds)message).AllPersistenceIds;

                foreach (var persistenceId in allPersistenceIds)
                {
                    _deliveryBuffer.Add(persistenceId);
                }
                _deliveryBuffer.Deliver();

                if (!_liveQuery && _deliveryBuffer.IsEmpty)
                {
                    OnCompleteThenStop();
                }
            }
            else if (message is SqlServerJournal.PersistenceIdAdded)
            {
                if (_liveQuery)
                {
                    _deliveryBuffer.Add(((SqlServerJournal.PersistenceIdAdded)message).PersistenceId);
                    _deliveryBuffer.Deliver();
                }
            }
            else if (message is Cancel)
            {
                Context.Stop(Self);
            }
            else
            {
                _deliveryBuffer.Deliver();
                if (!_liveQuery && _deliveryBuffer.IsEmpty)
                {
                    OnCompleteThenStop();
                }
            }

            return true;
        }

        public static Props Props(bool liveQuery, int maxBufSize, string writeJournalPluginId)
        {
            return Actor.Props.Create(() => new AllPersistenceIdsPublisher(liveQuery, maxBufSize, writeJournalPluginId));
        }
    }
}
