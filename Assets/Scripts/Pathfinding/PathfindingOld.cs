using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using UnityEngine;

public class PathfindingOld : MonoBehaviour
{
    private PathRequestManager requestManager;
    private PathGrid grid;

    void Awake()
    {
        requestManager = GetComponent<PathRequestManager>();
        grid = GetComponent<PathGrid>();
    }

    /*void Update()
    {
        if (Input.GetButtonDown("Jump"))
            FindPath(seeker.position, target.position);
    }*/

    public void UpdateGrid(float[,] newCostGrid)
    {
        grid.SetGrid(newCostGrid);
    }
    
    public void StartFindPath(Vector3 startPos, Vector3 targetPos) {
        StartCoroutine(FindPath(startPos,targetPos));
    }

    IEnumerator FindPath(Vector3 startPos, Vector3 targetPos)
    {
        var waypoints = new Vector3[0];
        var pathSuccess = false;

        var startNode = grid.GetNodeFromWorldPoint(startPos);
        var endNode = grid.GetNodeFromWorldPoint(targetPos);
        startNode.parent = startNode;

        var openSet = new Heap<PathNodeOld>(grid.MaxSize);
        var closedSet = new HashSet<PathNodeOld>();
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            var currentNode = openSet.RemoveFirst();
            closedSet.Add(currentNode);

            if (currentNode == endNode)
            {
                pathSuccess = true;
                break;
            }

            foreach (var n in grid.GetNeighbors(currentNode))
            {
                if (closedSet.Contains(n)) continue;

                var newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, n) + n.movementCost;
                if (newMovementCostToNeighbor >= n.gCost && openSet.Contains(n)) continue;
                n.gCost = newMovementCostToNeighbor;
                n.hCost = GetDistance(n, endNode);
                n.parent = currentNode;
                if (!openSet.Contains(n))
                    openSet.Add(n);
                else 
                    openSet.UpdateItem(n);
            }
        }

        yield return null;
        if (pathSuccess) {
            waypoints = RetracePath(startNode, endNode);
        }
        requestManager.FinishedProcessingPath(waypoints, pathSuccess);
    }

    private Vector3[] RetracePath(PathNodeOld startNode, PathNodeOld endNode)
    {
        var path = new List<PathNodeOld>();
        var currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        var waypoints = SimplifyPath(path);
        Array.Reverse(waypoints);
        return waypoints;
    }

    Vector3[] SimplifyPath(IReadOnlyList<PathNodeOld> path)
    {
        var waypoints = new List<Vector3>();
        var directionOld = Vector2.zero;
        waypoints.Add(path[0].worldPos);

        for (var i = 1; i < path.Count; i++)
        {
            var directionNew = new Vector2(path[i - 1].gridX - path[i].gridX, path[i - 1].gridY - path[i].gridY);
            if (directionNew != directionOld)
            {
                waypoints.Add(path[i].worldPos);
            }
            directionOld = directionNew;
        }

        return waypoints.ToArray();
    }

    private static float GetDistance(PathNodeOld nodeA, PathNodeOld nodeB)
    {
        var dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        var dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}
