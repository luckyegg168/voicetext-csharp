# asr_server/main.py
import io
import numpy as np
import soundfile as sf
from contextlib import asynccontextmanager
from typing import Optional
from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from asr_server import asr_engine
from asr_server.config import ASR_MODEL_ID, ASR_PORT
from asr_server.models import TranscribeResponse, HealthResponse


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Warm up model on startup
    try:
        asr_engine._get_model()
    except Exception as e:
        print(f"[warn] Model warm-up failed: {e}")
    yield


app = FastAPI(title="VoiceText ASR Server", lifespan=lifespan)


@app.get("/health", response_model=HealthResponse)
def health():
    return HealthResponse(
        status="ready",
        model_id=ASR_MODEL_ID,
        device=asr_engine.get_device_info(),
    )


@app.post("/transcribe", response_model=TranscribeResponse)
async def transcribe(
    audio: UploadFile = File(...),
    language: Optional[str] = Form(None),
):
    raw = await audio.read()
    try:
        buf = io.BytesIO(raw)
        audio_np, sr = sf.read(buf, dtype="float32", always_2d=False)
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid audio: {e}")

    # Convert to mono if stereo
    if audio_np.ndim == 2:
        audio_np = audio_np.mean(axis=1)

    # Resample to 16kHz if needed (simple linear interpolation)
    if sr != 16000:
        ratio = 16000 / sr
        new_len = int(len(audio_np) * ratio)
        audio_np = np.interp(
            np.linspace(0, len(audio_np) - 1, new_len),
            np.arange(len(audio_np)),
            audio_np,
        ).astype(np.float32)

    lang_param = None if language in (None, "", "auto") else language
    text, detected_lang, duration_ms = asr_engine.transcribe(audio_np, language=lang_param)
    return TranscribeResponse(text=text, language=detected_lang, duration_ms=duration_ms)


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("asr_server.main:app", host="127.0.0.1", port=ASR_PORT, reload=False)
