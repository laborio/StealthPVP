Shader "Custom/WaterWithFoam"
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
        
        // Specular
        _Glossiness ("Smoothness", Range(0, 1)) = 0.8
        _Metallic ("Metallic", Range(0, 1)) = 0.0
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        
        CGPROGRAM
        #pragma surface surf Standard alpha finalcolor:ResetAlpha vertex:vert
        #pragma target 3.0
        
        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
            float3 worldPos;
        };
        
        sampler2D _CameraDepthTexture;
        
        fixed4 _WaterColor;
        fixed4 _FoamColor;
        float _FoamDistance;
        float _FoamIntensity;
        float _WaveSpeed;
        float _WaveAmplitude;
        float _WaveFrequency;
        half _Glossiness;
        half _Metallic;
        
        // Vertex shader for wave animation
        void vert(inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float wave = sin((worldPos.x + worldPos.z) * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmplitude;
            v.vertex.y += wave;
        }
        
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Sample the depth texture
            float screenDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(IN.screenPos)));
            float surfaceDepth = IN.screenPos.z;
            float depthDifference = screenDepth - surfaceDepth;
            
            // Calculate foam based on depth difference
            float foam = 1.0 - saturate(depthDifference / _FoamDistance);
            foam = pow(foam, 2.0) * _FoamIntensity;
            
            // Add some noise to foam edge
            float noisePattern = frac(sin(dot(IN.worldPos.xz * 10.0, float2(12.9898, 78.233))) * 43758.5453);
            foam *= smoothstep(0.3, 1.0, noisePattern + 0.3);
            
            // Mix water color with foam
            fixed4 finalColor = lerp(_WaterColor, _FoamColor, foam);
            
            o.Albedo = finalColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = finalColor.a;
        }
        
        void ResetAlpha(Input IN, SurfaceOutputStandard o, inout fixed4 color)
        {
            color.a = _WaterColor.a;
        }
        
        ENDCG
    }
    
    FallBack "Transparent/Diffuse"
}
