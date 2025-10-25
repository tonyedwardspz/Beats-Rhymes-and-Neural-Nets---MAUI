

using Microsoft.Maui.Storage;
using Plugin.Maui.Audio;
using System.ComponentModel;
using Microsoft.Maui.Devices;

namespace MAUI_App.ViewModels;

public class WhisperPageViewModel : BaseViewModel
{
	readonly IAudioManager audioManager;
	readonly IDispatcher dispatcher;
	readonly IWhisperApiService whisperApiService;
	IAudioRecorder? audioRecorder;
	IAudioSource? audioSource = null;
	AsyncAudioPlayer? audioPlayer;
	
	// Chunked transcription properties
	private Timer? chunkedTimer;
	private bool isChunkedRecording = false;
	private int chunkCounter = 0;
	private string? chunkedSessionId = null;

	public bool IsRecording
	{
		get => audioRecorder?.IsRecording ?? false;
	}

	private bool isProcessing { get; set; } = false;
	public bool IsProcessing
	{
		get => isProcessing;
		set
		{
			isProcessing = value;
			NotifyPropertyChanged();
			TranscribeFileCommand.ChangeCanExecute();
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
		}
	}

	private string selectedFileName = string.Empty;
	public string SelectedFileName
	{
		get => selectedFileName;
		set
		{
			selectedFileName = value;
			NotifyPropertyChanged();
			NotifyPropertyChanged(nameof(HasSelectedFile));
		}
	}

	public bool HasSelectedFile => !string.IsNullOrEmpty(selectedFileName);

	private string selectedFilePath = string.Empty;

	bool isPlaying = false;
	public bool IsPlaying
	{
		get => isPlaying;
		set
		{
			isPlaying = value;
			PlayCommand.ChangeCanExecute();
			StopPlayCommand.ChangeCanExecute();
		}
	}
	
	public Command StartCommand { get; }
	public Command StopCommand { get; }
	public Command ProcessCommand { get; }
	public Command StopPlayCommand { get; }
	public Command PlayCommand { get; }
	public Command StartChunkedCommand { get; }
	public Command StopChunkedCommand { get; }
	public Command SelectFileCommand { get; }
	public Command TranscribeFileCommand { get; }

	public WhisperPageViewModel(
		IAudioManager audioManager,
		IDispatcher dispatcher,
		IWhisperApiService whisperApiService)
	{
		StartCommand = new Command(Start, () => !IsRecording);
		StopCommand = new Command(Stop, () => IsRecording);
		ProcessCommand = new Command(Process);
		PlayCommand = new Command(PlayAudio, () => !IsPlaying);
		StopPlayCommand = new Command(StopPlay, () => IsPlaying);
		StartChunkedCommand = new Command(StartChunkedTranscription, () => !isChunkedRecording);
		StopChunkedCommand = new Command(StopChunkedTranscription, () => isChunkedRecording);
		SelectFileCommand = new Command(SelectFile);
		TranscribeFileCommand = new Command(TranscribeFile, () => HasSelectedFile && !IsProcessing);


		this.audioManager = audioManager;
		this.dispatcher = dispatcher;
		this.whisperApiService = whisperApiService;
	}

	async void Start()
	{
		if (await CheckPermissionIsGrantedAsync<Microphone>())
		{
			audioRecorder = audioManager.CreateRecorder();
			var options = new AudioRecorderOptions
			{
				Channels = ChannelType.Mono,
				BitDepth = BitDepth.Pcm16bit,
				Encoding = Plugin.Maui.Audio.Encoding.Wav,
				ThrowIfNotSupported = true,
				SampleRate = 16000
			};

			try
			{
				await audioRecorder.StartAsync(options);
			}
			catch
			{
				var res = await AppShell.Current.DisplayActionSheet("Options not supported. Use Default?", "Yes", "No");
				if (res != "Yes")
				{
					return;
				}
				await audioRecorder.StartAsync();
			}
		}
		
		NotifyPropertyChanged(nameof(IsRecording));
		StartCommand.ChangeCanExecute();
		StopCommand.ChangeCanExecute();
	}

	async void Stop()
	{
		audioSource = await audioRecorder.StopAsync();
		
		NotifyPropertyChanged(nameof(IsRecording));
		StartCommand.ChangeCanExecute();
		StopCommand.ChangeCanExecute();
	}

	async void Process()
	{
		if (audioSource == null)
		{
			await AppShell.Current.DisplayAlert("Error", "No audio recorded. Please record audio first.", "OK");
			return;
		}

		IsProcessing = true;
		ProcessCommand.ChangeCanExecute();
		TranscriptionResult = "Processing...";

		try
		{
			// Get the audio stream from the audio source
			using var audioStream = audioSource.GetAudioStream();
			
			// Ensure stream position is at the beginning
			if (audioStream.CanSeek)
			{
				audioStream.Position = 0;
			}
			
			// Send to Whisper API for transcription
			var result = await whisperApiService.TranscribeWavAsync(audioStream, "recording.wav", "File Upload");
			
			if (result.IsSuccess && result.Data != null)
			{
				// Join all transcription results
				TranscriptionResult = string.Join("\n", result.Data.Results);
			}
			else
			{
				TranscriptionResult = $"Transcription failed: {result.ErrorMessage}";
			}
		}
		catch (Exception ex)
		{ 
			TranscriptionResult = $"Error: {ex.Message}";
		}
		finally
		{
			IsProcessing = false;
			ProcessCommand.ChangeCanExecute();
		}
	}

	async void PlayAudio()
	{
		if (audioSource != null)
		{
			audioPlayer = this.audioManager.CreateAsyncPlayer(((FileAudioSource)audioSource).GetAudioStream());

			IsPlaying = true;

			await audioPlayer.PlayAsync(CancellationToken.None);

			IsPlaying = false;
		}
	}

	void StopPlay()
	{
		audioPlayer.Stop();
	}

	async void StartChunkedTranscription()
	{
		if (await CheckPermissionIsGrantedAsync<Microphone>())
		{
			// Clear previous results
			TranscriptionResult = "Starting chunked transcription...\n";
			chunkCounter = 0;
			isChunkedRecording = true;
			chunkedSessionId = Guid.NewGuid().ToString(); // Generate session ID for this chunked session
			
			// Start the timer for 2-second intervals
			chunkedTimer = new Timer(ProcessChunk, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
			
			// Update command states
			StartChunkedCommand.ChangeCanExecute();
			StopChunkedCommand.ChangeCanExecute();
		}
	}

	void StopChunkedTranscription()
	{
		isChunkedRecording = false;
		chunkedTimer?.Dispose();
		chunkedTimer = null;
		// Don't reset session ID here - let the final chunk use it
		
		// Update command states
		StartChunkedCommand.ChangeCanExecute();
		StopChunkedCommand.ChangeCanExecute();
		
		TranscriptionResult += "\n--- Chunked transcription stopped ---";
	}

	async void ProcessChunk(object? state)
	{
		if (!isChunkedRecording) return;

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
				// Process the chunk
				using var audioStream = chunkAudioSource.GetAudioStream();
				if (audioStream.CanSeek)
				{
					audioStream.Position = 0;
				}

				var result = await whisperApiService.TranscribeWavAsync(audioStream, $"chunk_{chunkCounter}.wav", "Streaming", chunkedSessionId);
				
				await dispatcher.DispatchAsync(() =>
				{
					if (result.IsSuccess && result.Data != null && result.Data.Results.Length > 0)
					{
						var timeRange = $"{chunkCounter * 2} - {(chunkCounter + 1) * 2}s";
						var transcription = string.Join(" ", result.Data.Results);
						TranscriptionResult += $"{timeRange} --> {transcription}\n";
					}
					else
					{
						var timeRange = $"{chunkCounter * 2} - {(chunkCounter + 1) * 2}s";
						TranscriptionResult += $"{timeRange} --> [No speech detected]\n";
					}
				});
			}
		}
		catch (Exception ex)
		{
			await dispatcher.DispatchAsync(() =>
			{
				TranscriptionResult += $"Error in chunk {chunkCounter}: {ex.Message}\n";
			});
		}
		finally
		{
			chunkCounter++;
			
			// Reset session ID after the final chunk has been processed
			if (!isChunkedRecording)
			{
				chunkedSessionId = null;
			}
		}
	}

	async void SelectFile()
	{
		try
		{
			// First try with specific audio file types
			var customFileType = new FilePickerFileType(
				new Dictionary<DevicePlatform, IEnumerable<string>>
				{
					{ DevicePlatform.iOS, new[] { "public.audio" } },
					{ DevicePlatform.Android, new[] { "audio/*" } },
					{ DevicePlatform.WinUI, new[] { ".wav", ".mp3", ".m4a", ".aac", ".ogg", ".flac" } },
					{ DevicePlatform.macOS, new[] { "public.audio" } }
				});

			var result = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Select an audio file",
				FileTypes = customFileType
			});

			if (result != null)
			{
				SelectedFileName = result.FileName;
				selectedFilePath = result.FullPath;
				TranscribeFileCommand.ChangeCanExecute();
			}
		}
		catch (Exception ex)
		{
			// If specific audio types fail, try with all files
			try
			{
				var result = await FilePicker.Default.PickAsync(new PickOptions
				{
					PickerTitle = "Select an audio file (all files)"
				});

				if (result != null)
				{
					// Check if the file has an audio extension
					var extension = Path.GetExtension(result.FileName).ToLowerInvariant();
					var audioExtensions = new[] { ".wav", ".mp3", ".m4a", ".aac", ".ogg", ".flac", ".wma" };
					
					if (audioExtensions.Contains(extension))
					{
						SelectedFileName = result.FileName;
						selectedFilePath = result.FullPath;
						TranscribeFileCommand.ChangeCanExecute();
					}
					else
					{
						await AppShell.Current.DisplayAlert("Invalid File", 
							"Please select an audio file (WAV, MP3, M4A, AAC, OGG, FLAC, WMA)", "OK");
					}
				}
			}
			catch (Exception fallbackEx)
			{
				await AppShell.Current.DisplayAlert("Error", 
					$"Failed to select file: {ex.Message}\nFallback also failed: {fallbackEx.Message}", "OK");
			}
		}
	}

	async void TranscribeFile()
	{
		if (string.IsNullOrEmpty(selectedFilePath))
		{
			await AppShell.Current.DisplayAlert("Error", "No file selected. Please select a file first.", "OK");
			return;
		}

		IsProcessing = true;
		TranscribeFileCommand.ChangeCanExecute();
		TranscriptionResult = "Processing file...";

		try
		{
			// Send to Whisper API for transcription
			var result = await whisperApiService.TranscribeFileAsync(selectedFilePath, "File Upload");
			
			if (result.IsSuccess && result.Data != null)
			{
				// Join all transcription results
				TranscriptionResult = string.Join("\n", result.Data.Results);
			}
			else
			{
				TranscriptionResult = $"Transcription failed: {result.ErrorMessage}";
			}
		}
		catch (Exception ex)
		{ 
			TranscriptionResult = $"Error: {ex.Message}";
		}
		finally
		{
			IsProcessing = false;
			TranscribeFileCommand.ChangeCanExecute();
		}
	}
}