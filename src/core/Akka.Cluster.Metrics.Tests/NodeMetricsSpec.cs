//-----------------------------------------------------------------------
// <copyright file="NodeMetricsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Immutable;
using Akka.Actor;
using Akka.TestKit;
using Xunit;
using FluentAssertions;

namespace Akka.Cluster.Metrics.Tests
{
    public class NodeMetricsSpec : AkkaSpec
    {
        private readonly Address _node1 = new Address("akka.tcp", "sys", "a", 2554);
        private readonly Address _node2 = new Address("akka.tcp", "sys", "a", 2555);

        [Fact]
        public void NodeMetrics_must_return_correct_result_for_2_same_nodes()
        {
            new NodeMetrics(_node1, 1).Equals(new NodeMetrics(_node1, 2)).Should().BeTrue();
        }

        [Fact]
        public void NodeMetrics_must_return_correct_result_for_2_NOT_same_nodes()
        {
            new NodeMetrics(_node1, 1).Equals(new NodeMetrics(_node2, 2)).Should().BeFalse();
        }

        [Fact]
        public void NodeMetrics_must_merge_2_NodeMetrics_by_most_recent()
        {
            var sample1 = new NodeMetrics(_node1, 1, ImmutableHashSet.Create(Metric.Create("a", 10), Metric.Create("b", 20)));
            var sample2 = new NodeMetrics(_node1, 2, ImmutableHashSet.Create(Metric.Create("a", 11), Metric.Create("c", 30)));

            var merged = sample1.Merge(sample2);
            merged.Timestamp.Should().Be(sample2.Timestamp);
            merged.Metric("a").Value.Should().Be(11);
            merged.Metric("b").Value.Should().Be(20);
            merged.Metric("c").Value.Should().Be(30);
        }

        [Fact]
        public void NodeMetrics_must_not_merge_2_NodeMetrics_if_master_is_more_recent()
        {
            var sample1 = new NodeMetrics(_node1, 1, ImmutableHashSet.Create(Metric.Create("a", 10), Metric.Create("b", 20)));
            var sample2 = new NodeMetrics(_node1, 0, ImmutableHashSet.Create(Metric.Create("a", 11), Metric.Create("c", 30)));

            var merged = sample1.Merge(sample2); //older and not the same
            merged.Timestamp.Should().Be(sample1.Timestamp);
            merged.Metrics.Should().BeEquivalentTo(sample1.Metrics);
        }

        [Fact]
        public void NodeMetrics_must_update_2_NodeMetrics_by_most_recent()
        {
            var sample1 = new NodeMetrics(_node1, 1, ImmutableHashSet.Create(Metric.Create("a", 10), Metric.Create("b", 20)));
            var sample2 = new NodeMetrics(_node1, 2, ImmutableHashSet.Create(Metric.Create("a", 11), Metric.Create("c", 30)));

            var updated = sample1.Update(sample2);

            updated.Metrics.Count.Should().Be(3);
            updated.Timestamp.Should().Be(sample2.Timestamp);
            updated.Metric("a").Value.Should().Be(11);
            updated.Metric("b").Value.Should().Be(20);
            updated.Metric("c").Value.Should().Be(30);
        }

        [Fact]
        public void NodeMetrics_must_update_3_NodeMetrics_with_ewma_applied()
        {
            double decay = MetricsCollectorSpec.defaultDecayFactor;
            var epsilon = 0.001;

            var sample1 = new NodeMetrics(_node1, 1, ImmutableHashSet.Create(Metric.Create("a", 1, decay), Metric.Create("b", 4, decay)));
            var sample2 = new NodeMetrics(_node1, 2, ImmutableHashSet.Create(Metric.Create("a", 2, decay), Metric.Create("c", 5, decay)));
            var sample3 = new NodeMetrics(_node1, 3, ImmutableHashSet.Create(Metric.Create("a", 3, decay), Metric.Create("d", 6, decay)));

            var updated = sample1.Update(sample2).Update(sample3);

            updated.Metrics.Count.Should().Be(4);
            updated.Timestamp.Should().Be(sample3.Timestamp);

            updated.Metric("a").Value.Should().Be(3);
            updated.Metric("b").Value.Should().Be(4);
            updated.Metric("c").Value.Should().Be(5);
            updated.Metric("d").Value.Should().Be(6);

            updated.Metric("a").SmoothValue.Should().BeApproximately(1.512, epsilon);
            updated.Metric("b").SmoothValue.Should().BeApproximately(4.000, epsilon);
            updated.Metric("c").SmoothValue.Should().BeApproximately(5.000, epsilon);
            updated.Metric("d").SmoothValue.Should().BeApproximately(6.000, epsilon);
        }
    }
}
