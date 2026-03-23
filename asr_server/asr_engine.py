# asr_server/asr_engine.py
import time
import gc
import numpy as np
import torch
from typing import Optional, Tuple
from qwen_asr import Qwen3ASRModel
from asr_server.config import ASR_MODEL_ID, ASR_DEVICE, ASR_MAX_BATCH, ASR_MAX_NEW_TOKENS

_cached_model: Optional[Qwen3ASRModel] = None
_cached_model_id: Optional[str] = None


def _resolve_device() -> str:
    if ASR_DEVICE == "auto":
        return "cuda" if torch.cuda.is_available() else "cpu"
    return ASR_DEVICE


def _get_model(model_id: str = ASR_MODEL_ID) -> Qwen3ASRModel:
    global _cached_model, _cached_model_id
    if _cached_model is not None and _cached_model_id == model_id:
        return _cached_model
    # Evict previous model
    if _cached_model is not None:
        del _cached_model
        gc.collect()
        if torch.cuda.is_available():
            torch.cuda.empty_cache()

    device = _resolve_device()
    dtype = torch.float16 if device == "cuda" else torch.float32
    _cached_model = Qwen3ASRModel.from_pretrained(
        model_id,
        dtype=dtype,
        device_map=device,
        max_inference_batch_size=ASR_MAX_BATCH,
        max_new_tokens=ASR_MAX_NEW_TOKENS,
    )
    _cached_model_id = model_id
    return _cached_model


def transcribe(
    audio: np.ndarray,              # float32, mono, 16000 Hz
    language: Optional[str] = None,
    model_id: str = ASR_MODEL_ID,
) -> Tuple[str, str, float]:
    """Returns (text, language, duration_ms)"""
    t0 = time.perf_counter()
    model = _get_model(model_id)
    results = model.transcribe(audio=(audio, 16000), language=language)
    duration_ms = (time.perf_counter() - t0) * 1000
    return results[0].text, results[0].language, duration_ms


def get_device_info() -> str:
    return _resolve_device()
