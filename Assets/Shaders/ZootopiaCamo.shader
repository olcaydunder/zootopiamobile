Shader "Zootopia/Camo"
{
    Properties
    {
        _PatternTex ("Pattern (RGB colour, A glow mask)", 2D) = "white" {}
        _Scale ("Pattern Scale", Float) = 3
        _GlowColor ("Glow Colour", Color) = (0, 0, 0, 0)
        _GlowPulse ("Glow Pulse Speed", Float) = 2
        _Flow ("Pattern Flow Speed", Float) = 0
        _Shininess ("Shininess", Range(0.03, 1)) = 0.5
        _Gloss ("Gloss", Range(0, 1)) = 0.5
        _SpecColor ("Specular", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf BlinnPhong vertex:vert
        #pragma target 3.0

        sampler2D _PatternTex;
        float _Scale;
        fixed4 _GlowColor;
        float _GlowPulse;
        float _Flow;
        half _Shininess;
        half _Gloss;

        struct Input
        {
            float3 objPos;
            float3 objNormal;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
            o.objNormal = v.normal;
        }

        // Triplanar projection in object space: works on models without UVs.
        fixed4 Triplanar(float3 p, float3 n)
        {
            float3 w = pow(abs(n), 4.0);
            w /= max(0.0001, w.x + w.y + w.z);
            float2 flow = float2(_Time.y * _Flow, _Time.y * _Flow * 0.37);
            fixed4 x = tex2D(_PatternTex, p.zy * _Scale + flow);
            fixed4 y = tex2D(_PatternTex, p.xz * _Scale + flow);
            fixed4 z = tex2D(_PatternTex, p.xy * _Scale + flow);
            return x * w.x + y * w.y + z * w.z;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 t = Triplanar(IN.objPos, normalize(IN.objNormal));
            o.Albedo = t.rgb;
            float pulse = 0.65 + 0.35 * sin(_Time.y * _GlowPulse + IN.objPos.z * 6.0);
            o.Emission = _GlowColor.rgb * _GlowColor.a * t.a * pulse;
            o.Specular = _Shininess;
            o.Gloss = _Gloss;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
