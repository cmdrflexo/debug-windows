Shader "jcan/Celestial Systems/Curved MapMagic Terrain Layers"
{
    Properties
    {
        _Control ("Control (RGBA)", 2D) = "white" {}
        [HideInInspector] _LayerCount ("Layer Count", Float) = 1

        _Splat0 ("Layer 0 Diffuse", 2D) = "white" {}
        _Splat1 ("Layer 1 Diffuse", 2D) = "white" {}
        _Splat2 ("Layer 2 Diffuse", 2D) = "white" {}
        _Splat3 ("Layer 3 Diffuse", 2D) = "white" {}

        [Normal] _Normal0 ("Layer 0 Normal", 2D) = "bump" {}
        [Normal] _Normal1 ("Layer 1 Normal", 2D) = "bump" {}
        [Normal] _Normal2 ("Layer 2 Normal", 2D) = "bump" {}
        [Normal] _Normal3 ("Layer 3 Normal", 2D) = "bump" {}

        _Mask0 ("Layer 0 Mask", 2D) = "white" {}
        _Mask1 ("Layer 1 Mask", 2D) = "white" {}
        _Mask2 ("Layer 2 Mask", 2D) = "white" {}
        _Mask3 ("Layer 3 Mask", 2D) = "white" {}

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

        [HideInInspector] _Metallic0 ("Layer 0 Metallic", Range(0, 1)) = 0
        [HideInInspector] _Metallic1 ("Layer 1 Metallic", Range(0, 1)) = 0
        [HideInInspector] _Metallic2 ("Layer 2 Metallic", Range(0, 1)) = 0
        [HideInInspector] _Metallic3 ("Layer 3 Metallic", Range(0, 1)) = 0

        [HideInInspector] _Smoothness0 ("Layer 0 Smoothness", Range(0, 1)) = 0
        [HideInInspector] _Smoothness1 ("Layer 1 Smoothness", Range(0, 1)) = 0
        [HideInInspector] _Smoothness2 ("Layer 2 Smoothness", Range(0, 1)) = 0
        [HideInInspector] _Smoothness3 ("Layer 3 Smoothness", Range(0, 1)) = 0
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
        #pragma target 3.0

        #include "UnityStandardUtils.cginc"

        sampler2D _Control;

        sampler2D _Splat0;
        sampler2D _Splat1;
        sampler2D _Splat2;
        sampler2D _Splat3;

        sampler2D _Normal0;
        sampler2D _Normal1;
        sampler2D _Normal2;
        sampler2D _Normal3;

        sampler2D _Mask0;
        sampler2D _Mask1;
        sampler2D _Mask2;
        sampler2D _Mask3;

        float4 _Splat0_ST;
        float4 _Splat1_ST;
        float4 _Splat2_ST;
        float4 _Splat3_ST;

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

        struct Input
        {
            float2 uv_Control;
        };

        void Surface(
            Input input,
            inout SurfaceOutputStandard output)
        {
            half4 weights =
                saturate(
                    tex2D(
                        _Control,
                        input.uv_Control));

            if (_LayerCount < 3.5h)
            {
                weights.a = 0.0h;
            }

            if (_LayerCount < 2.5h)
            {
                weights.b = 0.0h;
            }

            if (_LayerCount < 1.5h)
            {
                weights.g = 0.0h;
            }

            if (_LayerCount < 0.5h)
            {
                weights.r = 1.0h;
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
            weights /=
                weightSum;

            float2 uv0 =
                input.uv_Control *
                    _Splat0_ST.xy +
                _Splat0_ST.zw;
            float2 uv1 =
                input.uv_Control *
                    _Splat1_ST.xy +
                _Splat1_ST.zw;
            float2 uv2 =
                input.uv_Control *
                    _Splat2_ST.xy +
                _Splat2_ST.zw;
            float2 uv3 =
                input.uv_Control *
                    _Splat3_ST.xy +
                _Splat3_ST.zw;

            fixed4 diffuse0 =
                tex2D(
                    _Splat0,
                    uv0);
            fixed4 diffuse1 =
                tex2D(
                    _Splat1,
                    uv1);
            fixed4 diffuse2 =
                tex2D(
                    _Splat2,
                    uv2);
            fixed4 diffuse3 =
                tex2D(
                    _Splat3,
                    uv3);

            output.Albedo =
                diffuse0.rgb *
                    weights.r +
                diffuse1.rgb *
                    weights.g +
                diffuse2.rgb *
                    weights.b +
                diffuse3.rgb *
                    weights.a;

            half3 normal0 =
                lerp(
                    half3(
                        0.0h,
                        0.0h,
                        1.0h),
                    UnpackScaleNormal(
                        tex2D(
                            _Normal0,
                            uv0),
                        _NormalScale0),
                    _HasNormal0);
            half3 normal1 =
                lerp(
                    half3(
                        0.0h,
                        0.0h,
                        1.0h),
                    UnpackScaleNormal(
                        tex2D(
                            _Normal1,
                            uv1),
                        _NormalScale1),
                    _HasNormal1);
            half3 normal2 =
                lerp(
                    half3(
                        0.0h,
                        0.0h,
                        1.0h),
                    UnpackScaleNormal(
                        tex2D(
                            _Normal2,
                            uv2),
                        _NormalScale2),
                    _HasNormal2);
            half3 normal3 =
                lerp(
                    half3(
                        0.0h,
                        0.0h,
                        1.0h),
                    UnpackScaleNormal(
                        tex2D(
                            _Normal3,
                            uv3),
                        _NormalScale3),
                    _HasNormal3);

            output.Normal =
                normalize(
                    normal0 *
                        weights.r +
                    normal1 *
                        weights.g +
                    normal2 *
                        weights.b +
                    normal3 *
                        weights.a);

            half4 mask0 =
                tex2D(
                    _Mask0,
                    uv0);
            half4 mask1 =
                tex2D(
                    _Mask1,
                    uv1);
            half4 mask2 =
                tex2D(
                    _Mask2,
                    uv2);
            half4 mask3 =
                tex2D(
                    _Mask3,
                    uv3);

            half metallic0 =
                lerp(
                    _Metallic0,
                    mask0.r,
                    _HasMask0);
            half metallic1 =
                lerp(
                    _Metallic1,
                    mask1.r,
                    _HasMask1);
            half metallic2 =
                lerp(
                    _Metallic2,
                    mask2.r,
                    _HasMask2);
            half metallic3 =
                lerp(
                    _Metallic3,
                    mask3.r,
                    _HasMask3);

            half smoothness0 =
                lerp(
                    _Smoothness0,
                    mask0.a,
                    _HasMask0);
            half smoothness1 =
                lerp(
                    _Smoothness1,
                    mask1.a,
                    _HasMask1);
            half smoothness2 =
                lerp(
                    _Smoothness2,
                    mask2.a,
                    _HasMask2);
            half smoothness3 =
                lerp(
                    _Smoothness3,
                    mask3.a,
                    _HasMask3);

            half occlusion0 =
                lerp(
                    1.0h,
                    mask0.g,
                    _HasMask0);
            half occlusion1 =
                lerp(
                    1.0h,
                    mask1.g,
                    _HasMask1);
            half occlusion2 =
                lerp(
                    1.0h,
                    mask2.g,
                    _HasMask2);
            half occlusion3 =
                lerp(
                    1.0h,
                    mask3.g,
                    _HasMask3);

            output.Metallic =
                metallic0 *
                    weights.r +
                metallic1 *
                    weights.g +
                metallic2 *
                    weights.b +
                metallic3 *
                    weights.a;
            output.Smoothness =
                smoothness0 *
                    weights.r +
                smoothness1 *
                    weights.g +
                smoothness2 *
                    weights.b +
                smoothness3 *
                    weights.a;
            output.Occlusion =
                occlusion0 *
                    weights.r +
                occlusion1 *
                    weights.g +
                occlusion2 *
                    weights.b +
                occlusion3 *
                    weights.a;
            output.Alpha =
                1.0h;
        }
        ENDCG
    }

    Fallback "Standard"
}
