Shader "MakubeX/V006 Misty Windows" {
 Properties { _Strength("Source radiance",Float)=1 _Color("Base",Color)=(.035,.028,.02,1) }
 SubShader { Tags {"RenderType"="Opaque"} Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Strength;float4 _Color;
 struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
 float4 frag(v2f i):SV_Target {
  float3 p=float3(-i.world.x,-i.world.z,i.world.y);float n=hash(floor(p*float3(.21,.17,.27)));
  float3 c=n<.38?float3(.012,.022,.04):n<.42?float3(.025,.035,.055):n<.70?float3(.16,.095,.044):n<.86?float3(.48,.30,.13):n<.96?float3(.85,.59,.28):float3(.25,.42,.60);
  c=c*_Strength+_Color.rgb*.025;float fog=saturate((-i.world.y-2)/32)*.92;fog=max(fog,saturate((distance(i.world,_WorldSpaceCameraPos)-90)/260)*.38);c=lerp(c,float3(.014,.024,.040),fog);
  #ifdef UNITY_COLORSPACE_GAMMA
  c=LinearToGammaSpace(c);
  #endif
  return float4(c,1);
 }
 ENDCG }
 UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
 } Fallback Off
}
