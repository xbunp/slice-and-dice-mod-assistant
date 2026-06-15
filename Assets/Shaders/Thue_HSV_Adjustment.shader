Shader "UI/Custom/THUE_HSV_Adjustment"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Targeted Hue Adjustment (Thue))]
        _THueColor ("Target Color", Color) = (1,0,0,1)
        _THueRange ("Target Range (0 to 99)", Range(0, 99)) = 0
        _THueShift ("Target Hue Shift (-99 to 99)", Range(-99, 99)) = 0

        [Header(Global HSV Adjustments)]
        // HSV Adjustments in range [-99, 99]
        _Hue ("Hue (-99 to 99)", Range(-99, 99)) = 0
        _Saturation ("Saturation (-99 to 99)", Range(-99, 99)) = 0
        _Value ("Value (-99 to 99)", Range(-99, 99)) = 0

        [Header(UI Masking Properties)]
        // Required for UI Masking
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

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

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            // Targeted Hue Variables
            float4 _THueColor;
            float _THueRange;
            float _THueShift;

            // Global HSV Variables
            float _Hue;
            float _Saturation;
            float _Value;

            // RGB to HSV Conversion helper
            float3 rgb2hsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            // HSV to RGB Conversion helper
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

                // Convert RGB to HSV
                float3 hsv = rgb2hsv(color.rgb);


                // ==========================================
                // 1. Targeted Hue Adjustment (Thue)
                // ==========================================
                float3 targetHSV = rgb2hsv(_THueColor.rgb);

                // Calculate the shortest distance along the circular hue wheel (0.0 to 0.5 max)
                // (+1.5 guarantees a positive number inside frac() to prevent platform-specific rounding issues)
                float hueDist = abs(frac(hsv.x - targetHSV.x + 1.5) - 0.5);

                // Calculate the threshold based on the 0-99 range parameter. 
                // A range of 99 maps to 0.5 (which covers the maximum possible distance, meaning the entire color wheel).
                // A range of 50 covers up to 0.25 distance (half the possible distance away from the color).
                float hueThreshold = (_THueRange / 99.0) * 0.5;

                // Create a mask (1.0 if the pixel is within the target range, 0.0 if not)
                float thueMask = step(hueDist, hueThreshold);

                // Apply the targeted shift (mapped to 0.0 - 1.0 circle) only to the masked areas
                float thueShiftAmount = (_THueShift / 99.0) * thueMask;
                hsv.x = frac(hsv.x + thueShiftAmount + 1.0);


                // ==========================================
                // 2. Global HSV Adjustments
                // ==========================================
                // Apply Hue shift: Map [-99, 99] to [-1.0, 1.0] cycle range
                float hShift = _Hue / 100.0;
                hsv.x = frac(hsv.x + hShift + 1.0);

                // Apply Saturation shift: Map [-99, 99] to [-1.0, 1.0]
                float sShift = _Saturation / 100.0;
                hsv.y = clamp(hsv.y + sShift, 0.0, 1.0);

                // Apply Value (brightness) shift: Map [-99, 99] to [-1.0, 1.0]
                float vShift = _Value / 100.0;
                hsv.z = clamp(hsv.z + vShift, 0.0, 1.0);


                // Convert back to RGB
                color.rgb = hsv2rgb(hsv);

                // Apply UI Canvas clipping (e.g., for ScrollRect masks)
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                return color;
            }
            ENDCG
        }
    }
}