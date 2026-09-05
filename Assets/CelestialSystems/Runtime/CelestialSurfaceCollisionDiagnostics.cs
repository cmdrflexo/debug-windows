/*
 * Checks radial queries against independently constructed triangle points and verifies collision coverage at cube corners.
 */

using System;
using System.Collections.Generic;

namespace jcan.CelestialSystems
{
    public sealed class CelestialSurfaceCollisionDiagnostics
    {
        public bool Passed { get; private set; }
        public int TestedSamples { get; private set; }
        public double MaximumQueryErrorMeters { get; private set; }
        public string LastError { get; private set; }

        public void Run(CelestialSurfaceRuntime runtime)
        {
            Passed = false;
            TestedSamples = 0;
            MaximumQueryErrorMeters = 0.0;
            LastError = string.Empty;
            try
            {
                var radius = runtime.BodyDefinition.ReferenceRadiusMeters;
                foreach (CubeSphereFace face in Enum.GetValues(typeof(CubeSphereFace)))
                    foreach (var level in new[] { 0, 8, 16, 20 })
                    {
                        var count = 1 << level;
                        var address = new CubeSpherePatchAddress(face, level, count / 2, count / 2);
                        const int resolution = 33;
                        var elevations = new float[resolution * resolution];
                        for (var y = 0; y < resolution; y++)
                            for (var x = 0; x < resolution; x++)
                                elevations[y * resolution + x] = (float)(Math.Sin(x * 1.7 + y * 0.9) *
                                    Math.Min(120.0, radius * 0.001));
                        var data = new CelestialSurfacePatchData(runtime.CacheKey, address, resolution, elevations);
                        for (var y = 0; y < resolution - 1; y += 5)
                            for (var x = 0; x < resolution - 1; x += 5)
                            {
                                var a = CelestialSurfaceGeometry.Vertex(data, radius, x, y);
                                var b = CelestialSurfaceGeometry.Vertex(data, radius, x + 1, y);
                                var c = CelestialSurfaceGeometry.Vertex(data, radius, x, y + 1);
                                var d = CelestialSurfaceGeometry.Vertex(data, radius, x + 1, y + 1);
                                CheckPoint(data, radius, a * 0.2 + b * 0.3 + c * 0.5);
                                CheckPoint(data, radius, b * 0.2 + d * 0.3 + c * 0.5);
                            }
                    }

                var levelForCoverage = CelestialSurfaceCollisionCoverage.ResolveLevel(radius, 33, 8.0, 0, 20);
                for (var x = -1; x <= 1; x += 2)
                    for (var y = -1; y <= 1; y += 2)
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var direction = new DoubleVector3(x, y, z);
                            direction /= direction.Magnitude;
                            var patches = new HashSet<CubeSpherePatchAddress>();
                            if (!CelestialSurfaceCollisionCoverage.Collect(direction, radius, 256.0,
                                    levelForCoverage, patches, 512)) throw new Exception("Corner coverage exceeded test capacity.");
                            var tangent = CelestialSurfaceGeometry.Cross(direction, new DoubleVector3(0, 1, 0));
                            tangent /= tangent.Magnitude;
                            var bitangent = CelestialSurfaceGeometry.Cross(direction, tangent);
                            for (var i = 0; i < 32; i++)
                            {
                                var angle = i * Math.PI * 2.0 / 32;
                                var point = direction * Math.Cos(200.0 / radius) +
                                    (tangent * Math.Cos(angle) + bitangent * Math.Sin(angle)) * Math.Sin(200.0 / radius);
                                CubeSphereMapping.TryDirectionToAddress(point, 0.0, out var sampleAddress);
                                CubeSpherePatchAddress.TryFromAddress(sampleAddress, levelForCoverage, out var patch);
                                if (!patches.Contains(patch)) throw new Exception("Coverage omitted a point inside a cube-corner footprint.");
                                TestedSamples++;
                            }
                        }

                // Cross cell boundaries at cell indices that doubles cannot represent exactly.
                var far = new UniversePosition(9007199254741000L, -9007199254741000L, 0,
                    UniversePosition.CellSizeMeters * 0.5 - 0.125,
                    -UniversePosition.CellSizeMeters * 0.5 + 0.25, 0.0);
                var shifted = CelestialSurfaceGeometry.Add(far, new DoubleVector3(0.25, -0.5, 1.0));
                if ((CelestialSurfaceGeometry.Difference(shifted, far) -
                    new DoubleVector3(0.25, -0.5, 1.0)).Magnitude > 1e-9)
                    throw new Exception("Large-cell subtraction lost local precision.");
                TestedSamples++;
                Passed = true;
            }
            catch (Exception exception) { LastError = exception.GetBaseException().Message; }
        }

        private void CheckPoint(CelestialSurfacePatchData data, double radius, DoubleVector3 expected)
        {
            if (!CelestialSurfaceGeometry.TrySample(data, radius, expected * 1.01, out var result, out var normal))
                throw new Exception("A radial query missed a known point inside a triangle.");
            var error = (result - expected).Magnitude;
            MaximumQueryErrorMeters = Math.Max(MaximumQueryErrorMeters, error);
            if (error > Math.Max(0.0001, radius * 1e-10) ||
                Math.Abs(normal.Magnitude - 1.0) > 1e-9 ||
                CelestialSurfaceGeometry.Dot(normal, result) <= 0.0)
                throw new Exception("A radial query returned the wrong triangle position or normal.");
            TestedSamples++;
        }
    }
}
