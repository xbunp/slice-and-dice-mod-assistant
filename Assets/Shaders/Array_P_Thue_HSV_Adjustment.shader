Shader "UI/Custom/Array_P_THUE_HSV_Universal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(UI Masking Properties)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // NOTE: Operation arrays are hidden because Unity's inspector cannot draw them natively.
        // They are driven by the attached C# script.
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
            #pragma target 3.0 // Target 3.0 required for loops and dynamic branching

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma shader_feature_local_fragment UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            // ==========================================
            // ARRAY PROPERTIES (Fed by C#)
            // ==========================================
            #define MAX_OPS 16 // Max number of operations allowed

            int _OpCount;
            float _OpTypes[MAX_OPS]; // 1 = PSwap, 2 = THue, 3 = GlobalHSV
            float4 _OpColorTargets[MAX_OPS]; // Target color for PSwap/THue
            float4 _OpColorReplaces[MAX_OPS]; // Replace Color (PSwap) OR HSV values (x=H, y=S, z=V)
            float4 _OpParams[MAX_OPS]; // x = range, y = rangeMult, z = shift

            // RGB to HSV Conversion
            float3 rgb2hsv(float3 c) {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            // HSV to RGB Conversion
            float3 hsv2rgb(float3 c) {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
            }

            v2f vert(appdata_t v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                half4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                
                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // Convert to Gamma space once before processing
                #ifndef UNITY_COLORSPACE_GAMMA
                    color.rgb = LinearToGammaSpace(color.rgb);
                #endif

                // Iterate through our arbitrary list of operations
                for(int j = 0; j < _OpCount; j++)
                {
                    int type = (int)_OpTypes[j];

                    // 1 = Palette Swap
                    if (type == 1) 
                    {
                        float3 pColor = _OpColorTargets[j].rgb;
                        float3 pReplace = _OpColorReplaces[j].rgb;
                        
                        #ifndef UNITY_COLORSPACE_GAMMA
                            pColor = LinearToGammaSpace(pColor);
                            pReplace = LinearToGammaSpace(pReplace);
                        #endif

                        float pDist = distance(color.rgb, pColor);
                        float pThreshold = (_OpParams[j].x / 99.0) * _OpParams[j].y;
                        float pMask = smoothstep(pThreshold + 1e-5, 0.0, pDist);
                        color.rgb = lerp(color.rgb, pReplace, pMask);
                    }
                    // 2 = Targeted Hue
                    else if (type == 2) 
                    {
                        float3 tColor = _OpColorTargets[j].rgb;
                        
                        #ifndef UNITY_COLORSPACE_GAMMA
                            tColor = LinearToGammaSpace(tColor);
                        #endif

                        float tDist = distance(color.rgb, tColor);
                        float tThreshold = (_OpParams[j].x / 99.0) * _OpParams[j].y;
                        float thueMask = step(tDist, tThreshold);

                        float3 hsv = rgb2hsv(color.rgb);
                        float thueShiftAmount = (_OpParams[j].z / 99.0) * thueMask;
                        hsv.x = frac(hsv.x + thueShiftAmount + 1.0);
                        color.rgb = hsv2rgb(hsv);
                    }
                    // 3 = Global HSV
                    else if (type == 3) 
                    {
                        float3 hsv = rgb2hsv(color.rgb);
                        // _OpColorReplaces is holding (Hue, Saturation, Value, 0)
                        hsv.x = frac(hsv.x + (_OpColorReplaces[j].x / 100.0) + 1.0);
                        hsv.y = clamp(hsv.y + (_OpColorReplaces[j].y / 100.0), 0.0, 1.0);
                        hsv.z = clamp(hsv.z + (_OpColorReplaces[j].z / 100.0), 0.0, 1.0);
                        color.rgb = hsv2rgb(hsv);
                    }
                }

                // Return to Native Pipeline Space
                #ifndef UNITY_COLORSPACE_GAMMA
                    color.rgb = GammaToLinearSpace(color.rgb);
                #endif

                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return color;
            }
            ENDCG
        }
    }
}