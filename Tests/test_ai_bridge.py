import importlib.util
import json
import socket
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("stroke_ai", ROOT / "AI" / "main.py")
ai = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ai)


def result_with_hand():
    points = [SimpleNamespace(x=i * 0.01, y=i * 0.02, z=i * 0.03) for i in range(21)]
    return SimpleNamespace(multi_hand_landmarks=[SimpleNamespace(landmark=points)])


class BridgeTests(unittest.TestCase):
    def test_model_contract_and_inference(self):
        model = ai.load_model()
        self.assertEqual(set(model.classes_), {"palm", "fist"})
        features = ai.extract_landmarks(result_with_hand())
        self.assertEqual(features.shape, (1, 63))
        np.testing.assert_allclose(features[0, :3], 0)
        probability = model.predict_proba(features)[0]
        self.assertAlmostEqual(float(probability.sum()), 1)

    def test_missing_hand_packet(self):
        result = SimpleNamespace(multi_hand_landmarks=[])
        self.assertIsNone(ai.extract_landmarks(result))
        self.assertEqual(ai.make_packet("fist", 1, result),
                         {"prediction": "none", "confidence": 0, "landmarks": []})

    def test_udp_json_jpeg_and_shutdown_packet(self):
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as data, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as video:
            data.bind(("127.0.0.1", 0))
            video.bind(("127.0.0.1", 0))
            data.settimeout(2)
            video.settimeout(2)
            sender = ai.UnitySender(data.getsockname()[1], video.getsockname()[1])
            try:
                packet = ai.make_packet("fist", 0.9, result_with_hand())
                sender.send(packet)
                self.assertEqual(json.loads(data.recv(65535)), packet)
                sender.preview(np.random.default_rng(7).integers(0, 256, (480, 640, 3), dtype=np.uint8))
                encoded = video.recv(65535)
                self.assertLessEqual(len(encoded), ai.MAX_DATAGRAM)
                decoded = cv2.imdecode(np.frombuffer(encoded, np.uint8), cv2.IMREAD_COLOR)
                self.assertEqual(decoded.shape, (240, 320, 3))
            finally:
                sender.close()
            self.assertEqual(json.loads(data.recv(65535))["prediction"], "none")

    def test_nonfinite_confidence_is_not_serialized(self):
        sender = ai.UnitySender()
        try:
            with self.assertRaises(ValueError):
                sender.send(ai.make_packet("fist", float("nan"), result_with_hand()))
        finally:
            sender.close()

    def test_camera_open_failure_releases_resources(self):
        args = SimpleNamespace(data_port=5052, video_port=5053, camera=0)
        with patch.object(ai.cv2, "VideoCapture") as capture, patch.object(ai, "UnitySender") as sender:
            capture.return_value.isOpened.return_value = False
            with self.assertRaisesRegex(RuntimeError, "Cannot open camera"):
                ai.run(args, None)
            capture.return_value.release.assert_called_once()
            sender.return_value.close.assert_called_once()

    def test_model_path_does_not_depend_on_working_directory(self):
        self.assertTrue(ai.MODEL_PATH.is_absolute())
        self.assertTrue(ai.MODEL_PATH.is_file())


if __name__ == "__main__":
    unittest.main()
