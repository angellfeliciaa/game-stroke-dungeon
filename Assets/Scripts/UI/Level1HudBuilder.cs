#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates editable uGUI objects in the open Level1 scene.</summary>
public static class Level1HudBuilder
{
    private static readonly Color32 Cream = new Color32(247, 222, 197, 255);
    private static readonly Color32 Burgundy = new Color32(43, 13, 17, 247);
    private static readonly Color32 Track = new Color32(94, 50, 61, 255);
    private static readonly Color32 Teal = new Color32(92, 219, 199, 255);
    private static readonly Color32 Red = new Color32(222, 89, 104, 255);

    [MenuItem("Tools/Stroke Dungeon/Build Level 1 HUD")]
    public static void Build()
    {
        Level1Manager manager = Object.FindAnyObjectByType<Level1Manager>();
        if (manager == null || manager.gameObject.scene.name != "Level1")
        {
            Debug.LogError("Open the Level1 scene before building its HUD.");
            return;
        }

        Canvas canvas = manager.panelDialog != null
            ? manager.panelDialog.GetComponentInParent<Canvas>()
            : Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas was found in Level1.");
            return;
        }

        Transform existing = canvas.transform.Find("HUD_Level1");
        if (existing != null)
        {
            RectTransform existingPrompt = existing.Find("SqueezePrompt") as RectTransform;
            if (existingPrompt != null)
            {
                Undo.RecordObject(existingPrompt, "Place squeeze prompt");
                PlaceSqueezePrompt(existingPrompt);
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("HUD_Level1 already exists; its prompt placement was refreshed.");
            return;
        }

        TMP_FontAsset font = manager.panelDialog != null
            ? manager.panelDialog.transform.Find("Name_Text")?.GetComponent<TMP_Text>()?.font
            : null;
        if (font == null)
            font = TMP_Settings.defaultFontAsset;

        RectTransform hudRoot = NewRect(canvas.transform, "HUD_Level1",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
        hudRoot.SetAsFirstSibling();
        Undo.RegisterCreatedObjectUndo(hudRoot.gameObject, "Build Level 1 HUD");

        RectTransform playerCard = Card(hudRoot, "PlayerHealth",
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, 1), new Vector2(28, -26), new Vector2(390, 108));
        Text(playerCard, "Label", "JOSHUA  /  HP", font, 29, Cream,
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, 1), new Vector2(20, -12), new Vector2(250, 34),
            TextAlignmentOptions.Left);
        TMP_Text playerValue = Text(playerCard, "Value", "5 / 5", font, 28, Cream,
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(1, 1), new Vector2(-18, -12), new Vector2(110, 34),
            TextAlignmentOptions.Right);
        Image playerFill = Bar(playerCard, "PlayerBar", Teal, 350);

        RectTransform enemyCard = Card(hudRoot, "EnemyHealth",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(430, 108));
        TMP_Text enemyName = Text(enemyCard, "Name", "SKELETON", font, 29, Cream,
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, 1), new Vector2(20, -12), new Vector2(285, 34),
            TextAlignmentOptions.Left);
        TMP_Text enemyValue = Text(enemyCard, "Value", "3 / 3", font, 28, Cream,
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(1, 1), new Vector2(-18, -12), new Vector2(110, 34),
            TextAlignmentOptions.Right);
        Image enemyFill = Bar(enemyCard, "EnemyBar", Red, 390);
        enemyCard.gameObject.SetActive(false);

        RectTransform prompt = Card(hudRoot, "SqueezePrompt",
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(1, 1), new Vector2(-28, -106), new Vector2(470, 64));
        TMP_Text promptText = Text(prompt, "Instruction", "SQUEEZE TO ATTACK", font, 32, Teal,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        prompt.gameObject.SetActive(false);

        Button pauseButton = Button(hudRoot, "PauseButton", "II  PAUSE", font,
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(1, 1), new Vector2(-28, -26), new Vector2(136, 62));

        RectTransform pausePanel = NewRect(canvas.transform, "PanelPauseLevel1",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(pausePanel.gameObject, "Build Level 1 pause panel");
        Image dimmer = pausePanel.gameObject.AddComponent<Image>();
        dimmer.color = new Color32(0, 0, 0, 205);
        dimmer.raycastTarget = true;
        RectTransform pauseCard = Card(pausePanel, "PauseCard",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(650, 340));
        Text(pauseCard, "Title", "PAUSED", font, 60, Cream,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(560, 80),
            TextAlignmentOptions.Center);
        Text(pauseCard, "Body", "Take a breath. Resume when you're ready.", font, 31, Cream,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(580, 65),
            TextAlignmentOptions.Center);
        Button resumeButton = Button(pauseCard, "ResumeButton", "RESUME", font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(250, 66));
        pausePanel.gameObject.SetActive(false);

        Level1Hud hud = manager.GetComponent<Level1Hud>();
        if (hud == null)
            hud = Undo.AddComponent<Level1Hud>(manager.gameObject);
        Undo.RecordObject(hud, "Connect Level 1 HUD");
        SerializedObject serialized = new SerializedObject(hud);
        Set(serialized, "manager", manager);
        Set(serialized, "hudRoot", hudRoot.gameObject);
        Set(serialized, "playerFill", playerFill);
        Set(serialized, "playerValue", playerValue);
        Set(serialized, "enemyBarRoot", enemyCard.gameObject);
        Set(serialized, "enemyFill", enemyFill);
        Set(serialized, "enemyName", enemyName);
        Set(serialized, "enemyValue", enemyValue);
        Set(serialized, "squeezePrompt", prompt.gameObject);
        Set(serialized, "squeezeText", promptText);
        Set(serialized, "pauseButton", pauseButton);
        Set(serialized, "pausePanel", pausePanel.gameObject);
        Set(serialized, "resumeButton", resumeButton);
        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = hudRoot.gameObject;
        Debug.Log("Level 1 HUD created. Save the scene to keep it.");
    }

    private static void Set(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void PlaceSqueezePrompt(RectTransform rect)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-28, -106);
        rect.sizeDelta = new Vector2(470, 64);
    }

    private static RectTransform NewRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image ImageRect(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        RectTransform rect = NewRect(parent, name, anchorMin, anchorMax, pivot, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Card(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        Image border = ImageRect(parent, name, Cream, anchorMin, anchorMax, pivot, position, size);
        Image inside = ImageRect(border.transform, "Background", Burgundy,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(-5, -5));
        inside.transform.SetAsFirstSibling();
        return border.rectTransform;
    }

    private static Image Bar(Transform parent, string name, Color fillColor, float width)
    {
        Image border = ImageRect(parent, name, Cream,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(20, 17), new Vector2(width, 34));
        ImageRect(border.transform, "Track", Track,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(-6, -6));
        Image fill = ImageRect(border.transform, "Fill", fillColor,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(-6, -6));
        // A sprite-less Image renders as a solid rect; Level1Hud changes its
        // right anchor to show the current HP fraction.
        return fill;
    }

    private static TMP_Text Text(Transform parent, string name, string value,
        TMP_FontAsset font, float size, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(parent, name, anchorMin, anchorMax, pivot, position, dimensions);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(Transform parent, string name, string label,
        TMP_FontAsset font, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        Image background = ImageRect(parent, name, Cream,
            anchorMin, anchorMax, pivot, position, size);
        background.raycastTarget = true;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Cream;
        colors.highlightedColor = Teal;
        colors.pressedColor = Red;
        colors.selectedColor = Cream;
        button.colors = colors;
        ImageRect(background.transform, "Inside", Burgundy,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(-5, -5));
        Text(background.transform, "Label", label, font, 30, Cream,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        return button;
    }
}
#endif
