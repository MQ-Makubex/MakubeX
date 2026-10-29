Shader "MakubeX/V005 Night Gradient" { SubShader { Tags {"Queue"="Background" "RenderType"="Background"} Cull Off ZWrite Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v2f{float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
 float4 frag(v2f i):SV_Target {float h=pow(saturate(abs(normalize(i.dir).y)),.55);float3 c=lerp(float3(.014,.024,.040),float3(.0015,.003,.006),h);
 #ifdef UNITY_COLORSPACE_GAMMA
 c=LinearToGammaSpace(c);
 #endif
 return float4(c,1);}
 ENDCG }
 } Fallback Off }
