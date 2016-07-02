//-----------------------------------------------------------------------
// <copyright file="ClusterMessageSerializer.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Akka.Actor;
using Akka.Serialization;
using Akka.Util.Internal;
using Google.ProtocolBuffers;
using Address = Akka.Cluster.Metrics.Proto.Msg.Address;
using MetricsGossipEnvelope = Akka.Cluster.Metrics.Proto.Msg.MetricsGossipEnvelope;

namespace Akka.Cluster.Metrics.Proto
{
    /// <summary>
    /// Protobuff serializer for cluster messages
    /// </summary>
    internal class MessageSerializer : SerializerWithStringManifest
    {
        public MessageSerializer(ExtendedActorSystem system) : base(system)
        {
        }

        private const int BufferSize = 1024 * 4;
        private const string MetricsGossipEnvelopeManifest = "a";

        public override string Manifest(object obj)
        {
            if (obj is MetricsGossipEnvelope)
                return MetricsGossipEnvelopeManifest;

            throw new ArgumentException($"Can't serialize object of type {obj.GetType()} in {typeof(MessageSerializer).Name}");
        }

        public override byte[] ToBinary(object obj)
        {
            if (obj is MetricsGossipEnvelope)
                return Compress(MetricsGossipEnvelopeToProto((MetricsGossipEnvelope)obj));

            throw new ArgumentException($"Can't serialize object of type {obj.GetType()} in {typeof(MessageSerializer).Name}");
        }

        public override object FromBinary(byte[] bytes, string manifest)
        {
            if (manifest.Equals(MetricsGossipEnvelopeManifest))
                return MetricsGossipEnvelopeFromBinary(bytes);

            throw new ArgumentException($"Unimplemented deserialization of message with manifest {manifest} in {typeof(MessageSerializer).Name}");
        }

        /// <summary>
        /// Compresses the protobuf message using GZIP compression
        /// </summary>
        public byte[] Compress(IMessageLite message)
        {
            using (var bos = new MemoryStream(BufferSize))
            using (var gzipStream = new GZipStream(bos, CompressionMode.Compress))
            {
                message.WriteTo(gzipStream);
                gzipStream.Close();
                return bos.ToArray();
            }
        }

        /// <summary>
        /// Decompresses the protobuf message using GZIP compression
        /// </summary>
        public byte[] Decompress(byte[] bytes)
        {
            using(var input = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[BufferSize];
                var bytesRead = input.Read(buffer, 0, BufferSize);
                while (bytesRead > 0)
                {
                    output.Write(buffer,0,bytesRead);
                    bytesRead = input.Read(buffer, 0, BufferSize);
                }
                return output.ToArray();
            }
        }

        #region Private internals

        // we don't care about races here since it's just a cache
        private volatile string _protocolCache = null;
        private volatile string _systemCache = null;

        private Address.Builder AddressToProto(Actor.Address address)
        {
            if (string.IsNullOrEmpty(address.Host) || !address.Port.HasValue)
                throw new ArgumentException(string.Format("Address [{0}] could not be serialized: host or port missing", address));

            return Address.CreateBuilder()
                .SetSystem(address.System)
                .SetHostname(address.Host)
                .SetPort((uint)address.Port.Value)
                .SetProtocol(address.Protocol);
        }

        private Actor.Address AddressFromProto(Address address)
        {
            return new Actor.Address(address.Protocol, address.System, address.Hostname, (int)address.Port);
        }

        private int MapWithErrorMessage<T>(Dictionary<T, int> map, T value, string unknown)
        {
            if (map.ContainsKey(value)) return map[value];
            throw new ArgumentException($"Unknown {unknown} [{value}] in cluster message");
        }

        private Msg.MetricsGossipEnvelope MetricsGossipEnvelopeToProto(MetricsGossipEnvelope envelope)
        {
            var allNodeMetrics = envelope.Gossip.Nodes;
            var allAddresses = allNodeMetrics.Select(x => x.Address).ToList();
            var addressMapping = allAddresses.ZipWithIndex();
            var allMetricNames = allNodeMetrics.Aggregate(ImmutableHashSet<string>.Empty,
                (set, metrics) => set.Union(metrics.Metrics.Select(x => x.Name)));
            var metricNamesMapping = allMetricNames.ZipWithIndex();

            Func<Actor.Address, int> mapAddress = address => MapWithErrorMessage<Actor.Address>(addressMapping, address, "address");
            Func<string, int> mapName = name => MapWithErrorMessage(metricNamesMapping, name, "address");
            Func<EWMA, Msg.NodeMetrics.Types.EWMA.Builder> ewmaToProto = ewma => ewma == null ? null :
                Msg.NodeMetrics.Types.EWMA.CreateBuilder().SetValue(ewma.Value).SetAlpha(ewma.Alpha);

            // we set all metric types as doubles, since we don't have a convenient Number base class like Scala
            Func<double, Msg.NodeMetrics.Types.Number.Builder> numberToProto = d => Msg.NodeMetrics.Types.Number.CreateBuilder()
                .SetType(Msg.NodeMetrics.Types.NumberType.Double)
                .SetValue64((ulong) BitConverter.DoubleToInt64Bits(d));

            Func<Metric, Msg.NodeMetrics.Types.Metric.Builder> metricToProto = metric =>
            {
                var builder =
                    Msg.NodeMetrics.Types.Metric.CreateBuilder()
                        .SetNameIndex(mapName(metric.Name))
                        .SetNumber(numberToProto(metric.Value));
                var ewmaBuilder = ewmaToProto(metric.Average);
                return ewmaBuilder != null ? builder.SetEwma(ewmaBuilder) : builder;
            };

            Func<NodeMetrics, Msg.NodeMetrics.Builder> nodeMetricsToProto = metrics => Msg.NodeMetrics.CreateBuilder()
                .SetAddressIndex(mapAddress(metrics.Address))
                .SetTimestamp(metrics.Timestamp)
                .AddRangeMetrics(metrics.Metrics.Select(x => metricToProto(x).Build()));

            var nodeMetrics = allNodeMetrics.Select(x => nodeMetricsToProto(x).Build());

            return Msg.MetricsGossipEnvelope.CreateBuilder().SetFrom(AddressToProto(envelope.From)).SetGossip(
                Msg.MetricsGossip.CreateBuilder()
                    .AddRangeAllAddresses(allAddresses.Select(x => AddressToProto(x).Build()))
                    .AddRangeAllMetricNames(allMetricNames).AddRangeNodeMetrics(nodeMetrics))
                .SetReply(envelope.Reply)
                .Build();
        }

        private MetricsGossipEnvelope MetricsGossipEnvelopeFromBinary(byte[] bytes)
        {
            return MetricsGossipEnvelopeFromProto(Msg.MetricsGossipEnvelope.ParseFrom(Decompress(bytes)));
        }

        private MetricsGossipEnvelope MetricsGossipEnvelopeFromProto(Msg.MetricsGossipEnvelope envelope)
        {
            var mgossip = envelope.Gossip;
            var addressMapping = mgossip.AllAddressesList.Select(AddressFromProto).ToList();
            var metricNameMapping = mgossip.AllMetricNamesList;

            Func<Msg.NodeMetrics.Types.EWMA, EWMA> ewmaFromProto = ewma => ewma == null ? null : new EWMA(ewma.Value, ewma.Alpha);
            Func<Msg.NodeMetrics.Types.Number, double> numberFromProto =
                number => BitConverter.Int64BitsToDouble((long) number.Value64);
            Func<Msg.NodeMetrics.Types.Metric, Metric> metricFromProto =
                metric =>
                    new Metric(metricNameMapping[metric.NameIndex], numberFromProto(metric.Number),
                        ewmaFromProto(metric.Ewma));
            Func<Msg.NodeMetrics, NodeMetrics> nodeMetricsFromProto = metrics => new NodeMetrics(addressMapping[metrics.AddressIndex], metrics.Timestamp, metrics.MetricsList.Select(metricFromProto).ToImmutableHashSet());

            var nodeMetrics = mgossip.NodeMetricsList.Select(nodeMetricsFromProto).ToImmutableHashSet();

            return new MetricsGossipEnvelope(AddressFromProto(envelope.From), new MetricsGossip(nodeMetrics), envelope.Reply);
        }

        #endregion
    }
}
