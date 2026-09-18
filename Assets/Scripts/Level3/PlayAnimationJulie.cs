using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAnimationJulie : MonoBehaviour
{

    public Animator anim;
    public GameObject Julie;

    // Start is called before the first frame update
    void Start()
    {
        //Jack.SetActive(false);
        anim.GetComponent<Animator>();
        anim.SetBool("Animation1", false);

        StartCoroutine("PlayAnimPart1");

    }

     IEnumerator PlayAnimPart1(){

    
        yield return new WaitForSeconds(35f); 
        //Jack.SetActive(true);

        anim.SetBool("Animation1", true);

        yield return new WaitForSeconds(90f);

        anim.SetBool("Animation1", false);
        Julie.SetActive(false);

      



    }
}
