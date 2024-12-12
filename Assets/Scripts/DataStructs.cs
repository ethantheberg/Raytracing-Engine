using UnityEngine;

[System.Serializable]
public struct RaytraceMaterial{
    public Vector3 albedo;
    public Vector3 emissive;  
    public float emissiveStrength;
    public float smoothness;
};

[System.Serializable]
public struct Sphere
{
    public Vector3 position;
    public float radius;
    public RaytraceMaterial material;
}

[System.Serializable]
public struct Triangle{
    public Vector3 v1;
    public Vector3 v2;
    public Vector3 v3;
    public Vector3 boundsMin;
    public Vector3 boundsMax;
}

[System.Serializable]
public struct MeshData{
    public int startIndex;
    public int triangleCount;
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public RaytraceMaterial material;
}
