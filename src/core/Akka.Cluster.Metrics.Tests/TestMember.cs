//-----------------------------------------------------------------------
// <copyright file="TestMember.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Util.Internal;

namespace Akka.Cluster.Metrics.Tests
{
    static class TestMember
    {
        public static Member Create(Address address, MemberStatus status)
        {
            return Create(address, status, ImmutableHashSet.Create<string>());
        }

        public static Member Create(Address address, MemberStatus status, ImmutableHashSet<string> roles)
        {
            return Member.Create(new UniqueAddress(address, 0), 0, status, roles);
        }
    }

    public class ClusterMetricsView : IDisposable
    {
        private class EventBusListenerActor : UntypedActor
        {
            private readonly ClusterMetricsExtension _extension;
            private ImmutableHashSet<NodeMetrics> _currentMetricsSet;
            private ImmutableList<ImmutableHashSet<NodeMetrics>> _collectedMetricsList;

            public EventBusListenerActor(
                ClusterMetricsExtension extension,
                ImmutableHashSet<NodeMetrics> currentMetricsSet,
                ImmutableList<ImmutableHashSet<NodeMetrics>> collectedMetricsList)
            {
                _extension = extension;
                _currentMetricsSet = currentMetricsSet;
                _collectedMetricsList = collectedMetricsList;
            }

            protected override void PreStart()
            {
                _extension.Subscribe(Self);
            }

            protected override void PostStop()
            {
                _extension.Unsubscribe(Self);
            }

            protected override void OnReceive(object message)
            {
                ClusterMetricsChanged clusterMetricsChanged = message as ClusterMetricsChanged;
                if (clusterMetricsChanged != null)
                {
                    var nodes = clusterMetricsChanged.NodeMetrics;
                    _currentMetricsSet = nodes;
                    _collectedMetricsList = _collectedMetricsList.Add(nodes);
                }
            }
        }

        private volatile ImmutableHashSet<NodeMetrics> currentMetricsSet = ImmutableHashSet<NodeMetrics>.Empty;
        private volatile ImmutableList<ImmutableHashSet<NodeMetrics>> collectedMetricsList = ImmutableList<ImmutableHashSet<NodeMetrics>>.Empty;
        private ClusterMetricsExtension extension;
        private IActorRef eventBusListener;

        public ClusterMetricsView(ExtendedActorSystem system)
        {
            extension = new ClusterMetricsExtension(system.AsInstanceOf<ExtendedActorSystem>());
            eventBusListener = system.SystemActorOf(
                Props.Create(() => new EventBusListenerActor(extension, currentMetricsSet, collectedMetricsList))
                    .WithDispatcher(Dispatchers.DefaultDispatcherId)
                    .WithDeploy(Deploy.Local), "metrics-event-bus-listener");
        }

        public ImmutableHashSet<NodeMetrics> ClusterMetrics { get; }

        public ImmutableList<ImmutableHashSet<NodeMetrics>> MetricsHistory { get; }

        public void Dispose()
        {
            eventBusListener.Tell(PoisonPill.Instance);
        }
    }
}

