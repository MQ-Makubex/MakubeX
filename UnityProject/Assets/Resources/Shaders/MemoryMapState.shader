Shader "MakubeX/MemoryMapState" {
 Properties { _MainTex ("Artwork", 2D) = "white" {} _Completed ("Completed chapter", Float) = 0 _Entrance ("Entrance",Range(0,1))=1 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   sampler2D _MainTex; float _Completed,_Entrance;
   float cloudHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float cloudNoise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(cloudHash(a),cloudHash(a+float2(1,0)),b.x),lerp(cloudHash(a+float2(0,1)),cloudHash(a+1),b.x),b.y);}
   v2f vert(appdata v) {
    v2f o;float t=saturate(_Entrance),rest=1-t;
    float diagonal=dot(float2(v.uv.x,1-v.uv.y),float2(.78,.62));
    float front=t*1.65-.12;
    float edge=1-smoothstep(0,.24,abs(diagonal-front));
    float flutter=sin(v.uv.x*12+v.uv.y*7-t*18)*36*rest;
    v.vertex.xy+=float2(-170*rest*rest+sin(v.uv.y*8-t*8)*12*rest,110*rest*rest+flutter+edge*55*rest);
    o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;
   }
   fixed4 frag(v2f i):SV_Target {
    fixed4 c=tex2D(_MainTex,i.uv)*i.color; float2 p=float2(i.uv.x,1-i.uv.y)*float2(1672,941);
    float2 centres[8]={float2(281,273),float2(620,185),float2(768,405),float2(1031,220),float2(1294,405),float2(851,626),float2(1158,690),float2(1519,686)};
    for(int k=0;k<8;k++) {
     float d=distance(p,centres[k]); float mask=1-smoothstep(86,94,d);
     if(k>=_Completed) {
      float grey=dot(c.rgb,float3(.299,.587,.114));
      float available=k==_Completed;
      c.rgb=lerp(c.rgb,lerp(grey.xxx*.42,c.rgb*.82,available),mask);
     } else {
      float rim=smoothstep(86,89,d)*(1-smoothstep(92,97,d));
      c.rgb+=float3(.32,.21,.06)*rim;
     }
    }
    if(_Entrance<.9999) {
     float2 top=float2(i.uv.x,1-i.uv.y);
     float n=cloudNoise(top*float2(9,5)+_Entrance*float2(1.7,-.8));
     float diagonal=dot(top,float2(.78,.62))+(n-.5)*.14;
     float front=_Entrance*1.65-.12;
     float reveal=1-smoothstep(front-.16,front+.035,diagonal);
     float fold=1-smoothstep(0,.20,abs(diagonal-front));
     c.rgb*=1-fold*.28*(1-_Entrance);
     c.rgb+=float3(.10,.14,.18)*fold*(1-_Entrance);
     c.a*=reveal;
    }
    return c;
   }
   ENDCG
  }
 }
}
