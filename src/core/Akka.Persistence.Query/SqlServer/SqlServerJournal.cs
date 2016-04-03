using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Akka.Persistence.Query.SqlServer
{
    public static class SqlServerJournal
    {
        public class SubscriptionCommand
        {
        }


        public class SubscribePersistenceId : SubscriptionCommand
        {
            public string PersistenceId { get; }

            public SubscribePersistenceId(string persistenceId)
            {
                PersistenceId = persistenceId;
            }
        }

        public class EventAppended : DeadLetterSuppression
        {
            public string PersistenceId { get; }

            public EventAppended(string persistenceId)
            {
                PersistenceId = persistenceId;
            }
        }


        public class SubscribeAllPersistenceIds : SubscriptionCommand
        {
        }

        public class CurrentPersistenceIds : DeadLetterSuppression
        {
            public List<string> AllPersistenceIds { get; }

            public CurrentPersistenceIds(List<string> allPersistenceIds)
            {
                AllPersistenceIds = allPersistenceIds;
            }
        }

        public class PersistenceIdAdded : DeadLetterSuppression
        {
            public string PersistenceId { get; }

            public PersistenceIdAdded(string persistenceId)
            {
                PersistenceId = persistenceId;
            }
        }

        public class SubscribeTag : SubscriptionCommand
        {
            public string Tag { get; }

            public SubscribeTag(string tag)
            {
                Tag = tag;
            }
        }

        public class TaggedEventAppended : DeadLetterSuppression
        {
            public string Tag { get; }

            public TaggedEventAppended(string tag)
            {
                Tag = tag;
            }
        }

        public class ReplayTaggedMessages : SubscriptionCommand
        {
            public long FromOffset { get; }
            public long ToOffset { get; }
            public int Limit { get; }
            public string Tag { get; }
            public IActorRef ReplyTo { get; }

            public ReplayTaggedMessages(long fromOffset, long toOffset, int limit, string tag, IActorRef replyTo)
            {
                FromOffset = fromOffset;
                ToOffset = toOffset;
                Limit = limit;
                Tag = tag;
                ReplyTo = replyTo;
            }
        }

        public class ReplayedTaggedMessage : DeadLetterSuppression
        {
            public IPersistentRepresentation Persistent { get; }
            public string Tag { get; }
            public long Offset { get; }

            public ReplayedTaggedMessage(IPersistentRepresentation persistent, string tag, long offset)
            {
                Persistent = persistent;
                Tag = tag;
                Offset = offset;
            }
        }
    }

    public class DeadLetterSuppression
    {
    }

    public class Continue
    {
        private Continue() { }

        public static Continue Instance { get; } = new Continue();
    }
}
