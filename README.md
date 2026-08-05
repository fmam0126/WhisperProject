# Whisper AutoSubtitle

A tool to generate translated subtitles for videos using OpenAI Whisper. It processes video and audio files from a configured input folder (or a single file in the GUI), converts them to audio, transcribes the audio using Whisper, translates the transcription to a desired language, and saves the subtitles in SRT format next to the original video file.

Available as both a **cross-platform desktop GUI** (Avalonia) and a **console/CLI application**.

## Project Structure

The solution (`WhisperProject.sln`) contains three projects targeting **.NET 10**:

| Project                        | Description                                                                                         |
| ------------------------------ | --------------------------------------------------------------------------------------------------- |
| `WhisperProject.Core`          | Console app and shared core library — transcription pipeline, LLM translation, file conversion      |
| `WhisperProject.Avalonia`      | Cross-platform desktop GUI built with [Avalonia UI](https://avaloniaui.net/)                        |
| `WhisperProject.Tests`         | Unit tests (xUnit) covering Core and Avalonia layers                                                |

## Features

- **Desktop GUI** with native file/folder picker, real-time progress log, and cancellation support.
- **Batch folder processing** — scan a folder (and subdirectories) for all supported media files.
- **Single-file mode** — process one video or audio file at a time (GUI only).
- Converts video/audio files (`.mp4`, `.mkv`, `.mp3`) to 16 kHz mono WAV audio using FFmpeg.
- Transcribes audio using a local Whisper model ([Whisper.net](https://github.com/sandrohanea/whisper.net) 1.9.1).
- **Voice Activity Detection (VAD)** using Silero VAD — detects speech segments, removes silence, and remaps timestamps back to the original timeline for accurate subtitles.
- **Voice enhancement** using DpdfNet (sherpa.onnx) — AI-based speech denoising to isolate speech frequencies before transcription.
- Translates the transcription to any target language via an OpenAI-compatible LLM API (Ollama, LM Studio, llama.cpp, etc.).
- **Context-aware batch translation** — sends multiple subtitle entries to the LLM at once with surrounding context for better translation quality.
- **Per-line translation mode** — translates each subtitle line independently.
- Saves subtitles in SRT format next to the original video file (e.g., `video.EN.srt`).
- **Auto-download** of Whisper model, VAD model, and DpdfNet model when missing.
- **Retry policies** with [Polly](https://github.com/App-vNext/Polly) for robust LLM API communication.
- **Reasoning-model support** — automatically strips `<think>` tags from LLM responses (for models like DeepSeek-R1, QwQ, etc.).
- **Cancellation support** during processing (GUI).

## Prerequisites

- [.NET 10 SDK or Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- [FFmpeg](https://ffmpeg.org/download.html) installed and available in the system `PATH`

## Quick Start (GUI)

1. Download the latest release from the [Releases](https://github.com/fmam0126/WhisperProject/releases) page, or build from source:
   ```bash
   git clone https://github.com/fmam0126/WhisperProject.git
   cd WhisperProject
   dotnet run --project WhisperProject.Avalonia
   ```
2. Choose **Single File** or **Folder (batch)** mode.
3. Click **Browse…** to select a media file or folder.
4. Click **Options…** to configure the LLM endpoint, Whisper model, VAD, voice enhancement, etc.
5. Click **Start Transcription** to begin processing.
6. Watch progress in the output log. Click **Cancel** to stop at any time.

Settings in the Options dialog are saved to and loaded from `appsettings.json` next to the executable.

## Quick Start (CLI)

1. Clone the repository and navigate to the project folder:
   ```bash
   git clone https://github.com/fmam0126/WhisperProject.git
   cd WhisperProject
   ```
2. Create an `appsettings.json` in `WhisperProject.Core/` based on the example below (or copy `appsettings.EXAMPLE.json`).
3. Ensure FFmpeg is installed and available in the system `PATH`.
4. Run the console app:
   ```bash
   dotnet run --project WhisperProject.Core
   ```

The CLI processes all supported files in the configured `InputPath` folder and outputs translated SRT files next to each original.

## Configuration

Create an `appsettings.json` file in `WhisperProject.Core/` with the following structure (see also `appsettings.EXAMPLE.json`):

```json
{
  "Settings": {
    "InputPath": "PATH_TO_INPUT_FOLDER",
    "Url": "http://127.0.0.1",
    "Port": 1234,
    "TargetLanguage": "en",
    "ApiKey": "YOUR_API_KEY",
    "GptPath": "/v1/chat/completions",
    "GptModel": "model-name",
    "Concurrency": 4,
    "UseContextTranslation": true,
    "ContextSize": 10,
    "SystemPrompt": "You are a helpful assistant for translating video subtitles. You receive text in the format of a subtitle file and you translate it to the target language without adding any comments or explanations, just output the translated text. Always keep the formatting of the original text.",
    "WhisperModelpath": "./ggml-largev2.bin",
    "WhisperLanguage": "auto",
    "UseVoiceActivityDetection": false,
    "ApplyDpdfNet": false,
    "DpdfNetModelPath": "./dpdfnet8.onnx",
    "DpdfNetDownloadUrl": "https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/dpdfnet8.onnx"
  }
}
```

### Settings Reference

| Setting                     | Type    | Default           | Description                                                                                                   |
| --------------------------- | ------- | ----------------- | ------------------------------------------------------------------------------------------------------------- |
| `InputPath`                 | string  | _(required)_      | Absolute path to the folder containing video/audio files to process (CLI). Overridden by file/folder picker in GUI. |
| `Url`                       | string  | `http://127.0.0.1`| Base URL of the OpenAI-compatible LLM API (e.g., Ollama, LM Studio, llama.cpp).                               |
| `Port`                      | int     | `1234`            | Port number for the LLM API endpoint.                                                                         |
| `TargetLanguage`            | string  | _(required)_      | Language for translation output (e.g., `"English"`, `"Norwegian"`, `"Spanish"` — can be a full name or ISO 639-1 code). |
| `ApiKey`                    | string  | `""`              | API key for LLM authentication. Leave empty for local LLMs that don't require one.                            |
| `GptPath`                   | string  | `/v1/chat/completions` | API base path. The `/chat/completions` suffix is stripped — the OpenAI SDK appends it automatically.    |
| `GptModel`                  | string  | _(required)_      | Name of the LLM model to use (e.g., `"gpt-4o"`, `"llama3.1"`, `"gemma2"`).                                   |
| `Concurrency`               | uint    | `4`               | Maximum number of parallel LLM translation requests.                                                          |
| `UseContextTranslation`     | bool    | `true`            | When `true`, sends batches of `ContextSize` subtitle entries to the LLM for context-aware translation. When `false`, translates each line independently. |
| `ContextSize`               | uint    | `10`              | Number of subtitle entries sent to the LLM per batch for context-aware translation.                           |
| `SystemPrompt`              | string  | _(see example)_   | System prompt instructing the LLM how to perform subtitle translation.                                        |
| `WhisperModelpath`          | string  | `./ggml-largev2.bin` | File path to the local Whisper GGML model. Auto-downloaded if missing.                                    |
| `WhisperLanguage`           | string  | `"auto"`          | Language code for Whisper transcription (e.g., `"en"`, `"no"`), or `"auto"` for automatic detection.         |
| `UseVoiceActivityDetection` | bool    | `false`           | Enable Silero VAD to detect speech segments and filter silence before transcription.                          |
| `ApplyDpdfNet`              | bool    | `false`           | Enable DpdfNet AI speech denoising to improve speech clarity before transcription.                            |
| `DpdfNetModelPath`          | string  | `./dpdfnet8.onnx` | File path to the DpdfNet ONNX model. Auto-downloaded from `DpdfNetDownloadUrl` if missing.                    |
| `DpdfNetDownloadUrl`        | string  | `https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/dpdfnet8.onnx` | URL used to download the DpdfNet model when not found locally. |

## Dependencies

### Core (`WhisperProject.Core`)

| Package                                                                              | Purpose                                      |
| ------------------------------------------------------------------------------------ | -------------------------------------------- |
| [Whisper.net](https://github.com/sandrohanea/whisper.net) 1.9.1                      | Local Whisper transcription                  |
| [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) 5.4.0                         | FFmpeg integration for media conversion      |
| [NAudio](https://github.com/naudio/NAudio) 3.0.0-preview.5                           | Audio processing and filtering               |
| [sherpa.onnx](https://github.com/k2-fsa/sherpa-onnx) 1.13.2                          | DpdfNet AI speech denoising                  |
| [Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI) 1.10.0     | LLM agent abstraction                        |
| [Polly](https://github.com/App-vNext/Polly) 8.6.6                                    | Resilience and retry policies                |
| [SubtitlesParserV2](https://www.nuget.org/packages/SubtitlesParserV2) 2.4.0          | SRT subtitle parsing                         |

### GUI (`WhisperProject.Avalonia`)

| Package                                                                        | Purpose                    |
| ------------------------------------------------------------------------------ | -------------------------- |
| [Avalonia UI](https://avaloniaui.net/) 12.0.2                                  | Cross-platform desktop UI |
| [Avalonia.DiagnosticsSupport](https://www.nuget.org/packages/AvaloniaUI.DiagnosticsSupport) 2.2.1 | Dev tools (Debug only) |

### Tests (`WhisperProject.Tests`)

| Package                                                          | Purpose            |
| ---------------------------------------------------------------- | ------------------ |
| [xUnit](https://xunit.net/) 2.9.3                                | Test framework     |
| [coverlet](https://github.com/coverlet-coverage/coverlet) 6.0.4  | Code coverage      |

## Development

### Build

```bash
dotnet build WhisperProject.sln
```

### Run Tests

```bash
dotnet test WhisperProject.sln
```

### Run the GUI

```bash
dotnet run --project WhisperProject.Avalonia
```

### Run the CLI

```bash
dotnet run --project WhisperProject.Core
```

## CI/CD

This project uses GitHub Actions for continuous integration and delivery:

| Workflow                 | Trigger                  | Actions                                                                                |
| ------------------------ | ------------------------ | -------------------------------------------------------------------------------------- |
| **Build & Test**         | Pull request to `main`   | Restores, builds, and runs tests on Ubuntu with .NET 10                                |
| **Canary Release**       | PR merged to `main` or manual dispatch | Builds, tests, publishes the Avalonia GUI as a self-contained Windows x64 binary, and creates a [canary GitHub Release](https://github.com/fmam0126/WhisperProject/releases) |

Workflows are in [`.github/workflows/`](.github/workflows/).

## How It Works

1. **File discovery** — `FolderParser` recursively finds `.mp4`, `.mkv`, and `.mp3` files.
2. **Conversion** — `FileConverter` uses FFmpeg to convert media to 16 kHz mono WAV (`pcm_s16le`).
3. **Voice enhancement** (optional) — `VoiceEmphasisFilter` applies DpdfNet AI denoising via sherpa.onnx.
4. **Transcription** — `WhisperClient` runs Whisper (ggml-largev2) to produce an SRT file. Optionally uses Silero VAD to detect speech segments and remap timestamps to the original audio timeline.
5. **Translation** — `SubtitleTranslator` sends the SRT to an OpenAI-compatible LLM using the Microsoft Agent Framework (`Microsoft.Agents.AI`), with Polly retry/timeout resilience. Supports context-batch and per-line modes.
6. **Cleanup** — Temporary WAV and SRT files are removed after translation.
