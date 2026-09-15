"""
main.py — Real-time inference & gamification UI for stroke rehabilitation.
Detects PALM → FIST → PALM cycles and counts repetitions via webcam.
"""

import socket
import json
import cv2
import numpy as np
import mediapipe as mp
from typing import Optional
mp_hands_module = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils
mp_drawing_styles = mp.solutions.drawing_styles
import joblib

# ─── Load Model & MediaPipe ───────────────────────────────────────────────────
print("[Init] Loading model...")
model = joblib.load("model_rehab.pkl")

# ─── UDP → Unity ─────────────────────────────────────────────────────────────

UDP_IP = "127.0.0.1"
UDP_PORT = 5052

udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

print(f"[UDP] Sending AI data to {UDP_IP}:{UDP_PORT}")


hands = mp_hands_module.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.7,
    min_tracking_confidence=0.5,
)

# ─── Feature Extraction (MUST match train.py) ─────────────────────────────────

def extract_landmarks(image_rgb: np.ndarray, result) -> Optional[np.ndarray]:
    """
    Extract normalized landmarks from a MediaPipe result.
    Normalization: subtract wrist (landmark 0) x/y/z from all 21 landmarks.
    Returns 63-dim float32 array, or None if no hand detected.
    """
    if not result.multi_hand_landmarks:
        return None

    lm = result.multi_hand_landmarks[0].landmark
    wx, wy, wz = lm[0].x, lm[0].y, lm[0].z

    features = []
    for point in lm:
        features.extend([point.x - wx, point.y - wy, point.z - wz])

    return np.array(features, dtype=np.float32).reshape(1, -1)


# ─── UI Helpers ───────────────────────────────────────────────────────────────

def overlay_rect(frame: np.ndarray, x1: int, y1: int, x2: int, y2: int,
                 color: tuple, alpha: float = 0.5) -> np.ndarray:
    """Draw a semi-transparent filled rectangle on the frame."""
    overlay = frame.copy()
    cv2.rectangle(overlay, (x1, y1), (x2, y2), color, -1)
    return cv2.addWeighted(overlay, alpha, frame, 1 - alpha, 0)


def draw_balloon(frame: np.ndarray, cx: int, cy: int, radius: int, color: tuple):
    """Draw a simple balloon (circle + string)."""
    if radius < 5:
        return
    cv2.circle(frame, (cx, cy), radius, color, -1)
    cv2.circle(frame, (cx, cy), radius, (255, 255, 255), 2)
    # String
    cv2.line(frame, (cx, cy + radius), (cx, cy + radius + 40), (180, 180, 180), 2)


def send_to_unity(prediction, confidence, result):
    landmarks = []

    if result.multi_hand_landmarks:
        for point in result.multi_hand_landmarks[0].landmark:
            landmarks.append({
                "x": float(point.x),
                "y": float(point.y),
                "z": float(point.z)
            })

    data = {
        "prediction": prediction if prediction is not None else "none",
        "confidence": float(confidence),
        "landmarks": landmarks
    }

    message = json.dumps(data)

    udp_socket.sendto(
        message.encode("utf-8"),
        (UDP_IP, UDP_PORT)
    )

# ─── Main Loop ────────────────────────────────────────────────────────────────

def main():
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("[Error] Cannot open webcam.")
        return

    # State machine variables
    counter = 0
    stage = None          # "down" when fist detected, "up" after palm completes rep
    

    print("[Main] Webcam started. Press 'q' to quit, 'r' to reset counter.")

    while True:
        ret, frame = cap.read()
        if not ret:
            break

        # Mirror (flip horizontal) for natural feel
        frame = cv2.flip(frame, 1)
        h, w = frame.shape[:2]

        # Process with MediaPipe
        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        image_rgb.flags.writeable = False
        result = hands.process(image_rgb)
        image_rgb.flags.writeable = True

        prediction = None
        confidence = 0.0

        features = extract_landmarks(image_rgb, result)
        if features is not None:
            prediction = model.predict(features)[0]
            proba = model.predict_proba(features)[0]
            confidence = max(proba)

            # ── State Machine ──────────────────────────────────────────────
            if prediction == "fist":
                stage = "down"

            elif prediction == "palm" and stage == "down":
                stage = "up"
                counter += 1
                print(f"[Rep] Counter: {counter}")

         # Kirim hasil AI ke Unity
        send_to_unity(prediction, confidence, result)

        # ── Draw Hand Landmarks ────────────────────────────────────────────
        if result.multi_hand_landmarks:
            for hand_landmarks in result.multi_hand_landmarks:
                mp_drawing.draw_landmarks(
                    frame,
                    hand_landmarks,
                    mp_hands_module.HAND_CONNECTIONS,
                    mp_drawing_styles.get_default_hand_landmarks_style(),
                    mp_drawing_styles.get_default_hand_connections_style(),
                )

        

        # ── Stats Panel (kiri atas, semi-transparan) ───────────────────────
        panel_w, panel_h = 260, 130
        frame = overlay_rect(frame, 0, 0, panel_w, panel_h, (0, 0, 0), alpha=0.55)

        # REPS counter — large & bold
        cv2.putText(frame, "REPS:", (15, 45),
                    cv2.FONT_HERSHEY_SIMPLEX, 1.0, (255, 255, 255), 2, cv2.LINE_AA)
        cv2.putText(frame, str(counter), (130, 45),
                    cv2.FONT_HERSHEY_SIMPLEX, 1.2, (0, 255, 128), 3, cv2.LINE_AA)

        # STATUS
        cv2.putText(frame, "STATUS:", (15, 85),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 255, 255), 2, cv2.LINE_AA)

        if prediction == "fist":
            status_text = "FIST"
            status_color = (0, 60, 255)   # Red
        elif prediction == "palm":
            status_text = "PALM"
            status_color = (0, 220, 0)    # Green
        else:
            status_text = "---"
            status_color = (150, 150, 150)

        cv2.putText(frame, status_text, (150, 85),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.9, status_color, 2, cv2.LINE_AA)

        # Confidence
        conf_str = f"CONF: {confidence:.0%}" if prediction else "CONF: ---"
        cv2.putText(frame, conf_str, (15, 118),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (200, 200, 200), 1, cv2.LINE_AA)

        # Hint text
        cv2.putText(frame, "Q: quit  R: reset", (10, h - 10),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (180, 180, 180), 1, cv2.LINE_AA)

        cv2.imshow("Stroke Rehab — Hand Exercise Counter", frame)

        key = cv2.waitKey(1) & 0xFF
        if key == ord("q"):
            break
        elif key == ord("r"):
            counter = 0
            stage = None
            radius_balon = 20
            print("[Reset] Counter reset.")

    cap.release()
    cv2.destroyAllWindows()
    hands.close()
    udp_socket.close()
    print(f"[Done] Session ended. Total reps: {counter}")


if __name__ == "__main__":
    main()
