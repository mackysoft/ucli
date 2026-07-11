Shader "Hidden/uCLI/ScreenshotNormalize"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        HLSLINCLUDE
        #include "UnityCG.cginc"

        struct VertexToFragment
        {
            float4 position : SV_POSITION;
            float2 outputUv : TEXCOORD0;
        };

        VertexToFragment FullscreenTriangleVertex (uint vertexId : SV_VertexID)
        {
            VertexToFragment output;
            output.outputUv = float2((vertexId << 1) & 2, vertexId & 2);
            output.position = float4(output.outputUv * 2.0 - 1.0, 0.0, 1.0);
            return output;
        }
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FullscreenTriangleVertex
            #pragma fragment NormalizeFragment

            sampler2D _SourceTex;
            float4 _SourceUvTransform;

            float4 NormalizeFragment (VertexToFragment input) : SV_Target
            {
                float2 sourceUv = input.outputUv * _SourceUvTransform.xy + _SourceUvTransform.zw;
                float4 source = tex2D(_SourceTex, sourceUv);
                return float4(source.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FullscreenTriangleVertex
            #pragma fragment CalibrationFragment

            float4 CalibrationFragment (VertexToFragment input) : SV_Target
            {
                if (input.outputUv.y < 0.5)
                {
                    return input.outputUv.x < 0.5
                        ? float4(1.0, 0.0, 0.0, 1.0)
                        : float4(0.0, 1.0, 0.0, 1.0);
                }

                return input.outputUv.x < 0.5
                    ? float4(0.0, 0.0, 1.0, 1.0)
                    : float4(1.0, 1.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
