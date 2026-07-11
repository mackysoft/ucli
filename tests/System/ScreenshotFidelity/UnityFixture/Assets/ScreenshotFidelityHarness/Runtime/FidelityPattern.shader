Shader "Hidden/uCLI/ScreenshotFidelityPattern"
{
    Properties
    {
        _UseSolid ("Use Solid", Float) = 0
        _SolidColor ("Solid Color", Color) = (1, 0, 1, 1)
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
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite On
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _UseSolid;
            float4 _SolidColor;

            Varyings Vertex (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 Pattern (float2 uv)
            {
                // The four primary-color corner blocks form the WindowServer crop signature.
                if (uv.x < 0.055 && uv.y > 0.945)
                    return float4(1.0, 0.0, 0.0, 1.0);
                if (uv.x > 0.945 && uv.y > 0.945)
                    return float4(0.0, 1.0, 0.0, 1.0);
                if (uv.x < 0.055 && uv.y < 0.055)
                    return float4(0.0, 0.0, 1.0, 1.0);
                if (uv.x > 0.945 && uv.y < 0.055)
                    return float4(1.0, 1.0, 0.0, 1.0);

                float2 edgeDistance = min(uv, 1.0 - uv) * _ScreenParams.xy;
                if (edgeDistance.y < 1.25)
                    return uv.y > 0.5
                        ? float4(0.0, 1.0, 1.0, 1.0)
                        : float4(1.0, 0.0, 1.0, 1.0);
                if (edgeDistance.x < 1.25)
                    return uv.x < 0.5
                        ? float4(1.0, 0.5, 0.0, 1.0)
                        : float4(0.45, 0.0, 1.0, 1.0);

                // Keep each corner sentinel disconnected from color-graded interior pixels.
                if ((uv.x < 0.07 || uv.x > 0.93) && (uv.y < 0.07 || uv.y > 0.93))
                    return float4(0.12, 0.12, 0.12, 1.0);

                // Seventeen linear gray steps expose missing and double sRGB conversion.
                if (uv.y >= 0.41 && uv.y <= 0.59)
                {
                    float gray = floor(saturate(uv.x) * 17.0) / 16.0;
                    return float4(gray, gray, gray, 1.0);
                }

                if (uv.y >= 0.22 && uv.y <= 0.36)
                {
                    if (uv.x >= 0.12 && uv.x <= 0.22)
                        return float4(0.62, 0.11, 0.055, 1.0);
                    if (uv.x >= 0.30 && uv.x <= 0.40)
                        return float4(0.08, 0.48, 0.16, 1.0);
                    if (uv.x >= 0.48 && uv.x <= 0.58)
                        return float4(0.55, 0.25, 0.16, 1.0);
                    if (uv.x >= 0.66 && uv.x <= 0.76)
                        return float4(0.07, 0.18, 0.68, 1.0);
                }

                // The oracle requires this neutral source patch to become warm after the intentional Volume pass.
                if (uv.x >= 0.82 && uv.x <= 0.90 && uv.y >= 0.72 && uv.y <= 0.80)
                    return float4(0.35, 0.35, 0.35, 1.0);

                float3 gradient = float3(
                    0.08 + uv.x * 0.64,
                    0.06 + uv.y * 0.56,
                    0.10 + (1.0 - uv.x) * 0.42);
                return float4(gradient, 1.0);
            }

            float4 Fragment (Varyings input) : SV_Target
            {
                return _UseSolid > 0.5 ? _SolidColor : Pattern(input.uv);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Off
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthOnlyAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthOnlyVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthOnlyVaryings DepthOnlyVertex (DepthOnlyAttributes input)
            {
                DepthOnlyVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 DepthOnlyFragment () : SV_Target
            {
                return 0.0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
