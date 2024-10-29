Shader "Custom/ScrollingNoise" 
{
        Properties
        {
            _BaseColor ("Base Color", Color) = (1,1,1,1)
            _MainTex("Texture", 2D) = "white"{}

            _CellSize ("Cell Size", Range(1,100)) = 2.0
            _BorderColor ("Border Color", Color) = (1,1,1,1)
            _BorderThickness ("Border Thickness", Range(0.01, 0.1)) = 0.05
            _TimeScale ("Time Scale", float) = 0.01
            _Smoothness ("Smoothness", float) = 32.0
        }
        SubShader
        {
            Tags { "RenderType"="Opaque" "Queue"="Geometry"}
            LOD 100

            HLSLINCLUDE
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CellSize;
                float4 _BaseColor;
                float4 _BorderColor;
                float _BorderThickness;
                float _TimeScale;
                float _Smoothness;
            CBUFFER_END
                
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            float rand2dTo1d(float2 seed) {
                seed = float2(dot(seed, float2(127.1, 311.7)),
                           dot(seed, float2(269.5, 183.3)));
                return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
            }

            float2 rand2dTo2d(float2 seed)
            {
                seed = float2(dot(seed, float2(127.1, 311.7)),
                              dot(seed, float2(269.5, 183.3)));
                return frac(sin(seed) * 43758.5453); 
            }

            float rand3dTo1d(float3 seed) {
                seed = float3(dot(seed, float3(127.1, 311.7, 74.7)),
                           dot(seed, float3(269.5, 183.3, 246.1)),
                           dot(seed, float3(113.5, 271.9, 54.1)));
                return frac(sin(dot(seed, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            float3 rand3dTo3d(float3 seed) {
                seed = float3(dot(seed, float3(127.1, 311.7, 74.7)),
                           dot(seed, float3(269.5, 183.3, 246.1)),
                           dot(seed, float3(113.5, 271.9, 54.1)));
                return frac(sin(seed) * 43758.5453);
            }

            float3 voronoiNoise2d(float2 value)
            {
                float2 basecell = floor(value);

                float minDistToCell = 10;
                float2 closestCell;
                float2 toClosestCell;

                for(int x=-1; x<=1; x++)
                {
                    for(int y=-1; y<=1; y++)
                    {
                        float2 cell = basecell + float2(x, y);
                        float2 cellPosition = cell + rand2dTo2d(cell);
                        float2 toCell = cellPosition - value;
                        float distToCell = length(toCell);
                        if(distToCell < minDistToCell){
                            minDistToCell = distToCell;
                            closestCell = cell;
                            toClosestCell = toCell;
                        }
                    }
                }

                //Edge Pass
                float minEdgeDistance = 10;
                
                for(int x1=-1; x1<=1; x1++)
                {
                    for(int y1=-1; y1<=1; y1++)
                    {
                        float2 cell = basecell + float2(x1, y1);
                        float2 cellPosition = cell + rand2dTo2d(cell);
                        float2 toCell = cellPosition - value;

                        float2 diffToClosestCell = abs(closestCell - cell);

                        bool isClosestCell = diffToClosestCell.x + diffToClosestCell.y < 0.1;

                        if(!isClosestCell){
                            float2 toCenter = (toClosestCell + toCell) * 0.5;
                            float2 cellDifference = normalize(toCell - toClosestCell);
                            float edgeDistance = dot(toCenter, cellDifference);
                            minEdgeDistance = min(minEdgeDistance, edgeDistance);
                        }
                    }
                }
                
                float random = rand2dTo1d(closestCell);
                return float3(minDistToCell, random, minEdgeDistance);
            }

            float3 voronoiNoise3d(float3 value)
            {
                float3 basecell = floor(value);

                float minDistToCell = 10;
                float3 closestCell;
                float3 toClosestCell;

                for(int x=-1; x<=1; x++)
                {
                    for(int y=-1; y<=1; y++)
                    {
                        for(int z =-1; z<=1; z++)
                        {
                            float3 cell = basecell + float3(x, y, z);
                            float3 cellPosition = cell + rand3dTo3d(cell);
                            float3 toCell = cellPosition - value;
                            float distToCell = length(toCell);
                            if(distToCell < minDistToCell){
                                minDistToCell = distToCell;
                                closestCell = cell;
                                toClosestCell = toCell;
                            }
                        }
                        
                    }
                }

                //Edge Pass
                float minEdgeDistance = 10;
                
                for(int x1=-1; x1<=1; x1++)
                {
                    for(int y1=-1; y1<=1; y1++)
                    {
                        for(int z1=-1; z1<=1; z1++)
                        {
                            float3 cell = basecell + float3(x1, y1, z1);
                            float3 cellPosition = cell + rand3dTo3d(cell);
                            float3 toCell = cellPosition - value;

                            float3 diffToClosestCell = abs(closestCell - cell);

                            bool isClosestCell = diffToClosestCell.x + diffToClosestCell.y + diffToClosestCell.z < 0.1;

                            if(!isClosestCell){
                                float3 toCenter = (toClosestCell + toCell) * 0.5;
                                float3 cellDifference = normalize(toCell - toClosestCell);
                                float edgeDistance = dot(toCenter, cellDifference);
                                minEdgeDistance = min(minEdgeDistance, edgeDistance);
                            }
                        }
                        
                    }
                }
                
                float random = rand3dTo1d(closestCell);
                return float3(minDistToCell, random, minEdgeDistance);
            }
            
            float3 voronoiNoise3d_smooth(float3 value, float smoothness)
            {
                float3 basecell = floor(value);

                float minDistToCell = 10;
                float3 closestCell;

                float res = 0.0;

                for(int x=-1; x<=1; x++)
                {
                    for(int y=-1; y<=1; y++)
                    {
                        for(int z =-1; z<=1; z++)
                        {
                            float3 cell = basecell + float3(x, y, z);
                            float3 cellPosition = cell + rand3dTo3d(cell);
                            float3 toCell = cellPosition - value;
                            float distToCell = length(toCell);

                            res += exp2(-exp2(smoothness) * distToCell);
                            
                            if(distToCell < minDistToCell){
                                minDistToCell = distToCell;
                                closestCell = cell;
                            }
                        }
                    }
                }

                res = -(1.0/exp2(smoothness)) * log2(res);
                
                float random = rand3dTo1d(closestCell);
                return float3(minDistToCell, random, res);
            }

            ENDHLSL

            Pass
            {
                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                VertexOutput vert(VertexInput i)
                {
                    VertexOutput o;
                    o.position = TransformObjectToHClip(i.position.xyz);
                    o.uv = i.uv;
                    o.worldPos = mul(unity_ObjectToWorld, i.position).xyz;
                    return o;
                }

                /* float4 frag(VertexOutput i) : SV_Target
                {
                    float3 value = i.worldPos.xyz * _CellSize;
                    value.y += _Time.y * _TimeScale;
                    float3 noise = voronoiNoise3dnew(value);

                    float valueChange = length(fwidth(value)) * 0.5;
                    float isBorder = 1 - smoothstep(_BorderThickness - valueChange, _BorderThickness + valueChange, noise.z);
                    float4 color = lerp(_BaseColor, _BorderColor, isBorder);
                    return color;
                } */

                float4 frag(VertexOutput i) : SV_Target {
                    float3 value = i.worldPos.xyz * _CellSize;
                    value.y += _Time.y * _TimeScale;
                
                    // Compute Voronoi noise
                    float3 noise = voronoiNoise3d_smooth(value, _Smoothness);

                    float valueChange = length(fwidth(value)) * 0.25;
                    float isBorder = smoothstep(_BorderThickness - valueChange, _BorderThickness + valueChange, noise.x - noise.z);

                    // Apply smooth interpolation to the border color
                    float4 color = lerp(_BaseColor, _BorderColor, isBorder);
                
                    return color;
                }
                

                ENDHLSL
            }
        }
}