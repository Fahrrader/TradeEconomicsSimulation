using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pathfinder : MonoBehaviour
{
    private const float MINPathUpdateTime = .2f;
    private const float PathUpdateMoveThreshold = .5f;
    
    public Transform target;
    public float speed = 3f;
    public float turnSpeed = 3;
    public float turnDst = 5;
    //public float stoppingDst = 10;

    private Path path;

    void Start()
    {
        StartCoroutine(UpdatePath());
    }

    private void OnPathFound(Vector3[] waypoints, bool pathSuccess)
    {
        if (!pathSuccess) return;
        
        path = new Path(waypoints, transform.position, turnDst);
        
        StopCoroutine(nameof(FollowPath));
        StartCoroutine(nameof(FollowPath));
    }

    private IEnumerator UpdatePath() 
    {
        if (Time.timeSinceLevelLoad < .3f) 
        {
            yield return new WaitForSeconds (.3f);
        }

        var position = target.position;
        PathRequestManager.RequestPath (transform.position, position, OnPathFound);

        const float sqrMoveThreshold = PathUpdateMoveThreshold * PathUpdateMoveThreshold;
        var targetPosOld = position;

        while (true) 
        {
            yield return new WaitForSeconds (MINPathUpdateTime);
            if (!((target.position - targetPosOld).sqrMagnitude > sqrMoveThreshold)) continue;
            PathRequestManager.RequestPath (transform.position, target.position, OnPathFound);
            targetPosOld = target.position;
        }
    }

    private IEnumerator FollowPath()
    {
        var followingPath = true;
        var pathIndex = 0;
        transform.LookAt(path.lookPoints[0]);

        while (followingPath)
        {
            var pos = new Vector2(transform.position.x, transform.position.z);
            while (path.turnBoundaries[pathIndex].HasCrossedLine(pos))
            {
                if (pathIndex == path.finishLineIndex)
                {
                    followingPath = false;
                    break;
                }
                else
                {
                    pathIndex++;
                }
            }

            if (followingPath)
            {

                /*if (pathIndex >= path.slowDownIndex && stoppingDst > 0)
                {
                    speedPercent = Mathf.Clamp01(path.turnBoundaries[path.finishLineIndex].DistanceFromPoint(pos) /
                                                 stoppingDst);
                    if (speedPercent < 0.01f)
                    {
                        followingPath = false;
                    }
                }*/

                var targetRotation = Quaternion.LookRotation(path.lookPoints[pathIndex] - transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
                transform.localEulerAngles = new Vector3(0, transform.localEulerAngles.y, 0);
                transform.Translate(Vector3.forward * Time.deltaTime * speed, Space.Self);
            }

            yield return null;
        }
    }

    public void OnDrawGizmos() {
        path?.DrawWithGizmos();
    }
}
