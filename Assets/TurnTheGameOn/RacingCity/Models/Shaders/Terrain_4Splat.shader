Shader "TurnTheGameOn/Terrain/Splat-4" {
    Properties {
        _BaseColor ("Base Color", Color) = (0.2705882,0.3215686,0.09019608,1)
        _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,1)
        _SplatMap ("Splat Map", 2D) = "white" {}
        _SplatRed ("Splat Red", 2D) = "white" {}
        _SplatRedNormal ("Splat Red Normal", 2D) = "bump" {}
        _SplatGreen ("Splat Green", 2D) = "white" {}
        _SplatGreenNormal ("Splat Green Normal", 2D) = "bump" {}
        _SplatBlue ("Splat Blue", 2D) = "white" {}
        _SplatBlueNormal ("Splat Blue Normal", 2D) = "bump" {}
        _SplatAlpha ("Splat Alpha", 2D) = "white" {}
        _SplatAlphaNormal ("Splat Alpha Normal", 2D) = "bump" {}
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //#define UNITY_PASS_FORWARDBASE
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 3.0
            uniform float4 _LightColor0;
            uniform sampler2D _SplatRed; uniform float4 _SplatRed_ST;
            uniform sampler2D _SplatGreen; uniform float4 _SplatGreen_ST;
            uniform sampler2D _SplatBlue; uniform float4 _SplatBlue_ST;
            uniform sampler2D _SplatAlpha; uniform float4 _SplatAlpha_ST;
            uniform sampler2D _SplatMap; uniform float4 _SplatMap_ST;
            uniform sampler2D _SplatRedNormal; uniform float4 _SplatRedNormal_ST;
            uniform sampler2D _SplatGreenNormal; uniform float4 _SplatGreenNormal_ST;
            uniform sampler2D _SplatBlueNormal; uniform float4 _SplatBlueNormal_ST;
            uniform sampler2D _SplatAlphaNormal; uniform float4 _SplatAlphaNormal_ST;
            uniform float4 _TintColor;
            uniform float4 _BaseColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 bitangentDir : TEXCOORD4;
                LIGHTING_COORDS(5,6)
                UNITY_FOG_COORDS(7)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _SplatRedNormal_var = UnpackNormal(tex2D(_SplatRedNormal,TRANSFORM_TEX(i.uv0, _SplatRedNormal)));
                float4 _SplatMap_var = tex2D(_SplatMap,TRANSFORM_TEX(i.uv0, _SplatMap));
                float3 _SplatGreenNormal_var = UnpackNormal(tex2D(_SplatGreenNormal,TRANSFORM_TEX(i.uv0, _SplatGreenNormal)));
                float3 _SplatBlueNormal_var = UnpackNormal(tex2D(_SplatBlueNormal,TRANSFORM_TEX(i.uv0, _SplatBlueNormal)));
                float3 _SplatAlphaNormal_var = UnpackNormal(tex2D(_SplatAlphaNormal,TRANSFORM_TEX(i.uv0, _SplatAlphaNormal)));
                float3 normalLocal = lerp(lerp(lerp(lerp(_SplatRedNormal_var.rgb,_SplatRedNormal_var.rgb,_SplatMap_var.r),_SplatGreenNormal_var.rgb,_SplatMap_var.g),_SplatBlueNormal_var.rgb,_SplatMap_var.b),_SplatAlphaNormal_var.rgb,_SplatMap_var.a);
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
/////// Diffuse:
                float NdotL = max(0.0,dot( normalDirection, lightDirection ));
                float3 directDiffuse = max( 0.0, NdotL) * attenColor;
                float3 indirectDiffuse = float3(0,0,0);
                indirectDiffuse += UNITY_LIGHTMODEL_AMBIENT.rgb; // Ambient Light
                float4 _SplatRed_var = tex2D(_SplatRed,TRANSFORM_TEX(i.uv0, _SplatRed));
                float4 _SplatGreen_var = tex2D(_SplatGreen,TRANSFORM_TEX(i.uv0, _SplatGreen));
                float4 _SplatBlue_var = tex2D(_SplatBlue,TRANSFORM_TEX(i.uv0, _SplatBlue));
                float4 _SplatAlpha_var = tex2D(_SplatAlpha,TRANSFORM_TEX(i.uv0, _SplatAlpha));
                float3 diffuseColor = (_TintColor.rgb*lerp(lerp(lerp(lerp(_BaseColor.rgb,_SplatRed_var.rgb,_SplatMap_var.r),_SplatGreen_var.rgb,_SplatMap_var.g),_SplatBlue_var.rgb,_SplatMap_var.b),_SplatAlpha_var.rgb,_SplatMap_var.a));
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse;
                fixed4 finalRGBA = fixed4(finalColor,1);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "FORWARD_DELTA"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //#define UNITY_PASS_FORWARDADD
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 3.0
            uniform float4 _LightColor0;
            uniform sampler2D _SplatRed; uniform float4 _SplatRed_ST;
            uniform sampler2D _SplatGreen; uniform float4 _SplatGreen_ST;
            uniform sampler2D _SplatBlue; uniform float4 _SplatBlue_ST;
            uniform sampler2D _SplatAlpha; uniform float4 _SplatAlpha_ST;
            uniform sampler2D _SplatMap; uniform float4 _SplatMap_ST;
            uniform sampler2D _SplatRedNormal; uniform float4 _SplatRedNormal_ST;
            uniform sampler2D _SplatGreenNormal; uniform float4 _SplatGreenNormal_ST;
            uniform sampler2D _SplatBlueNormal; uniform float4 _SplatBlueNormal_ST;
            uniform sampler2D _SplatAlphaNormal; uniform float4 _SplatAlphaNormal_ST;
            uniform float4 _TintColor;
            uniform float4 _BaseColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 bitangentDir : TEXCOORD4;
                LIGHTING_COORDS(5,6)
                UNITY_FOG_COORDS(7)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _SplatRedNormal_var = UnpackNormal(tex2D(_SplatRedNormal,TRANSFORM_TEX(i.uv0, _SplatRedNormal)));
                float4 _SplatMap_var = tex2D(_SplatMap,TRANSFORM_TEX(i.uv0, _SplatMap));
                float3 _SplatGreenNormal_var = UnpackNormal(tex2D(_SplatGreenNormal,TRANSFORM_TEX(i.uv0, _SplatGreenNormal)));
                float3 _SplatBlueNormal_var = UnpackNormal(tex2D(_SplatBlueNormal,TRANSFORM_TEX(i.uv0, _SplatBlueNormal)));
                float3 _SplatAlphaNormal_var = UnpackNormal(tex2D(_SplatAlphaNormal,TRANSFORM_TEX(i.uv0, _SplatAlphaNormal)));
                float3 normalLocal = lerp(lerp(lerp(lerp(_SplatRedNormal_var.rgb,_SplatRedNormal_var.rgb,_SplatMap_var.r),_SplatGreenNormal_var.rgb,_SplatMap_var.g),_SplatBlueNormal_var.rgb,_SplatMap_var.b),_SplatAlphaNormal_var.rgb,_SplatMap_var.a);
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                //float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = 1.0 * _LightColor0.xyz;
/////// Diffuse:
                float NdotL = max(0.0,dot( normalDirection, lightDirection ));
                float3 directDiffuse = max( 0.0, NdotL) * attenColor;
                float4 _SplatRed_var = tex2D(_SplatRed,TRANSFORM_TEX(i.uv0, _SplatRed));
                float4 _SplatGreen_var = tex2D(_SplatGreen,TRANSFORM_TEX(i.uv0, _SplatGreen));
                float4 _SplatBlue_var = tex2D(_SplatBlue,TRANSFORM_TEX(i.uv0, _SplatBlue));
                float4 _SplatAlpha_var = tex2D(_SplatAlpha,TRANSFORM_TEX(i.uv0, _SplatAlpha));
                float3 diffuseColor = (_TintColor.rgb*lerp(lerp(lerp(lerp(_BaseColor.rgb,_SplatRed_var.rgb,_SplatMap_var.r),_SplatGreen_var.rgb,_SplatMap_var.g),_SplatBlue_var.rgb,_SplatMap_var.b),_SplatAlpha_var.rgb,_SplatMap_var.a));
                float3 diffuse = directDiffuse * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse;
                fixed4 finalRGBA = fixed4(finalColor * 1,0);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}