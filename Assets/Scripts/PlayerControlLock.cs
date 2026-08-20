using UnityEngine;

/// <summary>
/// Freezes the player and frees the mouse while a memory panel is open.
///
/// Deliberately has NO hard reference to Starter Assets. You drag whichever
/// components should switch off into the list in the Inspector, which means
/// this keeps working if you swap character controllers later.
///
/// For Unity's Starter Assets, drag these from the PlayerArmature object:
///   - ThirdPersonController
///   - StarterAssetsInputs
///   - PlayerInput
/// </summary>
public class PlayerControlLock : MonoBehaviour
{
    [Tooltip("Components disabled while a memory is open. Drag ThirdPersonController, StarterAssetsInputs, and PlayerInput here.")]
    public MonoBehaviour[] componentsToDisable;

    [Tooltip("Animator on the character. Its speed params get zeroed so she does not freeze mid-run-cycle.")]
    public Animator animator;

    [Tooltip("Animator float parameters to zero out while locked.")]
    public string[] speedParameters = { "Speed", "MotionSpeed" };

    private bool _locked;

    public bool IsLocked => _locked;

    public void SetLocked(bool locked)
    {
        _locked = locked;

        foreach (var c in componentsToDisable)
        {
            if (c != null) c.enabled = !locked;
        }

        if (locked && animator != null)
        {
            foreach (var p in speedParameters)
            {
                if (HasParameter(animator, p))
                    animator.SetFloat(p, 0f);
            }
        }

        if (!locked)
        {
            // Clear latched input. If she was running when she hit the pickup and
            // let go of the key while reading, no input callback fires (the action
            // is disabled), so the old move vector is still set when control comes
            // back and she sprints off on her own. SendMessage keeps this file free
            // of any hard reference to Starter Assets.
            gameObject.SendMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
            gameObject.SendMessage("LookInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
            gameObject.SendMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);
            gameObject.SendMessage("JumpInput", false, SendMessageOptions.DontRequireReceiver);
        }

        Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = locked;
    }

    private static bool HasParameter(Animator anim, string paramName)
    {
        foreach (var p in anim.parameters)
            if (p.name == paramName) return true;
        return false;
    }
}
