using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
public class TerrainChunk
{
    TerrainHandler handler;
    public Vector3 position, chunkDimensions;
    public Vector3 numPoints;
    float featureSize, surfaceLevel, pointSpacing;
    bool remove = false;
    Material terrainMaterial;
    GameObject chunkObject;
    Mesh chunkMesh;

    public TerrainChunk(GameObject chunkObject, TerrainHandler handler, Vector3 position, Vector3 dimensions, float pointSpacing, float featureSize, float surfaceLevel, Material terrainMaterial)
    {
        this.handler = handler;
        this.position = position;
        this.chunkDimensions = dimensions;
        this.pointSpacing = pointSpacing;
        this.featureSize = featureSize;
        this.surfaceLevel = surfaceLevel;
        this.terrainMaterial = terrainMaterial;

        this.chunkObject = chunkObject;
        this.chunkObject.transform.position = position;

        chunkMesh = new Mesh();
    }
    public void CreateMesh(Vector3[] vertices, int[] triangles)
    {
        chunkMesh.vertices = vertices;
        chunkMesh.triangles = triangles;
        chunkMesh.RecalculateNormals();

        chunkObject.GetComponent<MeshFilter>().sharedMesh = chunkMesh;
        chunkObject.GetComponent<MeshCollider>().sharedMesh = chunkMesh;
        if(position.y >= 0)
        {
            terrainMaterial.color = new UnityEngine.Color(0.91f, 0.4f, 0.4f, 1);
        }
        else
        {
            terrainMaterial.color = new UnityEngine.Color(1, 1, 1, 1);
        }
        chunkObject.GetComponent<MeshRenderer>().sharedMaterial = new Material(terrainMaterial);

        chunkObject.SetActive(true);
    }
    NativeArray<float4> valueArray;
    public JobHandle ScheduleValueJob()
    {
        int numPoints = (int)((chunkDimensions.x + 1) * (chunkDimensions.y + 1) * (chunkDimensions.z + 1));
        int3 intChunkDimensions = new int3((int)chunkDimensions.x, (int)chunkDimensions.y, (int)chunkDimensions.z);

        valueArray = new NativeArray<float4>(numPoints, Allocator.Persistent);

        ChunkJob chunkJob = new ChunkJob
        {
            valueList = valueArray,
            chunkPos = position,
            chunkDimensions = intChunkDimensions,
            pointSpacing = pointSpacing,
            featureSize = featureSize,

        };
        return chunkJob.Schedule(numPoints, 100);
    }
    NativeArray<float> pointValueArray;
    NativeArray<float4> vertexArray;
    NativeList<float3> vertices;
    NativeList<int> triangles;
    public JobHandle ScheduleMeshJob()
    {
        vertices = new NativeList<float3>(Allocator.Persistent);
        triangles = new NativeList<int>(Allocator.Persistent);

        int3 intChunkDimensions = new int3((int)chunkDimensions.x, (int)chunkDimensions.y, (int)chunkDimensions.z);

        pointValueArray = new NativeArray<float>(8, Allocator.TempJob);
        vertexArray = new NativeArray<float4>(8, Allocator.TempJob);

        MeshJob meshJob = new MeshJob
        {
            values = valueArray,
            verticesOut = vertices,
            triangles = triangles,
            surfaceLevel = surfaceLevel,
            chunkDimensions = intChunkDimensions,
            triangulation = MarchingCubes.triangulationNative,
            cornerIndexAFromEdge = MarchingCubes.cornerIndexAFromEdgeNative,
            cornerIndexBFromEdge = MarchingCubes.cornerIndexBFromEdgeNative,
            pointValueArray = pointValueArray,
            vertexArray = vertexArray,
        };

        return meshJob.Schedule();
    }
    public void CompleteValueJob(JobHandle job)
    {
        job.Complete();
    }
    public void CompleteMeshJob(JobHandle job)
    {
        job.Complete();

        NativeArray<float3> verticesNative = vertices.ToArray(Allocator.Temp);
        
        Vector3[] verticesMesh = new Vector3[verticesNative.Length];
        for (int i = 0; i < verticesMesh.Length; i++)
        {
            verticesMesh[i] = verticesNative[i];
        }
        verticesNative.Dispose();

        NativeArray<int> trianglesNative = triangles.ToArray(Allocator.Temp);
        int[] trianglesMesh = new int[trianglesNative.Length];
        trianglesNative.CopyTo(trianglesMesh);

        trianglesNative.Dispose();

        valueArray.Dispose();
        vertices.Dispose();
        triangles.Dispose();
        pointValueArray.Dispose();
        vertexArray.Dispose();

        CreateMesh(verticesMesh, trianglesMesh);
    }
    public void SetRemove(bool remove)
    {
        this.remove = remove;
    }
    public bool ShouldRemove()
    {
        return this.remove;
    }
    public GameObject Remove()
    {
        chunkObject.SetActive(false);
        return chunkObject;
    }
}
[BurstCompile]
public struct ChunkJob : IJobParallelFor
{
    public NativeArray<float4> valueList;
    public float3 chunkPos;
    public int3 chunkDimensions;
    public float3 pointSpacing;
    public float featureSize;

    public float x, y, z;
    public void Execute(int index)
    {
        z = index % (chunkDimensions.z + 1);
        y = index / (chunkDimensions.y + 1) % (chunkDimensions.z + 1);
        x = index / ((chunkDimensions.x + 1) * (chunkDimensions.x + 1));

        float3 point = new float3(x, y, z);
        float3 samplePoint = new float3(x * pointSpacing.x, y * pointSpacing.y, z * pointSpacing.z) + chunkPos;
        float noiseValue = noise.snoise(samplePoint / (featureSize/2));
        float frequency = 0.91f, roughness = 2f;
        if (samplePoint.y >= 0)
        {
            for (int i = 0; i < 2; i++)
            {
                noiseValue += samplePoint.y / 20 + noise.snoise(samplePoint / featureSize * frequency)/10;
                frequency *= roughness;
            }
        }
        float finalNoise = noiseValue;
        valueList[index] = new float4(point, finalNoise);
    }
}
[BurstCompile]
public struct MeshJob : IJob
{
    public NativeArray<float4> values;

    public NativeList<float3> verticesOut;
    public NativeList<int> triangles;

    public float surfaceLevel;
    public int3 chunkDimensions;

    [ReadOnly] public NativeArray<int> triangulation;
    [ReadOnly] public NativeArray<int> cornerIndexAFromEdge, cornerIndexBFromEdge;

    public NativeArray<float> pointValueArray;
    public NativeArray<float4> vertexArray;
    public void Execute()
    {
        int triIndex = 0;
        float3 vertexPos;
        for(int count = 0; count < values.Length; count++)
        {
            float4 cube = values[count];
            for (int i = 0; i < 16; i++)
            {
                int cubeIndex = GetCubeIndex(new float3(cube.x, cube.y, cube.z));
                if (cubeIndex != -1)
                {
                    int edgeIndex = triangulation[cubeIndex * 16 + i];
                    if (edgeIndex != -1)
                    {
                        int indexA = cornerIndexAFromEdge[edgeIndex];
                        int indexB = cornerIndexBFromEdge[edgeIndex];

                        float t = (surfaceLevel - pointValueArray[indexA]) / (pointValueArray[indexB] - pointValueArray[indexA]);
                        float3 vertexA = new float3(vertexArray[indexA].x, vertexArray[indexA].y, vertexArray[indexA].z);
                        float3 vertexB = new float3(vertexArray[indexB].x, vertexArray[indexB].y, vertexArray[indexB].z);
                        vertexPos = vertexA + t * (vertexB - vertexA);
                        verticesOut.Add(vertexPos);
                        triangles.Add(triIndex);
                        triIndex++;
                    }
                }
            }
        }
    }
    public int GetCubeIndex(float3 pos)
    {
        int cubeIndex = 0;
        if (pos.x < chunkDimensions.x && pos.y < chunkDimensions.y && pos.z < chunkDimensions.z)
        {
            vertexArray[0] = values[GetValueIndex(pos)];
            vertexArray[1] = values[GetValueIndex(pos + new float3(1, 0, 0))];
            vertexArray[2] = values[GetValueIndex(pos + new float3(1, 0, 1))];
            vertexArray[3] = values[GetValueIndex(pos + new float3(0, 0, 1))];
            vertexArray[4] = values[GetValueIndex(pos + new float3(0, 1, 0))];
            vertexArray[5] = values[GetValueIndex(pos + new float3(1, 1, 0))];
            vertexArray[6] = values[GetValueIndex(pos + new float3(1, 1, 1))];
            vertexArray[7] = values[GetValueIndex(pos + new float3(0, 1, 1))];

            pointValueArray[0] = vertexArray[0].w;
            pointValueArray[1] = vertexArray[1].w;
            pointValueArray[2] = vertexArray[2].w;
            pointValueArray[3] = vertexArray[3].w;
            pointValueArray[4] = vertexArray[4].w;
            pointValueArray[5] = vertexArray[5].w;
            pointValueArray[6] = vertexArray[6].w;
            pointValueArray[7] = vertexArray[7].w;

            if (pointValueArray[0] < surfaceLevel) cubeIndex |= 1;
            if (pointValueArray[1] < surfaceLevel) cubeIndex |= 2;
            if (pointValueArray[2] < surfaceLevel) cubeIndex |= 4;
            if (pointValueArray[3] < surfaceLevel) cubeIndex |= 8;
            if (pointValueArray[4] < surfaceLevel) cubeIndex |= 16;
            if (pointValueArray[5] < surfaceLevel) cubeIndex |= 32;
            if (pointValueArray[6] < surfaceLevel) cubeIndex |= 64;
            if (pointValueArray[7] < surfaceLevel) cubeIndex |= 128;
        }
        else
        {
            cubeIndex = -1;
        }

        return cubeIndex;
    }
    public int GetValueIndex(float3 pos)
    {
        return (int)((pos.x * (chunkDimensions.z+1) + pos.y) * (chunkDimensions.x+1) + pos.z);
    }
}

