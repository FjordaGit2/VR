﻿using SimpleJSON;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
public class Sc2LectureHall : LevelScript
{
    [SerializeField] TextMeshPro text;
    [SerializeField] float delay = 1f;
    int count = 0;
    int currentNumber = 0;
    bool posted = false;
    float startTime = 0f;
    
    new void StartTask()
    {
        base.StartTask();
        StartCoroutine(ShowNumber(true));
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isStarted)
                StartTask();
            else if (!posted)
                StartCoroutine(Post(true));
        }
    }
    IEnumerator Post(bool pressed)
    {
        posted = false;
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("username", LevelScript.UserName));
        formData.Add(new MultipartFormDataSection("digit", currentNumber.ToString()));
        formData.Add(new MultipartFormDataSection("spacebar_pressed", (pressed)? "YES": "NO"));
        if (pressed)
        {
            formData.Add(new MultipartFormDataSection("accuracy", (currentNumber == 3) ? "Correct" : "Wrong"));
            formData.Add(new MultipartFormDataSection("reaction_time", ((Time.time - startTime) * 1000).ToString("0.0")));
        } 
        UnityWebRequest www = UnityWebRequest.Post(Constant.DOMAIN + Constant.SC2Data, formData);
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(www.error);
        }
    }
    IEnumerator ShowNumber(bool _startDelay = false)
    {
        if(_startDelay){
            yield return new WaitForSeconds(3);
        }
        // this is for remove repeat
        while (true)
        {
            int newNumber = Random.Range(1, 10);
            if (newNumber != currentNumber)
            {
                currentNumber = newNumber;
                break;
            }
        }
        text.text = currentNumber.ToString();
        startTime = Time.time; 
        yield return new WaitForSeconds(delay);
         //if (!posted)
         //{
         //    StartCoroutine(Post(false));
         //}
        posted = false;
        count++;
        if (count < 225)
        {
            StartCoroutine(ShowNumber());
        }
        else
        {
            yield return new WaitForSeconds(2f);
            NextScene();
        }
    }
}