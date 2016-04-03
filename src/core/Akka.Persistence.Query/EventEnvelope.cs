namespace Akka.Persistence.Query
{
    public class EventEnvelope
    {
        public EventEnvelope(long offset, string persistenceId, long sequenceNr, object @event)
        {
            Offset = offset;
            PersistenceId = persistenceId;
            SequenceNr = sequenceNr;
            Event = @event;
        }

        public long Offset { get; }

        public string PersistenceId { get; }

        public long SequenceNr { get; }

        public object Event { get; }
    }
}
