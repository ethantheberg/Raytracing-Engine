Shader "Custom/rayTraceShader"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
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

            
            static const float infinity = 1./0.;
            static const float PI = 3.14159265;
            static const int distanceLimit = 100;
            static const float3 SkyColor = float3(0.5, 0.5, 1.0);
            
            int bounceLimit;
            int raysPerPixel;
            bool applyLambert;

            float frameNumber;
            float3 nearClipPlane;
            float4x4 cameraToWorldMatrix;

            bool applyEnvironmentLighting;
			float4 GroundColor;
			float4 SkyColorHorizon;
			float4 SkyColorZenith;
			float SunFocus;
			float SunIntensity;

            int triangleCheckCount = 0;
            int boxCheckCount = 0;
            
            // https://www.pcg-random.org/
            uint RandomInt(inout uint state)
            {
                state = state * 747796405u + 2891336453u;
                uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
                return (word >> 22u) ^ word;
            }

            float RandomFloat01(inout uint state){
                return RandomInt(state)/4294967296.0;
            }

            float3 RandomNormal(inout uint state){
                float z = RandomFloat01(state) * 2 - 1;
                float a = RandomFloat01(state) * 2 * PI;
                float r = sqrt(1 - z * z);
                float x = r * cos(a);
                float y = r * sin(a);
                return float3(x, y, z);
            }

            float3 RandomNormalHemisphere(inout uint state, float3 hemisphereNormal){
                float3 normal = RandomNormal(state);
                if(dot(normal, hemisphereNormal) < 0){
                    return -normal;
                }
                return normal;
            }

            struct Ray
            {
                float3 origin;
                float3 direction;
            };

            struct RaytraceMaterial
            {
                float3 albedo;
                float3 emissive;
                float emissiveStrength;
                float smoothness;
            };

            struct Sphere
            {
                float3 center;
                float radius;
                RaytraceMaterial material;
            };

            struct Triangle{
                float3 v1;
                float3 v2;
                float3 v3;
                float3 boundsMin;
                float3 boundsMax;
            };

            struct MeshData{
                int startIndex;
                int triangleCount;
                float3 boundsMin;
                float3 boundsMax;
                
                RaytraceMaterial material;

            };

            struct HitData{
                bool didHit;
                float distance;
                float3 location;
                float3 normal;
                RaytraceMaterial material;
            };

            StructuredBuffer<Sphere> spheres;
            int sphereCount;

            StructuredBuffer<Triangle> triangles;
            StructuredBuffer<MeshData> meshes;
            int meshCount;

            //https://raytracing.github.io/books/RayTracingInOneWeekend.html
            HitData raySphereIntersection(Ray ray, Sphere sphere)
            {
                float3 offsetRayOrigin = ray.origin - sphere.center;

                //the coefficients of a quadratic whose solution is how far along the ray the intersection is
                //a = 1 as long as ray direction vector is normalised
                float halfb = dot(offsetRayOrigin, ray.direction);
                float c = dot(offsetRayOrigin, offsetRayOrigin) - sphere.radius * sphere.radius;
                float discriminant = halfb * halfb - c;

                HitData hitData;
                hitData.didHit = discriminant > 0;
                if(!hitData.didHit){
                    return hitData;
                }
                float distance = -halfb-sqrt(discriminant);
                if(distance <= 0){
                    hitData.didHit = false;
                    return hitData;
                }
                hitData.distance = distance;
                hitData.location = ray.origin + hitData.distance * ray.direction;
                hitData.normal = normalize(hitData.location - sphere.center);
                hitData.material = sphere.material;

                return hitData;
            }

            bool rayBoundingBoxIntersection(Ray ray, float3 boxMin, float3 boxMax)
			{
                boxCheckCount++;
				float3 invDir = 1 / ray.direction;
				float3 tMin = (boxMin - ray.origin) * invDir;
				float3 tMax = (boxMax - ray.origin) * invDir;
				float3 t1 = min(tMin, tMax);
				float3 t2 = max(tMin, tMax);
				float tNear = max(max(t1.x, t1.y), t1.z);
				float tFar = min(min(t2.x, t2.y), t2.z);
				return tNear <= tFar;
			};

            HitData rayTriangleIntersection(Ray ray, Triangle tri){
                if(!rayBoundingBoxIntersection(ray, tri.boundsMin, tri.boundsMax)){
                    HitData hitData;
                    hitData.didHit = false;
                    return hitData;
                }
                triangleCheckCount++;
                float3 edge1 = tri.v2 - tri.v1;
                float3 edge2 = tri.v3 - tri.v1;
                float3 triangleNormal = cross(edge1, edge2);
                
                float determinant = -dot(ray.direction, triangleNormal);
                float inverseDeterminant = 1.0/determinant;
                
                float3 AO = ray.origin - tri.v1;
                float3 DAO = cross(AO, ray.direction);
                
                float u = dot(edge2, DAO) * inverseDeterminant;
                float v = -dot(edge1, DAO) * inverseDeterminant;
                float w = 1 - u - v;

                float distance = dot(AO, triangleNormal) * inverseDeterminant;
                
                HitData hitData;
                hitData.didHit = (determinant >= 1E-6 && distance >= 0.0 && u >= 0.0 && v >= 0.0 && w >= 0.0);
                hitData.distance = distance;
                hitData.location = ray.origin + distance * ray.direction;
                hitData.normal = normalize(triangleNormal);
                return hitData;
            }

            HitData rayMeshIntersection(Ray ray, MeshData mesh){
                HitData closestHit;
                if(!rayBoundingBoxIntersection(ray, mesh.boundsMin, mesh.boundsMax)){
                    closestHit.didHit = false;
                    return closestHit;
                }
                closestHit.distance = infinity;
                HitData hitData;
                for(int i = mesh.startIndex; i < mesh.startIndex + mesh.triangleCount; ++i){
                    hitData = rayTriangleIntersection(ray, triangles[i]);
                    if(hitData.didHit && hitData.distance < closestHit.distance){
                        closestHit = hitData;
                    }
                }
                closestHit.material = mesh.material;
                return closestHit;
            }
            
            Ray constructRay(float2 uv){
                Ray ray;
                ray.origin = _WorldSpaceCameraPos;
                float3 localEndPoint = float3((uv.x*2 - 1), (uv.y*2 - 1), -1)*nearClipPlane;
                ray.direction = normalize(mul(cameraToWorldMatrix, localEndPoint));
                return ray;
            }

            //https://github.com/SebLague/Ray-Tracing/
            float3 getSkyColor(Ray ray){
                float skyGradientT = pow(smoothstep(0, 0.4, ray.direction.y), 0.35);
                float groundToSkyT = smoothstep(-0.01, 0, ray.direction.y);
                float3 skyGradient = lerp(SkyColorHorizon, SkyColorZenith, skyGradientT);
                float sun = pow(max(0, dot(ray.direction, _WorldSpaceLightPos0.xyz)), SunFocus) * SunIntensity;
                // Combine ground, sky, and sun
                float3 composite = lerp(GroundColor, skyGradient, groundToSkyT) + sun * (groundToSkyT>=1);
                return composite;
            }

            HitData cast(Ray ray){
                HitData closestHit;
                closestHit.distance = infinity;
                HitData hitData;
                for(int i = 0; i < sphereCount; ++i){
                    hitData = raySphereIntersection(ray, spheres[i]);
                    if(hitData.didHit && hitData.distance < closestHit.distance){
                        closestHit = hitData;
                    }
                }

                for(int i = 0; i < meshCount; ++i){
                    hitData = rayMeshIntersection(ray, meshes[i]);
                    if(hitData.didHit && hitData.distance < closestHit.distance){
                        closestHit = hitData;
                    }
                }

                return closestHit;
            }

            float3 trace(Ray ray, inout uint rngState){
                float3 cumulativeTint = 1;
                float3 rayColor = 0;
                for(int nBounces = 0; nBounces <= bounceLimit; ++nBounces){
                    HitData hitData = cast(ray);
                    if(hitData.distance > distanceLimit){
                        if(applyEnvironmentLighting){
                            rayColor += getSkyColor(ray)*cumulativeTint; 
                        }
                        break;
                    }
                    ray.origin = hitData.location + hitData.normal*0.001;
                    float3 specularDirection = reflect(ray.direction, hitData.normal);
                    float3 diffuseDirection = normalize(hitData.normal + RandomNormal(rngState));
                    ray.direction = lerp(diffuseDirection, specularDirection, hitData.material.smoothness);
                    
                    rayColor += hitData.material.emissive * hitData.material.emissiveStrength * cumulativeTint;

                    cumulativeTint *= hitData.material.albedo;

                    float p = max(cumulativeTint.r, max(cumulativeTint.g, cumulativeTint.b));
                    if (RandomFloat01(rngState) >= p) {
                        break;
                    }
                    cumulativeTint *= 1.0 / p;
                }
                return rayColor;
            }


            float4 frag (v2f i) : SV_Target
            {
                uint2 numPixels = _ScreenParams;
                uint2 pixelCoord = i.uv*numPixels;
                uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
                uint rngState = uint(uint(pixelCoord.x) * uint(1973) + uint(pixelCoord.y) * uint(9277) + uint(frameNumber) * uint(34576)) | uint(1);
                

                triangleCheckCount = 0;
                boxCheckCount = 0;

                Ray ray = constructRay(i.uv);
                float3 totalLight = 0;
                for(int i = 0; i < raysPerPixel; ++i){
                    totalLight += trace(ray, rngState);
                };
                totalLight = totalLight/raysPerPixel;
                //return triangleCheckCount/500;
                return float4(totalLight, 1);
            }
            ENDCG
        }
    }
}
                