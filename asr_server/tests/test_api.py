# asr_server/tests/test_api.py
import io
import numpy as np
import soundfile as sf
import pytest
from fastapi.testclient import TestClient
from unittest.mock import patch


def _make_wav_bytes(duration_s: float = 1.0, sr: int = 16000) -> bytes:
    audio = np.zeros(int(sr * duration_s), dtype=np.float32)
    buf = io.BytesIO()
    sf.write(buf, audio, sr, format="WAV", subtype="FLOAT")
    return buf.getvalue()


@pytest.fixture()
def client():
    from asr_server.main import app
    return TestClient(app)


def test_health_returns_ready(client):
    with patch("asr_server.main.asr_engine.get_device_info", return_value="cpu"):
        resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json()["status"] == "ready"


def test_transcribe_wav_upload(client):
    wav_bytes = _make_wav_bytes()
    with patch("asr_server.main.asr_engine.transcribe", return_value=("Hello", "English", 50.0)):
        resp = client.post(
            "/transcribe",
            files={"audio": ("test.wav", wav_bytes, "audio/wav")},
            data={"language": "English"},
        )
    assert resp.status_code == 200
    data = resp.json()
    assert data["text"] == "Hello"
    assert data["language"] == "English"


def test_transcribe_missing_file_returns_422(client):
    resp = client.post("/transcribe")
    assert resp.status_code == 422
