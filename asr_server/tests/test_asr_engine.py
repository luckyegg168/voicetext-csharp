# asr_server/tests/test_asr_engine.py
import numpy as np
import pytest
from unittest.mock import patch, MagicMock
from asr_server.asr_engine import transcribe


def test_transcribe_returns_text_and_language():
    mock_result = MagicMock()
    mock_result.text = "Hello world"
    mock_result.language = "English"

    with patch("asr_server.asr_engine._get_model") as mock_model:
        mock_model.return_value.transcribe.return_value = [mock_result]
        audio = np.zeros(16000, dtype=np.float32)
        text, lang, _ = transcribe(audio, language="English")

    assert text == "Hello world"
    assert lang == "English"


def test_transcribe_auto_detect_language():
    mock_result = MagicMock()
    mock_result.text = "你好"
    mock_result.language = "Chinese"

    with patch("asr_server.asr_engine._get_model") as mock_model:
        mock_model.return_value.transcribe.return_value = [mock_result]
        audio = np.zeros(16000, dtype=np.float32)
        text, lang, _ = transcribe(audio, language=None)

    assert lang == "Chinese"
