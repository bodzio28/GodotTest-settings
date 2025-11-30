using Godot;
using System;

public partial class AudioController : Node
{
	// --- ŚCIEŻKI DO PLIKÓW ---
	private const string PathHover = "res://Sounds/Hover.ogg";
	private const string PathButton = "res://Sounds/Button.ogg";
	private const string PathBgMusic = "res://Sounds/BackGround.mp3";

	private AudioStream _audioHover;
	private AudioStream _audioButton;
	private AudioStream _audioBgMusic;

	// ODTWARZACZE
	private AudioStreamPlayer _musicPlayer;
	private AudioStreamPlayer _sfxHover;
	private AudioStreamPlayer _sfxClick;

	public override void _Ready()
	{
		// Ładowanie
		_audioHover = GD.Load<AudioStream>(PathHover);
		_audioButton = GD.Load<AudioStream>(PathButton);
		_audioBgMusic = GD.Load<AudioStream>(PathBgMusic);

		// Konfiguracja odtwarzaczy
		SetupAudioPlayers();

		// Start muzyki
		PlayMusic();

		// Podłączenie do sygnału drzewa scen
		GetTree().NodeAdded += OnNodeAdded;
	}

	private void SetupAudioPlayers()
	{
		// --- Muzyka ---
		_musicPlayer = new AudioStreamPlayer();
		_musicPlayer.Stream = _audioBgMusic;
		_musicPlayer.VolumeDb = -15.0f;
		_musicPlayer.ProcessMode = ProcessModeEnum.Always; 
		_musicPlayer.Bus = "Music"; 
		AddChild(_musicPlayer);

		// --- CLICK ---
		_sfxClick = new AudioStreamPlayer();
		_sfxClick.Stream = _audioButton;
		_sfxClick.VolumeDb = -5.0f;
		_sfxClick.Bus = "SFX";
		AddChild(_sfxClick);

		// --- HOVER ---
		_sfxHover = new AudioStreamPlayer();
		_sfxHover.Stream = _audioHover;
		_sfxHover.VolumeDb = -10.0f;
		_sfxHover.Bus = "SFX";
		AddChild(_sfxHover);
	}

	private void PlayMusic()
	{
		if (!_musicPlayer.Playing)
		{
			_musicPlayer.Play();
		}
	}

	// Wykrywanie przycisków
	private void OnNodeAdded(Node node)
	{
		if (node is Button || node is TextureButton)
		{
			BaseButton btn = (BaseButton)node;

			if (!btn.IsConnected(Control.SignalName.MouseEntered, Callable.From(PlayHover)))
			{
				btn.MouseEntered += PlayHover;
				btn.Pressed += PlayClick;
			}
		}
	}

	private void PlayHover()
	{
		_sfxHover.PitchScale = (float)GD.RandRange(0.95, 1.05);
		_sfxHover.Play();
	}

	private void PlayClick()
	{
		_sfxClick.Play();
	}
}
