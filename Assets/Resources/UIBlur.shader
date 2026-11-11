Shader "Custom/UIBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0,10)) = 3
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _BlurSize;
            fixed4 _Color;

            v2f vert(appdata_t v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 offset =
                    _MainTex_TexelSize.xy * _BlurSize;

                fixed4 col = 0;

                col += tex2D(_MainTex, i.uv + offset * float2(-2,-2));
                col += tex2D(_MainTex, i.uv + offset * float2(-1,-1));
                col += tex2D(_MainTex, i.uv + offset * float2( 0, 0));
                col += tex2D(_MainTex, i.uv + offset * float2( 1, 1));
                col += tex2D(_MainTex, i.uv + offset * float2( 2, 2));

                col /= 5.0;

                return col * _Color;
            }

            ENDCG
        }
    }
}