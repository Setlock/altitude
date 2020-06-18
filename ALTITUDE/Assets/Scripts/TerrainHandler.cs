using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Linq;
using Unity.Jobs;
using Unity.Collections;

public class TerrainHandler : MonoBehaviour
{
    public Vector3 chunkDimensions;
    public float pointSpacing;
    public int3 viewDist;
    public float surfaceLevel, featureSize;
    public Transform viewer;
    public Material terrainMaterial;

    public Queue<GameObject> chunkObjects = new Queue<GameObject>();

    public NativeHashMap<float3, JobHandle> valueJobs;
    public NativeHashMap<float3, JobHandle> meshJobs;
    void Start()
    {
        int numJobs = 2*((viewDist.x * viewDist.y * viewDist.z)+1);

        valueJobs = new NativeHashMap<float3, JobHandle>(numJobs,Allocator.Persistent);
        meshJobs = new NativeHashMap<float3, JobHandle>(numJobs,Allocator.Persistent);

    MarchingCubes.CreateTriangulation();
        CreateChunkPool();
    }
    void CreateChunkPool()
    {
        int numChunks = ((viewDist.x * viewDist.y * viewDist.z)+1)*2;
        for(int i = 0; i < numChunks; i++)
        {
            GameObject chunkObject = new GameObject("Terrain Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            chunkObject.transform.SetParent(gameObject.transform, true);
            chunkObject.layer = LayerMask.NameToLayer("Ground");
            chunkObjects.Enqueue(chunkObject);
        }
    }
    Dictionary<Vector3, TerrainChunk> chunks = new Dictionary<Vector3, TerrainChunk>();
    void Update()
    {
        foreach(TerrainChunk tc in chunks.Values)
        {
            tc.SetRemove(true);
        }
        int currentX = Mathf.RoundToInt(viewer.position.x / chunkDimensions.x);
        int currentY = Mathf.RoundToInt(viewer.position.y / chunkDimensions.y);
        int currentZ = Mathf.RoundToInt(viewer.position.z / chunkDimensions.z);

        for(int x = -viewDist.x; x < viewDist.x; x++)
        {
            for (int y = -viewDist.y; y < viewDist.y; y++)
            {
                for (int z = -viewDist.z; z < viewDist.z; z++)
                {
                    Vector3 chunkCoord = new Vector3((currentX + x), (currentY + y), (currentZ+z));
                    if (chunks.ContainsKey(chunkCoord))
                    {
                        chunks[chunkCoord].SetRemove(false);
                    }
                    else
                    {
                        Vector3 worldCoord = new Vector3(chunkCoord.x * chunkDimensions.x, chunkCoord.y * chunkDimensions.y, chunkCoord.z * chunkDimensions.z);
                        TerrainChunk tc;
                        if (chunkObjects.Count > 0)
                        {
                            tc = new TerrainChunk(chunkObjects.Dequeue(), this, worldCoord, chunkDimensions, pointSpacing, featureSize, surfaceLevel, terrainMaterial);
                            chunks.Add(chunkCoord, tc);
                        }
                        else
                        {
                            GameObject newChunkObject = new GameObject("Terrain Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                            newChunkObject.transform.SetParent(gameObject.transform, true);
                            newChunkObject.layer = LayerMask.NameToLayer("Ground");

                            tc = new TerrainChunk(newChunkObject, this, worldCoord, chunkDimensions, pointSpacing, featureSize, surfaceLevel, terrainMaterial);
                            chunks.Add(chunkCoord, tc);
                        }
                        valueJobs.Add(chunkCoord, tc.ScheduleValueJob());
                    }
                }
            }
        }

        UpdateJobs();

        Vector3[] keys = chunks.Keys.ToArray();
        foreach(Vector3 key in keys)
        {
            if (chunks[key].ShouldRemove())
            {
                chunkObjects.Enqueue(chunks[key].Remove());
                chunks.Remove(key);
            }
        }
    }
    public void UpdateJobs()
    {
        if(valueJobs.Count() > 0)
        {
            NativeArray<float3> keys = valueJobs.GetKeyArray(Allocator.Temp);
            foreach(float3 key in keys)
            {
                if (valueJobs[key].IsCompleted || chunks[key].ShouldRemove())
                {
                    chunks[key].CompleteValueJob(valueJobs[key]);
                    meshJobs.Add(key, chunks[key].ScheduleMeshJob());

                    valueJobs.Remove(key);
                }
            }
            keys.Dispose();
        }
        if(meshJobs.Count() > 0)
        {
            NativeArray<float3> keys = meshJobs.GetKeyArray(Allocator.Temp);
            foreach (float3 key in keys)
            {
                if (meshJobs[key].IsCompleted || chunks[key].ShouldRemove())
                {
                    chunks[key].CompleteMeshJob(meshJobs[key]);

                    meshJobs.Remove(key);
                }
            }
            keys.Dispose();
        }
    }
    public void OnApplicationQuit()
    {
        meshJobs.Dispose();
        valueJobs.Dispose();
        MarchingCubes.Dispose();
    }
    public float3[] GetVertices(Vector3 pos, float pointSpacing)
    {
        float3[] vertices = new float3[8];
        vertices[0] = new Vector3(pos.x, pos.y, pos.z + pointSpacing);
        vertices[1] = new Vector3(pos.x + pointSpacing, pos.y, pos.z + pointSpacing);
        vertices[2] = new Vector3(pos.x + pointSpacing, pos.y, pos.z);
        vertices[3] = new Vector3(pos.x, pos.y, pos.z);
        vertices[4] = new Vector3(pos.x, pos.y + pointSpacing, pos.z + pointSpacing);
        vertices[5] = new Vector3(pos.x + pointSpacing, pos.y + pointSpacing, pos.z + pointSpacing);
        vertices[6] = new Vector3(pos.x + pointSpacing, pos.y + pointSpacing, pos.z);
        vertices[7] = new Vector3(pos.x, pos.y + pointSpacing, pos.z);
        return vertices;
    }
}