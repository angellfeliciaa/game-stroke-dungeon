using System;

public static class HandGestureStateTests
{
    private static int passed;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static HandData Packet(string label, float confidence)
    {
        var points = new Landmark[label == "none" ? 0 : 21];
        for (int i = 0; i < points.Length; i++) points[i] = new Landmark();
        return new HandData { prediction = label, confidence = confidence, landmarks = points };
    }
    private static HandGestureState Calibrated()
    {
        var state = new HandGestureState();
        state.BeginCalibration();
        for (int i = 0; i <= 30; i++) state.Accept(Packet("fist", 0.9f), i * 0.1);
        Check(state.CalibrationCompleted, "Three continuous seconds must calibrate.");
        return state;
    }
    private static void Test(string name, Action test)
    {
        test(); passed++; Console.WriteLine("PASS " + name);
    }
    public static int Main()
    {
        Test("calibration requires real input, not frame time", delegate {
            var s = new HandGestureState(); s.BeginCalibration(); s.Accept(Packet("fist", 0.9f), 0); s.Tick(4);
            Check(!s.CalibrationCompleted, "Silence must not complete calibration.");
        });
        Test("stable fist calibrates", delegate { Calibrated(); });
        Test("stale grip rejected even before next Unity Update", delegate {
            var s = Calibrated(); Check(s.IsGripStrongEnough(3.1), "Fresh grip should work.");
            Check(!s.IsGripStrongEnough(4), "Stale grip must not work.");
        });
        Test("reconnection cannot bridge a hold", delegate {
            var s = new HandGestureState(); s.BeginCalibration(); s.Accept(Packet("fist", 0.9f), 0);
            s.Accept(Packet("fist", 0.9f), 5);
            Check(!s.CalibrationCompleted && s.CalibrationProgressSeconds == 0, "Gap must restart hold.");
        });
        Test("low confidence interrupts calibration", delegate {
            var s = new HandGestureState(); s.BeginCalibration();
            s.Accept(Packet("fist", 0.9f), 0); s.Accept(Packet("fist", 0.2f), 0.1);
            Check(s.CalibrationProgressSeconds == 0 && s.CalibrationBaseline == 0, "Low confidence must reset.");
        });
        Test("confidence drift establishes a new baseline", delegate {
            var s = new HandGestureState(); s.CalibrationTolerance = 0.05f; s.BeginCalibration();
            s.Accept(Packet("fist", 0.98f), 0); s.Accept(Packet("fist", 0.8f), 0.1);
            Check(s.CalibrationBaseline == 0.8f && s.CalibrationProgressSeconds == 0, "Drift must restart baseline.");
        });
        Test("palm and no-hand reset holds", delegate {
            var s = Calibrated(); int version = s.InterruptionVersion;
            s.Accept(Packet("palm", 0.95f), 3.1); s.Accept(Packet("fist", 0.9f), 3.2);
            Check(s.InterruptionVersion > version, "A between-frame palm must interrupt the squeeze.");
            s.Accept(Packet("none", 0), 3.3); Check(!s.IsGripStrongEnough(3.3), "No hand must clear grip.");
        });
        Test("calibration survives disconnection but grip does not", delegate {
            var s = Calibrated(); s.Tick(5);
            Check(s.CalibrationCompleted && !s.IsGripStrongEnough(5), "Preserve calibration, clear input.");
            s.Accept(Packet("fist", 0.9f), 5.1); Check(s.IsGripStrongEnough(5.1), "Fresh data recovers grip.");
        });
        Test("invalid payloads fail closed", delegate {
            var s = Calibrated(); var bad = Packet("fist", float.NaN); s.Accept(bad, 3.1);
            Check(!s.IsGripStrongEnough(3.1), "NaN rejected.");
            bad = Packet("fist", 0.9f); bad.landmarks[0].x = float.PositiveInfinity;
            Check(!HandGestureState.IsValid(bad), "Nonfinite landmarks rejected.");
            bad = Packet("fist", 0.9f); bad.landmarks = new Landmark[1];
            Check(!HandGestureState.IsValid(bad), "Partial hand rejected.");
            Check(!HandGestureState.IsValid(Packet("unknown", 0.9f)), "Unknown label rejected.");
            Check(!HandGestureState.IsValid(Packet("fist", 2)), "Out of range confidence rejected.");
        });
        Test("retry resets previous calibration", delegate {
            var s = Calibrated(); s.BeginCalibration();
            Check(!s.CalibrationCompleted && !s.HasFreshData(4), "Retry must use new packets.");
        });
        Test("unsolicited packets do not start calibration", delegate {
            var s = new HandGestureState();
            for (int i = 0; i < 50; i++) s.Accept(Packet("fist", 0.9f), i * 0.1);
            Check(!s.CalibrationCompleted, "Explicit calibration start required.");
        });
        Console.WriteLine(passed + " gesture tests passed.");
        return 0;
    }
}
