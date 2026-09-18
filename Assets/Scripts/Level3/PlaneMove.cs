using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneMove : MonoBehaviour
{
    public GameObject Plane;
    public AudioSource PlaneSource;

    void Start()
    {

        //Plane.SetActive(false);
        StartCoroutine("MovePlane");
        
    }


    IEnumerator MovePlane(){

        yield return new WaitForSeconds(100f);

        iTween.MoveTo(Plane.gameObject, iTween.Hash("position", new Vector3(-460,122,-33), "time", 45f, "easetype", iTween.EaseType.easeInOutSine));
        PlaneSource.Play();

        yield return new WaitForSeconds(50f);
        Plane.SetActive(false);


    }
}
