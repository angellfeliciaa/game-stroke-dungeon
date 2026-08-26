using UnityEngine;
using System.Collections;

public class UIPopup : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    public float fadeDuration = 0.3f; // Durasi fade (0.3 detik)

    void Awake()
    {
        // Ambil komponen Canvas Group yang tadi kita pasang
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // Fungsi untuk memunculkan Panel (Fade In)
    public void Show()
    {
        gameObject.SetActive(true); // Nyalakan objeknya
        StartCoroutine(Fade(0, 1)); // Jalankan animasi dari 0 ke 1
    }

    // Fungsi untuk menyembunyikan Panel (Fade Out)
    public void Hide()
    {
        StartCoroutine(Fade(1, 0, true)); // Jalankan animasi dari 1 ke 0
    }

    private IEnumerator Fade(float start, float end, bool hideAfter = false)
    {
        float counter = 0f;

        while (counter < fadeDuration)
        {
            counter += Time.deltaTime;
            // Rumus matematika untuk mengubah Alpha perlahan
            canvasGroup.alpha = Mathf.Lerp(start, end, counter / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = end;

        // Jika ini adalah fungsi Hide, matikan objeknya setelah animasi selesai
        if (hideAfter) gameObject.SetActive(false);
    }
}