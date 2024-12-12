Shader "Custom/Accumulator"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.uv = v.uv;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            sampler2D _MainTex;
            sampler2D currentFrame;
            int frameNumber;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 oldColor = tex2D(_MainTex, i.uv);
                float4 newColor = tex2D(currentFrame, i.uv);

                float weight = 1.0/(frameNumber+1);
                return saturate(oldColor*(1-weight) + newColor*(weight));
            }
            ENDCG
        }
    }
}
