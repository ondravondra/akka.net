//-----------------------------------------------------------------------
// <copyright file="MetricsGossipSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.TestKit;
using Akka.Util.Internal;
using Xunit;
using FluentAssertions;

namespace Akka.Cluster.Metrics.Tests
{
    public class MetricsGossipSpec : MetricsCollectorFactory
    {
        public IActorRef Self { get { return TestActor; } }

        private IMetricsCollector _collector;

        public MetricsGossipSpec()
        {
            _collector = CreateMetricsCollector();
        }

        [Fact]
        public void MetricsGossip_must_add_new_NodeMetrics()
        {
            var m1 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2554), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);
            var m2 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2555), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);

            m1.Metrics.Count.Should().BeGreaterThan(3);
            m2.Metrics.Count.Should().BeGreaterThan(3);

            var g1 = MetricsGossip.Empty + m1;
            g1.Nodes.Count.Should().Be(1);
            g1.NodeMetricsFor(m1.Address).Metrics.Should().BeEquivalentTo(m1.Metrics);

            var g2 = g1 + m2;
            g2.Nodes.Count.ShouldBe(2);
            g2.NodeMetricsFor(m1.Address).Metrics.Should().BeEquivalentTo(m1.Metrics);
            g2.NodeMetricsFor(m2.Address).Metrics.Should().BeEquivalentTo(m2.Metrics);
        }

        [Fact]
        public void MetricsGossip_must_merge_peer_metrics()
        {
            var m1 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2554), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);
            var m2 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2555), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);

            var g1 = MetricsGossip.Empty + m1 + m2;
            g1.Nodes.Count.Should().Be(2);
            var beforeMergeNodes = g1.Nodes;

            var m2Updated = m2.Copy(metrics: _collector.Sample().Metrics, timestamp: m2.Timestamp + 1000);
            var g2 = g1 + m2Updated; //merge peers
            g2.Nodes.Count.Should().Be(2);
            g2.NodeMetricsFor(m1.Address).Metrics.Should().BeEquivalentTo(m1.Metrics);
            g2.NodeMetricsFor(m2.Address).Metrics.Should().BeEquivalentTo(m2.Metrics);
            g2.Nodes.Where(peer => peer.Address == m2.Address).ForEach(peer => peer.Timestamp.Should().Be(m2Updated.Timestamp));
        }

        [Fact]
        public void MetricsGossip_must_merge_an_existing_metric_set_for_a_node_and_update_node_ring()
        {
            var m1 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2554), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);
            var m2 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2555), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);
            var m3 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2556), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);
            var m2Updated = m2.Copy(metrics: _collector.Sample().Metrics, timestamp: m2.Timestamp + 1000);

            var g1 = MetricsGossip.Empty + m1 + m2;
            var g2 = MetricsGossip.Empty + m3 + m2Updated;
            g1.Nodes.Select(c => c.Address)
                .Should()
                .BeEquivalentTo(ImmutableHashSet.Create(m1.Address, m2.Address));

            //should contain nodes 1,3 and the most recent version of 2
            var mergedGossip = g1.Merge(g2);
            mergedGossip.Nodes.Select(c => c.Address)
                .Should()
                .BeEquivalentTo(ImmutableHashSet.Create(m1.Address, m2.Address, m3.Address));

            mergedGossip.NodeMetricsFor(m1.Address).Metrics.Should().BeEquivalentTo(m1.Metrics);
            mergedGossip.NodeMetricsFor(m2.Address).Metrics.Should().BeEquivalentTo(m2Updated.Metrics);
            mergedGossip.NodeMetricsFor(m3.Address).Metrics.Should().BeEquivalentTo(m3.Metrics);
            mergedGossip.Nodes.ForEach(_ => _.Metrics.Count.Should().BeGreaterThan(3));
            mergedGossip.NodeMetricsFor(m2.Address).Timestamp.Should().Be(m2Updated.Timestamp);
        }

        [Fact]
        public void MetricsGossip_must_get_the_current_NodeMetrics_if_it_exists_in_local_nodes()
        {
            var m1 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2554), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);
            var g1 = MetricsGossip.Empty + m1;
            g1.NodeMetricsFor(m1.Address).Metrics.Should().BeEquivalentTo(m1.Metrics);
        }

        [Fact]
        public void MetricsGossip_must_remove_a_node_if_it_is_no_longer_up()
        {
            var m1 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2554), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);
            var m2 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2555), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);

            var g1 = MetricsGossip.Empty + m1 + m2;
            g1.Nodes.Count.Should().Be(2);
            var g2 = g1.Remove(m1.Address);
            g2.Nodes.Count.Should().Be(1);
            g2.Nodes.Any(x => x.Address == m1.Address).Should().BeFalse();
            g2.NodeMetricsFor(m1.Address).Should().BeNull();
            g2.NodeMetricsFor(m2.Address).Metrics.Should().BeEquivalentTo(m2.Metrics);
        }

        [Fact]
        public void MetricsGossip_must_filter_nodes()
        {
            var m1 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2554), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);
            var m2 = new NodeMetrics(new Address("akka.tcp", "sys", "a", 2555), StandardMetrics.NewTimestamp(), _collector.Sample().Metrics);

            var g1 = MetricsGossip.Empty + m1 + m2;
            g1.Nodes.Count.Should().Be(2);
            var g2 = g1.Filter(ImmutableHashSet.Create(m2.Address));
            g2.Nodes.Count.Should().Be(1);
            g2.Nodes.Any(x => x.Address == m1.Address).Should().BeFalse();
            g2.NodeMetricsFor(m1.Address).Should().BeNull();
            g2.NodeMetricsFor(m2.Address).Metrics.Should().BeEquivalentTo(m2.Metrics);
        }
    }
}
