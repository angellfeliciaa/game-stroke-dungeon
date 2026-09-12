using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[System.Serializable]
public class DialogData
{
    public string namaKarakter;
    public string kalimat;
    public bool gantiWajahKeGoblin; 
}

public class PrologManager : MonoBehaviour
{
    [Header("Komponen UI Dialog")]
    public GameObject dialogContainer;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogText;
    public Image faceImage; 
    public GameObject promptText;
    public Sprite fotoMC;
    public Sprite fotoGoblin;

    [Header("Komponen Loading Screen")]
    public CanvasGroup cgFader;
    public TextMeshProUGUI teksLoading;
    public string[] daftarTipsLoading;

    [Header("Aktor & Titik Navigasi")]
    public Transform karakterUtama;
    public Animator animKarakter;
    public Transform titikRuangBesar; 
    public Transform titikDepanChest;
    
    [Header("Objek Interaktif")]
    public Animator animChest; 
    public GameObject objekPedang; 

    [Header("Mekanik Pintu Masuk (Kiri)")]
    public Transform titikDepanPintuMasuk; 
    public Animator animPintuMasuk; 

    [Header("Mekanik Pintu Keluar (Atas)")]
    public Transform titikDepanPintuKeluar; 
    public Transform titikPintuKeluar; 
    public Animator animPintuKeluar; 
    
    [Header("UI Helper")]
    public GameObject uiSqueezePrompt; 

    [Header("Hand Tracking")]
    public UDPReceiver udpReceiver;
    
    [Header("Pengaturan")]
    public float kecepatanJalan = 2.5f;
    public float kecepatanKetik = 0.04f;
    public string namaSceneLevel1 = "Level1";

    [Header("Pengaturan Squeeze")]
    public float durasiSqueeze = 2f;

    private bool userKlikLanjut = false;
    private Vector3 posisiAwalPedang; 

    void Start()
    {
        // Cari UDPReceiver yang sudah berjalan dari scene sebelumnya
        if (udpReceiver == null)
        {
            udpReceiver = FindFirstObjectByType<UDPReceiver>();
        }

        if (udpReceiver == null)
        {
            Debug.LogError("UDPReceiver tidak ditemukan!");
        }
        else
        {
            Debug.Log("UDPReceiver berhasil ditemukan oleh PrologManager.");
        }

        dialogContainer.SetActive(false);
        promptText.SetActive(false);

        if (uiSqueezePrompt != null)
            uiSqueezePrompt.SetActive(false);

        if (objekPedang != null)
            objekPedang.SetActive(false);

        posisiAwalPedang = objekPedang.transform.position;

        StartCoroutine(AlurPrologUtama());
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            userKlikLanjut = true;
        }
    }

    IEnumerator AlurPrologUtama()
    {
        // --- ADEGAN 1: Layar Hitam Memudar ---
        cgFader.alpha = 1;
        float p = 1;
        while (p > 0)
        {
            p -= Time.deltaTime * 2f;
            cgFader.alpha = p;
            yield return null;
        }

        // --- ADEGAN 2: Jalan ke Pintu Masuk Sambil Monolog ---
        dialogContainer.SetActive(true);
        bool obrolanLorongSelesai = false;
        StartCoroutine(JalankanDialogLorong(() => { obrolanLorongSelesai = true; }));

        while (Vector3.Distance(karakterUtama.position, titikDepanPintuMasuk.position) > 0.05f || !obrolanLorongSelesai)
        {
            if (Vector3.Distance(karakterUtama.position, titikDepanPintuMasuk.position) > 0.05f)
            {
                SetAnimasiJalan(true, titikDepanPintuMasuk.position);
                karakterUtama.position = Vector3.MoveTowards(karakterUtama.position, titikDepanPintuMasuk.position, kecepatanJalan * Time.deltaTime);
            }
            else
            {
                SetAnimasiJalan(false, karakterUtama.position); 
            }
            yield return null;
        }
        SetAnimasiJalan(false, karakterUtama.position);

        // --- ADEGAN 3: Mekanik Buka Pintu Masuk ---
        yield return StartCoroutine(TungguPemainSqueeze(animPintuMasuk));
        
        // --- ADEGAN 4: Masuk ke Ruang Besar Menemui Goblin ---
        while (Vector3.Distance(karakterUtama.position, titikRuangBesar.position) > 0.05f)
        {
            SetAnimasiJalan(true, titikRuangBesar.position);
            karakterUtama.position = Vector3.MoveTowards(karakterUtama.position, titikRuangBesar.position, kecepatanJalan * Time.deltaTime);
            yield return null;
        }
        SetAnimasiJalan(false, karakterUtama.position);

        // --- ADEGAN 5: Percakapan dengan Goblin ---
        dialogContainer.SetActive(true);
        yield return StartCoroutine(PutarDialog("Green Goblin", "Hehehe! Look who finally crawled out of the dark! Welcome, human, to the Pulse Crucible!", true));
        yield return StartCoroutine(PutarDialog("Jason", "Who are you? And what is this place?", false));
        yield return StartCoroutine(PutarDialog("Green Goblin", "I am the keeper of this chamber. You seek the power to escape, yes? Behold! What you need is right inside that chest!", true));
        dialogContainer.SetActive(false);

        // --- ADEGAN 6: MC Jalan ke Depan Chest ---
        while (Vector3.Distance(karakterUtama.position, titikDepanChest.position) > 0.05f)
        {
            SetAnimasiJalan(true, titikDepanChest.position);
            karakterUtama.position = Vector3.MoveTowards(karakterUtama.position, titikDepanChest.position, kecepatanJalan * Time.deltaTime);
            yield return null;
        }
        SetAnimasiJalan(false, karakterUtama.position);

        // --- ADEGAN 7: Squeeze untuk Buka Chest ---

        yield return StartCoroutine(TungguPemainSqueeze(null));

        if (animChest != null)
            animChest.SetTrigger("Open");

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(AnimasiPedangLompat());

        // --- ADEGAN 8: Chest Terbuka & Pedang Lompat ---
        if (animChest != null) animChest.SetTrigger("Open"); 
        yield return new WaitForSeconds(0.5f); 
        
        yield return StartCoroutine(AnimasiPedangLompat());

        // --- ADEGAN 9: Dialog Terima Kasih ---
        dialogContainer.SetActive(true);
        yield return StartCoroutine(PutarDialog("Jason", "The Pulse Blade... Thank you, keeper. One grip at a time, I will reclaim my strength.", false));
        dialogContainer.SetActive(false);

        // --- ADEGAN 10: Jalan ke Pintu Keluar (Atas) ---
        while (Vector3.Distance(karakterUtama.position, titikDepanPintuKeluar.position) > 0.05f)
        {
            SetAnimasiJalan(true, titikDepanPintuKeluar.position);
            karakterUtama.position = Vector3.MoveTowards(karakterUtama.position, titikDepanPintuKeluar.position, kecepatanJalan * Time.deltaTime);
            yield return null;
        }
        SetAnimasiJalan(false, karakterUtama.position); 

        // --- ADEGAN 11: Mekanik Buka Pintu Keluar ---
        yield return StartCoroutine(TungguPemainSqueeze(animPintuKeluar));

        // --- ADEGAN 12: Lewati Pintu Keluar & Load Level 1 ---
        while (Vector3.Distance(karakterUtama.position, titikPintuKeluar.position) > 0.05f)
        {
            SetAnimasiJalan(true, titikPintuKeluar.position);
            karakterUtama.position = Vector3.MoveTowards(karakterUtama.position, titikPintuKeluar.position, kecepatanJalan * Time.deltaTime);
            yield return null;
        }
        SetAnimasiJalan(false, karakterUtama.position);

        // Transisi Loading Screen
        if (teksLoading != null && daftarTipsLoading.Length > 0)
        {
            teksLoading.text = daftarTipsLoading[Random.Range(0, daftarTipsLoading.Length)];
        }

        float progress = 0;
        while (progress < 1)
        {
            progress += Time.deltaTime * 2f;
            cgFader.alpha = progress;
            yield return null;
        }

        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(namaSceneLevel1);
    }

    IEnumerator AnimasiPedangLompat()
    {
        objekPedang.SetActive(true);
        objekPedang.transform.position = posisiAwalPedang;

        Vector3 posisiAkhir = karakterUtama.position + new Vector3(0, 0.5f, 0); 
        float durasi = 0.6f; 
        float waktu = 0;

        while (waktu < durasi)
        {
            waktu += Time.deltaTime;
            float persen = waktu / durasi;
            Vector3 posisiSaatIni = Vector3.Lerp(posisiAwalPedang, posisiAkhir, persen);
            posisiSaatIni.y += Mathf.Sin(persen * Mathf.PI) * 1.5f; 
            objekPedang.transform.position = posisiSaatIni;
            yield return null;
        }

        objekPedang.SetActive(false);
    }

    IEnumerator TungguPemainSqueeze(Animator animPintuTarget)
    {
        if (uiSqueezePrompt != null)
            uiSqueezePrompt.SetActive(true);

        float waktuGrip = 0f;
        bool sudahMulaiGrip = false;

        while (waktuGrip < durasiSqueeze)
        {
            if (udpReceiver != null && udpReceiver.IsGripStrongEnough())
            {
                // Fist sedang aktif
                waktuGrip += Time.deltaTime;

                if (!sudahMulaiGrip)
                {
                    sudahMulaiGrip = true;

                    Debug.Log("[SQUEEZE] Mulai menahan fist...");
                }
            }
            else
            {
                // Fist dilepas sebelum 2 detik
                if (sudahMulaiGrip)
                {
                    Debug.Log("[SQUEEZE] Fist dilepas. Timer reset.");

                    sudahMulaiGrip = false;
                }

                waktuGrip = 0f;
            }

            yield return null;
        }

        // =========================
        // SQUEEZE BERHASIL
        // =========================

        Debug.Log(
            $"[SQUEEZE BERHASIL] " +
            $"Grip bertahan {durasiSqueeze:F2} detik | " +
            $"Grip Level: {udpReceiver.CurrentGripLevel:F2}"
        );

        if (uiSqueezePrompt != null)
            uiSqueezePrompt.SetActive(false);

        if (animPintuTarget != null)
        {
            animPintuTarget.SetTrigger("Open");
        }

        yield return new WaitForSeconds(1.0f);
    }

    IEnumerator JalankanDialogLorong(System.Action penandaSelesai)
    {
        yield return StartCoroutine(PutarDialog("Jason", "This corridor feels endless... My body is so heavy, like my strength was completely drained.", false));
        yield return StartCoroutine(PutarDialog("Jason", "But I can feel a strange vibration deeper inside. I must keep moving forward.", false));
        
        dialogContainer.SetActive(false);
        penandaSelesai?.Invoke(); 
    }

    IEnumerator PutarDialog(string nama, string kalimat, bool isGoblin)
    {
        nameText.text = nama;
        faceImage.sprite = isGoblin ? fotoGoblin : fotoMC;
        
        userKlikLanjut = false;
        promptText.SetActive(false);
        dialogText.text = "";

        foreach (char huruf in kalimat.ToCharArray())
        {
            dialogText.text += huruf;
            if (userKlikLanjut) 
            {
                dialogText.text = kalimat;
                break;
            }
            yield return new WaitForSeconds(kecepatanKetik);
        }

        promptText.SetActive(true);
        userKlikLanjut = false;

        while (!userKlikLanjut)
        {
            yield return null;
        }
        userKlikLanjut = false;
    }

    // --- FUNGSI ANIMASI JALAN YANG SUDAH DI-UPDATE ---
    void SetAnimasiJalan(bool isWalking, Vector3 targetPosition)
    {
        if (animKarakter != null)
        {
            if (isWalking)
            {
                // Hitung arah gerak (tujuan dikurangi posisi saat ini)
                Vector3 arah = (targetPosition - karakterUtama.position).normalized;

                // Cek apakah karakternya gerak vertikal atau horizontal
                if (Mathf.Abs(arah.x) > Mathf.Abs(arah.y))
                {
                    // Gerak Kiri / Kanan
                    animKarakter.SetFloat("MoveX", arah.x > 0 ? 1f : -1f);
                    animKarakter.SetFloat("MoveY", 0f);
                }
                else
                {
                    // Gerak Atas / Bawah
                    animKarakter.SetFloat("MoveX", 0f);
                    animKarakter.SetFloat("MoveY", arah.y > 0 ? 1f : -1f);
                }
                
                animKarakter.SetFloat("Speed", 1f);
            }
            else
            {
                animKarakter.SetFloat("Speed", 0f);
            }
        }
    }
}