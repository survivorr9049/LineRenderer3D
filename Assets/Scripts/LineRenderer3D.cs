using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;
[System.Serializable]
public class Point{
    public Vector3 position;
    [HideInInspector] public Vector3 direction;
    public float thickness;
    public Point(Vector3 position, Vector3 direction, float thickness){
        this.position = position;
        this.direction = direction;
        this.thickness = thickness;
    }
}
public struct Line3D : IJobParallelFor {
    public int resolution;
    [ReadOnly] public NativeArray<Vector3> positions;
    [ReadOnly] public NativeArray<Vector3> directions;
    [ReadOnly] public NativeArray<float> sines;
    [ReadOnly] public NativeArray<float> cosines;
    public NativeArray<Vector3> vertices;
    public NativeArray<int> indices;
    public void Execute(int i){
        int circleIndex = i / resolution;
        int localVertexIndex = i % resolution;
        Vector3 right = Vector3.Cross(directions[circleIndex], Vector3.right).normalized * 1;
        Vector3 up = Vector3.Cross(directions[circleIndex], right).normalized * 1;
        vertices[i] = positions[localVertexIndex];
        vertices[i] += cosines[localVertexIndex] * right;
        vertices[i] += sines[localVertexIndex] * up;
    }
}
public class LineRenderer3D : MonoBehaviour
{
    [SerializeField] List<Point> points = new List<Point>();
    [SerializeField] int resolution;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] Mesh mesh;
    [SerializeField] MeshRenderer meshRenderer;
    public Material material;
    //Vector3[] vertices;
    //-----------------------------------------------------------------------//
    NativeArray<Vector3> vertices;
    NativeArray<Vector3> positions;
    NativeArray<Vector3> directions;
    NativeArray<int> indices;
    NativeArray<float> sines;
    NativeArray<float> cosines;
    JobHandle jobHandle;
    void Awake(){
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = new Mesh();
    }
    void Start()
    {
        meshRenderer.sharedMaterial = material;
    }

    void Update()
    {
        vertices = new NativeArray<Vector3>(points.Count() * resolution, Allocator.Persistent);
        indices = new NativeArray<int>(points.Count() * resolution * 6, Allocator.Persistent);
        positions = new NativeArray<Vector3>(points.Count(), Allocator.Persistent);
        directions = new NativeArray<Vector3>(points.Count(), Allocator.Persistent);
        sines = new NativeArray<float>(resolution, Allocator.Persistent);
        cosines = new NativeArray<float>(resolution, Allocator.Persistent);
        for(int i = 0; i < points.Count(); i++){
            positions[i] = points[i].position;
        }
        for(int i = 0; i < resolution; i++){
            sines[i] = Mathf.Sin(i * Mathf.PI * 2 / resolution);
            cosines[i] = Mathf.Cos(i * Mathf.PI * 2 / resolution);
        }
        RecalculatePoints(); //jobify this and above?? 
        positions[0] = Vector3.up;
        var job = new Line3D() {
            resolution = resolution,
            indices = indices,
            vertices = vertices,
            positions = positions,
            directions = directions,
            sines = sines,
            cosines = cosines,
        };
        jobHandle = job.Schedule(vertices.Length, resolution);
        JobHandle.ScheduleBatchedJobs();
    }
    void LateUpdate(){
        jobHandle.Complete();
        List<Vector3> pts = vertices.ToList<Vector3>();
        foreach(Vector3 point in pts){
            Debug.DrawRay(point, Vector3.up * 0.5f, Color.blue);
            Debug.Log(point);
        }
    }
    public void RecalculatePoints(){
        for(int i = 1; i < points.Count() - 1; i++){
            Vector3 direction = Vector3.Lerp((points[i].position - points[i-1].position).normalized, (points[i+1].position - points[i].position).normalized, 0.5f).normalized;
            points[i].direction = direction;
        }
        points[0].direction = (points[1].position - points[0].position).normalized;
        points[points.Count()-1].direction = (points[points.Count-1].position - points[points.Count()-2].position).normalized;
    }
}
