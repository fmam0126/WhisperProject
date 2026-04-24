- make a system that parses files from a watch folder and checks if it already has subtitles. make it filter out file types that isnt mp3 mp4 mkv ✅
- convert the files to a smaller audio format. convert mp4/mkv to mp3. and skip mp3 files use ffmpeg ✅ - Ish. method to create the mp3 files has been made but no filter has been made
- save that converted file temporarily - decide where to save that file. in the project folder or in a folder next to the source file.

- send that audio file to be transcribed by whisper compatible api - implemented whisper running on the host. decide if its this i want.

- Format the output from whisper.net as a .srt with the correct languagecode - fix languagecode - should be implemented as the language the subtitles are translated to. ✅

- send the transcription to a llm to translate to english.✅

- recieve and save the finished translated subtitle file next to the video file as a srt in FILENAME.LANGUAGECODE.SRT ✅

  16.03

- use semaphore to add concurrency to the translation - Test if Things are In correct order. Looks okay right now

- look into Parallel.ForEachAsync - newer implementation of what semaphore does??

- Make a Srt Parser to Remove a Dependency - maybe

- add error handling - polly library

- comment code better

- add usage of args to remove readlines - added appsettings.json
