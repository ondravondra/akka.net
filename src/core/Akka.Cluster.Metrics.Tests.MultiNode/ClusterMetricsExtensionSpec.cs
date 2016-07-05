//-----------------------------------------------------------------------
// <copyright file="ClusterMetricsExtensionSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Cluster.TestKit;
using Akka.Configuration;
using Akka.Remote.TestKit;

namespace Akka.Cluster.Metrics.Tests.MultiNode
{
    public class ClusterMetricsDisabledConfig : ClusterMetricsExtensionConfig
    {
        public ClusterMetricsDisabledConfig()
        {
            CommonConfig = ImmutableHashSet.Create(
                CustomLogging(),
                DisableMetricsExtension(),
                DebugConfig(false),
                MultiNodeClusterSpec.ClusterConfigWithFailureDetectorPuppet()).Aggregate((conf1, conf2) => conf1.WithFallback(conf2));
        }
    }

    public class ClusterMetricsEnabledConfig : ClusterMetricsExtensionConfig
    {
        public ClusterMetricsEnabledConfig()
        {
            CommonConfig = ImmutableHashSet.Create(
                CustomLogging(),
                EnableMetricsExtension(),
                DebugConfig(false),
                MultiNodeClusterSpec.ClusterConfigWithFailureDetectorPuppet()).Aggregate((conf1, conf2) => conf1.WithFallback(conf2));
        }
    }

    public abstract class ClusterMetricsExtensionConfig : MultiNodeConfig
    {
        public RoleName Node1 { get; }
        public RoleName Node2 { get; }
        public RoleName Node3 { get; }
        public RoleName Node4 { get; }
        public RoleName Node5 { get; }

        public ClusterMetricsExtensionConfig()
        {
            Node1 = Role("node-1");
            Node2 = Role("node-2");
            Node3 = Role("node-3");
            Node4 = Role("node-4");
            Node4 = Role("node-5");
        }

        public IEnumerable<RoleName> NodeList()
        {
            return ImmutableHashSet.Create(Node1, Node2, Node3, Node4, Node5);
        }

        public Config EnableMetricsExtension()
        {
            return ConfigurationFactory.ParseString(@"
                akka.extensions=[""akka.cluster.metrics.ClusterMetricsExtension""]
                akka.cluster.metrics.collector.enabled = on
            ");
        }

        public Config DisableMetricsExtension()
        {
            return ConfigurationFactory.ParseString(@"
                akka.extensions=[""akka.cluster.metrics.ClusterMetricsExtension""]
                akka.cluster.metrics.collector.enabled = off
            ");
        }

        public Config CustomLogging()
        {
            return ConfigurationFactory.ParseString(@"

            ");
        }
    }

    public class ClusterMetricsEnabledMultiNode1 : ClusterMetricsEnabledSpec { }
    public class ClusterMetricsEnabledMultiNode2 : ClusterMetricsEnabledSpec { }
    public class ClusterMetricsEnabledMultiNode3 : ClusterMetricsEnabledSpec { }
    public class ClusterMetricsEnabledMultiNode4 : ClusterMetricsEnabledSpec { }
    public class ClusterMetricsEnabledMultiNode5 : ClusterMetricsEnabledSpec { }

    public abstract class ClusterMetricsEnabledSpec : MultiNodeClusterSpec
    {
        private readonly ClusterMetricsEnabledConfig _config;

        protected ClusterMetricsEnabledSpec() : base(new ClusterMetricsEnabledConfig())
        {
        }

        protected ClusterMetricsEnabledSpec(ClusterMetricsEnabledConfig config) : base(config)
        {
            _config = config;
        }

        [MultiNodeFact]
        public void ClusterMetricsEnabledSpecs()
        {
            Cluster_metrics_must_periodically_collect_metrics_on_each_node_publish_to_the_event_stream_and_gossip_metrics_around_the_node_ring();
            Cluster_metrics_must_reflect_the_correct_number_of_node_metrics_in_cluster_view();
        }

        public void Cluster_metrics_must_periodically_collect_metrics_on_each_node_publish_to_the_event_stream_and_gossip_metrics_around_the_node_ring()
        {
            
        }

        public void Cluster_metrics_must_reflect_the_correct_number_of_node_metrics_in_cluster_view()
        {

        }
    }

    public class ClusterMetricsDisabledMultiNode1 : ClusterMetricsDisabledSpec { }
    public class ClusterMetricsDisabledMultiNode2 : ClusterMetricsDisabledSpec { }
    public class ClusterMetricsDisabledMultiNode3 : ClusterMetricsDisabledSpec { }
    public class ClusterMetricsDisabledMultiNode4 : ClusterMetricsDisabledSpec { }
    public class ClusterMetricsDisabledMultiNode5 : ClusterMetricsDisabledSpec { }

    public abstract class ClusterMetricsDisabledSpec : MultiNodeClusterSpec
    {
        private readonly ClusterMetricsDisabledConfig _config;

        protected ClusterMetricsDisabledSpec() : base(new ClusterMetricsDisabledConfig())
        {
        }

        protected ClusterMetricsDisabledSpec(ClusterMetricsDisabledConfig config) : base(config)
        {
            _config = config;
        }

        [MultiNodeFact]
        public void ClusterMetricsDisabledSpecs()
        {
            Cluster_metrics_must_not_collect_metrics_not_publish_metrics_events_and_not_gossip_metrics();
        }

        public void Cluster_metrics_must_not_collect_metrics_not_publish_metrics_events_and_not_gossip_metrics()
        {

        }

    }
}
