using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Diagnostics;
using System.IO;

public class SequenceManager : MonoBehaviour
{
    [Header("Komponen UI & Karakter")]
    public UIPopup scriptPopup;
    public Transform karakterUtama;
    public Transform titikPintu;
    public Animator animKarakter;
    public UDPReceiver udpReceiver;
    [Header("Pengaturan Sutradara")]
    public float kecepatanJalan = 2.5f;
    public string namaSceneLevel1 = "Prologue";
    [Header("Pengaturan Loading Screen")]
    public Image layarHitamFader;
    public float kecepatanFade = 2f;
    public TextMeshProUGUI teksLoading;
    [TextArea(2, 3)] public string[] daftarTipsLoading;
    [Header("AI startup")]
    [Min(1)] public float connectionTimeout = 30f;
    [Min(1)] public float calibrationTimeout = 120f;
    public int cameraIndex = 0;

    private Process pythonProcess;
    private readonly object logLock = new object();
    private string lastPythonError;
    private string playerError;
    private bool sequenceRunning;
    private bool journeyStarted;
    private bool recovering;
    private double recoveryStarted;
    private bool ownsProcess;

    public void MulaiAdeganMasukDungeon()
    {
        if (sequenceRunning || journeyStarted) return;
        if (udpReceiver != null && UDPReceiver.Instance != udpReceiver) return;
        playerError = null;
        if (udpReceiver == null || !udpReceiver.IsListening || udpReceiver.ConnectionError != null)
        {
            Fail("The camera connection could not start. Please restart the game.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(namaSceneLevel1))
        {
            Fail("The next scene is unavailable.");
            return;
        }
        if (!StartPythonCV()) return;
        udpReceiver.BeginCalibration();
        sequenceRunning = true;
        StartCoroutine(JalankanSequence());
    }

    private bool StartPythonCV()
    {
        StopPythonCV();
        lock (logLock) lastPythonError = null;
        string root = Directory.GetParent(Application.dataPath).FullName;
        string aiFolder = Path.Combine(root, "AI");
        bool windows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
        string executable = Path.Combine(aiFolder, ".venv", windows ? "Scripts" : "bin", windows ? "python.exe" : "python");
        string script = Path.Combine(aiFolder, "main.py");
        if (!File.Exists(executable) || !File.Exists(script) || !File.Exists(Path.Combine(aiFolder, "model_rehab.pkl")))
        {
            UnityEngine.Debug.LogError($"AI files missing. Run AI/setup.ps1 on Windows or bash AI/setup.sh on macOS/Linux. Expected Python: {executable}");
            Fail("Camera input is not ready. Complete the AI setup, then try again.");
            return false;
        }
        try
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"-u \"{script}\" --no-window --camera {cameraIndex} --data-port {udpReceiver.dataPort} --video-port {udpReceiver.videoPort}",
                WorkingDirectory = aiFolder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data)) lock (logLock) lastPythonError = args.Data;
            };
            process.OutputDataReceived += (sender, args) => { /* Drain stdout to prevent a full pipe. */ };
            pythonProcess = process;
            ownsProcess = process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            return ownsProcess;
        }
        catch (System.Exception error)
        {
            UnityEngine.Debug.LogError("Python startup failed: " + error.Message);
            Fail("Camera input could not start. Check the AI setup and try again.");
            StopPythonCV();
            return false;
        }
    }

    private bool PythonRunning { get { return ownsProcess && pythonProcess != null && !pythonProcess.HasExited; } }

    private IEnumerator JalankanSequence()
    {
        scriptPopup.Show();
        double started = Time.realtimeSinceStartupAsDouble;
        while (!udpReceiver.HasFreshData)
        {
            if (!PythonRunning || Time.realtimeSinceStartupAsDouble - started > connectionTimeout || udpReceiver.ConnectionError != null)
            {
                Fail("The camera did not connect. Check camera access and try again.");
                StopPythonCV();
                yield break;
            }
            yield return null;
        }
        started = Time.realtimeSinceStartupAsDouble;
        while (!udpReceiver.IsCalibrationCompleted)
        {
            if (!PythonRunning || !udpReceiver.HasFreshData || udpReceiver.ConnectionError != null)
            {
                Fail("The camera connection was lost. Please try again.");
                StopPythonCV();
                yield break;
            }
            if (Time.realtimeSinceStartupAsDouble - started > calibrationTimeout)
            {
                Fail("Calibration timed out. Relax your hand, then try again.");
                StopPythonCV();
                yield break;
            }
            yield return null;
        }
        journeyStarted = true;
        scriptPopup.Hide();
        yield return new WaitForSeconds(0.5f);
        if (animKarakter != null)
        {
            animKarakter.SetFloat("MoveX", 1);
            animKarakter.SetFloat("MoveY", 0);
            animKarakter.SetFloat("Speed", 1);
        }
        while (Vector3.Distance(karakterUtama.position, titikPintu.position) > 0.05f)
        {
            karakterUtama.position = Vector3.MoveTowards(karakterUtama.position, titikPintu.position, kecepatanJalan * Time.deltaTime);
            yield return null;
        }
        if (animKarakter != null) animKarakter.SetFloat("Speed", 0);
        if (teksLoading != null && daftarTipsLoading != null && daftarTipsLoading.Length > 0)
            teksLoading.text = daftarTipsLoading[Random.Range(0, daftarTipsLoading.Length)];
        CanvasGroup fader = layarHitamFader.GetComponent<CanvasGroup>();
        if (fader == null) fader = layarHitamFader.gameObject.AddComponent<CanvasGroup>();
        for (float progress = 0; progress < 1; progress += Time.deltaTime * kecepatanFade)
        {
            fader.alpha = progress;
            yield return null;
        }
        fader.alpha = 1;
        yield return new WaitForSeconds(1.5f);
        yield return SceneManager.LoadSceneAsync(namaSceneLevel1);
        sequenceRunning = false;
    }

    private void Update()
    {
        // The receiver and this manager share a persistent root in SampleScene.
        if (!journeyStarted || playerError != null) return;
        if (recovering && PythonRunning && udpReceiver.HasFreshData) { recovering = false; return; }
        if (recovering && PythonRunning && Time.realtimeSinceStartupAsDouble - recoveryStarted < connectionTimeout) return;
        if (!PythonRunning || !udpReceiver.HasFreshData)
        {
            recovering = false;
            Fail("The camera connection was lost. Check the camera and try again.");
            StopPythonCV();
        }
    }

    private void Fail(string message)
    {
        sequenceRunning = false;
        playerError = message;
        if (udpReceiver != null) udpReceiver.ShowError(message);
        string details;
        lock (logLock) details = lastPythonError;
        UnityEngine.Debug.LogWarning(message + (details == null ? "" : "\nPython: " + details));
    }

    private void OnGUI()
    {
        if (playerError == null) return;
        GUILayout.BeginArea(new Rect((Screen.width - 440) / 2f, (Screen.height - 160) / 2f, 440, 160), GUI.skin.box);
        GUILayout.Label(playerError, new GUIStyle(GUI.skin.label) { wordWrap = true });
        if (GUILayout.Button("Try again", GUILayout.Height(40)))
        {
            playerError = null;
            if (!journeyStarted) MulaiAdeganMasukDungeon();
            else if (StartPythonCV())
            {
                recovering = true;
                recoveryStarted = Time.realtimeSinceStartupAsDouble;
            }
        }
        GUILayout.EndArea();
    }

    private void StopPythonCV()
    {
        if (pythonProcess == null) return;
        try
        {
            if (ownsProcess && !pythonProcess.HasExited)
            {
                pythonProcess.Kill();
                pythonProcess.WaitForExit(1000);
            }
        }
        catch (System.Exception error) { UnityEngine.Debug.LogWarning("Python shutdown: " + error.Message); }
        finally { pythonProcess.Dispose(); pythonProcess = null; ownsProcess = false; }
    }

    private void OnDisable() { StopPythonCV(); }
    private void OnDestroy() { StopPythonCV(); }
    private void OnApplicationQuit() { StopPythonCV(); }
}
