//-----------------------------------------------------------------------
// <copyright file="ClusterSingletonManager.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Remote;

namespace Akka.Cluster.Tools.Singleton
{
    /// <summary>
    /// TBD
    /// </summary>
    public interface IClusterSingletonMessage { }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class HandOverToMe : IClusterSingletonMessage, IDeadLetterSuppression
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly HandOverToMe Instance = new HandOverToMe();
        private HandOverToMe() { }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class HandOverInProgress : IClusterSingletonMessage
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly HandOverInProgress Instance = new HandOverInProgress();
        private HandOverInProgress() { }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class HandOverDone : IClusterSingletonMessage
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly HandOverDone Instance = new HandOverDone();
        private HandOverDone() { }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class TakeOverFromMe : IClusterSingletonMessage, IDeadLetterSuppression
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly TakeOverFromMe Instance = new TakeOverFromMe();
        private TakeOverFromMe() { }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class Cleanup : IClusterSingletonMessage
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly Cleanup Instance = new Cleanup();
        private Cleanup() { }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class StartOldestChangedBuffer
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly StartOldestChangedBuffer Instance = new StartOldestChangedBuffer();
        private StartOldestChangedBuffer() { }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class HandOverRetry
    {
        /// <summary>
        /// TBD
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="count">TBD</param>
        public HandOverRetry(int count)
        {
            Count = count;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class TakeOverRetry
    {
        /// <summary>
        /// TBD
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="count">TBD</param>
        public TakeOverRetry(int count)
        {
            Count = count;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    public interface IClusterSingletonData { }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class Uninitialized : IClusterSingletonData
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly Uninitialized Instance = new Uninitialized();
        private Uninitialized() { }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class YoungerData : IClusterSingletonData
    {
        /// <summary>
        /// TBD
        /// </summary>
        public Address Oldest { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="oldest">TBD</param>
        public YoungerData(Address oldest)
        {
            Oldest = oldest;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class BecomingOldestData : IClusterSingletonData
    {
        /// <summary>
        /// TBD
        /// </summary>
        public Address PreviousOldest { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="previousOldest">TBD</param>
        public BecomingOldestData(Address previousOldest)
        {
            PreviousOldest = previousOldest;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class OldestData : IClusterSingletonData
    {
        /// <summary>
        /// TBD
        /// </summary>
        public  IActorRef Singleton { get; }

        /// <summary>
        /// TBD
        /// </summary>
        public  bool SingletonTerminated { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="singleton">TBD</param>
        /// <param name="singletonTerminated">TBD</param>
        public OldestData(IActorRef singleton, bool singletonTerminated)
        {
            Singleton = singleton;
            SingletonTerminated = singletonTerminated;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class WasOldestData : IClusterSingletonData
    {
        /// <summary>
        /// TBD
        /// </summary>
        public IActorRef Singleton { get; }

        /// <summary>
        /// TBD
        /// </summary>
        public bool SingletonTerminated { get; }

        /// <summary>
        /// TBD
        /// </summary>
        public Address NewOldest { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="singleton">TBD</param>
        /// <param name="singletonTerminated">TBD</param>
        /// <param name="newOldest">TBD</param>
        public WasOldestData(IActorRef singleton, bool singletonTerminated, Address newOldest)
        {
            Singleton = singleton;
            SingletonTerminated = singletonTerminated;
            NewOldest = newOldest;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class HandingOverData : IClusterSingletonData
    {
        /// <summary>
        /// TBD
        /// </summary>
        public IActorRef Singleton { get; }

        /// <summary>
        /// TBD
        /// </summary>
        public IActorRef HandOverTo { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="singleton">TBD</param>
        /// <param name="handOverTo">TBD</param>
        public HandingOverData(IActorRef singleton, IActorRef handOverTo)
        {
            Singleton = singleton;
            HandOverTo = handOverTo;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class StoppingData : IClusterSingletonData
    {
        /// <summary>
        /// TBD
        /// </summary>
        public IActorRef Singleton { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="singleton">TBD</param>
        public StoppingData(IActorRef singleton)
        {
            Singleton = singleton;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class EndData : IClusterSingletonData
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly EndData Instance = new EndData();

        /// <summary>
        /// TBD
        /// </summary>
        public EndData()
        {
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    internal sealed class DelayedMemberRemoved
    {
        /// <summary>
        /// TBD
        /// </summary>
        public Member Member { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="member">TBD</param>
        public DelayedMemberRemoved(Member member)
        {
            Member = member;
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    [Serializable]
    public enum ClusterSingletonState
    {
        /// <summary>
        /// TBD
        /// </summary>
        Start,
        /// <summary>
        /// TBD
        /// </summary>
        Oldest,
        /// <summary>
        /// TBD
        /// </summary>
        Younger,
        /// <summary>
        /// TBD
        /// </summary>
        BecomingOldest,
        /// <summary>
        /// TBD
        /// </summary>
        WasOldest,
        /// <summary>
        /// TBD
        /// </summary>
        HandingOver,
        /// <summary>
        /// TBD
        /// </summary>
        TakeOver,
        /// <summary>
        /// TBD
        /// </summary>
        Stopping,
        /// <summary>
        /// TBD
        /// </summary>
        End
    }

    /// <summary>
    /// Thrown when a consistent state can't be determined within the defined retry limits.
    /// Eventually it will reach a stable state and can continue, and that is simplified 
    /// by starting over with a clean state. Parent supervisor should typically restart the actor, i.e. default decision.
    /// </summary>
    public sealed class ClusterSingletonManagerIsStuck : AkkaException
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="message">TBD</param>
        public ClusterSingletonManagerIsStuck(string message) : base(message) { }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="info">TBD</param>
        /// <param name="context">TBD</param>
        public ClusterSingletonManagerIsStuck(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// <para>
    /// Manages singleton actor instance among all cluster nodes or a group of nodes tagged with a specific role.
    /// At most one singleton instance is running at any point in time.
    /// </para>
    /// <para>
    /// The ClusterSingletonManager is supposed to be started on all nodes, or all nodes with specified role, 
    /// in the cluster with <see cref="ActorSystem.ActorOf"/>. The actual singleton is started on the oldest node 
    /// by creating a child actor from the supplied `singletonProps`.
    /// </para>
    /// <para>
    /// The singleton actor is always running on the oldest member with specified role. The oldest member is determined 
    /// by <see cref="Member.IsOlderThan"/>. This can change when removing members. A graceful hand over can normally  
    /// be performed when current oldest node is leaving the cluster. Be aware that there is a short time period when 
    /// there is no active singleton during the hand-over process.
    /// </para>
    /// <para>
    /// The cluster failure detector will notice when oldest node becomes unreachable due to things like CLR crash, 
    /// hard shut down, or network failure. When the crashed node has been removed (via down) from the cluster then 
    /// a new oldest node will take over and a new singleton actor is created.For these failure scenarios there 
    /// will not be a graceful hand-over, but more than one active singletons is prevented by all reasonable means.
    /// Some corner cases are eventually resolved by configurable timeouts.
    /// </para>
    /// <para>
    /// You access the singleton actor with <see cref="ClusterSingletonProxy"/>. Alternatively the singleton actor may 
    /// broadcast its existence when it is started.
    /// </para>
    /// <para>
    /// Use factory method <see cref="ClusterSingletonManager.Props"/> to create the <see cref="Actor.Props"/> for the actor.
    /// </para>
    /// </summary>
    public sealed class ClusterSingletonManager : FSM<ClusterSingletonState, IClusterSingletonData>
    {
        /// <summary>
        /// Returns default HOCON configuration for the cluster singleton.
        /// </summary>
        /// <returns>TBD</returns>
        public static Config DefaultConfig()
        {
            return ConfigurationFactory.FromResource<ClusterSingletonManager>("Akka.Cluster.Tools.Singleton.reference.conf");
        }

        /// <summary>
        /// Creates props for the current cluster singleton manager using <see cref="PoisonPill"/> 
        /// as the default termination message.
        /// </summary>
        /// <param name="singletonProps"><see cref="Actor.Props"/> of the singleton actor instance.</param>
        /// <param name="settings">Cluster singleton manager settings.</param>
        /// <returns>TBD</returns>
        public static Props Props(Props singletonProps, ClusterSingletonManagerSettings settings)
        {
            return Props(singletonProps, PoisonPill.Instance, settings);
        }

        /// <summary>
        /// Creates props for the current cluster singleton manager.
        /// </summary>
        /// <param name="singletonProps"><see cref="Actor.Props"/> of the singleton actor instance.</param>
        /// <param name="terminationMessage">
        /// When handing over to a new oldest node this <paramref name="terminationMessage"/> is sent to the singleton actor 
        /// to tell it to finish its work, close resources, and stop. The hand-over to the new oldest node 
        /// is completed when the singleton actor is terminated. Note that <see cref="PoisonPill"/> is a 
        /// perfectly fine <paramref name="terminationMessage"/> if you only need to stop the actor.
        /// </param>
        /// <param name="settings">Cluster singleton manager settings.</param>
        /// <returns>TBD</returns>
        public static Props Props(Props singletonProps, object terminationMessage, ClusterSingletonManagerSettings settings)
        {
            return Actor.Props.Create(() => new ClusterSingletonManager(singletonProps, terminationMessage, settings)).WithDeploy(Deploy.Local);
        }

        private readonly Props _singletonProps;
        private readonly object _terminationMessage;
        private readonly ClusterSingletonManagerSettings _settings;

        private const string HandOverRetryTimer = "hand-over-retry";
        private const string TakeOverRetryTimer = "take-over-retry";
        private const string CleanupTimer = "cleanup";

        // Previous GetNext request delivered event and new GetNext is to be sent
        private bool _oldestChangedReceived = true;
        private bool _selfExited;

        // started when when self member is Up
        private IActorRef _oldestChangedBuffer;
        // keep track of previously removed members
        private ImmutableDictionary<Address, Deadline> _removed = ImmutableDictionary<Address, Deadline>.Empty;
        private readonly TimeSpan _removalMargin;
        private readonly int _maxHandOverRetries;
        private readonly int _maxTakeOverRetries;
        private readonly Cluster _cluster = Cluster.Get(Context.System);
        private ILoggingAdapter _log;

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="singletonProps">TBD</param>
        /// <param name="terminationMessage">TBD</param>
        /// <param name="settings">TBD</param>
        /// <exception cref="ArgumentException">TBD</exception>
        /// <exception cref="ConfigurationException">TBD</exception>
        /// <returns>TBD</returns>
        public ClusterSingletonManager(Props singletonProps, object terminationMessage, ClusterSingletonManagerSettings settings)
        {
            var role = settings.Role;
            if (!string.IsNullOrEmpty(role) && !_cluster.SelfRoles.Contains(role))
                throw new ArgumentException(string.Format("This cluster member [{0}] doesn't have the role [{1}]", _cluster.SelfAddress, role));

            _singletonProps = singletonProps;
            _terminationMessage = terminationMessage;
            _settings = settings;

            _removalMargin = (settings.RemovalMargin <= TimeSpan.Zero) ? _cluster.DowningProvider.DownRemovalMargin : settings.RemovalMargin;

            var n = (int)(_removalMargin.TotalMilliseconds / _settings.HandOverRetryInterval.TotalMilliseconds);

            var minRetries = Context.System.Settings.Config.GetInt("akka.cluster.singleton.min-number-of-hand-over-retries");
            if (minRetries < 1)
                throw new ConfigurationException("min-number-of-hand-over-retries must be >= 1");

            _maxHandOverRetries = Math.Max(minRetries, n + 3);
            _maxTakeOverRetries = Math.Max(1, _maxHandOverRetries - 3);

            InitializeFSM();
        }

        private ILoggingAdapter Log { get { return _log ?? (_log = Context.GetLogger()); } }

        /// <summary>
        /// TBD
        /// </summary>
        protected override void PreStart()
        {
            base.PreStart();

            // subscribe to cluster changes, re-subscribe when restart
            _cluster.Subscribe(Self, new[] { typeof(ClusterEvent.MemberExited), typeof(ClusterEvent.MemberRemoved) });

            SetTimer(CleanupTimer, Cleanup.Instance, TimeSpan.FromMinutes(1.0), repeat: true);

            // defer subscription to avoid some jitter when
            // starting/joining several nodes at the same time
            var self = Self;
            _cluster.RegisterOnMemberUp(() => self.Tell(StartOldestChangedBuffer.Instance));
        }

        /// <summary>
        /// TBD
        /// </summary>
        protected override void PostStop()
        {
            CancelTimer(CleanupTimer);
            _cluster.Unsubscribe(Self);
            base.PostStop();
        }

        private void AddRemoved(Address address)
        {
            _removed = _removed.Add(address, Deadline.Now + TimeSpan.FromMinutes(15.0));
        }

        private void CleanupOverdueNotMemberAnyMore()
        {
            _removed = _removed.Where(kv => kv.Value.IsOverdue).ToImmutableDictionary();
        }

        private ActorSelection Peer(Address at)
        {
            return Context.ActorSelection(Self.Path.ToStringWithAddress(at));
        }

        private void GetNextOldestChange()
        {
            if (_oldestChangedReceived)
            {
                _oldestChangedReceived = false;
                _oldestChangedBuffer.Tell(OldestChangedBuffer.GetNext.Instance);
            }
        }

        private State<ClusterSingletonState, IClusterSingletonData> GoToOldest()
        {
            Log.Info("Singleton manager [{0}] starting singleton actor", _cluster.SelfAddress);
            var singleton = Context.Watch(Context.ActorOf(_singletonProps, _settings.SingletonName));
            return
                GoTo(ClusterSingletonState.Oldest).Using(new OldestData(singleton, false));
        }

        private void InitializeFSM()
        {
            When(ClusterSingletonState.Start, e =>
            {
                if (e.FsmEvent is StartOldestChangedBuffer)
                {
                    _oldestChangedBuffer = Context.ActorOf(Actor.Props.Create<OldestChangedBuffer>(_settings.Role).WithDispatcher(Context.Props.Dispatcher));
                    GetNextOldestChange();
                    return Stay();
                }
                else if (e.FsmEvent is OldestChangedBuffer.InitialOldestState initialOldestState)
                {
                    _oldestChangedReceived = true;
                    var isSelfOldest = _cluster.SelfAddress.Equals(initialOldestState.Oldest);

                    if (isSelfOldest && initialOldestState.SafeToBeOldest)
                        return GoToOldest();
                    else if (isSelfOldest)
                        return GoTo(ClusterSingletonState.BecomingOldest).Using(new BecomingOldestData(null));
                    else
                        return GoTo(ClusterSingletonState.Younger).Using(new YoungerData(initialOldestState.Oldest));
                }
                else return null;
            });

            When(ClusterSingletonState.Younger, e =>
            {
                DelayedMemberRemoved removed;
                YoungerData youngerData;
                if (e.FsmEvent is OldestChangedBuffer.OldestChanged && (youngerData = e.StateData as YoungerData) != null)
                {
                    var oldestChanged = (OldestChangedBuffer.OldestChanged)e.FsmEvent;

                    Log.Info("Younger observed OldestChanged: [{0} -> myself]", youngerData.Oldest);

                    _oldestChangedReceived = true;
                    if (oldestChanged.Oldest.Equals(_cluster.SelfAddress))
                    {
                        if (youngerData.Oldest == null) return GoToOldest();
                        else if (_removed.ContainsKey(youngerData.Oldest)) return GoToOldest();
                        else
                        {
                            Peer(youngerData.Oldest).Tell(HandOverToMe.Instance);
                            return GoTo(ClusterSingletonState.BecomingOldest).Using(new BecomingOldestData(youngerData.Oldest));
                        }
                    }
                    else
                    {
                        GetNextOldestChange();
                        return Stay().Using(new YoungerData(oldestChanged.Oldest));
                    }
                }
                else if (e.FsmEvent is ClusterEvent.MemberRemoved)
                {
                    var m = ((ClusterEvent.MemberRemoved)e.FsmEvent).Member;
                    if (m.Address.Equals(_cluster.SelfAddress))
                    {
                        Log.Info("Self removed, stopping ClusterSingletonManager");
                        return Stop();
                    }
                    else
                    {
                        ScheduleDelayedMemberRemoved(m);
                        return Stay();
                    }
                }
                else if ((removed = e.FsmEvent as DelayedMemberRemoved) != null
                    && (youngerData = e.StateData as YoungerData) != null
                    && removed.Member.Address.Equals(youngerData.Oldest))
                {
                    Log.Info("Previous oldest removed [{0}]", removed.Member.Address);
                    AddRemoved(removed.Member.Address);
                    // transition when OldestChanged
                    return Stay().Using(new YoungerData(null));
                }
                else return null;
            });

            When(ClusterSingletonState.BecomingOldest, e =>
            {
                DelayedMemberRemoved removed;
                var becomingOldest = e.StateData as BecomingOldestData;

                if (e.FsmEvent is HandOverInProgress)
                {
                    // confirmation that the hand-over process has started
                    Log.Info("Hand-over in progress at [{0}]", Sender.Path.Address);
                    CancelTimer(HandOverRetryTimer);
                    return Stay();
                }
                else if (e.FsmEvent is HandOverDone)
                {
                    if (becomingOldest == null || becomingOldest.PreviousOldest == null) return null;
                    else
                    {
                        if (Sender.Path.Address.Equals(becomingOldest.PreviousOldest))
                            return GoToOldest();
                        else
                        {
                            Log.Info("Ignoring HandOverDone in BecomingOldest from [{0}]. Expected previous oldest [{1}]", Sender.Path.Address, becomingOldest.PreviousOldest);
                            return Stay();
                        }
                    }
                }
                else if (e.FsmEvent is ClusterEvent.MemberRemoved)
                {
                    var member = ((ClusterEvent.MemberRemoved)e.FsmEvent).Member;
                    if (member.Address.Equals(_cluster.SelfAddress))
                    {
                        Log.Info("Self removed, stopping ClusterSingletonManager");
                        return Stop();
                    }
                    else
                    {
                        ScheduleDelayedMemberRemoved(member);
                        return Stay();
                    }
                }
                else if ((removed = e.FsmEvent as DelayedMemberRemoved) != null
                    && becomingOldest != null
                    && removed.Member.Address.Equals(becomingOldest.PreviousOldest))
                {
                    Log.Info("Previous oldest [{0}] removed", becomingOldest.PreviousOldest);
                    AddRemoved(removed.Member.Address);
                    return GoToOldest();
                }
                else if (e.FsmEvent is TakeOverFromMe)
                {
                    if (becomingOldest == null) return null;
                    else
                    {
                        if (becomingOldest.PreviousOldest == null)
                        {
                            Sender.Tell(HandOverToMe.Instance);
                            return Stay().Using(new BecomingOldestData(Sender.Path.Address));
                        }
                        else
                        {
                            if (becomingOldest.PreviousOldest.Equals(Sender.Path.Address))
                                Sender.Tell(HandOverToMe.Instance);
                            else
                                Log.Info("Ignoring TakeOver request in BecomingOldest from [{0}]. Expected previous oldest [{1}]", Sender.Path.Address, becomingOldest.PreviousOldest);

                            return Stay();
                        }
                    }
                }
                else if (e.FsmEvent is HandOverRetry && becomingOldest != null)
                {
                    var handOverRetry = (HandOverRetry)e.FsmEvent;
                    if (handOverRetry.Count <= _maxHandOverRetries)
                    {
                        Log.Info("Retry [{0}], sending HandOverToMe to [{1}]", handOverRetry.Count, becomingOldest.PreviousOldest);
                        if (becomingOldest.PreviousOldest != null)
                            Peer(becomingOldest.PreviousOldest).Tell(HandOverToMe.Instance);

                        SetTimer(HandOverRetryTimer, new HandOverRetry(handOverRetry.Count + 1), _settings.HandOverRetryInterval, repeat: false);
                        return Stay();
                    }
                    else if (becomingOldest.PreviousOldest != null && _removed.ContainsKey(becomingOldest.PreviousOldest))
                    {
                        // can't send HandOverToMe, previousOldest unknown for new node (or restart)
                        // previous oldest might be down or removed, so no TakeOverFromMe message is received
                        Log.Info("Timeout in BecomingOldest. Previous oldest unknown, removed and no TakeOver request.");
                        return GoToOldest();
                    }
                    else if (_cluster.IsTerminated)
                    {
                        return Stop();
                    }
                    else
                    {
                        throw new ClusterSingletonManagerIsStuck(string.Format("Becoming singleton oldest was stuck because previous oldest [{0}] is unresponsive", becomingOldest.PreviousOldest));
                    }
                }
                else return null;
            });

            When(ClusterSingletonState.Oldest, e =>
            {
                if (e.FsmEvent is OldestChangedBuffer.OldestChanged oldestChanged
                    && e.StateData is OldestData oldestData)
                {
                    switch(oldestChanged.Oldest)
                    {
                        case Address a when a.Equals(_cluster.SelfAddress):
                            // already oldest
                            return Stay();
                        case Address a when !_selfExited && _removed.ContainsKey(oldestChanged.Oldest):
                            // The member removal was not completed and the old removed node is considered
                            // oldest again. Safest is to terminate the singleton instance and goto Younger.
                            // This node will become oldest again when the other is removed again.
                            return GoToHandingOver(oldestData.Singleton, oldestData.SingletonTerminated, null);
                        case Address a:
                            // send TakeOver request in case the new oldest doesn't know previous oldest
                            Peer(a).Tell(TakeOverFromMe.Instance);
                            SetTimer(TakeOverRetryTimer, new TakeOverRetry(1), _settings.HandOverRetryInterval);
                            return GoTo(ClusterSingletonState.WasOldest)
                                .Using(new WasOldestData(oldestData.Singleton, oldestData.SingletonTerminated, oldestChanged.Oldest));
                        case null:
                            // new oldest will initiate the hand-over
                            SetTimer(TakeOverRetryTimer, new TakeOverRetry(1), _settings.HandOverRetryInterval);
                            return GoTo(ClusterSingletonState.WasOldest).Using(new WasOldestData(oldestData.Singleton, oldestData.SingletonTerminated, null));
                    }
                }
                else if (e.FsmEvent is HandOverToMe && e.StateData is OldestData oldestData2)
                {
                    return GoToHandingOver(oldestData2.Singleton, oldestData2.SingletonTerminated, Sender);
                }
                else if (e.FsmEvent is Terminated terminated 
                    && e.StateData is OldestData oldestData3
                    && terminated.ActorRef.Equals(oldestData3.Singleton))
                {
                    return Stay().Using(new OldestData(oldestData3.Singleton, true));
                }

                return null;
            });

            When(ClusterSingletonState.WasOldest, e =>
            {
                if (e.FsmEvent is TakeOverRetry takeOverRetry && e.StateData is WasOldestData wasOldestData)
                {
                    if (_cluster.IsTerminated && (wasOldestData.NewOldest == null || takeOverRetry.Count > _maxTakeOverRetries))
                    {
                        if (wasOldestData.SingletonTerminated)
                        {
                            return Stop();
                        }
                        else
                        {
                            return GoToStopping(wasOldestData.Singleton);
                        }
                    }
                    else if (takeOverRetry.Count <= _maxTakeOverRetries)
                    {
                        Log.Info("Retry [{0}], sending TakeOverFromMe to [{1}]", takeOverRetry.Count, wasOldestData.NewOldest);

                        if (wasOldestData.NewOldest != null)
                        {
                            Peer(wasOldestData.NewOldest).Tell(TakeOverFromMe.Instance);
                        }

                        SetTimer(TakeOverRetryTimer, new TakeOverRetry(takeOverRetry.Count + 1), _settings.HandOverRetryInterval);
                        return Stay();
                    }
                    else
                    {
                        throw new ClusterSingletonManagerIsStuck(string.Format("Expected hand-over to [{0}] never occured", wasOldestData.NewOldest));
                    }
                }
                else if (e.FsmEvent is HandOverToMe && e.StateData is WasOldestData wasOldestData2)
                {
                    return GoToHandingOver(wasOldestData2.Singleton, wasOldestData2.SingletonTerminated, Sender);
                }
                else if (e.FsmEvent is ClusterEvent.MemberRemoved memberRemoved 
                    && memberRemoved.Member.Address.Equals(_cluster.SelfAddress)
                    && !_selfExited)
                {
                    Log.Info("Self removed, stopping ClusterSingletonManager");
                    return Stop();
                }
                else if (e.FsmEvent is ClusterEvent.MemberRemoved memberRemoved2
                    && e.StateData is WasOldestData wasOldestData3
                    && wasOldestData3.NewOldest != null
                    && !_selfExited
                    && memberRemoved2.Member.Address.Equals(wasOldestData3.NewOldest))
                {
                    AddRemoved(memberRemoved2.Member.Address);
                    return GoToHandingOver(wasOldestData3.Singleton, wasOldestData3.SingletonTerminated, null);
                }
                else if (e.FsmEvent is Terminated terminated 
                    && e.StateData is WasOldestData wasOldestData4
                    && terminated.ActorRef.Equals(wasOldestData4.Singleton))
                {
                    return Stay().Using(new WasOldestData(wasOldestData4.Singleton, true, wasOldestData4.NewOldest));
                }

                return null;
            });

            State<ClusterSingletonState, IClusterSingletonData> GoToHandingOver(IActorRef singleton, bool singletonTerminated, IActorRef handOverTo)
            {
                if (singletonTerminated)
                {
                    return HandleHandOverDone(handOverTo);
                }
                else
                {
                    handOverTo?.Tell(HandOverInProgress.Instance);
                    singleton.Tell(_terminationMessage);
                    return GoTo(ClusterSingletonState.HandingOver).Using(new HandingOverData(singleton, handOverTo));
                }
            }

            When(ClusterSingletonState.HandingOver, e =>
            {
                if (e.FsmEvent is Terminated terminated 
                    && e.StateData is HandingOverData handingOverData
                    && terminated.ActorRef.Equals(handingOverData.Singleton))
                {
                    return HandleHandOverDone(handingOverData.HandOverTo);
                }
                else if (e.FsmEvent is HandOverToMe
                    && e.StateData is HandingOverData d
                    && d.HandOverTo.Equals(Sender))
                {
                    Sender.Tell(HandOverInProgress.Instance);
                    return Stay();
                }
                return null;
            });

            State<ClusterSingletonState, IClusterSingletonData> HandleHandOverDone(IActorRef handOverTo)
            {
                Address newOldest = handOverTo?.Path.Address;
                Log.Info("Singleton terminated, hand-over done [{0} -> {1}]", _cluster.SelfAddress, newOldest);
                handOverTo?.Tell(HandOverDone.Instance);

                if (_removed.ContainsKey(_cluster.SelfAddress))
                {
                    Log.Info("Self removed, stopping ClusterSingletonManager");
                    return Stop();
                }
                else if (_selfExited)
                {
                    return GoTo(ClusterSingletonState.End).Using(new EndData());
                }

                return GoTo(ClusterSingletonState.Younger).Using(new YoungerData(newOldest));
            }

            State<ClusterSingletonState, IClusterSingletonData> GoToStopping(IActorRef singleton)
            {
                singleton.Tell(_terminationMessage);
                return GoTo(ClusterSingletonState.Stopping).Using(new StoppingData(singleton));
            }

            When(ClusterSingletonState.Stopping, e =>
            {
                if (e.FsmEvent is Terminated terminated 
                    && e.StateData is StoppingData stoppingData
                    && terminated.ActorRef.Equals(stoppingData.Singleton))
                {
                    return Stop();
                }

                return null;
            });

            When(ClusterSingletonState.End, e =>
            {
                if (e.FsmEvent is ClusterEvent.MemberRemoved removed 
                    && removed.Member.Address.Equals(_cluster.SelfAddress))
                {
                    Log.Info("Self removed, stopping ClusterSingletonManager");
                    return Stop();
                }

                return null;
            });

            WhenUnhandled(e =>
            {
                if (e.FsmEvent is ClusterEvent.CurrentClusterState)
                {
                    return Stay();
                }
                else if (e.FsmEvent is ClusterEvent.MemberExited memberExited)
                {
                    if (memberExited.Member.Address.Equals(_cluster.SelfAddress))
                    {
                        _selfExited = true;
                        Log.Info("Exited [{0}]", memberExited.Member.Address);
                    }

                    return Stay();
                }
                else if (e.FsmEvent is ClusterEvent.MemberRemoved memberRemoved)
                {
                    if (memberRemoved.Member.Address.Equals(_cluster.SelfAddress) && !_selfExited)
                    {
                        Log.Info("Self removed, stopping ClusterSingletonManager");
                        return Stop();
                    }
                    else
                    {
                        if (!_selfExited)
                        {
                            Log.Info("Member removed [{0}]", memberRemoved.Member.Address);
                        }

                        AddRemoved(memberRemoved.Member.Address);
                        return Stay();
                    }
                }
                else if (e.FsmEvent is DelayedMemberRemoved delayedMemberRemoved)
                {
                    if (!_selfExited)
                        Log.Info("Member removed [{0}]", delayedMemberRemoved.Member.Address);

                    AddRemoved(delayedMemberRemoved.Member.Address);
                    return Stay();
                }
                else if (e.FsmEvent is TakeOverFromMe)
                {
                    Log.Info("Ignoring TakeOver request in [{0}] from [{1}].", StateName, Sender.Path.Address);
                    return Stay();
                }
                else if (e.FsmEvent is Cleanup)
                {
                    CleanupOverdueNotMemberAnyMore();
                    return Stay();
                }

                return null;
            });

            OnTransition((from, to) =>
            {
                Log.Info("ClusterSingletonManager state change [{0} -> {1}] {2}", from, to, StateData.ToString());

                if (to == ClusterSingletonState.BecomingOldest) SetTimer(HandOverRetryTimer, new HandOverRetry(1), _settings.HandOverRetryInterval);
                if (from == ClusterSingletonState.BecomingOldest) CancelTimer(HandOverRetryTimer);
                if (from == ClusterSingletonState.WasOldest) CancelTimer(TakeOverRetryTimer);
                if (to == ClusterSingletonState.Younger || to == ClusterSingletonState.Oldest) GetNextOldestChange();
                if (to == ClusterSingletonState.Younger || to == ClusterSingletonState.End)
                {
                    if (_removed.ContainsKey(_cluster.SelfAddress))
                    {
                        Log.Info("Self removed, stopping ClusterSingletonManager");
                        Context.Stop(Self);
                    }
                }
            });

            StartWith(ClusterSingletonState.Start, Uninitialized.Instance);
        }

        private void ScheduleDelayedMemberRemoved(Member member)
        {
            if (_removalMargin > TimeSpan.Zero)
            {
                Log.Debug("Schedule DelayedMemberRemoved for {0}", member.Address);
                Context.System.Scheduler.ScheduleTellOnce(_removalMargin, Self, new DelayedMemberRemoved(member), Self);
            }
            else Self.Tell(new DelayedMemberRemoved(member));
        }
    }
}