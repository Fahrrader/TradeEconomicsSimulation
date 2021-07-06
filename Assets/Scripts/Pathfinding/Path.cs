using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Path
{
    public readonly Vector3[] lookPoints;
    public readonly PathLine[] turnBoundaries;
    public readonly int finishLineIndex;

    public Path(Vector3[] waypoints, Vector3 startPos, float turnDst) 
    {
        lookPoints = waypoints;
        turnBoundaries = new PathLine[lookPoints.Length];
        finishLineIndex = turnBoundaries.Length - 1;

        var previousPoint = V3ToV2(startPos);
        for (var i = 0; i < lookPoints.Length; i++) 
        {
            var currentPoint = V3ToV2(lookPoints[i]);
            var dirToCurrentPoint = (currentPoint - previousPoint).normalized;
            var turnBoundaryPoint = i == finishLineIndex ? currentPoint : currentPoint - dirToCurrentPoint * turnDst;
            turnBoundaries[i] = new PathLine(turnBoundaryPoint, previousPoint - dirToCurrentPoint * turnDst);
            previousPoint = turnBoundaryPoint;
        }

        /*float dstFromEndPoint = 0;
        for (var i = lookPoints.Length - 1; i > 0; i--) 
        {
            dstFromEndPoint += Vector3.Distance(lookPoints[i], lookPoints[i - 1]);
            if (!(dstFromEndPoint > stoppingDst)) continue;
            slowDownIndex = i;
            break;
        }*/
    }

    private static Vector2 V3ToV2(Vector3 v3) 
    {
        return new Vector2(v3.x, v3.z);
    }

    public void DrawWithGizmos() 
    {
        Gizmos.color = Color.black;
        foreach (var p in lookPoints) 
        {
            Gizmos.DrawCube(p + Vector3.up * .1f, Vector3.one * 0.1f);
        }

        Gizmos.color = Color.white;
        foreach (var l in turnBoundaries) 
        {
            l.DrawWithGizmos(2);
        }

    }
}
