using System;
using Cacheta.Core.Bots;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>Menu inicial: nome do jogo e o seu personagem à esquerda, menu à direita.</summary>
    public class MainMenuScreen : IScreen
    {
        private readonly GameApp _app;
        public MainMenuScreen(GameApp app) => _app = app;

        public void Enter()
        {
            _app.Stage.SetEnvironment(EnvironmentId.QuintalAoEntardecer);
            _app.Stage.SetFraming(MenuStage.Framing.Menu);
        }

        public void Exit() { }
        public void Tick() { }

        public void Draw()
        {
            // Esquerda: título e nome do jogador (o personagem 3D aparece abaixo do título).
            UiKit.Shadowed(new Rect(80, 60, 900, 120), "CACHETA", UiKit.Title, Color.white);
            UiKit.Shadowed(new Rect(86, 170, 900, 50), "online", UiKit.Subtitle, Palette.Gold);
            var nameRect = new Rect(UiKit.W * 0.26f - 200, UiKit.H * 0.78f, 400, 50);
            UiKit.Panel(nameRect, 0.5f);
            GUI.Label(nameRect, _app.Profile.Name, UiKit.Center);

            // Direita: menu.
            float w = 560, x = UiKit.W - w - 90, y = UiKit.H * 0.2f, h = 84, gap = 18;
            UiKit.Panel(new Rect(x - 30, y - 40, w + 60, 6 * (h + gap) + 60), 0.45f);
            if (GUI.Button(new Rect(x, y, w, h), "Jogar Cacheta", UiKit.BigButton)) StartMode(GameMode.CachetaTradicional);
            y += h + gap;
            if (GUI.Button(new Rect(x, y, w, h), "Jogar Cachetex  (novo)", UiKit.BigButton)) StartMode(GameMode.Cachetex);
            y += h + gap;
            if (GUI.Button(new Rect(x, y, w, h), "Entrar em uma sala", UiKit.BigButton)) _app.Go(ScreenId.JoinRoom);
            y += h + gap;
            if (GUI.Button(new Rect(x, y, w, h), "Personalizar perfil", UiKit.BigButton)) _app.Go(ScreenId.Profile);
            y += h + gap;
            if (GUI.Button(new Rect(x, y, w, h), "Opções", UiKit.BigButton)) _app.Go(ScreenId.Options);
            y += h + gap;
            if (GUI.Button(new Rect(x, y, w, h), "Sair", UiKit.BigButton))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            UiKit.Label(new Rect(20, UiKit.H - 40, 900, 30), "Protótipo funcional: arte provisória", UiKit.Tiny, new Color(1, 1, 1, 0.6f));
        }

        private void StartMode(GameMode mode)
        {
            _app.Room = new RoomConfig { Mode = mode, Environment = _app.Room.Environment, Bots = _app.Room.Bots, Difficulty = _app.Room.Difficulty };
            _app.Go(ScreenId.CreateRoom);
        }
    }

    /// <summary>Criar/editar o personagem e o nome.</summary>
    public class ProfileScreen : IScreen
    {
        private readonly GameApp _app;
        private PlayerProfile _edit;
        public ProfileScreen(GameApp app) => _app = app;

        public void Enter()
        {
            _edit = _app.Profile.Clone();
            _app.Stage.SetFraming(MenuStage.Framing.Profile);
        }

        public void Exit() => _app.Stage?.SetAvatar(_app.Profile); // descarta alterações não salvas
        public void Tick() { }

        public void Draw()
        {
            float w = 760, x = UiKit.W - w - 70, y = 70, row = 58, gap = 14;
            UiKit.Panel(new Rect(x - 30, y - 30, w + 60, UiKit.H - 80), 0.6f);
            GUI.Label(new Rect(x, y, w, 60), SaveSystem.HasProfile ? "Seu personagem" : "Crie o seu personagem", UiKit.Subtitle);
            y += 80;

            var before = JsonUtility.ToJson(_edit);

            GUI.Label(new Rect(x, y, w * 0.42f, row), "Nome", UiKit.Text);
            _edit.Name = GUI.TextField(new Rect(x + w * 0.42f, y, w * 0.58f, row), _edit.Name ?? "", 16, UiKit.Field);
            y += row + gap;
            _edit.Gender = UiKit.Selector(new Rect(x, y, w, row), "Gênero", _edit.Gender, g => g.ToString());
            y += row + gap;
            _edit.SkinTone = UiKit.Swatches(new Rect(x, y, w, row), "Tom de pele", _edit.SkinTone, Palette.SkinTones);
            y += row + gap;
            _edit.Hair = UiKit.Selector(new Rect(x, y, w, row), "Cabelo", _edit.Hair, Names.Of);
            y += row + gap;
            _edit.HairColor = UiKit.Swatches(new Rect(x, y, w, row), "Cor do cabelo", _edit.HairColor, Palette.HairColors);
            y += row + gap;
            _edit.ShirtColor = UiKit.Swatches(new Rect(x, y, w, row), "Camisa", _edit.ShirtColor, Palette.ClothColors);
            y += row + gap;
            _edit.PantsColor = UiKit.Swatches(new Rect(x, y, w, row), "Calça", _edit.PantsColor, Palette.ClothColors);
            y += row + gap;
            _edit.Hat = UiKit.Selector(new Rect(x, y, w, row), "Chapéu", _edit.Hat, Names.Of);
            y += row + gap;
            _edit.Glasses = UiKit.Toggle(new Rect(x, y, w, row), "Óculos", _edit.Glasses);
            y += row + gap;
            _edit.Mustache = UiKit.Toggle(new Rect(x, y, w, row), "Bigode", _edit.Mustache);
            y += row + gap * 2;

            if (JsonUtility.ToJson(_edit) != before) _app.Stage.SetAvatar(_edit); // pré-visualização ao vivo

            if (GUI.Button(new Rect(x, y, 230, 70), "Aleatório", UiKit.Button))
            {
                var name = _edit.Name;
                _edit = PlayerProfile.Random(name, new System.Random());
                _app.Stage.SetAvatar(_edit);
            }
            if (GUI.Button(new Rect(x + w - 470, y, 220, 70), "Cancelar", UiKit.Button)) _app.Go(ScreenId.MainMenu);
            if (GUI.Button(new Rect(x + w - 230, y, 230, 70), "Salvar", UiKit.Selected))
            {
                if (string.IsNullOrWhiteSpace(_edit.Name)) { _app.Toast("Escolha um nome"); return; }
                _edit.Name = _edit.Name.Trim();
                _app.SaveProfile(_edit);
                _app.Go(ScreenId.MainMenu);
            }
        }
    }

    /// <summary>Tela de Opções do menu inicial.</summary>
    public class OptionsScreen : IScreen
    {
        private readonly GameApp _app;
        private OptionsPanel _panel;
        public OptionsScreen(GameApp app) => _app = app;

        public void Enter()
        {
            _panel = new OptionsPanel(_app);
            _app.Stage?.SetFraming(MenuStage.Framing.Centered);
        }

        public void Exit() { }
        public void Tick() { }
        public void Draw()
        {
            if (_panel.Draw()) _app.Go(ScreenId.MainMenu);
        }
    }

    /// <summary>Painel de opções (áudio, gráficos, jogo). Usado no menu e na pausa da mesa.</summary>
    public class OptionsPanel
    {
        private readonly GameApp _app;
        private readonly GameSettings _edit;
        private int _tab;

        public OptionsPanel(GameApp app)
        {
            _app = app;
            _edit = app.Settings.Clone();
        }

        /// <summary>Desenha o painel. Devolve true quando o jogador clica em Voltar.</summary>
        public bool Draw()
        {
            float w = 1000, x = (UiKit.W - w) / 2, top = 110, y = top, row = 58, gap = 16;
            UiKit.Panel(new Rect(x - 40, y - 50, w + 80, 860), 0.85f);
            GUI.Label(new Rect(x, y - 30, w, 60), "Opções", UiKit.Subtitle);
            y += 50;
            _tab = GUI.Toolbar(new Rect(x, y, w, 60), _tab, new[] { "Áudio", "Gráficos", "Jogo" }, UiKit.Button);
            y += 100;

            switch (_tab)
            {
                case 0:
                    _edit.MasterVolume = UiKit.Slider(new Rect(x, y, w, row), "Volume geral", _edit.MasterVolume, 0, 1);
                    y += row + gap;
                    _edit.MusicVolume = UiKit.Slider(new Rect(x, y, w, row), "Música", _edit.MusicVolume, 0, 1);
                    y += row + gap;
                    _edit.SfxVolume = UiKit.Slider(new Rect(x, y, w, row), "Efeitos", _edit.SfxVolume, 0, 1);
                    y += row + gap;
                    UiKit.Label(new Rect(x, y, w, row), "Música e efeitos entram quando os sons forem adicionados.", UiKit.Small, new Color(1, 1, 1, 0.6f));
                    break;
                case 1:
                    var res = Screen.resolutions;
                    if (res.Length > 0)
                    {
                        int idx = _edit.ResolutionIndex < 0 || _edit.ResolutionIndex >= res.Length ? CurrentResolutionIndex(res) : _edit.ResolutionIndex;
                        _edit.ResolutionIndex = UiKit.Selector(new Rect(x, y, w, row), "Resolução", idx, res.Length,
                                                               i => $"{res[i].width} x {res[i].height}");
                        y += row + gap;
                    }
                    _edit.Fullscreen = UiKit.Toggle(new Rect(x, y, w, row), "Tela cheia", _edit.Fullscreen);
                    y += row + gap;
                    var q = QualitySettings.names;
                    if (q.Length > 0)
                    {
                        int qi = _edit.QualityLevel < 0 || _edit.QualityLevel >= q.Length ? QualitySettings.GetQualityLevel() : _edit.QualityLevel;
                        _edit.QualityLevel = UiKit.Selector(new Rect(x, y, w, row), "Qualidade", qi, q.Length, i => q[i]);
                        y += row + gap;
                    }
                    _edit.VSync = UiKit.Toggle(new Rect(x, y, w, row), "VSync", _edit.VSync);
                    break;
                default:
                    _edit.BotDelay = UiKit.Slider(new Rect(x, y, w, row), "Pausa entre jogadas dos bots", _edit.BotDelay, 0.1f, 2f, "{0:0.0}s", 1f);
                    y += row + gap;
                    _edit.ShowHints = UiKit.Toggle(new Rect(x, y, w, row), "Dicas na mão", _edit.ShowHints);
                    y += row + gap;
                    _edit.ConfirmBater = UiKit.Toggle(new Rect(x, y, w, row), "Confirmar antes de bater", _edit.ConfirmBater);
                    break;
            }

            float by = top + 860 - 140;
            bool back = GUI.Button(new Rect(x, by, 300, 70), "Voltar", UiKit.Button);
            if (GUI.Button(new Rect(x + w - 300, by, 300, 70), "Aplicar e salvar", UiKit.Selected))
            {
                _app.SaveSettings(_edit);
                _app.Toast("Opções salvas", 2f);
            }
            return back;
        }

        private static int CurrentResolutionIndex(Resolution[] res)
        {
            for (int i = 0; i < res.Length; i++)
                if (res[i].width == Screen.width && res[i].height == Screen.height) return i;
            return res.Length - 1;
        }
    }

    /// <summary>Criar sala: modo, ambiente, bots. Por enquanto a sala é local, contra bots.</summary>
    public class CreateRoomScreen : IScreen
    {
        private readonly GameApp _app;
        public CreateRoomScreen(GameApp app) => _app = app;

        public void Enter()
        {
            _app.Stage.SetFraming(MenuStage.Framing.Centered);
            _app.Stage.SetEnvironment(_app.Room.Environment);
        }

        public void Exit() { }
        public void Tick() { }

        public void Draw()
        {
            var room = _app.Room;
            float w = 900, x = UiKit.W - w - 90, y = 120, row = 62, gap = 18;
            UiKit.Panel(new Rect(x - 40, y - 60, w + 80, 860), 0.75f);
            GUI.Label(new Rect(x, y - 40, w, 60), "Criar sala", UiKit.Subtitle);
            y += 60;

            room.Mode = UiKit.Selector(new Rect(x, y, w, row), "Modo", room.Mode, Names.Of);
            y += row + gap;
            if (room.Mode == GameMode.Cachetex)
            {
                UiKit.Label(new Rect(x, y, w, row), "Regras do Cachetex ainda em definição: por enquanto é a Cacheta sem limite de curingas por jogo.",
                            UiKit.Small, Palette.Gold);
                y += row + gap;
            }

            var env = UiKit.Selector(new Rect(x, y, w, row), "Ambiente", room.Environment, Names.Of);
            if (env != room.Environment)
            {
                room.Environment = env;
                _app.Stage.SetEnvironment(env); // pré-visualiza o ambiente atrás do menu
            }
            y += row + gap;
            room.Bots = UiKit.Selector(new Rect(x, y, w, row), "Bots na mesa", room.Bots - 1, 7, i => (i + 1).ToString()) + 1;
            y += row + gap;
            room.Difficulty = UiKit.Selector(new Rect(x, y, w, row), "Dificuldade", room.Difficulty, Names.Of);
            y += row + gap;

            GUI.enabled = false;
            UiKit.Toggle(new Rect(x, y, w, row), "Sala online", false);
            GUI.enabled = true;
            UiKit.Label(new Rect(x + w * 0.42f + 180, y, w * 0.58f - 180, row), "chega com o multiplayer", UiKit.Small, new Color(1, 1, 1, 0.6f));
            y += row + gap;

            float by = 120 - 60 + 860 - 130;
            if (GUI.Button(new Rect(x, by, 300, 80), "Voltar", UiKit.Button)) _app.Go(ScreenId.MainMenu);
            if (GUI.Button(new Rect(x + w - 360, by, 360, 80), "Começar partida", UiKit.Selected)) _app.Go(ScreenId.Table);
        }
    }

    /// <summary>Entrar numa sala por código. O online chega na fase de multiplayer; a tela já existe para o fluxo.</summary>
    public class JoinRoomScreen : IScreen
    {
        private readonly GameApp _app;
        private string _code = "";
        public JoinRoomScreen(GameApp app) => _app = app;

        public void Enter() => _app.Stage.SetFraming(MenuStage.Framing.Centered);
        public void Exit() { }
        public void Tick() { }

        public void Draw()
        {
            float w = 800, x = UiKit.W - w - 90, y = 240;
            UiKit.Panel(new Rect(x - 40, y - 60, w + 80, 520), 0.75f);
            GUI.Label(new Rect(x, y - 40, w, 60), "Entrar em uma sala", UiKit.Subtitle);
            y += 60;
            GUI.Label(new Rect(x, y, w, 40), "Código da sala", UiKit.Text);
            y += 50;
            _code = GUI.TextField(new Rect(x, y, w, 70), _code ?? "", 8, UiKit.Field).ToUpperInvariant();
            y += 100;
            UiKit.Label(new Rect(x, y, w, 80),
                "Salas online chegam na fase do multiplayer. Enquanto isso, use \"Jogar Cacheta\" para jogar contra bots.",
                UiKit.Small, new Color(1, 1, 1, 0.75f));
            y += 110;
            if (GUI.Button(new Rect(x, y, 300, 80), "Voltar", UiKit.Button)) _app.Go(ScreenId.MainMenu);
            if (GUI.Button(new Rect(x + w - 300, y, 300, 80), "Entrar", UiKit.Selected))
                _app.Toast(_code.Length == 0 ? "Digite o código da sala" : "Multiplayer online ainda não disponível");
        }
    }

    /// <summary>A mesa. Toda a lógica fica no <see cref="TableController"/>.</summary>
    public class TableScreen : IScreen
    {
        private readonly GameApp _app;
        private TableController _table;
        public TableScreen(GameApp app) => _app = app;

        public void Enter()
        {
            _table = new GameObject("Mesa").AddComponent<TableController>();
            _table.Init(_app, _app.Room);
        }

        public void Exit()
        {
            if (_table != null) UnityEngine.Object.Destroy(_table.gameObject);
            _table = null;
        }

        public void Tick() { }
        public void Draw() => _table?.DrawGui();
    }
}
