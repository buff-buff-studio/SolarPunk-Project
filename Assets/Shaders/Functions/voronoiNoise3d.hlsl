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

void voronoiNoise3d_float(float3 value, float smoothness, out float2 Out)
{
    float3 basecell = floor(value);

    float minDistToCell = 10;

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
                }
            }
        }
    }

    res = -(1.0/exp2(smoothness)) * log2(res);
    Out = float2(minDistToCell, res);
}


/* void voronoiNoise3d_float(float3 value, out float3 Out)
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
    Out = float3(minDistToCell, random, minEdgeDistance);
} */
  