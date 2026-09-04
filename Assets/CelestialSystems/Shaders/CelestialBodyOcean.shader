/*
 * Provides the Built-in Render Pipeline ocean shader foundation for generated celestial body meshes.
 */

Shader "jcan/Celestial Systems/Celestial Body Ocean"
{
    Properties
    {
        [HideInInspector] _MainTex ("Patch UV", 2D) = "white" {}
        _ShallowColor ("Shallow Color", Color) = (0.02, 0.35, 0.48, 1)
        _DeepColor ("Deep Color", Color) = (0.005, 0.035, 0.12, 1)
        _FresnelColor ("Fresnel Color", Color) = (0.35, 0.65, 0.8, 1)
        _FresnelPower ("Fresnel Power", Range(0.25, 12)) = 5
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.5
        _Opacity ("Opacity", Range(0, 1)) = 0.72
        _FresnelOpacityBoost ("Fresnel Opacity Boost", Range(0, 1)) = 0.2
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92

        [HideInInspector] _CelestialSurfaceType ("Celestial Surface Type", Float) = 1
        [HideInInspector] _PlanetCenterScenePosition ("Planet Center", Vector) = (0, 0, 0, 1)
        [HideInInspector] _PlanetRadiusMeters ("Datum Radius", Float) = 1
        [HideInInspector] _OceanSurfaceElevationMeters ("Ocean Surface Elevation", Float) = 0
        [HideInInspector] _OceanRadiusMeters ("Ocean Radius", Float) = 1
        [HideInInspector] _BodyNorthDirection ("Body North Direction", Vector) = (0, 1, 0, 0)
        [HideInInspector] _BodyPoleReferenceDirection ("Body Pole Reference Direction", Vector) = (0, 0, 1, 0)
        [HideInInspector] _ElevationDebugMinMeters ("Elevation Debug Minimum", Float) = -5000
        [HideInInspector] _ElevationDebugMaxMeters ("Elevation Debug Maximum", Float) = 5000
        [HideInInspector] _CoordinateDebugScaleMeters ("Coordinate Debug Scale", Float) = 1000
        [HideInInspector] _DebugMode ("Debug Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        LOD 300
        Cull Back
        ZWrite Off

        CGPROGRAM
        #pragma surface Surface Standard alpha:fade fullforwardshadows
        #pragma target 3.5

        #include "CelestialBodyShaderCommon.hlsl"

        half4 _ShallowColor;
        half4 _DeepColor;
        half4 _FresnelColor;
        half _FresnelPower;
        half _FresnelStrength;
        half _Opacity;
        half _FresnelOpacityBoost;
        half _Metallic;
        half _Smoothness;
        float4 _PlanetCenterScenePosition;
        float4 _BodyNorthDirection;
        float _PlanetRadiusMeters;
        float _ElevationDebugMinMeters;
        float _ElevationDebugMaxMeters;
        float _CoordinateDebugScaleMeters;
        float _DebugMode;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            float3 viewDir;
            INTERNAL_DATA
        };

        void Surface(
            Input input,
            inout SurfaceOutputStandard output)
        {
            half3 normalWS =
                normalize(
                    WorldNormalVector(
                        input,
                        half3(
                            0.0h,
                            0.0h,
                            1.0h)));
            half3 viewDirectionWS =
                normalize(
                    _WorldSpaceCameraPos.xyz -
                    input.worldPos);
            half viewFacing =
                saturate(
                    dot(
                        normalWS,
                        viewDirectionWS));
            half fresnel =
                pow(
                    1.0h -
                        viewFacing,
                    _FresnelPower);
            CelestialBodySurfaceCoordinates coordinates =
                CelestialBuildSurfaceCoordinates(
                    input.worldPos,
                    normalWS,
                    _PlanetCenterScenePosition.xyz,
                    _PlanetRadiusMeters,
                    _BodyNorthDirection.xyz);

            if (_DebugMode > 0.5)
            {
                output.Albedo = 0.0h;
                output.Emission =
                    CelestialResolveDebugColor(
                        _DebugMode,
                        normalWS,
                        coordinates,
                        input.uv_MainTex,
                        _ElevationDebugMinMeters,
                        _ElevationDebugMaxMeters,
                        _CoordinateDebugScaleMeters);
                output.Normal =
                    half3(
                        0.0h,
                        0.0h,
                        1.0h);
                output.Metallic = 0.0h;
                output.Smoothness = 0.0h;
                output.Occlusion = 1.0h;
                output.Alpha = 1.0h;
                return;
            }

            output.Albedo =
                lerp(
                    _DeepColor.rgb,
                    _ShallowColor.rgb,
                    viewFacing);
            output.Emission =
                _FresnelColor.rgb *
                fresnel *
                _FresnelStrength;
            output.Normal =
                half3(
                    0.0h,
                    0.0h,
                    1.0h);
            output.Metallic = _Metallic;
            output.Smoothness = _Smoothness;
            output.Occlusion = 1.0h;
            output.Alpha =
                saturate(
                    _Opacity +
                    fresnel *
                        _FresnelOpacityBoost);
        }
        ENDCG
    }

    Fallback "Transparent/Diffuse"
}
