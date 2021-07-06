using System;
using System.Collections;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public float speed = 10f;
    
    public float speedBoost = 1.25f;

    public float rotationXSensitivity = 1f;
    public float rotationYSensitivity = 1f;
    public float minLookUpAngle = -60f;
    public float maxLookUpAngle = 80f; 

    public float zoomSensitivity = 30f;
    public float maxDist = 200f;
    public float minDist = 10f;

    public float visibleAreaMul = 3f;
    
    private float visibleArea = 1f;
    private float baseZoom;

    private Vector2 mouseAnchor;

    void Awake()
    {
        baseZoom = transform.position.y;
    }

    void Update()
    {
        if (EditorHandler.currentlyEditing) return;
        
        var boost = Input.GetAxisRaw("Fire3") * speedBoost + 1f;

        var yDelta = Input.GetAxisRaw("Vertical");
        if (yDelta != 0f)
            transform.position += Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward * (
                yDelta * 
                boost * 
                visibleArea * 
                speed * Time.unscaledDeltaTime);

        var xDelta = Input.GetAxisRaw("Horizontal");
        if (xDelta != 0f)
            transform.position += Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.right * (
                xDelta * 
                boost * 
                visibleArea * 
                speed * Time.unscaledDeltaTime);

        var zoomDelta = Input.GetAxisRaw("Mouse ScrollWheel");
        if (zoomDelta != 0f && !EventSystem.current.IsPointerOverGameObject())
        {
            transform.position -= Vector3.up * (
                zoomDelta * 
                zoomSensitivity * 
                boost * 
                visibleArea * 
                speed * Time.unscaledDeltaTime);
        }

        var rotationDelta = Input.GetButton("Allow Mouse Rotation");
        if (rotationDelta)
        {
            if (mouseAnchor == Vector2.zero)
                mouseAnchor = Input.mousePosition;
            AdjustRotation((Vector2) Input.mousePosition - mouseAnchor);
            mouseAnchor = Input.mousePosition;
        }
        else mouseAnchor = Vector2.zero;
        
        transform.position = new Vector3(
            transform.position.x, 
            Mathf.Clamp(transform.position.y, minDist, maxDist),
            transform.position.z);

        visibleArea = transform.position.y / baseZoom * visibleAreaMul;
    }
    
    private void AdjustRotation(Vector2 delta)
    {
        var rotation = transform.rotation;
        rotation *= Quaternion.Euler(-delta.y * rotationYSensitivity, delta.x * rotationXSensitivity, 0f);
        var rotClamped = rotation.eulerAngles.x >= 180f ? rotation.eulerAngles.x - 360f : rotation.eulerAngles.x;
        rotation.eulerAngles = new Vector3(
            Mathf.Clamp(rotClamped, minLookUpAngle, maxLookUpAngle),
            rotation.eulerAngles.y, 
            0f);
        transform.rotation = rotation;
    }
}
