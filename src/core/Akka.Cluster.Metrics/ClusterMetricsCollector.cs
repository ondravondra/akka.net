//-----------------------------------------------------------------------
// <copyright file="ClusterMetricsCollector.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Util;
using Akka.Util.Internal;

namespace Akka.Cluster.Metrics
{
    /// <summary>
    /// Runtime collection management commands.
    /// </summary>
    public interface ICollectionControlMessage { }

    /// <summary>
    /// Command for <see cref="ClusterMetricsSupervisor"/> to start metrics collection.
    /// </summary>
    public class CollectionStartMessage : ICollectionControlMessage
    {
        public static CollectionStartMessage Instance = new CollectionStartMessage();
        private CollectionStartMessage() { }
    }

    /// <summary>
    /// Command for <see cref="ClusterMetricsSupervisor"/> to stop metrics collection.
    /// </summary>
    public class CollectionStopMessage : ICollectionControlMessage
    {
        public static CollectionStopMessage Instance = new CollectionStopMessage();
        private CollectionStopMessage() { }
    }

    internal class ClusterMetricsSupervisor : UntypedActor
    {
        private ClusterMetricsExtension metrics;
        private ILoggingAdapter log;
        private int collectorInstance;
        private string collectorName;

        public ClusterMetricsSupervisor()
        {
            metrics = new ClusterMetricsExtension(Context.System.AsInstanceOf<ExtendedActorSystem>());
            log = Context.GetLogger();
            collectorInstance = 0;
            collectorName = $"collector-{collectorInstance}";
        }

        protected override void PreStart()
        {
            if (metrics.Settings.CollectorEnabled)
                Self.Tell(CollectionStartMessage.Instance);
            else
                log.Warning($"Metrics collection is disabled in configuration. Use subtypes of {typeof(ICollectionControlMessage).Name}");
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return metrics.Strategy;
        }

        protected override void OnReceive(object message)
        {
            if (message is CollectionStartMessage)
            {
                Context.GetChildren().ForEach(c => Context.Stop(c));
                collectorInstance += 1;
                Context.ActorOf<ClusterMetricsCollector>(collectorName);
                log.Debug("Collection started.");
            }
            else if (message is CollectionStopMessage)
            {
                Context.GetChildren().ForEach(c => Context.Stop(c));
                log.Debug("Collection stopped.");
            }
        }
    }

    /// <summary>
    /// Local cluster metrics extension events.
    /// Published to local event bus subscribers by <see cref="ClusterMetricsCollector"/>.
    /// </summary>
    public interface IClusterMetricsEvent {}

    /// <summary>
    /// Current snapshot of cluster node metrics.
    /// </summary>
    public sealed class ClusterMetricsChanged : IClusterMetricsEvent
    {
        public ClusterMetricsChanged(ImmutableHashSet<NodeMetrics> nodeMetrics)
        {
            NodeMetrics = nodeMetrics;
        }

        public ImmutableHashSet<NodeMetrics> NodeMetrics { get; }
    }

    /// <summary>
    /// INTERNAL API
    /// Published to cluster members with metrics extension.
    /// </summary>
    internal interface IClusterMetricsMessage { }

    /// <summary>
    /// INTERNAL API
    /// Envelope adding a sender address to the gossip.
    /// </summary>
    internal sealed class MetricsGossipEnvelope : IClusterMetricsMessage
    {
        public MetricsGossipEnvelope(Address from, MetricsGossip gossip, bool reply)
        {
            Reply = reply;
            Gossip = gossip;
            From = from;
        }

        public Address From { get; }

        public MetricsGossip Gossip { get; }

        public bool Reply { get; }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = From.GetHashCode();
                hashCode = (hashCode * 397) ^ Gossip.GetHashCode();
                hashCode = (hashCode * 397) ^ Reply.GetHashCode();
                return hashCode;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var other = obj as MetricsGossipEnvelope;
            if (other == null)
            {
                return false;
            }

            return From.Equals(other.From) && Gossip.Equals(other.Gossip) && Reply.Equals(other.Reply);
        }
    }

    /// <summary>
    /// INTERNAL API
    /// 
    /// Cluster metrics is primarily for load-balancing of nodes. It controls metrics sampling
    /// at a regular frequency, prepares highly variable data for further analysis by other entities,
    /// and publishes the latest cluster metrics data around the node ring and local eventStream
    /// to assist in determining the need to redirect traffic to the least-loaded nodes.
    ///
    /// Metrics sampling is delegated to the <see cref="IMetricsCollector"/>.
    ///
    /// Smoothing of the data for each monitored process is delegated to the
    /// <see cref="EWMA"/> for exponential weighted moving average.
    /// </summary>
    internal class ClusterMetricsCollector : ReceiveActor
    {
        private readonly Cluster _cluster;

        private readonly ILoggingAdapter _log = Context.GetLogger();

        /// <summary>
        /// The node ring gossiped that contains only members that are <see cref="MemberStatus.Up"/>
        /// </summary>
        public ImmutableSortedSet<Address> Nodes { get; private set; }

        /// <summary>
        /// The latest metric values with their statistical data
        /// </summary>
        public MetricsGossip LatestGossip { get; private set; }

        /// <summary>
        /// The metrics collector that samples data on the node.
        /// </summary>
        public IMetricsCollector Collector { get; private set; }

        /// <summary>
        /// Start periodic gossip to random nodes in the cluster
        /// </summary>
        private ICancelable _gossipTask;

        /// <summary>
        /// Start periodic metrics collection
        /// </summary>
        private ICancelable _sampleTask;

        public ClusterMetricsCollector()
        {
            _cluster = Cluster.Get(Context.System);
            var metrics = new ClusterMetricsExtension(Context.System.AsInstanceOf<ExtendedActorSystem>());

            Nodes = ImmutableSortedSet<Address>.Empty;
            LatestGossip = MetricsGossip.Empty;
            Collector = MetricsCollector.Get(Context.System.AsInstanceOf<ExtendedActorSystem>());

            _gossipTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                metrics.Settings.PeriodicTasksInitialDelay.Max(metrics.Settings.CollectorGossipInterval),
                metrics.Settings.CollectorGossipInterval,
                Self,
                InternalClusterAction.GossipTick.Instance,
                Self);

            _sampleTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                metrics.Settings.PeriodicTasksInitialDelay.Max(metrics.Settings.CollectorSampleInterval),
                metrics.Settings.CollectorSampleInterval,
                Self,
                InternalClusterAction.MetricsTick.Instance, Self);

            Receive<InternalClusterAction.GossipTick>(tick => Gossip());
            Receive<InternalClusterAction.MetricsTick>(tick => Sample());
            Receive<MetricsGossipEnvelope>(envelope => ReceiveGossip(envelope));
            Receive<ClusterEvent.CurrentClusterState>(state => ReceiveState(state));
            Receive<ClusterEvent.MemberUp>(up => AddMember(up.Member));
            Receive<ClusterEvent.MemberRemoved>(removed => RemoveMember(removed.Member));
            Receive<ClusterEvent.MemberExited>(exited => RemoveMember(exited.Member));
            Receive<ClusterEvent.UnreachableMember>(member => RemoveMember(member.Member));
            Receive<ClusterEvent.ReachableMember>(member =>
            {
                if (member.Member.Status == MemberStatus.Up) AddMember(member.Member);
            });
            Receive<ClusterEvent.IMemberEvent>(e => { }); //not interested in other types of member event
        }

        protected override void PreStart()
        {
            _cluster.Subscribe(Self, new [] { typeof(ClusterEvent.IMemberEvent), typeof(ClusterEvent.ReachabilityEvent) });
            _cluster.LogInfo("Metrics collection has started successfully.");
        }

        protected override void PostStop()
        {
            _cluster.Unsubscribe(Self);
            _gossipTask.Cancel();
            _sampleTask.Cancel();
            Collector.Dispose();
        }

        /// <summary>
        /// Adds a member to the node ring.
        /// </summary>
        private void AddMember(Member member)
        {
            Nodes = Nodes.Add(member.Address);
        }

        /// <summary>
        /// Removes a member from the node ring.
        /// </summary>
        private void RemoveMember(Member member)
        {
            Nodes = Nodes.Remove(member.Address);
            LatestGossip = LatestGossip.Remove(member.Address);
            Publish();
        }

        /// <summary>
        /// Update the initial node ring for those nodes that are <see cref="MemberStatus.Up"/>
        /// </summary>
        private void ReceiveState(ClusterEvent.CurrentClusterState state)
        {
            Nodes = state.Members
                .Except(state.Unreachable)
                .Where(x => x.Status == MemberStatus.Up)
                .Select(x => x.Address).ToImmutableSortedSet();
        }

        /// <summary>
        /// Samples the latest metrics for the node, updates metrics statistics in <see cref="MetricsGossip"/>, and
        /// publishes the changes to the event bus.
        /// </summary>
        private void Sample()
        {
            LatestGossip = LatestGossip + Collector.Sample();
            Publish();
        }

        /// <summary>
        /// Receives changes from peer nodes, merges remote with local gossip nodes, then publishes
        /// changes to the event stream for load balancing router consumption, and gossip back.
        /// </summary>
        private void ReceiveGossip(MetricsGossipEnvelope envelope)
        {
            // remote node might not have same view of member nodes, this side should only care
            // about nodes that are known here, otherwise removed nodes can come back
            var otherGossip = envelope.Gossip.Filter(Nodes.ToImmutableHashSet());
            LatestGossip = LatestGossip.Merge(otherGossip);
            // changes will be published in the period collect task
            if (!envelope.Reply)
                ReplyGossipTo(envelope.From);
        }

        /* Gossip to peer nodes. */

        private void Gossip()
        {
            var targetAddress = SelectRandomNode(Nodes.Remove(_cluster.SelfAddress).ToImmutableList());
            if (targetAddress == null) return;
            GossipTo(targetAddress);
        }

        private void GossipTo(Address address)
        {
            SendGossip(address, new MetricsGossipEnvelope(_cluster.SelfAddress, LatestGossip, reply: false));
        }

        private void ReplyGossipTo(Address address)
        {
            SendGossip(address, new MetricsGossipEnvelope(_cluster.SelfAddress, LatestGossip, reply: true));
        }

        private void SendGossip(Address address, MetricsGossipEnvelope envelope)
        {
            Context.ActorSelection(Self.Path.ToStringWithAddress(address)).Tell(envelope);
        }

        private Address SelectRandomNode(ImmutableList<Address> addresses)
        {
            if (addresses.IsEmpty) return null;
            return addresses[ThreadLocalRandom.Current.Next(addresses.Count - 1)];
        }

        /// <summary>
        /// Publishes to the event stream.
        /// </summary>
        private void Publish()
        {
            Context.System.EventStream.Publish(new ClusterMetricsChanged(LatestGossip.Nodes));
        }
    }
}
