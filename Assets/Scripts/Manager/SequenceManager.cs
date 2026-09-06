using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Diagnostics;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SequenceManager : MonoBehaviour
{
    [Header("Komponen UI & Karakter")]
    public UIPopup scriptPopup;
    public Transform karakterUtama;
    public Transform titikPintu;
    public Animator animKarakter;

    public UDPReceiver udpReceiver;

    [Header("Pengaturan Sutradara")]
    public float waktuSimulasiKalibrasi = 3f;
    public float kecepatanJalan = 2.5f;
    public string namaSceneLevel1 = "Prologue";

    [Header("Pengaturan Loading Screen")]
    public Image layarHitamFader;
    public float kecepatanFade = 2f;

    [Header("Pengaturan Teks Loading Dinamis")]
    public TextMeshProUGUI teksLoading;

    [TextArea(2, 3)]
    public string[] daftarTipsLoading = {
        "One grip, one pulse of life.",
        "Focus on your grip, not the speed of your progress.",
        "The Pulse Blade responds only to true determination.",
        "Every small effort is a step toward recovery."
    };

    [Header("Python Computer Vision")]
    public string pythonPath;
    public string pythonScriptPath;

    private Process pythonProcess;

    // ==========================================================
    // MULAI PYTHON + SEQUENCE
    // ==========================================================

    public void MulaiAdeganMasukDungeon()
    {
        UnityEngine.Debug.Log("=== START JOURNEY CLICKED ===");

        // Jalankan Python computer vision
        StartPythonCV();

        // Mulai sequence game
        StartCoroutine(JalankanSequence());
    }

    // ==========================================================
    // JALANKAN PYTHON COMPUTER VISION
    // ==========================================================

   private void StartPythonCV()
    {
        if (
            pythonProcess != null &&
            !pythonProcess.HasExited
        )
        {
            UnityEngine.Debug.Log(
                "Python CV sudah berjalan."
            );

            return;
        }

        try
        {
            ProcessStartInfo startInfo =
                new ProcessStartInfo();

            startInfo.FileName =
                pythonPath;

            startInfo.Arguments =
                $"\"{pythonScriptPath}\"";

            startInfo.WorkingDirectory =
                Path.GetDirectoryName(
                    pythonScriptPath
                );

            startInfo.UseShellExecute =
                false;

            startInfo.CreateNoWindow =
                true;

            startInfo.RedirectStandardError =
                false;

            startInfo.RedirectStandardOutput =
                false;

            pythonProcess =
                new Process();

            pythonProcess.StartInfo =
                startInfo;

            pythonProcess.Start();

            UnityEngine.Debug.Log(
                "=== PYTHON CV STARTED ==="
            );

            UnityEngine.Debug.Log(
                $"Python: {pythonPath}"
            );

            UnityEngine.Debug.Log(
                $"Script: {pythonScriptPath}"
            );

            UnityEngine.Debug.Log(
                $"Working Directory: " +
                $"{startInfo.WorkingDirectory}"
            );
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError(
                "Gagal menjalankan Python CV: " +
                e.Message
            );
        }
    }
    // ==========================================================
    // STOP PYTHON COMPUTER VISION
    // ==========================================================

    private void StopPythonCV()
    {
        if (pythonProcess != null)
        {
            try
            {
                if (!pythonProcess.HasExited)
                {
                    pythonProcess.Kill();

                    pythonProcess.WaitForExit();
                }

                pythonProcess.Dispose();

                pythonProcess = null;

                UnityEngine.Debug.Log(
                    "=== PYTHON STOPPED ==="
                );
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"Gagal menghentikan Python: {e.Message}"
                );
            }
        }
    }

    // ==========================================================
    // SEQUENCE UTAMA
    // ==========================================================

    private IEnumerator JalankanSequence()
    {
        UnityEngine.Debug.Log(
            "=== SEQUENCE START ==="
        );

        // Beri sedikit waktu supaya
        // Python mulai membuka webcam
        yield return new WaitForSeconds(1f);

        UnityEngine.Debug.Log(
            "=== SHOW CALIBRATION POPUP ==="
        );

        // Tampilkan popup calibration
        scriptPopup.Show();

        // Pastikan UDP Receiver tersedia
        if (udpReceiver == null)
        {
            UnityEngine.Debug.LogError(
                "UDP Receiver belum dihubungkan!"
            );

            yield break;
        }

        UnityEngine.Debug.Log(
            "=== WAITING FOR CALIBRATION ==="
        );

        // Tunggu sampai pemain selesai calibration
        while (!udpReceiver.IsCalibrationCompleted)
        {
            yield return null;
        }

        UnityEngine.Debug.Log(
            "=== CALIBRATION COMPLETED ==="
        );

        // Sembunyikan popup calibration
        scriptPopup.Hide();

        yield return new WaitForSeconds(0.5f);

        // ======================================================
        // KARAKTER MULAI BERJALAN
        // ======================================================

        if (animKarakter != null)
        {
            animKarakter.SetFloat(
                "MoveX",
                1f
            );

            animKarakter.SetFloat(
                "MoveY",
                0f
            );

            animKarakter.SetFloat(
                "Speed",
                1f
            );
        }

        UnityEngine.Debug.Log(
            "=== CHARACTER START WALKING ==="
        );

        // Gerakkan karakter menuju pintu
        while (
            Vector3.Distance(
                karakterUtama.position,
                titikPintu.position
            ) > 0.05f
        )
        {
            karakterUtama.position =
                Vector3.MoveTowards(
                    karakterUtama.position,
                    titikPintu.position,
                    kecepatanJalan *
                    Time.deltaTime
                );

            yield return null;
        }

        // Hentikan animasi berjalan
        if (animKarakter != null)
        {
            animKarakter.SetFloat(
                "Speed",
                0f
            );
        }

        UnityEngine.Debug.Log(
            "=== CHARACTER REACHED DOOR ==="
        );

        // ======================================================
        // LOADING SCREEN
        // ======================================================

        if (
            teksLoading != null &&
            daftarTipsLoading.Length > 0
        )
        {
            int indexAcak =
                Random.Range(
                    0,
                    daftarTipsLoading.Length
                );

            teksLoading.text =
                daftarTipsLoading[indexAcak];
        }

        // Ambil CanvasGroup dari fader
        CanvasGroup cgFader =
            layarHitamFader.GetComponent<CanvasGroup>();

        // Kalau belum ada CanvasGroup,
        // buat otomatis
        if (cgFader == null)
        {
            cgFader =
                layarHitamFader.gameObject
                .AddComponent<CanvasGroup>();
        }

        // ======================================================
        // FADE OUT
        // ======================================================

        float progress = 0;

        while (progress < 1)
        {
            progress +=
                Time.deltaTime *
                kecepatanFade;

            cgFader.alpha =
                progress;

            yield return null;
        }

        yield return new WaitForSeconds(1.5f);

        // ======================================================
        // LOAD SCENE BERIKUTNYA
        // ======================================================

        UnityEngine.Debug.Log(
            $"=== LOADING SCENE: {namaSceneLevel1} ==="
        );

        AsyncOperation operasiLoading =
            SceneManager.LoadSceneAsync(
                namaSceneLevel1
            );

        operasiLoading.allowSceneActivation =
            false;

        while (
            operasiLoading.progress < 0.9f
        )
        {
            yield return null;
        }

        operasiLoading.allowSceneActivation =
            true;
    }

    // ==========================================================
    // UNITY EDITOR PLAY MODE
    // ==========================================================

#if UNITY_EDITOR

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
    #if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    #endif
    }

    private void OnPlayModeStateChanged(
        PlayModeStateChange state
    )
    {
        if (
            state ==
            PlayModeStateChange.ExitingPlayMode
        )
        {
            UnityEngine.Debug.Log(
                "=== PLAY MODE DIHENTIKAN ==="
            );

            StopPythonCV();
        }
    }

#endif

    // ==========================================================
    // SAAT APPLICATION DITUTUP
    // ==========================================================

    private void OnApplicationQuit()
    {
        StopPythonCV();
    }
}