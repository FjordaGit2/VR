﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class EventTriigger : MonoBehaviour
{
    [SerializeField] int EventIndex;
    bool triggered = false;
    LevelScript levelManager;
    void Start()
    {
        levelManager = FindObjectOfType<LevelScript>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggered && other.tag == "Player")
        {
            levelManager.SendMessage("OnEventTrigger", EventIndex);
        }
    }
}