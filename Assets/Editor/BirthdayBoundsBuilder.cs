using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates and configures the WorldBounds safety net from what is actually in
/// the scene: the terrain gives the centre and radius, the water gives the
/// kill plane height.
///
/// Tools > Birthday > Setup World Bounds
/// Safe to re-run after you move the water or resize the island.
/// </summary>
public static class BirthdayBoundsBuilder
{
    private const string BoundsName = "WorldBounds";
    private const string RespawnName = "PlayerRespawn";

    [MenuItem("Tools/Birthday/Setup World Bounds")]
    public static void SetupBounds()
    {
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("No terrain",
                "There is no Terrain in the scene, so there is nothing to measure.", "OK");
            return;
        }

        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = terrain.terrainData.size;
        Vector3 centre = new(tPos.x + tSize.x * 0.5f, tPos.y, tPos.z + tSize.z * 0.5f);

        // --- the bounds object
        var go = GameObject.Find(BoundsName);
        if (go == null)
        {
            go = new GameObject(BoundsName);
            Undo.RegisterCreatedObjectUndo(go, "Create WorldBounds");
        }

        Undo.RecordObject(go.transform, "Position bounds");
        go.transform.position = centre;

        var bounds = go.GetComponent<WorldBounds>();
        if (bounds == null) bounds = Undo.AddComponent<WorldBounds>(go);

        Undo.RecordObject(bounds, "Configure bounds");

        // Radius: just past the island, so she can wade a little before rescue.
        bounds.maxRadius = Mathf.Max(tSize.x, tSize.z) * 0.55f;

        // Kill plane: below the water if there is any, otherwise below the land.
        var water = GameObject.Find("Water");
        if (water != null)
        {
            bounds.killPlaneY = water.transform.position.y - 3f;
            Debug.Log($"[Birthday] Water found at y={water.transform.position.y:0.##}, " +
                      $"kill plane set 3m below it.");
        }
        else
        {
            bounds.killPlaneY = tPos.y - 5f;
            Debug.LogWarning("[Birthday] No object named 'Water' found. Kill plane set from the terrain instead. " +
                             "Run Build Water, then run this again.");
        }

        // --- respawn point, at the player's current spot on the ground
        var respawn = go.transform.Find(RespawnName);
        if (respawn == null)
        {
            var rgo = new GameObject(RespawnName);
            Undo.RegisterCreatedObjectUndo(rgo, "Create respawn");
            rgo.transform.SetParent(go.transform, false);
            respawn = rgo.transform;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        Vector3 spawnAt = player != null ? player.transform.position : centre;

        // Drop it onto the terrain so she never respawns underground or in the air.
        float ground = terrain.SampleHeight(spawnAt) + tPos.y;
        respawn.position = new Vector3(spawnAt.x, ground + 0.5f, spawnAt.z);

        bounds.playerRespawn = respawn;
        EditorUtility.SetDirty(bounds);

        // --- keep the head's own kill plane in agreement
        var head = Object.FindFirstObjectByType<HeadPickup>();
        if (head != null)
        {
            Undo.RecordObject(head, "Sync head kill plane");
            head.killPlaneY = bounds.killPlaneY;
            EditorUtility.SetDirty(head);
        }

        Selection.activeGameObject = go;

        Debug.Log($"<b>[Birthday]</b> WorldBounds set up. Radius {bounds.maxRadius:0.#}m, " +
                  $"kill plane y={bounds.killPlaneY:0.##}, respawn at {respawn.position}. " +
                  $"Select it to see the boundary circle in the Scene view.");
    }
}
