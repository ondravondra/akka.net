using System;
using Akka.Actor;
using Akka.Event;
using Akka.Streams.Actors;

namespace Akka.Persistence.Query.SqlServer
{
    public static class EventsByPersistenceIdPublisher
    {
        public static Props Props(string persistenceId, long fromSequenceNr, long toSequenceNr, TimeSpan refreshInterval, int maxBufSize, string writeJournalPluginId)
        {
            if (refreshInterval == TimeSpan.Zero)
            {
                return Actor.Props.Create(() => new CurrentEventsByPersistenceIdPublisher(persistenceId, fromSequenceNr, toSequenceNr, maxBufSize, writeJournalPluginId));
            }
            else
            {
                return Actor.Props.Create(() => new LiveEventsByPersistenceIdPublisher(persistenceId, fromSequenceNr, toSequenceNr, refreshInterval, maxBufSize, writeJournalPluginId));
            }
        }
    }

    internal abstract class AbstractEventsByPersistenceIdPublisher : ActorPublisher<EventEnvelope>
    {
        protected string persistenceId;
        protected long fromSequenceNr;
        protected long toSequenceNr;
        protected int maxBufSize;
        private readonly ILoggingAdapter _log = Context.GetLogger();

        protected DeliverBuffer<EventEnvelope> _deliveryBuffer;
        protected long curSequenceNo;

        protected IActorRef _journal;

        protected AbstractEventsByPersistenceIdPublisher(string persistenceId, long fromSequenceNr, long toSequenceNr, int maxBufSize, string writeJournalPluginId)
        {
            this.persistenceId = persistenceId;
            this.fromSequenceNr = fromSequenceNr;
            this.toSequenceNr = toSequenceNr;
            this.maxBufSize = maxBufSize;
            _deliveryBuffer = new DeliverBuffer<EventEnvelope>(this);
            curSequenceNo = fromSequenceNr;

            _journal = Persistence.Instance.Apply(Context.System).JournalFor(writeJournalPluginId);
        }

        protected override bool Receive(object message)
        {
            if (message is Continue)
            {
                // skip, wait for first Request
            }
            else if (message is Cancel)
            {
                Context.Stop(Self);
            }
            else
            {
                ReceiveInitialRequest();
            }

            return true;
        }

        protected abstract void ReceiveInitialRequest();

        protected bool Idle(object message)
        {
            if (message is Continue || message is SqlServerJournal.EventAppended)
            {
                if (TimeForReplay)
                {
                    Replay();
                }
            }
            else if (message is Cancel)
            {
                Context.Stop(Self);
            }
            else
            {
                ReceiveIdleRequest();
            }

            return true;
        }

        protected abstract void ReceiveIdleRequest();

        protected bool TimeForReplay => (_deliveryBuffer.IsEmpty || _deliveryBuffer.Size <= maxBufSize / 2) && (curSequenceNo <= toSequenceNr);

        protected void Replay()
        {
            var limit = maxBufSize - _deliveryBuffer.Size;
            _log.Debug("request replay for persistenceId [{0}] from [{1}] to [{2}] limit [{3}]", persistenceId,
                curSequenceNo, toSequenceNr, limit);
            _journal.Tell(new ReplayMessages(fromSequenceNr, toSequenceNr, limit, persistenceId, Self));
            Context.Become(Replaying(limit));
        }

        private Receive Replaying(int limit)
        {
            return message =>
            {
                if (message is ReplayedMessage)
                {
                    var replayedMessage = (ReplayedMessage)message;
                    _deliveryBuffer.Add(new EventEnvelope(
                        replayedMessage.Persistent.SequenceNr,
                        persistenceId,
                        replayedMessage.Persistent.SequenceNr,
                        replayedMessage.Persistent.Payload));

                    curSequenceNo = replayedMessage.Persistent.SequenceNr + 1;
                    _deliveryBuffer.Deliver();
                }
                else if (message is RecoverySuccess)
                {
                    var recoverySuccess = (RecoverySuccess)message;
                    _log.Debug("replay completed for persistenceId [{0}], currSeqNo [{1}]", persistenceId, curSequenceNo);
                    ReceiveRecoverySuccess(recoverySuccess.HighestSequenceNr);
                }
                else if (message is ReplayMessagesFailure)
                {
                    var replayMessagesFailure = (ReplayMessagesFailure) message;
                    _log.Debug("replay failed for persistenceId [{0}], due to [{1}]", persistenceId,
                        replayMessagesFailure.Cause.Message);
                    _deliveryBuffer.Deliver();
                    OnErrorThenStop(replayMessagesFailure.Cause);
                }
                else if (message is Continue || message is SqlServerJournal.EventAppended)
                {
                    // skip during replay
                }
                else if (message is Cancel)
                {
                    Context.Stop(Self);
                }
                else
                {
                    _deliveryBuffer.Deliver();
                }

                return true;
            };
        }

        protected abstract void ReceiveRecoverySuccess(long highestSeqNr);
    }

    internal class LiveEventsByPersistenceIdPublisher : AbstractEventsByPersistenceIdPublisher
    {
        private readonly long _toSequenceNr;
        private readonly ICancelable _tickTask;

        public LiveEventsByPersistenceIdPublisher(string persistenceId, long fromSequenceNr, long toSequenceNr, TimeSpan refreshInterval, int maxBufSize, string writeJournalPluginId) 
            : base(persistenceId, fromSequenceNr, toSequenceNr, maxBufSize, writeJournalPluginId)
        {
            _toSequenceNr = toSequenceNr;
            _tickTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(refreshInterval, refreshInterval, Self, Continue.Instance, Self);
        }

        protected override void PostStop()
        {
            _tickTask.Cancel();
        }

        protected override void ReceiveInitialRequest()
        {
            _journal.Tell(new SqlServerJournal.SubscribePersistenceId(persistenceId));
            Replay();
        }

        protected override void ReceiveIdleRequest()
        {
            _deliveryBuffer.Deliver();
            if (_deliveryBuffer.IsEmpty && curSequenceNo > _toSequenceNr)
            {
                OnCompleteThenStop();
            }
        }

        protected override void ReceiveRecoverySuccess(long highestSeqNr)
        {
            _deliveryBuffer.Deliver();
            if (_deliveryBuffer.IsEmpty && curSequenceNo > _toSequenceNr)
            {
                OnCompleteThenStop();
            }
            Context.Become(Idle);
        }
    }

    internal class CurrentEventsByPersistenceIdPublisher : AbstractEventsByPersistenceIdPublisher
    {
        public CurrentEventsByPersistenceIdPublisher(string persistenceId, long fromSequenceNr, long toSequenceNr, int maxBufSize, string writeJournalPluginId) 
            : base(persistenceId, fromSequenceNr, toSequenceNr, maxBufSize, writeJournalPluginId)
        {

        }

        protected override void ReceiveInitialRequest()
        {
            Replay();
        }

        protected override void ReceiveIdleRequest()
        {
            _deliveryBuffer.Deliver();
            if (_deliveryBuffer.IsEmpty && curSequenceNo > toSequenceNr)
            {
                OnCompleteThenStop();
            }
            else
            {
                Self.Tell(Continue.Instance);
            }
        }

        protected override void ReceiveRecoverySuccess(long highestSeqNr)
        {
            _deliveryBuffer.Deliver();
            if (highestSeqNr < toSequenceNr)
            {
                toSequenceNr = highestSeqNr;
            }

            if (_deliveryBuffer.IsEmpty && (curSequenceNo > toSequenceNr || curSequenceNo == fromSequenceNr))
            {
                OnCompleteThenStop();
            }
            else
            {
                Self.Tell(Continue.Instance);
            }

            Context.Become(Idle);
        }
    }
}
