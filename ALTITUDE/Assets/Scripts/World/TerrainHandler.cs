using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class TerrainHandler : MonoBehaviour
{
    public NoiseSettings noiseSettings;
    public Material terrainMat;
    public Transform viewer;
    public int3 viewDist;
    public int3 chunkSize;
    Dictionary<Vector3, TerrainChunk> chunks = new Dictionary<Vector3, TerrainChunk>();
    Queue<TerrainChunk> completePool = new Queue<TerrainChunk>();
    // Start is called before the first frame update
    void Start()
    {
        GenerateStartingChunks();
    }
    private void Update()
    {
        UpdateChunks();
    }
    float completeCountdown;
    float completeTime = 0.025f;
    // Update is called once per frame
    public void UpdateChunks()
    {
        foreach(TerrainChunk chunk in chunks.Values)
        {
            chunk.SetRemove(true);
        }

        int currentX = Mathf.RoundToInt(viewer.position.x / chunkSize.x);
        int currentY = Mathf.RoundToInt(viewer.position.y / chunkSize.y);
        int currentZ = Mathf.RoundToInt(viewer.position.z / chunkSize.z);
        int viewX = viewDist.x / 2;
        int viewY = viewDist.y / 2;
        int viewZ = viewDist.z / 2;
        for(int x = -viewX; x < viewX; x++)
        {
            for (int y = -viewY; y < viewY; y++)
            {
                for (int z = -viewZ; z < viewZ; z++)
                {
                    Vector3 chunkCoord = new Vector3(currentX + x, currentY + y, currentZ + z);
                    if (chunks.ContainsKey(chunkCoord))
                    {
                        chunks[chunkCoord].SetRemove(false);
                    }
                    else
                    {
                        Vector3 worldPosition = new Vector3(chunkCoord.x * chunkSize.x, chunkCoord.y * chunkSize.y, chunkCoord.z * chunkSize.z);
                        TerrainChunk chunk = new TerrainChunk(this, worldPosition, chunkSize, terrainMat);
                        chunk.ScheduleValueJob();
                        chunks.Add(chunkCoord, chunk);
                    }
                }
            }
        }

        completeCountdown -= Time.deltaTime;
        if (completeCountdown < 0)
        {
            completeCountdown = completeTime;
            if (completePool.Count > 0)
            {
                TerrainChunk chunk = completePool.Dequeue();
                if (!chunk.removed)
                {
                    chunk.CompleteValueJob();
                }
                chunk.queued = false;
            }
        }

        Vector3[] keys = chunks.Keys.ToArray();
        foreach(Vector3 key in keys)
        {
            TerrainChunk chunk = chunks[key];
            if (chunk.ShouldRemove())
            {
                chunk.Remove();
                chunks.Remove(key);
            }
            else if (!chunk.queued && !chunk.IsGenerated() && chunk.IsValueJobComplete())
            {
                completePool.Enqueue(chunk);
                chunk.queued = true;
            }
        }
    }
    public void GenerateStartingChunks()
    {
        int currentX = Mathf.RoundToInt(viewer.position.x / chunkSize.x);
        int currentY = Mathf.RoundToInt(viewer.position.y / chunkSize.y);
        int currentZ = Mathf.RoundToInt(viewer.position.z / chunkSize.z);
        int viewX = viewDist.x / 2;
        int viewY = viewDist.y / 2;
        int viewZ = viewDist.z / 2;
        for (int x = -viewX; x < viewX; x++)
        {
            for (int y = -viewY; y < viewY; y++)
            {
                for (int z = -viewZ; z < viewZ; z++)
                {
                    Vector3 chunkCoord = new Vector3(currentX + x, currentY + y, currentZ + z);
                    if (chunks.ContainsKey(chunkCoord))
                    {
                        chunks[chunkCoord].SetRemove(false);
                    }
                    else
                    {
                        Vector3 worldPosition = new Vector3(chunkCoord.x * chunkSize.x, chunkCoord.y * chunkSize.y, chunkCoord.z * chunkSize.z);
                        TerrainChunk chunk = new TerrainChunk(this, worldPosition, chunkSize, terrainMat);
                        chunk.ScheduleValueJob();
                        chunk.CompleteValueJob();
                        chunks.Add(chunkCoord, chunk);
                    }
                }
            }
        }
    }
    public void OnApplicationQuit()
    {
        foreach (TerrainChunk chunk in chunks.Values)
        {
            if (!chunk.IsGenerated())
            {
                chunk.CompleteValueJob();
            }
        }
    }
}
