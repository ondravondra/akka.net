//-----------------------------------------------------------------------
// <copyright file="EWMASpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Akka.Util;
using Xunit;
using FluentAssertions;

namespace Akka.Cluster.Metrics.Tests
{
    public class EWMASpec : MetricsCollectorFactory, IDisposable
    {
        private readonly IMetricsCollector _collector;
        public EWMASpec()
        {
            _collector = CreateMetricsCollector();
        }

        [Fact]
        public void DataStream_must_calculate_same_emwa_for_constant_values()
        {
            var ds = new EWMA(100.0d, 0.18) + 100.0D + 100.0D + 100.0D;
            ds.Value.Should().BeApproximately(100.0, 0.001);
        }

        [Fact]
        public void DataStream_must_calculate_correct_ewma_for_normal_decay()
        {
            var d0 = new EWMA(1000.0d, 2.0 / (1 + 10));
            d0.Value.Should().BeApproximately(1000.0, 0.01);
            var d1 = d0 + 10.0d;
            d1.Value.Should().BeApproximately(820.0, 0.01);
            var d2 = d1 + 10.0d;
            d2.Value.Should().BeApproximately(672.73, 0.01);
            var d3 = d2 + 10.0d;
            d3.Value.Should().BeApproximately(552.23, 0.01);
            var d4 = d3 + 10.0d;
            d4.Value.Should().BeApproximately(453.64, 0.01);

            var dn = Enumerable.Range(1, 100).Aggregate(d0, (d, i) => d + 10.0d);
            dn.Value.Should().BeApproximately(10.0, 0.1);
        }

        [Fact]
        public void DataStream_must_calculate_correct_emwa_value_for_alpha_10_max_bias_towards_latest_value()
        {
            var d0 = new EWMA(100.0d, 1.0);
            d0.Value.Should().BeApproximately(100.0, 0.01);
            var d1 = d0 + 1.0d;
            d1.Value.Should().BeApproximately(1.0, 0.01);
            var d2 = d1 + 57.0d;
            d2.Value.Should().BeApproximately(57.0, 0.01);
            var d3 = d2 + 10.0d;
            d3.Value.Should().BeApproximately(10.0, 0.01);
        }

        [Fact]
        public void DataStream_must_calculate_alpha_from_halflife_and_collect_interval()
        {
            // according to http://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average
            var expectedAlpha = 0.1;
            // alpha = 2.0 / (1 + N)
            var n = 19;
            var halfLife = n / 2.8854;
            var collectInterval = 1.Seconds();
            var halfLifeDuration = TimeSpan.FromMilliseconds(halfLife * 1000);
            EWMA.CalculateAlpha(halfLifeDuration, collectInterval).Should().BeApproximately(expectedAlpha, 0.001);
        }

        [Fact]
        public void DataStream_must_calculate_sane_alpha_from_short_halflife()
        {
            var alpha = EWMA.CalculateAlpha(1.Milliseconds(), 3.Seconds());
            alpha.Should().BeLessOrEqualTo(1.0);
            alpha.Should().BeGreaterOrEqualTo(0.0);
            alpha.Should().BeApproximately(1.0, 0.001);
        }

        [Fact]
        public void DataStream_must_calculate_sane_alpha_from_long_halflife()
        {
            var alpha = EWMA.CalculateAlpha(1.Days(), 3.Seconds());
            alpha.Should().BeLessOrEqualTo(1.0);
            alpha.Should().BeGreaterOrEqualTo(0.0);
            alpha.Should().BeApproximately(0.0, 0.001);
        }

        [Fact]
        public void Calculate_the_EWMA_for_multiple_variable_datastreams()
        {
            var streamingDataSet = ImmutableDictionary.Create<string, Metric>();
            var usedMemory = new byte[0];
            foreach (var i in Enumerable.Range(1, 50))
            {
                // wait a while between each message to give the metrics a chance to change
                Thread.Sleep(100);
                usedMemory =
                    usedMemory.Concat(Enumerable.Repeat(Convert.ToByte(ThreadLocalRandom.Current.Next(127)), 1024))
                        .ToArray();
                var changes = _collector.Sample().Metrics.Select(latest =>
                {
                    Metric previous;
                    if (!streamingDataSet.TryGetValue(latest.Name, out previous)) return latest;
                    if (latest.IsSmooth && latest.Value != previous.Value)
                    {
                        var updated = previous + latest;
                        updated.IsSmooth.Should().BeTrue();
                        updated.SmoothValue.Should().NotBe(previous.SmoothValue);
                        return updated;
                    }
                    else return latest;
                });
                streamingDataSet = streamingDataSet.Union(changes.ToDictionary(metric => metric.Name, metric => metric)).ToImmutableDictionary(pair => pair.Key, pair => pair.Value);
            }
        }

        #region IDisposable members

        /// <summary>
        /// Needs to hide previous Dispose implementation in order to avoid recursive disposal.
        /// </summary>
        public new void Dispose()
        {
            _collector.Dispose();
            base.Dispose();
        }

        #endregion
    }
}

