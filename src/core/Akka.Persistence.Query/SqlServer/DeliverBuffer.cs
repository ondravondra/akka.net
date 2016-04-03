using System.Collections.Generic;
using System.Linq;
using Akka.Streams.Actors;

namespace Akka.Persistence.Query.SqlServer
{
    public class DeliverBuffer<T>
    {
        private static List<T> _emptyList = new List<T>();

        private readonly ActorPublisher<T> _actorPublisher;
        private List<T> _buffer;

        public DeliverBuffer(ActorPublisher<T> actorPublisher)
        {
            _actorPublisher = actorPublisher;
            _buffer = new List<T>();
        }

        public void Deliver()
        {
            if (_buffer.Count > 0 && _actorPublisher.TotalDemand > 0)
            {
                if (_buffer.Count == 1)
                {
                    _actorPublisher.OnNext(_buffer.First());
                    _buffer = _emptyList;
                }
                else if (_actorPublisher.TotalDemand <= int.MaxValue)
                {
                    var lists = _buffer
                        .Select((x, i) => new { Index = i, Value = x })
                        .GroupBy(x => x.Index >= _actorPublisher.TotalDemand)
                        .Select(x => x.Select(v => v.Value).ToList())
                        .ToList();

                    _buffer = lists[0];
                    foreach (var element in lists[1])
                    {
                        _actorPublisher.OnNext(element);
                    }
                }
                else
                {
                    foreach (var element in _buffer)
                    {
                        _actorPublisher.OnNext(element);
                    }

                    _buffer = _emptyList;
                }
            }
        }

        public bool IsEmpty => _buffer.Count == 0;

        public int Size => _buffer.Count;

        public void Add(T persistenceId)
        {
            _buffer.Add(persistenceId);
        }
    }
}
