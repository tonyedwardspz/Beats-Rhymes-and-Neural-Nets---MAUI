using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using Plugin.Maui.Audio;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using MAUI_App.Models;
using MAUI_App.Services;
using MAUI_App.Helpers;

namespace MAUI_App.ViewModels;

public class RapModeViewModel : BaseViewModel
{
    readonly IAudioManager audioManager;
    readonly IDispatcher dispatcher;
    readonly IWhisperApiService whisperApiService;
    readonly ILLMApiService llmApiService;
    
    // Chunked transcription properties
    private Timer? chunkedTimer;
    private bool isChunkedRecording = false;
    private int chunkCounter = 0;
    private string? chunkedSessionId = null;
    private readonly ChunkedTranscriptionHelper _chunkedHelper = new(); // Helper for managing chunked transcription

    private string _title = "Rap Mode";
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            NotifyPropertyChanged();
        }
    }

    private string transcriptionResult = string.Empty;
    public string TranscriptionResult
    {
        get => transcriptionResult;
        set
        {
            transcriptionResult = value;
            NotifyPropertyChanged();
            AutoCorrectCommand.ChangeCanExecute();
        }
    }

    public bool IsChunkedRecording => isChunkedRecording;

    // Auto-correction properties
    private string correctedLyrics = string.Empty;
    public string CorrectedLyrics
    {
        get => correctedLyrics;
        set
        {
            correctedLyrics = value;
            NotifyPropertyChanged();
        }
    }

    private bool isCorrecting = false;
    public bool IsCorrecting
    {
        get => isCorrecting;
        set
        {
            isCorrecting = value;
            NotifyPropertyChanged();
            AutoCorrectCommand.ChangeCanExecute();
        }
    }


    // Toggle button properties
    public string ChunkedButtonText => IsChunkedRecording ? "Stop Session" : "Start Session";
    public Color ChunkedButtonColor => IsChunkedRecording ? Colors.Red : Colors.Orange;

    public Command StartChunkedCommand { get; }
    public Command StopChunkedCommand { get; }
    public Command ToggleChunkedCommand { get; }
    public Command AutoCorrectCommand { get; }

    public RapModeViewModel(
        IAudioManager audioManager,
        IDispatcher dispatcher,
        IWhisperApiService whisperApiService,
        ILLMApiService llmApiService)
    {
        StartChunkedCommand = new Command(StartChunkedTranscription, () => !isChunkedRecording);
        StopChunkedCommand = new Command(StopChunkedTranscription, () => isChunkedRecording);
        ToggleChunkedCommand = new Command(ToggleChunked);
        AutoCorrectCommand = new Command(AutoCorrectLyrics, () => !IsCorrecting && !string.IsNullOrWhiteSpace(TranscriptionResult));

        this.audioManager = audioManager;
        this.dispatcher = dispatcher;
        this.whisperApiService = whisperApiService;
        this.llmApiService = llmApiService;
    }

    async void StartChunkedTranscription()
    {
        if (await CheckPermissionIsGrantedAsync<Microphone>())
        {
            // Clear previous results
            TranscriptionResult = "Starting chunked transcription...\n";
            chunkCounter = 0;
            _chunkedHelper.Reset();
            isChunkedRecording = true;
            chunkedSessionId = Guid.NewGuid().ToString(); // Generate session ID for this chunked session
            
            // Start the timer for 2-second intervals
            chunkedTimer = new Timer(ProcessChunk, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            
            // Update command states
            StartChunkedCommand.ChangeCanExecute();
            StopChunkedCommand.ChangeCanExecute();
            NotifyPropertyChanged(nameof(IsChunkedRecording));
            NotifyPropertyChanged(nameof(ChunkedButtonText));
            NotifyPropertyChanged(nameof(ChunkedButtonColor));
        }
    }

    void StopChunkedTranscription()
    {
        isChunkedRecording = false;
        NotifyPropertyChanged(nameof(IsChunkedRecording));
        NotifyPropertyChanged(nameof(ChunkedButtonText));
        NotifyPropertyChanged(nameof(ChunkedButtonColor));
        chunkedTimer?.Dispose();
        chunkedTimer = null;
        // Don't reset session ID here - let the final chunk use it
        
        // Update command states
        StartChunkedCommand.ChangeCanExecute();
        StopChunkedCommand.ChangeCanExecute();
    }

    async void ProcessChunk(object? state)
    {
        if (!isChunkedRecording) return;

        // Capture the current chunk index before incrementing
        int currentChunkIndex = chunkCounter;
        chunkCounter++;

        try
        {
            // Create a new recorder for this chunk
            var chunkRecorder = audioManager.CreateRecorder();
            var options = new AudioRecorderOptions
            {
                Channels = ChannelType.Mono,
                BitDepth = BitDepth.Pcm16bit,
                Encoding = Plugin.Maui.Audio.Encoding.Wav,
                ThrowIfNotSupported = true,
                SampleRate = 16000
            };

            // Record for 2 seconds
            await chunkRecorder.StartAsync(options);
            await Task.Delay(2000); // Record for 2 seconds
            var chunkAudioSource = await chunkRecorder.StopAsync();

            if (chunkAudioSource != null)
            {
                using var audioStream = chunkAudioSource.GetAudioStream();
                if (audioStream.CanSeek)
                {
                    audioStream.Position = 0;
                }
                
                var result = await whisperApiService.TranscribeWavAsync(audioStream, $"chunk_{currentChunkIndex}.wav", "Streaming", chunkedSessionId, currentChunkIndex);
                
                await dispatcher.DispatchAsync(() =>
                {
                    // Extract and store the transcription text
                    var transcriptionText = ChunkedTranscriptionHelper.ExtractTranscriptionText(result);
                    _chunkedHelper.StoreChunkResult(currentChunkIndex, transcriptionText);
                    
                   	// Update the display with all chunks in order
                    TranscriptionResult = _chunkedHelper.BuildTranscriptionDisplay();
                });
            }
        }
        catch (Exception ex)
        {
            await dispatcher.DispatchAsync(() =>
            {
                // Store error in the results dictionary
                _chunkedHelper.StoreChunkResult(currentChunkIndex, $"Error in chunk {currentChunkIndex}: {ex.Message}");
                TranscriptionResult = _chunkedHelper.BuildTranscriptionDisplay();
            });
        }
        finally
        {
            if (!isChunkedRecording)
            {
                chunkedSessionId = null;
            }
        }
    }


    void ToggleChunked()
    {
        if (IsChunkedRecording)
        {
            StopChunkedTranscription();
        }
        else
        {
            StartChunkedTranscription();
        }
    }

    async void AutoCorrectLyrics()
    {
        if (string.IsNullOrWhiteSpace(TranscriptionResult))
        {
            await AppShell.Current.DisplayAlert("Error", "No transcription to correct. Please start a session first.", "OK");
            return;
        }

        IsCorrecting = true;
        CorrectedLyrics = "Correcting lyrics...";

        try
        {
            var promptText = await ReadAutocorrectPromptAsync();
            var fullPrompt = $"{promptText}\n\n{TranscriptionResult}";
            var result = await llmApiService.GenerateResponseAsync(fullPrompt);
            
            if (result.IsSuccess && result.Data != null)
            {
                CorrectedLyrics = result.Data.Response;
            }
            else
            {
                CorrectedLyrics = $"Error correcting lyrics: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            CorrectedLyrics = $"Error: {ex.Message}";
        }
        finally
        {
            IsCorrecting = false;
        }
    }

    private async Task<string> ReadAutocorrectPromptAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("Prompts/autocorrect.md");
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            return "Act as rap transcription editor, correcting mistakes in rap songs that have been transcribed by software. You have a lifetime of experience in writing songs for the best, chart topping artists.\n\nThere are likely to be subtle errors where the model has misheard what has been said. Use the context around potential mistakes to correct them to what they should be.\n\nHere are the transcribed lyrics:\n";
        }
    }


}
