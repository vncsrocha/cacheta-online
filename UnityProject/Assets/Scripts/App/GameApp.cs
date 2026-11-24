using System.Collections.Generic;
using UnityEngine;

namespace Cacheta.App
{
    public enum ScreenId
    {
        MainMenu,
        Profile,
        Options,
        CreateRoom,
        JoinRoom,
        Table,
    }

    /// <summary>Uma tela do jogo. As telas só desenham e chamam ações; o estado fica no <see cref="GameApp"/>.</summary>
    public interface IScreen
    {
        void Enter();
        void Exit();
        void Tick();
        void Draw();
    }

    /// <summary>
    /// Ponto de entrada do jogo. É criado sozinho ao apertar Play (em qualquer cena), guarda o perfil,
    /// as opções e a sala atual, e controla qual tela está ativa.
    /// </summary>
    public class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public PlayerProfile Profile { get; private set; }
        public GameSettings Settings { get; private set; }
        public RoomConfig Room { get; set; } = new RoomConfig();
        public Camera Cam { get; private set; }
        public MenuStage Stage { get; private set; }

        private readonly Dictionary<ScreenId, IScreen> _screens = new Dictionary<ScreenId, IScreen>();
        private IScreen _current;
        public ScreenId CurrentId { get; private set; }

        private string _toast;
        private float _toastUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (Instance != null) return;
            new GameObject("Cacheta App").AddComponent<GameApp>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // A cena inicial do Unity (SampleScene) traz câmera e luz; usamos a câmera e desligamos a luz dela,
            // porque cada ambiente traz a sua.
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            Cam.fieldOfView = 50f;
            Cam.nearClipPlane = 0.05f;
            foreach (var l in FindAll<Light>()) l.enabled = false;

            Profile = SaveSystem.LoadProfile();
            Settings = SaveSystem.LoadSettings();
            ApplySettings();

            _screens[ScreenId.MainMenu] = new MainMenuScreen(this);
            _screens[ScreenId.Profile] = new ProfileScreen(this);
            _screens[ScreenId.Options] = new OptionsScreen(this);
            _screens[ScreenId.CreateRoom] = new CreateRoomScreen(this);
            _screens[ScreenId.JoinRoom] = new JoinRoomScreen(this);
            _screens[ScreenId.Table] = new TableScreen(this);

            // Primeira vez: começa criando o personagem.
            Go(SaveSystem.HasProfile ? ScreenId.MainMenu : ScreenId.Profile);
        }

        public void Go(ScreenId id)
        {
            _current?.Exit();
            CurrentId = id;
            _current = _screens[id];

            // O cenário do menu existe em todas as telas, menos na mesa (que monta o próprio mundo).
            if (id == ScreenId.Table)
            {
                Stage?.Destroy();
                Stage = null;
            }
            else if (Stage == null)
            {
                Stage = new MenuStage(this);
            }
            _current.Enter();
        }

        private void Update()
        {
            _current?.Tick();
            Stage?.Tick();
        }

        private void OnGUI()
        {
            UiKit.Begin();
            _current?.Draw();
            if (_toast != null && Time.unscaledTime < _toastUntil)
            {
                var r = new Rect(UiKit.W / 2 - 500, UiKit.H - 200, 1000, 60);
                UiKit.Box(r, new Color(0.55f, 0.12f, 0.1f, 0.92f));
                GUI.Label(r, _toast, UiKit.Center);
            }
        }

        /// <summary>Mensagem curta na parte de baixo da tela.</summary>
        public void Toast(string message, float seconds = 3.5f)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + seconds;
        }

        public void SaveProfile(PlayerProfile p)
        {
            Profile = p.Clone();
            SaveSystem.Save(Profile);
            Stage?.SetAvatar(Profile);
        }

        public void SaveSettings(GameSettings s)
        {
            Settings = s.Clone();
            SaveSystem.Save(Settings);
            ApplySettings();
        }

        public void ApplySettings()
        {
            AudioListener.volume = Mathf.Clamp01(Settings.MasterVolume);
            if (Settings.QualityLevel >= 0 && Settings.QualityLevel < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel(Settings.QualityLevel, true);
            QualitySettings.vSyncCount = Settings.VSync ? 1 : 0;

            var res = Screen.resolutions;
            if (Settings.ResolutionIndex >= 0 && Settings.ResolutionIndex < res.Length)
            {
                var r = res[Settings.ResolutionIndex];
                Screen.SetResolution(r.width, r.height, Settings.Fullscreen);
            }
            else if (Screen.fullScreen != Settings.Fullscreen)
            {
                Screen.fullScreen = Settings.Fullscreen;
            }
            // Música e efeitos: os volumes ficam guardados para quando os sons entrarem (AudioMixer).
        }

        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return FindObjectsOfType<T>();
#endif
        }
    }
}
