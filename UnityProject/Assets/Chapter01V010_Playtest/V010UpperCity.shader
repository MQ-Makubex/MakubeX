Shader "MakubeX/V010 Upper City Iron" {
 Properties {
  _Dark("Shadow palette",Color)=(.02,.03,.04,1)
  _Light("Weathered palette",Color)=(.08,.09,.10,1)
  _RustAmount("Rust patches",Range(0,1))=.12
  _Metallic("Metallic",Range(0,1))=.5
  _Glossiness("Smoothness",Range(0,1))=.3
 }
 SubShader { Tags {"RenderType"="Opaque"}
 Pass { Tags {"LightMode"="ForwardBase"} CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fwdbase
 #pragma target 3.0
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 float4 _Dark,_Light; float _RustAmount,_Metallic,_Glossiness;
 struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
 v2f vert(appdata_base v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o; }
 float hash2(float2 p){p=frac(p*float2(.1031,.1030));p+=dot(p,p.yx+33.33);return frac((p.x+p.y)*p.x);}
 float noise2(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3.-2.*f);return lerp(lerp(hash2(i),hash2(i+float2(1,0)),f.x),lerp(hash2(i+float2(0,1)),hash2(i+float2(1,1)),f.x),f.y);}
 float4 frag(v2f i):SV_Target {
  float3 n=normalize(i.normal);float3 world=i.world;
  // One coherent low-frequency sample is stable across shared meshes.
  // Vertical planes use height; roof planes use their horizontal coordinates.
  float2 wall=float2(world.x+world.z*.71,world.y);
  float2 roof=world.xz;float2 uv=lerp(wall,roof,smoothstep(.55,.9,abs(n.y)))*.28;
  float grain=noise2(uv);
  float3 palette=lerp(_Dark.rgb,_Light.rgb,smoothstep(.12,.92,grain));
  float rust=smoothstep(.72,.94,grain)*_RustAmount;
  palette=lerp(palette,float3(.12,.048,.020),rust);
  float3 sun=normalize(UnityWorldSpaceLightDir(world));float3 view=normalize(_WorldSpaceCameraPos-world);
  float3 diffuse=float3(.10,.145,.20)+_LightColor0.rgb*saturate(dot(n,sun));
  float glint=pow(saturate(dot(n,normalize(view+sun))),24)*.008*_Metallic;
  float3 color=palette*diffuse+_LightColor0.rgb*glint;
  #ifdef UNITY_COLORSPACE_GAMMA
  color=LinearToGammaSpace(color);
  #endif
  return float4(color,1);
 }
 ENDCG }
 UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
 } Fallback Off
}
