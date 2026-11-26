// Pós-efeito da câmera (pipeline padrão): brilho estourado das lâmpadas (bloom), curva suave nos claros,
// correção de cor (calor, saturação, contraste) e vinheta. Usado pelo componente PostFx.
Shader "Hidden/Cacheta/PostFx"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _BloomTex_TexelSize;

    float4 _Threshold;      // x: limiar, y: joelho, z: 2 * joelho, w: 0.25 / joelho
    float _Intensity;
    float4 _BloomTint;
    float _Exposure;
    float _Shoulder;        // a partir daqui os claros são comprimidos (em vez de estourar no branco)
    float4 _WhiteBalance;   // multiplica a cor (calor)
    float _Saturation;
    float _Contrast;
    float _Vignette;
    float4 _VignetteColor;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Box (sampler2D tex, float2 uv, float2 texel, float delta)
    {
        float4 o = texel.xyxy * float2(-delta, delta).xxyy;
        half3 s = tex2D(tex, uv + o.xy).rgb + tex2D(tex, uv + o.zy).rgb + tex2D(tex, uv + o.xw).rgb + tex2D(tex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half3 Prefilter (half3 c)
    {
        half b = max(c.r, max(c.g, c.b));
        half soft = clamp(b - _Threshold.x + _Threshold.y, 0, _Threshold.z);
        soft = soft * soft * _Threshold.w;
        half contribution = max(soft, b - _Threshold.x) / max(b, 0.00001);
        return c * contribution;
    }

    half3 Shoulder (half3 x)
    {
        half s = _Shoulder;
        half3 over = max(x - s, 0);
        return min(x, s) + (1 - s) * (1 - exp(-over / (1 - s)));
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: reduz e separa só o que passa do limiar
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                return half4(Prefilter(Box(_MainTex, i.uv, _MainTex_TexelSize.xy, 1)), 1);
            }
            ENDCG
        }

        // 1: reduz
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Box(_MainTex, i.uv, _MainTex_TexelSize.xy, 1), 1); }
            ENDCG
        }

        // 2: aumenta e soma na camada de cima
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Box(_MainTex, i.uv, _MainTex_TexelSize.xy, 0.5), 1); }
            ENDCG
        }

        // 3: junta tudo e corrige a cor
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                float2 uvb = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) uvb.y = 1 - uvb.y;
                #endif
                half3 c = tex2D(_MainTex, i.uv).rgb;
                c += Box(_BloomTex, uvb, _BloomTex_TexelSize.xy, 0.5) * _Intensity * _BloomTint.rgb;
                c = Shoulder(c * _Exposure);

                #if !defined(UNITY_COLORSPACE_GAMMA)
                c = LinearToGammaSpace(c);
                #endif
                c *= _WhiteBalance.rgb;
                half l = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = lerp(l.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                float2 d = (uvb - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1);
                half v = smoothstep(0.45, 1.25, length(d) * 1.4);
                c = lerp(c, c * _VignetteColor.rgb, v * _Vignette);
                c = saturate(c);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                c = GammaToLinearSpace(c);
                #endif
                return half4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
