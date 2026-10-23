Shader "Hidden/DotLife/TaskFrameDelta"
{
    Properties
    {
        _MainTex ("Accumulated Ink", 2D) = "white" {}
        _BeforeTex ("Previous Authored Frame", 2D) = "white" {}
        _AfterTex ("Next Authored Frame", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _BeforeTex;
            sampler2D _AfterTex;

            float4 frag(v2f_img input) : SV_Target
            {
                // Each frame contributes only its own ink delta, independent of visit order.
                return tex2D(_MainTex, input.uv) + tex2D(_AfterTex, input.uv) - tex2D(_BeforeTex, input.uv);
            }
            ENDCG
        }
    }
    Fallback Off
}
