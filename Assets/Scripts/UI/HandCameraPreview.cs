using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small, non-interactive view of the existing local AI camera stream.
// The receiver owns the texture; this UI only displays it while it is fresh.
public sealed class HandCameraPreview
{
    private readonly RectTransform card;
    private readonly RawImage videoImage;
    private readonly TMP_Text status;

    public HandCameraPreview(Transform parent, TMP_FontAsset font,
        Vector2 topRightOffset, int siblingIndex = -1)
    {
        Color cream = new Color32(247, 222, 197, 255);
        Color burgundy = new Color32(43, 13, 17, 247);
        Color teal = new Color32(92, 219, 199, 255);

        card = AddRect(parent, "HandCameraPreview", Vector2.one,
            Vector2.one, Vector2.one, topRightOffset, new Vector2(320, 290));
        if (siblingIndex >= 0)
            card.SetSiblingIndex(siblingIndex);
        Image border = card.gameObject.AddComponent<Image>();
        border.color = cream;
        border.raycastTarget = false;

        RectTransform background = AddRect(card, "Background",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-5, -5));
        Image inside = background.gameObject.AddComponent<Image>();
        inside.color = burgundy;
        inside.raycastTarget = false;

        if (font == null) font = TMP_Settings.defaultFontAsset;
        AddText(card, "Title", "HAND CAMERA", font, 23, cream,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0.5f, 1), new Vector2(0, -9), new Vector2(288, 32));

        RectTransform frame = AddRect(card, "Video",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0.5f, 1), new Vector2(0, -43), new Vector2(288, 216));
        Image frameBackground = frame.gameObject.AddComponent<Image>();
        frameBackground.color = new Color32(18, 8, 16, 255);
        frameBackground.raycastTarget = false;
        RectTransform imageRect = AddRect(frame, "Image", Vector2.zero,
            Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        videoImage = imageRect.gameObject.AddComponent<RawImage>();
        videoImage.raycastTarget = false;
        videoImage.enabled = false;

        status = AddText(card, "Status", "WAITING FOR VIDEO", font, 20, teal,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(288, 28));
    }

    public void SetVisible(bool visible)
    {
        if (card != null && card.gameObject.activeSelf != visible)
            card.gameObject.SetActive(visible);
    }

    public void Refresh(UDPReceiver receiver)
    {
        if (card == null || !card.gameObject.activeSelf)
            return;

        Texture preview = receiver != null ? receiver.PreviewTexture : null;
        videoImage.texture = preview;
        videoImage.enabled = preview != null;
        if (receiver == null || receiver.ConnectionError != null || !receiver.IsListening)
            status.text = "CAMERA OFFLINE";
        else if (preview == null)
            status.text = "WAITING FOR VIDEO";
        else if (!receiver.HasFreshData)
            status.text = "NO HAND DATA";
        else if (receiver.IsPalm)
            status.text = "OPEN HAND " + Mathf.RoundToInt(receiver.CurrentConfidence * 100) + "%";
        else if (receiver.IsGripStrongEnough())
            status.text = "FIST " + Mathf.RoundToInt(receiver.CurrentConfidence * 100) + "%";
        else if (receiver.CurrentPrediction == "fist")
            status.text = "FIST UNCLEAR";
        else
            status.text = "SHOW YOUR HAND";
    }

    private static RectTransform AddRect(Transform parent, string name,
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

    private static TMP_Text AddText(Transform parent, string name,
        string value, TMP_FontAsset font, float size, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 dimensions)
    {
        RectTransform rect = AddRect(parent, name, anchorMin, anchorMax,
            pivot, position, dimensions);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }
}
