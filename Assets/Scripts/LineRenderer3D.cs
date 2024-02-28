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
[BurstCompile] public struct Line3D : IJobParallelFor {
    public int resolution;
    [ReadOnly] public NativeArray<Vector3> positions;
    [ReadOnly] public NativeArray<Vector3> directions;
    [ReadOnly] public NativeArray<float> sines;
    [ReadOnly] public NativeArray<float> cosines;
    //[NativeDisableParallelForRestriction] is unsafe and can cause race conditions,
    //but in this case each job works on n=resolution vertices so it's not an issue
    //look at it like at a 2d array of size Points x resolution
    [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
    [NativeDisableParallelForRestriction] public NativeArray<int> indices;
    public void Execute(int i) {
        Vector3 right = Vector3.Cross(directions[i], Vector3.right).normalized * 1;
        Vector3 up = Vector3.Cross(directions[i], right).normalized * 1;
        for (int j = 0; j < resolution; j++){
            vertices[i * resolution + j] = positions[i];
            vertices[i * resolution + j] += cosines[j] * right;
            vertices[i * resolution + j] += sines[j] * up;
            //if (i == positions.Count() - 1) continue;
            int[] ind = new int[4];
            ind[0] = j + i * resolution;
            ind[1] = (j + 1) % resolution + i * resolution;
            ind[2] = j + resolution + i * resolution;
            ind[3] = (j + 1) % resolution + resolution + i * resolution;
            int offset = i * resolution * 6 + j * 6;
            indices[i * resolution * 6 + j * 6] =     ind[0];
            indices[i * resolution * 6 + j * 6 + 1] = ind[1];
            indices[i * resolution * 6 + j * 6 + 2] = ind[2];
            indices[i * resolution * 6 + j * 6 + 3] = ind[1];
            indices[i * resolution * 6 + j * 6 + 4] = ind[3];
            indices[i * resolution * 6 + j * 6 + 5] = ind[2];
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
    NativeArray<Vector3> positions;
    NativeArray<Vector3> directions;
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
        indices = new NativeArray<int>(points.Count() * resolution * 7 - resolution * 6, Allocator.TempJob);
        positions = new NativeArray<Vector3>(points.Count(), Allocator.TempJob);
        directions = new NativeArray<Vector3>(points.Count(), Allocator.TempJob);
        sines = new NativeArray<float>(resolution, Allocator.TempJob);
        cosines = new NativeArray<float>(resolution, Allocator.TempJob);
        Debug.Log(vertices.Count());
        RecalculatePoints(); //jobify this and above?? 
        for(int i = 0; i < points.Count(); i++){
            positions[i] = points[i].position;
            directions[i] = points[i].direction;
        }
        for(int i = 0; i < resolution; i++){
            sines[i] = Mathf.Sin(i * Mathf.PI * 2 / resolution);
            cosines[i] = Mathf.Cos(i * Mathf.PI * 2 / resolution);
        }
        var job = new Line3D() {
            resolution = resolution,
            indices = indices,
            vertices = vertices,
            positions = positions,
            directions = directions,
            sines = sines,
            cosines = cosines,
        };
        jobHandle = job.Schedule(points.Count(), 16);
        JobHandle.ScheduleBatchedJobs();
    }
    void LateUpdate(){
        jobHandle.Complete();
        //mesh = new Mesh();
        vert = vertices.ToArray();
        ind =  indices.ToArray();
        /*mesh.vertices = vert;
        mesh.triangles = ind;
        meshFilter.sharedMesh = mesh;*/

        //mesh.RecalculateNormals();
        vertices.Dispose();
        indices.Dispose();
        positions.Dispose();
        directions.Dispose();
        sines.Dispose();
        cosines.Dispose();



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
