using Akka.Actor;
using Akka.TestKit;
using Akka.Util.Internal;

namespace Akka.Cluster.Metrics.Tests
{
    /// <summary>
    /// Used when testing metrics without full Cluster
    /// </summary>
    public class MetricsCollectorFactory : AkkaSpec //TODO: if we inherit from ClusterSpecBase, tests never run - must be a config chaining problem
    {
        public MetricsCollectorFactory()
            : base(MetricsEnabledSpec.Config)
        {
            ExtendedActorSystem = Sys.AsInstanceOf<ExtendedActorSystem>();
            SelfAddress = ExtendedActorSystem.Provider.RootPath.Address;
        }

        protected Address SelfAddress;
        protected ExtendedActorSystem ExtendedActorSystem;
        protected readonly double DefaultDecayFactor = 2.0/(1 + 1.0);

        protected IMetricsCollector CreateMetricsCollector()
        {
            return new PerformanceCounterMetricsCollector(SelfAddress, DefaultDecayFactor);
        }
    }
}