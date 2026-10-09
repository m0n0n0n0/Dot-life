Shader "DotLife/Toon Lit"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,1)) = 0.25
        [Header(Toon Bands)]
        [ToggleUI] _ThreeBands("Three Bands", Float) = 1
        _ShadeColor("Shade Tint", Color) = (0.35,0.4,0.55,1)
        _MidtoneColor("Midtone Tint", Color) = (0.7,0.75,0.85,1)
        _LitColor("Lit Tint", Color) = (1,1,1,1)
        _ShadeThreshold("Shade Threshold", Range(0,0.9)) = 0.3
        _LightThreshold("Light Threshold", Range(0.1,1)) = 0.7
        _BandSoftness("Band Softness", Range(0.001,0.2)) = 0.035
        [Header(Lighting)]
        _DirectStrength("Main Light Strength", Range(0,3)) = 1.2
        _AmbientStrength("Ambient Strength", Range(0,2)) = 0.55
        _FillStrength("Additional Light Strength", Range(0,2)) = 0.45
        _SpecColor("Highlight Color", Color) = (1,0.98,0.9,1)
        _SpecularStrength("Highlight Strength", Range(0,1)) = 0.04
        _SpecularThreshold("Highlight Threshold", Range(0.5,0.999)) = 0.96
        [Header(Outline)]
        [ToggleUI] _OutlineEnabled("Outline Enabled", Float) = 0
        _OutlineWidth("Outline Width (1080p Pixels)", Range(0,5)) = 2.5
        _OutlineColor("Outline Color", Color) = (0.035,0.035,0.045,1)
        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [ToggleUI] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _Surface("Surface Type", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1
        [HideInInspector] _DstBlend("Destination Blend", Float) = 0
        [HideInInspector] _SrcBlendAlpha("Source Alpha Blend", Float) = 1
        [HideInInspector] _DstBlendAlpha("Destination Alpha Blend", Float) = 0
        [HideInInspector] _ZWrite("Depth Write", Float) = 1
        [HideInInspector] _PremultiplyAlpha("Premultiply Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull [_Cull]
        ZWrite [_ZWrite]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadeColor, _MidtoneColor, _LitColor, _SpecColor;
            half _BumpScale, _ThreeBands, _ShadeThreshold, _LightThreshold, _BandSoftness;
            half _DirectStrength, _AmbientStrength, _FillStrength, _SpecularStrength, _SpecularThreshold;
            half _Cull, _AlphaClip, _Cutoff;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _OutlineEnabled;
            half _Surface, _PremultiplyAlpha;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 uv : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 tangentWS : TEXCOORD3;
            half3 vertexLighting : TEXCOORD4;
            half fog : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.normalWS = normal.normalWS;
            output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            output.vertexLighting = VertexLighting(position.positionWS, normal.normalWS);
            output.fog = ComputeFogFactor(position.positionCS.z);
            return output;
        }
        half4 Surface(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            if (_AlphaClip > 0.5h) clip(color.a - _Cutoff);
            return color;
        }
        half3 Normal(Varyings input)
        {
            half3 normal = NormalizeNormalPerPixel(input.normalWS);
            half3 tangent = SafeNormalize(input.tangentWS.xyz);
            half3 bitangent = cross(normal, tangent) * input.tangentWS.w;
            half3 mapped = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
            return NormalizeNormalPerPixel(TransformTangentToWorld(mapped, half3x3(tangent, bitangent, normal)));
        }
        half3 BandTint(half brightness)
        {
            // Derivative-aware edges keep the bands readable without a hard pixel staircase.
            half width = max(_BandSoftness, fwidth(brightness));
            half lower = smoothstep(_ShadeThreshold - width, _ShadeThreshold + width, brightness);
            half upperThreshold = max(_LightThreshold, _ShadeThreshold + 0.001h);
            half upper = smoothstep(upperThreshold - width, upperThreshold + width, brightness);
            half3 two = lerp(_ShadeColor.rgb, _LitColor.rgb, lower);
            half3 three = lerp(lerp(_ShadeColor.rgb, _MidtoneColor.rgb, lower), _LitColor.rgb, upper);
            return lerp(two, three, saturate(_ThreeBands));
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 surface = Surface(input.uv);
                half3 normal = Normal(input);
                Light main = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half brightness = saturate(dot(normal, main.direction)) * main.shadowAttenuation;
                half3 lighting = BandTint(brightness) * main.color * main.distanceAttenuation * _DirectStrength;
                lighting += SampleSH(normal) * _AmbientStrength;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    lighting += input.vertexLighting * _FillStrength;
                #elif defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        lighting += LightingLambert(light.color, light.direction, normal)
                            * light.distanceAttenuation * light.shadowAttenuation * _FillStrength;
                    LIGHT_LOOP_END
                #endif
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half specular = saturate(dot(normal, SafeNormalize(main.direction + view)));
                half edge = max(_BandSoftness * 0.25h, fwidth(specular));
                half highlight = smoothstep(_SpecularThreshold - edge, _SpecularThreshold + edge, specular);
                half3 color = surface.rgb * lighting + highlight * _SpecularStrength * _SpecColor.rgb
                    * main.color * main.distanceAttenuation * main.shadowAttenuation;
                if (_Surface > 0.5h && _OutlineEnabled > 0.5h)
                {
                    // Alpha silhouettes stay inside the transparent paper; never fill its holes or write depth.
                    float width = max(_OutlineWidth, 0) * _ScreenParams.y / 1080.0;
                    float2 dx = ddx(input.uv) * width, dy = ddy(input.uv) * width;
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                    half edge = abs(alpha - SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + dx).a);
                    edge = max(edge, abs(alpha - SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv - dx).a));
                    edge = max(edge, abs(alpha - SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + dy).a));
                    edge = max(edge, abs(alpha - SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv - dy).a));
                    color = lerp(color, _OutlineColor.rgb, smoothstep(0.05h, 0.2h, edge) * _OutlineColor.a);
                }
                color = MixFog(color, input.fog);
                if (_PremultiplyAlpha > 0.5h) color *= surface.a;
                return half4(color, surface.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode"="DotLifeOutline" }
            Cull Front
            ZWrite Off
            ZTest Less
            // Keep coplanar backs from leaking through a recessed front surface.
            Offset 1, 1
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            OutlineVaryings OutlineVert(Attributes input)
            {
                OutlineVaryings output = (OutlineVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);
                float4 directionCS = mul(UNITY_MATRIX_VP, float4(TransformObjectToWorldNormal(input.normalOS), 0));
                float2 ndc = output.positionCS.xy / max(abs(output.positionCS.w), 0.0001);
                float2 pixels = (directionCS.xy - ndc * directionCS.w) * _ScreenParams.xy;
                pixels /= max(length(pixels), 0.0001);
                // Reference-height units keep the line proportional at 1080p and 1440p, independent of zoom.
                float width = max(_OutlineWidth, 0) * _ScreenParams.y / 1080.0;
                output.positionCS.xy += pixels * (2.0 * width / _ScreenParams.xy) * output.positionCS.w;
                return output;
            }
            half4 OutlineFrag(OutlineVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_OutlineEnabled - 0.5h);
                clip(_OutlineWidth - 0.001);
                clip(0.5h - _Surface);
                Surface(input.uv);
                return half4(MixFog(_OutlineColor.rgb, input.fog), _OutlineColor.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = Vert(input);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - output.positionWS);
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, normal, direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Surface(input.uv);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Surface(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                Surface(input.uv);
                // Scene edges use geometry, not hand-painted/normal-map detail.
                float3 normal = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
