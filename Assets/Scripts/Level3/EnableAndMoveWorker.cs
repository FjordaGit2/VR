using UnityEngine;

/// <summary>
/// Legacy helper — worker show/hide and timing now live on DisableEnableMoveWorker.
/// Remove this component from the scene if DisableEnableMoveWorker is present (avoids duplicate logic).
/// </summary>
public class EnableAndMoveWorker : MonoBehaviour
{
    public GameObject worker;
    public Animator anim;
}
