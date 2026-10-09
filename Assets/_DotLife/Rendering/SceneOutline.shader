Shader "DotLife/Scene Outline"
{
    Properties
    {
        _OutlineColor("Line Color", Color) = (0.05,0.055,0.065,0.45)
        _Width("Width (1080p Pixels)", Range(0,3)) = 1.15
        _DepthThreshold("Depth Threshold", Range(0.001,0.1)) = 0.012
        _NormalThreshold("Normal Threshold", Range(0.05,1)) = 0.25
        _Softness("Edge Softness", Range(0.01,0.5)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "SceneEdges"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _Width, _DepthThreshold, _NormalThreshold, _Softness;
            CBUFFER_END

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(saturate(uv)), _ZBufferParams);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_Width - 0.001);
                float2 uv = input.texcoord;
                float2 stepUV = max(_Width, 0) * _ScaledScreenParams.y / 1080.0 / _ScaledScreenParams.xy;
                float center = EyeDepth(uv);
                // Second differences reject a flat sloping desk instead of outlining its entire surface.
                float depthEdge = max(abs(EyeDepth(uv - float2(stepUV.x, 0)) + EyeDepth(uv + float2(stepUV.x, 0)) - 2 * center),
                    abs(EyeDepth(uv - float2(0, stepUV.y)) + EyeDepth(uv + float2(0, stepUV.y)) - 2 * center)) / max(center, 0.001);
                float3 normal = SampleSceneNormals(uv);
                float normalEdge = 0;
                normalEdge = max(normalEdge, 1 - dot(normal, SampleSceneNormals(saturate(uv + float2(stepUV.x, 0)))));
                normalEdge = max(normalEdge, 1 - dot(normal, SampleSceneNormals(saturate(uv - float2(stepUV.x, 0)))));
                normalEdge = max(normalEdge, 1 - dot(normal, SampleSceneNormals(saturate(uv + float2(0, stepUV.y)))));
                normalEdge = max(normalEdge, 1 - dot(normal, SampleSceneNormals(saturate(uv - float2(0, stepUV.y)))));
                float strength = max(depthEdge / max(_DepthThreshold, 0.001), normalEdge / max(_NormalThreshold, 0.01));
                float edge = smoothstep(1 - _Softness, 1 + _Softness, strength);
                float foreground = step(center, _ProjectionParams.z * 0.99);
                return half4(_OutlineColor.rgb, _OutlineColor.a * edge * foreground);
            }
            ENDHLSL
        }
    }
}
