using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Streams.Actors;

namespace Akka.Persistence.Query.SqlServer
{
    public static class EventsByTagPublisher
    {
        public static Props Props(string tag, long fromOffset, long toOffset, TimeSpan interval, int maxBufSize, string writeJournalPluginId)
        {
            if (interval == TimeSpan.Zero)
            {
                return Actor.Props.Create(() => new CurrentEventsByTagPublisher(tag, fromOffset, toOffset, maxBufSize, writeJournalPluginId));
            }
            else
            {
                return Actor.Props.Create(() => new LiveEventsByTagPublisher(tag, fromOffset, toOffset, interval, maxBufSize, writeJournalPluginId));
            }
        }
    }

    public abstract class AbstractEventsByTagPublisher : ActorPublisher<EventEnvelope>
    {
        protected string tag;
        protected long fromOffset;
        protected long toOffset;
        protected TimeSpan interval;
        protected int maxBufSize;
        protected long currOffset;

        protected DeliverBuffer<EventEnvelope> _deliveryBuffer;
        private readonly ILoggingAdapter _log = Context.GetLogger();

        protected IActorRef _journal;

        protected AbstractEventsByTagPublisher(string tag, long fromOffset, long toOffset, TimeSpan interval, int maxBufSize, string writeJournalPluginId)
        {
            this.tag = tag;
            this.fromOffset = fromOffset;
            this.toOffset = toOffset;
            this.interval = interval;
            this.maxBufSize = maxBufSize;

            _deliveryBuffer = new DeliverBuffer<EventEnvelope>(this);
            currOffset = fromOffset;
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
            if (message is Continue || message is SqlServerJournal.TaggedEventAppended)
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

        protected bool TimeForReplay => (_deliveryBuffer.IsEmpty || _deliveryBuffer.Size <= maxBufSize / 2) && (currOffset <= toOffset);

        protected void Replay()
        {
            var limit = maxBufSize - _deliveryBuffer.Size;
            _log.Debug("request replay for tag [{0}] from [{1}] to [{2}] limit [{3}]", tag, currOffset, toOffset, limit);
            _journal.Tell(new SqlServerJournal.ReplayTaggedMessages(currOffset, toOffset, limit, tag, Self));
            Context.Become(Replaying(limit));
        }

        private Receive Replaying(int limit)
        {
            return message =>
            {
                if (message is SqlServerJournal.ReplayedTaggedMessage)
                {
                    var replayedMessage = (SqlServerJournal.ReplayedTaggedMessage)message;
                    _deliveryBuffer.Add(new EventEnvelope(
                        replayedMessage.Offset,
                        replayedMessage.Persistent.PersistenceId,
                        replayedMessage.Persistent.SequenceNr,
                        replayedMessage.Persistent.Payload));

                    currOffset = replayedMessage.Persistent.SequenceNr + 1;
                    _deliveryBuffer.Deliver();
                }
                else if (message is RecoverySuccess)
                {
                    var recoverySuccess = (RecoverySuccess)message;
                    _log.Debug("replay completed for tag [{0}], currSeqNo [{1}]", tag, currOffset);
                    ReceiveRecoverySuccess(recoverySuccess.HighestSequenceNr);
                }
                else if (message is ReplayMessagesFailure)
                {
                    var replayMessagesFailure = (ReplayMessagesFailure)message;
                    _log.Debug("replay failed for tag [{0}], due to [{1}]", tag,
                        replayMessagesFailure.Cause.Message);
                    _deliveryBuffer.Deliver();
                    OnErrorThenStop(replayMessagesFailure.Cause);
                }
                else if (message is Continue || message is SqlServerJournal.TaggedEventAppended)
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

    public class LiveEventsByTagPublisher : AbstractEventsByTagPublisher
    {
        private readonly ICancelable _tickTask;

        public LiveEventsByTagPublisher(string tag, long fromOffset, long toOffset, TimeSpan interval, int maxBufSize, string writeJournalPluginId)
            : base(tag, fromOffset, toOffset, interval, maxBufSize, writeJournalPluginId)
        {
            _tickTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(interval, interval, Self, Continue.Instance, Self);
        }

        protected override void PostStop()
        {
            _tickTask.Cancel();
        }

        protected override void ReceiveInitialRequest()
        {
            _journal.Tell(new SqlServerJournal.SubscribeTag(tag));
            Replay();
        }

        protected override void ReceiveIdleRequest()
        {
            _deliveryBuffer.Deliver();
            if (_deliveryBuffer.IsEmpty && currOffset > toOffset)
            {
                OnCompleteThenStop();
            }
        }

        protected override void ReceiveRecoverySuccess(long highestSeqNr)
        {
            _deliveryBuffer.Deliver();
            if (_deliveryBuffer.IsEmpty && currOffset > toOffset)
            {
                OnCompleteThenStop();
            }
            Context.Become(Idle);
        }
    }

    public class CurrentEventsByTagPublisher : AbstractEventsByTagPublisher
    {
        public CurrentEventsByTagPublisher(string tag, long fromOffset, long toOffset, int maxBufSize, string writeJournalPluginId)
            : base(tag, fromOffset, toOffset, TimeSpan.Zero, maxBufSize, writeJournalPluginId)
        {

        }

        protected override void ReceiveInitialRequest()
        {
            Replay();
        }

        protected override void ReceiveIdleRequest()
        {
            _deliveryBuffer.Deliver();
            if (_deliveryBuffer.IsEmpty && currOffset > toOffset)
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
            if (highestSeqNr < toOffset)
            {
                toOffset = highestSeqNr;
            }

            if (_deliveryBuffer.IsEmpty && (currOffset > toOffset || currOffset == fromOffset))
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
