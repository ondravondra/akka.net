//-----------------------------------------------------------------------
// <copyright file="ClusterMetricsStrategy.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;
using Akka.Configuration;

namespace Akka.Cluster.Metrics
{
    /// <summary>
    /// Default <see cref="ClusterMetricsSupervisor"/> strategy:
    /// A configurable <see cref="OneForOneStrategy"/> with restart-on-throwable decider.
    /// </summary>
    public class ClusterMetricsStrategy : OneForOneStrategy
    {
        public ClusterMetricsStrategy(Config config) 
            : base(
                maxNrOfRetries: config.GetInt("maxNrOfRetries"),
                withinTimeMilliseconds: config.GetTimeSpan("withinTimeRange").Milliseconds,
                loggingEnabled: config.GetBoolean("loggingEnabled"),
                decider: ClusterMetricsStrategy.MetricsDecider
            )
        {

        }

        public static IDecider MetricsDecider
        {
            get
            {
                return Actor.Decider.From(e =>
                {
                    if (e is ActorInitializationException)
                        return Directive.Stop;
                    if (e is ActorKilledException)
                        return Directive.Stop;
                    if (e is DeathPactException)
                        return Directive.Stop;

                    return Directive.Restart;
                });
            }
        }
    }
}