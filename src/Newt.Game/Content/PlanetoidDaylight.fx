#if OPENGL
    #define SV_POSITION POSITION
    #define VS_PROFILE vs_3_0
    #define PS_PROFILE ps_3_0
#else
    #define VS_PROFILE vs_4_0_level_9_1
    #define PS_PROFILE ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 ViewProjection;
float3 SunDirection;
float3 DaylightRamp;
float CloudLighting;

struct VertexInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float3 GlobePosition : TEXCOORD0;
};

PixelInput TransformVertex(VertexInput input)
{
    PixelInput output;
    float4 position = mul(input.Position, World);
    output.Position = mul(position, ViewProjection);
    output.Color = input.Color;
    output.GlobePosition = position.xyz;
    return output;
}

float4 ShadeSurface(PixelInput input) : COLOR0
{
    float sunElevation = dot(normalize(input.GlobePosition), SunDirection);
    float daylight = smoothstep(DaylightRamp.y, DaylightRamp.z, sunElevation);
    float light = lerp(DaylightRamp.x, 1, daylight);
    return float4(input.Color.rgb * lerp(light, light * light, CloudLighting), input.Color.a);
}

technique PlanetoidDaylight
{
    pass P0
    {
        VertexShader = compile VS_PROFILE TransformVertex();
        PixelShader = compile PS_PROFILE ShadeSurface();
    }
}
