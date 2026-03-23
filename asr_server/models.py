# asr_server/models.py
from pydantic import BaseModel
from typing import Optional


class TranscribeRequest(BaseModel):
    language: Optional[str] = None   # None = auto-detect


class TranscribeResponse(BaseModel):
    text: str
    language: str
    duration_ms: float


class HealthResponse(BaseModel):
    status: str                      # "ready" | "loading" | "error"
    model_id: str
    device: str
