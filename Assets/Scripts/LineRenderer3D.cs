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
    [HideInInspector] public Vector3 up;
    [HideInInspector] public Vector3 right;
    public float thickness;
    public Point(Vector3 position, Vector3 direction, Vector3 normal, Vector3 up, Vector3 right, float thickness){
        this.position = position;
        this.direction = direction;
        this.normal = normal;
        this.thickness = thickness;
        this.up = up;
        this.right = right;
    }
    public Point(Vector3 position, float thickness){
        this.position = position;
        this.direction = Vector3.zero;
        this.normal = Vector3.zero;
        this.thickness = thickness;
        this.up = Vector3.zero;
        this.right = Vector3.zero;
    }
}
[BurstCompile] public struct Line3D : IJobParallelFor {
    public int resolution;
    public int iterations;
    public bool uniformScale;
    [ReadOnly] public NativeArray<Point> nodes;
    [ReadOnly] public NativeArray<float> sines;
    [ReadOnly] public NativeArray<float> cosines;
    //[NativeDisableParallelForRestriction] is unsafe and can cause race conditions,
    //but in this case each job works on n=resolution vertices so it's not an issue
    //look at it like at a 2d array of size Points x resolution
    [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
    [NativeDisableParallelForRestriction] public NativeArray<int> indices;
    public void Execute(int i) {
        Vector3 right = nodes[i].right.normalized * nodes[i].thickness;
        Vector3 up = nodes[i].up.normalized * nodes[i].thickness;
        
        for (int j = 0; j < resolution; j++){
            vertices[i * resolution + j] = nodes[i].position;
            Vector3 vertexOffset = cosines[j] * right + sines[j] * up;
            if(uniformScale) vertexOffset += vertexOffset * Mathf.Abs(Vector3.Dot(nodes[i].normal.normalized, vertexOffset.normalized)) * (Mathf.Clamp(1/nodes[i].normal.magnitude, 0.5f, 2) - 1);
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
    public bool fixTwisting;
    public bool uniformScale;
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
    public float rotation;
    void Awake(){
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = new Mesh();
    }
    void Start()
    {
        meshRenderer.sharedMaterial = material;
        points.Clear();
        Vector3 position = Vector3.zero;
        for(int i = 0; i < 32; i++){
            position += new Vector3(Random.Range(-15, 15), Random.Range(-15, 15), Random.Range(-15, 15)) * 0.1f;
            points.Add(new Point(position, 0.3f));
        }
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
            iterations = points.Count(),
            uniformScale = uniformScale,
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
            Vector3 right = Vector3.Cross(direction, Vector3.right).normalized;
            Vector3 up = Vector3.Cross(direction, right).normalized;
            points[i] = new Point(points[i].position, direction, normal, up, right, points[i].thickness);

        }
        Vector3 edgeDirection = (points[1].position - points[0].position).normalized;
        Vector3 edgeRight = Vector3.Cross(edgeDirection, Vector3.right).normalized;
        Vector3 edgeUp = Vector3.Cross(edgeDirection, edgeRight).normalized;
        points[0] = new Point(points[0].position, edgeDirection, Vector3.zero, edgeUp, edgeRight, points[0].thickness);
        edgeDirection = (points[points.Count - 1].position - points[points.Count() - 2].position).normalized;
        edgeRight = Vector3.Cross(edgeDirection, Vector3.right).normalized;
        edgeUp = Vector3.Cross(edgeDirection, edgeRight).normalized;
        points[points.Count()-1] = new Point(points[points.Count()-1].position, edgeDirection, Vector3.zero, edgeUp, edgeRight, points[points.Count()-1].thickness); 
    
        for(int i = 0; i < points.Count(); i++){

            if (i == points.Count() - 1) continue;
            Vector3 fromTo = (points[i + 1].position - points[i].position).normalized;
            Vector3 firstRight = points[i].right - Vector3.Dot(points[i].right, fromTo) * fromTo;
            Vector3 secondRight = points[i+1].right - Vector3.Dot(points[i+1].right, fromTo) * fromTo;
            Vector3 firstUp = points[i].up - Vector3.Dot(points[i].up, fromTo) * fromTo;
            Vector3 secondUp = points[i+1].up - Vector3.Dot(points[i+1].up, fromTo) * fromTo;
            float angleRight = -Mathf.Acos(Vector3.Dot(firstRight, secondRight));
            float angleUp = -Mathf.Acos(Vector3.Dot(firstUp, secondUp));
            float angle = Mathf.Lerp(angleRight, angleUp, 0.5f);
            Quaternion rot = Quaternion.AngleAxis(angleRight * Mathf.Rad2Deg + rotation, points[i + 1].direction);
            if(fixTwisting) points[i+1] = new Point(points[i+1].position, points[i+1].direction, points[i+1].normal, rot * points[i+1].up, rot * points[i+1].right, points[i+1].thickness);
            Debug.DrawRay(points[i+1].position, points[i].up, Color.cyan);
            Debug.DrawRay(points[i+1].position, points[i].right, Color.magenta);
            Debug.DrawRay(points[i + 1].position, points[i + 1].direction, Color.green);
            Debug.DrawRay(points[i + 1].position + transform.position, points[i + 1].normal, Color.black);
        }   



    }
}
