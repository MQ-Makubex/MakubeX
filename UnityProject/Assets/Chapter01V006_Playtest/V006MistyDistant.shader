Shader "MakubeX/V006 Misty Distant" {
 Properties { _Color("Source palette",Color)=(.06,.08,.1,1) _Metallic("Metallic",Range(0,1))=.6 _Glossiness("Smoothness",Range(0,1))=.3 }
 SubShader { Tags {"RenderType"="Opaque"}
 Pass { Tags {"LightMode"="ForwardBase"} CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fwdbase
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 float4 _Color;float _Metallic,_Glossiness;
 struct v2f {float4 pos:SV_POSITION;float3 color:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);float3 p=mul(unity_ObjectToWorld,v.vertex).xyz,n=UnityObjectToWorldNormal(v.normal);
  float3 sun=normalize(UnityWorldSpaceLightDir(p));float3 view=normalize(_WorldSpaceCameraPos-p);
  float3 ambient=float3(.10,.145,.20);float3 light=ambient+_LightColor0.rgb*saturate(dot(n,sun));
  float glint=pow(saturate(dot(n,normalize(view+sun))),20)*.035*_Metallic;
  float fog=saturate((-p.y-2)/32)*.92;fog=max(fog,saturate((distance(p,_WorldSpaceCameraPos)-90)/260)*.38);o.color=lerp(_Color.rgb*light+_LightColor0.rgb*glint,float3(.014,.024,.040),fog);return o;}
 float4 frag(v2f i):SV_Target {float3 c=i.color;
 #ifdef UNITY_COLORSPACE_GAMMA
 c=LinearToGammaSpace(c);
 #endif
 return float4(c,1);}
 ENDCG }
 UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
 } Fallback Off
}
