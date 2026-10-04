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
    // Brief uncertain frames are normal while fingers move between poses.
    public double SqueezeArmGraceSeconds = 1.0;

    public string Prediction { get; private set; }
    public float Confidence { get; private set; }
    public bool CalibrationCompleted { get; private set; }
    public float CalibrationBaseline { get; private set; }
    public double CalibrationProgressSeconds { get; private set; }
    public int InterruptionVersion { get; private set; }
    // Increases once for each calibrated palm -> fist transition.
    // A version survives until gameplay observes it, including when both
    // packets arrive during the same Unity frame.
    public int SqueezeVersion { get; private set; }
    public bool Calibrating { get; private set; }
    private double lastPacket = double.NegativeInfinity;
    private double calibrationStart = double.NaN;
    private bool palmArmed;
    private int consecutivePalmFrames;
    private double lastPalmAt = double.NegativeInfinity;

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
            Confidence >= MinimumConfidence;
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
        palmArmed = false;
        consecutivePalmFrames = 0;
        lastPalmAt = double.NegativeInfinity;
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
        if (lastPacket != double.NegativeInfinity && !HasFreshData(now))
            ResetInput();
        else if (palmArmed && now - lastPalmAt > SqueezeArmGraceSeconds)
            palmArmed = false;
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
        if (CalibrationCompleted)
        {
            if (IsPalm(receivedAt))
            {
                consecutivePalmFrames = Math.Min(consecutivePalmFrames + 1, 2);
                lastPalmAt = receivedAt;
                if (consecutivePalmFrames >= 2) palmArmed = true;
            }
            else
            {
                consecutivePalmFrames = 0;
                if (receivedAt - lastPalmAt > SqueezeArmGraceSeconds)
                    palmArmed = false;
                if (IsGripStrongEnough(receivedAt))
                {
                    if (palmArmed) SqueezeVersion++;
                    palmArmed = false;
                    lastPalmAt = double.NegativeInfinity;
                }
            }
        }
        if (Prediction != "fist" || Confidence < MinimumConfidence)
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
