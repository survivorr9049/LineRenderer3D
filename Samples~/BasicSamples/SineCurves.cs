using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SineCurves : MonoBehaviour
{
    [SerializeField] LineRenderer3D lineRenderer;
    // Start is called before the first frame update
    void Start()
    {
        lineRenderer.SetPositions(128);
        lineRenderer.resolution = 8;
        lineRenderer.autoUpdate = true;
    }

    // Update is called once per frame
    void Update()
    {
        for (float i = 0; i < 128; i++){
            lineRenderer.SetPoint((int)i, new Vector3(Mathf.Cos(i / 6 + Time.time) * 5, Mathf.Sin(i / 4 +Time.time/2) * 3, Mathf.Sin(i / 5 + Time.time/1.4f) * 7), 0.4f);
        }
    }
}
