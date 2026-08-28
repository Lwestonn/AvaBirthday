using UnityEngine;

/// <summary>
/// Catches anything that leaves the island and puts it back.
///
/// You need this the moment your world stops being an infinite plane. Without it,
/// walking off the edge means falling forever with no way back, which on her
/// birthday means alt-F4.
///
/// Put this on an empty GameObject at the centre of your island. It watches the
/// player, the head, and optionally everything with a Rigidbody.
/// </summary>
public class WorldBounds : MonoBehaviour
{
    [Header("Bounds")]
    [Tooltip("Anything below this Y gets rescued.")]
    public float killPlaneY = -12f;

    [Tooltip("Anything further than this from the centre gets rescued. 0 disables the radius check.")]
    public float maxRadius = 120f;

    [Header("Respawn")]
    [Tooltip("Where the player reappears. Leave empty to use this object's position.")]
    public Transform playerRespawn;

    [Tooltip("Small upward nudge so she does not respawn inside the ground.")]
    public float respawnHeightOffset = 1.5f;

    [Header("What to watch")]
    public bool watchPlayer = true;

    [Tooltip("The head has its own recovery, but this is a backstop if its kill plane is set wrong.")]
    public bool watchHead = true;

    [Tooltip("How often to check, in seconds. No need to do this every frame.")]
    public float checkInterval = 0.35f;

    [Header("Feedback")]
    [Tooltip("Line the head says when she gets fished out. Leave blank for silence.")]
    public string rescueLine = "Okay that's far enough, come back.";

    private CharacterController _controller;
    private Transform _player;
    private HeadPickup _head;
    private float _timer;

    private void Start()
    {
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
        {
            _player = playerGo.transform;
            _controller = playerGo.GetComponent<CharacterController>();
        }

        _head = FindFirstObjectByType<HeadPickup>();

        if (playerRespawn == null) playerRespawn = transform;
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = checkInterval;

        if (watchPlayer && _player != null && IsOutOfBounds(_player.position))
            RescuePlayer();

        if (watchHead && _head != null && !_head.IsHeld && IsOutOfBounds(_head.transform.position))
            _head.Recover();
    }

    private bool IsOutOfBounds(Vector3 p)
    {
        if (p.y < killPlaneY) return true;

        if (maxRadius > 0f)
        {
            Vector3 flat = p - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > maxRadius * maxRadius) return true;
        }

        return false;
    }

    private void RescuePlayer()
    {
        Vector3 target = playerRespawn.position + Vector3.up * respawnHeightOffset;

        // A CharacterController overrides transform writes while enabled, so it has
        // to be switched off for the teleport to actually stick.
        if (_controller != null)
        {
            _controller.enabled = false;
            _player.position = target;
            _controller.enabled = true;
        }
        else
        {
            _player.position = target;
        }

        if (!string.IsNullOrWhiteSpace(rescueLine))
        {
            var barks = FindFirstObjectByType<HeadBarks>();
            if (barks != null) barks.Say(rescueLine);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw the play area so you can size the island against it.
        Gizmos.color = new Color(1f, 0.5f, 0.6f, 0.6f);
        if (maxRadius > 0f)
        {
            const int steps = 64;
            Vector3 prev = transform.position + new Vector3(maxRadius, 0f, 0f);
            for (int i = 1; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(a) * maxRadius, 0f, Mathf.Sin(a) * maxRadius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
        Vector3 c = transform.position;
        c.y = killPlaneY;
        Gizmos.DrawWireCube(c, new Vector3(maxRadius * 2f, 0.1f, maxRadius * 2f));
    }
}
