### Whisper AutoSubtitle

A tool to generate subtitles for videos using OpenAI Whisper. It processes video and audio files from a configured input folder, converts them to audio if necessary, transcribes the audio using Whisper, translates the transcription to a desired language, and saves the subtitles in SRT format next to the original video file.

#### Features

- Processes video and audio files (mp4, mkv, mp3) from a specified input folder.
- Converts video files to WAV audio format using FFmpeg.
- Transcribes audio using a local Whisper model (Whisper.net 1.9.1).
- Voice Activity Detection (VAD) for improved transcription accuracy on files with silence or background noise.
- Voice enhancement using DpdfNet (sherpa.onnx) to isolate speech frequencies before transcription.
- Translates the transcription to a target language via an LLM API.
- Saves subtitles in SRT format next to the original video file.
- Retry policies with Polly for robust LLM API communication.

#### Configuration

Create an `appsettings.json` file in the project root with the following structure:

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
    "WhisperModelpath": "./ggml-largev2.bin",
    "WhisperLanguage": "auto",
    "UseVoiceActivityDetection": false,
    "ApplyDpdfNet": false,
    "DpdfNetModelPath": "./dpdfnet8.onnx",
    "DpdfNetDownloadUrl": "https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/dpdfnet8.onnx"
  }
}
```

**Settings explained:**

| Setting                     | Description                                                                          |
| --------------------------- | ------------------------------------------------------------------------------------ |
| `InputPath`                 | Absolute path to the folder containing video/audio files to process.                 |
| `Url`                       | Base URL of the LLM API used for translating transcriptions.                         |
| `Port`                      | Port number for the LLM API endpoint.                                                |
| `TargetLanguage`            | ISO 639-1 language code for the desired translation output (e.g., "en", "no", "es"). |
| `ApiKey`                    | API key for authenticating with the LLM service.                                     |
| `GptPath`                   | API endpoint path for chat completions (e.g., "/v1/chat/completions").               |
| `GptModel`                  | Name of the LLM model to use for translation.                                        |
| `WhisperModelpath`          | File path to the local Whisper model binary.                                         |
| `WhisperLanguage`           | Language code for Whisper transcription, or "auto" for automatic detection.          |
| `UseVoiceActivityDetection` | Enable VAD to detect speech segments before transcription (`true`/`false`).          |
| `ApplyDpdfNet`              | Enable DpdfNet voice enhancement to improve speech clarity (`true`/`false`).         |
| `DpdfNetModelPath`          | Path to the DpdfNet ONNX model file for voice enhancement.                           |
| `DpdfNetDownloadUrl`        | URL to download the DpdfNet model if not found locally.                              |

#### Dependencies

- [Whisper.net](https://github.com/sandrohanea/whisper.net) 1.9.1 - Local Whisper transcription
- [NAudio](https://github.com/naudio/NAudio) - Audio processing and filtering
- [sherpa.onnx](https://github.com/k2-fsa/sherpa-onnx) - DpdfNet voice enhancement
- [Polly](https://github.com/App-vNext/Polly) - Resilience and retry policies for HTTP calls
- [SubtitlesParserV2](https://www.nuget.org/packages/SubtitlesParserV2) - SRT subtitle parsing

#### Usage

1. Clone the repository and navigate to the project folder.
2. Create an `appsettings.json` file based on the example above.
3. Ensure FFmpeg is installed and available in the system PATH.
4. Obtain a Whisper model file and update `WhisperModelpath` in the settings.
5. (Optional) Obtain a DpdfNet ONNX model if you want to use voice enhancement.
6. Run the project to process files in the input folder.
