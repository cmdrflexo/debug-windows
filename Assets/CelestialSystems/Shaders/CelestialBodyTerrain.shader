/*
 * Provides the first URP-lit and diagnostic shader foundation for generated celestial terrain meshes.
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
        [HideInInspector] _Metallic0 ("Layer 0 Metallic", Float) = 0
        [HideInInspector] _Metallic1 ("Layer 1 Metallic", Float) = 0
        [HideInInspector] _Metallic2 ("Layer 2 Metallic", Float) = 0
        [HideInInspector] _Metallic3 ("Layer 3 Metallic", Float) = 0
        [HideInInspector] _Smoothness0 ("Layer 0 Smoothness", Float) = 0
        [HideInInspector] _Smoothness1 ("Layer 1 Smoothness", Float) = 0
        [HideInInspector] _Smoothness2 ("Layer 2 Smoothness", Float) = 0
        [HideInInspector] _Smoothness3 ("Layer 3 Smoothness", Float) = 0

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
        [HideInInspector] _CoordinateDebugScaleMeters ("Coordinate Debug Scale", Float) = 1000
        [HideInInspector] _DebugMode ("Debug Mode", Float) = 0

        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface ("Surface Type", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainVertex
            #pragma fragment TerrainFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "CelestialBodyShaderCommon.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_Control);
            SAMPLER(sampler_Control);
            TEXTURE2D(_Splat0);
            SAMPLER(sampler_Splat0);
            TEXTURE2D(_Splat1);
            SAMPLER(sampler_Splat1);
            TEXTURE2D(_Splat2);
            SAMPLER(sampler_Splat2);
            TEXTURE2D(_Splat3);
            SAMPLER(sampler_Splat3);
            TEXTURE2D(_Normal0);
            SAMPLER(sampler_Normal0);
            TEXTURE2D(_Normal1);
            SAMPLER(sampler_Normal1);
            TEXTURE2D(_Normal2);
            SAMPLER(sampler_Normal2);
            TEXTURE2D(_Normal3);
            SAMPLER(sampler_Normal3);
            TEXTURE2D(_Mask0);
            SAMPLER(sampler_Mask0);
            TEXTURE2D(_Mask1);
            SAMPLER(sampler_Mask1);
            TEXTURE2D(_Mask2);
            SAMPLER(sampler_Mask2);
            TEXTURE2D(_Mask3);
            SAMPLER(sampler_Mask3);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Splat0_ST;
                float4 _Splat1_ST;
                float4 _Splat2_ST;
                float4 _Splat3_ST;
                half4 _BaseColor;
                half _NormalScale;
                half _Metallic;
                half _Smoothness;
                half _Occlusion;
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
                half _Metallic0;
                half _Metallic1;
                half _Metallic2;
                half _Metallic3;
                half _Smoothness0;
                half _Smoothness1;
                half _Smoothness2;
                half _Smoothness3;
                half _TileFade;
                half _LodMaskMode;
                float _LodFadeStartMeters;
                float _LodFadeEndMeters;
                float4 _LodCenterDirection;
                float4 _PlanetCenterScenePosition;
                float4 _BodyNorthDirection;
                float4 _BodyPoleReferenceDirection;
                float _PlanetRadiusMeters;
                float _OceanSurfaceElevationMeters;
                float _OceanRadiusMeters;
                float _ElevationDebugMinMeters;
                float _ElevationDebugMaxMeters;
                float _CoordinateDebugScaleMeters;
                float _DebugMode;
                float _CelestialSurfaceType;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings TerrainVertex(
                Attributes input)
            {
                Varyings output =
                    (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(
                    input,
                    output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(
                    output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS,
                        input.tangentOS);
                half tangentSign =
                    input.tangentOS.w *
                    GetOddNegativeScale();

                output.positionCS =
                    positionInputs.positionCS;
                output.positionWS =
                    positionInputs.positionWS;
                output.normalWS =
                    normalInputs.normalWS;
                output.tangentWS =
                    half4(
                        normalInputs.tangentWS,
                        tangentSign);
                output.uv =
                    input.uv;
                output.shadowCoord =
                    GetShadowCoord(
                        positionInputs);
                output.fogFactor =
                    ComputeFogFactor(
                        positionInputs.positionCS.z);
                return output;
            }

            float CelestialInterleavedGradientNoise(
                float2 pixelPosition)
            {
                return
                    frac(
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
                float3 planetOffset =
                    worldPosition -
                    _PlanetCenterScenePosition.xyz;
                float3 surfaceDirection =
                    CelestialSafeNormalize(
                        planetOffset,
                        float3(
                            0.0,
                            1.0,
                            0.0));
                float3 lodCenterDirection =
                    CelestialSafeNormalize(
                        _LodCenterDirection.xyz,
                        float3(
                            0.0,
                            1.0,
                            0.0));
                float chordDistanceMeters =
                    length(
                        (surfaceDirection -
                            lodCenterDirection) *
                        _PlanetRadiusMeters);
                float blendWidthMeters =
                    _LodFadeEndMeters -
                    _LodFadeStartMeters;

                if (blendWidthMeters <=
                    0.0001)
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
                if (_LayerCount <
                    0.5h)
                {
                    return
                        half4(
                            0.0h,
                            0.0h,
                            0.0h,
                            0.0h);
                }

                half4 weights =
                    _LayerCount <
                        1.5h
                        ? half4(
                            1.0h,
                            0.0h,
                            0.0h,
                            0.0h)
                        : saturate(
                            SAMPLE_TEXTURE2D(
                                _Control,
                                sampler_Control,
                                controlUv));

                if (_LayerCount <
                    3.5h)
                {
                    weights.a =
                        0.0h;
                }

                if (_LayerCount <
                    2.5h)
                {
                    weights.b =
                        0.0h;
                }

                if (_LayerCount <
                    1.5h)
                {
                    weights.g =
                        0.0h;
                }

                half weightSum =
                    max(
                        dot(
                            weights,
                            half4(
                                1.0h,
                                1.0h,
                                1.0h,
                                1.0h)),
                        0.0001h);
                return
                    weights /
                    weightSum;
            }

            half4 TerrainFragment(
                Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 pixelPosition =
                    floor(
                        input.positionCS.xy);
                half tileNoise =
                    CelestialInterleavedGradientNoise(
                        pixelPosition +
                        float2(
                            17.0,
                            31.0));
                clip(
                    _TileFade -
                    tileNoise);

                if (_LodMaskMode >
                    0.5h)
                {
                    half localVisibility =
                        ResolveLocalVisibility(
                            input.positionWS);
                    half lodNoise =
                        CelestialInterleavedGradientNoise(
                            pixelPosition);

                    if (_LodMaskMode <
                        1.5h)
                    {
                        clip(
                            localVisibility -
                            lodNoise);
                    }
                    else
                    {
                        clip(
                            lodNoise -
                            localVisibility);
                    }
                }

                half3 meshNormalWS =
                    NormalizeNormalPerPixel(
                        input.normalWS);
                half3 tangentWS =
                    normalize(
                        input.tangentWS.xyz);
                half3 bitangentWS =
                    input.tangentWS.w *
                    cross(
                        meshNormalWS,
                        tangentWS);
                float2 baseUv =
                    input.uv *
                        _BaseMap_ST.xy +
                    _BaseMap_ST.zw;
                float2 uv0 =
                    input.uv *
                        _Splat0_ST.xy +
                    _Splat0_ST.zw;
                float2 uv1 =
                    input.uv *
                        _Splat1_ST.xy +
                    _Splat1_ST.zw;
                float2 uv2 =
                    input.uv *
                        _Splat2_ST.xy +
                    _Splat2_ST.zw;
                float2 uv3 =
                    input.uv *
                        _Splat3_ST.xy +
                    _Splat3_ST.zw;
                half4 weights =
                    ResolveLayerWeights(
                        input.uv);
                half3 fallbackNormalTS =
                    UnpackNormalScale(
                        SAMPLE_TEXTURE2D(
                            _NormalMap,
                            sampler_NormalMap,
                            baseUv),
                        _NormalScale);
                half3 normal0 =
                    lerp(
                        half3(
                            0.0h,
                            0.0h,
                            1.0h),
                        UnpackNormalScale(
                            SAMPLE_TEXTURE2D(
                                _Normal0,
                                sampler_Normal0,
                                uv0),
                            _NormalScale0),
                        _HasNormal0);
                half3 normal1 =
                    lerp(
                        half3(
                            0.0h,
                            0.0h,
                            1.0h),
                        UnpackNormalScale(
                            SAMPLE_TEXTURE2D(
                                _Normal1,
                                sampler_Normal1,
                                uv1),
                            _NormalScale1),
                        _HasNormal1);
                half3 normal2 =
                    lerp(
                        half3(
                            0.0h,
                            0.0h,
                            1.0h),
                        UnpackNormalScale(
                            SAMPLE_TEXTURE2D(
                                _Normal2,
                                sampler_Normal2,
                                uv2),
                            _NormalScale2),
                        _HasNormal2);
                half3 normal3 =
                    lerp(
                        half3(
                            0.0h,
                            0.0h,
                            1.0h),
                        UnpackNormalScale(
                            SAMPLE_TEXTURE2D(
                                _Normal3,
                                sampler_Normal3,
                                uv3),
                            _NormalScale3),
                        _HasNormal3);
                half3 normalTS =
                    _LayerCount >
                        0.5h
                        ? normalize(
                            normal0 *
                                weights.r +
                            normal1 *
                                weights.g +
                            normal2 *
                                weights.b +
                            normal3 *
                                weights.a)
                        : fallbackNormalTS;
                half3 normalWS =
                    NormalizeNormalPerPixel(
                        TransformTangentToWorld(
                            normalTS,
                            half3x3(
                                tangentWS,
                                bitangentWS,
                                meshNormalWS)));
                CelestialBodySurfaceCoordinates coordinates =
                    CelestialBuildSurfaceCoordinates(
                        input.positionWS,
                        meshNormalWS,
                        _PlanetCenterScenePosition.xyz,
                        _PlanetRadiusMeters,
                        _BodyNorthDirection.xyz);

                if (_DebugMode >
                    0.5)
                {
                    return half4(
                        CelestialResolveDebugColor(
                            _DebugMode,
                            meshNormalWS,
                            coordinates,
                            input.uv,
                            _ElevationDebugMinMeters,
                            _ElevationDebugMaxMeters,
                            _CoordinateDebugScaleMeters),
                        1.0h);
                }

                half4 baseSample =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        baseUv) *
                    _BaseColor;
                half4 diffuse0 =
                    SAMPLE_TEXTURE2D(
                        _Splat0,
                        sampler_Splat0,
                        uv0);
                half4 diffuse1 =
                    SAMPLE_TEXTURE2D(
                        _Splat1,
                        sampler_Splat1,
                        uv1);
                half4 diffuse2 =
                    SAMPLE_TEXTURE2D(
                        _Splat2,
                        sampler_Splat2,
                        uv2);
                half4 diffuse3 =
                    SAMPLE_TEXTURE2D(
                        _Splat3,
                        sampler_Splat3,
                        uv3);
                half3 layerAlbedo =
                    diffuse0.rgb *
                        weights.r +
                    diffuse1.rgb *
                        weights.g +
                    diffuse2.rgb *
                        weights.b +
                    diffuse3.rgb *
                        weights.a;
                half4 mask0 =
                    SAMPLE_TEXTURE2D(
                        _Mask0,
                        sampler_Mask0,
                        uv0);
                half4 mask1 =
                    SAMPLE_TEXTURE2D(
                        _Mask1,
                        sampler_Mask1,
                        uv1);
                half4 mask2 =
                    SAMPLE_TEXTURE2D(
                        _Mask2,
                        sampler_Mask2,
                        uv2);
                half4 mask3 =
                    SAMPLE_TEXTURE2D(
                        _Mask3,
                        sampler_Mask3,
                        uv3);
                half layerMetallic =
                    lerp(
                        _Metallic0,
                        mask0.r,
                        _HasMask0) *
                        weights.r +
                    lerp(
                        _Metallic1,
                        mask1.r,
                        _HasMask1) *
                        weights.g +
                    lerp(
                        _Metallic2,
                        mask2.r,
                        _HasMask2) *
                        weights.b +
                    lerp(
                        _Metallic3,
                        mask3.r,
                        _HasMask3) *
                        weights.a;
                half layerSmoothness =
                    lerp(
                        _Smoothness0,
                        mask0.a,
                        _HasMask0) *
                        weights.r +
                    lerp(
                        _Smoothness1,
                        mask1.a,
                        _HasMask1) *
                        weights.g +
                    lerp(
                        _Smoothness2,
                        mask2.a,
                        _HasMask2) *
                        weights.b +
                    lerp(
                        _Smoothness3,
                        mask3.a,
                        _HasMask3) *
                        weights.a;
                half layerOcclusion =
                    lerp(
                        1.0h,
                        mask0.g,
                        _HasMask0) *
                        weights.r +
                    lerp(
                        1.0h,
                        mask1.g,
                        _HasMask1) *
                        weights.g +
                    lerp(
                        1.0h,
                        mask2.g,
                        _HasMask2) *
                        weights.b +
                    lerp(
                        1.0h,
                        mask3.g,
                        _HasMask3) *
                        weights.a;
                SurfaceData surfaceData =
                    (SurfaceData)0;
                surfaceData.albedo =
                    _LayerCount >
                        0.5h
                        ? layerAlbedo
                        : baseSample.rgb;
                surfaceData.metallic =
                    _LayerCount >
                        0.5h
                        ? layerMetallic
                        : _Metallic;
                surfaceData.specular =
                    half3(
                        0.0h,
                        0.0h,
                        0.0h);
                surfaceData.smoothness =
                    _LayerCount >
                        0.5h
                        ? layerSmoothness
                        : _Smoothness;
                surfaceData.normalTS =
                    normalTS;
                surfaceData.emission =
                    half3(
                        0.0h,
                        0.0h,
                        0.0h);
                surfaceData.occlusion =
                    _LayerCount >
                        0.5h
                        ? layerOcclusion
                        : _Occlusion;
                surfaceData.alpha =
                    1.0h;
                surfaceData.clearCoatMask =
                    0.0h;
                surfaceData.clearCoatSmoothness =
                    0.0h;

                InputData inputData =
                    (InputData)0;
                inputData.positionWS =
                    input.positionWS;
                inputData.positionCS =
                    input.positionCS;
                inputData.normalWS =
                    normalWS;
                inputData.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(
                        input.positionWS);
                inputData.shadowCoord =
                    input.shadowCoord;
                inputData.fogCoord =
                    input.fogFactor;
                inputData.vertexLighting =
                    VertexLighting(
                        input.positionWS,
                        normalWS);
                inputData.bakedGI =
                    SampleSH(
                        normalWS);
                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(
                        input.positionCS);
                inputData.shadowMask =
                    half4(
                        1.0h,
                        1.0h,
                        1.0h,
                        1.0h);

                half4 color =
                    UniversalFragmentPBR(
                        inputData,
                        surfaceData);
                color.rgb =
                    MixFog(
                        color.rgb,
                        input.fogFactor);
                color.a =
                    1.0h;
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
