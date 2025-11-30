using Godot;
using System;
using System.Collections.Generic;

public partial class Settings : Control
{
	// --- STAŁE ---
	private const string SAVE_PATH = "user://settings.cfg";
	private const float MIN_DB = -80.0f; // Wartość ciszy w decybelach

	// --- ELEMENTY UI (Przyciski i kontenery) ---
	private Button _backButton;
	private Button _saveButton;

	// --- ELEMENTY UI (Audio) ---
	private HSlider _masterVolumeSlider;
	private HSlider _musicVolumeSlider;
	private HSlider _sfxVolumeSlider;
	private CheckButton _mutedCheckBox;

	// --- ELEMENTY UI (Wideo) ---
	private OptionButton _resolutionOptionButton;
	private OptionButton _screenModeOptionButton;
	private HSlider _scaleUISlider;

	// --- DANE (Cache indeksów audio dla wydajności) ---
	private int _busIndexMaster;
	private int _busIndexMusic;
	private int _busIndexSFX;

	// Lista dostępnych rozdzielczości
	private readonly List<Vector2I> _availableResolutions = new List<Vector2I>
	{
		new Vector2I(3840, 2160), new Vector2I(3440, 1440),
		new Vector2I(2560, 1440), new Vector2I(1920, 1200),
		new Vector2I(1920, 1080), new Vector2I(1600, 900),
		new Vector2I(1366, 768),  new Vector2I(1280, 720)
	};

	// --- KLASY DANYCH (Struktura pliku zapisu) ---
	private class SoundSettings
	{
		public float MasterVolume { get; set; } = 1.0f;
		public float MusicVolume { get; set; } = 1.0f;
		public float SFXVolume { get; set; } = 1.0f;
		public bool Muted { get; set; } = false;
	}

	private class VideoSettings
	{
		public int DisplayMode { get; set; } = 0;      // 0 = Okno, 1 = Fullscreen
		public int ResolutionIndex { get; set; } = 4; // Domyślnie 1920x1080
		public float UIScale { get; set; } = 1.0f;
		public bool VSync { get; set; } = true;
	}

	private class ConfigData
	{
		public SoundSettings Sound { get; set; } = new SoundSettings();
		public VideoSettings Video { get; set; } = new VideoSettings();
	}

	// Główny obiekt trzymający stan ustawień
	private ConfigData _configData = new ConfigData();

	// =================================================================
	// METODY STARTOWE
	// =================================================================

	public override void _Ready()
	{
		GD.Print("⚙️ Inicjalizacja ustawień...");

		// 1. Pobieranie indeksów audio (żeby nie szukać ich co klatkę)
		_busIndexMaster = AudioServer.GetBusIndex("Master");
		_busIndexMusic = AudioServer.GetBusIndex("Music");
		_busIndexSFX = AudioServer.GetBusIndex("SFX");

		// 2. Przypisanie węzłów UI
		AssignNodes();

		// 3. Wypełnienie list rozwijanych (Dropdownów)
		SetupVideoOptions();

		// 4. Ładowanie ustawień z pliku
		LoadSettings();

		// 5. Aktualizacja wyglądu UI na podstawie załadowanych danych
		UpdateUI();

		// 6. Podłączenie sygnałów (zdarzeń)
		ConnectSignals();

		GD.Print("✅ Ustawienia gotowe.");
	}

	private void AssignNodes()
	{
		// Używamy try-catch lub sprawdzenia null, żeby gra się nie wywaliła przy zmianie nazw w edytorze
		try 
		{
			_backButton = GetNode<Button>("Control/BackButton");
			_saveButton = GetNode<Button>("SettingsPanel/SettingsCenter/VSettings/SaveButton");

			_masterVolumeSlider = GetNode<HSlider>("SettingsPanel/SettingsCenter/VSettings/TabContainer/Dźwięk/MasterSlider");
			_musicVolumeSlider = GetNode<HSlider>("SettingsPanel/SettingsCenter/VSettings/TabContainer/Dźwięk/MusicSlider");
			_sfxVolumeSlider = GetNode<HSlider>("SettingsPanel/SettingsCenter/VSettings/TabContainer/Dźwięk/SFXSlider");
			_mutedCheckBox = GetNode<CheckButton>("SettingsPanel/SettingsCenter/VSettings/TabContainer/Dźwięk/MuteCheckBox");

			_screenModeOptionButton = GetNode<OptionButton>("SettingsPanel/SettingsCenter/VSettings/TabContainer/Video/ScreenOption");
			_resolutionOptionButton = GetNode<OptionButton>("SettingsPanel/SettingsCenter/VSettings/TabContainer/Video/ResolutionOption");
			_scaleUISlider = GetNode<HSlider>("SettingsPanel/SettingsCenter/VSettings/TabContainer/Video/SliderUI");
		}
		catch (Exception e)
		{
			GD.PrintErr("❌ BŁĄD: Nie znaleziono węzłów UI! Sprawdź ścieżki w skrypcie Settings.cs. " + e.Message);
		}
	}

	private void SetupVideoOptions()
	{
		if (_screenModeOptionButton == null || _resolutionOptionButton == null) return;

		_screenModeOptionButton.Clear();
		_screenModeOptionButton.AddItem("W oknie", 0);
		_screenModeOptionButton.AddItem("Pełny ekran", 1);

		_resolutionOptionButton.Clear();
		for (int i = 0; i < _availableResolutions.Count; i++)
		{
			Vector2I res = _availableResolutions[i];
			_resolutionOptionButton.AddItem($"{res.X} x {res.Y}", i);
		}

		if (_scaleUISlider != null)
		{
			_scaleUISlider.MinValue = 0.5f;
			_scaleUISlider.MaxValue = 1.5f;
			_scaleUISlider.Step = 0.1f;
		}
	}

	private void ConnectSignals()
	{
		// Podłączamy tylko te elementy, które faktycznie znaleziono
		if (_backButton != null) _backButton.Pressed += OnBackButtonPressed;
		if (_saveButton != null) _saveButton.Pressed += SaveSettings;

		if (_masterVolumeSlider != null) _masterVolumeSlider.ValueChanged += (val) => UpdateBusVolume("Master", _busIndexMaster, (float)val);
		if (_musicVolumeSlider != null) _musicVolumeSlider.ValueChanged += (val) => UpdateBusVolume("Music", _busIndexMusic, (float)val);
		if (_sfxVolumeSlider != null) _sfxVolumeSlider.ValueChanged += (val) => UpdateBusVolume("SFX", _busIndexSFX, (float)val);
		
		if (_mutedCheckBox != null) _mutedCheckBox.Toggled += OnMutedToggled;
		
		if (_screenModeOptionButton != null) _screenModeOptionButton.ItemSelected += OnWindowModeSelected;
		if (_resolutionOptionButton != null) _resolutionOptionButton.ItemSelected += OnResolutionSelected;
		if (_scaleUISlider != null) _scaleUISlider.ValueChanged += OnUIScaleChanged;
	}

	// =================================================================
	// LOGIKA ZAPISU I ODCZYTU
	// =================================================================

	private void LoadSettings()
	{
		ConfigFile configFile = new ConfigFile();
		Error err = configFile.Load(SAVE_PATH);

		if (err != Error.Ok)
		{
			GD.Print("⚠️ Brak pliku ustawień. Używam domyślnych.");
			ApplyAllSettings(); // Aplikujemy domyślne
			return;
		}

		// Sekcja Sound
		_configData.Sound.MasterVolume = (float)configFile.GetValue("Sound", "MasterVolume", 1.0f);
		_configData.Sound.MusicVolume = (float)configFile.GetValue("Sound", "MusicVolume", 1.0f);
		_configData.Sound.SFXVolume = (float)configFile.GetValue("Sound", "SFXVolume", 1.0f);
		_configData.Sound.Muted = (bool)configFile.GetValue("Sound", "Muted", false);

		// Sekcja Video
		_configData.Video.DisplayMode = (int)configFile.GetValue("Video", "DisplayMode", 0);
		_configData.Video.ResolutionIndex = (int)configFile.GetValue("Video", "ResolutionIndex", 4);
		_configData.Video.UIScale = (float)configFile.GetValue("Video", "UIScale", 1.0f);
		_configData.Video.VSync = (bool)configFile.GetValue("Video", "VSync", true);

		ApplyAllSettings();
		GD.Print("📂 Ustawienia załadowane.");
	}

	private void SaveSettings()
	{
		ConfigFile configFile = new ConfigFile();

		// Sound
		configFile.SetValue("Sound", "MasterVolume", _configData.Sound.MasterVolume);
		configFile.SetValue("Sound", "MusicVolume", _configData.Sound.MusicVolume);
		configFile.SetValue("Sound", "SFXVolume", _configData.Sound.SFXVolume);
		configFile.SetValue("Sound", "Muted", _configData.Sound.Muted);

		// Video
		configFile.SetValue("Video", "DisplayMode", _configData.Video.DisplayMode);
		configFile.SetValue("Video", "ResolutionIndex", _configData.Video.ResolutionIndex);
		configFile.SetValue("Video", "UIScale", _configData.Video.UIScale);
		configFile.SetValue("Video", "VSync", _configData.Video.VSync);

		configFile.Save(SAVE_PATH);
		GD.Print("💾 Ustawienia zapisane na dysku.");
	}

	// =================================================================
	// AKTUALIZACJA UI
	// =================================================================

	private void UpdateUI()
	{
		if (_masterVolumeSlider != null) _masterVolumeSlider.Value = _configData.Sound.MasterVolume;
		if (_musicVolumeSlider != null) _musicVolumeSlider.Value = _configData.Sound.MusicVolume;
		if (_sfxVolumeSlider != null) _sfxVolumeSlider.Value = _configData.Sound.SFXVolume;
		if (_mutedCheckBox != null) _mutedCheckBox.ButtonPressed = _configData.Sound.Muted;

		if (_screenModeOptionButton != null) _screenModeOptionButton.Selected = _configData.Video.DisplayMode;
		if (_resolutionOptionButton != null) _resolutionOptionButton.Selected = _configData.Video.ResolutionIndex;
		if (_scaleUISlider != null) _scaleUISlider.Value = _configData.Video.UIScale;

		CheckResolutionLock();
	}

	// =================================================================
	// LOGIKA AUDIO
	// =================================================================

	/// <summary>
	/// Uniwersalna funkcja do zmiany głośności
	/// </summary>
	private void UpdateBusVolume(string busName, int busIdx, float linearValue)
	{
		// 1. Zapisujemy w danych
		switch (busName)
		{
			case "Master": _configData.Sound.MasterVolume = linearValue; break;
			case "Music": _configData.Sound.MusicVolume = linearValue; break;
			case "SFX": _configData.Sound.SFXVolume = linearValue; break;
		}

		// 2. Jeśli szyna nie istnieje, przerywamy
		if (busIdx == -1) return;

		// 3. Konwersja (0.0 do 1.0) -> Decybele
		// Mathf.LinearToDb(0) zwraca -Infinity, co może powodować błędy, dlatego używamy sztywnej podłogi (-80db)
		float dbValue = linearValue > 0.001f ? Mathf.LinearToDb(linearValue) : MIN_DB;
		
		AudioServer.SetBusVolumeDb(busIdx, dbValue);
	}

	private void OnMutedToggled(bool pressed)
	{
		_configData.Sound.Muted = pressed;
		if (_busIndexMaster != -1)
		{
			AudioServer.SetBusMute(_busIndexMaster, pressed);
		}
	}

	// =================================================================
	// LOGIKA VIDEO
	// =================================================================

	private void OnWindowModeSelected(long index)
	{
		_configData.Video.DisplayMode = (int)index;
		ApplyWindowMode();
	}

	private void OnResolutionSelected(long index)
	{
		_configData.Video.ResolutionIndex = (int)index;
		ApplyWindowMode(); // Aplikujemy ponownie tryb, żeby zaktualizować rozmiar
	}

	private void OnUIScaleChanged(double value)
	{
		_configData.Video.UIScale = (float)value;
		GetTree().Root.ContentScaleFactor = (float)value;
	}

	// Aplikuje wszystkie ustawienia na raz (np. przy starcie)
	private void ApplyAllSettings()
	{
		// Audio
		UpdateBusVolume("Master", _busIndexMaster, _configData.Sound.MasterVolume);
		UpdateBusVolume("Music", _busIndexMusic, _configData.Sound.MusicVolume);
		UpdateBusVolume("SFX", _busIndexSFX, _configData.Sound.SFXVolume);
		AudioServer.SetBusMute(_busIndexMaster, _configData.Sound.Muted);

		// Video
		GetTree().Root.ContentScaleFactor = _configData.Video.UIScale;
		
		// VSync
		DisplayServer.WindowSetVsyncMode(_configData.Video.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
		
		// Okno
		ApplyWindowMode();
	}

	private void ApplyWindowMode()
	{
		int mode = _configData.Video.DisplayMode;
		
		if (mode == 0) // Windowed
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
			
			// Ustaw rozdzielczość
			if (_configData.Video.ResolutionIndex >= 0 && _configData.Video.ResolutionIndex < _availableResolutions.Count)
			{
				Vector2I size = _availableResolutions[_configData.Video.ResolutionIndex];
				DisplayServer.WindowSetSize(size);
				CenterWindow(); // Centrowanie okna na ekranie
			}
		}
		else if (mode == 1) // Fullscreen
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		}

		CheckResolutionLock();
	}

	private void CenterWindow()
	{
		int screenId = DisplayServer.WindowGetCurrentScreen();
		Vector2I screenSize = DisplayServer.ScreenGetSize(screenId);
		Vector2I windowSize = DisplayServer.WindowGetSize();
		// Prosta matematyka: środek ekranu - połowa szerokości okna
		Vector2I centerPos = (screenSize / 2) - (windowSize / 2);
		DisplayServer.WindowSetPosition(centerPos);
	}

	// Blokuje zmianę rozdzielczości, jeśli jesteśmy w Fullscreenie (bo wtedy nie ma to sensu)
	private void CheckResolutionLock()
	{
		if (_resolutionOptionButton == null) return;

		bool isWindowed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed;
		_resolutionOptionButton.Disabled = !isWindowed;
	}

	// =================================================================
	// NAWIGACJA
	// =================================================================

	private void OnBackButtonPressed()
	{
		GD.Print("🔙 Powrót do menu...");
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu/main.tscn");
	}
}
