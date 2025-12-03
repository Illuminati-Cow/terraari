sampler uImage0 : register(s0) {
    Filter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

float uTime; 

static const int COLOR_COUNT = 9;
static const float3 colors[COLOR_COUNT] = {
    float3(255.0/255.0, 139.0/255.0, 139.0/255.0),
    float3(255.0/255.0, 203.0/255.0, 164.0/255.0),
    float3(255.0/255.0, 232.0/255.0, 058.0/255.0),
    float3(168.0/255.0, 255.0/255.0, 157.0/255.0),
    float3(188.0/255.0, 255.0/255.0, 244.0/255.0),
    float3(134.0/255.0, 167.0/255.0, 255.0/255.0),
    float3(135.0/255.0, 121.0/255.0, 255.0/255.0),
    float3(255.0/255.0, 124.0/255.0, 255.0/255.0),
    float3(255.0/255.0, 175.0/255.0, 255.0/255.0)
};

float3 lerp(float3 a, float3 b, float t) {
    return a + (b - a) * t;
}

bool isGreen(float3 c) {
    return c.r < 0.05 && c.g > 0.4 && c.b < 0.05;
}

float4 ArmorBasic(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0 { 

    float4 colorSample = tex2D(uImage0, coords);

    if (!isGreen(colorSample)) {
        return colorSample;
    }

    const float gradientScaleInv = 1.0/3.0;
    const float gradientSpeed = 1.0/4.0;
    const float sectionSizeInv = COLOR_COUNT;

    float x = coords.x*gradientScaleInv + uTime*gradientSpeed;

    float currentSection = fmod(x*sectionSizeInv, COLOR_COUNT);
    float sectionProgress = frac(currentSection);
    int c1 = currentSection;
    int c2 = currentSection+1.0;
    if (c2 == COLOR_COUNT) c2 = 0;
    float3 color = lerp(colors[c1], colors[c2], sectionProgress);
    return float4(color, colorSample.a);
} 
     
technique Technique1 
{ 
    pass ArmorBasic 
    { 
        PixelShader = compile ps_2_0 ArmorBasic(); 
    } 
}
