using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Nomnom.LCProjectPatcher.Editor.Modules
{
    public static class FFmpegModule
    {
        private static bool FFmpegInPath()
        {
            var envPath = Environment.GetEnvironmentVariable("PATH");
            if (envPath is null)
                return false;
            
            // Yes, I'm ignoring windows, you generally don't need to re-encode on windows to get videos working in Unity - Rune
            var ffmpegPath = envPath.Split(';', ':')
                .Select(envVar => Path.Combine(envVar, "ffmpeg"))
                .Where(File.Exists)
                .FirstOrDefault();
            
            var ffprobePath = envPath.Split(';', ':')
                .Select(envVar => Path.Combine(envVar, "ffprobe"))
                .Where(File.Exists)
                .FirstOrDefault();

            return !string.IsNullOrWhiteSpace(ffmpegPath) && !string.IsNullOrWhiteSpace(ffprobePath);
        }

        private static Process CreateFFmpegProcess(string inputFile, string outputFile)
        {
            Debug.Log($"Encoding {inputFile} to vp8 at {outputFile}");
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{inputFile}\" -c:v libvpx -qmin 0 -qmax 50 -crf 5 -b:v 2M -deadline best -c:a libvorbis -y -progress - -nostats \"{outputFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            return process;
        }

        private static float GetDurationOfVideo(string file)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"\"{file}\" -show_entries format=duration -v quiet -of default=noprint_wrappers=1:nokey=1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                process.Start();
                process.WaitForExit();

                var output = process.StandardOutput.ReadToEnd();
                
                return float.Parse(output);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error running ffprobe: {e}");
                return 0;
            }
        }
        
        public static async UniTask ReEncodeVideosForPlatform(LCPatcherSettings settings)
        {
            if (Application.platform != RuntimePlatform.LinuxEditor)
            {
                Debug.Log("No re-encoding necessary on this platform, skipping.");
                return;
            }
            
            // TODO: look into allowing users to provide a custom ffmpeg path.
            if (!FFmpegInPath())
            {
                Debug.LogError("FFmpeg not found in PATH! Skipping video re-encoding!");
                return;
            }
            
            var videosDir = Path.Combine(settings.GetLethalCompanyGamePath(fullPath: true), "Videos");
            
            var videoFilesCount = Directory.GetFiles(videosDir, "*.m4v", SearchOption.AllDirectories).Length;
            var videoIndex = 0;

            foreach (var file in Directory.GetFiles(videosDir, "*", SearchOption.AllDirectories))
            {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                var fileName = Path.GetFileName(file);
                
                if (file.EndsWith(".m4v"))
                {
                    var outputFile = file.Replace(fileName, $"{fileNameWithoutExt}.webm");

                    using var encodeProcess = CreateFFmpegProcess(file, outputFile);

                    try
                    {
                        var totalDuration = GetDurationOfVideo(file);

                        encodeProcess.Start();

                        EditorUtility.DisplayProgressBar($"Re-Encoding Videos ({++videoIndex}/{videoFilesCount})", fileNameWithoutExt, 0);
                        
                        while (!encodeProcess.HasExited)
                        {
                            var line = await encodeProcess.StandardOutput.ReadLineAsync();
                            
                            var progressData = line.Split("\n")
                                .Where(x => line.StartsWith("out_time_ms="))
                                .Select(x => line["out_time_ms=".Length..])
                                .Select(float.Parse)
                                .Select(timeMs => timeMs / 1000000f)
                                .ToArray();

                            if (progressData.Length != 1)
                                continue;

                            var currentTime = progressData[0];
                            
                            var progress = Mathf.Min(currentTime / totalDuration, 1f);

                            EditorUtility.DisplayProgressBar($"Re-Encoding Videos ({videoIndex}/{videoFilesCount})", fileNameWithoutExt, progress);
                            
                            await UniTask.Yield();
                        }
                        
                        if (encodeProcess.ExitCode != 0) {
                            throw new Exception($"ffmpeg failed to run with exit code {encodeProcess.ExitCode}. Error: {encodeProcess.StandardError.ReadToEnd()}");
                        }
                        
                        File.Delete(file);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error running ffmpeg: {e}");
                    }
                    
                    EditorUtility.ClearProgressBar();
                    continue;
                }

                if (file.EndsWith(".m4v.meta"))
                {
                    try
                    {
                        File.Move(file, file.Replace(".m4v.meta", ".webm.meta"));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error while renaming file: {fileName} to: {fileName.Replace(".m4v.meta", ".webm.meta")}\n{e}");
                    }
                }
            }
        }
    }
}