Shader "MakubeX/MemoryMapFog" {
 Properties { _MainTex ("Unused UI texture",2D)="white"{} _Entrance ("Entrance",Range(0,1))=0 }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
   float _Entrance;
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
   fixed4 frag(v2f i):SV_Target {
    float2 p=i.uv*float2(6,3.5);p+=float2(_Time.y*.028,-_Time.y*.019);
    float n=noise(p)*.63+noise(p*2.1+4.7)*.27+noise(p*4.2)*.1;
    float veil=1-smoothstep(.45,1,_Entrance);
    float3 c=lerp(float3(.025,.047,.082),float3(.26,.33,.41),smoothstep(.15,.9,n));
    c+=float3(.035,.044,.052)*pow(saturate(1-distance(i.uv,float2(.22,.8))),3);
    return float4(c,veil);
   }
   ENDCG
  }
 }
}
