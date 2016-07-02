using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.Cluster.Metrics
{
    /// <summary>
    /// Metrics key/value
    /// Equality of metric based on its name
    /// </summary>
    public sealed class Metric : MetricNumericConverter
    {
        /// <summary>
        /// Equality of Metric is based on its name.
        /// </summary>
        /// <param name="name">the metric name</param>
        /// <param name="value">the metric value, which must be a valid numerical value, a valid value is neither negative nor NaN/Infinite.</param>
        /// <param name="average">the data stream of the metric value, for trending over time. Metrics that are already averages (e.g.system load average) or finite (e.g. as number of processors), are not trended.</param>
        internal Metric(string name, double value, EWMA average = null)
        {
            Name = name;
            Value = value;
            Average = average;
            
            // TODO: doesn't work
            //if (!Defined(value))
            //    throw new ArgumentNullException(nameof(name), $"Invalid Metric {name} value {value}");
        }

        public string Name { get; }

        public double Value { get; }

        public EWMA Average { get; }

        /// <summary>
        /// The numerical value of the average, if defined, otherwise the latest value
        /// </summary>
        public double SmoothValue
        {
            get
            {
                return Average != null ? Average.Value : Value;
            }
        }

        /// <summary>
        /// Returns true if the value is smoothed
        /// </summary>
        public bool IsSmooth
        {
            get { return Average != null; }
        }

        public bool SameAs(Metric that)
        {
            return Name == that.Name;
        }

        #region Operators

        public static Metric operator +(Metric original, Metric latest)
        {
            if (original.SameAs(latest))
            {
                if (original.Average != null) return new Metric(original.Name, latest.Value, original.Average + latest.Value);
                if (latest.Average != null) return new Metric(original.Name, latest.Value, latest.Average);
                return new Metric(original.Name, latest.Value);
            }
            return original;
        }

        #endregion

        #region Equality

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            var other = obj as Metric;
            if (other == null) return false;
            return string.Equals(Name, other.Name);
        }

        #endregion

        #region Static methods

        /// <summary>
        /// Creates a new <see cref="Metric"/> instance if <paramref name="value"/> is valid, otherwise
        /// returns null. Invalid numeric values are negative and NaN/Infinite.
        /// </summary>
        public static Metric Create(string name, double value, double? decayFactor = null)
        {
            return Defined(value) ? new Metric(name, value, CreateEWMA(value, decayFactor)) : null;
        }

        // ReSharper disable once InconsistentNaming
        public static EWMA CreateEWMA(double value, double? decayFactor = null)
        {
            return decayFactor.HasValue ? new EWMA(value, decayFactor.Value) : null;
        }

        #endregion
    }

    /// <summary>
    /// INTERNAL API
    /// 
    /// Encapsulates evaluation of validity of metric values, conversion of an actual metric value to
    /// an <see cref="Metric"/> for consumption by subscribed cluster entities.
    /// </summary>
    public abstract class MetricNumericConverter
    {
        /// <summary>
        /// A defined value is greater than zero and not NaN / Infinity
        /// </summary>
        public static bool Defined(double value)
        {
            return (value >= 0) && !(Double.IsNaN(value) || Double.IsInfinity(value));
        }

        /// <summary>
        /// Here in .NET-istan, we're going to use <see cref="double"/> for all metrics since we
        /// don't have convenient base classes for denoting general numeric types like Scala.
        /// 
        /// If a specific metrics method needs an integral data type, it should convert down from double.
        /// </summary>
        public static double ConvertNumber(object from)
        {
            if (from is double) return (double)from;
            if (from is float) return Convert.ToDouble((float)from);
            if (from is int) return Convert.ToDouble((int)from);
            if (from is uint) return Convert.ToDouble((uint)from);
            if (from is long) return Convert.ToDouble((long)from);
            if (from is ulong) return Convert.ToDouble((ulong)from);
            throw new ArgumentException(string.Format("Not a number [{0}]", from), "from");
        }
    }

    /// <summary>
    /// Definitions of the built-in standard metrics
    /// 
    /// The following extractors and data structures make it easy to consume the
    /// <see cref="NodeMetrics"/> in for example load balancers.
    /// </summary>
    internal static class StandardMetrics
    {
        // Constants for memory-related Metric names (accounting for differences between JVM and .NET)
        public const string SystemMemoryMax = "system-memory-max";
        public const string ClrProcessMemoryUsed = "clr-process-memory-used"; //memory for the individual .NET process running Akka.NET
        public const string SystemMemoryAvailable = "system-memory-available";

        //Constants for cpu-related Metric names
        public const string SystemLoadAverage = "system-load-average";
        public const string Processors = "processors";
        public const string CpuCombined = "cpu-combined";

        public static long NewTimestamp()
        {
            return DateTime.UtcNow.Ticks;
        }

        public sealed class SystemMemory
        {
            public Address Address { get; private set; }
            public long Timestamp { get; private set; }
            public long Used { get; private set; }
            public long Available { get; private set; }
            public long? Max { get; private set; }

            public SystemMemory(Address address, long timestamp, long used, long available, long? max = null)
            {
                Address = address;
                Timestamp = timestamp;
                Used = used;
                Available = available;
                Max = max;

                if (!(used > 0L)) throw new ArgumentOutOfRangeException(nameof(used), "CLR heap memory expected to be > 0 bytes");
                if (Max.HasValue && !(Max.Value > 0)) throw new ArgumentOutOfRangeException(nameof(max), "system max memory expected to be > 0 bytes");
            }

            #region Static methods

            public static SystemMemory ExtractSystemMemory(NodeMetrics nodeMetrics)
            {
                var used = nodeMetrics.Metric(ClrProcessMemoryUsed);
                var available = nodeMetrics.Metric(SystemMemoryAvailable);
                if (used == null || available == null) return null;
                var max = nodeMetrics.Metric(SystemMemoryAvailable) != null ? (long?)Convert.ToInt64(nodeMetrics.Metric(SystemMemoryMax).SmoothValue) : null;
                return new SystemMemory(nodeMetrics.Address, nodeMetrics.Timestamp,
                    Convert.ToInt64(used.SmoothValue), Convert.ToInt64(available.SmoothValue), max);
            }

            #endregion
        }

        /**
        * @param address <see cref="Akka.Actor.Address"/> of the node the metrics are gathered at
        * @param timestamp the time of sampling, in milliseconds since midnight, January 1, 1970 UTC
        * @param systemLoadAverage OS-specific average load on the CPUs in the system, for the past 1 minute,
        *    The system is possibly nearing a bottleneck if the system load average is nearing number of cpus/cores.
        * @param cpuCombined combined CPU sum of User + Sys + Nice + Wait, in percentage ([0.0 - 1.0]. This
        *   metric can describe the amount of time the CPU spent executing code during n-interval and how
        *   much more it could theoretically.
        * @param processors the number of available processors
        */
        public sealed class Cpu
        {
            public Address Address { get; private set; }
            public long Timestamp { get; private set; }
            public int Cores { get; private set; }
            public double? SystemLoadAverageMeasurement { get; private set; }
            public double? CpuCombinedMeasurement { get; private set; }

            public Cpu(Address address, long timestamp, int cores, double? systemLoadAverage = null, double? cpuCombined = null)
            {
                Address = address;
                Timestamp = timestamp;
                Cores = cores;
                SystemLoadAverageMeasurement = systemLoadAverage;
                CpuCombinedMeasurement = cpuCombined;
            }

            #region Static methods

            /// <summary>
            /// Given a <see cref="NodeMetrics"/> it returns the <see cref="Cpu"/> data of the nodeMetrics
            /// contains the necessary cpu metrics.
            /// </summary>
            public static Cpu ExtractCpu(NodeMetrics nodeMetrics)
            {
                var processors = nodeMetrics.Metric(Processors);
                if (processors == null) return null;
                var systemLoadAverage = nodeMetrics.Metric(SystemLoadAverage) != null ? (double?)nodeMetrics.Metric(SystemLoadAverage).SmoothValue : null;
                var cpuCombined = nodeMetrics.Metric(CpuCombined) != null
                     ? (double?)nodeMetrics.Metric(CpuCombined).SmoothValue
                     : null;

                return new Cpu(nodeMetrics.Address, nodeMetrics.Timestamp, Convert.ToInt32(processors.Value), systemLoadAverage, cpuCombined);
            }

            #endregion
        }
    }

    /// <summary>
    /// The snapshot of current sampled health metrics for any monitored process.
    /// Collected and gossiped at regular intervals for dynamic cluster management strategies.
    /// 
    /// Equality of <see cref="NodeMetrics"/> is based on its <see cref="Address"/>.
    /// </summary>
    public sealed class NodeMetrics
    {
        /// <summary>
        /// The snapshot of current sampled health metrics for any monitored process.
        /// Collected and gossiped at regular intervals for dynamic cluster management strategies.
        /// </summary>
        /// <param name="address"><see cref="Akka.Actor.Address"/> of the node the metrics are gathered at</param>
        /// <param name="timestamp">the time of sampling, in milliseconds since midnight, January 1, 1970 UTC</param>
        /// <param name="metrics">the set of sampled <see cref="Akka.Cluster.Metrics.Metric"/></param>
        public NodeMetrics(Address address, long timestamp, IImmutableSet<Metric> metrics = null)
        {
            Address = address;
            Timestamp = timestamp;
            Metrics = metrics ?? ImmutableHashSet<Metric>.Empty;
        }

        /// <summary>
        /// <see cref="Akka.Actor.Address"/> of the node the metrics are gathered at
        /// </summary>
        public Address Address { get; }


        /// <summary>
        /// the time of sampling, in milliseconds since midnight, January 1, 1970 UTC
        /// </summary>
        public long Timestamp { get; }

        /// <summary>
        /// the set of sampled <see cref="Akka.Cluster.Metrics.Metric"/>
        /// </summary>
        public IImmutableSet<Metric> Metrics { get; }

        /// <summary>
        /// Returns the most recent data
        /// </summary>
        public NodeMetrics Merge(NodeMetrics that)
        {
            if (!Address.Equals(that.Address))
                throw new ArgumentException($"Merge is only allowed for the same address. {Address} != {that.Address}");

            if (Timestamp >= that.Timestamp) return this; // that is older
            
            // equality is based on the name of the Metric and Set doesn't replace existing element
            return Copy(metrics: that.Metrics.Union(Metrics), timestamp: that.Timestamp);
        }

        /// <summary>
        /// Returns the most recent data with <see cref="EWMA"/> averaging.
        /// </summary>
        public NodeMetrics Update(NodeMetrics that)
        {
            if (!Address.Equals(that.Address))
                throw new ArgumentException($"Update is only allowed for the same address. {Address} != {that.Address}");

            NodeMetrics latestNode;
            NodeMetrics currentNode;

            if (this.Timestamp >= that.Timestamp)
            {
                latestNode = this;
                currentNode = that;
            }
            else
            {
                latestNode = that;
                currentNode = this;
            }

            var updated = (from latest in latestNode.Metrics
                           from current in currentNode.Metrics
                           where latest.SameAs(current)
                           select current + latest).ToList();
            // Append metrics missing from either latest or current.
            // Equality is based on the [[Metric.name]] and [[Set]] doesn't replace existing elements.
            var merged = updated.Union(latestNode.Metrics).Union(currentNode.Metrics).ToImmutableHashSet();
            return Copy(metrics: merged, timestamp: latestNode.Timestamp);
        }

        /// <summary>
        /// Return the metric that matches <paramref name="key"/>. Returns null if not found.
        /// </summary>
        public Metric Metric(string key)
        {
            return Metrics.FirstOrDefault(metric => metric.Name.Equals(key));
        }

        #region Equality
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((NodeMetrics)obj);
        }

        protected bool Equals(NodeMetrics other)
        {
            return Address.Equals(other.Address);
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode();
        }
        #endregion

        public NodeMetrics Copy(Address address = null, long? timestamp = null, IImmutableSet<Metric> metrics = null)
        {
            return new NodeMetrics(address ?? Address, timestamp.HasValue ? timestamp.Value : Timestamp, metrics ?? Metrics);
        }
    }

    /// <summary>
    /// INTERNAL API
    /// </summary>
    internal sealed class MetricsGossip
    {
        public MetricsGossip(ImmutableHashSet<NodeMetrics> nodes)
        {
            Nodes = nodes;
        }

        public ImmutableHashSet<NodeMetrics> Nodes { get; }

        /// <summary>
        /// Remove nodes if their correlating node ring members are not <see cref="MemberStatus.Up"/>
        /// </summary>
        public MetricsGossip Remove(Address node)
        {
            return Copy(Nodes.Where(n => !n.Address.Equals(node)).ToImmutableHashSet());
        }

        /// <summary>
        /// Only the nodes that are in the <paramref name="includeNodes"/> set.
        /// </summary>
        public MetricsGossip Filter(ImmutableHashSet<Address> includeNodes)
        {
            return Copy(Nodes.Where(x => includeNodes.Contains(x.Address)).ToImmutableHashSet());
        }

        /// <summary>
        /// Adds new remote <see cref="NodeMetrics"/> and merges existing from a remote gossip.
        /// </summary>
        public MetricsGossip Merge(MetricsGossip otherGossip)
        {
            return otherGossip.Nodes.Aggregate(this, (gossip, metrics) => gossip + metrics);
        }

        /// <summary>
        /// Returns <see cref="NodeMetrics"/> for a node if exists.
        /// </summary>
        public NodeMetrics NodeMetricsFor(Address address)
        {
            return Nodes.FirstOrDefault(x => x.Address == address);
        }

        #region Operators

        /// <summary>
        /// Adds new local <see cref="NodeMetrics"/> or merges an existing one.
        /// </summary>
        public static MetricsGossip operator +(MetricsGossip original, NodeMetrics newNode)
        {
            var existingNodeMetrics = original.NodeMetricsFor(newNode.Address);
            return original.Copy(existingNodeMetrics != null ?
                original.Nodes.Remove(existingNodeMetrics).Add(existingNodeMetrics.Merge(newNode)) :
                original.Nodes.Add(newNode));
        }

        #endregion

        #region Static members

        public static readonly MetricsGossip Empty = new MetricsGossip(ImmutableHashSet.Create<NodeMetrics>());

        #endregion

        #region Equality
        private bool Equals(MetricsGossip other)
        {
            return Nodes.SequenceEqual(other.Nodes);
        }

        public override int GetHashCode()
        {
            return (Nodes != null ? Nodes.GetHashCode() : 0);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MetricsGossip)obj);
        }
        #endregion

        public MetricsGossip Copy(ImmutableHashSet<NodeMetrics> nodes = null)
        {
            return nodes == null ? new MetricsGossip(Nodes.ToImmutableHashSet()) : new MetricsGossip(nodes);
        }
    }
}
