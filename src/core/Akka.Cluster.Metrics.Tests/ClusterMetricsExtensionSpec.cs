//-----------------------------------------------------------------------
// <copyright file="ClusterMetricsExtensionSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.TestKit;
using Akka.Util.Internal;
using Xunit;
using FluentAssertions;

namespace Akka.Cluster.Metrics.Tests
{
    public class ClusterMetricsExtensionSpec : AkkaSpec
    {
        private Cluster cluster;
        private ClusterMetricsExtension extension;
        private ClusterMetricsView metricsView;
        private TimeSpan sampleInterval;

        // This is a single node test.
        private const int nodeCount = 1;

        // Limit collector sample count.
        private const int sampleCount = 10;

        // Metrics verification precision.
        private const double epsilon = 0.001;

        public ClusterMetricsExtensionSpec()
        {
            cluster = Cluster.Get(Sys);
            extension = new ClusterMetricsExtension(Sys.AsInstanceOf<ExtendedActorSystem>());
            metricsView = new ClusterMetricsView(Sys.AsInstanceOf<ExtendedActorSystem>());
            sampleInterval = extension.Settings.CollectorSampleInterval;
        }

        private int MetricsNodeCount()
        {
            return metricsView.ClusterMetrics.Count;
        }

        private int MetricsHistorySize()
        {
            return metricsView.MetricsHistory.Count;
        }

        [Fact]
        public void Metric_Extension_must_collect_metrics_after_start_command()
        {
            extension.Supervisor.Tell(CollectionStartMessage.Instance);
            AwaitAssert(() => MetricsNodeCount().Should().Be(nodeCount));
        }
    }
}
