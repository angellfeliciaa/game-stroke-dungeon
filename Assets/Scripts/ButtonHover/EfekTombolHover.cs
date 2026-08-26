using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // deteksi mouse
using TMPro; // buat ngedit TextMeshPro

public class EfekTombolHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Masukin Objek dari Hierarchy ke Sini:")]
    public Image latarTombol;
    public TMP_Text teksUtama;
    public TMP_Text teksBayangan;

    [Header("Warna Normal (Pas Diam):")]
    public Color warnaLatarNormal = new Color(1f, 1f, 1f, 0f); // Transparan
    public Color warnaTeksNormal = Color.white; 

    [Header("Warna Hover (Pas Disentuh):")]
    public Color warnaLatarHover = Color.white; // Kotak jadi putih
    public Color warnaTeksHover = new Color(0.2f, 0.2f, 0.2f, 1f); // Teks jadi abu-abu gelap

    void Start()
    {
        // Pas awal main, pastikan tombol dalam mode normal
        OnPointerExit(null);
    }

    // Fungsi ini jalan otomatis pas KURSOR MASUK ke tombol
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (latarTombol != null) latarTombol.color = warnaLatarHover;
        if (teksUtama != null) teksUtama.color = warnaTeksHover;
        if (teksBayangan != null) teksBayangan.enabled = false; // Bayangan dimatikan biar rapi
    }

    // Fungsi ini jalan otomatis pas KURSOR KELUAR dari tombol
    public void OnPointerExit(PointerEventData eventData)
    {
        if (latarTombol != null) latarTombol.color = warnaLatarNormal;
        if (teksUtama != null) teksUtama.color = warnaTeksNormal;
        if (teksBayangan != null) teksBayangan.enabled = true; // Bayangan nyala lagi
    }
}