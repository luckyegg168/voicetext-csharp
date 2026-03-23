# VoiceText C# — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 本地全離線語音輸入工具，Apple macOS 風格 WPF UI，支援 VAD 自動斷句、Qwen3-ASR 轉錄、Ollama/llama.cpp 潤稿與翻譯，API key 快速切換。

**Architecture:**
- **Python ASR Server** (`asr_server/`): FastAPI + qwen-asr，提供 `/transcribe` HTTP 端點，C# 透過 HTTP 呼叫。Qwen3-ASR 為 Python 專屬 library，無法直接從 C# 呼叫。
- **C# WPF Frontend** (`src/`): .NET 8 WPF，Apple 風格 UI，NAudio 麥克風擷取，Silero VAD ONNX 靜音偵測，HTTP 呼叫 ASR Server 與 Ollama/llama.cpp。
- **Process Manager**: C# 啟動時自動 spawn Python ASR server subprocess，關閉時一併終止。

**Tech Stack:**
- Python 3.11+, FastAPI, uvicorn, qwen-asr, numpy, soundfile
- .NET 8, WPF, NAudio, Microsoft.ML.OnnxRuntime (Silero VAD), CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, System.Text.Json

---

## 補充需求（使用者未提及但必要）

| 項目 | 說明 |
|------|------|
| **Python server 生命週期** | C# 啟動/停止 Python ASR server，避免殘留 process |
| **音頻格式轉換** | Qwen3-ASR 需要 16kHz mono float32；NAudio 預設 44.1kHz stereo，需 resample |
| **VAD 靜默逾時** | 設定幾秒靜默後自動送出（預設 1.5s），可調整 |
| **全局熱鍵** | Win+Alt+V 切換錄音，不需要滑鼠點擊 |
| **系統托盤圖示** | 最小化到托盤，常駐背景 |
| **剪貼簿自動複製** | 轉錄/潤稿完成後自動複製到剪貼簿（可選） |
| **多麥克風選擇** | 設定中選擇輸入裝置 |
| **GPU 自動偵測** | ASR server 啟動時自動選 CUDA/CPU |
| **Prompt 模板** | 不同潤稿風格：正式/口語/技術文件/會議記錄 |
| **歷史記錄** | SQLite 本機儲存，可搜尋與重複使用 |
| **波形視覺化** | 即時音量條，錄音狀態清晰顯示 |
| **深色/淺色模式** | 跟隨 Windows 系統設定自動切換 |
| **ASR 串流輸出** | vLLM 後端支援逐字串流顯示（進階選項） |

---

## File Structure

```
voicetext_csharp/
├── asr_server/                         # Python ASR 後端
│   ├── main.py                         # FastAPI app 入口
│   ├── asr_engine.py                   # Qwen3ASRModel 封裝 + 快取
│   ├── models.py                       # Pydantic 請求/回應模型
│   ├── config.py                       # 設定（model_id, device, port）
│   ├── requirements.txt
│   └── tests/
│       ├── test_asr_engine.py
│       └── test_api.py
│
├── src/
│   ├── VoiceText.sln
│   ├── VoiceText.App/                  # WPF 主程式
│   │   ├── App.xaml / App.xaml.cs      # DI 容器、主題初始化
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml         # 主浮動視窗
│   │   │   ├── SettingsWindow.xaml     # 設定面板
│   │   │   └── HistoryWindow.xaml      # 歷史記錄面板
│   │   ├── Controls/                   # Apple 風格自訂控制項
│   │   │   ├── RecordButton.xaml       # 脈衝動畫錄音按鈕
│   │   │   ├── WaveformBar.xaml        # 即時音量條
│   │   │   ├── GlassCard.xaml          # 毛玻璃卡片容器
│   │   │   └── StatusBadge.xaml        # 狀態指示器
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs        # 錄音流程狀態機
│   │   │   ├── SettingsViewModel.cs    # 設定頁邏輯
│   │   │   └── HistoryViewModel.cs     # 歷史記錄邏輯
│   │   ├── Styles/
│   │   │   ├── AppleColors.xaml        # Apple 色板（system blue, gray, etc.）
│   │   │   ├── AppleTypography.xaml    # Segoe UI Variable 字型階層
│   │   │   ├── AppleControls.xaml      # Button, TextBox, ComboBox 模板
│   │   │   └── AppleAnimations.xaml    # Storyboard 動畫庫
│   │   ├── Converters/
│   │   │   ├── RecordingStateConverter.cs
│   │   │   └── BoolToVisibilityConverter.cs
│   │   └── Helpers/
│   │       ├── ClipboardHelper.cs
│   │       ├── GlobalHotkeyHelper.cs
│   │       └── SystemThemeHelper.cs
│   │
│   ├── VoiceText.Audio/                # 音頻引擎
│   │   ├── AudioCaptureService.cs      # NAudio 麥克風擷取
│   │   ├── AudioResampler.cs           # 44.1kHz→16kHz PCM 轉換
│   │   ├── VadEngine.cs                # Silero VAD ONNX 推論
│   │   ├── VadPipeline.cs              # 擷取→VAD→緩衝→觸發 ASR
│   │   ├── AudioChunk.cs               # 音頻資料結構（immutable）
│   │   └── IAudioCaptureService.cs
│   │
│   ├── VoiceText.Asr/                  # ASR 服務
│   │   ├── IAsrService.cs
│   │   ├── QwenAsrHttpService.cs       # HTTP client → Python server
│   │   ├── AsrServerManager.cs         # Python server process 管理
│   │   └── AsrResult.cs                # 轉錄結果（immutable）
│   │
│   ├── VoiceText.Llm/                  # LLM 服務（潤稿/翻譯）
│   │   ├── ILlmService.cs
│   │   ├── OllamaService.cs            # Ollama HTTP client
│   │   ├── LlamaCppService.cs          # llama.cpp server HTTP client
│   │   ├── LlmRouter.cs                # 根據設定路由到正確後端
│   │   ├── PolishService.cs            # 潤稿 prompt + 呼叫 LLM
│   │   ├── TranslationService.cs       # 翻譯 prompt + 呼叫 LLM
│   │   └── PromptTemplates.cs          # 各種 prompt 模板常數
│   │
│   ├── VoiceText.Config/               # 設定與 API Key 管理
│   │   ├── AppSettings.cs              # 設定 POCO（immutable record）
│   │   ├── SettingsService.cs          # 讀寫 JSON 設定檔
│   │   ├── ApiKeyStore.cs              # Windows DPAPI 加密 API key
│   │   └── MicrophoneEnumerator.cs     # 列舉系統麥克風
│   │
│   ├── VoiceText.Storage/              # 歷史記錄
│   │   ├── IHistoryRepository.cs
│   │   ├── HistoryRepository.cs        # SQLite via Microsoft.Data.Sqlite
│   │   └── HistoryEntry.cs             # 歷史記錄 record（immutable）
│   │
│   └── VoiceText.Tests/
│       ├── Audio/
│       │   ├── AudioResamplerTests.cs
│       │   └── VadEngineTests.cs
│       ├── Asr/
│       │   └── QwenAsrHttpServiceTests.cs
│       ├── Llm/
│       │   ├── OllamaServiceTests.cs
│       │   └── PolishServiceTests.cs
│       └── Config/
│           └── ApiKeyStoreTests.cs
│
└── docs/
    └── superpowers/plans/
        └── 2026-03-23-voicetext-csharp.md
```

---

## Phase 1: Python ASR Server

### Task 1: Python 專案骨架

**Files:**
- Create: `asr_server/requirements.txt`
- Create: `asr_server/config.py`
- Create: `asr_server/models.py`

- [ ] **Step 1: 建立 requirements.txt**

```
qwen-asr>=0.1.0
fastapi>=0.115.0
uvicorn[standard]>=0.32.0
numpy>=1.26.0
soundfile>=0.12.1
python-multipart>=0.0.12
```

- [ ] **Step 2: 建立 config.py**

```python
# asr_server/config.py
import os

ASR_MODEL_ID = os.getenv("ASR_MODEL_ID", "Qwen/Qwen3-ASR-0.6B")
ASR_DEVICE = os.getenv("ASR_DEVICE", "auto")   # auto | cuda | cpu
ASR_PORT = int(os.getenv("ASR_PORT", "8765"))
ASR_MAX_BATCH = int(os.getenv("ASR_MAX_BATCH", "4"))
ASR_MAX_NEW_TOKENS = int(os.getenv("ASR_MAX_NEW_TOKENS", "1024"))
```

- [ ] **Step 3: 建立 models.py**

```python
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
```

- [ ] **Step 4: 安裝依賴**

```bash
cd asr_server
pip install -r requirements.txt
```

- [ ] **Step 5: Commit**

```bash
git add asr_server/
git commit -m "chore: add Python ASR server scaffolding"
```

---

### Task 2: ASR Engine 封裝

**Files:**
- Create: `asr_server/asr_engine.py`
- Create: `asr_server/tests/test_asr_engine.py`

- [ ] **Step 1: 撰寫測試（RED）**

```python
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
```

- [ ] **Step 2: 執行測試，確認 FAIL**

```bash
cd asr_server
python -m pytest tests/test_asr_engine.py -v
# Expected: ModuleNotFoundError or ImportError
```

- [ ] **Step 3: 實作 asr_engine.py**

```python
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
```

- [ ] **Step 4: 執行測試，確認 PASS**

```bash
python -m pytest tests/test_asr_engine.py -v
```

- [ ] **Step 5: Commit**

```bash
git add asr_server/asr_engine.py asr_server/tests/test_asr_engine.py
git commit -m "feat: add Qwen3-ASR engine wrapper with model caching"
```

---

### Task 3: FastAPI 端點

**Files:**
- Create: `asr_server/main.py`
- Create: `asr_server/tests/test_api.py`

- [ ] **Step 1: 撰寫 API 測試（RED）**

```python
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
```

- [ ] **Step 2: 執行測試，確認 FAIL**

```bash
python -m pytest tests/test_api.py -v
```

- [ ] **Step 3: 實作 main.py**

```python
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

    # Resample to 16kHz if needed (simple linear; for production use librosa)
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
```

- [ ] **Step 4: 執行測試，確認 PASS**

```bash
python -m pytest tests/ -v
```

- [ ] **Step 5: 手動驗證 server 啟動**

```bash
python -m asr_server.main
# 另開終端: curl http://127.0.0.1:8765/health
```

- [ ] **Step 6: Commit**

```bash
git add asr_server/main.py asr_server/tests/test_api.py
git commit -m "feat: add FastAPI transcribe and health endpoints"
```

---

## Phase 2: C# WPF 專案骨架

### Task 4: 建立 .NET Solution

**Files:**
- Create: `src/VoiceText.sln`
- Create: `src/VoiceText.App/VoiceText.App.csproj`
- Create: `src/VoiceText.Audio/VoiceText.Audio.csproj`
- Create: `src/VoiceText.Asr/VoiceText.Asr.csproj`
- Create: `src/VoiceText.Llm/VoiceText.Llm.csproj`
- Create: `src/VoiceText.Config/VoiceText.Config.csproj`
- Create: `src/VoiceText.Storage/VoiceText.Storage.csproj`
- Create: `src/VoiceText.Tests/VoiceText.Tests.csproj`

- [ ] **Step 1: 建立 solution 與專案**

```bash
cd src
dotnet new sln -n VoiceText
dotnet new wpf -n VoiceText.App -f net8.0-windows
dotnet new classlib -n VoiceText.Audio -f net8.0-windows
dotnet new classlib -n VoiceText.Asr -f net8.0-windows
dotnet new classlib -n VoiceText.Llm -f net8.0-windows
dotnet new classlib -n VoiceText.Config -f net8.0-windows
dotnet new classlib -n VoiceText.Storage -f net8.0-windows
dotnet new xunit -n VoiceText.Tests -f net8.0-windows

dotnet sln add VoiceText.App VoiceText.Audio VoiceText.Asr VoiceText.Llm VoiceText.Config VoiceText.Storage VoiceText.Tests
```

- [ ] **Step 2: 加入 NuGet 套件**

```bash
# Audio
dotnet add VoiceText.Audio package NAudio --version 2.2.1
dotnet add VoiceText.Audio package Microsoft.ML.OnnxRuntime --version 1.20.1

# Config
dotnet add VoiceText.Config package System.Text.Json --version 8.0.5

# Storage
dotnet add VoiceText.Storage package Microsoft.Data.Sqlite --version 8.0.10

# App
dotnet add VoiceText.App package CommunityToolkit.Mvvm --version 8.3.2
dotnet add VoiceText.App package Microsoft.Extensions.DependencyInjection --version 8.0.1
dotnet add VoiceText.App package Microsoft.Extensions.Hosting --version 8.0.1

# Tests
dotnet add VoiceText.Tests package Moq --version 4.20.72
dotnet add VoiceText.Tests package FluentAssertions --version 6.12.2

# 加入專案參考
dotnet add VoiceText.App reference VoiceText.Audio VoiceText.Asr VoiceText.Llm VoiceText.Config VoiceText.Storage
dotnet add VoiceText.Tests reference VoiceText.Audio VoiceText.Asr VoiceText.Llm VoiceText.Config VoiceText.Storage
```

- [ ] **Step 3: 確認 build 成功**

```bash
dotnet build VoiceText.sln
# Expected: Build succeeded.
```

- [ ] **Step 4: Commit**

```bash
git add src/
git commit -m "chore: initialize .NET 8 WPF solution with all projects"
```

---

### Task 5: Apple 風格設計系統

**Files:**
- Create: `src/VoiceText.App/Styles/AppleColors.xaml`
- Create: `src/VoiceText.App/Styles/AppleTypography.xaml`
- Create: `src/VoiceText.App/Styles/AppleControls.xaml`
- Create: `src/VoiceText.App/Styles/AppleAnimations.xaml`
- Modify: `src/VoiceText.App/App.xaml`

- [ ] **Step 1: 建立 AppleColors.xaml**

```xml
<!-- src/VoiceText.App/Styles/AppleColors.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- System Colors (Light Mode) -->
  <Color x:Key="SystemBlueColor">#FF007AFF</Color>
  <Color x:Key="SystemGreenColor">#FF34C759</Color>
  <Color x:Key="SystemRedColor">#FFFF3B30</Color>
  <Color x:Key="SystemOrangeColor">#FFFF9500</Color>
  <Color x:Key="SystemGrayColor">#FF8E8E93</Color>

  <!-- Background -->
  <Color x:Key="BackgroundPrimaryColor">#FFF2F2F7</Color>
  <Color x:Key="BackgroundSecondaryColor">#FFFFFFFF</Color>
  <Color x:Key="BackgroundTertiaryColor">#FFE5E5EA</Color>

  <!-- Label Colors -->
  <Color x:Key="LabelPrimaryColor">#FF000000</Color>
  <Color x:Key="LabelSecondaryColor">#993C3C43</Color>
  <Color x:Key="LabelTertiaryColor">#4C3C3C43</Color>

  <!-- Separator -->
  <Color x:Key="SeparatorColor">#3C3C3C4A</Color>

  <!-- Brushes -->
  <SolidColorBrush x:Key="SystemBlueBrush" Color="{StaticResource SystemBlueColor}"/>
  <SolidColorBrush x:Key="SystemGreenBrush" Color="{StaticResource SystemGreenColor}"/>
  <SolidColorBrush x:Key="SystemRedBrush" Color="{StaticResource SystemRedColor}"/>
  <SolidColorBrush x:Key="SystemOrangeBrush" Color="{StaticResource SystemOrangeColor}"/>
  <SolidColorBrush x:Key="SystemGrayBrush" Color="{StaticResource SystemGrayColor}"/>
  <SolidColorBrush x:Key="BackgroundPrimaryBrush" Color="{StaticResource BackgroundPrimaryColor}"/>
  <SolidColorBrush x:Key="BackgroundSecondaryBrush" Color="{StaticResource BackgroundSecondaryColor}"/>
  <SolidColorBrush x:Key="BackgroundTertiaryBrush" Color="{StaticResource BackgroundTertiaryColor}"/>
  <SolidColorBrush x:Key="LabelPrimaryBrush" Color="{StaticResource LabelPrimaryColor}"/>
  <SolidColorBrush x:Key="LabelSecondaryBrush" Color="{StaticResource LabelSecondaryColor}"/>
  <SolidColorBrush x:Key="LabelTertiaryBrush" Color="{StaticResource LabelTertiaryColor}"/>
  <SolidColorBrush x:Key="SeparatorBrush" Color="{StaticResource SeparatorColor}"/>
</ResourceDictionary>
```

- [ ] **Step 2: 建立 AppleTypography.xaml**

```xml
<!-- src/VoiceText.App/Styles/AppleTypography.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Font Family: Segoe UI Variable is closest to SF Pro on Windows -->
  <FontFamily x:Key="AppleFont">Segoe UI Variable, Segoe UI, sans-serif</FontFamily>

  <!-- Text Styles -->
  <Style x:Key="LargeTitle" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource AppleFont}"/>
    <Setter Property="FontSize" Value="34"/>
    <Setter Property="FontWeight" Value="Regular"/>
    <Setter Property="Foreground" Value="{StaticResource LabelPrimaryBrush}"/>
  </Style>

  <Style x:Key="Title1" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource AppleFont}"/>
    <Setter Property="FontSize" Value="28"/>
    <Setter Property="FontWeight" Value="Regular"/>
    <Setter Property="Foreground" Value="{StaticResource LabelPrimaryBrush}"/>
  </Style>

  <Style x:Key="Headline" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource AppleFont}"/>
    <Setter Property="FontSize" Value="17"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{StaticResource LabelPrimaryBrush}"/>
  </Style>

  <Style x:Key="Body" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource AppleFont}"/>
    <Setter Property="FontSize" Value="17"/>
    <Setter Property="FontWeight" Value="Regular"/>
    <Setter Property="Foreground" Value="{StaticResource LabelPrimaryBrush}"/>
  </Style>

  <Style x:Key="Callout" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource AppleFont}"/>
    <Setter Property="FontSize" Value="16"/>
    <Setter Property="Foreground" Value="{StaticResource LabelPrimaryBrush}"/>
  </Style>

  <Style x:Key="Caption" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{StaticResource AppleFont}"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Foreground" Value="{StaticResource LabelSecondaryBrush}"/>
  </Style>
</ResourceDictionary>
```

- [ ] **Step 3: 建立 AppleControls.xaml**

```xml
<!-- src/VoiceText.App/Styles/AppleControls.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Apple-style Button -->
  <Style x:Key="AppleButton" TargetType="Button">
    <Setter Property="Background" Value="{StaticResource SystemBlueBrush}"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="FontFamily" Value="{StaticResource AppleFont}"/>
    <Setter Property="FontSize" Value="15"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Padding" Value="16,8"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Root" Background="{TemplateBinding Background}"
                  CornerRadius="8" Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Root" Property="Opacity" Value="0.85"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Root" Property="Opacity" Value="0.7"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="Root" Property="Opacity" Value="0.4"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Apple-style TextBox -->
  <Style x:Key="AppleTextBox" TargetType="TextBox">
    <Setter Property="Background" Value="{StaticResource BackgroundSecondaryBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource SeparatorBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="10,8"/>
    <Setter Property="FontFamily" Value="{StaticResource AppleFont}"/>
    <Setter Property="FontSize" Value="15"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Border Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="8"
                  Padding="{TemplateBinding Padding}">
            <ScrollViewer x:Name="PART_ContentHost"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsFocused" Value="True">
              <Setter Property="BorderBrush" Value="{StaticResource SystemBlueBrush}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Apple-style Card -->
  <Style x:Key="AppleCard" TargetType="Border">
    <Setter Property="Background" Value="{StaticResource BackgroundSecondaryBrush}"/>
    <Setter Property="CornerRadius" Value="12"/>
    <Setter Property="Padding" Value="16"/>
    <Setter Property="Effect">
      <Setter.Value>
        <DropShadowEffect BlurRadius="20" ShadowDepth="2" Direction="270"
                          Color="#20000000" Opacity="0.15"/>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>
```

- [ ] **Step 4: 更新 App.xaml 引入所有樣式**

```xml
<!-- src/VoiceText.App/App.xaml -->
<Application x:Class="VoiceText.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Styles/AppleColors.xaml"/>
        <ResourceDictionary Source="Styles/AppleTypography.xaml"/>
        <ResourceDictionary Source="Styles/AppleControls.xaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 5: Commit**

```bash
git add src/VoiceText.App/Styles/ src/VoiceText.App/App.xaml
git commit -m "feat: add Apple-style WPF design system (colors, typography, controls)"
```

---

## Phase 3: Config 與 API Key 管理

### Task 6: AppSettings & SettingsService

**Files:**
- Create: `src/VoiceText.Config/AppSettings.cs`
- Create: `src/VoiceText.Config/SettingsService.cs`
- Create: `src/VoiceText.Tests/Config/SettingsServiceTests.cs`

- [ ] **Step 1: 撰寫測試（RED）**

```csharp
// src/VoiceText.Tests/Config/SettingsServiceTests.cs
using FluentAssertions;
using VoiceText.Config;

public class SettingsServiceTests
{
    [Fact]
    public void Save_and_Load_roundtrips_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var svc = new SettingsService(path);
        var original = AppSettings.Default with { AsrServerPort = 9999 };

        svc.Save(original);
        var loaded = svc.Load();

        loaded.AsrServerPort.Should().Be(9999);
        File.Delete(path);
    }

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var svc = new SettingsService("/nonexistent/settings.json");
        var settings = svc.Load();
        settings.Should().Be(AppSettings.Default);
    }
}
```

- [ ] **Step 2: 執行測試，確認 FAIL**

```bash
dotnet test VoiceText.Tests --filter "SettingsServiceTests"
```

- [ ] **Step 3: 實作 AppSettings.cs**

```csharp
// src/VoiceText.Config/AppSettings.cs
namespace VoiceText.Config;

public record AppSettings
{
    public string AsrServerHost { get; init; } = "127.0.0.1";
    public int AsrServerPort { get; init; } = 8765;
    public string AsrModelId { get; init; } = "Qwen/Qwen3-ASR-0.6B";
    public string AsrLanguage { get; init; } = "auto";

    public string LlmBackend { get; init; } = "Ollama";     // "Ollama" | "LlamaCpp"
    public string OllamaBaseUrl { get; init; } = "http://localhost:11434";
    public string LlamaCppBaseUrl { get; init; } = "http://localhost:8080";
    public string LlmModelName { get; init; } = "llama3.2";
    public string PolishPromptStyle { get; init; } = "natural";  // natural|formal|technical|meeting

    public string MicrophoneDeviceId { get; init; } = "";
    public double VadSilenceTimeoutMs { get; init; } = 1500;
    public bool AutoCopyToClipboard { get; init; } = true;
    public bool StartMinimized { get; init; } = false;
    public string Theme { get; init; } = "System";          // System|Light|Dark
    public string GlobalHotkey { get; init; } = "Alt+Shift+V";

    public static AppSettings Default => new();
}
```

- [ ] **Step 4: 實作 SettingsService.cs**

```csharp
// src/VoiceText.Config/SettingsService.cs
using System.Text.Json;

namespace VoiceText.Config;

public class SettingsService
{
    private readonly string _path;
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public SettingsService(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
            return AppSettings.Default;
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, _opts));
    }
}
```

- [ ] **Step 5: 執行測試，確認 PASS**

```bash
dotnet test VoiceText.Tests --filter "SettingsServiceTests"
```

- [ ] **Step 6: Commit**

```bash
git add src/VoiceText.Config/ src/VoiceText.Tests/Config/
git commit -m "feat: add AppSettings record and SettingsService with JSON persistence"
```

---

### Task 7: ApiKeyStore（Windows DPAPI 加密）

**Files:**
- Create: `src/VoiceText.Config/ApiKeyStore.cs`
- Create: `src/VoiceText.Tests/Config/ApiKeyStoreTests.cs`

- [ ] **Step 1: 撰寫測試（RED）**

```csharp
// src/VoiceText.Tests/Config/ApiKeyStoreTests.cs
using FluentAssertions;
using VoiceText.Config;

public class ApiKeyStoreTests
{
    [Fact]
    public void SetAndGet_roundtrips_api_key()
    {
        var store = new ApiKeyStore("TestProfile");
        store.Set("OllamaApiKey", "my-secret-key");
        store.Get("OllamaApiKey").Should().Be("my-secret-key");
        store.Delete("OllamaApiKey");
    }

    [Fact]
    public void Get_returns_null_for_missing_key()
    {
        var store = new ApiKeyStore("TestProfile");
        store.Get("NonExistentKey").Should().BeNull();
    }
}
```

- [ ] **Step 2: 實作 ApiKeyStore.cs（使用 Windows DPAPI）**

```csharp
// src/VoiceText.Config/ApiKeyStore.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VoiceText.Config;

/// <summary>
/// Stores API keys encrypted with Windows DPAPI (per-user scope).
/// Keys are stored in %APPDATA%\VoiceText\keys\{profile}.json
/// </summary>
public class ApiKeyStore
{
    private readonly string _filePath;

    public ApiKeyStore(string profile = "default")
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceText", "keys");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, $"{profile}.json");
    }

    public void Set(string name, string value)
    {
        var store = LoadRaw();
        var encrypted = Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
        store[name] = encrypted;
        File.WriteAllText(_filePath, JsonSerializer.Serialize(store));
    }

    public string? Get(string name)
    {
        var store = LoadRaw();
        if (!store.TryGetValue(name, out var encrypted)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return null; }
    }

    public void Delete(string name)
    {
        var store = LoadRaw();
        store.Remove(name);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(store));
    }

    private Dictionary<string, string> LoadRaw()
    {
        if (!File.Exists(_filePath)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_filePath)) ?? new(); }
        catch { return new(); }
    }
}
```

- [ ] **Step 3: 執行測試，確認 PASS**

```bash
dotnet test VoiceText.Tests --filter "ApiKeyStoreTests"
```

- [ ] **Step 4: Commit**

```bash
git add src/VoiceText.Config/ApiKeyStore.cs src/VoiceText.Tests/Config/
git commit -m "feat: add DPAPI-encrypted API key store"
```

---

## Phase 4: 音頻引擎

### Task 8: 音頻擷取與重新採樣

**Files:**
- Create: `src/VoiceText.Audio/AudioChunk.cs`
- Create: `src/VoiceText.Audio/IAudioCaptureService.cs`
- Create: `src/VoiceText.Audio/AudioCaptureService.cs`
- Create: `src/VoiceText.Audio/AudioResampler.cs`
- Create: `src/VoiceText.Tests/Audio/AudioResamplerTests.cs`

- [ ] **Step 1: 建立 AudioChunk（immutable record）**

```csharp
// src/VoiceText.Audio/AudioChunk.cs
namespace VoiceText.Audio;

public record AudioChunk(float[] Samples, int SampleRate, DateTime CapturedAt)
{
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}
```

- [ ] **Step 2: 撰寫 Resampler 測試（RED）**

```csharp
// src/VoiceText.Tests/Audio/AudioResamplerTests.cs
using FluentAssertions;
using VoiceText.Audio;

public class AudioResamplerTests
{
    [Fact]
    public void Resample_44100_to_16000_preserves_duration()
    {
        var original = new float[44100]; // 1 second at 44.1kHz
        var result = AudioResampler.Resample(original, 44100, 16000);
        result.Length.Should().Be(16000);
    }

    [Fact]
    public void Resample_same_rate_returns_same_length()
    {
        var original = new float[16000];
        var result = AudioResampler.Resample(original, 16000, 16000);
        result.Length.Should().Be(16000);
    }

    [Fact]
    public void StereoToMono_averages_channels()
    {
        // Channel L = 1.0, Channel R = 0.0, expected mono = 0.5
        var stereo = new float[] { 1.0f, 0.0f, 1.0f, 0.0f };
        var mono = AudioResampler.StereoToMono(stereo);
        mono.Should().AllBeApproximately(0.5f, 0.001f);
    }
}
```

- [ ] **Step 3: 實作 AudioResampler.cs**

```csharp
// src/VoiceText.Audio/AudioResampler.cs
namespace VoiceText.Audio;

public static class AudioResampler
{
    public static float[] Resample(float[] samples, int sourceSampleRate, int targetSampleRate)
    {
        if (sourceSampleRate == targetSampleRate)
            return samples;

        double ratio = (double)targetSampleRate / sourceSampleRate;
        int newLength = (int)(samples.Length * ratio);
        var result = new float[newLength];
        for (int i = 0; i < newLength; i++)
        {
            double srcPos = i / ratio;
            int lo = (int)srcPos;
            int hi = Math.Min(lo + 1, samples.Length - 1);
            double t = srcPos - lo;
            result[i] = (float)(samples[lo] * (1 - t) + samples[hi] * t);
        }
        return result;
    }

    public static float[] StereoToMono(float[] stereo)
    {
        var mono = new float[stereo.Length / 2];
        for (int i = 0; i < mono.Length; i++)
            mono[i] = (stereo[i * 2] + stereo[i * 2 + 1]) * 0.5f;
        return mono;
    }
}
```

- [ ] **Step 4: 實作 AudioCaptureService.cs**

```csharp
// src/VoiceText.Audio/AudioCaptureService.cs
using NAudio.Wave;

namespace VoiceText.Audio;

public interface IAudioCaptureService : IDisposable
{
    event EventHandler<AudioChunk> ChunkAvailable;
    IReadOnlyList<(string Id, string Name)> GetAvailableDevices();
    void StartCapture(string? deviceId = null);
    void StopCapture();
}

public class AudioCaptureService : IAudioCaptureService
{
    private WaveInEvent? _waveIn;
    private readonly List<float> _buffer = new();
    private readonly object _lock = new();
    private const int ChunkMs = 30;   // VAD chunk size: 30ms

    public event EventHandler<AudioChunk>? ChunkAvailable;

    public IReadOnlyList<(string Id, string Name)> GetAvailableDevices()
    {
        var devices = new List<(string, string)>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add((i.ToString(), caps.ProductName));
        }
        return devices;
    }

    public void StartCapture(string? deviceId = null)
    {
        StopCapture();
        _waveIn = new WaveInEvent
        {
            DeviceNumber = int.TryParse(deviceId, out int id) ? id : 0,
            WaveFormat = new WaveFormat(44100, 16, 1),
            BufferMilliseconds = ChunkMs,
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
    }

    public void StopCapture()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;
        lock (_lock) { _buffer.Clear(); }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Convert Int16 PCM to float
        var floats = new float[e.BytesRecorded / 2];
        for (int i = 0; i < floats.Length; i++)
            floats[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;

        // Resample 44100 → 16000
        var resampled = AudioResampler.Resample(floats, 44100, 16000);
        ChunkAvailable?.Invoke(this, new AudioChunk(resampled, 16000, DateTime.UtcNow));
    }

    public void Dispose() => StopCapture();
}
```

- [ ] **Step 5: 執行測試**

```bash
dotnet test VoiceText.Tests --filter "AudioResamplerTests"
```

- [ ] **Step 6: Commit**

```bash
git add src/VoiceText.Audio/ src/VoiceText.Tests/Audio/AudioResamplerTests.cs
git commit -m "feat: add audio capture service and resampler (44.1kHz→16kHz)"
```

---

### Task 9: Silero VAD Engine

**Files:**
- Create: `src/VoiceText.Audio/VadEngine.cs`
- Create: `src/VoiceText.Audio/VadPipeline.cs`
- Create: `src/VoiceText.Tests/Audio/VadEngineTests.cs`

> Silero VAD ONNX model: 下載 `silero_vad.onnx` 至 `src/VoiceText.App/Assets/`
> 下載：https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx

- [ ] **Step 1: 撰寫 VAD 測試（RED）**

```csharp
// src/VoiceText.Tests/Audio/VadEngineTests.cs
using FluentAssertions;
using VoiceText.Audio;

public class VadEngineTests
{
    [Fact]
    public void Silence_chunk_returns_false()
    {
        var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "silero_vad.onnx");
        if (!File.Exists(modelPath)) return; // Skip if model not downloaded

        using var vad = new VadEngine(modelPath);
        var silence = new float[512]; // all zeros = silence
        vad.IsSpeech(silence, 16000).Should().BeFalse();
    }
}
```

- [ ] **Step 2: 實作 VadEngine.cs**

```csharp
// src/VoiceText.Audio/VadEngine.cs
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VoiceText.Audio;

public class VadEngine : IDisposable
{
    private readonly InferenceSession _session;
    private float[] _h = new float[2 * 1 * 64];
    private float[] _c = new float[2 * 1 * 64];
    private const float Threshold = 0.5f;

    public VadEngine(string modelPath)
    {
        _session = new InferenceSession(modelPath);
    }

    public bool IsSpeech(float[] samples, int sampleRate)
    {
        // Silero VAD expects chunks of exactly 512 (16kHz) or 256 (8kHz) samples
        var inputTensor = new DenseTensor<float>(samples, [1, samples.Length]);
        var srTensor = new DenseTensor<long>([sampleRate], [1]);
        var hTensor = new DenseTensor<float>(_h, [2, 1, 64]);
        var cTensor = new DenseTensor<float>(_c, [2, 1, 64]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
            NamedOnnxValue.CreateFromTensor("sr", srTensor),
            NamedOnnxValue.CreateFromTensor("h", hTensor),
            NamedOnnxValue.CreateFromTensor("c", cTensor),
        };

        using var outputs = _session.Run(inputs);
        var prob = outputs.First(o => o.Name == "output").AsEnumerable<float>().First();
        _h = outputs.First(o => o.Name == "hn").AsEnumerable<float>().ToArray();
        _c = outputs.First(o => o.Name == "cn").AsEnumerable<float>().ToArray();

        return prob >= Threshold;
    }

    public void Reset()
    {
        _h = new float[2 * 1 * 64];
        _c = new float[2 * 1 * 64];
    }

    public void Dispose() => _session.Dispose();
}
```

- [ ] **Step 3: 實作 VadPipeline.cs**

```csharp
// src/VoiceText.Audio/VadPipeline.cs
namespace VoiceText.Audio;

public class VadPipeline : IDisposable
{
    private readonly VadEngine _vad;
    private readonly List<float> _speechBuffer = new();
    private readonly double _silenceTimeoutMs;
    private DateTime _lastSpeechTime = DateTime.MinValue;
    private bool _wasSpeech = false;
    private const int VadChunkSamples = 512; // 32ms at 16kHz

    public event EventHandler<float[]>? SpeechSegmentReady;

    public VadPipeline(VadEngine vad, double silenceTimeoutMs = 1500)
    {
        _vad = vad;
        _silenceTimeoutMs = silenceTimeoutMs;
    }

    public void Feed(AudioChunk chunk)
    {
        // Process in 512-sample windows
        var queue = new Queue<float>(chunk.Samples);
        var window = new List<float>();

        while (queue.Count > 0)
        {
            window.Add(queue.Dequeue());
            if (window.Count < VadChunkSamples) continue;

            bool isSpeech = _vad.IsSpeech([.. window], chunk.SampleRate);
            window.Clear();

            if (isSpeech)
            {
                _speechBuffer.AddRange(window);
                _lastSpeechTime = DateTime.UtcNow;
                _wasSpeech = true;
            }
            else if (_wasSpeech)
            {
                double silenceMs = (DateTime.UtcNow - _lastSpeechTime).TotalMilliseconds;
                if (silenceMs >= _silenceTimeoutMs)
                {
                    var segment = _speechBuffer.ToArray();
                    _speechBuffer.Clear();
                    _vad.Reset();
                    _wasSpeech = false;
                    SpeechSegmentReady?.Invoke(this, segment);
                }
            }
        }
    }

    public float[]? FlushIfAny()
    {
        if (_speechBuffer.Count == 0) return null;
        var segment = _speechBuffer.ToArray();
        _speechBuffer.Clear();
        _vad.Reset();
        _wasSpeech = false;
        return segment;
    }

    public void Dispose() => _vad.Dispose();
}
```

- [ ] **Step 4: Commit**

```bash
git add src/VoiceText.Audio/VadEngine.cs src/VoiceText.Audio/VadPipeline.cs
git add src/VoiceText.Tests/Audio/VadEngineTests.cs
git commit -m "feat: add Silero VAD engine and speech pipeline with silence timeout"
```

---

## Phase 5: ASR HTTP Client

### Task 10: QwenAsrHttpService + AsrServerManager

**Files:**
- Create: `src/VoiceText.Asr/AsrResult.cs`
- Create: `src/VoiceText.Asr/IAsrService.cs`
- Create: `src/VoiceText.Asr/QwenAsrHttpService.cs`
- Create: `src/VoiceText.Asr/AsrServerManager.cs`
- Create: `src/VoiceText.Tests/Asr/QwenAsrHttpServiceTests.cs`

- [ ] **Step 1: 撰寫測試（RED）**

```csharp
// src/VoiceText.Tests/Asr/QwenAsrHttpServiceTests.cs
using FluentAssertions;
using Moq;
using System.Net;
using System.Net.Http;
using VoiceText.Asr;

public class QwenAsrHttpServiceTests
{
    private static HttpClient MakeFakeClient(string responseJson, HttpStatusCode code = HttpStatusCode.OK)
    {
        var handler = new TestHttpMessageHandler(responseJson, code);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8765") };
    }

    [Fact]
    public async Task Transcribe_returns_text_on_success()
    {
        var json = """{"text":"Hello world","language":"English","duration_ms":120.5}""";
        var svc = new QwenAsrHttpService(MakeFakeClient(json));
        var audio = new float[16000];

        var result = await svc.TranscribeAsync(audio, "English");

        result.Text.Should().Be("Hello world");
        result.Language.Should().Be("English");
    }

    [Fact]
    public async Task Transcribe_throws_on_server_error()
    {
        var svc = new QwenAsrHttpService(MakeFakeClient("error", HttpStatusCode.InternalServerError));
        var act = () => svc.TranscribeAsync(new float[16000], null);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

// Test helper
class TestHttpMessageHandler(string response, HttpStatusCode code) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(response) });
}
```

- [ ] **Step 2: 實作 ASR 服務**

```csharp
// src/VoiceText.Asr/AsrResult.cs
namespace VoiceText.Asr;
public record AsrResult(string Text, string Language, double DurationMs);

// src/VoiceText.Asr/IAsrService.cs
namespace VoiceText.Asr;
public interface IAsrService
{
    Task<AsrResult> TranscribeAsync(float[] audio16kHz, string? language, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

// src/VoiceText.Asr/QwenAsrHttpService.cs
using System.Net.Http.Headers;
using System.Text.Json;

namespace VoiceText.Asr;

public class QwenAsrHttpService : IAsrService
{
    private readonly HttpClient _http;

    public QwenAsrHttpService(HttpClient http) { _http = http; }

    public async Task<AsrResult> TranscribeAsync(float[] audio16kHz, string? language, CancellationToken ct = default)
    {
        var wavBytes = ToWav(audio16kHz, 16000);
        using var form = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audioContent, "audio", "audio.wav");
        if (!string.IsNullOrEmpty(language))
            form.Add(new StringContent(language), "language");

        var response = await _http.PostAsync("/transcribe", form, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ASR server error: {response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json).RootElement;
        return new AsrResult(
            doc.GetProperty("text").GetString()!,
            doc.GetProperty("language").GetString()!,
            doc.GetProperty("duration_ms").GetDouble());
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static byte[] ToWav(float[] samples, int sampleRate)
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        int byteCount = samples.Length * 2;
        writer.Write("RIFF"u8.ToArray()); writer.Write(36 + byteCount);
        writer.Write("WAVE"u8.ToArray()); writer.Write("fmt "u8.ToArray());
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(sampleRate); writer.Write(sampleRate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8.ToArray()); writer.Write(byteCount);
        foreach (var s in samples)
            writer.Write((short)(s * 32767f));
        return ms.ToArray();
    }
}
```

- [ ] **Step 3: 實作 AsrServerManager.cs**

```csharp
// src/VoiceText.Asr/AsrServerManager.cs
using System.Diagnostics;

namespace VoiceText.Asr;

public class AsrServerManager : IDisposable
{
    private Process? _process;
    private readonly string _pythonExe;
    private readonly string _serverScript;
    private readonly int _port;

    public AsrServerManager(string pythonExe, string serverScript, int port = 8765)
    {
        _pythonExe = pythonExe;
        _serverScript = serverScript;
        _port = port;
    }

    public void Start()
    {
        if (_process is { HasExited: false }) return;

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = $"-m asr_server.main",
                WorkingDirectory = Path.GetDirectoryName(_serverScript),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                EnvironmentVariables = { ["ASR_PORT"] = _port.ToString() },
            },
        };
        _process.Start();
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(3000);
        }
        _process?.Dispose();
        _process = null;
    }

    public bool IsRunning => _process is { HasExited: false };

    public void Dispose() => Stop();
}
```

- [ ] **Step 4: 執行測試**

```bash
dotnet test VoiceText.Tests --filter "QwenAsrHttpServiceTests"
```

- [ ] **Step 5: Commit**

```bash
git add src/VoiceText.Asr/ src/VoiceText.Tests/Asr/
git commit -m "feat: add Qwen ASR HTTP client and Python server process manager"
```

---

## Phase 6: LLM 服務（潤稿與翻譯）

### Task 11: Ollama & llama.cpp HTTP 客戶端

**Files:**
- Create: `src/VoiceText.Llm/ILlmService.cs`
- Create: `src/VoiceText.Llm/OllamaService.cs`
- Create: `src/VoiceText.Llm/LlamaCppService.cs`
- Create: `src/VoiceText.Llm/LlmRouter.cs`
- Create: `src/VoiceText.Tests/Llm/OllamaServiceTests.cs`

- [ ] **Step 1: 撰寫測試（RED）**

```csharp
// src/VoiceText.Tests/Llm/OllamaServiceTests.cs
using FluentAssertions;
using VoiceText.Llm;

public class OllamaServiceTests
{
    [Fact]
    public async Task Complete_returns_response_text()
    {
        var json = """{"model":"llama3.2","message":{"content":"Polished text."}}""";
        var svc = new OllamaService(MakeFakeClient(json));
        var result = await svc.CompleteAsync("llama3.2", "system", "Polish this.");
        result.Should().Be("Polished text.");
    }

    private static HttpClient MakeFakeClient(string json) =>
        new(new TestHttpMessageHandler(json)) { BaseAddress = new Uri("http://localhost:11434") };
}
```

- [ ] **Step 2: 實作 LLM 服務**

```csharp
// src/VoiceText.Llm/ILlmService.cs
namespace VoiceText.Llm;
public interface ILlmService
{
    Task<string> CompleteAsync(string model, string systemPrompt, string userPrompt,
                               CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string model, string systemPrompt, string userPrompt,
                                          CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

// src/VoiceText.Llm/OllamaService.cs
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace VoiceText.Llm;

public class OllamaService : ILlmService
{
    private readonly HttpClient _http;
    public OllamaService(HttpClient http) { _http = http; }

    public async Task<string> CompleteAsync(string model, string systemPrompt, string userPrompt,
                                             CancellationToken ct = default)
    {
        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            stream = false,
        };
        var resp = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        resp.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString()!;
    }

    public async IAsyncEnumerable<string> StreamAsync(string model, string systemPrompt, string userPrompt,
                                                        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            stream = true,
        };
        var resp = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            var doc = JsonDocument.Parse(line);
            var delta = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
            if (!string.IsNullOrEmpty(delta)) yield return delta;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try { return (await _http.GetAsync("/api/tags", ct)).IsSuccessStatusCode; }
        catch { return false; }
    }
}
```

- [ ] **Step 3: 實作 LlmRouter.cs**

```csharp
// src/VoiceText.Llm/LlmRouter.cs
using VoiceText.Config;

namespace VoiceText.Llm;

public class LlmRouter : ILlmService
{
    private readonly Func<AppSettings> _getSettings;
    private readonly ILlmService _ollama;
    private readonly ILlmService _llamaCpp;

    public LlmRouter(Func<AppSettings> getSettings, ILlmService ollama, ILlmService llamaCpp)
    {
        _getSettings = getSettings;
        _ollama = ollama;
        _llamaCpp = llamaCpp;
    }

    private ILlmService Active => _getSettings().LlmBackend == "LlamaCpp" ? _llamaCpp : _ollama;

    public Task<string> CompleteAsync(string model, string sys, string user, CancellationToken ct = default)
        => Active.CompleteAsync(model, sys, user, ct);
    public IAsyncEnumerable<string> StreamAsync(string model, string sys, string user, CancellationToken ct = default)
        => Active.StreamAsync(model, sys, user, ct);
    public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Active.IsHealthyAsync(ct);
}
```

- [ ] **Step 4: Commit**

```bash
git add src/VoiceText.Llm/ src/VoiceText.Tests/Llm/
git commit -m "feat: add Ollama + llama.cpp LLM clients with router"
```

---

### Task 12: 潤稿與翻譯服務

**Files:**
- Create: `src/VoiceText.Llm/PromptTemplates.cs`
- Create: `src/VoiceText.Llm/PolishService.cs`
- Create: `src/VoiceText.Llm/TranslationService.cs`
- Create: `src/VoiceText.Tests/Llm/PolishServiceTests.cs`

- [ ] **Step 1: 建立 PromptTemplates.cs**

```csharp
// src/VoiceText.Llm/PromptTemplates.cs
namespace VoiceText.Llm;

public static class PromptTemplates
{
    public const string PolishSystemNatural =
        "你是一位專業編輯。請在保留原意的前提下，修正語音轉錄文字的語法、標點和用詞，使其流暢自然。只輸出修正後的文字，不加說明。";

    public const string PolishSystemFormal =
        "你是一位專業文書助理。請將以下語音轉錄文字改寫為正式書面語，修正語法並補充標點符號。只輸出修正後的文字。";

    public const string PolishSystemTechnical =
        "你是一位技術文件編輯。請將語音轉錄的技術內容整理為清晰的技術文件格式，保留術語準確性。只輸出修正後的文字。";

    public const string PolishSystemMeeting =
        "你是一位會議記錄助理。請將語音轉錄整理為結構化的會議摘要，標示重點決議與行動事項。只輸出整理後的文字。";

    public const string TranslationSystemToEnglish =
        "你是一位專業翻譯。請將以下文字翻譯成自然流暢的英文。只輸出翻譯結果，不加說明。";

    public const string TranslationSystemToChinese =
        "You are a professional translator. Translate the following text into natural, fluent Traditional Chinese. Output only the translation.";

    public static string GetPolishSystem(string style) => style switch
    {
        "formal" => PolishSystemFormal,
        "technical" => PolishSystemTechnical,
        "meeting" => PolishSystemMeeting,
        _ => PolishSystemNatural,
    };
}
```

- [ ] **Step 2: 實作 PolishService & TranslationService**

```csharp
// src/VoiceText.Llm/PolishService.cs
using VoiceText.Config;

namespace VoiceText.Llm;

public class PolishService
{
    private readonly ILlmService _llm;
    private readonly Func<AppSettings> _getSettings;

    public PolishService(ILlmService llm, Func<AppSettings> getSettings)
    {
        _llm = llm;
        _getSettings = getSettings;
    }

    public Task<string> PolishAsync(string rawText, CancellationToken ct = default)
    {
        var s = _getSettings();
        var sys = PromptTemplates.GetPolishSystem(s.PolishPromptStyle);
        return _llm.CompleteAsync(s.LlmModelName, sys, rawText, ct);
    }

    public IAsyncEnumerable<string> PolishStreamAsync(string rawText, CancellationToken ct = default)
    {
        var s = _getSettings();
        return _llm.StreamAsync(s.LlmModelName, PromptTemplates.GetPolishSystem(s.PolishPromptStyle), rawText, ct);
    }
}

// src/VoiceText.Llm/TranslationService.cs
using VoiceText.Config;

namespace VoiceText.Llm;

public class TranslationService
{
    private readonly ILlmService _llm;
    private readonly Func<AppSettings> _getSettings;

    public TranslationService(ILlmService llm, Func<AppSettings> getSettings)
    {
        _llm = llm;
        _getSettings = getSettings;
    }

    public Task<string> TranslateToEnglishAsync(string text, CancellationToken ct = default)
    {
        var s = _getSettings();
        return _llm.CompleteAsync(s.LlmModelName, PromptTemplates.TranslationSystemToEnglish, text, ct);
    }

    public Task<string> TranslateToChineseAsync(string text, CancellationToken ct = default)
    {
        var s = _getSettings();
        return _llm.CompleteAsync(s.LlmModelName, PromptTemplates.TranslationSystemToChinese, text, ct);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/VoiceText.Llm/PromptTemplates.cs src/VoiceText.Llm/PolishService.cs src/VoiceText.Llm/TranslationService.cs
git commit -m "feat: add text polish and translation services with prompt templates"
```

---

## Phase 7: 歷史記錄

### Task 13: SQLite 歷史儲存

**Files:**
- Create: `src/VoiceText.Storage/HistoryEntry.cs`
- Create: `src/VoiceText.Storage/IHistoryRepository.cs`
- Create: `src/VoiceText.Storage/HistoryRepository.cs`

- [ ] **Step 1: 實作 Storage 層**

```csharp
// src/VoiceText.Storage/HistoryEntry.cs
namespace VoiceText.Storage;
public record HistoryEntry(
    int Id,
    DateTime CreatedAt,
    string RawText,
    string? PolishedText,
    string? TranslatedText,
    string Language,
    double DurationMs
);

// src/VoiceText.Storage/IHistoryRepository.cs
namespace VoiceText.Storage;
public interface IHistoryRepository
{
    Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int limit = 50);
    Task<int> AddAsync(HistoryEntry entry);
    Task DeleteAsync(int id);
    Task<IReadOnlyList<HistoryEntry>> SearchAsync(string query);
}

// src/VoiceText.Storage/HistoryRepository.cs
using Microsoft.Data.Sqlite;

namespace VoiceText.Storage;

public class HistoryRepository : IHistoryRepository
{
    private readonly string _connectionString;

    public HistoryRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        InitDb();
    }

    private void InitDb()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        conn.CreateCommand().ExecuteNonQuery(
        """
        CREATE TABLE IF NOT EXISTS history (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            created_at TEXT NOT NULL,
            raw_text TEXT NOT NULL,
            polished_text TEXT,
            translated_text TEXT,
            language TEXT NOT NULL DEFAULT '',
            duration_ms REAL NOT NULL DEFAULT 0
        )
        """);
    }

    public async Task<int> AddAsync(HistoryEntry entry)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO history (created_at, raw_text, polished_text, translated_text, language, duration_ms)
            VALUES (@created_at, @raw, @polished, @translated, @lang, @dur);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@created_at", entry.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@raw", entry.RawText);
        cmd.Parameters.AddWithValue("@polished", (object?)entry.PolishedText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@translated", (object?)entry.TranslatedText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lang", entry.Language);
        cmd.Parameters.AddWithValue("@dur", entry.DurationMs);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int limit = 50)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM history ORDER BY created_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadEntries(cmd);
    }

    public async Task<IReadOnlyList<HistoryEntry>> SearchAsync(string query)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM history WHERE raw_text LIKE @q OR polished_text LIKE @q ORDER BY created_at DESC LIMIT 100";
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        return await ReadEntries(cmd);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM history WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<HistoryEntry>> ReadEntries(SqliteCommand cmd)
    {
        var list = new List<HistoryEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new HistoryEntry(
                reader.GetInt32(0),
                DateTime.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetDouble(6)));
        }
        return list;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/VoiceText.Storage/
git commit -m "feat: add SQLite history repository with search"
```

---

## Phase 8: WPF UI

### Task 14: 主視窗（MainWindow）

**Files:**
- Create: `src/VoiceText.App/Views/MainWindow.xaml`
- Create: `src/VoiceText.App/Views/MainWindow.xaml.cs`
- Create: `src/VoiceText.App/ViewModels/MainViewModel.cs`
- Create: `src/VoiceText.App/Controls/RecordButton.xaml`
- Create: `src/VoiceText.App/Controls/WaveformBar.xaml`

- [ ] **Step 1: 實作 MainViewModel**

```csharp
// src/VoiceText.App/ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceText.Asr;
using VoiceText.Audio;
using VoiceText.Llm;
using VoiceText.Storage;

namespace VoiceText.App.ViewModels;

public enum RecordingState { Idle, Recording, Transcribing, Polishing, Done, Error }

public partial class MainViewModel : ObservableObject
{
    private readonly IAudioCaptureService _audio;
    private readonly VadPipeline _vad;
    private readonly IAsrService _asr;
    private readonly PolishService _polish;
    private readonly TranslationService _translation;
    private readonly IHistoryRepository _history;

    [ObservableProperty] private RecordingState _state = RecordingState.Idle;
    [ObservableProperty] private string _rawText = "";
    [ObservableProperty] private string _polishedText = "";
    [ObservableProperty] private string _statusMessage = "準備就緒";
    [ObservableProperty] private float _audioLevel = 0f;
    [ObservableProperty] private bool _isPolishEnabled = true;
    [ObservableProperty] private bool _isTranslateEnabled = false;
    [ObservableProperty] private string _selectedLanguage = "auto";

    public MainViewModel(IAudioCaptureService audio, VadPipeline vad,
                         IAsrService asr, PolishService polish,
                         TranslationService translation, IHistoryRepository history)
    {
        _audio = audio;
        _vad = vad;
        _asr = asr;
        _polish = polish;
        _translation = translation;
        _history = history;
        _vad.SpeechSegmentReady += OnSpeechSegmentReady;
        _audio.ChunkAvailable += (_, chunk) => _vad.Feed(chunk);
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (State == RecordingState.Recording)
            StopRecording();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        State = RecordingState.Recording;
        StatusMessage = "錄音中...";
        RawText = "";
        PolishedText = "";
        _audio.StartCapture();
    }

    private void StopRecording()
    {
        _audio.StopCapture();
        var pending = _vad.FlushIfAny();
        if (pending != null)
            _ = ProcessSegmentAsync(pending);
        else
            State = RecordingState.Idle;
    }

    private async void OnSpeechSegmentReady(object? sender, float[] segment)
    {
        await App.Current.Dispatcher.InvokeAsync(() =>
            _ = ProcessSegmentAsync(segment));
    }

    private async Task ProcessSegmentAsync(float[] segment)
    {
        try
        {
            State = RecordingState.Transcribing;
            StatusMessage = "轉錄中...";
            var result = await _asr.TranscribeAsync(segment, _selectedLanguage == "auto" ? null : _selectedLanguage);
            RawText = result.Text;

            if (_isPolishEnabled)
            {
                State = RecordingState.Polishing;
                StatusMessage = "潤稿中...";
                PolishedText = await _polish.PolishAsync(result.Text);
            }

            await _history.AddAsync(new(0, DateTime.Now, result.Text,
                _isPolishEnabled ? PolishedText : null, null, result.Language, result.DurationMs));

            State = RecordingState.Done;
            StatusMessage = "完成";
        }
        catch (Exception ex)
        {
            State = RecordingState.Error;
            StatusMessage = $"錯誤: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TranslateAsync()
    {
        var source = string.IsNullOrEmpty(PolishedText) ? RawText : PolishedText;
        if (string.IsNullOrEmpty(source)) return;
        StatusMessage = "翻譯中...";
        PolishedText = await _translation.TranslateToEnglishAsync(source);
        StatusMessage = "翻譯完成";
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        var text = string.IsNullOrEmpty(PolishedText) ? RawText : PolishedText;
        if (!string.IsNullOrEmpty(text))
            System.Windows.Clipboard.SetText(text);
    }
}
```

- [ ] **Step 2: 實作主視窗 XAML**

```xml
<!-- src/VoiceText.App/Views/MainWindow.xaml -->
<Window x:Class="VoiceText.App.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:VoiceText.App.ViewModels"
        Title="VoiceText" Height="520" Width="420"
        Background="{StaticResource BackgroundPrimaryBrush}"
        WindowStyle="None" AllowsTransparency="True"
        ResizeMode="CanResize" Topmost="True">

  <Window.Effect>
    <DropShadowEffect BlurRadius="30" ShadowDepth="0" Opacity="0.25"/>
  </Window.Effect>

  <Border CornerRadius="16" Background="{StaticResource BackgroundPrimaryBrush}"
          BorderBrush="{StaticResource SeparatorBrush}" BorderThickness="0.5">
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height="44"/>   <!-- Title bar -->
        <RowDefinition Height="*"/>    <!-- Content -->
        <RowDefinition Height="56"/>   <!-- Toolbar -->
      </Grid.RowDefinitions>

      <!-- Title bar (drag region) -->
      <Grid Grid.Row="0" MouseLeftButtonDown="TitleBar_MouseLeftButtonDown">
        <TextBlock Text="VoiceText" Style="{StaticResource Headline}"
                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
        <Button Content="⚙" HorizontalAlignment="Right" Margin="0,0,12,0"
                Style="{StaticResource AppleButton}" Background="Transparent"
                Foreground="{StaticResource SystemGrayBrush}"
                Command="{Binding OpenSettingsCommand}"/>
        <Button Content="✕" HorizontalAlignment="Right" Margin="0,0,40,0"
                Style="{StaticResource AppleButton}" Background="Transparent"
                Foreground="{StaticResource SystemGrayBrush}"
                Click="CloseButton_Click"/>
      </Grid>

      <!-- Main content -->
      <ScrollViewer Grid.Row="1" Padding="16,0" VerticalScrollBarVisibility="Auto">
        <StackPanel>
          <!-- Record button -->
          <Border HorizontalAlignment="Center" Margin="0,20,0,16">
            <Button Command="{Binding ToggleRecordingCommand}" Width="80" Height="80"
                    Background="{Binding State, Converter={StaticResource StateToColorConverter}}"
                    BorderThickness="0" Cursor="Hand">
              <Button.Template>
                <ControlTemplate TargetType="Button">
                  <Grid>
                    <Ellipse Width="80" Height="80"
                             Fill="{Binding State, Converter={StaticResource StateToColorConverter}}">
                      <Ellipse.Effect>
                        <DropShadowEffect BlurRadius="16" ShadowDepth="0" Opacity="0.3"
                                          Color="{Binding State, Converter={StaticResource StateToShadowColorConverter}}"/>
                      </Ellipse.Effect>
                    </Ellipse>
                    <TextBlock Text="{Binding State, Converter={StaticResource StateToIconConverter}}"
                               FontSize="30" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                  </Grid>
                </ControlTemplate>
              </Button.Template>
            </Button>
          </Border>

          <!-- Status -->
          <TextBlock Text="{Binding StatusMessage}" Style="{StaticResource Caption}"
                     HorizontalAlignment="Center" Margin="0,0,0,16"/>

          <!-- Raw text card -->
          <Border Style="{StaticResource AppleCard}" Margin="0,0,0,8">
            <StackPanel>
              <TextBlock Text="語音轉錄" Style="{StaticResource Caption}" Margin="0,0,0,6"/>
              <TextBox Text="{Binding RawText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                       Style="{StaticResource AppleTextBox}" TextWrapping="Wrap"
                       MinHeight="80" AcceptsReturn="True" IsReadOnly="False"/>
            </StackPanel>
          </Border>

          <!-- Polished text card -->
          <Border Style="{StaticResource AppleCard}" Margin="0,0,0,8"
                  Visibility="{Binding IsPolishEnabled, Converter={StaticResource BoolToVis}}">
            <StackPanel>
              <TextBlock Text="潤稿結果" Style="{StaticResource Caption}" Margin="0,0,0,6"/>
              <TextBox Text="{Binding PolishedText, Mode=TwoWay}"
                       Style="{StaticResource AppleTextBox}" TextWrapping="Wrap"
                       MinHeight="80" AcceptsReturn="True"/>
            </StackPanel>
          </Border>
        </StackPanel>
      </ScrollViewer>

      <!-- Toolbar -->
      <Border Grid.Row="2" Background="{StaticResource BackgroundSecondaryBrush}"
              BorderBrush="{StaticResource SeparatorBrush}" BorderThickness="0,0.5,0,0">
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="8">
          <Button Content="📋 複製" Style="{StaticResource AppleButton}"
                  Background="{StaticResource BackgroundTertiaryBrush}"
                  Foreground="{StaticResource LabelPrimaryBrush}"
                  Command="{Binding CopyToClipboardCommand}"/>
          <Button Content="🌐 翻譯" Style="{StaticResource AppleButton}"
                  Background="{StaticResource BackgroundTertiaryBrush}"
                  Foreground="{StaticResource LabelPrimaryBrush}"
                  Command="{Binding TranslateCommand}"/>
          <Button Content="🕐 歷史" Style="{StaticResource AppleButton}"
                  Background="{StaticResource BackgroundTertiaryBrush}"
                  Foreground="{StaticResource LabelPrimaryBrush}"
                  Command="{Binding OpenHistoryCommand}"/>
        </StackPanel>
      </Border>
    </Grid>
  </Border>
</Window>
```

- [ ] **Step 3: Commit**

```bash
git add src/VoiceText.App/Views/MainWindow.xaml src/VoiceText.App/ViewModels/MainViewModel.cs
git commit -m "feat: add main window UI and recording state machine ViewModel"
```

---

### Task 15: 設定視窗

**Files:**
- Create: `src/VoiceText.App/Views/SettingsWindow.xaml`
- Create: `src/VoiceText.App/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: 實作 SettingsViewModel**

```csharp
// src/VoiceText.App/ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceText.Config;

namespace VoiceText.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ApiKeyStore _keyStore;
    private readonly MicrophoneEnumerator _micEnum;

    [ObservableProperty] private string _asrServerHost = "127.0.0.1";
    [ObservableProperty] private int _asrServerPort = 8765;
    [ObservableProperty] private string _asrModelId = "Qwen/Qwen3-ASR-0.6B";
    [ObservableProperty] private string _llmBackend = "Ollama";
    [ObservableProperty] private string _ollamaBaseUrl = "http://localhost:11434";
    [ObservableProperty] private string _llamaCppBaseUrl = "http://localhost:8080";
    [ObservableProperty] private string _llmModelName = "llama3.2";
    [ObservableProperty] private string _polishPromptStyle = "natural";
    [ObservableProperty] private double _vadSilenceTimeoutMs = 1500;
    [ObservableProperty] private bool _autoCopyToClipboard = true;
    [ObservableProperty] private string _globalHotkey = "Alt+Shift+V";
    [ObservableProperty] private IReadOnlyList<(string Id, string Name)> _microphones = [];
    [ObservableProperty] private string _selectedMicrophoneId = "";

    public IReadOnlyList<string> LlmBackends { get; } = ["Ollama", "LlamaCpp"];
    public IReadOnlyList<string> PolishStyles { get; } = ["natural", "formal", "technical", "meeting"];
    public IReadOnlyList<string> AsrModels { get; } = ["Qwen/Qwen3-ASR-0.6B", "Qwen/Qwen3-ASR-1.7B"];

    public SettingsViewModel(SettingsService settingsService, ApiKeyStore keyStore, MicrophoneEnumerator micEnum)
    {
        _settingsService = settingsService;
        _keyStore = keyStore;
        _micEnum = micEnum;
        LoadFromSettings(settingsService.Load());
        Microphones = micEnum.GetDevices();
    }

    private void LoadFromSettings(AppSettings s)
    {
        AsrServerHost = s.AsrServerHost;
        AsrServerPort = s.AsrServerPort;
        AsrModelId = s.AsrModelId;
        LlmBackend = s.LlmBackend;
        OllamaBaseUrl = s.OllamaBaseUrl;
        LlamaCppBaseUrl = s.LlamaCppBaseUrl;
        LlmModelName = s.LlmModelName;
        PolishPromptStyle = s.PolishPromptStyle;
        VadSilenceTimeoutMs = s.VadSilenceTimeoutMs;
        AutoCopyToClipboard = s.AutoCopyToClipboard;
        GlobalHotkey = s.GlobalHotkey;
        SelectedMicrophoneId = s.MicrophoneDeviceId;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new AppSettings
        {
            AsrServerHost = AsrServerHost,
            AsrServerPort = AsrServerPort,
            AsrModelId = AsrModelId,
            LlmBackend = LlmBackend,
            OllamaBaseUrl = OllamaBaseUrl,
            LlamaCppBaseUrl = LlamaCppBaseUrl,
            LlmModelName = LlmModelName,
            PolishPromptStyle = PolishPromptStyle,
            VadSilenceTimeoutMs = VadSilenceTimeoutMs,
            AutoCopyToClipboard = AutoCopyToClipboard,
            GlobalHotkey = GlobalHotkey,
            MicrophoneDeviceId = SelectedMicrophoneId,
        };
        _settingsService.Save(settings);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/VoiceText.App/ViewModels/SettingsViewModel.cs
git commit -m "feat: add settings ViewModel with all configurable parameters"
```

---

## Phase 9: 全局熱鍵與系統托盤

### Task 16: 全局熱鍵

**Files:**
- Create: `src/VoiceText.App/Helpers/GlobalHotkeyHelper.cs`

```csharp
// src/VoiceText.App/Helpers/GlobalHotkeyHelper.cs
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VoiceText.App.Helpers;

public class GlobalHotkeyHelper : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HotkeyId = 9001;
    private IntPtr _hwnd;
    private HwndSource? _source;

    public event EventHandler? HotkeyPressed;

    public void Register(Window window, uint modifiers, uint vk)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
        RegisterHotKey(_hwnd, HotkeyId, modifiers, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_hwnd, HotkeyId);
        _source?.RemoveHook(WndProc);
    }
}

// Modifier constants
public static class HotkeyModifiers
{
    public const uint Alt = 0x0001;
    public const uint Ctrl = 0x0002;
    public const uint Shift = 0x0004;
    public const uint Win = 0x0008;
}
```

- [ ] **Step 1: Commit**

```bash
git add src/VoiceText.App/Helpers/GlobalHotkeyHelper.cs
git commit -m "feat: add Win32 global hotkey registration helper"
```

---

### Task 17: App.xaml.cs DI 容器組裝

**Files:**
- Modify: `src/VoiceText.App/App.xaml.cs`

- [ ] **Step 1: 組裝完整 DI 容器**

```csharp
// src/VoiceText.App/App.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using VoiceText.App.ViewModels;
using VoiceText.App.Views;
using VoiceText.Asr;
using VoiceText.Audio;
using VoiceText.Config;
using VoiceText.Llm;
using VoiceText.Storage;

namespace VoiceText.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceText", "settings.json");
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceText", "history.db");

        var settingsService = new SettingsService(settingsPath);
        var settings = settingsService.Load();

        var services = new ServiceCollection();

        // Config
        services.AddSingleton(settingsService);
        services.AddSingleton<ApiKeyStore>();
        services.AddSingleton<MicrophoneEnumerator>();
        services.AddSingleton<Func<AppSettings>>(() => settingsService.Load());

        // Audio
        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        var vadModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "silero_vad.onnx");
        services.AddSingleton(_ => new VadEngine(vadModelPath));
        services.AddSingleton(sp => new VadPipeline(
            sp.GetRequiredService<VadEngine>(),
            settingsService.Load().VadSilenceTimeoutMs));

        // ASR
        services.AddHttpClient<IAsrService, QwenAsrHttpService>(c =>
            c.BaseAddress = new Uri($"http://{settings.AsrServerHost}:{settings.AsrServerPort}"));
        services.AddSingleton(sp => new AsrServerManager(
            "python",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "asr_server"),
            settings.AsrServerPort));

        // LLM
        services.AddHttpClient<OllamaService>(c => c.BaseAddress = new Uri(settings.OllamaBaseUrl));
        services.AddHttpClient<LlamaCppService>(c => c.BaseAddress = new Uri(settings.LlamaCppBaseUrl));
        services.AddSingleton<ILlmService>(sp => new LlmRouter(
            () => settingsService.Load(),
            sp.GetRequiredService<OllamaService>(),
            sp.GetRequiredService<LlamaCppService>()));
        services.AddSingleton<PolishService>();
        services.AddSingleton<TranslationService>();

        // Storage
        services.AddSingleton<IHistoryRepository>(_ => new HistoryRepository(dbPath));

        // ViewModels & Views
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainWindow>();

        Services = services.BuildServiceProvider();

        // Start ASR server
        var serverManager = Services.GetRequiredService<AsrServerManager>();
        serverManager.Start();

        var main = Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.GetService<AsrServerManager>()?.Stop();
        base.OnExit(e);
    }
}
```

- [ ] **Step 2: 完整 build 驗證**

```bash
dotnet build src/VoiceText.sln
```

- [ ] **Step 3: Commit**

```bash
git add src/VoiceText.App/App.xaml.cs
git commit -m "feat: wire DI container, auto-start ASR server on app launch"
```

---

## Phase 10: 整合測試與收尾

### Task 18: 端對端煙霧測試

- [ ] **Step 1: 啟動 Python ASR server**

```bash
cd asr_server
ASR_MODEL_ID=Qwen/Qwen3-ASR-0.6B python -m asr_server.main
```

- [ ] **Step 2: 驗證 health endpoint**

```bash
curl http://127.0.0.1:8765/health
# Expected: {"status":"ready","model_id":"Qwen/Qwen3-ASR-0.6B","device":"cuda"}
```

- [ ] **Step 3: 啟動 C# 應用程式**

```bash
cd src
dotnet run --project VoiceText.App
```

- [ ] **Step 4: 手動測試流程**
  1. 點選錄音按鈕，說一段話
  2. 靜默 1.5 秒後應自動送出
  3. 確認轉錄文字出現
  4. 確認潤稿文字出現
  5. 點選翻譯，確認英文出現
  6. 點選複製，貼上驗證

- [ ] **Step 5: 執行所有測試**

```bash
dotnet test src/VoiceText.sln
python -m pytest asr_server/tests/ -v
```

- [ ] **Step 6: Final commit**

```bash
git add .
git commit -m "feat: complete VoiceText v1.0 — ASR + polish + translation + Apple UI"
```

---

## 附錄：快速啟動指南

### 先決條件

```bash
# Python 環境
python -m venv .venv && source .venv/bin/activate  # Windows: .venv\Scripts\activate
pip install -r asr_server/requirements.txt

# 下載 Silero VAD 模型
curl -L https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx \
     -o src/VoiceText.App/Assets/silero_vad.onnx

# 下載 Qwen3-ASR 模型（首次啟動自動下載，或手動預下載）
python -c "from qwen_asr import Qwen3ASRModel; Qwen3ASRModel.from_pretrained('Qwen/Qwen3-ASR-0.6B')"

# Ollama（選擇性）
ollama pull llama3.2
```

### 設定檔位置

| 檔案 | 路徑 |
|------|------|
| 應用程式設定 | `%APPDATA%\VoiceText\settings.json` |
| API Keys（DPAPI 加密） | `%APPDATA%\VoiceText\keys\default.json` |
| 歷史記錄 | `%APPDATA%\VoiceText\history.db` |
