Shader "Custom/WaterWithFoam_URP"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.1, 0.4, 0.7, 0.8)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamDistance ("Foam Distance", Range(0, 2)) = 0.5
        _FoamIntensity ("Foam Intensity", Range(0, 1)) = 1.0
        
        // Normal map
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 20)) = 1.0
        _NormalScale ("Normal Tiling", Float) = 1.0
        _NormalSpeed ("Normal Scroll Speed", Vector) = (0.1, 0.1, 0, 0)
        _NormalSpeed2 ("Second Normal Scroll Speed", Vector) = (-0.08, 0.05, 0, 0)
        
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
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                float fogFactor : TEXCOORD6;
            };
            
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _FoamColor;
                float _FoamDistance;
                float _FoamIntensity;
                float _NormalStrength;
                float _NormalScale;
                float4 _NormalSpeed;
                float4 _NormalSpeed2;
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
                
                // Calculate tangent and bitangent for normal mapping
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
                float3 bitangent = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
                output.bitangentWS = TransformObjectToWorldDir(bitangent);
                
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
                
                // Sample normal map twice with different UVs and speeds for animation
                float2 uv1 = input.positionWS.xz * _NormalScale + _NormalSpeed.xy * _Time.y;
                float2 uv2 = input.positionWS.xz * _NormalScale * 0.8 + _NormalSpeed2.xy * _Time.y;
                
                float3 normalMap1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1));
                float3 normalMap2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2));
                
                // Blend the two normal maps
                float3 blendedNormal = normalize(normalMap1 + normalMap2);
                blendedNormal.xy *= _NormalStrength;
                blendedNormal = normalize(blendedNormal);
                
                // Transform normal from tangent space to world space
                float3 tangent = normalize(input.tangentWS.xyz);
                float3 bitangent = normalize(input.bitangentWS);
                float3 normal = normalize(input.normalWS);
                float3x3 tangentToWorld = float3x3(tangent, bitangent, normal);
                float3 worldNormal = normalize(mul(blendedNormal, tangentToWorld));
                
                // Mix water color with foam (simple opaque foam)
                half4 finalColor = lerp(_WaterColor, _FoamColor, foam);
                
                // Apply depth-based transparency (foam stays opaque)
                finalColor.a = lerp(waterAlpha, 1.0, foam);
                
                // Simple specular highlight using the normal mapped surface
                Light mainLight = GetMainLight();
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 halfVector = normalize(mainLight.direction + viewDir);
                float NdotH = saturate(dot(worldNormal, halfVector));
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
