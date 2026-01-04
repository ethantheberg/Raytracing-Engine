using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

[CustomEditor(typeof(Raytracer))]
public class RaytracerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        Raytracer raytracer = (Raytracer)target;
        if(GUILayout.Button("Reset Frame Counter")){
            raytracer.ResetFrameCounter();
        }
        if(GUILayout.Button("Update Objects")){
            raytracer.SetEnvironmentSettings();
            raytracer.UpdateTriangleBuffer();
            raytracer.UpdateSphereBuffer();
        }
    }
}