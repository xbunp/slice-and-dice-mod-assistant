Shader "UI/Custom/P_THUE_HSV_Complete"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Palette Swap (P Swap))]
        _PColor ("P-Target Color", Color) = (1,1,1,1)
        _PReplaceColor ("P-Replace Color", Color) = (1,1,1,1)
        _PRange ("P-Target Range (0 to 99)", Range(0, 99)) = 0
        _PRangeMultiplier ("P-Range Multiplier", Float) = 1.46

        [Header(Targeted Hue Adjustment (Thue))]
        _THueColor ("Target Color", Color) = (1,0,0,1)
        _THueRange ("Target Range (0 to 99)", Range(0, 99)) = 0
        _THueRangeMultiplier ("Target Range Multiplier", Float) = 1.46
        _THueShift ("Target Hue Shift (-99 to 99)", Range(-99, 99)) = 0

        [Header(Global HSV Adjustments)]
        _Hue ("Hue (-99 to 99)", Range(-99, 99)) = 0
        _Saturation ("Saturation (-99 to 99)", Range(-99, 99)) = 0
        _Value ("Value (-99 to 99)", Range(-99, 99)) = 0

        [Header(Asymmetric Hue Multiplier Controls)]
        _HueBias ("Hue Linear Bias (Sign Control)", Range(-1.0, 1.0)) = 0.12
        _HueAsym ("Hue Asymmetric Balance", Range(-1.0, 1.0)) = 0.0

        [Header(UI Masking Properties)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma shader_feature_local_fragment UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float4 _PColor, _PReplaceColor;
            float _PRange, _PRangeMultiplier;
            float4 _THueColor;
            float _THueRange, _THueRangeMultiplier, _THueShift;
            float _Hue, _Saturation, _Value;
            float _HueBias, _HueAsym;

            // RGB to HSV Conversion
            float3 rgb2hsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            // HSV to RGB Conversion
            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // ==========================================
                // 1. Palette Swap (P-Swap - 3D RGB Euclidean)
                // ==========================================
                float pColorDist = distance(color.rgb, _PColor.rgb);
                float pThreshold = (_PRange / 99.0) * _PRangeMultiplier;
                
                // Smooth distance falloff interpolates exact matches at 1.0 down to 0.0 at threshold
                float pMask = smoothstep(pThreshold + 1e-5, 0.0, pColorDist);
                
                // Direct color blend ensures exact target matches output 100% of the replacement color
                color.rgb = lerp(color.rgb, _PReplaceColor.rgb, pMask);
                

                // ==========================================
                // 2. Targeted Hue Adjustment (Thue - 3D RGB Euclidean)
                // ==========================================
                float tColorDist = distance(color.rgb, _THueColor.rgb);
                float tThreshold = (_THueRange / 99.0) * _THueRangeMultiplier;
                float thueMask = step(tColorDist, tThreshold);

                float3 hsv = rgb2hsv(color.rgb);
                float thueShiftAmount = (_THueShift / 99.0) * thueMask;
                hsv.x = frac(hsv.x + thueShiftAmount + 1.0);


                // ==========================================
                // 3. Global HSV Adjustments (Asymmetric Multiplier)
                // ==========================================
                float normHue = _Hue / 100.0;
                float dynamicMultiplier = 1.0 + (normHue * _HueBias) + (abs(normHue) * _HueAsym);
                float scaledHue = normHue * dynamicMultiplier;

                hsv.x = frac(hsv.x + scaledHue + 1.0);
                hsv.y = clamp(hsv.y + (_Saturation / 100.0), 0.0, 1.0);
                hsv.z = clamp(hsv.z + (_Value / 100.0), 0.0, 1.0);

                color.rgb = hsv2rgb(hsv);
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return color;
            }
            ENDCG
        }
    }
}