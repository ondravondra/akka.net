//-----------------------------------------------------------------------
// <copyright file="ClusterMetricsSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Dispatch;

namespace Akka.Cluster.Metrics
{
    public sealed class ClusterMetricsSettings
    {
        public static Config DefaultConfig()
        {
            return ConfigurationFactory.FromResource<ClusterMetricsSettings>("Akka.Cluster.Metrics.reference.conf");
        }

        public ClusterMetricsSettings(Config config)
        {
            var cc = config.GetConfig("akka.cluster.metrics");

            // Extension
            MetricsDispatcher = cc.GetString("dispatcher");
            if (string.IsNullOrEmpty(MetricsDispatcher))
                MetricsDispatcher = Dispatchers.DefaultDispatcherId;
            PeriodicTasksInitialDelay = cc.GetTimeSpan("periodic-tasks-initial-delay");

            // Supervisor
            SupervisorName = cc.GetString("supervisor.name");
            SupervisorStrategyProvider = cc.GetString("supervisor.strategy.provider");
            SupervisorStrategyConfiguration = cc.GetConfig("supervisor.strategy.configuration");

            // Collector
            CollectorEnabled = cc.GetBoolean("collector.enabled");
            CollectorProvider = cc.GetString("collector.provider");
            CollectorFallback = cc.GetBoolean("collector.fallback");

            CollectorSampleInterval = cc.GetTimeSpan("collector.sample-interval");
            if (CollectorSampleInterval == TimeSpan.Zero)
                throw new ArgumentException("collector.sample-interval must be > 0");

            CollectorGossipInterval = cc.GetTimeSpan("collector.gossip-interval");
            if (CollectorGossipInterval == TimeSpan.Zero)
                throw new ArgumentException("collector.gossip-interval must be > 0");

            CollectorMovingAverageHalfLife = cc.GetTimeSpan("collector.moving-average-half-life");
            if (CollectorMovingAverageHalfLife == TimeSpan.Zero)
                throw new ArgumentException("collector.moving-average-half-life must be > 0");
        }

        public string MetricsDispatcher { get; }

        public TimeSpan PeriodicTasksInitialDelay { get; }

        public string SupervisorName { get; }

        public string SupervisorStrategyProvider { get; }

        public Config SupervisorStrategyConfiguration { get; }

        public bool CollectorEnabled { get; }

        public string CollectorProvider { get; }

        public bool CollectorFallback { get; }

        public TimeSpan CollectorSampleInterval { get; }

        public TimeSpan CollectorGossipInterval { get; }

        public TimeSpan CollectorMovingAverageHalfLife { get; }
    }
}
