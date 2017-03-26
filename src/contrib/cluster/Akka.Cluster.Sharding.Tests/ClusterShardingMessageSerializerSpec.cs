//-----------------------------------------------------------------------
// <copyright file="ClusterShardingMessageSerializerSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using Akka.Actor;
using Akka.TestKit;
using Xunit;

namespace Akka.Cluster.Sharding.Tests
{
    public class ClusterShardingMessageSerializerSpec : AkkaSpec
    {
        [Fact]
        public void Can_serialize_CoordinatorState()
        {
            var region1 = Sys.ActorOf(Props.Empty, "region1");
            var region2 = Sys.ActorOf(Props.Empty, "region2");
            var region3 = Sys.ActorOf(Props.Empty, "region3");
            var regionProxy1 = Sys.ActorOf(Props.Empty, "regionProxy1");
            var regionProxy2 = Sys.ActorOf(Props.Empty, "regionProxy2");

            var shards = new Dictionary<string, IActorRef>
            {
                ["a"] = region1,
                ["b"] = region2,
                ["c"] = region3
            }.ToImmutableDictionary();

            var regions = new Dictionary<IActorRef, IImmutableList<string>>
            {
                [region1] = ImmutableArray.Create("a"),
                [region2] = ImmutableArray.Create("b", "c"),
                [region3] = ImmutableArray<string>.Empty
            }.ToImmutableDictionary();

            var message = new PersistentShardCoordinator.State(
                shards,
                regions,
                ImmutableHashSet.Create(regionProxy1, regionProxy2),
                ImmutableHashSet.Create("d"));

            AssertEqual(message);
        }

        [Fact]
        public void Can_serialize_ShardIdMessage()
        {

        }

        [Fact]
        public void Can_serialize_ShardHomeAllocated()
        {
            var shard = Sys.ActorOf(Props.Empty, "region1");
            var message = new PersistentShardCoordinator.ShardHomeAllocated("b", shard);
            AssertEqual(message);
        }

        [Fact]
        public void Can_serialize_ShardHome()
        {
            var shard = Sys.ActorOf(Props.Empty, "region1");
            var message = new PersistentShardCoordinator.ShardHome("a", shard);
            AssertEqual(message);
        }

        [Fact]
        public void Can_serialize_EntityState()
        {
            var entries = ImmutableHashSet.Create("e1", "e2", "e3");
            var message = new Shard.ShardState(entries);
            AssertEqual(message);
        }

        [Fact]
        public void Can_serialize_EntityStarted()
        {
            var message = new Shard.EntityStarted("e1");
            AssertEqual(message);
        }

        [Fact]
        public void Can_serialize_EntityStopped()
        {
            var message = new Shard.EntityStopped("e1");
            AssertEqual(message);
        }

        [Fact]
        public void Can_serialize_ShardStats()
        {
            var message = new Shard.ShardStats("a", 23);
            AssertEqual(message);
        }

        private T AssertAndReturn<T>(T message)
        {
            var serializer = Sys.Serialization.FindSerializerFor(message);
            var serialized = serializer.ToBinary(message);
            var result = serializer.FromBinary(serialized, typeof(T));
            return (T)result;
        }

        private void AssertEqual<T>(T message)
        {
            var deserialized = AssertAndReturn(message);
            Assert.Equal(message, deserialized);
        }
    }
}