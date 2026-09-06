/*
 * Checks universe motion serialization and angular sampling without requiring a running scene or Gravity Engine setup.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class UniverseMotionStateValidation
    {
        [Serializable]
        private struct LegacyPose
        {
            public UniversePosition position;
            public Quaternion rotation;
        }

        [MenuItem("Tools/Celestial Systems/Validate Universe Motion State")]
        public static void Validate()
        {
            ValidateSerialization();
            ValidateSampling();
            Debug.Log("Universe motion validation passed: serialization, legacy pose data, " +
                "stationary poses, universe axes, small angles, quaternion signs, repeated reads, " +
                "same-tick updates, shortest arcs, reset, rewind, and invalid inputs.");
        }

        private static void ValidateSerialization()
        {
            var position = new UniversePosition(123456789, -234567890, 345678901,
                499999999.125, -499999999.25, 0.00025);
            var rotation = Quaternion.AngleAxis(37.0f, Vector3.up);
            var linear = new DoubleVector3(123456.000000125, -765.25, 0.000000001);
            var angular = new DoubleVector3(-0.00007292115, 0.125, 0.000000025);
            var original = new UniverseMotionState(position, rotation, linear, angular);
            var roundTrip = JsonUtility.FromJson<UniverseMotionState>(JsonUtility.ToJson(original));
            RequireSamePose(roundTrip, position, rotation);
            RequireNear(roundTrip.LinearVelocityMetersPerSecond, linear, 1e-9, "linear serialization");
            RequireNear(roundTrip.AngularVelocityRadiansPerSecond, angular, 1e-14, "angular serialization");

            var legacyJson = JsonUtility.ToJson(new LegacyPose { position = position, rotation = rotation });
            var migrated = JsonUtility.FromJson<UniverseMotionState>(legacyJson);
            RequireSamePose(migrated, position, rotation);
            RequireZero(migrated.LinearVelocityMetersPerSecond, "legacy linear default");
            RequireZero(migrated.AngularVelocityRadiansPerSecond, "legacy angular default");

            var stationary = new UniverseMotionState(position, rotation);
            RequireSamePose(stationary, position, rotation);
            RequireZero(stationary.LinearVelocityMetersPerSecond, "stationary linear velocity");
            RequireZero(stationary.AngularVelocityRadiansPerSecond, "stationary angular velocity");
        }

        private static void ValidateSampling()
        {
            var sampler = new UniverseAngularVelocitySampler();
            Require(sampler.TrySample(Quaternion.identity, 10.0), "first sample");
            Require(!sampler.HasEstimate, "first sample must establish a baseline");
            RequireZero(sampler.AngularVelocityRadiansPerSecond, "first-sample velocity");

            var quarterTurn = Quaternion.AngleAxis(90.0f, Vector3.up);
            Sample(sampler, quarterTurn, 12.0, new DoubleVector3(0.0, Math.PI / 4.0, 0.0));
            for (var i = 0; i < 10; i++)
                Sample(sampler, quarterTurn, 12.0, new DoubleVector3(0.0, Math.PI / 4.0, 0.0));

            // A rotation writer can run after an earlier read at the same simulation time.
            Sample(sampler, Quaternion.AngleAxis(120.0f, Vector3.up), 12.0,
                new DoubleVector3(0.0, Math.PI / 3.0, 0.0));
            Sample(sampler, Quaternion.AngleAxis(120.0f, Vector3.up), 14.0, DoubleVector3.zero);

            sampler.Reset();
            var initial = Quaternion.AngleAxis(45.0f, Vector3.right);
            Require(sampler.TrySample(initial, 0.0), "nonidentity baseline");
            Sample(sampler, quarterTurn * initial, 2.0,
                new DoubleVector3(0.0, Math.PI / 4.0, 0.0));

            // An Earth-like rate per fixed step is small enough that float acos(w) loses it.
            sampler.Reset();
            const double earthRate = 0.00007292115;
            Require(sampler.TrySample(Quaternion.identity, 0.0), "small-angle baseline");
            Sample(sampler, Quaternion.AngleAxis((float)(earthRate * 0.02 * 180.0 / Math.PI), Vector3.up),
                0.02, new DoubleVector3(0.0, earthRate, 0.0), 1e-10);

            sampler.Reset();
            Require(sampler.TrySample(quarterTurn, 0.0), "sign baseline");
            var negative = new Quaternion(-quarterTurn.x, -quarterTurn.y, -quarterTurn.z, -quarterTurn.w);
            Sample(sampler, negative, 1.0, DoubleVector3.zero);

            sampler.Reset();
            Require(sampler.TrySample(Quaternion.AngleAxis(179.0f, Vector3.up), 0.0), "wrap baseline");
            Sample(sampler, Quaternion.AngleAxis(-179.0f, Vector3.up), 1.0,
                new DoubleVector3(0.0, 2.0 * Math.PI / 180.0, 0.0));

            Require(sampler.TrySample(initial, -1.0), "rewind sample");
            Require(!sampler.HasEstimate, "rewind clears estimate");
            RequireZero(sampler.AngularVelocityRadiansPerSecond, "rewind velocity");
            Sample(sampler, quarterTurn * initial, 1.0,
                new DoubleVector3(0.0, Math.PI / 4.0, 0.0));

            sampler.Reset();
            Require(!sampler.HasEstimate, "explicit reset clears estimate");
            RequireZero(sampler.AngularVelocityRadiansPerSecond, "reset velocity");
            Require(sampler.TrySample(initial, 1.0), "baseline after reset");
            Require(!sampler.HasEstimate, "reset cannot retain an old interval");

            Require(!sampler.TrySample(new Quaternion(0, 0, 0, 0), 2.0), "reject zero quaternion");
            Require(!sampler.TrySample(new Quaternion(float.NaN, 0, 0, 1), 2.0), "reject NaN rotation");
            Require(!sampler.TrySample(Quaternion.identity, double.NaN), "reject NaN time");
            Require(!sampler.TrySample(Quaternion.identity, double.PositiveInfinity), "reject infinite time");
            Require(!sampler.HasEstimate, "invalid sample clears estimate");
            Require(sampler.TrySample(initial, 3.0), "recovery after invalid sample");
            Require(!sampler.HasEstimate, "recovery establishes a new baseline");
        }

        private static void Sample(UniverseAngularVelocitySampler sampler, Quaternion rotation,
            double time, DoubleVector3 expected, double tolerance = 1e-6)
        {
            Require(sampler.TrySample(rotation, time), "valid rotation sample");
            Require(sampler.HasEstimate, "two distinct timestamps provide an estimate");
            RequireNear(sampler.AngularVelocityRadiansPerSecond, expected, tolerance, "sampled angular velocity");
        }

        private static void RequireSamePose(UniverseMotionState state, UniversePosition position, Quaternion rotation)
        {
            var actual = state.Position;
            Require(actual.CellX == position.CellX && actual.CellY == position.CellY &&
                actual.CellZ == position.CellZ && actual.LocalXMeters == position.LocalXMeters &&
                actual.LocalYMeters == position.LocalYMeters && actual.LocalZMeters == position.LocalZMeters,
                "global position including cell and submeter precision");
            Require(Quaternion.Angle(state.Rotation, rotation) < 0.01f, "rotation preserved");
        }

        private static void RequireZero(DoubleVector3 value, string description)
        {
            RequireNear(value, DoubleVector3.zero, 0.0, description);
        }

        private static void RequireNear(DoubleVector3 actual, DoubleVector3 expected, double tolerance, string description)
        {
            Require(Math.Abs(actual.x - expected.x) <= tolerance &&
                Math.Abs(actual.y - expected.y) <= tolerance && Math.Abs(actual.z - expected.z) <= tolerance,
                description + ": expected " + expected + ", received " + actual);
        }

        private static void Require(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException("Universe motion validation failed: " + description);
        }
    }
}
