using System.Collections.Generic;
using System.Linq;
using Cacheta.Core;
using Cacheta.Core.Bots;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// A mesa 3D: monta o ambiente, os personagens e as cartas, controla a câmera (vista geral / zoom na mão),
    /// o arrastar das cartas, os bots e a HUD. Toda regra vem do <see cref="CachetaMatch"/>; aqui só se desenha
    /// o estado e se chamam as ações. No online, as chamadas ao match viram mensagens ao servidor.
    /// </summary>
    public class TableController : MonoBehaviour
    {
        private const int Me = 0;
        private const float TableTop = TableProps.TableTop;
        private const float TableRadius = TableProps.TableRadius;
        private const float SeatRadius = TableProps.SeatRadius;
        private const float CardW = 0.085f, CardH = 0.125f;   // um pouco maiores que as de verdade, para ler bem
        // Distância entre as cartas da sua mão: juntinhas segurando, abertas no zoom.
        private float HandSpacing => Mathf.Lerp(0.03f, 0.062f, Mathf.SmoothStep(0, 1, _zoomT));

        private static readonly string[] BotNames = { "Zé do Bar", "Dona Cida", "Tonhão", "Seu Juca", "Neide", "Baiano", "Rosinha" };

        // Câmera: os seus olhos, sentado no lugar 0 (vista geral) ou inclinado olhando a mão (zoom).
        // Tudo medido a partir da sua cadeira (em -Z, a SeatRadius do centro) e da altura da mesa; os olhos ficam
        // na mesma altura dos olhos dos personagens sentados.
        public static readonly Vector3 OverviewPos = new Vector3(0, TableTop + 0.43f, -SeatRadius + 0.1f);
        public static readonly Vector3 OverviewLook = new Vector3(0, TableTop + 0.04f, 0.25f);
        private static readonly Vector3 ZoomPos = new Vector3(0, TableTop + 0.45f, -SeatRadius + 0.18f);
        private static readonly Vector3 ZoomLook = new Vector3(0, TableTop + 0.12f, -SeatRadius + 0.64f);
        // As suas mãos seguram o leque aqui: na parte de baixo da vista geral (com as mãos aparecendo), e mais
        // alto e perto do rosto no zoom.
        private static readonly Vector3 HandLow = new Vector3(0, TableTop + 0.18f, -SeatRadius + 0.53f);
        private static readonly Vector3 HandHigh = new Vector3(0, TableTop + 0.21f, -SeatRadius + 0.5f);
        private Vector3 HandCenter => Vector3.Lerp(HandLow, HandHigh, Mathf.SmoothStep(0, 1, _zoomT));

        private GameApp _app;
        private RoomConfig _room;
        private CachetaMatch _match;
        private List<IBot> _bots;
        private List<PlayerProfile> _profiles;
        private Camera _cam;
        private Transform _world;

        // Assentos
        private Vector3[] _seatPos;
        private GameObject[] _avatars;
        private CardHolder[] _holders;
        private FirstPersonHands _myHands;
        private List<GameObject>[] _heldBacks;
        private Transform _turnMarker;

        // Centro da mesa
        private Transform _stock;
        private Transform _stockTop;
        private Renderer _viraRenderer;
        private GameObject _discardGo;
        private Renderer _discardRenderer;
        private Vector3 _stockPos, _discardPos;
        private int _flyingToDiscard;

        // Mão
        private class CardView
        {
            public int Id;
            public GameObject Go;
            public Renderer Frame;
        }

        private readonly Dictionary<int, CardView> _hand = new Dictionary<int, CardView>();
        private Vector3 _fanLeft, _fanRight;            // pontas do seu leque (onde as suas mãos seguram)
        private Quaternion _fanLeftRot, _fanRightRot;
        private readonly List<int> _order = new List<int>();
        private int? _selected;
        private int? _lastDrawn;
        private Vector3 _spawnAt;
        private int? _pressId;
        private Vector2 _pressScreen;
        private bool _dragging;
        private Vector3 _dragPoint;

        // Cartas voando (animação simples de compra/descarte dos outros)
        private class Flyer
        {
            public GameObject Go;
            public Vector3 From, To;
            public Quaternion RotFrom, RotTo;
            public float T, Duration;
            public bool ToDiscard;
        }

        private readonly List<Flyer> _flyers = new List<Flyer>();

        // Câmera
        private bool _zoom;
        private float _zoomT;

        // Olhar em volta (a cabeça de quem está jogando): botão direito arrastado, ou A/D e W/S.
        private const float MaxYaw = 75f, MinPitch = -30f, MaxPitch = 28f;
        private static readonly Vector3 EyeFromNeck = new Vector3(0, 0.07f, 0.09f); // os olhos ficam à frente do pescoço
        private float _lookYaw, _lookPitch;           // para onde a cabeça quer olhar
        private float _headYaw, _headPitch;           // para onde ela está olhando agora (suavizado)
        private bool _rightDown, _rightDragged;
        private Vector2 _rightDownAt;
        private float _keyYaw, _keyPitch;             // teclas seguradas

        // Interface
        private readonly List<string> _log = new List<string>();
        private GameEvent _lastBater;
        private bool _annulled;
        private bool _paused;
        private OptionsPanel _options;
        private bool _confirmBater;
        private bool _confirmAtravessar;
        private int _semDisputa = -1;        // quem ganhou a rodada porque os outros correram
        private string _atravessarChecked;   // cada descarte dá uma chance só para os bots atravessarem
        private bool _hintCanAtravessar;
        private float _nextBot;
        private string _hintKey;
        private HashSet<int> _hintInMeld = new HashSet<int>();
        private bool _hintCanBater;

        // ================================================================ montagem

        public void Init(GameApp app, RoomConfig room)
        {
            _app = app;
            _room = room;
            _cam = app.Cam;
            _cam.fieldOfView = 55f;
            _world = new GameObject("Mundo da mesa").transform;
            _world.SetParent(transform, false);

            int players = room.Bots + 1;
            var rng = new System.Random();
            _profiles = new List<PlayerProfile> { app.Profile.Clone() };
            var botNames = BotNames.OrderBy(_ => rng.Next()).ToList();
            // Os lugares são ocupados pelos personagens do jogo (sorteados); o dono do cenário (o chefe que joga
            // em casa) senta de frente para você. Se a mesa tiver mais lugares que personagens, completa com bots genéricos.
            var anfitriao = PlayerProfile.ChefeDe(room.Environment);
            var chefes = PlayerProfile.Chefes().Where(c => c.Name != anfitriao.Name).OrderBy(_ => rng.Next()).ToList();
            int extra = 0;
            for (int i = 0; i < room.Bots; i++)
            {
                if (i == room.Bots / 2) _profiles.Add(anfitriao);
                else if (chefes.Count > 0) { _profiles.Add(chefes[0]); chefes.RemoveAt(0); }
                else _profiles.Add(PlayerProfile.Random(botNames[extra++ % botNames.Count], rng));
            }

            _match = new CachetaMatch(_profiles.Select(p => p.Name).ToList(), room.BuildRules(), (ulong)System.DateTime.Now.Ticks);
            // Cada bot tem um jeito de jogar: personagens fixos têm o seu, os outros sorteiam (corajoso, conservador...).
            var styleRng = new Rng((ulong)System.DateTime.Now.Ticks);
            _bots = Enumerable.Range(0, players)
                .Select(i => (IBot)new SimpleBot(room.Difficulty, (ulong)(System.DateTime.Now.Ticks + i * 7919),
                                                 BotPersonality.ForCharacter(_profiles[i].Name, styleRng))).ToList();
            _match.EventRaised += OnEvent;

            var look = EnvironmentBuilder.Build(room.Environment, _world, _cam);
            BuildTable(look);
            BuildSeats(players, look);
            BuildCenter();

            _cam.transform.position = OverviewPos;
            _cam.transform.rotation = Quaternion.LookRotation(OverviewLook - OverviewPos);
            NextRound();
        }

        private void OnDestroy()
        {
            if (_match != null) _match.EventRaised -= OnEvent;
        }

        private void BuildTable(EnvironmentLook look) => TableProps.Table(_world, look);

        /// <summary>Você fica de frente para a mesa (-Z); os adversários se espalham do outro lado, da esquerda para a direita.</summary>
        private void BuildSeats(int players, EnvironmentLook look)
        {
            // A mesa tem sempre as 8 cadeiras; quem não tem jogador fica vazia.
            TableProps.Chairs(_world, look);
            _seatPos = TableProps.Seats(players);
            _avatars = new GameObject[players];
            _holders = new CardHolder[players];
            _heldBacks = new List<GameObject>[players];
            for (int i = 1; i < players; i++)
            {
                _avatars[i] = TableProps.Seated(_profiles[i], _world, _seatPos[i]);
                _holders[i] = _avatars[i].GetComponentInChildren<CardHolder>();
                _heldBacks[i] = new List<GameObject>();
            }
            _heldBacks[Me] = new List<GameObject>();
            _myHands = FirstPersonHands.Build(_profiles[Me], _world);

            _turnMarker = Palette.Prim(PrimitiveType.Cylinder, _world, Vector3.zero, new Vector3(0.22f, 0.004f, 0.22f), Palette.Gold, "Marcador da vez").transform;
        }

        private void BuildCenter()
        {
            _stockPos = new Vector3(-0.14f, TableTop, 0.02f);
            _discardPos = new Vector3(0.15f, TableTop, 0.02f);

            // Vira: de face para cima, atravessada sob o monte.
            var vira = NewCardQuad("Vira", _world, keepCollider: false);
            vira.transform.position = _stockPos + new Vector3(0.06f, 0.001f, 0);
            vira.transform.rotation = Quaternion.Euler(90, 90, 0);
            _viraRenderer = vira.GetComponent<Renderer>();

            // Monte: um bloco com o verso por cima. Clicável.
            var stock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stock.name = "Monte";
            stock.transform.SetParent(_world, false);
            stock.GetComponent<Renderer>().sharedMaterial = Palette.Mat(new Color(0.92f, 0.9f, 0.85f));
            _stock = stock.transform;
            var top = NewCardQuad("Topo do monte", _stock, keepCollider: false);
            top.GetComponent<Renderer>().material = Palette.Textured(CardTextures.Back);
            _stockTop = top.transform;

            // Descarte: área clicável e onde se solta a carta arrastada.
            _discardGo = NewCardQuad("Descarte", _world, keepCollider: true);
            _discardGo.transform.position = _discardPos + new Vector3(0, 0.003f, 0);
            _discardGo.transform.rotation = Quaternion.Euler(90, -8, 0);
            _discardRenderer = _discardGo.GetComponent<Renderer>();
            var bc = _discardGo.GetComponent<BoxCollider>();
            bc.size = new Vector3(1.5f, 1.4f, 0.5f); // área de soltar um pouco maior que a carta
        }

        /// <summary>Uma carta é um Quad com colisor de caixa (o MeshCollider do Quad não serve para raycast dos dois lados).</summary>
        private static GameObject NewCardQuad(string name, Transform parent, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(CardW, CardH, 1);
            Object.Destroy(go.GetComponent<Collider>());
            if (keepCollider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(1, 1, 0.05f);
            }
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ================================================================ partida

        private void NextRound()
        {
            _lastBater = null;
            _semDisputa = -1;
            _confirmAtravessar = false;
            _annulled = false;
            _selected = null;
            _lastDrawn = null;
            foreach (var v in _hand.Values) Destroy(v.Go);
            _hand.Clear();
            _order.Clear();
            _spawnAt = _stockPos;
            var r = _match.StartNextRound();
            if (!r.Ok) _app.Toast(r.Error);
            _viraRenderer.material = Palette.Textured(CardTextures.Face(_match.Jokers.Vira));
            _nextBot = Time.time + _app.Settings.BotDelay + 0.5f;
        }

        private void OnEvent(GameEvent e)
        {
            if (!e.IsVisibleTo(Me)) return; // nunca mostra o que você não poderia ver
            string who = e.Seat >= 0 && e.Seat < _match.PlayerCount ? _match.PlayerNames[e.Seat] : "";
            switch (e.Type)
            {
                case GameEventType.RoundStarted:
                    Log($"Rodada {e.RoundNumber}: vira {e.Card}, curinga {JokerText()}");
                    break;
                case GameEventType.HandDealt:
                    _order.Clear();
                    _order.AddRange(e.Cards.Select(c => c.Id));
                    _spawnAt = _stockPos;
                    break;
                case GameEventType.CardReceived:
                    _order.Add(e.Card.Value.Id);
                    _lastDrawn = e.Card.Value.Id;
                    _spawnAt = _stockPos;
                    break;
                case GameEventType.DrewFromStock:
                    if (e.Seat != Me)
                    {
                        Log($"{who} comprou do monte");
                        Fly(null, _stockPos, HeldAnchor(e.Seat), false);
                    }
                    break;
                case GameEventType.Decidiu:
                    Log(e.Seat == Me ? (e.Amount == 1 ? "Você vai" : "Você correu") : $"{who} {(e.Amount == 1 ? "vai" : "correu")}");
                    break;
                case GameEventType.SentadoDeFora:
                    Log(e.Seat == Me ? "Você cumpre esta rodada de fora (bateu furado)" : $"{who} cumpre a rodada de fora");
                    break;
                case GameEventType.RodadaSemDisputa:
                    _semDisputa = e.Seat;
                    Log($"{(e.Seat == Me ? "Você ganhou" : who + " ganhou")} sem jogar: os outros correram");
                    break;
                case GameEventType.BaterFurado:
                    Log(e.Seat == Me ? "Você bateu furado! Fica a próxima rodada de fora." : $"{who} bateu furado e fica a próxima rodada de fora");
                    if (e.Seat == Me) _app.Toast("Bateu furado: a mão não fechava. Você fica a próxima rodada de fora.");
                    break;
                case GameEventType.DrewFromDiscard:
                    if (e.Message == "atravessando") Log($"{(e.Seat == Me ? "Você pegou" : who + " pegou")} {e.Card} atravessando!");
                    if (e.Seat == Me)
                    {
                        _order.Add(e.Card.Value.Id);
                        _lastDrawn = e.Card.Value.Id;
                        _spawnAt = _discardPos;
                    }
                    else
                    {
                        if (e.Message != "atravessando") Log($"{who} pegou {e.Card} do descarte");
                        Fly(e.Card, _discardPos, HeldAnchor(e.Seat), false);
                    }
                    break;
                case GameEventType.Discarded:
                    Log($"{who} descartou {e.Card}");
                    if (e.Seat == Me) DiscardMyView(e.Card.Value.Id);
                    else Fly(e.Card, HeldAnchor(e.Seat), _discardPos, true);
                    break;
                case GameEventType.StockReshuffled:
                    Log("O monte acabou: descarte reembaralhado");
                    break;
                case GameEventType.Bateu:
                    _lastBater = e;
                    if (e.Seat == Me && e.Card.HasValue) DiscardMyView(e.Card.Value.Id);
                    Log($"{who} BATEU{(e.BatidaDeDez ? " DE 10" : "")}{(e.Atravessando ? " ATRAVESSANDO" : "")}! Quem foi perde {e.Amount}.");
                    break;
                case GameEventType.RoundAnnulled:
                    _annulled = true;
                    Log("Rodada anulada: " + e.Message);
                    break;
                case GameEventType.PlayerEliminated:
                    Log($"{who} ficou sem tentos");
                    break;
                case GameEventType.MatchEnded:
                    Log($"{who} levou a mesa!");
                    if (_room.Fase >= 0 && _match.WinnerSeat == Me) ReiDoCarteado.Venceu(_room.Fase);
                    break;
            }
        }

        private void Log(string s)
        {
            _log.Add(s);
            if (_log.Count > 40) _log.RemoveAt(0);
        }

        private string JokerText()
        {
            var j = _match.Jokers;
            string rank = new Card(j.JokerRank, Suit.Paus).RankLabel;
            switch (j.Rule)
            {
                case JokerSuitRule.MesmoNaipe: return rank + j.Vira.Suit.Symbol();
                case JokerSuitRule.MesmaCor: return rank + (j.Vira.Suit.IsRed() ? " vermelho" : " preto");
                case JokerSuitRule.QualquerNaipe: return rank + " (qualquer naipe)";
                default: return "nenhum";
            }
        }

        // ================================================================ ações do jogador

        private bool MyTurn(TurnPhase phase) => !_paused && _match.CurrentSeat == Me && _match.Phase == phase;

        private void DoDrawStock()
        {
            if (!MyTurn(TurnPhase.Comprar)) return;
            Result(_match.DrawFromStock(Me));
        }

        private void DoDrawDiscard()
        {
            if (!MyTurn(TurnPhase.Comprar)) return;
            Result(_match.DrawFromDiscard(Me));
        }

        private void DoDiscard(int id)
        {
            if (!MyTurn(TurnPhase.Descartar)) { _app.Toast("Agora não é hora de descartar"); return; }
            if (Result(_match.Discard(Me, Card.FromId(id))))
            {
                _selected = null;
                _lastDrawn = null;
                _nextBot = Time.time + _app.Settings.BotDelay;
            }
        }

        private void DoBater()
        {
            if (!MyTurn(TurnPhase.Descartar)) return;
            if (!_selected.HasValue) { _app.Toast("Escolha a carta que vai descartar para bater"); return; }
            _confirmBater = false;
            if (Result(_match.Bater(Me, Card.FromId(_selected.Value)))) _selected = null;
        }

        private void DoDecide(bool vai)
        {
            if (!MyTurn(TurnPhase.Decidir)) return;
            if (Result(_match.Decide(Me, vai))) _nextBot = Time.time + _app.Settings.BotDelay;
        }

        /// <summary>Pega o descarte de outro fora da vez para bater. Se a mão não fechar, é bater furado.</summary>
        private void DoAtravessar()
        {
            _confirmAtravessar = false;
            if (_paused || !_match.CanAtravessar(Me)) return;
            Card? discard = _selected.HasValue ? Card.FromId(_selected.Value) : (Card?)null;
            if (!discard.HasValue)
            {
                // Sem carta escolhida só vale a batida de 10 (fica com as 10 cartas na mão).
                var hand = _match.GetHand(Me).ToList();
                hand.Add(_match.DiscardTop.Value);
                if (HandEvaluator.BatidaDeDez(hand, _match.Rules, _match.Jokers) == null)
                {
                    _app.Toast("Escolha a carta que vai descartar para bater");
                    return;
                }
            }
            if (Result(_match.BaterAtravessando(Me, discard))) _selected = null;
        }

        private bool Result(ActionResult r)
        {
            if (!r.Ok) _app.Toast(r.Error);
            return r.Ok;
        }

        private void ArrangeByMelds()
        {
            var arr = HandEvaluator.Arrange(_match.GetHand(Me), _match.Rules, _match.Jokers);
            _order.Clear();
            foreach (var m in arr.Melds) _order.AddRange(m.Cards.Select(c => c.Id));
            _order.AddRange(arr.Unmatched.OrderBy(c => c).Select(c => c.Id));
        }

        private void MoveSelected(int delta)
        {
            if (!_selected.HasValue) return;
            int i = _order.IndexOf(_selected.Value);
            int j = Mathf.Clamp(i + delta, 0, _order.Count - 1);
            if (i < 0 || i == j) return;
            _order.RemoveAt(i);
            _order.Insert(j, _selected.Value);
        }

        public void SetPaused(bool p) => _paused = p;

        // ================================================================ atualização por frame

        private void Update()
        {
            if (_match == null) return;
            float dt = Time.deltaTime;

            // Bots: logo depois de um descarte, qualquer bot pode bater atravessando (uma chance por descarte).
            if (!_paused && !_match.IsOver && _match.Phase == TurnPhase.Comprar && _match.AtravessarFrom >= 0 && Time.time >= _nextBot)
            {
                string key = _match.RoundNumber + "|" + _match.AtravessarFrom + "|" + _match.DiscardTop?.Id;
                if (key != _atravessarChecked)
                {
                    _atravessarChecked = key;
                    if (BotDriver.TryAtravessar(_match, _bots, s => s != Me)) _nextBot = Time.time + _app.Settings.BotDelay;
                }
            }
            if (!_paused && !_match.IsOver && _match.Phase != TurnPhase.FimDaRodada && _match.CurrentSeat != Me && Time.time >= _nextBot)
            {
                var r = BotDriver.Step(_match, _bots[_match.CurrentSeat]);
                if (!r.Ok) Log("Bot errou: " + r.Error);
                _nextBot = Time.time + _app.Settings.BotDelay;
            }

            // Câmera
            _zoomT = Mathf.MoveTowards(_zoomT, _zoom ? 1f : 0f, dt * 3f);
            float z = Mathf.SmoothStep(0, 1, _zoomT);
            var camPos = Vector3.Lerp(OverviewPos, ZoomPos, z);
            var camLook = Vector3.Lerp(OverviewLook, ZoomLook, z);
            UpdateHead(dt);
            // A cabeça gira em volta do pescoço: os olhos andam um pouquinho quando você olha para o lado.
            var baseRot = Quaternion.LookRotation(camLook - camPos);
            var headRot = Quaternion.AngleAxis(_headYaw, Vector3.up) * baseRot * Quaternion.AngleAxis(_headPitch, Vector3.right);
            var neck = camPos - baseRot * EyeFromNeck;
            _cam.transform.position = neck + headRot * EyeFromNeck;
            _cam.transform.rotation = headRot;

            SyncHand();
            LayoutHand(camPos, dt);
            UpdateCenter();
            UpdateSeats();
            UpdateFlyers(dt);
            UpdateHints();
        }

        /// <summary>
        /// Movimento da cabeça. Com o zoom na mão a cabeça volta a olhar para as cartas; sem zoom ela fica para onde
        /// você deixou, até você olhar de volta (ou apertar C).
        /// </summary>
        private void UpdateHead(float dt)
        {
            if (_paused) { _keyYaw = 0; _keyPitch = 0; }
            else
            {
                _lookYaw = Mathf.Clamp(_lookYaw + _keyYaw * 90f * dt, -MaxYaw, MaxYaw);
                _lookPitch = Mathf.Clamp(_lookPitch + _keyPitch * 60f * dt, MinPitch, MaxPitch);
            }
            if (_zoom && !_rightDragged) { _lookYaw = 0; _lookPitch = 0; }
            float k = 1f - Mathf.Exp(-10f * dt);
            _headYaw = Mathf.Lerp(_headYaw, _lookYaw, k);
            _headPitch = Mathf.Lerp(_headPitch, _lookPitch, k);
        }

        private void CenterHead()
        {
            _lookYaw = 0;
            _lookPitch = 0;
        }

        /// <summary>Mantém as cartas 3D iguais à mão real (o motor é a fonte da verdade).</summary>
        private void SyncHand()
        {
            var hand = _match.GetHand(Me);
            var ids = new HashSet<int>(hand.Select(c => c.Id));
            _order.RemoveAll(id => !ids.Contains(id));
            foreach (var c in hand) if (!_order.Contains(c.Id)) _order.Add(c.Id);

            foreach (var id in _hand.Keys.Where(id => !ids.Contains(id)).ToList())
            {
                Destroy(_hand[id].Go);
                _hand.Remove(id);
            }
            foreach (var id in _order)
            {
                if (_hand.ContainsKey(id)) continue;
                var go = NewCardQuad("Carta " + Card.FromId(id), _world, keepCollider: true);
                go.GetComponent<Renderer>().material = Palette.Textured(CardTextures.Face(Card.FromId(id)));
                go.transform.position = _spawnAt + Vector3.up * 0.01f;
                go.transform.rotation = Quaternion.Euler(90, 0, 0);
                var frame = NewCardQuad("Moldura", go.transform, keepCollider: false);
                frame.transform.localScale = new Vector3(1.12f, 1.08f, 1);
                frame.transform.localPosition = new Vector3(0, 0, 0.004f);
                var fr = frame.GetComponent<Renderer>();
                fr.material = Palette.UnlitColor(Color.white);
                fr.enabled = false;
                _hand[id] = new CardView { Id = id, Go = go, Frame = fr };
            }
        }

        private void LayoutHand(Vector3 camPos, float dt)
        {
            int n = _order.Count;
            var toCam = (camPos - HandCenter).normalized;
            var right = Vector3.right;
            var up = Vector3.Cross(toCam, right).normalized * -1f;
            if (Vector3.Dot(up, Vector3.up) < 0) up = -up;
            float k = 1f - Mathf.Exp(-14f * dt);
            bool hints = _app.Settings.ShowHints;

            for (int i = 0; i < n; i++)
            {
                var v = _hand[_order[i]];
                float x = (i - (n - 1) / 2f) * HandSpacing;
                var pos = HandCenter + right * x - up * (x * x * 0.9f) + toCam * (i * 0.002f);
                var rot = Quaternion.LookRotation(-toCam, up) * Quaternion.Euler(0, 0, -x * 90f);
                if (i == 0) { _fanLeft = pos; _fanLeftRot = rot; }
                if (i == n - 1) { _fanRight = pos; _fanRightRot = rot; }
                bool sel = _selected == v.Id;
                if (sel) pos += rot * Vector3.up * 0.035f;

                if (_dragging && _pressId == v.Id)
                {
                    pos = _dragPoint + toCam * 0.06f;
                    rot = Quaternion.LookRotation(-toCam, up);
                }

                v.Go.transform.position = Vector3.Lerp(v.Go.transform.position, pos, k);
                v.Go.transform.rotation = Quaternion.Slerp(v.Go.transform.rotation, rot, k);

                // Moldura: selecionada (azul) > curinga (dourado) > já em jogo (verde, com dicas) > nenhuma
                var card = Card.FromId(v.Id);
                Color? frame = null;
                if (sel) frame = new Color(0.2f, 0.55f, 1f);
                else if (_match.Jokers.IsJoker(card)) frame = Palette.Gold;
                else if (hints && _hintInMeld.Contains(v.Id)) frame = new Color(0.25f, 0.85f, 0.35f);
                else if (_lastDrawn == v.Id) frame = new Color(1f, 1f, 1f, 1f);
                v.Frame.enabled = frame.HasValue;
                if (frame.HasValue) Palette.SetColor(v.Frame.material, frame.Value);
            }

            // As suas mãos seguram as duas pontas do leque; os antebraços vêm de baixo, do seu corpo.
            _myHands?.Place(_fanLeft, _fanLeftRot, _fanRight, _fanRightRot, n > 0, camPos, CardW, CardH, dt);
        }

        private void UpdateCenter()
        {
            int count = _match.StockCount;
            float h = 0.004f + count * 0.0005f;
            _stock.gameObject.SetActive(count > 0);
            _stock.position = _stockPos + new Vector3(0, h / 2 + 0.002f, 0);
            _stock.localScale = new Vector3(CardW, h, CardH);
            // O topo (verso) é filho do bloco: em escala local 1 ele fica exatamente do tamanho da carta.
            _stockTop.localScale = Vector3.one;
            _stockTop.localPosition = new Vector3(0, 0.5f + 0.001f / h, 0);
            _stockTop.localRotation = Quaternion.Euler(90, 0, 0);

            var pile = _match.DiscardPile;
            int idx = pile.Count - 1 - _flyingToDiscard;
            if (idx >= 0)
            {
                _discardRenderer.enabled = true;
                _discardRenderer.material = MaterialFor(pile[idx]);
            }
            else _discardRenderer.enabled = false;
        }

        private readonly Dictionary<int, Material> _faceMats = new Dictionary<int, Material>();

        private Material MaterialFor(Card c)
        {
            int key = c.Rank * 4 + (int)c.Suit;
            if (!_faceMats.TryGetValue(key, out var m))
            {
                m = Palette.Textured(CardTextures.Face(c));
                _faceMats[key] = m;
            }
            return m;
        }

        private void UpdateSeats()
        {
            for (int s = 1; s < _match.PlayerCount; s++)
            {
                var st = _match.GetStatus(s);
                int want = st == SeatStatus.Vai || st == SeatStatus.Decidindo ? _match.GetHand(s).Count : 0;
                var list = _heldBacks[s];
                var holder = _holders[s];
                if (holder != null) holder.Cards = want;
                if (list.Count != want)
                {
                    foreach (var g in list) Destroy(g);
                    list.Clear();
                    // O leque fica nas mãos do personagem (o CardHolder acompanha o peito e dobra os braços).
                    var parent = holder != null ? holder.Fan : _world;
                    for (int i = 0; i < want; i++)
                    {
                        var g = NewCardQuad("Verso", parent, keepCollider: false);
                        g.GetComponent<Renderer>().sharedMaterial = BackMat;
                        float x = (i - (want - 1) / 2f) * 0.021f;
                        if (holder != null) holder.LayCard(g.transform, i, want);
                        else
                        {
                            g.transform.localScale = new Vector3(CardW * 0.8f, CardH * 0.8f, 1);
                            var toCenter = (-_seatPos[s]).normalized;
                            g.transform.position = HeldAnchor(s) + Vector3.Cross(Vector3.up, toCenter).normalized * x;
                            g.transform.rotation = Quaternion.LookRotation(-toCenter, Vector3.up) * Quaternion.Euler(-15, 0, x * 200f);
                        }
                        list.Add(g);
                    }
                }
            }

            bool show = !_match.IsOver && _match.Phase != TurnPhase.FimDaRodada && _match.CurrentSeat >= 0;
            _turnMarker.gameObject.SetActive(show);
            if (show)
            {
                var dir = _seatPos[_match.CurrentSeat].normalized;
                _turnMarker.position = new Vector3(0, TableTop + 0.001f, 0) + dir * (TableRadius - 0.14f);
            }
        }

        private Material _backMat;
        private Material BackMat => _backMat != null ? _backMat : (_backMat = Palette.Textured(CardTextures.Back));

        private Vector3 HeldAnchor(int seat)
        {
            if (seat == Me) return HandCenter;
            if (_holders[seat] != null) return _holders[seat].Fan.position;
            var toCenter = (-_seatPos[seat]).normalized;
            return _seatPos[seat] + toCenter * 0.36f + Vector3.up * (TableTop + 0.15f);
        }

        private void Fly(Card? face, Vector3 from, Vector3 to, bool toDiscard)
        {
            var go = NewCardQuad("Voando", _world, keepCollider: false);
            go.GetComponent<Renderer>().material = face.HasValue ? MaterialFor(face.Value) : BackMat;
            go.transform.position = from;
            var flat = Quaternion.Euler(90, Random.Range(-10f, 10f), 0);
            if (toDiscard) _flyingToDiscard++;
            _flyers.Add(new Flyer
            {
                Go = go, From = from + Vector3.up * 0.01f, To = to + Vector3.up * 0.01f,
                RotFrom = flat, RotTo = flat, T = 0, Duration = 0.4f, ToDiscard = toDiscard,
            });
        }

        private void DiscardMyView(int id)
        {
            if (_hand.TryGetValue(id, out var v))
            {
                var go = v.Go;
                _hand.Remove(id);
                go.GetComponent<Collider>().enabled = false;
                v.Frame.enabled = false;
                _flyingToDiscard++;
                _flyers.Add(new Flyer
                {
                    Go = go, From = go.transform.position, To = _discardPos + Vector3.up * 0.01f,
                    RotFrom = go.transform.rotation, RotTo = Quaternion.Euler(90, Random.Range(-10f, 10f), 0),
                    T = 0, Duration = 0.3f, ToDiscard = true,
                });
            }
            _order.Remove(id);
        }

        private void UpdateFlyers(float dt)
        {
            for (int i = _flyers.Count - 1; i >= 0; i--)
            {
                var f = _flyers[i];
                f.T += dt / f.Duration;
                float t = Mathf.Clamp01(f.T);
                var p = Vector3.Lerp(f.From, f.To, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.15f;
                f.Go.transform.position = p;
                f.Go.transform.rotation = Quaternion.Slerp(f.RotFrom, f.RotTo, t);
                if (f.T >= 1f)
                {
                    if (f.ToDiscard) _flyingToDiscard = Mathf.Max(0, _flyingToDiscard - 1);
                    Destroy(f.Go);
                    _flyers.RemoveAt(i);
                }
            }
        }

        /// <summary>Dicas (cartas já em jogo, se dá para bater). Recalcula só quando a mão muda.</summary>
        private void UpdateHints()
        {
            var hand = _match.GetHand(Me);
            string key = string.Join(",", hand.Select(c => c.Id).OrderBy(i => i)) + "|" + _match.Phase + "|" + _match.CurrentSeat
                         + "|" + _match.AtravessarFrom + "|" + _match.DiscardTop?.Id;
            if (key == _hintKey) return;
            _hintKey = key;
            _hintInMeld = new HashSet<int>();
            _hintCanBater = false;
            _hintCanAtravessar = false;
            if (hand.Count == 0) return;
            var arr = HandEvaluator.Arrange(hand, _match.Rules, _match.Jokers);
            foreach (var m in arr.Melds) foreach (var c in m.Cards) _hintInMeld.Add(c.Id);
            if (_match.CurrentSeat == Me && _match.Phase == TurnPhase.Descartar)
                _hintCanBater = SimpleBot.FindBater(hand, _match.GetView(Me), _match.PickedFromDiscard).HasValue;
            if (_match.CanAtravessar(Me))
            {
                var top = _match.DiscardTop.Value;
                _hintCanAtravessar = SimpleBot.FindBater(hand.Concat(new[] { top }).ToList(), _match.GetView(Me), top).HasValue;
            }
        }

        // ================================================================ entrada (mouse/teclado via IMGUI, funciona com qualquer sistema de input)

        private void HandleWorldInput(bool roundOver)
        {
            var e = Event.current;
            if (_paused || _confirmBater || _confirmAtravessar) return;
            if (roundOver && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag || e.type == EventType.MouseUp) && e.button == 0) return;
            Vector2 screen = GUIUtility.GUIToScreenPoint(e.mousePosition);
            var ray = _cam.ScreenPointToRay(new Vector3(screen.x, Screen.height - screen.y, 0));

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                {
                    if (Physics.Raycast(ray, out var hit, 20f))
                    {
                        var go = hit.collider.gameObject;
                        int? id = _hand.Values.FirstOrDefault(v => v.Go == go)?.Id;
                        if (id.HasValue)
                        {
                            _pressId = id;
                            _pressScreen = screen;
                            _dragging = false;
                        }
                        else if (go.transform == _stock) DoDrawStock();
                        else if (go == _discardGo) DoDrawDiscard();
                    }
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when _pressId.HasValue:
                {
                    if (!_dragging && (screen - _pressScreen).magnitude > 8f) _dragging = true;
                    if (_dragging)
                    {
                        var toCam = (_cam.transform.position - HandCenter).normalized;
                        var plane = new Plane(toCam, HandCenter);
                        if (plane.Raycast(ray, out float d))
                        {
                            _dragPoint = ray.GetPoint(d);
                            // Reordena enquanto arrasta: a carta entra na posição mais próxima.
                            int n = _order.Count;
                            float lx = Vector3.Dot(_dragPoint - HandCenter, Vector3.right);
                            int target = Mathf.Clamp(Mathf.RoundToInt(lx / HandSpacing + (n - 1) / 2f), 0, n - 1);
                            int cur = _order.IndexOf(_pressId.Value);
                            if (cur >= 0 && cur != target)
                            {
                                _order.RemoveAt(cur);
                                _order.Insert(target, _pressId.Value);
                            }
                        }
                    }
                    e.Use();
                    break;
                }
                case EventType.MouseUp when e.button == 0 && _pressId.HasValue:
                {
                    int id = _pressId.Value;
                    if (_dragging)
                    {
                        // Soltou em cima do descarte? Então descarta.
                        bool onDiscard = Physics.RaycastAll(ray, 20f).Any(h => h.collider.gameObject == _discardGo);
                        if (onDiscard) DoDiscard(id);
                    }
                    else _selected = _selected == id ? (int?)null : id;
                    _pressId = null;
                    _dragging = false;
                    e.Use();
                    break;
                }
                // Botão direito: clique rápido liga/desliga o zoom; segurar e arrastar move a cabeça.
                case EventType.MouseDown when e.button == 1:
                    _rightDown = true;
                    _rightDragged = false;
                    _rightDownAt = screen;
                    e.Use();
                    break;
                case EventType.MouseDrag when e.button == 1 && _rightDown:
                    if (!_rightDragged && (screen - _rightDownAt).magnitude > 6f) _rightDragged = true;
                    if (_rightDragged)
                    {
                        // e.delta vem nas coordenadas da interface (1080 de altura), então a velocidade é igual em qualquer resolução.
                        _lookYaw = Mathf.Clamp(_lookYaw + e.delta.x * 0.09f, -MaxYaw, MaxYaw);
                        _lookPitch = Mathf.Clamp(_lookPitch + e.delta.y * 0.09f, MinPitch, MaxPitch);
                        if (_zoom) _zoom = false; // olhar em volta sai do zoom na mão
                    }
                    e.Use();
                    break;
                case EventType.MouseUp when e.button == 1:
                    if (_rightDown && !_rightDragged) { _zoom = !_zoom; if (_zoom) CenterHead(); }
                    _rightDown = false;
                    _rightDragged = false;
                    e.Use();
                    break;
                // Botão do meio (clique na roda): olha de volta para a mesa.
                case EventType.MouseDown when e.button == 2:
                    CenterHead();
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    _zoom = e.delta.y < 0;
                    if (_zoom) CenterHead();
                    e.Use();
                    break;
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Tab) { _zoom = !_zoom; if (_zoom) CenterHead(); e.Use(); }
                    else if (e.keyCode == KeyCode.LeftArrow) { MoveSelected(-1); e.Use(); }
                    else if (e.keyCode == KeyCode.RightArrow) { MoveSelected(1); e.Use(); }
                    else if (e.keyCode == KeyCode.Return && _selected.HasValue) { DoDiscard(_selected.Value); e.Use(); }
                    else if (e.keyCode == KeyCode.C) { CenterHead(); e.Use(); }
                    else if (LookKey(e.keyCode, true)) e.Use();
                    break;
                case EventType.KeyUp:
                    if (LookKey(e.keyCode, false)) e.Use();
                    break;
            }
        }

        /// <summary>A/D olham para os lados e W/S para cima e para baixo, enquanto a tecla estiver apertada.</summary>
        private bool LookKey(KeyCode k, bool down)
        {
            float v = down ? 1f : 0f;
            switch (k)
            {
                case KeyCode.A: _keyYaw = -v; return true;
                case KeyCode.D: _keyYaw = v; return true;
                case KeyCode.W: _keyPitch = -v; return true;
                case KeyCode.S: _keyPitch = v; return true;
                default: return false;
            }
        }

        // ================================================================ HUD

        public void DrawGui()
        {
            if (_match == null) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (_options != null) _options = null;
                else _paused = !_paused;
                e.Use();
            }

            DrawNameTags();
            DrawTopBar();
            DrawMiniMap();
            DrawBottomBar();

            bool roundOver = _match.Phase == TurnPhase.FimDaRodada || _match.IsOver;
            if (roundOver && _flyers.Count == 0) DrawRoundEnd();
            if (_confirmBater) DrawConfirmBater();
            if (_confirmAtravessar) DrawConfirmAtravessar();
            if (_paused) DrawPause();

            HandleWorldInput(roundOver);
        }

        private void DrawNameTags()
        {
            for (int s = 1; s < _match.PlayerCount; s++)
            {
                var world = _seatPos[s] + Vector3.up * 1.45f;
                var sp = _cam.WorldToScreenPoint(world);
                if (sp.z < 0) continue;
                float x = sp.x / UiKit.Scale, y = (Screen.height - sp.y) / UiKit.Scale;
                var r = new Rect(x - 170, y - 70, 340, 70);
                bool turn = _match.CurrentSeat == s && _match.Phase != TurnPhase.FimDaRodada;
                bool out_ = _match.IsEliminated(s);
                UiKit.Box(r, turn ? new Color(1f, 0.78f, 0.1f, 0.9f) : new Color(0, 0, 0, out_ ? 0.3f : 0.6f));
                var txt = turn ? Color.black : Color.white;
                UiKit.Label(new Rect(r.x + 10, r.y + 4, r.width - 20, 30),
                    _match.PlayerNames[s] + (_match.DealerSeat == s ? "  (D)" : "")
                    + (_bots[s] is SimpleBot sb ? $"  ·  {sb.Personality.Estilo.ToLower()}" : ""), UiKit.Small, txt);
                UiKit.Label(new Rect(r.x + 10, r.y + 34, r.width - 20, 30),
                    out_ ? "fora da mesa" : $"{_match.GetTentos(s)} tentos  ·  {StatusText(s)}", UiKit.Tiny, txt);
            }
        }

        private string StatusText(int s)
        {
            switch (_match.GetStatus(s))
            {
                case SeatStatus.Decidindo: return "pensando...";
                case SeatStatus.Correu: return "correu";
                case SeatStatus.Sentado: return "de fora";
                case SeatStatus.Eliminado: return "fora da mesa";
                default: return $"vai  ·  {_match.GetHand(s).Count} cartas";
            }
        }

        private void DrawTopBar()
        {
            if (GUI.Button(new Rect(20, 20, 70, 60), "II", UiKit.Button)) _paused = !_paused;
            var r = new Rect(105, 20, _room.Fase >= 0 ? 940 : 620, 60);
            UiKit.Panel(r, 0.55f);
            GUI.Label(new Rect(r.x + 15, r.y + 14, r.width, 40),
                $"{(_room.Fase >= 0 ? $"Rei do Carteado, fase {_room.Fase + 1}: {ReiDoCarteado.Fases[_room.Fase].Chefe.Name}" : Names.Of(_room.Mode))}  •  Rodada {_match.RoundNumber}  •  Curinga: {JokerText()}", UiKit.Small);

            string turn;
            if (_match.Phase == TurnPhase.FimDaRodada || _match.IsOver) turn = "";
            else if (_match.CurrentSeat == Me)
                turn = _match.Phase == TurnPhase.Decidir
                    ? $"Vira {_match.Jokers.Vira}: olhe sua mão. Vai ou corre?"
                    : _match.Phase == TurnPhase.Comprar
                    ? "Sua vez: clique no MONTE ou no DESCARTE"
                    : "Sua vez: arraste uma carta até o descarte (ou selecione e Bata)";
            else if (_match.GetStatus(Me) == SeatStatus.Correu) turn = $"Você correu nesta rodada  ·  vez de {_match.PlayerNames[_match.CurrentSeat]}";
            else if (_match.GetStatus(Me) == SeatStatus.Sentado) turn = "Você está de fora nesta rodada (bateu furado)";
            else if (_match.Phase == TurnPhase.Decidir) turn = $"{_match.PlayerNames[_match.CurrentSeat]} está decidindo se vai...";
            else turn = $"Vez de {_match.PlayerNames[_match.CurrentSeat]}...";
            if (turn.Length > 0)
            {
                var tr = new Rect(UiKit.W / 2 - 420, 100, 840, 50);
                UiKit.Box(tr, _match.CurrentSeat == Me ? new Color(1f, 0.78f, 0.1f, 0.9f) : new Color(0, 0, 0, 0.5f));
                UiKit.Label(tr, turn, UiKit.Center, _match.CurrentSeat == Me ? Color.black : Color.white);
            }
        }

        /// <summary>A mesa vista de cima, sempre no canto: assentos, vira, monte, descarte e o que aconteceu.</summary>
        private void DrawMiniMap()
        {
            float size = 380, x0 = UiKit.W - size - 20, y0 = 20;
            var panel = new Rect(x0, y0, size, size + 220);
            UiKit.Panel(panel, 0.6f);
            var c = new Vector2(x0 + size / 2, y0 + size / 2);
            UiKit.Box(new Rect(c.x - 120, c.y - 120, 240, 240), new Color(0.07f, 0.36f, 0.2f, 0.9f));
            UiKit.Border(new Rect(c.x - 120, c.y - 120, 240, 240), new Color(0, 0, 0, 0.4f), 3);

            // Vira, monte e descarte
            GUI.DrawTexture(new Rect(c.x - 92, c.y - 38, 44, 66), CardTextures.Face(_match.Jokers.Vira));
            UiKit.Label(new Rect(c.x - 100, c.y + 28, 60, 22), "vira", UiKit.Tiny);
            GUI.DrawTexture(new Rect(c.x - 34, c.y - 38, 44, 66), CardTextures.Back);
            UiKit.Label(new Rect(c.x - 34, c.y + 28, 70, 22), _match.StockCount.ToString(), UiKit.Tiny);
            if (_match.DiscardTop.HasValue)
                GUI.DrawTexture(new Rect(c.x + 26, c.y - 38, 44, 66), CardTextures.Face(_match.DiscardTop.Value));
            UiKit.Label(new Rect(c.x + 18, c.y + 28, 90, 22), "descarte", UiKit.Tiny);

            // Assentos (você embaixo)
            for (int s = 0; s < _match.PlayerCount; s++)
            {
                var p = _seatPos[s] / SeatRadius;
                var pos = new Vector2(c.x + p.x * 150, c.y - p.z * 150);
                var r = new Rect(pos.x - 52, pos.y - 20, 104, 40);
                bool turn = _match.CurrentSeat == s && _match.Phase != TurnPhase.FimDaRodada && !_match.IsOver;
                UiKit.Box(r, turn ? new Color(1f, 0.78f, 0.1f, 0.95f) : _match.IsEliminated(s) ? new Color(0.3f, 0.3f, 0.3f, 0.8f) : new Color(0.1f, 0.1f, 0.12f, 0.9f));
                string nm = s == Me ? "Você" : _match.PlayerNames[s];
                if (nm.Length > 9) nm = nm.Substring(0, 9);
                UiKit.Label(new Rect(r.x + 4, r.y, r.width - 8, 22), nm, UiKit.Tiny, turn ? Color.black : Color.white);
                UiKit.Label(new Rect(r.x + 4, r.y + 18, r.width - 8, 22), $"♥{_match.GetTentos(s)}  {MiniStatus(s)}", UiKit.Tiny,
                            turn ? Color.black : new Color(1, 1, 1, 0.8f));
            }

            // Últimos acontecimentos
            float ly = y0 + size + 6;
            int start = Mathf.Max(0, _log.Count - 8);
            for (int i = start; i < _log.Count; i++)
            {
                UiKit.Label(new Rect(x0 + 12, ly, size - 24, 26), _log[i], UiKit.Tiny, i == _log.Count - 1 ? Color.white : new Color(1, 1, 1, 0.7f));
                ly += 26;
            }
        }

        private string MiniStatus(int s)
        {
            var st = _match.GetStatus(s);
            return st == SeatStatus.Correu ? "correu" : st == SeatStatus.Sentado ? "fora" : st == SeatStatus.Decidindo ? "?" : $"{_match.GetHand(s).Count}c";
        }

        private void DrawBottomBar()
        {
            float y = UiKit.H - 90, x = 30;
            var before0 = GUI.backgroundColor;

            // Vai ou corre: a vira já está na mesa e você vê a sua mão.
            if (MyTurn(TurnPhase.Decidir))
            {
                GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
                if (GUI.Button(new Rect(x, y, 260, 64), "Vou!", UiKit.Selected)) DoDecide(true);
                GUI.backgroundColor = before0;
                if (GUI.Button(new Rect(x + 275, y, 260, 64), "Corro", UiKit.Button)) DoDecide(false);
                if (GUI.Button(new Rect(x + 550, y, 210, 64), "Organizar", UiKit.Button)) ArrangeByMelds();
                if (GUI.Button(new Rect(x + 775, y, 250, 64), _zoom ? "Vista da mesa" : "Ver minha mão", UiKit.Button)) _zoom = !_zoom;
                UiKit.Label(new Rect(x, y - 36, 1400, 30),
                    $"Correr: perde {_match.Rules.TentosLostCorreu} tento  •  ir e perder: {_match.Rules.TentosLostFoi}  •  batida de 10 vale mais ({_match.Rules.TentosLostCorreuBatidaDeDez} e {_match.Rules.TentosLostFoiBatidaDeDez})",
                    UiKit.Tiny, new Color(1, 1, 1, 0.75f));
                return;
            }

            // Bater atravessando: pegar o descarte de outro fora da vez, só para bater.
            if (_match.CanAtravessar(Me) && !_paused)
            {
                bool glowA = _app.Settings.ShowHints && _hintCanAtravessar;
                if (glowA) GUI.backgroundColor = Color.yellow;
                if (GUI.Button(new Rect(UiKit.W / 2 - 200, y - 110, 400, 64), glowA ? "BATER ATRAVESSANDO!" : "Bater atravessando", UiKit.Button))
                {
                    if (_app.Settings.ConfirmBater) _confirmAtravessar = true;
                    else DoAtravessar();
                }
                GUI.backgroundColor = before0;
            }
            bool discardPhase = MyTurn(TurnPhase.Descartar);

            GUI.enabled = discardPhase && _selected.HasValue;
            if (GUI.Button(new Rect(x, y, 210, 64), "Descartar", UiKit.Button)) DoDiscard(_selected.Value);
            GUI.enabled = discardPhase;
            var before = GUI.backgroundColor;
            bool glow = _app.Settings.ShowHints && _hintCanBater && discardPhase;
            if (glow) GUI.backgroundColor = Color.yellow;
            if (GUI.Button(new Rect(x + 225, y, 210, 64), glow ? "BATER!" : "Bater", UiKit.Button))
            {
                if (!_selected.HasValue) _app.Toast("Escolha a carta que vai descartar para bater");
                else if (_app.Settings.ConfirmBater) _confirmBater = true;
                else DoBater();
            }
            GUI.backgroundColor = before;
            GUI.enabled = !_paused;
            if (GUI.Button(new Rect(x + 450, y, 210, 64), "Organizar", UiKit.Button)) ArrangeByMelds();
            if (GUI.Button(new Rect(x + 675, y, 250, 64), _zoom ? "Vista da mesa" : "Ver minha mão", UiKit.Button)) _zoom = !_zoom;
            GUI.enabled = true;

            UiKit.Label(new Rect(x, y - 36, 1200, 30),
                "Arraste as cartas para reorganizar  •  clique para selecionar  •  botão direito ou Espaço: zoom  •  segure o botão direito e arraste (ou A/D) para olhar em volta  •  C: olhar a mesa  •  Esc: pausa",
                UiKit.Tiny, new Color(1, 1, 1, 0.75f));
        }

        private void DrawRoundEnd()
        {
            var r = new Rect(UiKit.W / 2 - 700, 170, 1400, 660);
            UiKit.Panel(r, 0.85f);
            string title;
            if (_match.IsOver)
                title = _match.WinnerSeat == Me ? "Você levou a mesa!" : $"{_match.PlayerNames[_match.WinnerSeat ?? 0]} levou a mesa!";
            else if (_annulled) title = "Rodada anulada (o monte acabou)";
            else if (_semDisputa >= 0)
                title = (_semDisputa == Me ? "Você ganhou" : $"{_match.PlayerNames[_semDisputa]} ganhou") + " sem jogar: todo mundo correu!";
            else if (_lastBater != null)
                title = (_lastBater.Seat == Me ? "Você bateu" : $"{_match.PlayerNames[_lastBater.Seat]} bateu")
                        + (_lastBater.BatidaDeDez ? " DE 10" : "") + (_lastBater.Atravessando ? " atravessando" : "") + "!";
            else title = "Fim da rodada";
            UiKit.Label(new Rect(r.x, r.y + 25, r.width, 60), title, UiKit.Subtitle);

            if (_lastBater?.Arrangement != null)
            {
                float x = r.x + 40, y = r.y + 120;
                foreach (var m in _lastBater.Arrangement.Melds)
                {
                    var wild = new HashSet<Card>(m.WildCards);
                    foreach (var c in m.Cards)
                    {
                        var cr = new Rect(x, y, 96, 144);
                        GUI.DrawTexture(cr, CardTextures.Face(c));
                        if (wild.Contains(c)) UiKit.Border(cr, Palette.Gold, 5);
                        x += 66;
                    }
                    x += 60;
                }
                if (_lastBater.Card.HasValue)
                    GUI.Label(new Rect(r.x + 40, y + 160, 600, 30), $"Descartou {_lastBater.Card.Value}", UiKit.Small);
                var rules = _match.Rules;
                int correu = _lastBater.BatidaDeDez ? rules.TentosLostCorreuBatidaDeDez : rules.TentosLostCorreu;
                GUI.Label(new Rect(r.x + 40, y + 200, r.width - 80, 36),
                    rules.VaiOuCorre ? $"Quem foi perdeu {_lastBater.Amount} tento(s). Quem correu perdeu {correu}." : $"Cada um dos outros perdeu {_lastBater.Amount} tento(s).",
                    UiKit.Text);
            }

            string tentos = string.Join("    ", Enumerable.Range(0, _match.PlayerCount)
                .Select(i => $"{(i == Me ? "Você" : _match.PlayerNames[i])}: {(_match.IsEliminated(i) ? "fora" : _match.GetTentos(i).ToString())}"));
            GUI.Label(new Rect(r.x + 40, r.yMax - 200, r.width - 80, 80), tentos, UiKit.Text);

            if (_match.IsOver && _room.Fase >= 0)
            {
                // Rei do Carteado: ganhou, volta para o mapa com a próxima fase liberada; perdeu, tenta de novo.
                bool won = _match.WinnerSeat == Me;
                if (GUI.Button(new Rect(r.center.x - 320, r.yMax - 100, 300, 70), won ? "Ver as fases" : "Tentar de novo", UiKit.Selected))
                {
                    if (won) _app.Go(ScreenId.ReiDoCarteado); else Restart();
                }
                if (GUI.Button(new Rect(r.center.x + 20, r.yMax - 100, 300, 70), won ? "Menu" : "Ver as fases", UiKit.Button))
                    _app.Go(won ? ScreenId.MainMenu : ScreenId.ReiDoCarteado);
            }
            else if (_match.IsOver)
            {
                if (GUI.Button(new Rect(r.center.x - 320, r.yMax - 100, 300, 70), "Jogar de novo", UiKit.Selected)) Restart();
                if (GUI.Button(new Rect(r.center.x + 20, r.yMax - 100, 300, 70), "Menu", UiKit.Button)) _app.Go(ScreenId.MainMenu);
            }
            else if (GUI.Button(new Rect(r.center.x - 160, r.yMax - 100, 320, 70), "Próxima rodada", UiKit.Selected)) NextRound();
        }

        private void DrawConfirmBater()
        {
            var r = new Rect(UiKit.W / 2 - 350, UiKit.H / 2 - 120, 700, 240);
            UiKit.Panel(r, 0.92f);
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 50), $"Bater descartando {Card.FromId(_selected ?? 0)}?", UiKit.Center);
            if (GUI.Button(new Rect(r.x + 60, r.yMax - 100, 260, 70), "Cancelar", UiKit.Button)) _confirmBater = false;
            if (GUI.Button(new Rect(r.xMax - 320, r.yMax - 100, 260, 70), "Bater!", UiKit.Selected)) DoBater();
        }

        private void DrawConfirmAtravessar()
        {
            var r = new Rect(UiKit.W / 2 - 400, UiKit.H / 2 - 140, 800, 280);
            UiKit.Panel(r, 0.92f);
            string what = _selected.HasValue ? $"pegando {_match.DiscardTop} e descartando {Card.FromId(_selected.Value)}" : $"pegando {_match.DiscardTop} (batida de 10)";
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 50), $"Bater atravessando, {what}?", UiKit.Center);
            GUI.Label(new Rect(r.x, r.y + 85, r.width, 40), "Se a mão não fechar, é bater furado: você fica a próxima rodada de fora.", UiKit.Center);
            if (GUI.Button(new Rect(r.x + 80, r.yMax - 100, 260, 70), "Cancelar", UiKit.Button)) _confirmAtravessar = false;
            if (GUI.Button(new Rect(r.xMax - 340, r.yMax - 100, 260, 70), "Bater!", UiKit.Selected)) DoAtravessar();
        }

        private void DrawPause()
        {
            if (_options != null)
            {
                if (_options.Draw()) _options = null;
                return;
            }
            var r = new Rect(UiKit.W / 2 - 300, UiKit.H / 2 - 230, 600, 460);
            UiKit.Panel(r, 0.9f);
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 60), "Pausa", UiKit.Subtitle);
            float y = r.y + 120;
            if (GUI.Button(new Rect(r.x + 100, y, 400, 70), "Continuar", UiKit.Selected)) _paused = false;
            y += 90;
            if (GUI.Button(new Rect(r.x + 100, y, 400, 70), "Opções", UiKit.Button)) _options = new OptionsPanel(_app);
            y += 90;
            if (GUI.Button(new Rect(r.x + 100, y, 400, 70), "Sair para o menu", UiKit.Button)) _app.Go(ScreenId.MainMenu);
        }

        private void Restart()
        {
            // Recria a mesa inteira com a mesma sala.
            _app.Go(ScreenId.Table);
        }
    }
}
