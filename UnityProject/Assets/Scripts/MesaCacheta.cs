using System.Collections.Generic;
using System.Linq;
using Cacheta.Core;
using Cacheta.Core.Bots;
using UnityEngine;

/// <summary>
/// Mesa de teste da Cacheta: você contra bots, com arte provisória desenhada por código (IMGUI).
/// Não precisa montar cena: ao apertar Play em qualquer cena, a mesa é criada automaticamente.
/// Toda regra vem do pacote Cacheta.Core; este script só desenha o estado e chama as ações.
/// A thread de arte vai trocar este desenho provisório por sprites e animações de verdade.
/// </summary>
public class MesaCacheta : MonoBehaviour
{
    private const int Eu = 0;
    private const float RefW = 1920f, RefH = 1080f;

    // ---------------------------------------------------------------- configuração da partida
    private int _numBots = 3;
    private BotDifficulty _dificuldade = BotDifficulty.Medio;
    private bool _dicas = true;
    private float _pausaBots = 0.9f;

    // ---------------------------------------------------------------- estado
    private CachetaMatch _match;
    private List<IBot> _bots;
    private readonly List<int> _ordemMao = new List<int>(); // ids das cartas na ordem que o jogador vê
    private int? _selecionada;
    private int? _ultimaComprada;
    private float _proximaAcaoBot;
    private string _aviso;
    private float _avisoAte;
    private readonly List<string> _log = new List<string>();
    private GameEvent _ultimaBatida;
    private bool _rodadaAnulada;
    private Vector2 _logScroll;

    private static readonly string[] NomesBots = { "Zé do Bar", "Dona Cida", "Tonhão", "Marquinho", "Seu Juca", "Neide", "Baiano" };

    // ---------------------------------------------------------------- bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarMesaAutomaticamente()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindAnyObjectByType<MesaCacheta>() != null) return;
#else
        if (Object.FindObjectOfType<MesaCacheta>() != null) return;
#endif
        if (Camera.main == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var nova = camGo.AddComponent<Camera>();
            nova.orthographic = true;
        }
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = new Color(0.07f, 0.36f, 0.2f); // feltro verde
        new GameObject("Mesa Cacheta").AddComponent<MesaCacheta>();
    }

    // ---------------------------------------------------------------- partida

    private void NovaPartida()
    {
        var nomes = new List<string> { "Você" };
        for (int i = 0; i < _numBots; i++) nomes.Add(NomesBots[i % NomesBots.Length]);
        _match = new CachetaMatch(nomes, RuleSet.Default(), (ulong)System.DateTime.Now.Ticks);
        _bots = nomes.Select((_, i) => (IBot)new SimpleBot(_dificuldade, (ulong)(System.DateTime.Now.Ticks + i * 7919))).ToList();
        _match.EventRaised += AoEvento;
        _log.Clear();
        ProximaRodada();
    }

    private void ProximaRodada()
    {
        _ultimaBatida = null;
        _rodadaAnulada = false;
        _ordemMao.Clear();
        _selecionada = null;
        _ultimaComprada = null;
        var r = _match.StartNextRound();
        if (!r.Ok) Avisar(r.Error);
        _proximaAcaoBot = Time.time + _pausaBots;
    }

    private void AoEvento(GameEvent e)
    {
        if (!e.IsVisibleTo(Eu)) return; // nunca mostra o que você não poderia ver
        string quem = e.Seat >= 0 && e.Seat < _match.PlayerCount ? _match.PlayerNames[e.Seat] : "";
        switch (e.Type)
        {
            case GameEventType.RoundStarted:
                Log($"— Rodada {e.RoundNumber}: vira {e.Card}, curinga {Rotulo(_match.Jokers.JokerRank)} —");
                break;
            case GameEventType.HandDealt:
                _ordemMao.Clear();
                _ordemMao.AddRange(e.Cards.Select(c => c.Id));
                break;
            case GameEventType.CardReceived:
                _ordemMao.Add(e.Card.Value.Id);
                _ultimaComprada = e.Card.Value.Id;
                break;
            case GameEventType.DrewFromStock:
                if (e.Seat != Eu) Log($"{quem} comprou do monte");
                break;
            case GameEventType.DrewFromDiscard:
                if (e.Seat == Eu)
                {
                    _ordemMao.Add(e.Card.Value.Id);
                    _ultimaComprada = e.Card.Value.Id;
                }
                else Log($"{quem} pegou {e.Card} do descarte");
                break;
            case GameEventType.Discarded:
                if (e.Seat == Eu) _ordemMao.Remove(e.Card.Value.Id);
                Log($"{quem} descartou {e.Card}");
                break;
            case GameEventType.StockReshuffled:
                Log("O monte acabou: descarte reembaralhado");
                break;
            case GameEventType.Bateu:
                _ultimaBatida = e;
                Log($"{quem} BATEU{(e.Clean ? " LIMPO" : "")}! Os outros perdem {e.Amount}.");
                break;
            case GameEventType.RoundAnnulled:
                _rodadaAnulada = true;
                Log("Rodada anulada: " + e.Message);
                break;
            case GameEventType.PlayerEliminated:
                Log($"{quem} ficou sem tentos e saiu da mesa");
                break;
            case GameEventType.MatchEnded:
                Log($"{quem} levou a mesa!");
                break;
        }
    }

    private void Update()
    {
        if (_match == null || _match.IsOver || _match.Phase == TurnPhase.FimDaRodada) return;
        if (_match.CurrentSeat == Eu || Time.time < _proximaAcaoBot) return;
        var r = BotDriver.Step(_match, _bots[_match.CurrentSeat]);
        if (!r.Ok) Log("Bot errou: " + r.Error);
        _proximaAcaoBot = Time.time + _pausaBots;
    }

    // ---------------------------------------------------------------- ações do jogador

    private void ComprarMonte() => Resultado(_match.DrawFromStock(Eu));
    private void ComprarDescarte() => Resultado(_match.DrawFromDiscard(Eu));

    private void Descartar()
    {
        if (!_selecionada.HasValue) { Avisar("Clique numa carta da sua mão primeiro"); return; }
        if (Resultado(_match.Discard(Eu, Card.FromId(_selecionada.Value))))
        {
            _selecionada = null;
            _ultimaComprada = null;
            _proximaAcaoBot = Time.time + _pausaBots;
        }
    }

    private void Bater()
    {
        if (!_selecionada.HasValue) { Avisar("Escolha a carta que vai descartar para bater"); return; }
        if (Resultado(_match.Bater(Eu, Card.FromId(_selecionada.Value)))) _selecionada = null;
    }

    private bool Resultado(ActionResult r)
    {
        if (!r.Ok) Avisar(r.Error);
        return r.Ok;
    }

    private void Avisar(string msg)
    {
        _aviso = msg;
        _avisoAte = Time.time + 3.5f;
    }

    private void Log(string s)
    {
        _log.Add(s);
        if (_log.Count > 60) _log.RemoveAt(0);
        _logScroll.y = float.MaxValue;
    }

    /// <summary>Reorganiza a mão: jogos formados primeiro, depois as soltas por naipe.</summary>
    private void OrganizarPorJogos()
    {
        var mao = _match.GetHand(Eu);
        var arr = HandEvaluator.Arrange(mao, _match.Rules, _match.Jokers);
        _ordemMao.Clear();
        foreach (var m in arr.Melds) _ordemMao.AddRange(m.Cards.Select(c => c.Id));
        _ordemMao.AddRange(arr.Unmatched.OrderBy(c => c).Select(c => c.Id));
    }

    private void OrganizarPorNaipe()
    {
        _ordemMao.Clear();
        _ordemMao.AddRange(_match.GetHand(Eu).OrderBy(c => c).Select(c => c.Id));
    }

    private void MoverSelecionada(int delta)
    {
        if (!_selecionada.HasValue) return;
        int i = _ordemMao.IndexOf(_selecionada.Value);
        int j = Mathf.Clamp(i + delta, 0, _ordemMao.Count - 1);
        if (i < 0 || i == j) return;
        _ordemMao.RemoveAt(i);
        _ordemMao.Insert(j, _selecionada.Value);
    }

    // ---------------------------------------------------------------- desenho

    private Texture2D _branco;
    private GUIStyle _sCanto, _sNaipe, _sTexto, _sTitulo, _sPequeno, _sBotao, _sCentro, _sLog;
    private float _vw, _vh; // tamanho virtual da tela (altura sempre 1080)

    private void IniciarEstilos()
    {
        if (_branco != null) return;
        _branco = Texture2D.whiteTexture;
        _sCanto = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        _sNaipe = new GUIStyle(GUI.skin.label) { fontSize = 56, alignment = TextAnchor.MiddleCenter };
        _sTexto = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
        _sTexto.normal.textColor = Color.white;
        _sTitulo = new GUIStyle(_sTexto) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _sCentro = new GUIStyle(_sTexto) { alignment = TextAnchor.MiddleCenter };
        _sPequeno = new GUIStyle(_sTexto) { fontSize = 20 };
        _sLog = new GUIStyle(_sTexto) { fontSize = 18 };
        _sBotao = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
    }

    private void OnGUI()
    {
        IniciarEstilos();
        float s = Screen.height / RefH;
        if (Screen.width / RefW < s) s = Screen.width / RefW;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        _vw = Screen.width / s;
        _vh = Screen.height / s;

        if (_match == null) { DesenharMenu(); return; }

        DesenharTopo();
        DesenharAdversarios();
        DesenharCentro();
        DesenharMao();
        DesenharLog();

        if (_match.Phase == TurnPhase.FimDaRodada || _match.IsOver) DesenharFimDeRodada();

        if (_aviso != null && Time.time < _avisoAte)
        {
            var r = new Rect(_vw / 2 - 450, _vh - 330, 900, 50);
            Caixa(r, new Color(0.6f, 0.1f, 0.1f, 0.9f));
            GUI.Label(r, _aviso, _sCentro);
        }
    }

    private void DesenharMenu()
    {
        var r = new Rect(_vw / 2 - 400, _vh / 2 - 300, 800, 600);
        Caixa(r, new Color(0, 0, 0, 0.55f));
        GUI.Label(new Rect(r.x, r.y + 30, r.width, 60), "Cacheta — mesa de teste", _sTitulo);

        float y = r.y + 140;
        GUI.Label(new Rect(r.x + 60, y, 300, 40), $"Bots na mesa: {_numBots}", _sTexto);
        if (GUI.Button(new Rect(r.x + 420, y, 60, 44), "−", _sBotao)) _numBots = Mathf.Max(1, _numBots - 1);
        if (GUI.Button(new Rect(r.x + 490, y, 60, 44), "+", _sBotao)) _numBots = Mathf.Min(7, _numBots + 1);

        y += 80;
        GUI.Label(new Rect(r.x + 60, y, 300, 40), "Dificuldade:", _sTexto);
        string[] nomes = { "Fácil", "Médio", "Difícil" };
        _dificuldade = (BotDifficulty)GUI.Toolbar(new Rect(r.x + 300, y, 420, 44), (int)_dificuldade, nomes, _sBotao);

        y += 80;
        GUI.Label(new Rect(r.x + 60, y, 300, 40), $"Velocidade dos bots: {_pausaBots:0.0}s", _sTexto);
        _pausaBots = GUI.HorizontalSlider(new Rect(r.x + 420, y + 15, 300, 30), _pausaBots, 0.1f, 2f);

        y += 70;
        _dicas = GUI.Toggle(new Rect(r.x + 60, y, 600, 40), _dicas, "  Mostrar dicas (jogos formados, quando dá para bater)", _sTexto);

        if (GUI.Button(new Rect(r.x + 250, r.yMax - 110, 300, 70), "Jogar", _sBotao)) NovaPartida();
    }

    private void DesenharTopo()
    {
        Caixa(new Rect(0, 0, _vw, 60), new Color(0, 0, 0, 0.35f));
        GUI.Label(new Rect(20, 12, 900, 40),
            $"Rodada {_match.RoundNumber}   •   Curinga: {Rotulo(_match.Jokers.JokerRank)} {DescricaoCuringa()}   •   Tentos: " +
            string.Join("   ", Enumerable.Range(0, _match.PlayerCount).Select(i => $"{_match.PlayerNames[i]} {_match.GetTentos(i)}")),
            _sPequeno);
        if (GUI.Button(new Rect(_vw - 200, 8, 180, 44), "Sair", _sBotao)) _match = null;
    }

    private string DescricaoCuringa()
    {
        var j = _match.Jokers;
        switch (j.Rule)
        {
            case JokerSuitRule.MesmoNaipe: return "de " + j.Vira.Suit.Symbol();
            case JokerSuitRule.MesmaCor: return j.Vira.Suit.IsRed() ? "vermelho" : "preto";
            case JokerSuitRule.QualquerNaipe: return "(qualquer naipe)";
            default: return "";
        }
    }

    private void DesenharAdversarios()
    {
        var outros = Enumerable.Range(1, _match.PlayerCount - 1).ToList();
        float w = 230, gap = 20;
        float total = outros.Count * w + (outros.Count - 1) * gap;
        float x = (_vw - 360 - total) / 2; // 360 = espaço do log à direita
        foreach (int seat in outros)
        {
            var r = new Rect(x, 80, w, 170);
            bool vez = _match.CurrentSeat == seat;
            bool fora = _match.IsEliminated(seat);
            Caixa(r, vez ? new Color(1f, 0.85f, 0.2f, 0.45f) : new Color(0, 0, 0, fora ? 0.15f : 0.35f));
            string tag = _match.DealerSeat == seat ? "  (dá as cartas)" : "";
            GUI.Label(new Rect(r.x + 12, r.y + 6, w - 20, 30), _match.PlayerNames[seat] + tag, _sPequeno);
            GUI.Label(new Rect(r.x + 12, r.y + 34, w - 20, 30), fora ? "fora da mesa" : $"{_match.GetTentos(seat)} tentos", _sPequeno);
            int n = _match.GetHand(seat).Count;
            for (int i = 0; i < n; i++) Verso(new Rect(r.x + 12 + i * 18, r.y + 72, 50, 72));
            x += w + gap;
        }
    }

    private void DesenharCentro()
    {
        float cx = (_vw - 360) / 2;
        float y = 300;
        bool podeComprar = _match.CurrentSeat == Eu && _match.Phase == TurnPhase.Comprar;

        // Vira
        GUI.Label(new Rect(cx - 380, y - 34, 140, 30), "Vira", _sCentro);
        DesenharCarta(new Rect(cx - 380, y, 140, 200), _match.Jokers.Vira, false, false, false);

        // Monte
        GUI.Label(new Rect(cx - 170, y - 34, 140, 30), $"Monte ({_match.StockCount})", _sCentro);
        var rMonte = new Rect(cx - 170, y, 140, 200);
        Verso(rMonte);
        if (podeComprar) Borda(rMonte, Color.yellow, 5);
        if (GUI.Button(rMonte, GUIContent.none, GUIStyle.none) && podeComprar) ComprarMonte();

        // Descarte
        GUI.Label(new Rect(cx + 40, y - 34, 140, 30), "Descarte", _sCentro);
        var rDesc = new Rect(cx + 40, y, 140, 200);
        if (_match.DiscardTop.HasValue)
        {
            var top = _match.DiscardTop.Value;
            DesenharCarta(rDesc, top, _match.Jokers.IsJoker(top), false, podeComprar);
            if (GUI.Button(rDesc, GUIContent.none, GUIStyle.none) && podeComprar) ComprarDescarte();
        }
        else Caixa(rDesc, new Color(0, 0, 0, 0.2f));

        // De quem é a vez
        string vez;
        if (_match.Phase == TurnPhase.FimDaRodada || _match.IsOver) vez = "";
        else if (_match.CurrentSeat == Eu)
            vez = _match.Phase == TurnPhase.Comprar ? "Sua vez: clique no MONTE ou no DESCARTE para comprar"
                                                    : "Sua vez: escolha uma carta e DESCARTE (ou BATA)";
        else vez = $"Vez de {_match.PlayerNames[_match.CurrentSeat]}...";
        GUI.Label(new Rect(cx - 450, y + 225, 900, 40), vez, _sCentro);
    }

    private void DesenharMao()
    {
        var mao = _match.GetHand(Eu);
        // Mantém _ordemMao em sincronia com a mão real (segurança contra qualquer evento perdido).
        _ordemMao.RemoveAll(id => !mao.Any(c => c.Id == id));
        foreach (var c in mao) if (!_ordemMao.Contains(c.Id)) _ordemMao.Add(c.Id);

        HashSet<int> emJogo = new HashSet<int>();
        bool podeBater = false;
        if (_dicas && mao.Count > 0)
        {
            var arr = HandEvaluator.Arrange(mao, _match.Rules, _match.Jokers);
            foreach (var m in arr.Melds) foreach (var c in m.Cards) emJogo.Add(c.Id);
            if (_match.CurrentSeat == Eu && _match.Phase == TurnPhase.Descartar)
                podeBater = SimpleBot.FindBater(mao, _match.GetView(Eu), _match.PickedFromDiscard).HasValue;
        }

        float cw = 130, ch = 190, gap = 12;
        float total = _ordemMao.Count * cw + (_ordemMao.Count - 1) * gap;
        float x = (_vw - 360 - total) / 2;
        float y = _vh - ch - 110;

        Caixa(new Rect(x - 20, y - 40, total + 40, ch + 60), new Color(0, 0, 0, 0.25f));
        GUI.Label(new Rect(x - 10, y - 36, 600, 30), _dicas ? "Sua mão (borda verde = carta já em jogo)" : "Sua mão", _sPequeno);

        foreach (int id in _ordemMao.ToList())
        {
            var c = Card.FromId(id);
            bool sel = _selecionada == id;
            var r = new Rect(x, sel ? y - 30 : y, cw, ch);
            DesenharCarta(r, c, _match.Jokers.IsJoker(c), sel, false);
            if (emJogo.Contains(id)) Borda(r, new Color(0.2f, 0.9f, 0.3f), 4);
            if (_ultimaComprada == id) GUI.Label(new Rect(r.x, r.yMax + 2, cw, 26), "nova", _sCentro);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _selecionada = sel ? (int?)null : id;
            x += cw + gap;
        }

        // Botões
        float by = _vh - 80;
        float bx = (_vw - 360) / 2 - 560;
        bool minhaVezDescartar = _match.CurrentSeat == Eu && _match.Phase == TurnPhase.Descartar;
        GUI.enabled = minhaVezDescartar;
        if (GUI.Button(new Rect(bx, by, 220, 60), "Descartar", _sBotao)) Descartar();
        var corAntes = GUI.backgroundColor;
        if (podeBater) GUI.backgroundColor = Color.yellow;
        if (GUI.Button(new Rect(bx + 235, by, 220, 60), podeBater ? "BATER!" : "Bater", _sBotao)) Bater();
        GUI.backgroundColor = corAntes;
        GUI.enabled = true;
        if (GUI.Button(new Rect(bx + 500, by, 200, 60), "Organizar", _sBotao)) OrganizarPorJogos();
        if (GUI.Button(new Rect(bx + 715, by, 160, 60), "Naipe", _sBotao)) OrganizarPorNaipe();
        GUI.enabled = _selecionada.HasValue;
        if (GUI.Button(new Rect(bx + 890, by, 100, 60), "◀", _sBotao)) MoverSelecionada(-1);
        if (GUI.Button(new Rect(bx + 1000, by, 100, 60), "▶", _sBotao)) MoverSelecionada(1);
        GUI.enabled = true;
    }

    private void DesenharLog()
    {
        var r = new Rect(_vw - 340, 80, 320, _vh - 100);
        Caixa(r, new Color(0, 0, 0, 0.35f));
        GUI.Label(new Rect(r.x + 10, r.y + 6, 300, 30), "Mesa", _sPequeno);
        float lineH = 26;
        var view = new Rect(0, 0, 280, Mathf.Max(r.height - 50, _log.Count * lineH));
        _logScroll = GUI.BeginScrollView(new Rect(r.x + 10, r.y + 40, 300, r.height - 50), _logScroll, view);
        for (int i = 0; i < _log.Count; i++) GUI.Label(new Rect(0, i * lineH, 280, lineH), _log[i], _sLog);
        GUI.EndScrollView();
    }

    private void DesenharFimDeRodada()
    {
        var r = new Rect((_vw - 360) / 2 - 650, 180, 1300, 640);
        Caixa(r, new Color(0, 0, 0, 0.8f));
        string titulo;
        if (_match.IsOver)
            titulo = _match.WinnerSeat == Eu ? "Você levou a mesa!" : $"{_match.PlayerNames[_match.WinnerSeat ?? 0]} levou a mesa!";
        else if (_rodadaAnulada) titulo = "Rodada anulada (o monte acabou)";
        else if (_ultimaBatida != null)
            titulo = (_ultimaBatida.Seat == Eu ? "Você bateu" : $"{_match.PlayerNames[_ultimaBatida.Seat]} bateu") +
                     (_ultimaBatida.Clean ? " LIMPO!" : "!");
        else titulo = "Fim da rodada";
        GUI.Label(new Rect(r.x, r.y + 20, r.width, 60), titulo, _sTitulo);

        if (_ultimaBatida != null && _ultimaBatida.Arrangement != null)
        {
            float x = r.x + 40, y = r.y + 120;
            foreach (var m in _ultimaBatida.Arrangement.Melds)
            {
                var wild = new HashSet<Card>(m.WildCards);
                foreach (var c in m.Cards)
                {
                    DesenharCarta(new Rect(x, y, 100, 146), c, wild.Contains(c), false, false);
                    x += 70;
                }
                x += 60;
            }
            if (_ultimaBatida.Card.HasValue)
                GUI.Label(new Rect(r.x + 40, y + 160, 600, 30), $"Descartou {_ultimaBatida.Card.Value}", _sPequeno);
            GUI.Label(new Rect(r.x + 40, y + 200, r.width - 80, 30),
                $"Cada um dos outros perdeu {_ultimaBatida.Amount} tento(s).", _sTexto);
        }

        string tentos = string.Join("    ", Enumerable.Range(0, _match.PlayerCount)
            .Select(i => $"{_match.PlayerNames[i]}: {(_match.IsEliminated(i) ? "fora" : _match.GetTentos(i).ToString())}"));
        GUI.Label(new Rect(r.x + 40, r.yMax - 190, r.width - 80, 70), tentos, _sTexto);

        if (_match.IsOver)
        {
            if (GUI.Button(new Rect(r.center.x - 320, r.yMax - 100, 300, 70), "Jogar de novo", _sBotao)) NovaPartida();
            if (GUI.Button(new Rect(r.center.x + 20, r.yMax - 100, 300, 70), "Menu", _sBotao)) _match = null;
        }
        else if (GUI.Button(new Rect(r.center.x - 150, r.yMax - 100, 300, 70), "Próxima rodada", _sBotao)) ProximaRodada();
    }

    // ---------------------------------------------------------------- primitivas de desenho

    private void DesenharCarta(Rect r, Card c, bool curinga, bool selecionada, bool destaque)
    {
        Caixa(r, Color.white);
        Borda(r, new Color(0, 0, 0, 0.4f), 2);
        var cor = c.Suit.IsRed() ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.1f, 0.1f, 0.1f);
        _sCanto.normal.textColor = cor;
        _sNaipe.normal.textColor = cor;
        float k = r.width / 130f;
        _sCanto.fontSize = Mathf.RoundToInt(30 * k);
        _sNaipe.fontSize = Mathf.RoundToInt(64 * k);
        GUI.Label(new Rect(r.x + 8 * k, r.y + 4 * k, r.width, 40 * k), c.RankLabel + "\n" + c.Suit.Symbol(), _sCanto);
        GUI.Label(new Rect(r.x, r.y + 20 * k, r.width, r.height), c.Suit.Symbol(), _sNaipe);
        if (curinga)
        {
            Borda(r, new Color(1f, 0.75f, 0f), Mathf.Max(3, 6 * k));
            _sCanto.normal.textColor = new Color(0.85f, 0.6f, 0f);
            GUI.Label(new Rect(r.xMax - 36 * k, r.y + 4 * k, 40 * k, 40 * k), "★", _sCanto);
        }
        if (selecionada) Borda(r, new Color(0.2f, 0.5f, 1f), 6);
        if (destaque) Borda(r, Color.yellow, 5);
    }

    private void Verso(Rect r)
    {
        Caixa(r, new Color(0.55f, 0.08f, 0.12f));
        Borda(r, Color.white, Mathf.Max(2, r.width / 30f));
        var inner = new Rect(r.x + r.width * 0.18f, r.y + r.height * 0.15f, r.width * 0.64f, r.height * 0.7f);
        Borda(inner, new Color(1f, 0.8f, 0.4f, 0.7f), Mathf.Max(1, r.width / 40f));
    }

    private void Caixa(Rect r, Color c)
    {
        var antes = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _branco);
        GUI.color = antes;
    }

    private void Borda(Rect r, Color c, float t)
    {
        Caixa(new Rect(r.x, r.y, r.width, t), c);
        Caixa(new Rect(r.x, r.yMax - t, r.width, t), c);
        Caixa(new Rect(r.x, r.y, t, r.height), c);
        Caixa(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private static string Rotulo(int rank) => new Card(rank, Suit.Paus).RankLabel;
}
