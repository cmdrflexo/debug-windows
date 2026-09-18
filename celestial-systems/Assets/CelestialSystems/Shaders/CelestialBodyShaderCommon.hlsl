/*
 * Shared body-relative coordinate and diagnostic functions for celestial surface shaders.
 */

#ifndef JCAN_CELESTIAL_BODY_SHADER_COMMON_INCLUDED
#define JCAN_CELESTIAL_BODY_SHADER_COMMON_INCLUDED

#ifndef JCAN_CELESTIAL_SLOPE_DEBUG_EXTERNAL
float _SlopeDebugMinDegrees;
float _SlopeDebugMaxDegrees;
#endif

struct CelestialBodySurfaceCoordinates
{
    float3 bodyOffsetWS;
    float3 radialNormalWS;
    float elevationMeters;
    float slopeDegrees;
    half latitude;
};

float3 CelestialSafeNormalize(
    float3 value,
    float3 fallback)
{
    float lengthSquared =
        dot(
            value,
            value);

    return
        lengthSquared >
            0.00000001
            ? value *
                rsqrt(
                    lengthSquared)
            : fallback;
}

CelestialBodySurfaceCoordinates CelestialBuildSurfaceCoordinates(
    float3 positionWS,
    half3 meshNormalWS,
    float3 bodyCenterWS,
    float datumRadiusMeters,
    float3 bodyNorthDirectionWS)
{
    CelestialBodySurfaceCoordinates coordinates;
    coordinates.bodyOffsetWS =
        positionWS -
        bodyCenterWS;
    float radialDistance =
        length(
            coordinates.bodyOffsetWS);
    coordinates.radialNormalWS =
        CelestialSafeNormalize(
            coordinates.bodyOffsetWS,
            float3(
                0.0,
                1.0,
                0.0));
    coordinates.elevationMeters =
        radialDistance -
        datumRadiusMeters;
    float3 resolvedMeshNormal =
        CelestialSafeNormalize(
            meshNormalWS,
            coordinates.radialNormalWS);
    float radialAlignment =
        clamp(
            dot(
                resolvedMeshNormal,
                coordinates.radialNormalWS),
            -1.0,
            1.0);
    coordinates.slopeDegrees =
        degrees(
            acos(
                radialAlignment));
    float3 resolvedNorth =
        CelestialSafeNormalize(
            bodyNorthDirectionWS,
            float3(
                0.0,
                1.0,
                0.0));
    coordinates.latitude =
        saturate(
            abs(
                dot(
                    coordinates.radialNormalWS,
                    resolvedNorth)));
    return coordinates;
}

half3 CelestialElevationDebugColor(
    float elevationMeters,
    float minimumMeters,
    float maximumMeters)
{
    float rangeMeters =
        max(
            maximumMeters -
                minimumMeters,
            0.001);
    half elevation01 =
        saturate(
            (elevationMeters -
                minimumMeters) /
            rangeMeters);
    half3 lowColor =
        half3(
            0.0h,
            0.15h,
            1.0h);
    half3 middleColor =
        half3(
            0.1h,
            0.85h,
            0.2h);
    half3 highColor =
        half3(
            1.0h,
            0.2h,
            0.05h);

    return
        elevation01 <
            0.5h
            ? lerp(
                lowColor,
                middleColor,
                elevation01 *
                    2.0h)
            : lerp(
                middleColor,
                highColor,
                (elevation01 -
                    0.5h) *
                    2.0h);
}

half3 CelestialResolveDebugColor(
    float debugMode,
    half3 meshNormalWS,
    CelestialBodySurfaceCoordinates coordinates,
    float2 patchUv,
    float elevationDebugMinimumMeters,
    float elevationDebugMaximumMeters,
    float coordinateDebugScaleMeters)
{
    if (debugMode <
        1.5)
    {
        return
            meshNormalWS *
                0.5h +
            0.5h;
    }

    if (debugMode <
        2.5)
    {
        return
            (half3)coordinates.radialNormalWS *
                0.5h +
            0.5h;
    }

    if (debugMode <
        3.5)
    {
        return
            CelestialElevationDebugColor(
                coordinates.elevationMeters,
                elevationDebugMinimumMeters,
                elevationDebugMaximumMeters);
    }

    if (debugMode <
        4.5)
    {
        float slopeRangeDegrees =
            max(
                _SlopeDebugMaxDegrees -
                    _SlopeDebugMinDegrees,
                0.001);
        half slope01 =
            saturate(
                (coordinates.slopeDegrees -
                    _SlopeDebugMinDegrees) /
                slopeRangeDegrees);
        return
            slope01.xxx;
    }

    if (debugMode <
        5.5)
    {
        return
            half3(
                coordinates.latitude,
                0.2h,
                1.0h -
                    coordinates.latitude);
    }

    if (debugMode <
        6.5)
    {
        return
            half3(
                frac(
                    patchUv.x),
                frac(
                    patchUv.y),
                0.0h);
    }

    float safeCoordinateScale =
        max(
            coordinateDebugScaleMeters,
            0.001);
    return
        (half3)frac(
            abs(
                coordinates.bodyOffsetWS) /
            safeCoordinateScale);
}

#endif
