Shader "Hidden/jcan/Celestial Systems/Automatic Exposure"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Meter Luminance"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment MeterLuminanceFragment

            // CHANGED: URP Core defines TEXTURE2D_X and stereo texture helpers used by Blit.hlsl.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 MeterLuminanceFragment(
                Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(
                    input);
                float3 color =
                    max(
                        SAMPLE_TEXTURE2D_X(
                            _BlitTexture,
                            sampler_LinearClamp,
                            input.texcoord).rgb,
                        0.0);
                float luminance =
                    dot(
                        color,
                        float3(
                            0.2126,
                            0.7152,
                            0.0722));

                return float4(
                    luminance,
                    0.0,
                    0.0,
                    1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Apply Exposure"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment ApplyExposureFragment

            // CHANGED: URP Core defines TEXTURE2D_X and stereo texture helpers used by Blit.hlsl.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            StructuredBuffer<float> _ExposureState;

            float4 ApplyExposureFragment(
                Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(
                    input);
                float4 color =
                    SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_LinearClamp,
                        input.texcoord);
                color.rgb *=
                    exp2(
                        _ExposureState[0]);
                return color;
            }
            ENDHLSL
        }
    }
}
