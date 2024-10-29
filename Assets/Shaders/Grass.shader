Shader "Custom/Grass"
{
	Properties
	{
		_GrassColorMap("Grass Color Map", 2D) = "white" {}
		_BaseColor("Base Color", Color) = (1, 1, 1, 1)
		_TipColor("Tip Color", Color) = (1, 1, 1, 1)
		
		_BladeWidthMin("Blade Width (Min)", Range(0, 0.1)) = 0.02
		_BladeWidthMax("Blade Width (Max)", Range(0, 0.1)) = 0.05
		_BladeHeightMin("Blade Height (Min)", Range(0, 4)) = 0.1
		_BladeHeightMax("Blade Height (Max)", Range(0, 4)) = 0.2
		
		_BladeBendDistance("Blade Forward Amount", Float) = 0.38
		_BladeBendCurve("Blade Curvature Amount", Range(1, 4)) = 2

		_BendDelta("Bend Variation", Range(0, 1)) = 0.2
		
		_MinGrassDensity("Min Grass Density", Range(0, 10)) = 1
		_MaxGrassDensity("Max Grass Density", Range(0, 10)) = 10
		_CameraFar("Camera Far", Range(0, 100)) = 10

		_GrassMap("Grass Visibility Map", 2D) = "white" {}
		_GrassThreshold("Grass Visibility Threshold", Range(-0.1, 1)) = 0.5
		_GrassFalloff("Grass Visibility Fade-In Falloff", Range(0, 0.5)) = 0.05

		_WindMap("Wind Offset Map", 2D) = "bump" {}
		_WindVelocity("Wind Velocity", Vector) = (1, 0, 0, 0)
		_WindFrequency("Wind Pulse Frequency", Range(0, 1)) = 0.01
		
		_HeightMap("Height Map", 2D) = "black" {}
		_HeightMapMax("Height Map Max", Range(0, 100)) = 25
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"RenderPipeline" = "UniversalPipeline"
			"Cull" = "Off"
		}
		LOD 100
		Cull Off

		HLSLINCLUDE
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT

			#define UNITY_PI 3.14159265359f
			#define UNITY_TWO_PI 6.28318530718f
			#define MAX_BLADE_SEGMENTS 5
			
			CBUFFER_START(UnityPerMaterial)
				float4 _BaseColor;
				float4 _TipColor;
				sampler2D _GrassColorMap;
				float4 _GrassColorMap_ST;
				
				float _BladeWidthMin;
				float _BladeWidthMax;
				float _BladeHeightMin;
				float _BladeHeightMax;

				float _BladeBendDistance;
				float _BladeBendCurve;

				float _BendDelta;

				float _MinGrassDensity;
				float _MaxGrassDensity;
				float _CameraFar;
				
				sampler2D _GrassMap;
				float4 _GrassMap_ST;
				float  _GrassThreshold;
				float  _GrassFalloff;
				
				sampler2D _WindMap;
				float4 _WindMap_ST;
				float4 _WindVelocity;
				float  _WindFrequency;

				float4 _ShadowColor;

				sampler2D _HeightMap;
				float4 _HeightMap_ST;
				float  _HeightMapMax;
			CBUFFER_END

			float _effectorData[41];
		
			struct VertexInput
			{
				float4 vertex  : POSITION;
				float3 normal  : NORMAL;
				float4 tangent : TANGENT;
				float2 uv      : TEXCOORD0;
			};

			struct VertexOutput
			{
				float4 vertex  : SV_POSITION;
				float3 normal  : NORMAL;
				float4 tangent : TANGENT;
				float2 uv      : TEXCOORD0;
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside  : SV_InsideTessFactor;
			};

			struct GeomData
			{
				float4 pos : SV_POSITION;
				float2 uv  : TEXCOORD0;
				float3 world_pos : TEXCOORD1;
			};

			// Following functions from Roystan's code:
			// (https://github.com/IronWarrior/UnityGrassGeometryShader)

			// Simple noise function, sourced from http://answers.unity.com/answers/624136/view.html
			// Extended discussion on this function can be found at the following link:
			// https://forum.unity.com/threads/am-i-over-complicating-this-random-function.454887/#post-2949326
			// Returns a number in the 0...1 range.
			float rand(float3 co)
			{
				return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
			}

			// Construct a rotation matrix that rotates around the provided axis, sourced from:
			// https://gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
			float3x3 angle_axis3_x3(const float angle, const float3 axis)
			{
				float c, s;
				sincos(angle, s, c);

				const float t = 1 - c;
				const float x = axis.x;
				const float y = axis.y;
				const float z = axis.z;

				return float3x3
				(
					t * x * x + c, t * x * y - s * z, t * x * z + s * y,
					t * x * y + s * z, t * y * y + c, t * y * z - s * x,
					t * x * z - s * y, t * y * z + s * x, t * z * z + c
				);
			}

			// Regular vertex shader used by typical shaders.
			VertexOutput vert(VertexInput v)
			{
				VertexOutput o;
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				o.normal = v.normal;
				o.tangent = v.tangent;
				o.uv = TRANSFORM_TEX(v.uv, _GrassMap);
				return o;
			}

			// Vertex shader which just passes data to tessellation stage.
			VertexOutput tessVert(VertexInput v)
			{
				VertexOutput o;
				o.vertex = v.vertex;
				o.normal = v.normal;
				o.tangent = v.tangent;
				o.uv = v.uv;
				return o;
			}

			// Vertex shader which translates from object to world space.
			VertexOutput geomVert (VertexInput v)
            {
				VertexOutput o; 
				o.vertex = float4(v.vertex.xyz, 1.0f);
				o.normal = v.normal;
				o.tangent = v.tangent;
				o.uv = TRANSFORM_TEX(v.uv, _GrassMap);
                return o;
            }

			// This function lets us derive the tessellation factor for an edge
			// from the vertices.
			float tessellationEdgeFactor(VertexInput vert0, VertexInput vert1)
			{
				const float3 edge_center = (vert0.vertex.xyz + vert1.vertex.xyz) * 0.5f;
				const float view_dist = saturate(distance(edge_center, _WorldSpaceCameraPos) / _CameraFar);
				
				const float3 v0 = vert0.vertex.xyz;
				const float3 v1 = vert1.vertex.xyz;
				const float edge_length = distance(v0, v1) * 2;
				
				return edge_length * lerp(_MaxGrassDensity, _MinGrassDensity, view_dist);
			}
		
			TessellationFactors patch_constant_func(const InputPatch<VertexInput, 3> patch)
			{
				TessellationFactors f;

				f.edge[0] = tessellationEdgeFactor(patch[1], patch[2]);
				f.edge[1] = tessellationEdgeFactor(patch[2], patch[0]);
				f.edge[2] = tessellationEdgeFactor(patch[0], patch[1]);
				f.inside = (f.edge[0] + f.edge[1] + f.edge[2]) / 2.0f;

				return f;
			}

			[domain("tri")]
			[outputcontrolpoints(3)]
			[outputtopology("triangle_cw")]
			[partitioning("integer")]
			[patchconstantfunc("patch_constant_func")]
			VertexInput hull(const InputPatch<VertexInput, 3> patch, const uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput domain(TessellationFactors factors, const OutputPatch<VertexInput, 3> patch,const float3 barycentric_coordinates : SV_DomainLocation)
			{
				VertexInput i;

				#define INTERPOLATE(field_name) i.field_name = \
					patch[0].field_name * barycentric_coordinates.x + \
					patch[1].field_name * barycentric_coordinates.y + \
					patch[2].field_name * barycentric_coordinates.z;

				INTERPOLATE(vertex)
				INTERPOLATE(normal)
				INTERPOLATE(tangent)
				INTERPOLATE(uv)

				return tessVert(i);
			}

			GeomData TransformGeomToClip(float3 pos, float3 offset, float3x3 transformationMatrix, float2 uv)
			{
				GeomData o;

				o.pos = TransformObjectToHClip(pos + mul(transformationMatrix, offset));
				o.uv = uv;
				o.world_pos = TransformObjectToWorld(pos + mul(transformationMatrix, offset));

				return o;
			}

			void geom_base(point VertexOutput input[1], inout TriangleStream<GeomData> triStream, const int max_blades)
			{
				const float alpha = tex2Dlod(_GrassMap, float4(input[0].uv, 0, 0)).a;
				if (alpha < _GrassThreshold)
					return;	
				
				float3 pos = input[0].vertex.xyz;
				const float view_dist = saturate(distance(pos, _WorldSpaceCameraPos) / _CameraFar);
				const float blade_segments = lerp(max_blades, 1, view_dist);
				
				const float3 normal = input[0].normal;
				const float4 tangent = input[0].tangent;
				const float3 bitangent = cross(normal, tangent.xyz) * tangent.w;

				const float3x3 tangent_to_local = float3x3
				(
					tangent.x, bitangent.x, normal.x,
					tangent.y, bitangent.y, normal.y,
					tangent.z, bitangent.z, normal.z
				);

				// Rotate around the y-axis a random amount.
				const float3x3 rand_rot_matrix = angle_axis3_x3(rand(pos) * UNITY_TWO_PI, float3(0, 0, 1.0f));
				
				// Rotate around the bottom of the blade a random amount.
				const float3x3 rand_bend_matrix = angle_axis3_x3(rand(pos.zzx) * _BendDelta * UNITY_PI * 0.5f, float3(-1.0f, 0, 0));

				const float2 wind_uv = pos.xz * _WindMap_ST.xy + _WindMap_ST.zw + normalize(_WindVelocity.xzy) * _WindFrequency * _Time.y;
				const float2 wind_sample = (tex2Dlod(_WindMap, float4(wind_uv, 0, 0)).xy * 2 - 1) * length(_WindVelocity);

				const float3 wind_axis = normalize(float3(wind_sample.x, wind_sample.y, 0));
				const float3x3 wind_matrix = angle_axis3_x3(UNITY_PI * wind_sample, wind_axis);

				// Transform the grass blades to the correct tangent space.
				const float3x3 base_transformation_matrix = mul(tangent_to_local, rand_rot_matrix);
				const float3x3 tip_transformation_matrix = mul(mul(mul(tangent_to_local, wind_matrix), rand_bend_matrix), rand_rot_matrix);

				const float falloff = smoothstep(_GrassThreshold, _GrassThreshold + _GrassFalloff, alpha);
				const float width  = lerp(_BladeWidthMin, _BladeWidthMax, rand(pos.xzy) * falloff);
				float height = lerp(_BladeHeightMin, _BladeHeightMax, rand(pos.zyx) * falloff);
				float forward = rand(pos.yyz) * _BladeBendDistance;

				const float height_variation = tex2Dlod(_HeightMap, float4(input[0].uv, 0, 0)).g * _HeightMapMax;
				//pos.x += (rand(pos.yzx) - 0.5f) * 0.25f;
				//pos.z += (rand(pos.zxy) - 0.5f) * 0.25f;
				pos.y += height_variation;
				
				//Effectors
				const float3 world_pos = TransformObjectToWorld(pos);
				for (int j = 0; j < _effectorData[0]; j ++)
            	{
            		int k = j * 4 + 1;
            		const float3 o_pos = float3(_effectorData[k], _effectorData[k + 1], _effectorData[k + 2]);
            		const float radius = _effectorData[k + 3];
					const float dist = distance(world_pos, o_pos);

            		if (dist < radius)
					{
						const float factor = saturate((dist / radius - 0.75f) * 4);
            			height *= factor;
            			forward *= (1 - factor) * 5;
						break;
					}
            	}
				
				// Create blade segments by adding two vertices at once.
				for (int i = 0; i < blade_segments; ++i)
				{
					float t = i / blade_segments;
					float3 offset = float3(width * (1 - t), pow(t, _BladeBendCurve) * forward, height * t);

					const float3x3 transformation_matrix = (i == 0) ? base_transformation_matrix : tip_transformation_matrix;
					triStream.Append(TransformGeomToClip(pos, float3( offset.x, offset.y, offset.z), transformation_matrix, float2(0, t)));
					triStream.Append(TransformGeomToClip(pos, float3(-offset.x, offset.y, offset.z), transformation_matrix, float2(1, t)));
				}

				// Add the final vertex at the tip of the grass blade.
				triStream.Append(TransformGeomToClip(pos, float3(0, forward, height), tip_transformation_matrix, float2(0.5, 1)));

				triStream.RestartStrip();
			}

			[maxvertexcount(MAX_BLADE_SEGMENTS * 2 + 1)]
			void geom(point VertexOutput input[1], inout TriangleStream<GeomData> triStream)
			{
				geom_base(input, triStream, MAX_BLADE_SEGMENTS);
			}

			[maxvertexcount(MAX_BLADE_SEGMENTS * 2 + 1)]
			void geom_single(point VertexOutput input[1], inout TriangleStream<GeomData> triStream)
			{
				geom_base(input, triStream, 1);
			}
		ENDHLSL

		// This pass draws the grass blades generated by the geometry shader.
        Pass
        {
			Name "GrassPass"
			Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
			#pragma require geometry
			#pragma require tessellation tessHW
			#pragma vertex geomVert
			#pragma hull hull
			#pragma domain domain
			#pragma geometry geom
            #pragma fragment frag

			// The lighting sections of the frag shader taken from this helpful post by Ben Golus:
			// https://forum.unity.com/threads/water-shader-graph-transparency-and-shadows-universal-render-pipeline-order.748142/#post-5518747
            float4 frag (GeomData i) : SV_Target
            {
				float4 color = 1.0f;

			//#ifdef _MAIN_LIGHT_SHADOWS
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = i.world_pos;

				float4 shadowCoord = GetShadowCoord(vertexInput);
				half shadowAttenuation = saturate(MainLightRealtimeShadow(shadowCoord) + 0.25f);
				float4 shadowColor = lerp(0.0f, 1.0f, shadowAttenuation);
				color *= shadowColor;
			//#endif
            	
            	float2 texUv = TRANSFORM_TEX(float2(i.world_pos.x, i.world_pos.z), _GrassColorMap);
            	float4 texColor = tex2Dlod(_GrassColorMap, float4(texUv, 0, 0));
            	
                return color * texColor * lerp(_BaseColor, _TipColor, i.uv.y);
			}

			ENDHLSL
		}
		
		//Shadow caster pass
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			HLSLPROGRAM
			#pragma require geometry
			#pragma require tessellation tessHW
			#pragma vertex geomVert
			#pragma hull hull
			#pragma domain domain
			#pragma geometry geom_single
            #pragma fragment frag

			float4 frag (GeomData i) : SV_Target
            {
                return 1;
			}

			ENDHLSL
		}
	}
}