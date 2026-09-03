using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Finds the music and sound files, builds the MusicDirector, and connects every
/// place that needs to make a noise.
///
/// Tools > Birthday > Build Audio
/// </summary>
public static class BirthdayAudioBuilder
{
    private const string DirectorName = "MusicDirector";

    [MenuItem("Tools/Birthday/Build Audio")]
    public static void BuildAudio()
    {
        var start = Find("Music_StartScreen");
        var island = Find("Music_Island");
        var fin = Find("Music_Finale");
        var chime = Find("SFX_MemoryChime");

        if (start == null && island == null && fin == null && chime == null)
        {
            EditorUtility.DisplayDialog("No audio found",
                "Could not find Music_StartScreen, Music_Island, Music_Finale or SFX_MemoryChime.\n\n" +
                "Unzip the audio into Assets/Audio/ and run this again.", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Audio</b>\n");

        // ---- import settings, before anything references them
        int tuned = 0;
        tuned += Tune(start, streaming: true);
        tuned += Tune(island, streaming: true);
        tuned += Tune(fin, streaming: true);
        tuned += Tune(chime, streaming: false);
        sb.AppendLine($"  Import settings adjusted on {tuned} clip(s).");

        // ---- director
        var go = GameObject.Find(DirectorName);
        if (go == null)
        {
            go = new GameObject(DirectorName);
            Undo.RegisterCreatedObjectUndo(go, "Create music director");
            sb.AppendLine($"  Created '{DirectorName}'.");
        }

        var src = go.GetComponent<AudioSource>();
        if (src == null) src = Undo.AddComponent<AudioSource>(go);

        var director = go.GetComponent<MusicDirector>();
        if (director == null) director = Undo.AddComponent<MusicDirector>(go);

        Undo.RecordObject(director, "Wire audio");
        Undo.RecordObject(src, "Wire audio");

        director.startScreenTrack = start;
        director.islandTrack = island;
        director.finaleTrack = fin;

        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;

        sb.AppendLine($"  Start screen: {Name(start)}");
        sb.AppendLine($"  Island:       {Name(island)}");
        sb.AppendLine($"  Finale:       {Name(fin)}");

        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(src);

        // ---- the finale crossfade lives on FinaleGrowth
        var growth = Object.FindFirstObjectByType<FinaleGrowth>(FindObjectsInactive.Include);
        if (growth != null)
        {
            Undo.RecordObject(growth, "Wire audio");

            // FinaleGrowth does its own crossfade on an AudioSource. Point it at
            // the director's source so there is only ever one piece of music.
            growth.music = src;
            growth.finaleMusic = fin;

            EditorUtility.SetDirty(growth);
            sb.AppendLine("\n  FinaleGrowth will crossfade to the finale track when his head goes back on.");
        }
        else
        {
            sb.AppendLine("\n  No FinaleGrowth found, so the ending music is not wired.");
        }

        // ---- the chime on every orb
        if (chime != null)
        {
            var pickups = Object.FindObjectsByType<MemoryPickup>(FindObjectsInactive.Include,
                                                                FindObjectsSortMode.None);
            int set = 0;

            foreach (var p in pickups)
            {
                if (p.collectSound == chime) continue;

                Undo.RecordObject(p, "Wire chime");
                p.collectSound = chime;
                EditorUtility.SetDirty(p);
                set++;
            }

            sb.AppendLine($"  Collect chime assigned to {set} orb(s) " +
                          $"({pickups.Length - set} already had it).");
        }

        // ---- switch to the island track when she presses Play
        var menus = Object.FindFirstObjectByType<GameMenus>(FindObjectsInactive.Include);
        if (menus != null && menus.playButton != null && island != null)
        {
            bool already = false;
            var evt = menus.playButton.onClick;

            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                if (evt.GetPersistentTarget(i) == director &&
                    evt.GetPersistentMethodName(i) == "PlayIsland") already = true;

            if (!already)
            {
                Undo.RecordObject(menus.playButton, "Wire music");
                UnityEditor.Events.UnityEventTools.AddPersistentListener(evt, director.PlayIsland);
                EditorUtility.SetDirty(menus.playButton);
                sb.AppendLine("  Play button now also starts the island track.");
            }
            else
            {
                sb.AppendLine("  Play button was already wired to the island track.");
            }
        }
        else
        {
            sb.AppendLine("  Could not find the Play button, so the music will not change when she " +
                          "starts. Wire GameMenus > Play Button > MusicDirector.PlayIsland by hand.");
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        sb.AppendLine("\n  Volume is on the MusicDirector, default 0.42. Turn it down if it fights " +
                      "the speech bubble blips.");

        Debug.Log(sb.ToString());
        Selection.activeGameObject = go;
    }

    // -------------------------------------------------------------------

    /// <summary>
    /// Long music streams from disk instead of decompressing into memory, which
    /// matters a lot in WebGL. Short sounds load fully so there is no delay when
    /// they fire.
    /// </summary>
    private static int Tune(AudioClip clip, bool streaming)
    {
        if (clip == null) return 0;

        string path = AssetDatabase.GetAssetPath(clip);
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return 0;

        var s = importer.defaultSampleSettings;
        s.loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = streaming ? 0.55f : 0.8f;

        // preloadAudioData moved onto the per-platform sample settings in newer
        // Unity. The old importer-level property still exists but is deprecated.
        s.preloadAudioData = !streaming;

        importer.defaultSampleSettings = s;
        importer.forceToMono = true;
        importer.SaveAndReimport();

        return 1;
    }

    private static AudioClip Find(string name)
    {
        string guid = AssetDatabase.FindAssets($"{name} t:AudioClip").FirstOrDefault();
        return guid == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static string Name(AudioClip c) => c != null ? c.name : "NOT FOUND";
}
