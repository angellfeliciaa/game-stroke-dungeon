#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Wires the selected CC0 clips into the open Level1 scene.</summary>
public static class Level1AudioBuilder
{
    [MenuItem("Tools/Stroke Dungeon/Build Level 1 Audio")]
    public static void Build()
    {
        Level1Manager manager = Object.FindAnyObjectByType<Level1Manager>();
        if (manager == null || manager.gameObject.scene.name != "Level1")
        {
            Debug.LogError("Open the Level1 scene before building its audio.");
            return;
        }

        Transform existing = manager.transform.Find("Audio_Level1");
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("Audio_Level1 already exists; no duplicate was created.");
            return;
        }

        string root = "Assets/Audio/Level1/";
        Dictionary<string, string> paths = new Dictionary<string, string>
        {
            { "dungeonMusic", root + "Music/DungeonAmbience.ogg" },
            { "playerAttack", root + "SFX/PlayerAttack.ogg" },
            { "enemyAttack", root + "SFX/EnemyAttack.ogg" },
            { "enemyHit", root + "SFX/EnemyHit.ogg" },
            { "enemyDefeat", root + "SFX/EnemyDefeat.ogg" },
            { "doorOpen", root + "SFX/DoorOpen.ogg" },
            { "footstepA", root + "SFX/FootstepA.ogg" },
            { "footstepB", root + "SFX/FootstepB.ogg" },
            { "restCrystal", root + "SFX/RestCrystal.ogg" },
            { "levelComplete", root + "SFX/LevelComplete.ogg" },
            { "uiClick", "Assets/Audio/UI/Click.mp3" }
        };
        Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        foreach (KeyValuePair<string, string> entry in paths)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(entry.Value);
            if (clip == null)
            {
                Debug.LogError("Missing audio clip: " + entry.Value);
                return;
            }
            clips.Add(entry.Key, clip);
        }

        GameObject audioRoot = new GameObject("Audio_Level1");
        audioRoot.transform.SetParent(manager.transform, false);
        Undo.RegisterCreatedObjectUndo(audioRoot, "Build Level 1 Audio");

        AudioSource music = Undo.AddComponent<AudioSource>(audioRoot);
        music.playOnAwake = false;
        music.loop = true;
        music.spatialBlend = 0f;
        AudioSource effects = Undo.AddComponent<AudioSource>(audioRoot);
        effects.playOnAwake = false;
        effects.loop = false;
        effects.spatialBlend = 0f;

        Level1Audio audio = Undo.AddComponent<Level1Audio>(audioRoot);
        SerializedObject serialized = new SerializedObject(audio);
        serialized.FindProperty("musicSource").objectReferenceValue = music;
        serialized.FindProperty("effectsSource").objectReferenceValue = effects;
        foreach (KeyValuePair<string, AudioClip> entry in clips)
            serialized.FindProperty(entry.Key).objectReferenceValue = entry.Value;
        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = audioRoot;
        Debug.Log("Level 1 audio is connected. Save the scene to keep it.");
    }
}
#endif
