/*
 * Renders a single simple body sphere from fused cube-face control and ocean-mask textures.
 */

Shader "jcan/Celestial Systems/Celestial Body Simple Fused"
{
    Properties
    {
        [HideInInspector] _CelestialSurfaceType ("Celestial Surface Type", Float) = 0
        [HideInInspector] _PlanetCenterScenePosition ("Planet Center", Vector) = (0,0,0,1)
        [HideInInspector] _PlanetRadiusMeters ("Planet Radius", Float) = 1
        [HideInInspector] _SurfaceLightingMode ("Lighting Mode", Float) = 0

        [HideInInspector] _ControlFaces ("Control Faces", 2DArray) = "" {}
        [HideInInspector] _OceanFaces ("Ocean Faces", 2DArray) = "" {}

        [HideInInspector] _LayerMap0 ("Layer 0", 2D) = "white" {}
        [HideInInspector] _LayerMap1 ("Layer 1", 2D) = "white" {}
        [HideInInspector] _LayerMap2 ("Layer 2", 2D) = "white" {}
        [HideInInspector] _LayerMap3 ("Layer 3", 2D) = "white" {}
        [HideInInspector] _LayerTint0 ("Layer 0 Tint", Color) = (1,1,1,1)
        [HideInInspector] _LayerTint1 ("Layer 1 Tint", Color) = (1,1,1,1)
        [HideInInspector] _LayerTint2 ("Layer 2 Tint", Color) = (1,1,1,1)
        [HideInInspector] _LayerTint3 ("Layer 3 Tint", Color) = (1,1,1,1)
        [HideInInspector] _LayerScale0 ("Layer 0 Scale", Float) = 1
        [HideInInspector] _LayerScale1 ("Layer 1 Scale", Float) = 1
        [HideInInspector] _LayerScale2 ("Layer 2 Scale", Float) = 1
        [HideInInspector] _LayerScale3 ("Layer 3 Scale", Float) = 1
        [HideInInspector] _LayerMetallic0 ("Layer 0 Metallic", Float) = 0
        [HideInInspector] _LayerMetallic1 ("Layer 1 Metallic", Float) = 0
        [HideInInspector] _LayerMetallic2 ("Layer 2 Metallic", Float) = 0
        [HideInInspector] _LayerMetallic3 ("Layer 3 Metallic", Float) = 0
        [HideInInspector] _LayerSmoothness0 ("Layer 0 Smoothness", Float) = 0.25
        [HideInInspector] _LayerSmoothness1 ("Layer 1 Smoothness", Float) = 0.25
        [HideInInspector] _LayerSmoothness2 ("Layer 2 Smoothness", Float) = 0.25
        [HideInInspector] _LayerSmoothness3 ("Layer 3 Smoothness", Float) = 0.25
        [HideInInspector] _LayerOcclusion0 ("Layer 0 Occlusion", Float) = 1
        [HideInInspector] _LayerOcclusion1 ("Layer 1 Occlusion", Float) = 1
        [HideInInspector] _LayerOcclusion2 ("Layer 2 Occlusion", Float) = 1
        [HideInInspector] _LayerOcclusion3 ("Layer 3 Occlusion", Float) = 1
        [HideInInspector][HDR] _LayerEmission0 ("Layer 0 Emission", Color) = (0,0,0,1)
        [HideInInspector][HDR] _LayerEmission1 ("Layer 1 Emission", Color) = (0,0,0,1)
        [HideInInspector][HDR] _LayerEmission2 ("Layer 2 Emission", Color) = (0,0,0,1)
        [HideInInspector][HDR] _LayerEmission3 ("Layer 3 Emission", Color) = (0,0,0,1)

        [HideInInspector] _OceanMetallic ("Ocean Metallic", Float) = 0
        [HideInInspector] _OceanSmoothness ("Ocean Smoothness", Float) = 0.92
        [HideInInspector] _OceanFresnelColor ("Ocean Fresnel Color", Color) = (0.35,0.65,0.8,1)
        [HideInInspector] _OceanFresnelPower ("Ocean Fresnel Power", Float) = 5
        [HideInInspector] _OceanFresnelStrength ("Ocean Fresnel Strength", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SimpleVertex
            #pragma fragment SimpleFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_ControlFaces); SAMPLER(sampler_ControlFaces);
            TEXTURE2D_ARRAY(_OceanFaces); SAMPLER(sampler_OceanFaces);
            TEXTURE2D(_LayerMap0); SAMPLER(sampler_LayerMap0);
            TEXTURE2D(_LayerMap1); SAMPLER(sampler_LayerMap1);
            TEXTURE2D(_LayerMap2); SAMPLER(sampler_LayerMap2);
            TEXTURE2D(_LayerMap3); SAMPLER(sampler_LayerMap3);

            CBUFFER_START(UnityPerMaterial)
                float4 _PlanetCenterScenePosition;
                float _PlanetRadiusMeters;
                float _SurfaceLightingMode;
                half4 _LayerTint0;
                half4 _LayerTint1;
                half4 _LayerTint2;
                half4 _LayerTint3;
                float _LayerScale0;
                float _LayerScale1;
                float _LayerScale2;
                float _LayerScale3;
                half _LayerMetallic0;
                half _LayerMetallic1;
                half _LayerMetallic2;
                half _LayerMetallic3;
                half _LayerSmoothness0;
                half _LayerSmoothness1;
                half _LayerSmoothness2;
                half _LayerSmoothness3;
                half _LayerOcclusion0;
                half _LayerOcclusion1;
                half _LayerOcclusion2;
                half _LayerOcclusion3;
                half4 _LayerEmission0;
                half4 _LayerEmission1;
                half4 _LayerEmission2;
                half4 _LayerEmission3;
                half _OceanMetallic;
                half _OceanSmoothness;
                half4 _OceanFresnelColor;
                half _OceanFresnelPower;
                half _OceanFresnelStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 directionOS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half3 vertexLighting : TEXCOORD4;
            };

            Varyings SimpleVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positions =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals =
                    GetVertexNormalInputs(input.normalOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.directionOS = normalize(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                output.vertexLighting =
                    VertexLighting(positions.positionWS, normals.normalWS);
                return output;
            }

            float2 DirectionToFaceUv(float tangentU, float tangentV)
            {
                const float inverseHalfFaceAngle = 1.27323954473516;
                return saturate(
                    float2(
                        atan(clamp(tangentU, -1.0, 1.0)),
                        atan(clamp(tangentV, -1.0, 1.0))) *
                    inverseHalfFaceAngle *
                    0.5 +
                    0.5);
            }

            void ResolveFace(float3 direction, out int face, out float2 uv)
            {
                float3 absoluteDirection = abs(direction);

                if (absoluteDirection.x >= absoluteDirection.y &&
                    absoluteDirection.x >= absoluteDirection.z)
                {
                    if (direction.x >= 0.0)
                    {
                        face = 0;
                        uv = DirectionToFaceUv(
                            -direction.z / absoluteDirection.x,
                            direction.y / absoluteDirection.x);
                    }
                    else
                    {
                        face = 1;
                        uv = DirectionToFaceUv(
                            direction.z / absoluteDirection.x,
                            direction.y / absoluteDirection.x);
                    }
                }
                else if (absoluteDirection.y >= absoluteDirection.z)
                {
                    if (direction.y >= 0.0)
                    {
                        face = 2;
                        uv = DirectionToFaceUv(
                            direction.x / absoluteDirection.y,
                            -direction.z / absoluteDirection.y);
                    }
                    else
                    {
                        face = 3;
                        uv = DirectionToFaceUv(
                            direction.x / absoluteDirection.y,
                            direction.z / absoluteDirection.y);
                    }
                }
                else if (direction.z >= 0.0)
                {
                    face = 4;
                    uv = DirectionToFaceUv(
                        direction.x / absoluteDirection.z,
                        direction.y / absoluteDirection.z);
                }
                else
                {
                    face = 5;
                    uv = DirectionToFaceUv(
                        -direction.x / absoluteDirection.z,
                        direction.y / absoluteDirection.z);
                }
            }

            half4 SampleControl(int face, float2 uv)
            {
                return SAMPLE_TEXTURE2D_ARRAY(
                    _ControlFaces,
                    sampler_ControlFaces,
                    uv,
                    face);
            }

            half4 SampleOcean(int face, float2 uv)
            {
                return SAMPLE_TEXTURE2D_ARRAY(
                    _OceanFaces,
                    sampler_OceanFaces,
                    uv,
                    face);
            }

            void SampleFusedFaces(
                float3 direction,
                out half4 control,
                out half4 ocean)
            {
                float3 absoluteDirection =
                    abs(direction);
                float largestComponent =
                    max(
                        absoluteDirection.x,
                        max(
                            absoluteDirection.y,
                            absoluteDirection.z));
                float3 relativeComponents =
                    absoluteDirection /
                    max(
                        largestComponent,
                        0.0001);
                half3 faceWeights =
                    smoothstep(
                        0.82,
                        1.0,
                        relativeComponents);
                half totalWeight =
                    max(
                        faceWeights.x +
                            faceWeights.y +
                            faceWeights.z,
                        0.0001);
                faceWeights /=
                    totalWeight;

                control =
                    half4(
                        0.0,
                        0.0,
                        0.0,
                        0.0);
                ocean =
                    half4(
                        0.0,
                        0.0,
                        0.0,
                        0.0);

                if (faceWeights.x > 0.0)
                {
                    int face =
                        direction.x >= 0.0
                            ? 0
                            : 1;
                    float inverseComponent =
                        rcp(
                            max(
                                absoluteDirection.x,
                                0.0001));
                    float tangentU =
                        direction.x >= 0.0
                            ? -direction.z *
                                inverseComponent
                            : direction.z *
                                inverseComponent;
                    float2 uv =
                        DirectionToFaceUv(
                            tangentU,
                            direction.y *
                                inverseComponent);
                    control +=
                        SampleControl(
                            face,
                            uv) *
                        faceWeights.x;
                    ocean +=
                        SampleOcean(
                            face,
                            uv) *
                        faceWeights.x;
                }

                if (faceWeights.y > 0.0)
                {
                    int face =
                        direction.y >= 0.0
                            ? 2
                            : 3;
                    float inverseComponent =
                        rcp(
                            max(
                                absoluteDirection.y,
                                0.0001));
                    float tangentV =
                        direction.y >= 0.0
                            ? -direction.z *
                                inverseComponent
                            : direction.z *
                                inverseComponent;
                    float2 uv =
                        DirectionToFaceUv(
                            direction.x *
                                inverseComponent,
                            tangentV);
                    control +=
                        SampleControl(
                            face,
                            uv) *
                        faceWeights.y;
                    ocean +=
                        SampleOcean(
                            face,
                            uv) *
                        faceWeights.y;
                }

                if (faceWeights.z > 0.0)
                {
                    int face =
                        direction.z >= 0.0
                            ? 4
                            : 5;
                    float inverseComponent =
                        rcp(
                            max(
                                absoluteDirection.z,
                                0.0001));
                    float tangentU =
                        direction.z >= 0.0
                            ? direction.x *
                                inverseComponent
                            : -direction.x *
                                inverseComponent;
                    float2 uv =
                        DirectionToFaceUv(
                            tangentU,
                            direction.y *
                                inverseComponent);
                    control +=
                        SampleControl(
                            face,
                            uv) *
                        faceWeights.z;
                    ocean +=
                        SampleOcean(
                            face,
                            uv) *
                        faceWeights.z;
                }
            }

            half3 TriplanarWeights(float3 direction)
            {
                half3 weights = pow(abs(direction), 4.0);
                return weights / max(dot(weights, 1.0), 0.0001);
            }

            half3 SampleTriplanar(
                TEXTURE2D_PARAM(textureMap, samplerMap),
                float3 direction,
                float scaleMeters)
            {
                float3 meters =
                    direction *
                    _PlanetRadiusMeters /
                    max(scaleMeters, 0.001);
                half3 weights =
                    TriplanarWeights(direction);
                half3 x =
                    SAMPLE_TEXTURE2D(
                        textureMap,
                        samplerMap,
                        meters.zy).rgb;
                half3 y =
                    SAMPLE_TEXTURE2D(
                        textureMap,
                        samplerMap,
                        meters.xz).rgb;
                half3 z =
                    SAMPLE_TEXTURE2D(
                        textureMap,
                        samplerMap,
                        meters.xy).rgb;
                return
                    x * weights.x +
                    y * weights.y +
                    z * weights.z;
            }

            half4 SimpleFragment(Varyings input) : SV_Target
            {
                float3 direction =
                    normalize(input.directionOS);
                half4 weights;
                half4 ocean;
                SampleFusedFaces(
                    direction,
                    weights,
                    ocean);
                weights /=
                    max(
                        dot(weights, 1.0),
                        0.0001);
                half oceanMask =
                    step(
                        0.5,
                        ocean.a);

                half3 layer0 =
                    SampleTriplanar(
                        TEXTURE2D_ARGS(_LayerMap0, sampler_LayerMap0),
                        direction,
                        _LayerScale0) *
                    _LayerTint0.rgb;
                half3 layer1 =
                    SampleTriplanar(
                        TEXTURE2D_ARGS(_LayerMap1, sampler_LayerMap1),
                        direction,
                        _LayerScale1) *
                    _LayerTint1.rgb;
                half3 layer2 =
                    SampleTriplanar(
                        TEXTURE2D_ARGS(_LayerMap2, sampler_LayerMap2),
                        direction,
                        _LayerScale2) *
                    _LayerTint2.rgb;
                half3 layer3 =
                    SampleTriplanar(
                        TEXTURE2D_ARGS(_LayerMap3, sampler_LayerMap3),
                        direction,
                        _LayerScale3) *
                    _LayerTint3.rgb;
                half3 terrainAlbedo =
                    layer0 * weights.x +
                    layer1 * weights.y +
                    layer2 * weights.z +
                    layer3 * weights.w;
                half metallic =
                    dot(
                        weights,
                        half4(
                            _LayerMetallic0,
                            _LayerMetallic1,
                            _LayerMetallic2,
                            _LayerMetallic3));
                half smoothness =
                    dot(
                        weights,
                        half4(
                            _LayerSmoothness0,
                            _LayerSmoothness1,
                            _LayerSmoothness2,
                            _LayerSmoothness3));
                half occlusion =
                    dot(
                        weights,
                        half4(
                            _LayerOcclusion0,
                            _LayerOcclusion1,
                            _LayerOcclusion2,
                            _LayerOcclusion3));
                half3 emission =
                    _LayerEmission0.rgb * weights.x +
                    _LayerEmission1.rgb * weights.y +
                    _LayerEmission2.rgb * weights.z +
                    _LayerEmission3.rgb * weights.w;

                half3 normalWS =
                    NormalizeNormalPerPixel(
                        input.normalWS);
                half3 viewDirectionWS =
                    SafeNormalize(
                        GetWorldSpaceViewDir(
                            input.positionWS));
                half fresnel =
                    pow(
                        1.0 -
                            saturate(
                                dot(
                                    normalWS,
                                    viewDirectionWS)),
                        max(
                            _OceanFresnelPower,
                            0.001));
                half3 oceanAlbedo =
                    ocean.rgb +
                    _OceanFresnelColor.rgb *
                    fresnel *
                    _OceanFresnelStrength;

                half3 albedo =
                    lerp(
                        terrainAlbedo,
                        oceanAlbedo,
                        oceanMask);
                metallic =
                    lerp(
                        metallic,
                        _OceanMetallic,
                        oceanMask);
                smoothness =
                    lerp(
                        smoothness,
                        _OceanSmoothness,
                        oceanMask);
                occlusion =
                    lerp(
                        occlusion,
                        1.0,
                        oceanMask);
                emission *=
                    1.0 -
                    oceanMask;

                if (_SurfaceLightingMode > 0.5 &&
                    _SurfaceLightingMode < 1.5)
                {
                    return half4(
                        emission,
                        1.0);
                }

                SurfaceData surfaceData =
                    (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0.0, 0.0, 0.0);
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = half3(0.0, 0.0, 1.0);
                surfaceData.occlusion = occlusion;
                surfaceData.emission =
                    _SurfaceLightingMode > 1.5
                        ? emission
                        : half3(0.0, 0.0, 0.0);
                surfaceData.alpha = 1.0;

                InputData lightingInput =
                    (InputData)0;
                lightingInput.positionWS =
                    input.positionWS;
                lightingInput.normalWS =
                    normalWS;
                lightingInput.viewDirectionWS =
                    viewDirectionWS;
                lightingInput.shadowCoord =
                    TransformWorldToShadowCoord(
                        input.positionWS);
                lightingInput.fogCoord =
                    input.fogFactor;
                lightingInput.vertexLighting =
                    input.vertexLighting;
                lightingInput.bakedGI =
                    SampleSH(
                        normalWS);
                lightingInput.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(
                        input.positionCS);
                lightingInput.shadowMask =
                    half4(1.0, 1.0, 1.0, 1.0);

                half4 color =
                    UniversalFragmentPBR(
                        lightingInput,
                        surfaceData);
                color.rgb =
                    MixFog(
                        color.rgb,
                        input.fogFactor);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
