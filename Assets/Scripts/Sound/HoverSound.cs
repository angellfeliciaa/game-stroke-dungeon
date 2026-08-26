using UnityEngine;
using UnityEngine.EventSystems; // Wajib untuk deteksi mouse

public class HoverSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    [Header("Pengaturan Suara")]
    public AudioSource sumberSuara;
    public AudioClip suaraHover;
    public AudioClip suaraKlik; // Variabel baru untuk menampung suara klik

    // Otomatis dipanggil saat kursor masuk (Hover)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (sumberSuara != null && suaraHover != null)
        {
            sumberSuara.PlayOneShot(suaraHover);
        }
    }

    // Otomatis dipanggil saat tombol mouse ditekan (Klik)
    public void OnPointerDown(PointerEventData eventData)
    {
        if (sumberSuara != null && suaraKlik != null)
        {
            sumberSuara.PlayOneShot(suaraKlik);
        }
    }
}