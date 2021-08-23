﻿using System.Collections;
using UnityEngine;
public class PlayerController : MonoBehaviour
{
    float xRotation = 0f;
    [SerializeField] float mouseSensitivity = 100f;
    [SerializeField] bool isMove = false;
    Transform cameraTransform;
    CharacterController controller;
    private void Start()
    {
        cameraTransform = transform.GetComponentInChildren<Camera>().transform;
        if (isMove)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.fixedDeltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.fixedDeltaTime;
        transform.Rotate(Vector3.up, mouseX);
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation , -90 , 90);
        cameraTransform.localRotation = Quaternion.Euler(xRotation , 0f , 0f);
        if (isMove)
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            Vector3 move = transform.right * x + transform.forward * z;
            controller.Move(move / 10);
        }
    }
}