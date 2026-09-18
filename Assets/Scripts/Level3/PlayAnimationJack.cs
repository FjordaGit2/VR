using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAnimationJack : MonoBehaviour
{

    public Animator anim;
    public GameObject Jack;

    // Start is called before the first frame update
    void Start()
    {
        //Jack.SetActive(false);
        anim.GetComponent<Animator>();
        anim.SetBool("Animation1", false);

        StartCoroutine("PlayAnimPart1");

    }

     IEnumerator PlayAnimPart1(){

    
        yield return new WaitForSeconds(145f); 
        //Jack.SetActive(true);

        anim.SetBool("Animation1", true);

        yield return new WaitForSeconds(50f);

        anim.SetBool("Animation1", false);
        Jack.SetActive(false);

      



    }
}
