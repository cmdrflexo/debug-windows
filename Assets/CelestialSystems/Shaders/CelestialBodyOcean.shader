/*
 * Provides the first URP-lit, transparent, Fresnel, and diagnostic shader foundation for generated celestial ocean meshes.
 */

Shader "jcan/Celestial Systems/Celestial Body Ocean"
{
    Properties
    {
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

        [HideInInspector] _Surface ("Surface Type", Float) = 1
        [HideInInspector] _Blend ("Blend Mode", Float) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _ZWrite ("Z Write", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OceanVertex
            #pragma fragment OceanFragment

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
            #include "CelestialBodyShaderCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OceanVertex(
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
                        input.normalOS);

                output.positionCS =
                    positionInputs.positionCS;
                output.positionWS =
                    positionInputs.positionWS;
                output.normalWS =
                    normalInputs.normalWS;
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

            half4 OceanFragment(
                Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS =
                    NormalizeNormalPerPixel(
                        input.normalWS);
                half3 viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(
                        input.positionWS);
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
                        input.positionWS,
                        normalWS,
                        _PlanetCenterScenePosition.xyz,
                        _PlanetRadiusMeters,
                        _BodyNorthDirection.xyz);

                if (_DebugMode >
                    0.5)
                {
                    return half4(
                        CelestialResolveDebugColor(
                            _DebugMode,
                            normalWS,
                            coordinates,
                            input.uv,
                            _ElevationDebugMinMeters,
                            _ElevationDebugMaxMeters,
                            _CoordinateDebugScaleMeters),
                        1.0h);
                }

                half3 liquidColor =
                    lerp(
                        _DeepColor.rgb,
                        _ShallowColor.rgb,
                        viewFacing);
                SurfaceData surfaceData =
                    (SurfaceData)0;
                surfaceData.albedo =
                    liquidColor;
                surfaceData.metallic =
                    _Metallic;
                surfaceData.specular =
                    half3(
                        0.0h,
                        0.0h,
                        0.0h);
                surfaceData.smoothness =
                    _Smoothness;
                surfaceData.normalTS =
                    half3(
                        0.0h,
                        0.0h,
                        1.0h);
                surfaceData.emission =
                    half3(
                        0.0h,
                        0.0h,
                        0.0h);
                surfaceData.occlusion =
                    1.0h;
                surfaceData.alpha =
                    saturate(
                        _Opacity +
                        fresnel *
                            _FresnelOpacityBoost);
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
                    viewDirectionWS;
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
                    lerp(
                        color.rgb,
                        _FresnelColor.rgb,
                        fresnel *
                            _FresnelStrength);
                color.rgb =
                    MixFog(
                        color.rgb,
                        input.fogFactor);
                color.a =
                    surfaceData.alpha;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
