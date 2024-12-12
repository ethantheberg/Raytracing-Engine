using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaytracedSphere : MonoBehaviour
{
    [SerializeField] Color albedo;
    [SerializeField] Color emmissive;
    [SerializeField] float emmissiveStrength;
    [SerializeField, Range(0,1)] float smoothness;
    public Sphere GetSphere(){
        Sphere sphere = new Sphere();
        sphere.material.albedo = new Vector3(albedo.r, albedo.g, albedo.b);
        sphere.material.emissive = new Vector3(emmissive.r, emmissive.g, emmissive.b);
        sphere.material.emissiveStrength = emmissiveStrength;
        sphere.material.smoothness = smoothness;
        sphere.position = transform.position;
        sphere.radius = transform.localScale.x / 2;
        return sphere;
    }
}
