using System;
using System.Collections;
using UnityEngine;

// Lets GameManager / PenaltyKickInput talk to either the procedural primitive
// kicker (KickerAnimator) or a real Mixamo skinned character (MixamoKickerAnimator)
// without caring which one is in the scene. An abstract MonoBehaviour (not a plain
// C# interface) so Unity can still serialize the reference in the saved scene.
public abstract class KickerAnimatorBase : MonoBehaviour
{
    public abstract void ResetToStart();
    public abstract IEnumerator ApproachRoutine();
    public abstract IEnumerator PlayKick(Action onContact);
    public abstract IEnumerator PlayCelebration();
}
