#ifndef WATER_DIRTY
#define WATER_DIRTY

float2 _waterFlowDirection;
float _waterDirtyRegion[41];
float _waterFlowingRegion[61];
float _waterPipeExit[41];

void water_PipeExit_float(float3 position, out float2 Position, out float Strength)
{
    float2 pos = position.xz;
    
    float value = 0;
    float2 center = float2(0, 0);

    for (int j = 0; j < _waterPipeExit[0]; j ++)
    {
        int idx = j * 3 + 1;
        float pX = _waterPipeExit[idx];
        float pY = _waterPipeExit[idx + 1];
        float radius = _waterPipeExit[idx + 2];
        float strength = _waterPipeExit[idx + 3];
        float distanceFromCenter = distance(pos, float2(pX, pY));
       
        if (distanceFromCenter < radius)
        {
            center = pos - float2(pX, pY);
            float ramp = 1.f - saturate(distanceFromCenter / radius);
            value = sqrt(sqrt(ramp * strength));
        }
    }

    Position = center;
    Strength = value;
}

void water_GetDirtyRegion_float(float3 position, out float Out)
{
    float2 pos = position.xz;

    float value = 0;

    for (int j = 0; j < _waterDirtyRegion[0]; j ++)
    {
        int idx = j * 4 + 1;
        float pX = _waterDirtyRegion[idx];
        float pY = _waterDirtyRegion[idx + 1];
        float radius = _waterDirtyRegion[idx + 2];
        float transitionRadius = _waterDirtyRegion[idx + 3];
        float distanceFromCenter = distance(pos, float2(pX, pY));

        if (distanceFromCenter < radius)
        {
            value = max(value, 1);
            break;
        }
        
        if (distanceFromCenter < radius + transitionRadius)
        {
            value = max(value, 1 - (distanceFromCenter - radius) / transitionRadius);
        }
    }
    
    Out = value;
}

void water_GetMainData_float(float3 position, float waveStrength, float waveSpeed, out float2 FinalResult, out float Displacement)
{
    float2 pos = position.xz;
    float2 direction =_waterFlowDirection;

    for (int j = 0; j < _waterFlowingRegion[0]; j ++)
    {
        int idx = j * 6 + 1;
        float pX = _waterFlowingRegion[idx];
        float pY = _waterFlowingRegion[idx + 1];
        float radius = _waterFlowingRegion[idx + 2];
        float transitionRadius = _waterFlowingRegion[idx + 3];
        float distanceFromCenter = distance(pos, float2(pX, pY));
        float2 dir = float2(_waterFlowingRegion[idx + 4], _waterFlowingRegion[idx + 5]);
        
        if (distanceFromCenter < radius)
        {
            direction += dir * _Time.y;
        }
        else if (distanceFromCenter < radius + transitionRadius)
        {
            direction += dir * _Time.y * 0.5f;
        } 
    }
    
    //float dirty = 0;
    //water_GetDirtyRegion_float(position, dirty);
    //direction = lerp(direction, -direction, saturate((dirty - 0.5f) * 2.f)); 

    float fac = length(direction)/100;
    float wave = waveStrength * fac * sin((position.x + position.z + _Time.y % 16) / 3.0f);
    
    //Pipes
    float2 pipeCenter;
    float pipeStrength;
    water_PipeExit_float(position, pipeCenter, pipeStrength);
    direction = lerp(direction, float2(1,1) * _Time.x, pipeStrength);
    wave = lerp(wave, -0.5f, pipeStrength);
   
    FinalResult = waveSpeed * direction;
    Displacement = wave;
}

#endif