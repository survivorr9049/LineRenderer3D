using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
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
    [ReadOnly] public NativeArray<Point> nodes;
    [ReadOnly] public NativeArray<float> sines;
    [ReadOnly] public NativeArray<float> cosines;
    //[NativeDisableParallelForRestriction] is unsafe and can cause race conditions,
    //but in this case each job works on n=resolution vertices so it's not an issue
    //look at it like at a 2d array of size Points x resolution
    //i used this approach because it makes adressing points way easier
    [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
    [NativeDisableParallelForRestriction] public NativeArray<int> indices;
    [NativeDisableParallelForRestriction] public NativeArray<Vector3> normals;
    public void Execute(int i) {
        Vector3 right = nodes[i].right.normalized * nodes[i].thickness;
        Vector3 up = nodes[i].up.normalized * nodes[i].thickness;
        for (int j = 0; j < resolution; j++){
            vertices[i * resolution + j] = nodes[i].position;
            Vector3 vertexOffset = cosines[j] * right + sines[j] * up;
            normals[i * resolution + j] += vertexOffset.normalized;
            vertexOffset += nodes[i].normal.normalized * Vector3.Dot(nodes[i].normal.normalized, vertexOffset) * (Mathf.Clamp(1/nodes[i].normal.magnitude, 0, 2) - 1);
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
[BurstCompile] public struct CalculatePointData : IJobParallelFor{
    [NativeDisableParallelForRestriction] public NativeArray<Point> nodes;
    public void Execute(int i){
        if (i == 0) return;
        Vector3 previous = (nodes[i].position - nodes[i-1].position).normalized;
        Vector3 next = (nodes[i+1].position - nodes[i].position).normalized;
        Vector3 direction = Vector3.Lerp(previous, next, 0.5f).normalized;
        Vector3 normal = (next - previous).normalized * Mathf.Abs(Vector3.Dot(previous, direction)); //length encodes cosine of angle   
        Vector3 right = Vector3.Cross(direction, Vector3.right).normalized;
        if(right.magnitude < 0.05f){
            right = Vector3.Cross(direction, Vector3.forward).normalized;
        }
        Vector3 up = Vector3.Cross(direction, right).normalized;
        nodes[i] = new Point(nodes[i].position, direction, normal, up, right, nodes[i].thickness);
    }
}
[BurstCompile] public struct FixPointsRotation : IJob{
    public NativeArray<Point> nodes;
    public void Execute(){
            for(int i = 0; i < nodes.Length - 1; i++){
            Vector3 fromTo = (nodes[i+1].position - nodes[i].position).normalized;
            Vector3 firstRight = nodes[i].right - Vector3.Dot(nodes[i].right, fromTo) * fromTo;
            Vector3 secondRight = nodes[i+1].right - Vector3.Dot(nodes[i+1].right, fromTo) * fromTo;
            float angle = -Vector3.SignedAngle(firstRight, secondRight, fromTo);
            Quaternion rot = Quaternion.AngleAxis(angle, nodes[i+1].direction);
            nodes[i+1] = new Point(nodes[i+1].position, nodes[i+1].direction, nodes[i+1].normal, rot * nodes[i+1].up, rot * nodes[i+1].right, nodes[i+1].thickness);
        }   
    }
}
public class LineRenderer3D : MonoBehaviour
{
    public bool autoUpdate;
    public int resolution;
    public Material material;
    MeshFilter meshFilter;
    Mesh mesh;
    MeshRenderer meshRenderer;
    [SerializeField] List<Point> points = new List<Point>();
    bool autoComplete;
    //-----------------------------------------------------------------------//
    NativeArray<Vector3> vertices;
    NativeArray<Vector3> normals;
    NativeArray<Point> nodes;
    NativeArray<int> indices;
    NativeArray<float> sines;
    NativeArray<float> cosines;
    JobHandle jobHandle;
    JobHandle pointsJobHandle;
    JobHandle rotationJobHandle;
    void Awake(){
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = new Mesh();
    }
    void Start()
    {
        mesh = new Mesh();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        //points.Clear();
        Vector3 direction = Vector3.forward;
        Vector3 position = Vector3.zero;
        Vector3 lastDirection = Vector3.forward;
        //for(float i = 0; i < 2048; i++){
            /*int random = Random.Range(0, 6);
            if(random == 0){
                direction = Vector3.up;
            }else if (random == 1){
                direction = Vector3.right;
            }else if (random == 2){
                direction = Vector3.forward;
            }else if (random == 3){
                direction = Vector3.left;
            }else if (random == 4){
                direction = Vector3.down;
            }else if (random == 5){
                direction = Vector3.back;
            }
            if (Vector3.Dot(lastDirection, direction) < 0) direction = -direction;
            position += direction;
            points.Add(new Point(position, 0.2f));
            lastDirection = direction;*/
            //points.Add(new Point(new Vector3(Mathf.Cos(i/34 + 1.245f)*56, Mathf.Sin(i/56 + 0.456f)*74, Mathf.Sin(i/51)*62), 5f));
        //}
    }
    void Update()
    {
        if(autoUpdate) BeginGeneration();
    }
    void LateUpdate(){
        if(autoUpdate) CompleteGeneration();
        else if(autoComplete){
            CompleteGeneration();
            autoComplete = false;
        }
    }
    public void BeginGenerationAutoComplete(){
        BeginGeneration();
        autoComplete = true;
    }
    public void BeginGeneration(){
        vertices = new NativeArray<Vector3>(points.Count() * resolution, Allocator.TempJob);
        normals = new NativeArray<Vector3>(points.Count() * resolution, Allocator.TempJob);
        indices = new NativeArray<int>(points.Count() * resolution * 6 - resolution * 6, Allocator.TempJob);
        nodes = new NativeArray<Point>(points.Count(), Allocator.TempJob);
        sines = new NativeArray<float>(resolution, Allocator.TempJob);
        cosines = new NativeArray<float>(resolution, Allocator.TempJob);
        for(int i = 0; i < points.Count(); i++){
            nodes[i] = points[i];
        }

        var pointsJob = new CalculatePointData()
        {
            nodes = nodes
        };
        pointsJobHandle = pointsJob.Schedule(points.Count() - 1, 32);
        for(int i = 0; i < resolution; i++){
            sines[i] = Mathf.Sin(i * Mathf.PI * 2 / resolution);
            cosines[i] = Mathf.Cos(i * Mathf.PI * 2 / resolution);
        }
        pointsJobHandle.Complete();
        CalculateEdgePoints(); 

        var rotationJob = new FixPointsRotation()
        {
            nodes = nodes
        };
        rotationJobHandle = rotationJob.Schedule();
        rotationJobHandle.Complete(); //uses job only to utilize burst system for better performance
        var meshJob = new Line3D() {
            resolution = resolution,
            indices = indices,
            vertices = vertices,
            sines = sines,
            nodes = nodes,
            cosines = cosines,
            normals = normals,
            iterations = points.Count(),
        };
        jobHandle = meshJob.Schedule(points.Count(), 16);
        JobHandle.ScheduleBatchedJobs();
    }
    public void CompleteGeneration(){
        jobHandle.Complete();
        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetNormals(normals);

        vertices.Dispose();
        indices.Dispose();
        sines.Dispose();
        cosines.Dispose();
        nodes.Dispose();
        normals.Dispose();
    }
    void CalculateEdgePoints(){
        Vector3 edgeDirection = (nodes[1].position - nodes[0].position).normalized;
        Vector3 edgeRight = Vector3.Cross(edgeDirection, Vector3.right).normalized;
        Vector3 edgeUp = Vector3.Cross(edgeDirection, edgeRight).normalized;
        nodes[0] = new Point(nodes[0].position, edgeDirection, Vector3.zero, edgeUp, edgeRight, nodes[0].thickness);
        edgeDirection = (nodes[nodes.Length-1].position - nodes[nodes.Length-2].position).normalized;
        edgeRight = Vector3.Cross(edgeDirection, Vector3.right).normalized;
        edgeUp = Vector3.Cross(edgeDirection, edgeRight).normalized;
        nodes[nodes.Count()-1] = new Point(nodes[nodes.Length-1].position, edgeDirection, Vector3.zero, edgeUp, edgeRight, nodes[nodes.Length-1].thickness); 
    }

     ///<summary> initialize renderer with set amount of empty points </summary>
    public void SetPositions(int positionCount){
        points.Clear();
        Point p = new Point(Vector3.zero, 0);
        for(int i = 0; i < positionCount; i++){
            points.Add(p);
        }
    }
    ///<summary> remove point at index </summary>
    public void RemovePoint(int index){
        points.RemoveAt(index);
    }
    ///<summary> add new point </summary>
    public void AddPoint(Vector3 position, float thickness){
        points.Add(new Point(position, thickness));
    }
    ///<summary> change point at index </summary>
    public void SetPoint(int index, Vector3 position, float thickness){
        points[index] = new Point(position, thickness);
    }
    ///<summary> set points to an array of vector3 with uniform thickness </summary>
    public void SetPoints(Vector3[] positions, float thickness){
        points = positions.Select(position => new Point(position, thickness)).ToList();
    }
    ///<summary> set points to an array of vector3 and float (thickness) </summary>
    public void SetPoints(Vector3[] positions, float[] thicknesses){
        points = positions.Zip(thicknesses, (position, thickness) => new Point(position, thickness)).ToList();
    }
}
