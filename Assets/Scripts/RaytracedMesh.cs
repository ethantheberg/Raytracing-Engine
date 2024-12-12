using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaytracedMesh : MonoBehaviour
{
    [SerializeField] Color albedo;
    [SerializeField] Color emmissive;
    [SerializeField] float emmissiveStrength;
    [SerializeField, Range(0,1)] float smoothness;

    public RaytraceMaterial material{
        get{
            return new RaytraceMaterial{
                albedo = new Vector3(albedo.r, albedo.g, albedo.b),
                emissive = new Vector3(emmissive.r, emmissive.g, emmissive.b),
                emissiveStrength = emmissiveStrength,
                smoothness = smoothness
            };
        }
    }

    private Bounds adjustBounds(Bounds bounds){
        Vector3[] corners = new Vector3[]{
            new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
        };
        return GeometryUtility.CalculateBounds(corners, transform.localToWorldMatrix);
    }

    public MeshData GetMeshData(){
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        Bounds adjustedBounds = adjustBounds(mesh.bounds);
        return new MeshData{
            startIndex = 0,
            triangleCount = 0,
            material = material,
            boundsMin = adjustedBounds.min,
            boundsMax = adjustedBounds.max
        };
    }

    public List<Triangle> GetTriangles(){
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        List<Triangle> tris = new List<Triangle>();
        for(int i = 0; i < triangles.Length; i+=3){
            Vector3 v1 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 v2 = transform.TransformPoint(vertices[triangles[i+1]]);
            Vector3 v3 = transform.TransformPoint(vertices[triangles[i+2]]);
            Bounds bounds = new Bounds();
            bounds.Encapsulate(v1);
            bounds.Encapsulate(v2);
            bounds.Encapsulate(v3);
            tris.Add(new Triangle{
                v1 = v1,
                v2 = v2,
                v3 = v3,
                boundsMin = bounds.min,
                boundsMax = bounds.max

            });
        }
        return tris;
    }
}
