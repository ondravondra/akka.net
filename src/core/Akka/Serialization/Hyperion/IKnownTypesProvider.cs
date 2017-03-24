//-----------------------------------------------------------------------
// <copyright file="HyperionSerializer.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch.SysMsg;
using Akka.Routing;

namespace Akka.Serialization
{
    /// <summary>
    /// Interface that can be implemented in order to determine some 
    /// custom logic, that's going to provide a list of types that 
    /// are known to be shared for all corresponding parties during 
    /// remote communication.
    /// </summary>
    public interface IKnownTypesProvider
    {
        IEnumerable<Type> GetKnownTypes();
    }

    internal sealed class NoKnownTypes : IKnownTypesProvider
    {
        public IEnumerable<Type> GetKnownTypes() => new Type[0];
    }

    internal sealed class DefaultKnownTypes : IKnownTypesProvider
    {
        public IEnumerable<Type> GetKnownTypes()
        {
            return new[]
            {
                typeof(ActorPath),
                typeof(Identify),
                typeof(ActorIdentity),
                typeof(PoisonPill),
                typeof(Watch),
                typeof(Unwatch),
                typeof(DeathWatchNotification),
                typeof(Terminate),
                typeof(Supervise),
                typeof(Address),
                typeof(Config),
                typeof(Decider),
                typeof(Directive),
                typeof(IDecider),
                typeof(OneForOneStrategy),
                typeof(AllForOneStrategy),
                typeof(DefaultResizer),
                typeof(RoundRobinPool),
                typeof(RoundRobinGroup),
                typeof(RandomGroup),
                typeof(RandomPool),
                typeof(ConsistentHashingPool),
                typeof(ConsistentHashingGroup),
                typeof(TailChoppingPool),
                typeof(TailChoppingGroup),
                typeof(ScatterGatherFirstCompletedPool),
                typeof(ScatterGatherFirstCompletedGroup),
                typeof(SmallestMailboxPool),
                typeof(Kill),
                typeof(ActorSelectionMessage),
                typeof(SelectChildName)
            };
        }
    }
}