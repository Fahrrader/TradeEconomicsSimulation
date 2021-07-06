using System;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

public class PathGrid : MonoBehaviour
{
    public LayerMask unwalkableMask;
    public float nodeRadius;
    private PathNodeOld[,] grid;

    private Vector2 gridWorldSize;
    private int gridSizeX, gridSizeY;
    private float nodeDiameter;
    
    public List<PathNodeOld> path;
    public bool showGridGizmos;

    void Awake()
    {
        gridWorldSize = new Vector2(0, 0);
        nodeRadius = 0.5f;
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        //SetGrid();
    }

    public int MaxSize => gridSizeX * gridSizeY;

    public void SetGrid(float[,] costGrid)
    {
        gridWorldSize = new Vector2(costGrid.GetLength(0), costGrid.GetLength(1));
        gridSizeX = costGrid.GetLength(0);//Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = costGrid.GetLength(1);//Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        
        grid = new PathNodeOld[gridSizeX, gridSizeY];
        var worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x/2 - Vector3.forward * gridWorldSize.y/2;
        
        for (var x = 0; x < gridSizeX; x++)
        {
            for (var y = 0; y < gridSizeY; y++)
            {
                var worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) +
                                 Vector3.forward * (y * nodeDiameter + nodeRadius);
                var walkable = !Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask);

                var movementCost = costGrid[x, y];

                grid[x,y] = new PathNodeOld(worldPoint, walkable, x, y, movementCost);

                /*var ray = new Ray(worldPoint + Vector3.up * 50, Vector3.down);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 100, walkableMask))*/
            }            
        }
    } 

    public PathNodeOld GetNodeFromWorldPoint(Vector3 worldPos)
    {
        var percentX = (worldPos.x + gridWorldSize.x / 2) / gridWorldSize.x;
        var percentY = (worldPos.z + gridWorldSize.y / 2) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        var x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        var y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return grid[x,y];
    }

    public List<PathNodeOld> GetNeighbors(PathNodeOld node) {
        var neighbours = new List<PathNodeOld>();

        for (var x = -1; x <= 1; x++) {
            for (var y = -1; y <= 1; y++) {
                if (x == 0 && y == 0)
                    continue;

                var checkX = node.gridX + x;
                var checkY = node.gridY + y;

                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY) {
                    neighbours.Add(grid[checkX,checkY]);
                }
            }
        }

        return neighbours;
    }

    private void OnDrawGizmos()
    {
        if (!showGridGizmos) return;
        
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1, gridWorldSize.y));
        if (grid == null) return;
        foreach (var n in grid)
        {
            Gizmos.color = Color.Lerp(Color.white, Color.red, n.movementCost); //(n.walkable) ? Color.white : Color.red;
            Gizmos.DrawCube(n.worldPos + Vector3.up * .1f, (Vector3.forward + Vector3.right) * (nodeDiameter - .05f));
        }
    }
}
