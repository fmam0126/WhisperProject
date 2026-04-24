- make a system that parses files from a watch folder and checks if it already has subtitles. make it filter out file types that isnt mp3 mp4 mkv ✅
- convert the files to a smaller audio format. convert mp4/mkv to mp3. and skip mp3 files use ffmpeg ✅ - Ish. method to create the mp3 files has been made but no filter has been made
- save that converted file temporarily - decide where to save that file. in the project folder or in a folder next to the source file.


- send that audio file to be transcribed by whisper compatible api  - implemented whisper running on the host. decide if its this i want.


- Format the output from whisper.net as a .srt with the correct languagecode - fix languagecode - should be implemented as the language the subtitles are translated to. ✅

- send the transcription to a llm to translate to english.✅


- recieve and save the finished translated subtitle file next to the video file as a srt in FILENAME.LANGUAGECODE.SRT ✅


16.03 
- use semaphore to add concurrency to the translation  - Test if Things are In correct order. Looks okay right now

- look into Parallel.ForEachAsync - newer implementation of what semaphore does??

- Make a Srt Parser to Remove a Dependency - maybe

- add error handling - polly library

- comment code better 

- add usage of args to remove readlines - added appsettings.json 


## Plan: Add Silero VAD with ONNX Runtime

TL;DR: Add a Silero voice activity detector as a preprocessing step before Whisper transcription. Use `Microsoft.ML.OnnxRuntime` to run a Silero VAD ONNX model on mono 16k WAV audio, produce speech segments, and feed those segments to the existing Whisper transcription flow.

Steps
1. Update project dependencies
   - Add `Microsoft.ML.OnnxRuntime` to `WhisperProject.csproj`.
   - Optionally keep `Whisper.net.AllRuntimes` as-is unless a later Whisper version is needed.

2. Extend settings and configuration
   - Add new appsettings keys for `SileroVadModelPath`, `SileroVadThreshold`, `SileroVadMinUtteranceMs`, `SileroVadMaxSilenceMs`, and `SileroVadWindowMs`.
   - Update `class/Settings.cs` to expose the new settings.

3. Improve audio conversion
   - In `class/FileConverter.cs`, ensure WAV export is 16kHz mono audio by adding `WithAudioChannels(1)` or equivalent.
   - Keep the temp audio directory behavior but ensure output is ready for Silero and Whisper.

4. Add a Silero VAD service class
   - Create `class/SileroVad.cs` or `class/SileroVadService.cs`.
   - Responsibilities:
     - Load the ONNX model from disk.
     - Read WAV audio into a normalized mono float array.
     - Run the ONNX model and interpret output probabilities.
     - Apply thresholding and smoothing to emit speech segments with start/end times.
   - Design segment detection with configurable min utterance and max silence values.
   - Provide a `List<VoiceSegment>` output or a similar segment type.

5. Integrate VAD into the pipeline
   - Modify `Program.cs` to run VAD after WAV conversion and before Whisper transcription.
   - If no speech segments are found, either skip Whisper or fall back to full-file transcription.
   - For each speech segment, transcribe only that segment so `WhisperClient` works on shorter audio chunks.
   - Optionally, create temporary segment WAV files or extend `WhisperClient` to accept a trimmed audio stream.

6. Adjust `WhisperClient.cs` if needed
   - Keep the existing transcription logic, but add a new method to transcribe segments or multiple files in sequence.
   - Ensure segment boundaries are used for SRT timing.
   - Preserve the existing SRT writer behavior, now using VAD segment timestamps.

7. Add model management
   - Add a small helper that verifies the `silero_vad.onnx` model exists at `SileroVadModelPath`.
   - Optionally include a download step if the file is missing, or document that the model must be placed in that path.

8. Testing and verification
   - Run `dotnet build`.
   - Test with a sample folder containing audio/video.
   - Verify VAD segments are detected and transcription still produces valid `.srt` output.
   - Check that the VAD model can load and that speech segments are produced before Whisper runs.
   - Confirm there are no audio format mismatches: WAV should be mono 16k.

Relevant files
- `WhisperProject.csproj` — add ONNX runtime package reference.
- `appsettings.json` — configure Silero VAD settings and model path.
- `class/Settings.cs` — expose VAD config.
- `class/FileConverter.cs` — ensure mono 16k WAV output.
- `Program.cs` — orchestrate conversion, VAD, and transcription.
- `WhisperClient.cs` — add segment-aware transcription support.
- `class/SileroVadService.cs` — new ONNX inference and speech segmentation logic.

Verification
1. Build with `dotnet build` in the project root.
2. Run the app on a sample input folder and confirm it generates `.srt` output.
3. Check that the VAD model can load and that speech segments are produced before Whisper runs.
4. Confirm there are no audio format mismatches: WAV should be mono 16k.

Decisions
- Use ONNX Runtime CPU inference first for compatibility on Windows.
- Keep Whisper transcription mostly unchanged and add VAD as a preprocessing layer.
- Do not replace the existing Whisper model or transcription library unless strictly required.

Further Considerations
1. If you want a tighter implementation, add an audio slicing helper that trims WAV audio in memory rather than writing many temp files.
2. If the current `Whisper.net.AllRuntimes` package is too old later, update it separately after VAD integration.
