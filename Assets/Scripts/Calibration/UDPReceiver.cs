using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UDPReceiver : MonoBehaviour
{
    public static UDPReceiver Instance { get; private set; }

    [Header("UI Components")]
    public RawImage layarWebcam;
    public Image gripBarFill;
    public TextMeshProUGUI textStatus;
    [Header("Network Settings")]
    public int dataPort = 5052;
    public int videoPort = 5053;
    [Min(0.1f)] public float dataTimeout = 0.5f;
    [Header("Calibration (prediction confidence, not physical force)")]
    [Min(0.1f)] public float calibrationDuration = 3f;
    [Range(0, 1)] public float calibrationTolerance = 0.1f;
    [Range(0, 1)] public float minimumGripLevel = 0.75f;

    private readonly HandGestureState state = new HandGestureState();
    private readonly object bufferLock = new object();
    private readonly Queue<Packet> pendingData = new Queue<Packet>();
    private sealed class Packet { public string Json; public double ReceivedAt; }
    private UdpClient dataClient, videoClient;
    private Thread dataThread, videoThread;
    private volatile bool running;
    private byte[] pendingVideo;
    private double videoReceivedAt;
    private string networkError;
    private Texture2D webcamTexture;
    private string statusOverride;

    private static double Now { get { return (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency; } }
    public bool HasFreshData { get { return state.HasFreshData(Now); } }
    public bool IsPalm { get { return state.IsPalm(Now); } }
    public bool IsFist { get { return HasFreshData && state.Prediction == "fist"; } }
    public string CurrentPrediction { get { return HasFreshData ? state.Prediction : "none"; } }
    public float CurrentGripLevel { get { return IsFist ? state.Confidence : 0; } }
    public float CalibrationBaseline { get { return state.CalibrationBaseline; } }
    public bool IsCalibrationCompleted { get { return state.CalibrationCompleted; } }
    public float CalibrationProgress { get { return Mathf.Clamp01((float)(state.CalibrationProgressSeconds / state.CalibrationDuration)); } }
    public int InterruptionVersion { get { return state.InterruptionVersion; } }
    public bool IsListening { get { return running; } }
    public string ConnectionError { get { lock (bufferLock) return networkError; } }
    public bool IsGripStrongEnough() { return state.IsGripStrongEnough(Now); }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        state.DataTimeout = Mathf.Max(0.1f, dataTimeout);
        state.CalibrationDuration = Mathf.Max(0.1f, calibrationDuration);
        state.MinimumConfidence = minimumGripLevel;
        state.CalibrationTolerance = calibrationTolerance;
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
        if (Instance != this) return;
        try
        {
            // Bind synchronously so a port conflict is visible before launching Python.
            dataClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, dataPort));
            videoClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, videoPort));
            lock (bufferLock) networkError = null;
            running = true;
            dataThread = new Thread(ReceiveData) { IsBackground = true };
            videoThread = new Thread(ReceiveVideo) { IsBackground = true };
            dataThread.Start();
            videoThread.Start();
        }
        catch (Exception error)
        {
            lock (bufferLock) networkError = "AI connection could not start. Please restart the game.";
            Debug.LogError("UDP startup failed: " + error.Message);
            StopNetwork();
        }
    }

    private void ReceiveData()
    {
        IPEndPoint source = new IPEndPoint(IPAddress.Loopback, 0);
        try
        {
            while (running)
            {
                byte[] bytes = dataClient.Receive(ref source);
                var packet = new Packet { Json = Encoding.UTF8.GetString(bytes), ReceivedAt = Now };
                lock (bufferLock)
                {
                    // Preserve gesture transitions within a frame. Overflow invalidates the hold.
                    if (pendingData.Count >= 120)
                    {
                        pendingData.Clear();
                        pendingData.Enqueue(new Packet { Json = "", ReceivedAt = packet.ReceivedAt });
                    }
                    pendingData.Enqueue(packet);
                }
            }
        }
        catch (Exception error)
        {
            if (running) lock (bufferLock) networkError = "AI data connection stopped: " + error.Message;
        }
    }

    private void ReceiveVideo()
    {
        IPEndPoint source = new IPEndPoint(IPAddress.Loopback, 0);
        try
        {
            while (running)
            {
                byte[] bytes = videoClient.Receive(ref source);
                lock (bufferLock) { pendingVideo = bytes; videoReceivedAt = Now; }
            }
        }
        catch (Exception) { /* Preview failure must not break hand input. */ }
    }

    private void Update()
    {
        double now = Now;
        Packet[] packets;
        byte[] video;
        double videoTime;
        lock (bufferLock)
        {
            packets = pendingData.ToArray();
            pendingData.Clear();
            video = pendingVideo;
            pendingVideo = null;
            videoTime = videoReceivedAt;
        }
        foreach (Packet packet in packets)
        {
            if (now - packet.ReceivedAt > state.DataTimeout) { state.ResetInput(); continue; }
            try { state.Accept(JsonUtility.FromJson<HandData>(packet.Json), packet.ReceivedAt); }
            catch (ArgumentException) { state.ResetInput(); }
        }
        state.Tick(now);

        if (layarWebcam != null)
        {
            if (video != null && now - videoTime <= 1 && video.Length >= 4 && video[0] == 0xff && video[1] == 0xd8)
            {
                if (webcamTexture == null) webcamTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (webcamTexture.LoadImage(video)) layarWebcam.texture = webcamTexture;
            }
            if (now - videoTime > 1) layarWebcam.texture = null;
        }
        if (gripBarFill != null)
            gripBarFill.fillAmount = Mathf.Lerp(gripBarFill.fillAmount, CurrentGripLevel, Time.unscaledDeltaTime * 10);
        if (textStatus == null) return;
        textStatus.color = Color.yellow;
        if (statusOverride != null) textStatus.text = statusOverride;
        else if (!HasFreshData) textStatus.text = "Waiting for the camera connection...";
        else if (IsCalibrationCompleted)
        {
            textStatus.text = "Calibration complete!";
            textStatus.color = Color.green;
        }
        else if (state.CalibrationProgressSeconds > 0)
            textStatus.text = $"Hold your fist... {state.CalibrationProgressSeconds:F1}s / {calibrationDuration:F1}s";
        else textStatus.text = "Show your hand and hold a clear fist.";
    }

    public void BeginCalibration()
    {
        lock (bufferLock) pendingData.Clear();
        statusOverride = null;
        state.BeginCalibration();
    }

    public void ShowError(string message)
    {
        state.StopCalibration();
        state.ResetInput();
        statusOverride = message;
        if (textStatus != null) textStatus.text = message;
    }

    private void StopNetwork()
    {
        running = false;
        dataClient?.Close();
        videoClient?.Close();
        dataThread?.Join(500);
        videoThread?.Join(500);
        dataClient = null;
        videoClient = null;
        dataThread = null;
        videoThread = null;
        lock (bufferLock) { pendingData.Clear(); pendingVideo = null; }
        state.ResetInput();
    }

    private void OnDisable()
    {
        StopNetwork();
        if (Instance == this) Instance = null;
    }
    private void OnApplicationQuit() { StopNetwork(); }
    private void OnDestroy()
    {
        StopNetwork();
        if (webcamTexture != null) Destroy(webcamTexture);
        if (Instance == this) Instance = null;
    }
}
