//-----------------------------------------------------------------------
// <copyright file="ClusterMessageSerializerSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using Akka.Actor;
using Akka.Serialization;
using Akka.TestKit;
using Akka.Util.Internal;
using Xunit;
using FluentAssertions;

namespace Akka.Cluster.Metrics.Tests.Proto
{
    public class MessageSerializerSpec : AkkaSpec
    {
        private SerializerWithStringManifest serializer;

        public MessageSerializerSpec() : base(@"akka.actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""")
        {
            serializer = new Akka.Cluster.Metrics.Proto.MessageSerializer(Sys.AsInstanceOf<ExtendedActorSystem>());
        }

        private void CheckSerialization(object obj)
        {
            var blob = serializer.ToBinary(obj);
            var reference = serializer.FromBinary(blob, serializer.Manifest(obj));
            reference.Should().Be(obj);
        }

        private static readonly Member a1 = TestMember.Create(new Address("akka.tcp", "sys", "a", 2552), MemberStatus.Joining);
        private static readonly Member b1 = TestMember.Create(new Address("akka.tcp", "sys", "b", 2552), MemberStatus.Up, ImmutableHashSet.Create<string>("r1"));

        [Fact]
        public void ClusterMessages_must_be_serializable()
        {
            var metricsGossip = new MetricsGossip(ImmutableHashSet.Create(
                new NodeMetrics(a1.Address, 4711, ImmutableHashSet.Create(new Metric("foo", 1.2, null))),
                new NodeMetrics(b1.Address, 4712,
                    ImmutableHashSet.Create(
                        new Metric("foo", 2.1, new EWMA(100.0, 0.18)),
                        new Metric("bar1", Double.MinValue, null),
                        new Metric("bar2", float.MaxValue, null),
                        new Metric("bar3", int.MaxValue, null),
                        new Metric("bar4", long.MaxValue, null), 
                        new Metric("bar5", double.MaxValue, null)))
            ));

            CheckSerialization(new MetricsGossipEnvelope(a1.Address, metricsGossip, true));
        }
    }
}
