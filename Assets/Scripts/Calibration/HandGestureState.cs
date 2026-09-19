using System;

[Serializable]
public class HandData
{
    public Landmark[] landmarks;
    public string prediction;
    public float confidence;
}

[Serializable]
public class Landmark
{
    public float x;
    public float y;
    public float z;
}

// Pure input state: timestamps are monotonic seconds, independent of Unity FPS.
public sealed class HandGestureState
{
    public double DataTimeout = 0.5;
    public double CalibrationDuration = 3;
    public float MinimumConfidence = 0.75f;
    public float CalibrationTolerance = 0.1f;

    public string Prediction { get; private set; }
    public float Confidence { get; private set; }
    public bool CalibrationCompleted { get; private set; }
    public float CalibrationBaseline { get; private set; }
    public double CalibrationProgressSeconds { get; private set; }
    public int InterruptionVersion { get; private set; }
    public bool Calibrating { get; private set; }
    private double lastPacket = double.NegativeInfinity;
    private double calibrationStart = double.NaN;

    public HandGestureState() { Prediction = "none"; }

    public bool HasFreshData(double now)
    {
        return now >= lastPacket && now - lastPacket <= DataTimeout;
    }

    public bool IsPalm(double now)
    {
        return HasFreshData(now) && Prediction == "palm" && Confidence >= MinimumConfidence;
    }

    public bool IsGripStrongEnough(double now)
    {
        return CalibrationCompleted && HasFreshData(now) && Prediction == "fist" &&
            Confidence >= Math.Max(MinimumConfidence, CalibrationBaseline - CalibrationTolerance);
    }

    public void BeginCalibration()
    {
        CalibrationCompleted = false;
        CalibrationBaseline = 0;
        Calibrating = true;
        ResetInput();
    }

    public void StopCalibration() { Calibrating = false; }

    public void ResetInput()
    {
        lastPacket = double.NegativeInfinity;
        ClearGesture();
    }

    private void ClearGesture()
    {
        Prediction = "none";
        Confidence = 0;
        InterruptionVersion++;
        ResetCalibrationAttempt();
    }

    private void ResetCalibrationAttempt()
    {
        calibrationStart = double.NaN;
        if (!CalibrationCompleted)
        {
            CalibrationProgressSeconds = 0;
            CalibrationBaseline = 0;
        }
    }

    public void Tick(double now)
    {
        if (!HasFreshData(now) && Prediction != "none") ClearGesture();
    }

    public static bool IsValid(HandData data)
    {
        if (data == null || data.landmarks == null || !Finite(data.confidence) ||
            data.confidence < 0 || data.confidence > 1) return false;
        if (data.prediction == "none") return data.landmarks.Length == 0;
        if (data.prediction != "palm" && data.prediction != "fist") return false;
        if (data.landmarks.Length != 21) return false;
        foreach (Landmark point in data.landmarks)
            if (point == null || !Finite(point.x) || !Finite(point.y) || !Finite(point.z)) return false;
        return true;
    }

    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    public void Accept(HandData data, double receivedAt)
    {
        if (!IsValid(data) || receivedAt < lastPacket)
        {
            ResetInput();
            return;
        }
        // A resumed sender must not complete a hold started before a disconnection.
        Tick(receivedAt);
        lastPacket = receivedAt;
        Prediction = data.prediction;
        Confidence = data.confidence;
        if (Prediction != "fist" || Confidence < MinimumConfidence ||
            (CalibrationCompleted && Confidence < CalibrationBaseline - CalibrationTolerance))
        {
            InterruptionVersion++;
            ResetCalibrationAttempt();
            return;
        }
        if (!Calibrating || CalibrationCompleted) return;
        if (double.IsNaN(calibrationStart) || Math.Abs(Confidence - CalibrationBaseline) > CalibrationTolerance)
        {
            calibrationStart = receivedAt;
            CalibrationBaseline = Confidence;
        }
        CalibrationProgressSeconds = receivedAt - calibrationStart;
        if (CalibrationProgressSeconds >= CalibrationDuration)
        {
            CalibrationCompleted = true;
            Calibrating = false;
        }
    }
}
