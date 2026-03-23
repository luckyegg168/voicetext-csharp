# asr_server/config.py
import os

ASR_MODEL_ID = os.getenv("ASR_MODEL_ID", "Qwen/Qwen3-ASR-0.6B")
ASR_DEVICE = os.getenv("ASR_DEVICE", "auto")   # auto | cuda | cpu
ASR_PORT = int(os.getenv("ASR_PORT", "8765"))
ASR_MAX_BATCH = int(os.getenv("ASR_MAX_BATCH", "4"))
ASR_MAX_NEW_TOKENS = int(os.getenv("ASR_MAX_NEW_TOKENS", "1024"))
