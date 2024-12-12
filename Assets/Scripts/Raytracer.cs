using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class Raytracer : MonoBehaviour
{
    [SerializeField] Material rayTracerMaterial;
    [SerializeField] Material AccumulatorMaterial;
    [SerializeField] bool UseOnSceneCamera = true;
    [SerializeField] [Range(1, 100)] int bounceLimit = 10;
    [SerializeField] [Range(1, 100)] int raysPerPixel = 10;
    [SerializeField] bool applyEnvironmentLighting = true;
    [SerializeField] Color skyColorHorizon = new Color(0.5f, 0.5f, 0.5f, 1);
    [SerializeField] Color skyColorZenith = new Color(0.5f, 0.5f, 0.5f, 1);
    [SerializeField] Color groundColor = new Color(0.5f, 0.5f, 0.5f, 1);
    [SerializeField] float sunFocus = 1;
    [SerializeField] float sunIntensity = 1;
    [SerializeField] bool accumulate = true;
    
    private RenderTexture accumulator;
    private ComputeBuffer sphereBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer meshBuffer;
    private int frameNumber = 0;

    private Texture2D debugTexture;

    private List<Color> debugRays;

    private string cameraName;
    void Init(RenderTexture src)
    {
        InitializeAccumulatorTexture(src);
        SetRaytracerSettings();
        if(applyEnvironmentLighting){
            SetEnvironmentSettings();
        }
    }

    public void ResetShader(){
        
    }

    private void SetRaytracerSettings()
    {
        rayTracerMaterial.SetInt("frameNumber", frameNumber);
        rayTracerMaterial.SetMatrix("cameraToWorldMatrix", Camera.current.cameraToWorldMatrix);
        rayTracerMaterial.SetVector("nearClipPlane", GetNearClipPlane(Camera.current));
        rayTracerMaterial.SetInt("bounceLimit", bounceLimit);
        rayTracerMaterial.SetInt("raysPerPixel", raysPerPixel);
    }
    public void UpdateTriangleBuffer(){
        RaytracedMesh[] toBeRaytraced = FindObjectsOfType<RaytracedMesh>();
        List<Triangle> allTriangles = new List<Triangle>();
        List<MeshData> allMeshData = new List<MeshData>();
        int meshIndexCounter = 0;
        foreach(RaytracedMesh mesh in toBeRaytraced){
            List<Triangle> triangles = mesh.GetTriangles();
            allTriangles.AddRange(triangles);
            MeshData meshData = mesh.GetMeshData();
            meshData.startIndex = meshIndexCounter;
            meshData.triangleCount = triangles.Count;
            allMeshData.Add(meshData);
            meshIndexCounter += triangles.Count;
        }

        if(triangleBuffer == null || triangleBuffer.count != 1){
            triangleBuffer?.Release();
            triangleBuffer = new ComputeBuffer(Mathf.Max(1, allTriangles.Count), System.Runtime.InteropServices.Marshal.SizeOf(typeof(Triangle)));
        }
        triangleBuffer.SetData(allTriangles.ToArray());
        rayTracerMaterial.SetBuffer("triangles", triangleBuffer);

        if(meshBuffer == null || meshBuffer.count != 1){
            meshBuffer?.Release();
            meshBuffer = new ComputeBuffer(Mathf.Max(1, allMeshData.Count), System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshData)));
        }
        meshBuffer.SetData(allMeshData.ToArray());
        rayTracerMaterial.SetBuffer("meshes", meshBuffer);
        rayTracerMaterial.SetInt("meshCount", allMeshData.Count);

    }
    public void UpdateSphereBuffer()
    {
        List<Sphere> spheres = new List<Sphere>();
        RaytracedSphere[] toBeRaytraced = FindObjectsOfType<RaytracedSphere>();
        foreach (RaytracedSphere sphere in toBeRaytraced)
        {
            spheres.Add(sphere.GetSphere());
        }
        if(sphereBuffer == null || sphereBuffer.count != spheres.Count){
            sphereBuffer?.Release();
            sphereBuffer = new ComputeBuffer(Mathf.Max(1, spheres.Count), System.Runtime.InteropServices.Marshal.SizeOf(typeof(Sphere)));
        }
        sphereBuffer.SetData(spheres);
        rayTracerMaterial.SetBuffer("spheres", sphereBuffer);
        rayTracerMaterial.SetInt("sphereCount", spheres.Count);
    }
    public void resetFrameCounter(){
        frameNumber = 0;
    }
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (!UseOnSceneCamera && Camera.current.cameraType == CameraType.SceneView)
        {
            Graphics.Blit(src, dest);
            return;
        }

        Init(src);

        /*if (Camera.current.cameraType == CameraType.SceneView)
        {
            //Debug.Log("Scene view");
            Graphics.Blit(null, dest, rayTracerMaterial);
            frameNumber = 0;
            return;
        }*/
        if(Camera.current.transform.hasChanged || cameraName != Camera.current.name){
            Camera.current.transform.hasChanged = false;
            frameNumber = 0;
        }

        if(!accumulate || Camera.current.cameraType == CameraType.SceneView){
            Graphics.Blit(src, dest, rayTracerMaterial);
            return;
        }


        RenderTexture previousFrame = RenderTexture.GetTemporary(src.width, src.height, 0, src.graphicsFormat);
        Graphics.Blit(accumulator, previousFrame);

        RenderTexture currentFrame = RenderTexture.GetTemporary(src.width, src.height, 0, src.graphicsFormat);
        Graphics.Blit(src, currentFrame, rayTracerMaterial);

        //debugTexture = new Texture2D(src.width, src.height, TextureFormat.RGBAFloat, false);
        //RenderTexture.active = currentFrame;
        //debugTexture.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        //Debug.Log("copied texture"); 

        AccumulatorMaterial.SetInt("frameNumber", frameNumber);
        AccumulatorMaterial.SetTexture("currentFrame", currentFrame);
        Graphics.Blit(previousFrame, accumulator, AccumulatorMaterial);

        Graphics.Blit(accumulator, dest);

        RenderTexture.ReleaseTemporary(previousFrame);
        RenderTexture.ReleaseTemporary(currentFrame);
        frameNumber++;
        cameraName = Camera.current.name;
    }

    private void InitializeAccumulatorTexture(RenderTexture src)
    {
        if (accumulator == null || accumulator.width != src.width || accumulator.height != src.height)
        {
            if (accumulator != null)
            {
                accumulator.Release();
            }
            accumulator = new RenderTexture(src.width, src.height, 0, src.graphicsFormat);
        }
    }
    public void SetEnvironmentSettings(){
        rayTracerMaterial.SetInt("applyEnvironmentLighting", applyEnvironmentLighting ? 1 : 0);
        rayTracerMaterial.SetVector("SkyColorHorizon", skyColorHorizon);
        rayTracerMaterial.SetVector("SkyColorZenith", skyColorZenith);
        rayTracerMaterial.SetVector("GroundColor", groundColor);
        rayTracerMaterial.SetFloat("SunFocus", sunFocus);
        rayTracerMaterial.SetFloat("SunIntensity", sunIntensity);
    }
    Vector3 GetNearClipPlane(Camera camera){ // halfwidth, halfheight, distance
        float halfheight = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfwidth = halfheight * camera.aspect;
        return new Vector3(halfwidth, halfheight, camera.nearClipPlane);
    }
    void OnDestroy()
    {
        sphereBuffer?.Release();
        triangleBuffer?.Release();
        meshBuffer?.Release();
        accumulator?.Release();
    }
    void OnDrawGizmos()
    {
       /*Camera camera = GetComponent<Camera>();
        Vector3 nearClipPlane = GetNearClipPlane(camera);
        for (int i = 0; i <= 16; i++)
        {
            for (int j = 0; j <= 9; j++)
            {
                float x = i / 8.0f - 1f;
                float y = j / 4.5f - 1f;
                Vector3 localPosition = new Vector3(x * nearClipPlane.x, y * nearClipPlane.y, nearClipPlane.z);
                Debug.DrawRay(transform.position, transform.localToWorldMatrix.MultiplyVector(localPosition));
            }
        }*/
        /*
        if(debugTexture == null){
            return;
        }
        debugRays = new List<Color>(debugTexture.GetPixels());
        int x = 0;
        int y = 0;
        while(x < debugTexture.width && y < debugTexture.height){
            int i = y * debugTexture.width + x;
            Vector3 debugRay = new Vector3(debugRays[i].r, debugRays[i].g, debugRays[i].b);
            Debug.DrawRay(Camera.main.transform.position, debugRay);
            x+=15;
            if(x >= debugTexture.width){
                x = 0;
                y+=15;
            }
        }*/
    }
}
