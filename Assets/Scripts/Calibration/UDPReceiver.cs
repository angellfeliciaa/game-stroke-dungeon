using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

// --- Struktur Data JSON (Harus sama persis dengan yang dikirim Python) ---
[System.Serializable]
public class HandData
{
    public Landmark[] landmarks;
    public string prediction;
    public float confidence;
}

[System.Serializable]
public class Landmark
{
    public float x;
    public float y;
    public float z;
}
// --------------------------------------------------------------------------

public class UDPReceiver : MonoBehaviour
{
    [Header("UI Components (Colokan Inspector)")]
    public RawImage layarWebcam;
    public Image gripBarFill;
    public TextMeshProUGUI textStatus;

    [Header("Network Settings")]
    public int dataPort = 5052;
    public int videoPort = 5053;

    // ==========================================================
    // PENGATURAN KALIBRASI
    // ==========================================================

    [Header("Calibration")]

    // Berapa lama pemain harus mempertahankan grip
    // sebelum kalibrasi dianggap berhasil
    public float calibrationDuration = 3f;

    // Seberapa jauh nilai grip boleh berubah
    // dari nilai grip saat pertama kali calibration dimulai
    public float calibrationTolerance = 0.15f;

    // Nilai grip minimum supaya calibration bisa dimulai
    public float minimumGripLevel = 0.75f;

    // Toleransi kalau fist sempat tidak terdeteksi
    // karena ada sedikit noise dari computer vision
    public float fistLostTolerance = 0.75f;

    // Menyimpan progress calibration
    private float calibrationTimer = 0f;

    // Menyimpan nilai grip saat calibration pertama kali dimulai
    private float calibrationBaseline = 0f;

    public float CalibrationBaseline
    {
        get
        {
            return calibrationBaseline;
        }
    }

    private float currentGripLevel = 0f;

    public float CurrentGripLevel
    {
        get
        {
            return currentGripLevel;
        }
    }

    private string currentPrediction = "none";

    public string CurrentPrediction
    {
        get
        {
            return currentPrediction;
        }
    }

    public bool IsFist
    {
        get
        {
            return currentPrediction == "fist";
        }
    }

    // Menandakan apakah calibration sudah dimulai
    private bool calibrationStarted = false;

    // Menandakan apakah calibration sudah selesai
    private bool calibrationCompleted = false;

    // Menyimpan waktu saat calibration dimulai
    // supaya timer tidak bergantung pada jumlah data UDP
    private float calibrationStartTime = 0f;

    // Menghitung berapa lama fist tidak terdeteksi
    private float fistLostTimer = 0f;

    // ==========================================================
    // KOMPONEN JARINGAN & THREADING
    // ==========================================================

    private UdpClient dataClient;
    private UdpClient videoClient;

    private Thread dataThread;
    private Thread videoThread;

    // ==========================================================
    // BUFFER PENYIMPANAN DATA SEMENTARA
    // ==========================================================

    private byte[] latestVideoBytes;
    private bool hasNewVideo = false;

    private HandData latestHandData;
    private bool hasNewData = false;

    // ==========================================================
    // TEKSTUR UNTUK NAMPILIN VIDEO
    // ==========================================================

    private Texture2D webcamTexture;

    void Start()
    {
        // Membuat UDPReceiver tetap hidup
        // meskipun scene berganti
        DontDestroyOnLoad(gameObject);

        // Reset semua status calibration
        // setiap kali game dimulai
        calibrationTimer = 0f;
        calibrationBaseline = 0f;
        calibrationStarted = false;
        calibrationCompleted = false;
        calibrationStartTime = 0f;
        fistLostTimer = 0f;

        // Siapkan kanvas kosong untuk video
        // ukurannya disamakan dengan video dari Python
        webcamTexture = new Texture2D(
            320,
            240,
            TextureFormat.RGB24,
            false
        );

        // Untuk sekarang preview webcam di Unity
        // belum kita proses.
        // Python tetap membuka dan menjalankan webcam.
        //
        // Nanti setelah gameplay sudah berjalan,
        // bagian preview ini bisa kita aktifkan kembali.
        if (layarWebcam != null)
        {
            layarWebcam.texture = webcamTexture;
        }

        // Tampilkan status awal
        if (textStatus != null)
        {
            textStatus.text =
                "Status: Menghubungkan ke Kamera...";

            textStatus.color = Color.yellow;
        }

        // Grip bar dimulai dari kosong
        if (gripBarFill != null)
        {
            gripBarFill.fillAmount = 0f;
        }

        // Nyalakan antena penangkap data
        // di background thread
        StartUDPThreads();
    }

    private void StartUDPThreads()
    {
        // Thread khusus untuk menangkap data
        // dari Python berupa prediction, confidence,
        // dan hand landmarks
        dataThread = new Thread(ReceiveData);
        dataThread.IsBackground = true;
        dataThread.Start();

        // Thread khusus untuk menangkap gambar webcam
        // Untuk sekarang thread ini tetap aktif,
        // tetapi hasil video belum ditampilkan di Unity.
        videoThread = new Thread(ReceiveVideo);
        videoThread.IsBackground = true;
        videoThread.Start();
    }

    private void ReceiveData()
    {
        try
        {
            dataClient = new UdpClient(dataPort);

            IPEndPoint endPoint =
                new IPEndPoint(IPAddress.Any, dataPort);

            Debug.Log(
                $"=== DATA UDP LISTENING ON PORT {dataPort} ==="
            );

            while (true)
            {
                try
                {
                    // Tunggu data dari Python
                    byte[] receiveBytes =
                        dataClient.Receive(ref endPoint);

                    // Ubah byte menjadi teks JSON
                    string json =
                        Encoding.UTF8.GetString(receiveBytes);

                    // Terjemahkan JSON menjadi object C#
                    latestHandData =
                        JsonUtility.FromJson<HandData>(json);

                    // Tandai bahwa ada data baru
                    // yang harus diproses oleh Unity
                    hasNewData = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning(
                        "Error terima data: " + e.Message
                    );
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "DATA UDP GAGAL START: " +
                e.Message
            );
        }
    }

    private void ReceiveVideo()
    {
        try
        {
            videoClient = new UdpClient(videoPort);

            Debug.Log(
                $"=== VIDEO UDP LISTENING ON PORT {videoPort} ==="
            );

            IPEndPoint endPoint =
                new IPEndPoint(IPAddress.Any, videoPort);

            while (true)
            {
                try
                {
                    // Tunggu data gambar dari Python
                    byte[] receiveBytes =
                        videoClient.Receive(ref endPoint);

                    // Simpan gambar terbaru
                    latestVideoBytes = receiveBytes;

                    // Tandai bahwa ada video baru
                    hasNewVideo = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning(
                        "Error terima video: " + e.Message
                    );
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "VIDEO UDP GAGAL START: " +
                e.Message
            );
        }
    }

    void Update()
    {
        // ==========================================================
        // 1. UPDATE LAYAR WEBCAM
        // ==========================================================

        // Untuk sekarang bagian video sengaja tidak diproses.
        //
        // Python tetap membuka webcam dan tetap mengirim video
        // melalui UDP 5053, tetapi Unity tidak melakukan
        // LoadImage() terlebih dahulu.
        //
        // Kita lakukan ini supaya masalah preview webcam
        // tidak mengganggu gameplay dan calibration.
        //
        // Nanti setelah game logic sudah berjalan,
        // bagian ini bisa kita aktifkan kembali.

        /*
        if (hasNewVideo && latestVideoBytes != null)
        {
            Debug.Log(
                $"VIDEO RECEIVED: {latestVideoBytes.Length} bytes"
            );

            bool success = webcamTexture.LoadImage(
                latestVideoBytes
            );

            Debug.Log(
                $"VIDEO LOAD RESULT: {success}"
            );

            hasNewVideo = false;
        }
        */

        // ==========================================================
        // 2. UPDATE STATUS & GRIP BAR
        // ==========================================================

        if (hasNewData && latestHandData != null)
        {
            Debug.Log(
                $"Prediction: {currentPrediction} | " +
                $"Grip: {currentGripLevel:F2} | " +
                $"Strong: {IsGripStrongEnough()}"
            );
            // Simpan prediction terbaru dari Python
            currentPrediction = latestHandData.prediction;

            // Confidence fist digunakan sebagai grip level
            if (latestHandData.prediction == "fist")
            {
                currentGripLevel =
                    latestHandData.confidence;
            }
            else
            {
                currentGripLevel = 0f;
            }

            // Cek apakah ada tangan yang berhasil
            // dideteksi oleh MediaPipe
            if (
                latestHandData.landmarks != null &&
                latestHandData.landmarks.Length > 0
            )
            {
                // ==================================================
                // FIST / TANGAN MENGEPAL
                // ==================================================

                if (latestHandData.prediction == "fist")
                {
                    // Confidence dari Python digunakan
                    // sebagai grip level untuk sementara
                    float targetFill =
                        latestHandData.confidence;

                    // Smooth transition supaya grip bar
                    // tidak bergerak terlalu patah-patah
                    if (gripBarFill != null)
                    {
                        gripBarFill.fillAmount = Mathf.Lerp(
                            gripBarFill.fillAmount,
                            targetFill,
                            Time.deltaTime * 10f
                        );
                    }

                    // Karena fist terdeteksi lagi,
                    // reset timer kehilangan fist
                    fistLostTimer = 0f;

                    // Lanjutkan proses calibration
                    ProcessCalibration(targetFill);
                }

                // ==================================================
                // BUKAN FIST
                // ==================================================

                else
                {
                    // Kalau tangan bukan fist,
                    // grip bar turun secara perlahan
                    if (gripBarFill != null)
                    {
                        gripBarFill.fillAmount = Mathf.Lerp(
                            gripBarFill.fillAmount,
                            0f,
                            Time.deltaTime * 5f
                        );
                    }

                    // Kalau calibration belum selesai,
                    // jangan langsung reset.
                    // Bisa saja prediction salah sebentar
                    // karena noise dari computer vision.
                    if (!calibrationCompleted)
                    {
                        fistLostTimer += Time.deltaTime;

                        // Kalau fist benar-benar hilang
                        // terlalu lama, baru calibration di-reset
                        if (
                            fistLostTimer >=
                            fistLostTolerance
                        )
                        {
                            ResetCalibration();

                            if (textStatus != null)
                            {
                                textStatus.text =
                                    "Kepalkan tangan sekuat mungkin!";

                                textStatus.color =
                                    Color.yellow;
                            }

                            Debug.Log(
                                "Calibration reset: fist lost too long."
                            );
                        }
                    }
                }
            }

            // ======================================================
            // TIDAK ADA TANGAN
            // ======================================================

            else
            {
                currentPrediction = "none";
                currentGripLevel = 0f;
                if (textStatus != null)
                {
                    textStatus.text =
                        "Posisikan tangan di depan kamera.";

                    textStatus.color =
                        Color.yellow;
                }

                // Turunkan grip bar secara perlahan
                if (gripBarFill != null)
                {
                    gripBarFill.fillAmount = Mathf.Lerp(
                        gripBarFill.fillAmount,
                        0f,
                        Time.deltaTime * 5f
                    );
                }

                // Jangan langsung reset calibration
                // hanya karena satu-dua frame kehilangan tangan
                if (!calibrationCompleted)
                {
                    fistLostTimer += Time.deltaTime;

                    if (
                        fistLostTimer >=
                        fistLostTolerance
                    )
                    {
                        ResetCalibration();

                        if (textStatus != null)
                        {
                            textStatus.text =
                                "Tangan tidak terdeteksi. Coba lagi.";

                            textStatus.color =
                                Color.yellow;
                        }

                        Debug.Log(
                            "Calibration reset: hand lost too long."
                        );
                    }
                }
            }

            // Data sudah selesai diproses
            hasNewData = false;
        }
    }

    // ==========================================================
    // PROSES KALIBRASI GRIP
    // ==========================================================

    private void ProcessCalibration(float gripLevel)
    {
        // Kalau calibration sudah selesai,
        // tidak perlu melakukan proses lagi
        if (calibrationCompleted)
        {
            if (textStatus != null)
            {
                textStatus.text =
                    "Kalibrasi berhasil!";

                textStatus.color =
                    Color.green;
            }

            return;
        }

        // ==========================================================
        // TAHAP 1
        // MENUNGGU GRIP YANG CUKUP KUAT
        // ==========================================================

        if (!calibrationStarted)
        {
            // Kalau grip masih di bawah minimum,
            // pemain harus mengepalkan tangan lebih kuat
            if (gripLevel < minimumGripLevel)
            {
                if (textStatus != null)
                {
                    textStatus.text =
                        "Kepalkan tangan lebih kuat!";

                    textStatus.color =
                        Color.yellow;
                }

                calibrationTimer = 0f;
                calibrationBaseline = 0f;

                return;
            }

            // Grip sudah cukup kuat
            // → calibration dimulai
            calibrationStarted = true;

            calibrationTimer = 0f;

            // Simpan grip pertama sebagai baseline
            calibrationBaseline =
                gripLevel;

            // Simpan waktu mulai calibration
            calibrationStartTime =
                Time.time;

            // Reset timer kehilangan fist
            fistLostTimer = 0f;

            if (textStatus != null)
            {
                textStatus.text =
                    "Pertahankan kepalan tangan...";

                textStatus.color =
                    Color.yellow;
            }

            Debug.Log(
                $"Calibration Started | " +
                $"Baseline Grip: {calibrationBaseline:F2}"
            );

            return;
        }

        // ==========================================================
        // TAHAP 2
        // CEK APAKAH GRIP MASIH STABIL
        // ==========================================================

        // Hitung selisih antara grip sekarang
        // dengan grip saat calibration dimulai
        float difference =
            Mathf.Abs(
                gripLevel -
                calibrationBaseline
            );

        // Kalau perubahan grip masih dalam batas tolerance,
        // berarti grip masih dianggap stabil
        if (difference <= calibrationTolerance)
        {
            // Hitung waktu berdasarkan waktu nyata
            // bukan berdasarkan jumlah data UDP
            calibrationTimer =
                Time.time -
                calibrationStartTime;

            if (textStatus != null)
            {
                textStatus.text =
                    $"Pertahankan kepalan tangan... " +
                    $"{calibrationTimer:F1}s / " +
                    $"{calibrationDuration:F1}s";

                textStatus.color =
                    Color.yellow;
            }

            // ======================================================
            // KALIBRASI BERHASIL
            // ======================================================

            if (
                calibrationTimer >=
                calibrationDuration
            )
            {
                calibrationCompleted =
                    true;

                if (textStatus != null)
                {
                    textStatus.text =
                        "Kalibrasi berhasil!";

                    textStatus.color =
                        Color.green;
                }

                Debug.Log(
                    "=== CALIBRATION SUCCESS ==="
                );
            }
        }

        // ==========================================================
        // GRIP BERUBAH TERLALU JAUH
        // ==========================================================

        else
        {
            // Kalau grip berubah terlalu jauh,
            // timer dimulai lagi dari awal
            calibrationTimer = 0f;

            calibrationStartTime =
                Time.time;

            if (textStatus != null)
            {
                textStatus.text =
                    "Pertahankan kepalan tangan.";

                textStatus.color =
                    Color.red;
            }

            Debug.Log(
                $"Calibration Timer Reset | " +
                $"Baseline: {calibrationBaseline:F2} | " +
                $"Current: {gripLevel:F2} | " +
                $"Difference: {difference:F2}"
            );
        }
    }

    // ==========================================================
    // RESET CALIBRATION
    // ==========================================================

    private void ResetCalibration()
    {
        // Kembalikan semua status calibration
        // ke kondisi awal
        calibrationStarted = false;

        calibrationTimer = 0f;

        calibrationBaseline = 0f;

        calibrationStartTime = 0f;

        fistLostTimer = 0f;
    }

    // ==========================================================
    // STATUS CALIBRATION
    // ==========================================================

    public bool IsGripStrongEnough()
    {
        if (!calibrationCompleted)
            return false;

        if (!IsFist)
            return false;

        return currentGripLevel >=
            calibrationBaseline - calibrationTolerance;
    }

    public bool IsCalibrationCompleted
    {
        get
        {
            return calibrationCompleted;
        }
    }

    public float CalibrationProgress
    {
        get
        {
            return Mathf.Clamp01(
                calibrationTimer /
                calibrationDuration
            );
        }
    }

    // ==========================================================
    // MATIKAN KONEKSI SAAT GAME DITUTUP
    // ==========================================================

    void OnApplicationQuit()
    {
        // Tutup koneksi UDP supaya thread
        // tidak tetap berjalan saat game ditutup
        if (dataClient != null)
        {
            dataClient.Close();
        }

        if (videoClient != null)
        {
            videoClient.Close();
        }

        if (dataThread != null)
        {
            dataThread.Abort();
        }

        if (videoThread != null)
        {
            videoThread.Abort();
        }
    }
}