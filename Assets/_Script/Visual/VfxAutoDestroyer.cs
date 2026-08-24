using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class VfxAutoDestroyer : MonoBehaviour
{
    private void Start()
    {
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            Destroy(gameObject, stateInfo.length);
        }
        else
        {
            Destroy(gameObject, 1f);
        }
    }
}