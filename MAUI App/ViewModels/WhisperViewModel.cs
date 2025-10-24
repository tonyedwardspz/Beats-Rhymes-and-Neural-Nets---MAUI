
namespace MAUI_App.ViewModels;

public class WhisperPageViewModel : BaseViewModel
{
	readonly IAudioManager audioManager;
	readonly IDispatcher dispatcher;
	readonly IWhisperApiService whisperApiService;
	IAudioRecorder? audioRecorder;
	IAudioSource? audioSource = null;
	AsyncAudioPlayer? audioPlayer;

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
			var result = await whisperApiService.TranscribeWavAsync(audioStream, "recording.wav");
			
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
}