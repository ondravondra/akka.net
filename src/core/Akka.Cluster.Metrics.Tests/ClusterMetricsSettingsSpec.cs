//-----------------------------------------------------------------------
// <copyright file="ClusterMetricsSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Dispatch;
using Akka.TestKit;
using Xunit;
using FluentAssertions;

namespace Akka.Cluster.Metrics.Tests
{
    public class ClusterMetricsSettingsSpec : AkkaSpec
    {
        [Fact]
        public void ClusterMetricsSettings_must_be_able_to_parse_generic_metrics_config_elements()
        {
            // TODO: should remove it
            var conf = ClusterMetricsSettings.DefaultConfig();
            var settings = new ClusterMetricsSettings(conf);

            // Extension.
            settings.MetricsDispatcher.Should().Be(Dispatchers.DefaultDispatcherId);
            settings.PeriodicTasksInitialDelay.Should().Be(1.Seconds());

            // Supervisor.
            settings.SupervisorName.Should().Be("cluster-metrics");
            settings.SupervisorStrategyProvider.Should().Be("Akka.Cluster.Metrics.ClusterMetricsStrategy, Akka.Cluster.Metrics");
            // TODO: need equals on config
            //settings.SupervisorStrategyConfiguration.Should().Be(ConfigurationFactory.ParseString("loggingEnabled=true,maxNrOfRetries=3,withinTimeRange=3s"));

            settings.CollectorEnabled.Should().BeTrue();
            settings.CollectorProvider.Should().BeEmpty();
            settings.CollectorSampleInterval.Should().Be(3.Seconds());
            settings.CollectorGossipInterval.Should().Be(3.Seconds());
            settings.CollectorMovingAverageHalfLife.Should().Be(12.Seconds());
        }
    }
}
