using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Wajib ditambahin buat ngontrol teks TextMeshPro
using System.Collections;

public class SequenceManager : MonoBehaviour
{
    [Header("Komponen UI & Karakter")]
    public UIPopup scriptPopup; 
    public Transform karakterUtama; 
    public Transform titikPintu; 
    public Animator animKarakter; 
    
    [Header("Pengaturan Sutradara")]
    public float waktuSimulasiKalibrasi = 3f; 
    public float kecepatanJalan = 2.5f;
    public string namaSceneLevel1 = "Level1"; 

    [Header("Pengaturan Loading Screen")]
    public Image layarHitamFader; 
    public float kecepatanFade = 2f;
    
    [Header("Pengaturan Teks Loading Dinamis")]
    public TextMeshProUGUI teksLoading; 
    [TextArea(2, 3)] // Biar kolom input di Unity lebih lebar
    public string[] daftarTipsLoading = {
        "One grip, one pulse of life.",
        "Focus on your grip, not the speed of your progress.",
        "The Pulse Blade responds only to true determination.",
        "Every small effort is a step toward recovery."
    };

    // Fungsi ini yang akan dicolok ke tombol "Start Journey"
    public void MulaiAdeganMasukDungeon()
    {
        StartCoroutine(JalankanSequence());
    }

    private IEnumerator JalankanSequence()
    {
        // Adegan 1: Munculkan pop-up kalibrasi (pakai efek fade-in)
        scriptPopup.Show();

        // Adegan 2: Tunggu beberapa detik (Pura-puranya pasien lagi ngeremas tangan)
        yield return new WaitForSeconds(waktuSimulasiKalibrasi);

        // Adegan 3: Tutup pop-up kalibrasi
        scriptPopup.Hide();

        // Adegan 4: Tunggu sebentar biar efek fade-out UI nya selesai (0.5 detik)
        yield return new WaitForSeconds(0.5f);

        // Adegan 5: Karakter mulai jalan!
        if (animKarakter != null) 
        {
            animKarakter.SetFloat("MoveX", 1f); 
            animKarakter.SetFloat("MoveY", 0f);
            animKarakter.SetFloat("Speed", 1f); 
        }

        while (Vector3.Distance(karakterUtama.position, titikPintu.position) > 0.05f)
        {
            karakterUtama.position = Vector3.MoveTowards(karakterUtama.position, titikPintu.position, kecepatanJalan * Time.deltaTime);
            yield return null; 
        }

        // Adegan 6: Karakter sampai di pintu, buat dia jadi diam (Idle)
        if (animKarakter != null) 
        {
            animKarakter.SetFloat("Speed", 0f); 
        }
        
        // --- BAGIAN FADE DENGAN CANVAS GROUP & TEKS DINAMIS ---
        
        // Pilih satu teks secara acak dari daftar
        if (teksLoading != null && daftarTipsLoading.Length > 0)
        {
            int indexAcak = Random.Range(0, daftarTipsLoading.Length);
            teksLoading.text = daftarTipsLoading[indexAcak];
        }

        // Kita butuh akses ke Canvas Group si layar hitam
        CanvasGroup cgFader = layarHitamFader.GetComponent<CanvasGroup>();
        
        // Jaga-jaga kalau kamu lupa nambahin komponen Canvas Group di Unity, script ini bakal nambahin otomatis
        if (cgFader == null)
        {
            cgFader = layarHitamFader.gameObject.AddComponent<CanvasGroup>();
        }

        // 1. Munculkan layar hitam & teks perlahan (Fade In)
        float progress = 0;
        while (progress < 1)
        {
            progress += Time.deltaTime * kecepatanFade;
            cgFader.alpha = progress; // Mengubah transparansi total (termasuk teks)
            yield return null;
        }

        // 2. Jeda sebentar biar mata pemain nyaman dan bisa baca tulisan Loading-nya
        yield return new WaitForSeconds(1.5f);

        // 3. Mulai muat level 1 di belakang layar (Async)
        AsyncOperation operasiLoading = SceneManager.LoadSceneAsync(namaSceneLevel1);
        operasiLoading.allowSceneActivation = false; // Tahan dulu, jangan langsung pindah

        // Tunggu sampai memori Unity siap (progress mencapai 0.9 atau 90%)
        while (operasiLoading.progress < 0.9f)
        {
            yield return null;
        }

        // 4. Eksekusi perpindahan Scene yang super mulus!
        operasiLoading.allowSceneActivation = true;
    }
}