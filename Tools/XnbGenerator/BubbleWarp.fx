sampler uImage0 : register(s0); 
sampler uImage1 : register(s1); 
float uTime; 

static const int COLOR_COUNT = 4;
static const float3 colors[COLOR_COUNT] = {
    float3(254.0/255.0, 157.0/255.0, 253.0/255.0),
    float3(249.0/255.0, 241.0/255.0, 234.0/255.0),
    float3(195.0/255.0, 253.0/255.0, 254.0/255.0),
    float3(205.0/255.0, 197.0/255.0, 254.0/255.0)
};

float3 lerp(float3 a, float3 b, float t) {
    return a + (b - a) * t;
}

float4 ArmorBasic(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{ 
    float4 texColor = tex2D(uImage0, coords);
    float2 centered = (coords - 0.5) * 2.0;
    float dist = length(centered);
    
    // Use x coordinate for horizontal gradient (change to uv.y for vertical)
    float t = coords.x;
    
    // Calculate which color segment we're in
    float segmentSize = 1.0 / float(COLOR_COUNT);
    int segmentIndex = int(floor(t / segmentSize));
    
    // Clamp to valid range
    segmentIndex = clamp(segmentIndex, 0, COLOR_COUNT - 1);
    
    // Calculate local t within this segment (0 to 1)
    float localT = (t - float(segmentIndex) * segmentSize) / segmentSize;
    
    // Interpolate between the two colors
    float3 color = lerp(colors[segmentIndex%COLOR_COUNT], colors[(segmentIndex + 1)%COLOR_COUNT], localT);
    
    return float4(color, 1.0);

    // if (dist > 1.0)
    //     return float4(0, 0, 0, 0);
    
    // if (dist > 0.8)
    //     return float4(1, 0, 0, 1);

    // return tex2D(uImage1, float2(0.9, 0.9));
} 
     
technique Technique1 
{ 
    pass ArmorBasic 
    { 
        PixelShader = compile ps_2_0 ArmorBasic(); 
    } 
}