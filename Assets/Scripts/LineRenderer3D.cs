using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Burst;
[System.Serializable]
public struct Point{
    public Vector3 position;
    [HideInInspector] public Vector3 direction;
    [HideInInspector] public Vector3 normal;
    public float thickness;
    public Point(Vector3 position, Vector3 direction, Vector3 normal, float thickness){
        this.position = position;
        this.direction = direction;
        this.normal = normal;
        this.thickness = thickness;
    }
}
[BurstCompile] public struct Line3D : IJobParallelFor {
    public int resolution;
    public int iterations;
    [ReadOnly] public NativeArray<Point> nodes;
    [ReadOnly] public NativeArray<float> sines;
    [ReadOnly] public NativeArray<float> cosines;
    //[NativeDisableParallelForRestriction] is unsafe and can cause race conditions,
    //but in this case each job works on n=resolution vertices so it's not an issue
    //look at it like at a 2d array of size Points x resolution
    [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
    [NativeDisableParallelForRestriction] public NativeArray<int> indices;
    public void Execute(int i) {
        Vector3 right = Vector3.Cross(nodes[i].direction, Vector3.right).normalized* nodes[i].thickness;
        Vector3 up = Vector3.Cross(nodes[i].direction, right).normalized* nodes[i].thickness;
        Debug.DrawRay(nodes[i].position, right, Color.green);
        Debug.DrawRay(nodes[i].position, up, Color.blue);
        for (int j = 0; j < resolution; j++){
            vertices[i * resolution + j] = nodes[i].position;
            Vector3 vertexOffset = cosines[j] * right + sines[j] * up;
            vertexOffset += vertexOffset * Mathf.Abs(Vector3.Dot(nodes[i].normal.normalized, vertexOffset.normalized)) * (Mathf.Clamp(1/nodes[i].normal.magnitude, 0.5f, 4) - 1);
            vertices[i * resolution + j] += vertexOffset;
            if (i == iterations - 1) continue;
            int offset = i * resolution * 6 + j * 6;
            indices[offset] = j + i * resolution;
            indices[offset + 1] = (j + 1) % resolution + i * resolution;
            indices[offset + 2] = j + resolution + i * resolution;
            indices[offset + 3] = (j + 1) % resolution + i * resolution;
            indices[offset + 4] = (j + 1) % resolution + resolution + i * resolution;
            indices[offset + 5] = j + resolution + i * resolution;
        }
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
    NativeArray<Point> nodes;
    NativeArray<int> indices;
    NativeArray<float> sines;
    NativeArray<float> cosines;
    public Vector3[] vert;
    public int[] ind;
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
        vertices = new NativeArray<Vector3>(points.Count() * resolution, Allocator.TempJob);
        indices = new NativeArray<int>(points.Count() * resolution * 6 - resolution * 6, Allocator.TempJob);

        nodes = new NativeArray<Point>(points.Count(), Allocator.TempJob);
        sines = new NativeArray<float>(resolution, Allocator.TempJob);
        cosines = new NativeArray<float>(resolution, Allocator.TempJob);
        RecalculatePoints(); //jobify this and below?? 
        for(int i = 0; i < points.Count(); i++){
            nodes[i] = points[i];
        }
        for(int i = 0; i < resolution; i++){
            sines[i] = Mathf.Sin(i * Mathf.PI * 2 / resolution);
            cosines[i] = Mathf.Cos(i * Mathf.PI * 2 / resolution);
        }
        var job = new Line3D() {
            resolution = resolution,
            indices = indices,
            vertices = vertices,
            sines = sines,
            nodes = nodes,
            cosines = cosines,
            iterations = points.Count()
        };
        jobHandle = job.Schedule(points.Count(), 16);
        JobHandle.ScheduleBatchedJobs();
    }
    void LateUpdate(){
        jobHandle.Complete();
        mesh = new Mesh();
        vert = vertices.ToArray();
        ind =  indices.ToArray();
        mesh.vertices = vert;
        mesh.triangles = ind;
        meshFilter.sharedMesh = mesh;

        mesh.RecalculateNormals();
        vertices.Dispose();
        indices.Dispose();
        sines.Dispose();
        cosines.Dispose();
        nodes.Dispose();



    }
    public void RecalculatePoints(){
        for(int i = 1; i < points.Count() - 1; i++){
            Vector3 previous = (points[i].position - points[i-1].position).normalized;
            Vector3 next = (points[i+1].position - points[i].position).normalized;
            Vector3 direction = Vector3.Lerp(previous, next, 0.5f).normalized;
            Vector3 normal = (next - previous).normalized * Mathf.Abs(Vector3.Dot(previous, direction)); //length encodes cosine of angle   
            points[i] = new Point(points[i].position, direction, normal, points[i].thickness);
        }
        points[0] = new Point(points[0].position, (points[1].position - points[0].position).normalized, Vector3.zero, points[0].thickness); 
        points[points.Count()-1] = new Point(points[points.Count()-1].position, (points[points.Count-1].position - points[points.Count()-2].position).normalized, Vector3.zero, points[points.Count()-1].thickness); 
    }
}
