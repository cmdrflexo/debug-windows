using System;

namespace jcan.CelestialSystems
{
    /// <summary>Direct timestamp evaluation, without frame integration or Gravity Engine.</summary>
    public static class KeplerianConicTrajectoryEvaluator
    {
        private const double TwoPi = 2.0 * Math.PI;

        public static bool TryCalculatePeriodSeconds(KeplerianConicTrajectory definition,
            double referenceMassKilograms, double orbitingMassKilograms,
            out double periodSeconds, out string error)
        {
            periodSeconds = 0.0;
            if (!TryParameters(definition, referenceMassKilograms, orbitingMassKilograms,
                out _, out var n, out error)) return false;
            if (!definition.IsClosed)
            {
                error = "Hyperbolic trajectories have no orbital period.";
                return false;
            }
            var period = TwoPi / n;
            if (!KeplerianConicTrajectory.Finite(period))
            {
                error = "Orbital period exceeds the supported numeric range.";
                return false;
            }
            periodSeconds = period;
            return true;
        }

        public static bool TryEvaluateRelativeState(KeplerianConicTrajectory definition,
            double referenceMassKilograms, double orbitingMassKilograms, double universalTimeSeconds,
            out DoubleVector3 positionMeters, out DoubleVector3 velocityMetersPerSecond, out string error)
        {
            positionMeters = new DoubleVector3();
            velocityMetersPerSecond = new DoubleVector3();
            if (!TryParameters(definition, referenceMassKilograms, orbitingMassKilograms,
                out var a, out var n, out error)) return false;
            var elapsed = universalTimeSeconds - definition.EpochUniversalTimeSeconds;
            if (!KeplerianConicTrajectory.Finite(universalTimeSeconds) || !KeplerianConicTrajectory.Finite(elapsed))
            {
                error = "Evaluation time and elapsed time must be finite.";
                return false;
            }
            var sign = (double)definition.Direction;
            var e = definition.Eccentricity;
            var m0 = definition.MeanAnomalyAtEpochDegrees;
            // Reduce before multiplication to preserve phase across many complete revolutions.
            var mean = definition.IsClosed
                ? (m0 % 360.0) * (Math.PI / 180.0) + sign * n * (elapsed % (TwoPi / n))
                : m0 * (Math.PI / 180.0) + sign * n * elapsed;
            if (!KeplerianConicTrajectory.Finite(mean))
            {
                error = "Mean anomaly exceeds the supported numeric range.";
                return false;
            }
            if (definition.IsClosed) mean = Math.IEEERemainder(mean, TwoPi);
            if (!TrySolve(e, mean, definition.IsClosed, out var anomaly))
            {
                error = "Kepler equation did not converge within the supported numeric range.";
                return false;
            }

            double x, y, vx, vy;
            if (definition.IsClosed)
            {
                var c = Math.Cos(anomaly);
                var s = Math.Sin(anomaly);
                var beta = Math.Sqrt((1.0 - e) * (1.0 + e));
                var rate = sign * n / ((1.0 - e) + 2.0 * e * Math.Pow(Math.Sin(anomaly / 2.0), 2.0));
                x = definition.PeriapsisDistanceMeters - 2.0 * a * Math.Pow(Math.Sin(anomaly / 2.0), 2.0);
                y = a * beta * s;
                vx = -a * s * rate;
                vy = a * beta * c * rate;
            }
            else
            {
                var c = Math.Cosh(anomaly);
                var s = Math.Sinh(anomaly);
                var beta = Math.Sqrt(e - 1.0) * Math.Sqrt(e + 1.0);
                var rate = sign * n / ((e - 1.0) + 2.0 * e * Math.Pow(Math.Sinh(anomaly / 2.0), 2.0));
                x = definition.PeriapsisDistanceMeters - 2.0 * a * Math.Pow(Math.Sinh(anomaly / 2.0), 2.0);
                y = a * beta * s;
                vx = -a * s * rate;
                vy = a * beta * c * rate;
            }

            var node = Radians(definition.LongitudeOfAscendingNodeDegrees);
            var inc = Radians(definition.InclinationDegrees);
            var peri = Radians(definition.ArgumentOfPeriapsisDegrees);
            var u = new DoubleVector3(Math.Cos(node), 0.0, -Math.Sin(node));
            var v = new DoubleVector3(Math.Sin(node) * Math.Cos(inc), Math.Sin(inc), Math.Cos(node) * Math.Cos(inc));
            var p = u * Math.Cos(peri) + v * Math.Sin(peri);
            var q = u * -Math.Sin(peri) + v * Math.Cos(peri);
            var position = p * x + q * y;
            var velocity = p * vx + q * vy;
            if (!Finite(position) || !Finite(velocity))
            {
                error = "Conic state exceeds the supported numeric range.";
                return false;
            }
            positionMeters = position;
            velocityMetersPerSecond = velocity;
            return true;
        }

        private static bool TryParameters(KeplerianConicTrajectory d, double referenceMass, double orbitingMass,
            out double a, out double n, out string error)
        {
            a = n = 0.0;
            error = "A conic trajectory definition is required.";
            if (d == null || !d.TryValidate(out error)) return false;
            if (!KeplerianConicTrajectory.Finite(referenceMass) || referenceMass <= 0.0 ||
                !KeplerianConicTrajectory.Finite(orbitingMass) || orbitingMass <= 0.0)
            {
                error = "Conic evaluation requires finite positive reference and orbiting masses.";
                return false;
            }
            var mu = CelestialTrajectoryEvaluator.GravitationalConstant * referenceMass +
                CelestialTrajectoryEvaluator.GravitationalConstant * orbitingMass;
            a = Math.Abs(d.SemiMajorAxisMeters);
            n = Math.Sqrt(mu / a) / a;
            if (!KeplerianConicTrajectory.Finite(a) || a <= 0.0 ||
                !KeplerianConicTrajectory.Finite(n) || n <= 0.0)
            {
                error = "Conic parameters exceed the supported numeric range.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        // Monotonic bracketed bisection avoids Newton divergence near e = 1.
        private static bool TrySolve(double e, double mean, bool ellipse, out double anomaly)
        {
            var target = Math.Abs(mean);
            anomaly = 0.0;
            if (target == 0.0) return true;
            var lo = 0.0;
            var hi = ellipse ? Math.PI : 1.0;
            if (!ellipse)
            {
                while (e * Math.Sinh(hi) - hi < target && hi < 710.0)
                    hi = Math.Min(710.0, hi * 2.0);
            }
            for (var iteration = 0; iteration < 160; iteration++)
            {
                var mid = lo + (hi - lo) * 0.5;
                var value = ellipse ? (1.0 - e) * mid + e * Difference(mid, false)
                    : (e - 1.0) * mid + e * Difference(mid, true);
                if (double.IsNaN(value)) return false;
                if (value > target) hi = mid; else lo = mid;
                if (hi - lo <= 4e-15 * Math.Max(1e-12, mid))
                {
                    anomaly = (mean < 0.0 ? -1.0 : 1.0) * (lo + (hi - lo) * 0.5);
                    return true;
                }
            }
            return false;
        }

        // Stable x - sin(x) / sinh(x) - x near periapsis.
        private static double Difference(double x, bool hyperbolic)
        {
            if (Math.Abs(x) >= 0.1) return hyperbolic ? Math.Sinh(x) - x : x - Math.Sin(x);
            var x2 = x * x;
            var term = x * x2 / 6.0;
            var sum = term;
            for (var k = 5; k <= 15; k += 2)
            {
                term *= (hyperbolic ? x2 : -x2) / ((k - 1.0) * k);
                sum += term;
            }
            return sum;
        }

        private static double Radians(double degrees) => (degrees % 360.0) * (Math.PI / 180.0);
        private static bool Finite(DoubleVector3 v) =>
            KeplerianConicTrajectory.Finite(v.x) && KeplerianConicTrajectory.Finite(v.y) && KeplerianConicTrajectory.Finite(v.z);
    }
}
