Shader "Zootopia/Stage"
{
    // The lobby showroom's backdrop: a dark curved wall drawn procedurally in world space (sharp at any resolution):
    // a soft spotlight glow behind the character, panel seams, vertical LED strips and an amber skirting light.
    Properties
    {
        _Center ("Stage Centre", Vector) = (0, 0, 0, 0)
        _Base ("Base", Color) = (0.05, 0.07, 0.11, 1)
        _Top ("Top", Color) = (0.015, 0.02, 0.035, 1)
        _Glow ("Glow", Color) = (0.16, 0.5, 0.95, 1)
        _Accent ("Accent", Color) = (1, 0.7, 0.2, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Center;
            fixed4 _Base, _Top, _Glow, _Accent;

            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // 1 on a line of half-width w (in the same units as d), anti-aliased over one pixel
            float Line (float d, float w)
            {
                float aa = fwidth(d) + 1e-5;
                return 1.0 - smoothstep(w, w + aa, abs(d));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 p = i.wp - _Center.xyz;
                float h = p.y;
                float ang = atan2(p.x, p.z);          // 0 = straight behind the character
                float r = length(p.xz);
                float arc = ang * r;                   // metres along the wall

                float3 col = lerp(_Base.rgb, _Top.rgb, saturate(h / 7.0));

                // spotlight glow on the wall behind the character
                float g = exp(-ang * ang / (2.0 * 0.32 * 0.32)) * exp(-(h - 2.1) * (h - 2.1) / (2.0 * 1.8 * 1.8));
                col += _Glow.rgb * g * 0.5;

                // panel seams
                float sv = (frac(arc / 2.4 + 0.5) - 0.5) * 2.4;
                float sh = (frac(h / 1.6 + 0.5) - 0.5) * 1.6;
                float seam = max(Line(sv, 0.01), Line(sh, 0.01)) * step(0.3, h);
                col *= 1.0 - 0.45 * seam;

                // vertical LED strips with a soft halo
                float a = abs(arc);
                float fade = smoothstep(0.3, 0.9, h) * (1.0 - smoothstep(5.0, 6.8, h));
                float strip = max(Line(a - 2.9, 0.025), Line(a - 5.8, 0.025));
                float halo = exp(-(a - 2.9) * (a - 2.9) / 0.06) + exp(-(a - 5.8) * (a - 5.8) / 0.06);
                col += _Glow.rgb * (strip * 1.8 + halo * 0.18) * fade;

                // amber skirting light along the floor
                float sk = Line(h - 0.14, 0.012);
                col += _Accent.rgb * (sk * 1.4 + exp(-(h - 0.14) * (h - 0.14) / 0.01) * 0.12);

                // darker towards the sides, so the eye stays on the character
                col *= lerp(1.0, 0.45, saturate(abs(ang) / 2.0));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
