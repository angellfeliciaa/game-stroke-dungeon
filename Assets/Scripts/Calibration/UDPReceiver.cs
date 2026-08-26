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

    // Komponen Jaringan & Threading
    private UdpClient dataClient;
    private UdpClient videoClient;
    private Thread dataThread;
    private Thread videoThread;

    // Buffer Penyimpanan Data Sementara
    private byte[] latestVideoBytes;
    private bool hasNewVideo = false;

    private HandData latestHandData;
    private bool hasNewData = false;

    // Tekstur untuk nampilin video
    private Texture2D webcamTexture;

    void Start()
    {
        // Siapkan kanvas kosong untuk video (ukurannya disamakan dengan kompresi Python 320x240)
        webcamTexture = new Texture2D(320, 240, TextureFormat.RGB24, false);
        layarWebcam.texture = webcamTexture;
        textStatus.text = "Status: Menghubungkan ke Kamera...";
        textStatus.color = Color.yellow;
        gripBarFill.fillAmount = 0f;

        // Nyalakan Antena Penangkap di latar belakang
        StartUDPThreads();
    }

    private void StartUDPThreads()
    {
        // Thread khusus untuk menangkap teks koordinat/AI
        dataThread = new Thread(ReceiveData);
        dataThread.IsBackground = true;
        dataThread.Start();

        // Thread khusus untuk menangkap gambar video
        videoThread = new Thread(ReceiveVideo);
        videoThread.IsBackground = true;
        videoThread.Start();
    }

    private void ReceiveData()
    {
        dataClient = new UdpClient(dataPort);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, dataPort);

        while (true)
        {
            try
            {
                byte[] receiveBytes = dataClient.Receive(ref endPoint);
                string json = Encoding.UTF8.GetString(receiveBytes);

                // Terjemahkan teks JSON jadi objek C#
                latestHandData = JsonUtility.FromJson<HandData>(json);
                hasNewData = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error terima data: " + e.Message);
            }
        }
    }

    private void ReceiveVideo()
    {
        videoClient = new UdpClient(videoPort);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, videoPort);

        while (true)
        {
            try
            {
                byte[] receiveBytes = videoClient.Receive(ref endPoint);
                latestVideoBytes = receiveBytes;
                hasNewVideo = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error terima video: " + e.Message);
            }
        }
    }

    void Update()
    {
        // --- BAGIAN INI BERJALAN DI MAIN THREAD UNITY UNTUK UPDATE UI ---

        // 1. Update Layar Webcam
        if (hasNewVideo && latestVideoBytes != null)
        {
            // Ubah susunan byte jadi gambar dan tempel ke layar
            webcamTexture.LoadImage(latestVideoBytes);
            hasNewVideo = false; // Reset status biar nggak update gambar yang sama
        }

        // 2. Update Status & Grip Bar
        if (hasNewData && latestHandData != null)
        {
            // Cek apakah tangan terdeteksi (landmarks ada isinya)
            if (latestHandData.landmarks != null && latestHandData.landmarks.Length > 0)
            {
                textStatus.text = "Status: Tangan Terdeteksi!";
                textStatus.color = Color.green;

                // Logika Grip Bar: Jika AI nebak 'fist' (mengepal), isi bar sesuai keyakinannya (confidence)
                if (latestHandData.prediction == "fist")
                {
                    // Smooth transition biar pergerakan bar-nya nggak kaku
                    float targetFill = latestHandData.confidence; 
                    gripBarFill.fillAmount = Mathf.Lerp(gripBarFill.fillAmount, targetFill, Time.deltaTime * 10f);
                }
                else 
                {
                    // Kalau tangannya kebuka (palm) atau lainnya, bar-nya turun pelan-pelan
                    gripBarFill.fillAmount = Mathf.Lerp(gripBarFill.fillAmount, 0f, Time.deltaTime * 5f);
                }
            }
            else
            {
                textStatus.text = "Status: Menunggu tangan...";
                textStatus.color = Color.yellow;
                gripBarFill.fillAmount = Mathf.Lerp(gripBarFill.fillAmount, 0f, Time.deltaTime * 5f);
            }

            hasNewData = false;
        }
    }

    // PENTING: Matikan koneksi jaringan saat game ditutup biar Unity nggak error/hang
    void OnApplicationQuit()
    {
        if (dataClient != null) dataClient.Close();
        if (videoClient != null) videoClient.Close();
        
        if (dataThread != null) dataThread.Abort();
        if (videoThread != null) videoThread.Abort();
    }
}