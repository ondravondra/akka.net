//-----------------------------------------------------------------------
// <copyright file="ClusterShardingGuardian.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Cluster.Tools.Singleton;
using Akka.Pattern;

namespace Akka.Cluster.Sharding
{
    /// <summary>
    /// INTERNAL API: <see cref="ShardRegion"/> and <see cref="PersistentShardCoordinator"/> actors are createad as children of this actor.
    /// </summary>
    internal sealed class ClusterShardingGuardian : ReceiveActor
    {
        #region messages

        [Serializable]
        public sealed class Start : INoSerializationVerificationNeeded
        {
            public Start(
                string typeName,
                Props entityProps,
                ClusterShardingSettings settings,
                IdExtractor idExtractor,
                ShardResolver shardResolver,
                IShardAllocationStrategy allocationStrategy,
                object handOffStopMessage)
            {
                if (string.IsNullOrEmpty(typeName))
                    throw new ArgumentNullException(nameof(typeName), "ClusterSharding start requires type name to be provided");
                if (entityProps == null)
                    throw new ArgumentNullException(nameof(entityProps), string.Format("ClusterSharding start requires Props for [{0}] to be provided", typeName));

                TypeName = typeName;
                EntityProps = entityProps;
                Settings = settings;
                IdExtractor = idExtractor;
                ShardResolver = shardResolver;
                AllocationStrategy = allocationStrategy;
                HandOffStopMessage = handOffStopMessage;
            }

            public string TypeName { get; }

            public Props EntityProps { get; }

            public ClusterShardingSettings Settings { get; }

            public IdExtractor IdExtractor { get; }

            public ShardResolver ShardResolver { get; }

            public IShardAllocationStrategy AllocationStrategy { get; }

            public object HandOffStopMessage { get; }
        }

        [Serializable]
        public sealed class StartProxy : INoSerializationVerificationNeeded
        {
            public StartProxy(string typeName, ClusterShardingSettings settings, IdExtractor extractEntityId, ShardResolver extractShardId)
            {
                if (string.IsNullOrEmpty(typeName))
                    throw new ArgumentNullException(nameof(typeName), "ClusterSharding start proxy requires type name to be provided");

                TypeName = typeName;
                Settings = settings;
                ExtractEntityId = extractEntityId;
                ExtractShardId = extractShardId;
            }

            public string TypeName { get; }

            public ClusterShardingSettings Settings { get; }

            public IdExtractor ExtractEntityId { get; }

            public ShardResolver ExtractShardId { get; }
        }

        [Serializable]
        public sealed class Started : INoSerializationVerificationNeeded
        {
            public Started(IActorRef shardRegion)
            {
                ShardRegion = shardRegion;
            }

            public IActorRef ShardRegion { get; }
        }

        #endregion

        public ClusterShardingGuardian()
        {
            Receive<Start>(start =>
            {
                var settings = start.Settings;
                var encName = Uri.EscapeDataString(start.TypeName);
                var coordinatorSingletonManagerName = CoordinatorSingletonManagerName(encName);
                var coordinatorPath = CoordinatorPath(encName);
                var shardRegion = Context.Child(encName);

                if (Equals(shardRegion, ActorRefs.Nobody))
                {
                    var minBackoff = settings.TunningParameters.CoordinatorFailureBackoff;
                    var maxBackoff = new TimeSpan(minBackoff.Ticks * 5);
                    var coordinatorProps = PersistentShardCoordinator.Props(start.TypeName, settings, start.AllocationStrategy);
                    var singletonProps = Props.Create(() => new BackoffSupervisor(
                        coordinatorProps,
                        "coordinator",
                        minBackoff,
                        maxBackoff,
                        0.2)).WithDeploy(Deploy.Local);

                    var singletonSettings = settings.CoordinatorSingletonSettings
                        .WithSingletonName("singleton")
                        .WithRole(settings.Role);

                    Context.ActorOf(ClusterSingletonManager.Props(
                        singletonProps,
                        PoisonPill.Instance,
                        singletonSettings).WithDispatcher(Context.Props.Dispatcher), 
                        coordinatorSingletonManagerName);

                    shardRegion = Context.ActorOf(ShardRegion.Props(
                        typeName: start.TypeName,
                        entityProps: start.EntityProps,
                        settings: settings,
                        coordinatorPath: coordinatorPath,
                        extractEntityId: start.IdExtractor,
                        extractShardId: start.ShardResolver,
                        handOffStopMessage: start.HandOffStopMessage).WithDispatcher(Context.Props.Dispatcher),
                        encName);
                }

                Sender.Tell(new Started(shardRegion));
            });

            Receive<StartProxy>(startProxy =>
            {
                var settings = startProxy.Settings;
                var encName = Uri.EscapeDataString(startProxy.TypeName);
                var coordinatorSingletonManagerName = CoordinatorSingletonManagerName(encName);
                var coordinatorPath = CoordinatorPath(encName);
                var shardRegion = Context.Child(encName);

                if (Equals(shardRegion, ActorRefs.Nobody))
                {
                    shardRegion = Context.ActorOf(ShardRegion.ProxyProps(
                        typeName: startProxy.TypeName,
                        settings: settings,
                        coordinatorPath: coordinatorPath,
                        extractEntityId: startProxy.ExtractEntityId,
                        extractShardId: startProxy.ExtractShardId).WithDispatcher(Context.Props.Dispatcher),
                        encName);
                }

                Sender.Tell(new Started(shardRegion));
            });
        }

        private string CoordinatorSingletonManagerName(string encName)
        {
            return encName + "Coordinator";
        }

        private string CoordinatorPath(string encName)
        {
            return (Self.Path / CoordinatorSingletonManagerName(encName) / "singleton" / "coordinator").ToStringWithoutAddress();
        }
    }
}