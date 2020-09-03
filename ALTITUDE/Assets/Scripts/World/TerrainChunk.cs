using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

public class TerrainChunk
{
    private Material terrainMat;
    private bool remove;
    private bool generated;
    public bool queued, removed;

    private JobHandle valueHandle;
    private TerrainHandler handler;
    private GameObject chunkObject;
    private Vector3 position;
    private int3 chunkSize;

    NativeArray<float4> valueArray;

    public TerrainChunk(TerrainHandler handler, Vector3 position, int3 chunkSize, Material terrainMat)
    {
        this.handler = handler;
        this.position = position;
        this.chunkSize = chunkSize;

        chunkObject = new GameObject("Chunk Object: " + position);
        chunkObject.transform.position = position;
        chunkObject.transform.SetParent(handler.gameObject.transform, true);
        this.terrainMat = terrainMat;
    }
    public void ScheduleValueJob()
    {
        int3 chunkDimPlusOne = chunkSize + new int3(1, 1, 1);
        valueArray = new NativeArray<float4>(chunkDimPlusOne.x * chunkDimPlusOne.y * chunkDimPlusOne.z, Allocator.Persistent);
        ChunkValueJob valueJob = new ChunkValueJob
        {
            scale = handler.noiseSettings.scale,
            baseRoughness = handler.noiseSettings.baseRoughness,
            roughness = handler.noiseSettings.roughness,
            persistence = handler.noiseSettings.persistence,
            strength = handler.noiseSettings.strength,
            recede = handler.noiseSettings.recede,
            minValue = handler.noiseSettings.minValue,
            numLayers = handler.noiseSettings.numLayers,
            seed = handler.noiseSettings.seed,
            chunkPos = position,
            chunkDim = chunkDimPlusOne,
            values = valueArray,

            noiseSample = new float3(),
            position = new float3(),
            worldPosition = new float3(),
        };
        valueHandle = valueJob.Schedule(valueArray.Length, chunkDimPlusOne.x);
    }
    public bool IsValueJobComplete()
    {
        return valueHandle.IsCompleted;
    }
    public void CompleteValueJob()
    {
        valueHandle.Complete();
        if(chunkObject.GetComponent<MeshRenderer>() == null)
        {
            chunkObject.AddComponent<MeshRenderer>();
            chunkObject.AddComponent<MeshFilter>();
            chunkObject.AddComponent<MeshCollider>();
        }
        Mesh chunkMesh = MarchingCubes.GenerateMesh(valueArray.ToArray(), chunkSize + new int3(1,1,1), 0.5f);
        chunkObject.GetComponent<MeshFilter>().sharedMesh = chunkMesh;
        chunkObject.GetComponent<MeshRenderer>().sharedMaterial = terrainMat;
        chunkObject.GetComponent<MeshCollider>().sharedMesh = chunkMesh;
        chunkObject.layer = LayerMask.NameToLayer("Ground");

        generated = true;
        valueArray.Dispose();
    }
    public bool IsGenerated()
    {
        return generated;
    }
    public void SetRemove(bool rem)
    {
        remove = rem;
    }
    public bool ShouldRemove()
    {
        return remove;
    }
    public GameObject GetObject()
    {
        return this.chunkObject;
    }
    public void Remove()
    {
        if (!generated)
        {
            CompleteValueJob();
        }
        chunkObject.SetActive(false);
        removed = true;
    }
    [BurstCompile]
    public struct ChunkValueJob : IJobParallelFor
    {
        public float scale;
        public float baseRoughness;
        public float roughness;
        public float persistence;
        public float strength;
        public float recede;
        public float minValue;
        public int numLayers;
        public int seed;

        public float3 chunkPos;
        public int3 chunkDim;
        public NativeArray<float4> values;

        public float3 position;
        public float3 worldPosition;
        public float3 noiseSample;

        public void Execute(int index)
        {
            position.x = index / (chunkDim.x * chunkDim.x);
            position.y = ((index / chunkDim.y) % chunkDim.z);
            position.z = (index % chunkDim.z);

            worldPosition = position + chunkPos;

            float noiseVal = -worldPosition.y + GetNoise(worldPosition);

            values[index] = new float4(position.x, position.y, position.z, noiseVal);
        }
        
        public float GetNoise(Vector3 point)
        {
            float noiseValue = 0;
            float frequency = baseRoughness;
            float amplitude = 1;
            for (int i = 0; i < numLayers; i++)
            {
                float x = (point.x + seed) / scale * frequency;
                float y = (point.y + seed) / scale * frequency;
                float z = (point.z + seed) / scale * frequency;

                noiseSample.x = x;
                noiseSample.y = y;
                noiseSample.z = z;

                float v = noise.snoise(noiseSample);

                noiseValue += v * amplitude;
                frequency *= roughness;
                amplitude *= persistence;
            }
            noiseValue = Mathf.Max(minValue, noiseValue - recede);
            return noiseValue * strength;
        }
    }
}
