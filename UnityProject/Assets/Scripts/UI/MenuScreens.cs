using System;
using Cacheta.Core.Bots;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Menu inicial (wireframe "1. Menu principal"): o nome do jogo e o seu personagem à esquerda, os modos à direita.
    /// Cacheta Clássica e Carteado abrem as opções "Criar sala" e "Entrar em uma sala" logo abaixo do botão.
    /// </summary>
    public class MainMenuScreen : IScreen
    {
        private readonly GameApp _app;
        private GameMode? _aberto;   // modo com as opções de sala abertas
        public MainMenuScreen(GameApp app) => _app = app;

        public void Enter()
        {
            _aberto = null;
            _app.Stage.SetEnvironment(EnvironmentId.BarDoTioBeto);
            _app.Stage.SetFraming(MenuStage.Framing.Menu);
        }

        public void Exit() { }
        public void Tick() { }

        public void Draw()
        {
            // Esquerda: logo e, abaixo, o seu personagem 3D (vem do palco) com o nome embaixo.
            UiKit.Shadowed(new Rect(80, 60, 900, 120), "CARTEADO", UiKit.Title, Color.white);
            UiKit.Shadowed(new Rect(86, 170, 900, 40), "cacheta com os amigos", UiKit.Small, Palette.Gold);
            var nameRect = new Rect(UiKit.W * 0.26f - 200, UiKit.H * 0.86f, 400, 50);
            UiKit.Panel(nameRect, 0.5f);
            GUI.Label(nameRect, _app.Profile.Name, UiKit.Center);

            float w = 600, x = UiKit.W - w - 90, gap = 14, h = 76, sub = 62;
            int abertos = _aberto.HasValue ? 1 : 0;

            // Canto de cima: perfil, nível e moedas (nível e moedas ainda não existem: ficam em 1 e 0).
            var chip = new Rect(x + w - 470, 40, 470, 64);
            UiKit.Panel(chip, 0.6f);
            UiKit.Label(new Rect(chip.x + 20, chip.y + 14, 260, 36), _app.Profile.Name, UiKit.Small);
            UiKit.Label(new Rect(chip.x + 220, chip.y + 14, 240, 36), "Nível 1  ·  0 moedas", UiKit.Small, Palette.Gold);

            // Direita: o menu, centralizado na altura.
            float total = 3 * (h + gap) + abertos * 2 * (sub + gap) + 4 * (h + gap) - gap;
            float y = Mathf.Max(140, (UiKit.H - total) / 2 + 20);
            UiKit.Panel(new Rect(x - 30, y - 30, w + 60, total + 60), 0.45f);

            ModeButton(new Rect(x, y, w, h), GameMode.CachetaTradicional, "Cacheta Clássica", ref y, gap, sub);
            ModeButton(new Rect(x, y, w, h), GameMode.Carteado, "Carteado", ref y, gap, sub);
            if (GUI.Button(new Rect(x, y, w, h), "Rei do Carteado", UiKit.BigButton)) _app.Go(ScreenId.ReiDoCarteado);
            y += h + gap;

            float half = (w - gap) / 2;
            if (GUI.Button(new Rect(x, y, half, h), "Fichário", UiKit.BigButton)) _app.Go(ScreenId.Fichario);
            if (GUI.Button(new Rect(x + half + gap, y, half, h), "Loja", UiKit.BigButton)) _app.Go(ScreenId.Loja);
            y += h + gap;
            if (GUI.Button(new Rect(x, y, w, h), "Personalizar perfil", UiKit.BigButton)) _app.Go(ScreenId.Profile);
            y += h + gap;
            if (GUI.Button(new Rect(x, y, half, h), "Opções", UiKit.BigButton)) _app.Go(ScreenId.Options);
            if (GUI.Button(new Rect(x + half + gap, y, half, h), "Sair", UiKit.BigButton))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            // Ferramenta de revisão (sai antes do lançamento): ver o elenco 3D em cada cenário.
            if (GUI.Button(new Rect(20, UiKit.H - 110, 300, 56), "Elenco nos cenários", UiKit.Button)) _app.Go(ScreenId.Elenco);
            UiKit.Label(new Rect(20, UiKit.H - 40, 900, 30), "Protótipo funcional: arte provisória", UiKit.Tiny, new Color(1, 1, 1, 0.6f));
        }

        /// <summary>Botão de modo que abre "Criar sala" e "Entrar em uma sala" logo abaixo.</summary>
        private void ModeButton(Rect r, GameMode mode, string label, ref float y, float gap, float sub)
        {
            bool open = _aberto == mode;
            var style = mode == GameMode.Carteado || open ? SelectedBig : UiKit.BigButton;
            if (GUI.Button(r, (open ? "▾  " : "") + label, style)) _aberto = open ? (GameMode?)null : mode;
            y += r.height + gap;
            if (!open) return;

            float indent = 60;
            if (GUI.Button(new Rect(r.x + indent, y, r.width - indent, sub), "Criar sala", UiKit.Button))
            {
                var room = _app.Room.Clone();
                room.Mode = mode;
                room.Fase = -1;
                _app.Room = room;
                _app.Go(ScreenId.CreateRoom);
            }
            y += sub + gap;
            if (GUI.Button(new Rect(r.x + indent, y, r.width - indent, sub), "Entrar em uma sala", UiKit.Button)) _app.Go(ScreenId.JoinRoom);
            y += sub + gap;
        }

        private static GUIStyle _selectedBig;
        private static GUIStyle SelectedBig
        {
            get
            {
                if (_selectedBig == null)
                {
                    _selectedBig = new GUIStyle(UiKit.BigButton);
                    _selectedBig.normal.textColor = Palette.Gold;
                    _selectedBig.hover.textColor = Palette.Gold;
                }
                return _selectedBig;
            }
        }
    }

    /// <summary>
    /// Rei do Carteado (wireframe "13"): as 7 fases, uma por chefe, na casa dele. Vencer uma fase libera a próxima.
    /// Os power-ups de chefe ainda não estão no jogo: por enquanto a fase é uma Carteado normal com o chefe na mesa.
    /// </summary>
    public class ReiDoCarteadoScreen : IScreen
    {
        private readonly GameApp _app;
        private int _sel;
        private BotDifficulty _dificuldade = BotDifficulty.Medio;
        public ReiDoCarteadoScreen(GameApp app) => _app = app;

        public void Enter()
        {
            _sel = Mathf.Min(ReiDoCarteado.Vencidas, ReiDoCarteado.Fases.Length - 1);
            _app.Stage.SetFraming(MenuStage.Framing.Centered);
            _app.Stage.SetEnvironment(ReiDoCarteado.Fases[_sel].Ambiente);
        }

        public void Exit() { }
        public void Tick() { }

        public void Draw()
        {
            var fases = ReiDoCarteado.Fases;
            int vencidas = ReiDoCarteado.Vencidas;
            float x0 = 80, y0 = 60, cw = 300, ch = 250, gap = 24;

            UiKit.Panel(new Rect(x0 - 30, y0 - 20, 4 * cw + 3 * gap + 60, 120 + 2 * ch + gap + 20), 0.6f);
            UiKit.Shadowed(new Rect(x0, y0, 900, 60), "Rei do Carteado", UiKit.Subtitle, Color.white);
            UiKit.Label(new Rect(x0 + 380, y0 + 14, 900, 40), $"{vencidas} de {fases.Length} chefes vencidos", UiKit.Small, Palette.Gold);

            for (int i = 0; i < fases.Length; i++)
            {
                var f = fases[i];
                var r = new Rect(x0 + (i % 4) * (cw + gap), y0 + 90 + (i / 4) * (ch + gap), cw, ch);
                bool liberada = ReiDoCarteado.Liberada(i), vencida = i < vencidas, final = i == fases.Length - 1;
                UiKit.Box(r, i == _sel ? new Color(0.35f, 0.26f, 0.05f, 0.92f) : new Color(0.1f, 0.09f, 0.12f, liberada ? 0.88f : 0.6f));
                UiKit.Border(r, i == _sel ? Palette.Gold : new Color(1, 1, 1, liberada ? 0.5f : 0.15f), i == _sel ? 5 : 2);
                string tag = final ? "FASE FINAL" : $"FASE {i + 1}";
                if (vencida) tag += "  ·  VENCIDA";
                else if (!liberada) tag += "  ·  BLOQUEADA";
                var dim = liberada ? Color.white : new Color(1, 1, 1, 0.4f);
                UiKit.Label(new Rect(r.x + 18, r.y + 14, r.width - 30, 30), tag, UiKit.Tiny, vencida ? new Color(0.6f, 1f, 0.6f) : Palette.Gold);
                UiKit.Label(new Rect(r.x + 18, r.y + 54, r.width - 30, 50), liberada || vencida ? f.Chefe.Name : "???", UiKit.Text, dim);
                UiKit.Label(new Rect(r.x + 18, r.y + 100, r.width - 30, 30), Names.Of(f.Ambiente), UiKit.Small, dim);
                UiKit.Label(new Rect(r.x + 18, r.y + 150, r.width - 30, 80), "Power-up: " + f.PowerUp, UiKit.Small, dim);
                if (liberada && GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    _sel = i;
                    _app.Stage.SetEnvironment(f.Ambiente);
                }
            }
            // Oitavo espaço: a recompensa.
            var rw = new Rect(x0 + 3 * (cw + gap), y0 + 90 + ch + gap, cw, ch);
            UiKit.Border(rw, new Color(1, 1, 1, 0.3f), 2);
            UiKit.Label(new Rect(rw.x + 18, rw.y + 20, rw.width - 30, 40), "Recompensa", UiKit.Text, Palette.Gold);
            UiKit.Label(new Rect(rw.x + 18, rw.y + 70, rw.width - 30, 160), "Cada chefe vencido dá packs de power-up e o colecionável dele.", UiKit.Small);

            // Painel de baixo: a fase escolhida.
            var sel = fases[_sel];
            float py = UiKit.H - 250;
            UiKit.Panel(new Rect(0, py, UiKit.W, 250), 0.7f);
            UiKit.Label(new Rect(80, py + 25, 1100, 50), $"{sel.Chefe.Name}  ·  {Names.Of(sel.Ambiente)}", UiKit.Subtitle);
            UiKit.Label(new Rect(80, py + 85, 1100, 40), $"Power-up do chefe: {sel.PowerUp}. {sel.Descricao}", UiKit.Small);
            UiKit.Label(new Rect(80, py + 125, 1100, 40), "Os power-ups ainda não estão no jogo: por enquanto é uma partida de Carteado com o chefe sentado de frente para você.",
                        UiKit.Tiny, new Color(1, 1, 1, 0.65f));
            _dificuldade = UiKit.Selector(new Rect(80, py + 165, 700, 62), "Dificuldade", _dificuldade, Names.Of);

            if (GUI.Button(new Rect(UiKit.W - 760, py + 150, 300, 80), "Voltar", UiKit.Button)) { _app.Go(ScreenId.MainMenu); return; }
            GUI.enabled = ReiDoCarteado.Liberada(_sel);
            if (GUI.Button(new Rect(UiKit.W - 420, py + 150, 340, 80), _sel < ReiDoCarteado.Vencidas ? "Jogar de novo" : "Jogar a fase", UiKit.Selected))
            {
                _app.Room = ReiDoCarteado.Sala(_sel, _dificuldade);
                _app.Go(ScreenId.Table);
            }
            GUI.enabled = true;
        }
    }

    /// <summary>Telas que ainda não existem (Fichário e Loja): explicam o que vai ter nelas.</summary>
    public class EmBreveScreen : IScreen
    {
        private readonly GameApp _app;
        private readonly string _titulo, _texto;
        public EmBreveScreen(GameApp app, string titulo, string texto)
        {
            _app = app;
            _titulo = titulo;
            _texto = texto;
        }

        public void Enter() => _app.Stage.SetFraming(MenuStage.Framing.Centered);
        public void Exit() { }
        public void Tick() { }

        public void Draw()
        {
            float w = 800, x = UiKit.W - w - 90, y = 240;
            UiKit.Panel(new Rect(x - 40, y - 60, w + 80, 520), 0.75f);
            GUI.Label(new Rect(x, y - 40, w, 60), _titulo, UiKit.Subtitle);
            UiKit.Label(new Rect(x, y + 40, w, 40), "Em breve", UiKit.Text, Palette.Gold);
            UiKit.Label(new Rect(x, y + 100, w, 220), _texto, UiKit.Small);
            if (GUI.Button(new Rect(x, y + 340, 300, 80), "Voltar", UiKit.Button)) _app.Go(ScreenId.MainMenu);
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
            float w = 760, x = UiKit.W - w - 70, y = 50, row = 46, gap = 8;
            UiKit.Panel(new Rect(x - 30, y - 25, w + 60, UiKit.H - 50), 0.6f);
            GUI.Label(new Rect(x, y, w, 60), SaveSystem.HasProfile ? "Seu personagem" : "Crie o seu personagem", UiKit.Subtitle);
            y += 70;

            var before = JsonUtility.ToJson(_edit);

            GUI.Label(new Rect(x, y, w * 0.42f, row), "Nome", UiKit.Text);
            _edit.Name = GUI.TextField(new Rect(x + w * 0.42f, y, w * 0.58f, row), _edit.Name ?? "", 16, UiKit.Field);
            y += row + gap;
            _edit.Gender = UiKit.Selector(new Rect(x, y, w, row), "Gênero", _edit.Gender, g => g.ToString());
            y += row + gap;
            _edit.Body = UiKit.Selector(new Rect(x, y, w, row), "Corpo", _edit.Body, Names.Of);
            y += row + gap;
            _edit.SkinTone = UiKit.Swatches(new Rect(x, y, w, row), "Tom de pele", _edit.SkinTone, Palette.SkinTones);
            y += row + gap;
            _edit.Hair = UiKit.Selector(new Rect(x, y, w, row), "Cabelo", _edit.Hair, Names.Of);
            y += row + gap;
            _edit.HairColor = UiKit.Swatches(new Rect(x, y, w, row), "Cor do cabelo", _edit.HairColor, Palette.HairColors);
            y += row + gap;
            _edit.Pattern = UiKit.Selector(new Rect(x, y, w, row), "Camisa", _edit.Pattern, pt => pt.ToString());
            y += row + gap;
            _edit.ShirtColor = UiKit.Swatches(new Rect(x, y, w, row), "Cor da camisa", _edit.ShirtColor, Palette.ClothColors);
            y += row + gap;
            if (_edit.Pattern == ShirtPattern.Listrada)
            {
                _edit.ShirtColor2 = UiKit.Swatches(new Rect(x, y, w, row), "Cor da listra", _edit.ShirtColor2, Palette.ClothColors);
                y += row + gap;
            }
            _edit.Bottom = UiKit.Selector(new Rect(x, y, w, row), "Parte de baixo", _edit.Bottom, Names.Of);
            y += row + gap;
            _edit.PantsColor = UiKit.Swatches(new Rect(x, y, w, row), "Cor de " + Names.Of(_edit.Bottom).ToLower(), _edit.PantsColor, Palette.ClothColors);
            y += row + gap;
            _edit.Shoes = UiKit.Selector(new Rect(x, y, w, row), "Nos pés", _edit.Shoes, Names.Of);
            y += row + gap;
            _edit.Hat = UiKit.Selector(new Rect(x, y, w, row), "Chapéu", _edit.Hat, Names.Of);
            y += row + gap;
            float tw = w / 3f;
            _edit.Glasses = SmallToggle(new Rect(x, y, tw - 10, row), "Óculos", _edit.Glasses);
            _edit.Mustache = SmallToggle(new Rect(x + tw, y, tw - 10, row), "Bigode", _edit.Mustache);
            _edit.Chain = SmallToggle(new Rect(x + tw * 2, y, tw - 10, row), "Corrente", _edit.Chain);
            y += row + gap * 3;

            if (JsonUtility.ToJson(_edit) != before) _app.Stage.SetAvatar(_edit); // pré-visualização ao vivo

            if (GUI.Button(new Rect(x, y, 230, 64), "Aleatório", UiKit.Button))
            {
                var name = _edit.Name;
                _edit = PlayerProfile.Random(name, new System.Random());
                _app.Stage.SetAvatar(_edit);
            }
            if (GUI.Button(new Rect(x + w - 470, y, 220, 64), "Cancelar", UiKit.Button)) _app.Go(ScreenId.MainMenu);
            if (GUI.Button(new Rect(x + w - 230, y, 230, 64), "Salvar", UiKit.Selected))
            {
                if (string.IsNullOrWhiteSpace(_edit.Name)) { _app.Toast("Escolha um nome"); return; }
                _edit.Name = _edit.Name.Trim();
                _app.SaveProfile(_edit);
                _app.Go(ScreenId.MainMenu);
            }
        }

        /// <summary>Botão liga/desliga compacto (vários por linha).</summary>
        private static bool SmallToggle(Rect r, string label, bool value)
        {
            if (GUI.Button(r, (value ? "[x] " : "[  ] ") + label, value ? UiKit.Selected : UiKit.Button)) value = !value;
            return value;
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
            if (room.Mode == GameMode.Carteado)
            {
                UiKit.Label(new Rect(x, y, w, row), "Carteado é a Clássica com power-ups. Os power-ups ainda não estão no jogo: por enquanto as regras são as mesmas.",
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
            // Cacheta Clássica: de 3 a 8 jogadores, então de 2 a 7 bots.
            room.Bots = UiKit.Selector(new Rect(x, y, w, row), "Jogadores na mesa", Mathf.Clamp(room.Bots, 2, 7) - 2, 6, i => (i + 3).ToString()) + 2;
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
                "Salas online chegam na fase do multiplayer. Enquanto isso, crie uma sala para jogar contra bots.",
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
