using UnityEngine;

// Same purpose as KickerAnimatorBase: a common base so GameManager can drive
// either the procedural or the Mixamo goalkeeper interchangeably.
public abstract class GoalkeeperAnimatorBase : MonoBehaviour
{
    public abstract void ResetKeeper();

    // Decides immediately whether this shot is saved (a coin-flip weighted by
    // difficulty/accuracy) and kicks off the dive animation for show - the
    // animation's visual timing/reach is cosmetic only and never the thing that
    // decides the outcome, so it stays correct regardless of how the underlying
    // rig/clip actually moves the character.
    public abstract bool ReactToShot(float targetX, float difficulty);

    // Plays the dive/catch visual toward an already-decided X, with no RNG
    // and no outcome of its own - used by ReactToShot internally for the AI,
    // and directly by GoalkeeperInput when a human player picks the dive.
    public abstract void PlayDirectedDive(float diveTargetX);

    // Instantly slides the keeper to worldX with no dive animation or
    // coroutine - used while a human player is actively dragging, so the
    // keeper tracks the finger/mouse in real time instead of committing to
    // one gesture. PlayDirectedDive is still what plays the actual dive/save
    // flourish once the drag resolves.
    public abstract void SetLateralPosition(float worldX);
}
