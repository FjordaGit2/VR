using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OtherBirdsMove : MonoBehaviour
{
    public GameObject Birds;

    void Start()
    {
        StartCoroutine("MoveOtherBirds");
        
    }


    IEnumerator MoveOtherBirds(){

        yield return new WaitForSeconds(70f);

        iTween.MoveTo(Birds.gameObject, iTween.Hash("position", new Vector3(-512,160,-310), "time", 70f, "easetype", iTween.EaseType.easeInOutSine));

        yield return new WaitForSeconds(50f);
       
        Birds.SetActive(false);


    }
}
