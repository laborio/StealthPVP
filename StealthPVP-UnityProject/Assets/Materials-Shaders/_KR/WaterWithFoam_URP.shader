Shader "Custom/WaterWithFoam_URP"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.1, 0.4, 0.7, 0.8)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamDistance ("Foam Distance", Range(0, 2)) = 0.5
        _FoamIntensity ("Foam Intensity", Range(0, 1)) = 1.0
        
        // Wave properties
        _WaveSpeed ("Wave Speed", Range(0, 2)) = 0.5
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.1)) = 0.02
        _WaveFrequency ("Wave Frequency", Range(0, 10)) = 2.0
        
        // Surface properties
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        
        // Depth transparency
        _DepthTransparency ("Depth Transparency", Range(0, 10)) = 2.0
        _ShallowAlpha ("Shallow Water Alpha", Range(0, 1)) = 0.3
        _DeepAlpha ("Deep Water Alpha", Range(0, 1)) = 0.9
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _FoamColor;
                float _FoamDistance;
                float _FoamIntensity;
                float _WaveSpeed;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _Smoothness;
                float _Metallic;
                float _DepthTransparency;
                float _ShallowAlpha;
                float _DeepAlpha;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Calculate wave displacement
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float wave = sin((positionWS.x + positionWS.z) * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmplitude;
                
                // Apply wave to vertex position
                float3 displacedPositionOS = input.positionOS.xyz;
                displacedPositionOS.y += wave;
                
                // Transform to clip space
                output.positionWS = TransformObjectToWorld(displacedPositionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                
                // Calculate screen position for depth sampling
                output.screenPos = ComputeScreenPos(output.positionCS);
                
                // Pass through UVs
                output.uv = input.uv;
                
                // Transform normal to world space
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // Calculate fog
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample scene depth
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                
                // Calculate depth difference for foam
                float depthDifference = sceneDepth - surfaceDepth;
                float foam = 1.0 - saturate(depthDifference / _FoamDistance);
                foam = pow(foam, 2.0) * _FoamIntensity;
                
                // Calculate water transparency based on depth
                float depthFactor = saturate(depthDifference / _DepthTransparency);
                float waterAlpha = lerp(_ShallowAlpha, _DeepAlpha, depthFactor);
                
                // Mix water color with foam (simple opaque foam)
                half4 finalColor = lerp(_WaterColor, _FoamColor, foam);
                
                // Apply depth-based transparency (foam stays opaque)
                finalColor.a = lerp(waterAlpha, 1.0, foam);
                
                // Simple specular highlight
                Light mainLight = GetMainLight();
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 halfVector = normalize(mainLight.direction + viewDir);
                float NdotH = saturate(dot(input.normalWS, halfVector));
                float specular = pow(NdotH, _Smoothness * 128.0) * _Smoothness;
                
                // Add specular to color
                finalColor.rgb += specular * mainLight.color * 0.5;
                
                // Apply fog
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
