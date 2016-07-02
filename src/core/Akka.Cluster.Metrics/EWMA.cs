//-----------------------------------------------------------------------
// <copyright file="EWMA.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Akka.Cluster.Metrics
{
    /// <summary>
    /// The exponentially weighted moving average (EWMA) approach captures short-term
    /// movements in volatility for a conditional volatility forecasting model. By virtue
    /// of its alpha, or decay factor, this provides a statistical streaming data model
    /// that is exponentially biased towards newer entries.
    ///
    /// http://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average
    ///
    /// An EWMA only needs the most recent forecast value to be kept, as opposed to a standard
    /// moving average model.
    /// </summary>
    public sealed class EWMA
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value">alpha decay factor, sets how quickly the exponential weighting decays for past data compared to new data</param>
        /// <param name="alpha">value the current exponentially weighted moving average, e.g. Y(n - 1), or, the sampled value resulting from the previous smoothing iteration. This value is always used as the previous EWMA to calculate the new EWMA</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public EWMA(double value, double alpha)
        {
            Alpha = alpha;
            Value = value;
            if (!(0.0 <= alpha && alpha <= 1.0)) throw new ArgumentOutOfRangeException(nameof(alpha), "alpha must be between 0.0 and 1.0");
        }

        public double Value { get; }

        public double Alpha { get; }

        #region Operators

        public static EWMA operator +(EWMA ewma, double xn)
        {
            var newValue = (ewma.Alpha * xn) + (1 - ewma.Alpha) * ewma.Value;
            if (newValue == ewma.Value) return ewma;
            return new EWMA(newValue, ewma.Alpha);
        }

        #endregion

        #region Static members

        /// <summary>
        /// Math.Log(2)
        /// </summary>
        private const double LogOf2 = 0.69315D;

        ///<summary>
        /// Calculate the alpha (decay factor) used in <see cref="EWMA"/>
        /// from specified half-life and interval between observations.
        /// Half-life is the interval over which the weights decrease by a factor of two.
        /// The relevance of each data sample is halved for every passing half-life duration,
        /// i.e. after 4 times the half-life, a data sample's relevance is reduced to 6% of
        /// its original relevance. The initial relevance of a data sample is given by
        /// 1 – 0.5 ^ (collect-interval / half-life).
        ///</summary>
        public static double CalculateAlpha(TimeSpan halfLife, TimeSpan collectInterval)
        {
            var halfLifeMillis = halfLife.TotalMilliseconds;
            if (halfLifeMillis < 0) throw new ArgumentOutOfRangeException(nameof(halfLife), "halfLife must be > 0s");
            var decayRate = LogOf2 / halfLifeMillis;
            return 1 - Math.Exp(-decayRate * collectInterval.TotalMilliseconds);
        }

        #endregion
    }
}
