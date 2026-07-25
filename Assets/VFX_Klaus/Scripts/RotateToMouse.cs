using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RotateToMouse : MonoBehaviour
{
    public Camera cam;
    public float maximumLength;

    private Ray rayMouse;
    private Vector3 pos;
    private Vector3 direction;
    private Quaternion rotation;

    void Update()
    {
        if (cam == null)
        {
            Debug.Log("No Camera");
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        RaycastHit hit;
        Vector2 mousePos = mouse.position.ReadValue();
        rayMouse = cam.ScreenPointToRay(mousePos);
        if (Physics.Raycast(rayMouse.origin, rayMouse.direction, out hit, maximumLength))
        {
            RotateToMouseDirection(gameObject, hit.point);
        }
        else
        {
            Vector3 targetPosition = rayMouse.GetPoint(maximumLength);
            RotateToMouseDirection(gameObject, targetPosition);
        }
    }

    void RotateToMouseDirection (GameObject obj, Vector3 destination)
    {
        direction = destination - obj.transform.position;
        rotation = Quaternion.LookRotation (direction);
        obj.transform.localRotation = Quaternion.Lerp(obj.transform.rotation, rotation, 1);
    }

    public Quaternion GetRotation()
    {
        return rotation;
    }
}
