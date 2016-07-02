//-----------------------------------------------------------------------
// <copyright file="ClusterMetricsExtension.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Configuration;

namespace Akka.Cluster.Metrics
{
    /// <summary>
    /// Cluster metrics extension.
    /// 
    /// Cluster metrics is primarily for load-balancing of nodes.It controls metrics sampling
    /// at a regular frequency, prepares highly variable data for further analysis by other entities,
    /// and publishes the latest cluster metrics data around the node ring and local eventStream
    /// to assist in determining the need to redirect traffic to the least-loaded nodes.
    /// 
    /// Metrics sampling is delegated to the <see cref="MetricsCollector"/>.
    /// 
    /// Smoothing of the data for each monitored process is delegated to the
    /// <see cref="EWMA"/> for exponential weighted moving average.
    /// </summary>
    public class ClusterMetricsExtension : IExtension
    {
        public ExtendedActorSystem System { get; }

        /// <summary>
        /// Metrics extension configuration.
        /// </summary>
        public ClusterMetricsSettings Settings { get; }

        public ClusterMetricsExtension(ExtendedActorSystem system)
        {
            System = system;
            Settings = new ClusterMetricsSettings(system.Settings.Config);

            Supervisor = system.SystemActorOf(Props
                .Create<ClusterMetricsSupervisor>()
                .WithDispatcher(Settings.MetricsDispatcher)
                .WithDeploy(Deploy.Local), Settings.SupervisorName);

            try
            {
                var strategyType = Type.GetType(Settings.SupervisorStrategyProvider, true);
                Strategy = (SupervisorStrategy)Activator.CreateInstance(strategyType, Settings.SupervisorStrategyConfiguration);
            }
            catch (Exception)
            {
                system.Log.Error($"Configured strategy provider {Settings.SupervisorStrategyProvider} failed to load, using default {typeof(ClusterMetricsStrategy).Name}.");
                Strategy = new ClusterMetricsStrategy(Settings.SupervisorStrategyConfiguration);
            }
        }

        internal SupervisorStrategy Strategy { get; }

        /// <summary>
        /// Supervisor actor.
        /// Accepts subtypes of <see cref="ICollectionControlMessage"/>s to manage metrics collection at runtime.
        /// </summary>
        public IActorRef Supervisor { get; }

        /// <summary>
        /// Subscribe user metrics listener actor unto <see cref="IClusterMetricsEvent"/>
        /// events published by extension on the system event bus.
        /// </summary>
        /// <param name="metricsListener">Metric listener actor</param>
        public void Subscribe(IActorRef metricsListener)
        {
            System.EventStream.Subscribe(metricsListener, typeof(IClusterMetricsEvent));
        }

        /// <summary>
        /// Unsubscribe user metrics listener actor from <see cref="IClusterMetricsEvent"/>
        /// events published by extension on the system event bus.
        /// </summary>
        /// <param name="metricsListener">Metric listener actor</param>
        public void Unsubscribe(IActorRef metricsListener)
        {
            System.EventStream.Unsubscribe(metricsListener, typeof(IClusterMetricsEvent));
        }
    }

    /// <summary>
    /// Cluster metrics extension provider.
    /// </summary>
    public class ClusterMetricsExtensionProvider : ExtensionIdProvider<ClusterMetricsExtension>
    {
        public override ClusterMetricsExtension CreateExtension(ExtendedActorSystem system)
        {
            return new ClusterMetricsExtension(system);
        }
    }
}
