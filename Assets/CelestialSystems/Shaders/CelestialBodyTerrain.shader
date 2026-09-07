/*
 * Provides the Built-in Render Pipeline terrain shader foundation for generated celestial body meshes.
 */

Shader "jcan/Celestial Systems/Celestial Body Terrain"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.35, 0.35, 0.35, 1)
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        _Occlusion ("Occlusion", Range(0, 1)) = 1
        [Enum(Lit, 0, Emissive, 1, Lit And Emissive, 2)] _SurfaceLightingMode ("Surface Lighting Mode", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _EmissionIntensity ("Emission Intensity", Float) = 0

        [HideInInspector] _Control ("MapMagic Control", 2D) = "white" {}
        [HideInInspector] _LayerCount ("MapMagic Layer Count", Float) = 0
        [HideInInspector] _Splat0 ("Layer 0 Diffuse", 2D) = "white" {}
        [HideInInspector] _Splat1 ("Layer 1 Diffuse", 2D) = "white" {}
        [HideInInspector] _Splat2 ("Layer 2 Diffuse", 2D) = "white" {}
        [HideInInspector] _Splat3 ("Layer 3 Diffuse", 2D) = "white" {}
        [HideInInspector] [Normal] _Normal0 ("Layer 0 Normal", 2D) = "bump" {}
        [HideInInspector] [Normal] _Normal1 ("Layer 1 Normal", 2D) = "bump" {}
        [HideInInspector] [Normal] _Normal2 ("Layer 2 Normal", 2D) = "bump" {}
        [HideInInspector] [Normal] _Normal3 ("Layer 3 Normal", 2D) = "bump" {}
        [HideInInspector] _Mask0 ("Layer 0 Mask", 2D) = "white" {}
        [HideInInspector] _Mask1 ("Layer 1 Mask", 2D) = "white" {}
        [HideInInspector] _Mask2 ("Layer 2 Mask", 2D) = "white" {}
        [HideInInspector] _Mask3 ("Layer 3 Mask", 2D) = "white" {}
        [HideInInspector] _EmissionMap0 ("Layer 0 Emission", 2D) = "white" {}
        [HideInInspector] _EmissionMap1 ("Layer 1 Emission", 2D) = "white" {}
        [HideInInspector] _EmissionMap2 ("Layer 2 Emission", 2D) = "white" {}
        [HideInInspector] _EmissionMap3 ("Layer 3 Emission", 2D) = "white" {}
        [HideInInspector] _HasNormal0 ("Layer 0 Has Normal", Float) = 0
        [HideInInspector] _HasNormal1 ("Layer 1 Has Normal", Float) = 0
        [HideInInspector] _HasNormal2 ("Layer 2 Has Normal", Float) = 0
        [HideInInspector] _HasNormal3 ("Layer 3 Has Normal", Float) = 0
        [HideInInspector] _HasMask0 ("Layer 0 Has Mask", Float) = 0
        [HideInInspector] _HasMask1 ("Layer 1 Has Mask", Float) = 0
        [HideInInspector] _HasMask2 ("Layer 2 Has Mask", Float) = 0
        [HideInInspector] _HasMask3 ("Layer 3 Has Mask", Float) = 0
        [HideInInspector] _NormalScale0 ("Layer 0 Normal Scale", Float) = 1
        [HideInInspector] _NormalScale1 ("Layer 1 Normal Scale", Float) = 1
        [HideInInspector] _NormalScale2 ("Layer 2 Normal Scale", Float) = 1
        [HideInInspector] _NormalScale3 ("Layer 3 Normal Scale", Float) = 1
        [HideInInspector] _Tint0 ("Layer 0 Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _Tint1 ("Layer 1 Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _Tint2 ("Layer 2 Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _Tint3 ("Layer 3 Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _Metallic0 ("Layer 0 Metallic", Float) = 0
        [HideInInspector] _Metallic1 ("Layer 1 Metallic", Float) = 0
        [HideInInspector] _Metallic2 ("Layer 2 Metallic", Float) = 0
        [HideInInspector] _Metallic3 ("Layer 3 Metallic", Float) = 0
        [HideInInspector] _Smoothness0 ("Layer 0 Smoothness", Float) = 0
        [HideInInspector] _Smoothness1 ("Layer 1 Smoothness", Float) = 0
        [HideInInspector] _Smoothness2 ("Layer 2 Smoothness", Float) = 0
        [HideInInspector] _Smoothness3 ("Layer 3 Smoothness", Float) = 0
        [HideInInspector] _OcclusionStrength0 ("Layer 0 Occlusion Strength", Range(0, 1)) = 1
        [HideInInspector] _OcclusionStrength1 ("Layer 1 Occlusion Strength", Range(0, 1)) = 1
        [HideInInspector] _OcclusionStrength2 ("Layer 2 Occlusion Strength", Range(0, 1)) = 1
        [HideInInspector] _OcclusionStrength3 ("Layer 3 Occlusion Strength", Range(0, 1)) = 1
        [HideInInspector] [HDR] _EmissionColor0 ("Layer 0 Emission Color", Color) = (1, 1, 1, 1)
        [HideInInspector] [HDR] _EmissionColor1 ("Layer 1 Emission Color", Color) = (1, 1, 1, 1)
        [HideInInspector] [HDR] _EmissionColor2 ("Layer 2 Emission Color", Color) = (1, 1, 1, 1)
        [HideInInspector] [HDR] _EmissionColor3 ("Layer 3 Emission Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _EmissionIntensity0 ("Layer 0 Emission Intensity", Float) = 0
        [HideInInspector] _EmissionIntensity1 ("Layer 1 Emission Intensity", Float) = 0
        [HideInInspector] _EmissionIntensity2 ("Layer 2 Emission Intensity", Float) = 0
        [HideInInspector] _EmissionIntensity3 ("Layer 3 Emission Intensity", Float) = 0

        [HideInInspector] _TileFade ("Tile Fade", Range(0, 1)) = 1
        [HideInInspector] _LodMaskMode ("LOD Mask Mode", Float) = 0
        [HideInInspector] _LodFadeStartMeters ("LOD Fade Start", Float) = 0
        [HideInInspector] _LodFadeEndMeters ("LOD Fade End", Float) = 0
        [HideInInspector] _LodCenterDirection ("LOD Center Direction", Vector) = (0, 1, 0, 0)

        [HideInInspector] _CelestialSurfaceType ("Celestial Surface Type", Float) = 0
        [HideInInspector] _PlanetCenterScenePosition ("Planet Center", Vector) = (0, 0, 0, 1)
        [HideInInspector] _PlanetRadiusMeters ("Datum Radius", Float) = 1
        [HideInInspector] _OceanSurfaceElevationMeters ("Ocean Surface Elevation", Float) = 0
        [HideInInspector] _OceanRadiusMeters ("Ocean Radius", Float) = 1
        [HideInInspector] _BodyNorthDirection ("Body North Direction", Vector) = (0, 1, 0, 0)
        [HideInInspector] _BodyPoleReferenceDirection ("Body Pole Reference Direction", Vector) = (0, 0, 1, 0)
        [HideInInspector] _ElevationDebugMinMeters ("Elevation Debug Minimum", Float) = -5000
        [HideInInspector] _ElevationDebugMaxMeters ("Elevation Debug Maximum", Float) = 5000
        [HideInInspector] _SlopeDebugMinDegrees ("Slope Debug Minimum Degrees", Float) = 0
        [HideInInspector] _SlopeDebugMaxDegrees ("Slope Debug Maximum Degrees", Float) = 20
        [HideInInspector] _CoordinateDebugScaleMeters ("Coordinate Debug Scale", Float) = 1000
        [HideInInspector] _DebugMode ("Debug Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 300

        CGPROGRAM
        #pragma surface Surface Standard fullforwardshadows
        #pragma target 3.5

        #include "UnityStandardUtils.cginc"
        #include "CelestialBodyShaderCommon.hlsl"

        UNITY_DECLARE_TEX2D(_BaseMap);
        UNITY_DECLARE_TEX2D(_Control);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_NormalMap);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Splat0);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Splat1);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Splat2);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Splat3);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal0);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal1);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal2);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal3);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Mask0);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Mask1);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Mask2);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Mask3);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_EmissionMap);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_EmissionMap0);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_EmissionMap1);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_EmissionMap2);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_EmissionMap3);

        float4 _Splat0_ST;
        float4 _Splat1_ST;
        float4 _Splat2_ST;
        float4 _Splat3_ST;
        half4 _BaseColor;
        half _NormalScale;
        half _Metallic;
        half _Smoothness;
        half _Occlusion;
        half _SurfaceLightingMode;
        half4 _EmissionColor;
        half _EmissionIntensity;
        half _LayerCount;
        half _HasNormal0;
        half _HasNormal1;
        half _HasNormal2;
        half _HasNormal3;
        half _HasMask0;
        half _HasMask1;
        half _HasMask2;
        half _HasMask3;
        half _NormalScale0;
        half _NormalScale1;
        half _NormalScale2;
        half _NormalScale3;
        half4 _Tint0;
        half4 _Tint1;
        half4 _Tint2;
        half4 _Tint3;
        half _Metallic0;
        half _Metallic1;
        half _Metallic2;
        half _Metallic3;
        half _Smoothness0;
        half _Smoothness1;
        half _Smoothness2;
        half _Smoothness3;
        half _OcclusionStrength0;
        half _OcclusionStrength1;
        half _OcclusionStrength2;
        half _OcclusionStrength3;
        half4 _EmissionColor0;
        half4 _EmissionColor1;
        half4 _EmissionColor2;
        half4 _EmissionColor3;
        half _EmissionIntensity0;
        half _EmissionIntensity1;
        half _EmissionIntensity2;
        half _EmissionIntensity3;
        half _TileFade;
        half _LodMaskMode;
        float _LodFadeStartMeters;
        float _LodFadeEndMeters;
        float4 _LodCenterDirection;
        float4 _PlanetCenterScenePosition;
        float4 _BodyNorthDirection;
        float _PlanetRadiusMeters;
        float _ElevationDebugMinMeters;
        float _ElevationDebugMaxMeters;
        float _CoordinateDebugScaleMeters;
        float _DebugMode;

        struct Input
        {
            float2 uv_BaseMap;
            float2 uv_Control;
            float3 worldPos;
            float3 worldNormal;
            float4 screenPos;
            INTERNAL_DATA
        };

        float InterleavedGradientNoise(
            float2 pixelPosition)
        {
            return frac(
                52.9829189 *
                frac(
                    dot(
                        pixelPosition,
                        float2(
                            0.06711056,
                            0.00583715))));
        }

        half ResolveLocalVisibility(
            float3 worldPosition)
        {
            float3 surfaceDirection =
                CelestialSafeNormalize(
                    worldPosition -
                        _PlanetCenterScenePosition.xyz,
                    float3(0.0, 1.0, 0.0));
            float3 lodCenterDirection =
                CelestialSafeNormalize(
                    _LodCenterDirection.xyz,
                    float3(0.0, 1.0, 0.0));
            float chordDistanceMeters =
                length(
                    (surfaceDirection -
                        lodCenterDirection) *
                    _PlanetRadiusMeters);
            float blendWidthMeters =
                _LodFadeEndMeters -
                _LodFadeStartMeters;

            if (blendWidthMeters <= 0.0001)
            {
                return
                    chordDistanceMeters <=
                        _LodFadeEndMeters
                        ? 1.0h
                        : 0.0h;
            }

            return
                1.0h -
                smoothstep(
                    _LodFadeStartMeters,
                    _LodFadeEndMeters,
                    chordDistanceMeters);
        }

        half4 ResolveLayerWeights(
            float2 controlUv)
        {
            if (_LayerCount < 0.5h)
            {
                return 0.0h;
            }

            half4 weights =
                _LayerCount < 1.5h
                    ? half4(1.0h, 0.0h, 0.0h, 0.0h)
                    : saturate(
                        UNITY_SAMPLE_TEX2D(
                            _Control,
                            controlUv));
            if (_LayerCount < 3.5h) weights.a = 0.0h;
            if (_LayerCount < 2.5h) weights.b = 0.0h;
            if (_LayerCount < 1.5h) weights.g = 0.0h;
            return
                weights /
                max(
                    dot(
                        weights,
                        half4(
                            1.0h,
                            1.0h,
                            1.0h,
                            1.0h)),
                    0.0001h);
        }

        void ApplyLightingMode(
            inout SurfaceOutputStandard output,
            half3 emission)
        {
            if (_SurfaceLightingMode > 0.5h)
            {
                output.Emission =
                    emission;

                if (_SurfaceLightingMode < 1.5h)
                {
                    output.Albedo = 0.0h;
                    output.Metallic = 0.0h;
                    output.Smoothness = 0.0h;
                    output.Occlusion = 1.0h;
                }
            }
            else
            {
                output.Emission = 0.0h;
            }
        }

        void Surface(
            Input input,
            inout SurfaceOutputStandard output)
        {
            float2 pixelPosition =
                floor(
                    input.screenPos.xy /
                    max(input.screenPos.w, 0.00001) *
                    _ScreenParams.xy);
            clip(
                _TileFade -
                InterleavedGradientNoise(
                    pixelPosition +
                    float2(17.0, 31.0)));

            if (_LodMaskMode > 0.5h)
            {
                half localVisibility =
                    ResolveLocalVisibility(
                        input.worldPos);
                half lodNoise =
                    InterleavedGradientNoise(
                        pixelPosition);
                clip(
                    _LodMaskMode < 1.5h
                        ? localVisibility - lodNoise
                        : lodNoise - localVisibility);
            }

            half3 meshNormalWS =
                normalize(
                    WorldNormalVector(
                        input,
                        half3(0.0h, 0.0h, 1.0h)));
            CelestialBodySurfaceCoordinates coordinates =
                CelestialBuildSurfaceCoordinates(
                    input.worldPos,
                    meshNormalWS,
                    _PlanetCenterScenePosition.xyz,
                    _PlanetRadiusMeters,
                    _BodyNorthDirection.xyz);

            if (_DebugMode > 0.5)
            {
                output.Albedo = 0.0h;
                output.Emission =
                    CelestialResolveDebugColor(
                        _DebugMode,
                        meshNormalWS,
                        coordinates,
                        input.uv_Control,
                        _ElevationDebugMinMeters,
                        _ElevationDebugMaxMeters,
                        _CoordinateDebugScaleMeters);
                output.Normal = half3(0.0h, 0.0h, 1.0h);
                output.Metallic = 0.0h;
                output.Smoothness = 0.0h;
                output.Occlusion = 1.0h;
                output.Alpha = 1.0h;
                return;
            }

            if (_LayerCount < 0.5h)
            {
                half3 baseSurfaceColor =
                    UNITY_SAMPLE_TEX2D(
                        _BaseMap,
                        input.uv_BaseMap).rgb *
                    _BaseColor.rgb;
                output.Albedo =
                    baseSurfaceColor;
                output.Normal =
                    UnpackScaleNormal(
                        UNITY_SAMPLE_TEX2D_SAMPLER(
                            _NormalMap,
                            _BaseMap,
                            input.uv_BaseMap),
                        _NormalScale);
                output.Metallic = _Metallic;
                output.Smoothness = _Smoothness;
                output.Occlusion = _Occlusion;
                output.Alpha = 1.0h;
                half3 baseEmission =
                    baseSurfaceColor *
                    UNITY_SAMPLE_TEX2D_SAMPLER(
                        _EmissionMap,
                        _BaseMap,
                        input.uv_BaseMap).rgb *
                    _EmissionColor.rgb *
                    max(_EmissionIntensity, 0.0h);
                ApplyLightingMode(
                    output,
                    baseEmission);
                return;
            }

            half4 weights =
                ResolveLayerWeights(
                    input.uv_Control);
            float2 uv0 = input.uv_Control * _Splat0_ST.xy + _Splat0_ST.zw;
            float2 uv1 = input.uv_Control * _Splat1_ST.xy + _Splat1_ST.zw;
            float2 uv2 = input.uv_Control * _Splat2_ST.xy + _Splat2_ST.zw;
            float2 uv3 = input.uv_Control * _Splat3_ST.xy + _Splat3_ST.zw;

            half4 diffuse0 = UNITY_SAMPLE_TEX2D_SAMPLER(_Splat0, _BaseMap, uv0);
            half4 diffuse1 = UNITY_SAMPLE_TEX2D_SAMPLER(_Splat1, _BaseMap, uv1);
            half4 diffuse2 = UNITY_SAMPLE_TEX2D_SAMPLER(_Splat2, _BaseMap, uv2);
            half4 diffuse3 = UNITY_SAMPLE_TEX2D_SAMPLER(_Splat3, _BaseMap, uv3);
            half3 surfaceColor0 = diffuse0.rgb * _Tint0.rgb;
            half3 surfaceColor1 = diffuse1.rgb * _Tint1.rgb;
            half3 surfaceColor2 = diffuse2.rgb * _Tint2.rgb;
            half3 surfaceColor3 = diffuse3.rgb * _Tint3.rgb;
            output.Albedo =
                surfaceColor0 * weights.r +
                surfaceColor1 * weights.g +
                surfaceColor2 * weights.b +
                surfaceColor3 * weights.a;

            half3 normal0 = lerp(half3(0.0h, 0.0h, 1.0h), UnpackScaleNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_Normal0, _BaseMap, uv0), _NormalScale0), _HasNormal0);
            half3 normal1 = lerp(half3(0.0h, 0.0h, 1.0h), UnpackScaleNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_Normal1, _BaseMap, uv1), _NormalScale1), _HasNormal1);
            half3 normal2 = lerp(half3(0.0h, 0.0h, 1.0h), UnpackScaleNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_Normal2, _BaseMap, uv2), _NormalScale2), _HasNormal2);
            half3 normal3 = lerp(half3(0.0h, 0.0h, 1.0h), UnpackScaleNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_Normal3, _BaseMap, uv3), _NormalScale3), _HasNormal3);
            output.Normal =
                normalize(
                    normal0 * weights.r +
                    normal1 * weights.g +
                    normal2 * weights.b +
                    normal3 * weights.a);

            half4 mask0 = UNITY_SAMPLE_TEX2D_SAMPLER(_Mask0, _BaseMap, uv0);
            half4 mask1 = UNITY_SAMPLE_TEX2D_SAMPLER(_Mask1, _BaseMap, uv1);
            half4 mask2 = UNITY_SAMPLE_TEX2D_SAMPLER(_Mask2, _BaseMap, uv2);
            half4 mask3 = UNITY_SAMPLE_TEX2D_SAMPLER(_Mask3, _BaseMap, uv3);
            output.Metallic =
                lerp(_Metallic0, mask0.r, _HasMask0) * weights.r +
                lerp(_Metallic1, mask1.r, _HasMask1) * weights.g +
                lerp(_Metallic2, mask2.r, _HasMask2) * weights.b +
                lerp(_Metallic3, mask3.r, _HasMask3) * weights.a;
            output.Smoothness =
                lerp(_Smoothness0, mask0.a, _HasMask0) * weights.r +
                lerp(_Smoothness1, mask1.a, _HasMask1) * weights.g +
                lerp(_Smoothness2, mask2.a, _HasMask2) * weights.b +
                lerp(_Smoothness3, mask3.a, _HasMask3) * weights.a;
            output.Occlusion =
                lerp(1.0h, mask0.g, _HasMask0 * _OcclusionStrength0) * weights.r +
                lerp(1.0h, mask1.g, _HasMask1 * _OcclusionStrength1) * weights.g +
                lerp(1.0h, mask2.g, _HasMask2 * _OcclusionStrength2) * weights.b +
                lerp(1.0h, mask3.g, _HasMask3 * _OcclusionStrength3) * weights.a;
            output.Alpha = 1.0h;

            half3 emission0 =
                surfaceColor0 *
                UNITY_SAMPLE_TEX2D_SAMPLER(_EmissionMap0, _BaseMap, uv0).rgb *
                _EmissionColor0.rgb *
                max(_EmissionIntensity0, 0.0h);
            half3 emission1 =
                surfaceColor1 *
                UNITY_SAMPLE_TEX2D_SAMPLER(_EmissionMap1, _BaseMap, uv1).rgb *
                _EmissionColor1.rgb *
                max(_EmissionIntensity1, 0.0h);
            half3 emission2 =
                surfaceColor2 *
                UNITY_SAMPLE_TEX2D_SAMPLER(_EmissionMap2, _BaseMap, uv2).rgb *
                _EmissionColor2.rgb *
                max(_EmissionIntensity2, 0.0h);
            half3 emission3 =
                surfaceColor3 *
                UNITY_SAMPLE_TEX2D_SAMPLER(_EmissionMap3, _BaseMap, uv3).rgb *
                _EmissionColor3.rgb *
                max(_EmissionIntensity3, 0.0h);
            ApplyLightingMode(
                output,
                emission0 * weights.r +
                emission1 * weights.g +
                emission2 * weights.b +
                emission3 * weights.a);
        }
        ENDCG
    }

    Fallback "Standard"
}
