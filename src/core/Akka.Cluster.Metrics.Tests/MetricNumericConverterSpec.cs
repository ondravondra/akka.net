//-----------------------------------------------------------------------
// <copyright file="MetricNumericConverterSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using FluentAssertions;
using Xunit;

namespace Akka.Cluster.Metrics.Tests
{
    public class MetricNumericConverterSpec
    {
        [Fact(Skip = "Don't know how to implement the test")]
        public void MetricNumericConverter_must_convert()
        {
            MetricNumericConverter.ConvertNumber(0);
            MetricNumericConverter.ConvertNumber(1);
            MetricNumericConverter.ConvertNumber(1L);
            MetricNumericConverter.ConvertNumber(0.0);
        }

        [Fact]
        public void MetricNumericConverter_must_define_a_new_metric()
        {
            var metric = Metric.Create("HeapMemoryUsed", 256L, decayFactor: 0.18);
            metric.Name.Should().Be("HeapMemoryUsed");
            metric.Value.Should().Be(256L);
            metric.IsSmooth.Should().BeTrue();
            metric.SmoothValue.Should().BeApproximately(256.0, 0.0001);
        }

        [Fact]
        public void MetricNumericConverter_must_define_an_undefined_value_with_a_null()
        {
            Metric.Create("x", -1, null).Should().BeNull();
            Metric.Create("x", double.NaN, null).Should().BeNull();
        }

        [Fact]
        public void MetricNumericConverter_must_recognize_whether_a_metric_value_is_defined()
        {
            MetricNumericConverter.Defined(0).Should().BeTrue();
            MetricNumericConverter.Defined(0.0).Should().BeTrue();
        }

        [Fact]
        public void MetricNumericConverter_must_recognize_whether_a_metric_value_is_not_defined()
        {
            MetricNumericConverter.Defined(-1).Should().BeFalse();
            MetricNumericConverter.Defined(-1.0).Should().BeFalse();
            MetricNumericConverter.Defined(double.NaN).Should().BeFalse();
        }
    }
}
