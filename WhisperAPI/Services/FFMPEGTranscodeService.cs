// <copyright file="FFMpegTranscodeService.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// https://github.com/drasticactions/MauiWhisper
// </copyright>


namespace WhisperAPI.Services;

public class FfMpegTranscodeService : ITranscodeService
{
    private string basePath;
    private string? generatedFilename;

    public FfMpegTranscodeService(string? basePath = default, string? generatedFilename = default)
    {
        this.basePath = basePath ?? Path.GetTempPath();
        this.generatedFilename = generatedFilename;
    }

    /// <inheritdoc/>
    public string BasePath => this.basePath;

    /// <inheritdoc/>
    public async Task<string> ProcessFile(string file)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Starting FFMPEG conversion for file: {file}");
            
            // Check if input file exists
            if (!File.Exists(file))
            {
                System.Diagnostics.Debug.WriteLine($"Input file does not exist: {file}");
                return string.Empty;
            }

            // Check if FFMPEG is available
            var ffmpegPath = FFmpeg.ExecutablesPath;
            System.Diagnostics.Debug.WriteLine($"FFMPEG path: {ffmpegPath}");
            
            // Try to test FFMPEG availability by running a simple command
            try
            {
                var testConversion = FFmpeg.Conversions.New();
                System.Diagnostics.Debug.WriteLine("FFMPEG library initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFMPEG library initialization failed: {ex.Message}");
                return string.Empty;
            }

            // Test FFMPEG availability with the input file
            try
            {
                var testInfo = await FFmpeg.GetMediaInfo(file);
                System.Diagnostics.Debug.WriteLine($"Media info retrieved successfully. Duration: {testInfo.Duration}");
                
                if (testInfo.AudioStreams.Count() == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No audio streams found in the file");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get media info: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"This might indicate FFMPEG is not properly installed or the file format is not supported");
                return string.Empty;
            }

            var mediaInfo = await FFmpeg.GetMediaInfo(file);
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();
            if (audioStream is null)
            {
                System.Diagnostics.Debug.WriteLine("No audio stream found in file");
                return string.Empty;
            }

            System.Diagnostics.Debug.WriteLine($"Audio stream found: Codec={audioStream.Codec}, SampleRate={audioStream.SampleRate}, Channels={audioStream.Channels}");

            // Always convert to WAV format with 16kHz sample rate for Whisper compatibility
            var outputfile = Path.Combine(this.basePath, $"{this.generatedFilename ?? Path.GetRandomFileName()}.wav");
            
            System.Diagnostics.Debug.WriteLine($"Converting {file} to {outputfile}");
            
            var conversion = FFmpeg.Conversions.New()
                .AddStream(audioStream)
                .AddParameter("-c pcm_s16le -ar 16000 -ac 1") // Convert to 16kHz mono WAV
                .SetOutput(outputfile)
                .SetOverwriteOutput(true);

            System.Diagnostics.Debug.WriteLine($"Starting conversion with parameters: -c pcm_s16le -ar 16000 -ac 1");
            
            var result = await conversion.Start();

            System.Diagnostics.Debug.WriteLine($"Conversion completed. Result: {result?.ToString() ?? "null"}");
            
            if (result is null)
            {
                System.Diagnostics.Debug.WriteLine("FFMPEG conversion result is null");
                return string.Empty;
            }
            
            // Wait a moment for file system to catch up
            await Task.Delay(100);
            
            // Verify the output file was created and has content
            if (File.Exists(outputfile))
            {
                var fileInfo = new FileInfo(outputfile);
                System.Diagnostics.Debug.WriteLine($"Output file exists: {outputfile}, size: {fileInfo.Length} bytes");
                
                if (fileInfo.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully converted to {outputfile}, size: {fileInfo.Length} bytes");
                    return outputfile;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Output file is empty");
                    return string.Empty;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Output file was not created");
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            // Log the exception for debugging
            System.Diagnostics.Debug.WriteLine($"FFMPEG conversion error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return string.Empty;
        }
    }
}