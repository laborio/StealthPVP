Shader "StealthPVP/FogOfWarOverlay"
{
    Properties
    {
        _FogColor("Fog Color", Color) = (0,0,0,0.8)
        _EdgeSoftness("Edge Softness", Range(0,0.5)) = 0.12
    }

    SubShader
    {
        // URP screen-space overlay: reconstructs world position from depth to avoid parallax.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FogOfWarScreenSpace"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _EdgeSoftness;
            CBUFFER_END

            float4 _FogWorldMin;
            float4 _FogWorldMax;

            // Depth and fog textures
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D(_FogOfWarTex);
            SAMPLER(sampler_FogOfWarTex);
            float4 _FogOfWarTex_TexelSize;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                float3 worldPos = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);

                float2 fogUV = (worldPos.xz - _FogWorldMin.xz) / (_FogWorldMax.xz - _FogWorldMin.xz);
                fogUV = saturate(fogUV);

                // Use pre-blurred texture and remap to keep contrast.
                float visibility = SAMPLE_TEXTURE2D(_FogOfWarTex, sampler_FogOfWarTex, fogUV).r;
                visibility = smoothstep(0.5 - _EdgeSoftness, 0.5 + _EdgeSoftness, visibility);
                float fogFactor = 1.0 - visibility;
                float alpha = fogFactor * _FogColor.a;
                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
