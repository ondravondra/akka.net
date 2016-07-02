// <copyright file="MetricsCollector.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Akka.Actor;
using Akka.Configuration;
using Akka.Util.Internal;

namespace Akka.Cluster.Metrics
{
    /// <summary>
    /// Implementations of cluster system metrics implement this interface
    /// </summary>
    public interface IMetricsCollector : IDisposable
    {
        /// <summary>
        /// Sample and collects new data points.
        /// This method is invoked periodically and should return
        /// current metrics for this node.
        /// </summary>
        NodeMetrics Sample();
    }

    /// <summary>
    /// INTERNAL API
    /// Factory to create a configured <see cref="IMetricsCollector"/>.
    /// </summary>
    internal static class MetricsCollector
    {
        public static IMetricsCollector Get(ExtendedActorSystem system)
        {
            var log = system.Log;
            var settings = new ClusterMetricsSettings(system.Settings.Config);

            var fqcn = settings.CollectorProvider;
            if (fqcn == typeof(PerformanceCounterMetricsCollector).AssemblyQualifiedName) return new PerformanceCounterMetricsCollector(system);

            var metricsCollectorClass = Type.GetType(fqcn);
            if (metricsCollectorClass == null)
            {
                throw new ConfigurationException(string.Format("Could not create custom metrics collector {0}", fqcn));
            }

            try
            {
                var metricsCollector = (IMetricsCollector)Activator.CreateInstance(metricsCollectorClass, system);
                return metricsCollector;
            }
            catch (Exception ex)
            {
                throw new ConfigurationException(string.Format("Could not create custom metrics collector {0} because: {1}", fqcn, ex.Message));
            }
        }
    }

    /// <summary>
    /// Loads Windows system metrics through Windows Performance Counters
    /// </summary>
    internal class PerformanceCounterMetricsCollector : IMetricsCollector
    {
        public PerformanceCounterMetricsCollector(Address address, double decayFactor)
        {
            DecayFactor = decayFactor;
            Address = address;
        }

        private PerformanceCounterMetricsCollector(Address address, ClusterMetricsSettings settings) 
            : this(address, EWMA.CalculateAlpha(settings.CollectorMovingAverageHalfLife, settings.CollectorSampleInterval))
        {
        }

        /// <summary>
        /// This constructor is used when creating an instance from configured fully-qualified name
        /// </summary>
        public PerformanceCounterMetricsCollector(ActorSystem system)
            : this(Cluster.Get(system).SelfAddress, new ClusterMetricsExtension(system.AsInstanceOf<ExtendedActorSystem>()).Settings)
        {
        }

        #region Performance counters

        private PerformanceCounter _systemLoadAverageCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
        private PerformanceCounter _systemAvailableMemory = new PerformanceCounter("Memory", "Available MBytes", true);

        private static readonly bool IsRunningOnMono = Type.GetType("Mono.Runtime") != null;


        // Mono doesn't support Microsoft.VisualBasic, so need an alternative way of sampling this value
        // see http://stackoverflow.com/questions/105031/how-do-you-get-total-amount-of-ram-the-computer-has
        private PerformanceCounter _monoSystemMaxMemory = IsRunningOnMono
            ? new PerformanceCounter("Mono Memory", "Total Physical Memory")
            : null;


        #endregion

        public Address Address { get; private set; }

        public Double DecayFactor { get; private set; }

        public ImmutableHashSet<Metric> Metrics()
        {
            return ImmutableHashSet.Create(SystemLoadAverage(), Processors(), SystemMaxMemory(), SystemMemoryAvailable(), ClrProcessMemoryUsed());
        }

        /// <summary>
        /// Samples and collects new data points.
        /// Create a new instance each time.
        /// </summary>
        public NodeMetrics Sample()
        {
            return new NodeMetrics(Address, StandardMetrics.NewTimestamp(), Metrics());
        }

        #region Metric collection methods

        /// <summary>
        /// Returns the system load average. Creates a new instance each time.
        /// </summary>
        public Metric SystemLoadAverage()
        {
            return Metric.Create(StandardMetrics.SystemLoadAverage, _systemLoadAverageCounter.NextValue());
        }

        /// <summary>
        /// Returns the number of available processors. Creates a new instance each time.
        /// </summary>
        public Metric Processors()
        {
            return Metric.Create(StandardMetrics.Processors, Environment.ProcessorCount, null);
        }



        /// <summary>
        /// Gets the amount of memory used by this particular CLR process. Creates a new instance each time.
        /// </summary>
        public Metric ClrProcessMemoryUsed()
        {
            return Metric.Create(StandardMetrics.ClrProcessMemoryUsed, Process.GetCurrentProcess().WorkingSet64,
                DecayFactor);
        }

        /// <summary>
        /// Gets the amount of system memory available. Creates a new instance each time.
        /// </summary>
        public Metric SystemMemoryAvailable()
        {
            return Metric.Create(StandardMetrics.SystemMemoryAvailable, _systemAvailableMemory.NextValue(), DecayFactor);
        }

        /// <summary>
        /// Gets the total amount of system memory. Creates a new instance each time.
        /// </summary>
        public Metric SystemMaxMemory()
        {
            return Metric.Create(StandardMetrics.SystemMemoryMax,
                IsRunningOnMono
                    ? _monoSystemMaxMemory.RawValue
                    : GetVbTotalPhysicalMemory());
        }

        double GetVbTotalPhysicalMemory()
        {
#if __MonoCS__
            throw new NotImplementedException();
#else
            return new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
#endif
        }

        #endregion


        #region IDisposable members

        public void Dispose()
        {
            _systemAvailableMemory.Dispose();
            _systemLoadAverageCounter.Dispose();
        }

        #endregion
    }
}
