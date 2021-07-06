using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HexGrid : MonoBehaviour
{
    public Color defaultColor = Color.white;

    public HexCell cellPrefab;
    public Image cellHighlightPrefab;
    public HexGridChunk chunkPrefab;

    public Texture2D noiseSource;

    [NonSerialized]
    public WorldManager manager;

    private int seed;
    private HexGridChunk[] chunks;
    private HexCell[] cells;

    private int chunkCountX, chunkCountZ;
    private int cellCountX, cellCountZ;

    public void Initialize(int chunkCountX, int chunkCountZ, int seed, WorldManager manager)
    {
        this.manager = manager;
        this.seed = seed;
        this.chunkCountX = chunkCountX;
        this.chunkCountZ = chunkCountZ;

        cellCountX = this.chunkCountX * HexMetrics.ChunkSizeX;
        cellCountZ = this.chunkCountZ * HexMetrics.ChunkSizeZ;
        
        HexMetrics.noiseSource = noiseSource;
        HexMetrics.InitializeHashGrid(seed);

        CreateChunks();
        CreateCells();
        //PopulateChunks();
    }

    private void CreateChunks()
    {
        chunks = new HexGridChunk[chunkCountX * chunkCountZ];

        for (int z = 0, i = 0; z < chunkCountZ; z++)
        for (var x = 0; x < chunkCountX; x++)
        {
            var chunk = chunks[i++] = Instantiate(chunkPrefab);
            chunk.transform.SetParent(transform);
        }
    }

    private void CreateCells()
    {
        cells = new HexCell[cellCountZ * cellCountX];

        for (int z = 0, i = 0; z < cellCountZ; z++)
        for (var x = 0; x < cellCountX; x++)
            CreateCell(x, z, i++);
    }

    /*private void PopulateChunks()
    {
        foreach (var chunk in chunks)
        {
            chunk.GenerateChunkData(manager);
        }
    }*/

    private void CreateCell(int x, int z, int i)
    {
        Vector3 position;
        position.x = (x + z % 2 * 0.5f) * (HexMetrics.InnerRadius * 2f);
        position.y = 0f;
        position.z = z * (HexMetrics.OuterRadius * 1.5f);

        var cell = cells[i] = Instantiate(cellPrefab);
        cell.transform.localPosition = position; // cell.Position = 
        cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
        cell.Color = defaultColor;

        if (x > 0) cell.SetNeighbor(HexDirection.W, cells[i - 1]);
        if (z > 0)
        {
            if ((z & 1) == 0)
            {
                cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX]);
                if (x > 0) cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX - 1]);
            }
            else
            {
                cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX]);
                if (x < cellCountX - 1) cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX + 1]);
            }
        }

        var highlight = Instantiate(cellHighlightPrefab);
        highlight.rectTransform.anchoredPosition = new Vector2(position.x, position.z);
        cell.uiRect = highlight.rectTransform;
        cell.highlight = highlight;

        AddCellToChunk(x, z, cell);
    }

    private void AddCellToChunk(int x, int z, HexCell cell)
    {
        var chunkX = x / HexMetrics.ChunkSizeX;
        var chunkZ = z / HexMetrics.ChunkSizeZ;
        var chunk = chunks[chunkX + chunkZ * chunkCountX];

        var localX = x - chunkX * HexMetrics.ChunkSizeX;
        var localZ = z - chunkZ * HexMetrics.ChunkSizeZ;
        chunk.AddCell(localX + localZ * HexMetrics.ChunkSizeX, cell);
    }

    private void OnEnable()
    {
        if (!HexMetrics.noiseSource)
        {
            HexMetrics.noiseSource = noiseSource;
            HexMetrics.InitializeHashGrid(seed);
        }
    }

    public HexCell GetCell(Vector3 position)
    {
        position = transform.InverseTransformPoint(position);
        var coordinates = HexCoordinates.FromPosition(position);
        var index =
            coordinates.X + coordinates.Z * cellCountX + coordinates.Z / 2;
        return cells[index];
    }

    public HexCell GetCell(HexCoordinates coordinates)
    {
        var z = coordinates.Z;
        if (z < 0 || z >= cellCountZ) return null;
        var x = coordinates.X + z / 2;
        if (x < 0 || x >= cellCountX) return null;
        return cells[x + z * cellCountX];
    }

    public HexCell GetCell(int x, int z)
    {
        if (z < 0 || z >= cellCountZ) return null;

        if (x < 0 || x >= cellCountX) return null;

        return cells[x + z * cellCountX];
    }

    /*public void ShowUI(bool visible)
    {
        for (var i = 0; i < chunks.Length; i++) chunks[i].ShowUI(visible);
    }*/
}