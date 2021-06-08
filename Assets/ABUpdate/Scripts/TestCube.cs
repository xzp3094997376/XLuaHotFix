using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCube : MonoBehaviour
{
    Bounds bounds;

    void Start()
    {
        Vector3 center = Vector3.zero;
        Renderer[] renders = transform.GetComponentsInChildren<Renderer>();
        foreach (Renderer child in renders)
        {
            center += child.bounds.center;
        }
        center /= transform.GetComponentsInChildren<Transform>().Length;
        bounds = new Bounds(center, Vector3.zero);
        foreach (Renderer child in renders)
        {
            bounds.Encapsulate(child.bounds);
        }
    }
    // Update is called once per frame
    void Update()
    {
        //transform.Rotate(Vector3.forward, Space.World);
        transform.RotateAround(bounds.center, Vector3.forward, 1f);
    }
}
