"""Webcam hand classification and UDP input for Unity. No camera opens on import."""

import argparse
import json
import socket
import sys
import time
from pathlib import Path

import cv2
import joblib
import mediapipe as mp
import numpy as np

MODEL_PATH = Path(__file__).resolve().with_name("model_rehab.pkl")
MAX_DATAGRAM = 60000


def load_model(path=MODEL_PATH):
    model = joblib.load(path)
    if set(model.classes_) != {"fist", "palm"} or model.n_features_in_ != 63:
        raise ValueError("Expected a model with fist/palm labels and 63 landmark features.")
    return model


def extract_landmarks(result):
    """Keep the model's original wrist-relative 21 x 3 feature ordering."""
    if not result.multi_hand_landmarks:
        return None
    points = result.multi_hand_landmarks[0].landmark
    wrist = points[0]
    return np.array([
        value for p in points for value in (p.x - wrist.x, p.y - wrist.y, p.z - wrist.z)
    ], dtype=np.float32).reshape(1, 63)


def make_packet(prediction, confidence, result):
    points = result.multi_hand_landmarks[0].landmark if result.multi_hand_landmarks else []
    if not points:
        return {"prediction": "none", "confidence": 0.0, "landmarks": []}
    return {
        "prediction": str(prediction),
        "confidence": float(confidence),
        "landmarks": [{"x": float(p.x), "y": float(p.y), "z": float(p.z)} for p in points],
    }


def encode_preview(frame):
    """One complete JPEG per datagram, below the UDP size limit."""
    preview = cv2.resize(frame, (320, 240))
    for quality in (65, 40, 20):
        ok, encoded = cv2.imencode(".jpg", preview, [cv2.IMWRITE_JPEG_QUALITY, quality])
        if ok and encoded.nbytes <= MAX_DATAGRAM:
            return encoded.tobytes()
    return None


class UnitySender:
    def __init__(self, data_port=5052, video_port=5053):
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.data_target = ("127.0.0.1", data_port)
        self.video_target = ("127.0.0.1", video_port)

    def send(self, packet):
        self.socket.sendto(json.dumps(packet, allow_nan=False).encode("utf-8"), self.data_target)

    def preview(self, frame):
        encoded = encode_preview(frame)
        if encoded is not None:
            self.socket.sendto(encoded, self.video_target)

    def close(self):
        try:
            self.send({"prediction": "none", "confidence": 0.0, "landmarks": []})
        except OSError:
            pass
        finally:
            self.socket.close()


def self_test(model):
    # Smoke check only: synthetic features do not establish recognition accuracy.
    features = np.zeros((1, 63), dtype=np.float32)
    probabilities = model.predict_proba(features)[0]
    assert np.isfinite(probabilities).all() and np.isclose(probabilities.sum(), 1)
    with mp.solutions.hands.Hands(static_image_mode=True, max_num_hands=1) as hands:
        result = hands.process(np.zeros((240, 320, 3), dtype=np.uint8))
        assert extract_landmarks(result) is None
    assert encode_preview(np.zeros((240, 320, 3), dtype=np.uint8)) is not None
    print(f"SELF_TEST_OK model={type(model).__name__} classes={list(model.classes_)} features={model.n_features_in_}")


def run(args, model):
    sender = UnitySender(args.data_port, args.video_port)
    capture = None
    counter = 0
    stage = "waiting_palm"
    last_preview = 0.0
    try:
        capture = cv2.VideoCapture(args.camera)
        if not capture.isOpened():
            raise RuntimeError(f"Cannot open camera {args.camera}. Check camera permission or other camera apps.")
        with mp.solutions.hands.Hands(
            max_num_hands=1, min_detection_confidence=0.7, min_tracking_confidence=0.5
        ) as hands:
            print(f"CAMERA_READY data={args.data_port} video={args.video_port}", flush=True)
            frame_count = 0
            while True:
                ok, frame = capture.read()
                if not ok:
                    raise RuntimeError("Camera stopped producing frames.")
                frame = cv2.flip(frame, 1)
                result = hands.process(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
                features = extract_landmarks(result)
                prediction, confidence = "none", 0.0
                if features is not None:
                    probabilities = model.predict_proba(features)[0]
                    index = int(np.argmax(probabilities))
                    prediction, confidence = str(model.classes_[index]), float(probabilities[index])
                sender.send(make_packet(prediction, confidence, result))

                # A repetition requires PALM -> FIST -> PALM with confident frames.
                if confidence < 0.75 or prediction == "none":
                    stage = "waiting_palm"
                elif prediction == "palm":
                    if stage == "fist":
                        counter += 1
                    stage = "palm"
                elif prediction == "fist" and stage == "palm":
                    stage = "fist"

                if result.multi_hand_landmarks:
                    mp.solutions.drawing_utils.draw_landmarks(
                        frame, result.multi_hand_landmarks[0], mp.solutions.hands.HAND_CONNECTIONS
                    )
                cv2.putText(frame, f"{prediction.upper()}  {confidence:.0%}  REPS: {counter}",
                            (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 128), 2)
                now = time.monotonic()
                if now - last_preview >= 1 / 15:
                    try:
                        sender.preview(frame)
                    except OSError:
                        pass  # Preview is optional; hand data remains independent.
                    last_preview = now
                if not args.no_window:
                    cv2.imshow("Stroke Rehab - Q: quit, R: reset", frame)
                    key = cv2.waitKey(1) & 0xFF
                    if key == ord("q"):
                        break
                    if key == ord("r"):
                        counter, stage = 0, "waiting_palm"
                frame_count += 1
                if args.max_frames and frame_count >= args.max_frames:
                    break
    finally:
        if capture is not None:
            capture.release()
        sender.close()
        cv2.destroyAllWindows()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--data-port", type=int, default=5052)
    parser.add_argument("--video-port", type=int, default=5053)
    parser.add_argument("--no-window", action="store_true", help="Preview appears inside Unity only")
    parser.add_argument("--self-test", action="store_true", help="Test model and dependencies without opening a camera")
    parser.add_argument("--max-frames", type=int, default=0, help="Stop after this many frames; 0 runs until closed")
    args = parser.parse_args()
    if not (1 <= args.data_port <= 65535 and 1 <= args.video_port <= 65535) or args.data_port == args.video_port:
        parser.error("Use two distinct ports between 1 and 65535.")
    try:
        model = load_model()
        if args.self_test:
            self_test(model)
        else:
            run(args, model)
        return 0
    except KeyboardInterrupt:
        return 0
    except Exception as error:
        print(f"[AI ERROR] {error}", file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    sys.exit(main())
