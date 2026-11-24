// Sombreado "chapado" (2-3 tons) com contorno preto grosso, para o visual de desenho com traço forte.
// Pipeline padrão (Built-in) do Unity. O contorno é feito com "casca invertida": o modelo é desenhado de novo,
// inflado pelas normais e só com as faces de trás, em preto. A espessura é em pixels (constante na tela).
Shader "Cacheta/ToonOutline"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _MainTex ("Textura", 2D) = "white" {}
        _ShadowTint ("Cor da sombra", Color) = (0.55, 0.45, 0.75, 1)
        _MidTone ("Tom intermediário", Range(0,1)) = 0.78
        _ShadowTone ("Tom da sombra", Range(0,1)) = 0.58
        _OutlineColor ("Cor do contorno", Color) = (0.06, 0.05, 0.07, 1)
        _OutlineWidth ("Espessura do contorno (px em 1080p)", Range(0, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        // ---------------------------------------------------------------- contorno
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 normVS = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 dir = mul((float2x2)UNITY_MATRIX_P, normVS.xy);
                dir = normalize(dir + 1e-5);
                float px = _OutlineWidth * (_ScreenParams.y / 1080.0);
                o.pos.xy += dir * px * 2.0 * o.pos.w / _ScreenParams.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return _OutlineColor; }
            ENDCG
        }

        // ---------------------------------------------------------------- cor chapada
        Pass
        {
            Name "TOON"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ShadowTint;
            float _MidTone;
            float _ShadowTone;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                SHADOW_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float3 n = normalize(i.worldNormal);
                float ndl = dot(n, normalize(_WorldSpaceLightPos0.xyz));
                float shadow = SHADOW_ATTENUATION(i);
                float l = min(ndl, shadow * 2.0 - 1.0);

                // 3 faixas com borda levemente suavizada
                float lit = smoothstep(0.28, 0.33, l);
                float mid = smoothstep(-0.12, -0.07, l);
                float tone = lerp(_ShadowTone, lerp(_MidTone, 1.0, lit), mid);

                float3 shade = lerp(_ShadowTint.rgb, float3(1,1,1), tone);
                float3 light = lerp(float3(1,1,1), _LightColor0.rgb, 0.35);
                float3 col = albedo * shade * light + albedo * ShadeSH9(float4(n, 1)) * 0.15;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
