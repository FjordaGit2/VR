using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAnimationMale : MonoBehaviour
{
  
  public Animator anim2;


    void Start()
    {
        
        anim2.GetComponent<Animator>();
        anim2.SetBool("AnimationMale2", false);
        anim2.SetBool("AnimationMale3", false);
        StartCoroutine("PlayAnimPart1");
    }

    IEnumerator PlayAnimPart1(){

    
        yield return new WaitForSeconds(4f);

        anim2.SetBool("AnimationMale2", true);
        anim2.SetBool("AnimationMale3", false);

        yield return new WaitForSeconds(4f);

        anim2.SetBool("AnimationMale3", true);
         anim2.SetBool("AnimationMale2", false);

        yield return new WaitForSeconds(4f);

        StartCoroutine("PlayAnimPart2");
        


    }

     IEnumerator PlayAnimPart2(){

    
        yield return new WaitForSeconds(4f);

        anim2.SetBool("AnimationMale2", true);
        anim2.SetBool("AnimationMale3", false);

        yield return new WaitForSeconds(4f);

        anim2.SetBool("AnimationMale3", true);
         anim2.SetBool("AnimationMale2", false);

        yield return new WaitForSeconds(4f);

        StartCoroutine("PlayAnimPart3");
        


    }

     IEnumerator PlayAnimPart3(){

    
        yield return new WaitForSeconds(4f);

        anim2.SetBool("AnimationMale2", true);
        anim2.SetBool("AnimationMale3", false);

        yield return new WaitForSeconds(4f);

        anim2.SetBool("AnimationMale3", true);
         anim2.SetBool("AnimationMale2", false);

        yield return new WaitForSeconds(4f);

        StartCoroutine("PlayAnimPart4");
        


    }

     IEnumerator PlayAnimPart4(){

    
        yield return new WaitForSeconds(4f);

        anim2.SetBool("AnimationMale2", true);
        anim2.SetBool("AnimationMale3", false);

        yield return new WaitForSeconds(4f);

        anim2.SetBool("AnimationMale3", true);
         anim2.SetBool("AnimationMale2", false);

        yield return new WaitForSeconds(4f);

        StartCoroutine("PlayAnimPart3");
        


    }

    
    
}
