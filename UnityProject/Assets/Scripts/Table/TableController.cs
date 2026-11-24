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
        private const float TableTop = 0.8f;
        private const float TableRadius = 1.15f;
        private const float SeatRadius = 1.62f;
        private const float CardW = 0.12f, CardH = 0.18f;
        private const float HandSpacing = 0.1f;

        private static readonly string[] BotNames = { "Zé do Bar", "Dona Cida", "Tonhão", "Marquinho", "Seu Juca", "Neide", "Baiano" };

        // Câmera: vista geral (sentado à mesa) e zoom (olhando para a mão e o centro da mesa).
        private static readonly Vector3 OverviewPos = new Vector3(0, 1.5f, -1.95f);
        private static readonly Vector3 OverviewLook = new Vector3(0, 0.85f, 0.4f);
        private static readonly Vector3 ZoomPos = new Vector3(0, 1.72f, -1.62f);
        private static readonly Vector3 ZoomLook = new Vector3(0, 0.86f, -0.45f);
        private static readonly Vector3 HandCenter = new Vector3(0, 1.1f, -1.35f);

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

        // Interface
        private readonly List<string> _log = new List<string>();
        private GameEvent _lastBater;
        private bool _annulled;
        private bool _paused;
        private OptionsPanel _options;
        private bool _confirmBater;
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
            for (int i = 0; i < room.Bots; i++) _profiles.Add(PlayerProfile.Random(botNames[i % botNames.Count], rng));

            _match = new CachetaMatch(_profiles.Select(p => p.Name).ToList(), room.BuildRules(), (ulong)System.DateTime.Now.Ticks);
            _bots = Enumerable.Range(0, players)
                .Select(i => (IBot)new SimpleBot(room.Difficulty, (ulong)(System.DateTime.Now.Ticks + i * 7919))).ToList();
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

        private void BuildTable(EnvironmentLook look)
        {
            Palette.Prim(PrimitiveType.Cylinder, _world, new Vector3(0, TableTop - 0.02f, 0), new Vector3(TableRadius * 2, 0.02f, TableRadius * 2), look.Table, "Tampo");
            Palette.Prim(PrimitiveType.Cylinder, _world, new Vector3(0, TableTop - 0.05f, 0), new Vector3(TableRadius * 2 + 0.04f, 0.015f, TableRadius * 2 + 0.04f), look.TableEdge);
            Palette.Prim(PrimitiveType.Cylinder, _world, new Vector3(0, TableTop / 2, 0), new Vector3(0.12f, TableTop / 2, 0.12f), look.TableEdge, "Pé");
            Palette.Prim(PrimitiveType.Cylinder, _world, new Vector3(0, 0.02f, 0), new Vector3(0.8f, 0.02f, 0.8f), look.TableEdge);
        }

        /// <summary>Você fica de frente para a mesa (-Z); os adversários se espalham do outro lado, da esquerda para a direita.</summary>
        private void BuildSeats(int players, EnvironmentLook look)
        {
            _seatPos = new Vector3[players];
            _avatars = new GameObject[players];
            _heldBacks = new List<GameObject>[players];
            _seatPos[Me] = new Vector3(0, 0, -SeatRadius);
            int k = players - 1;
            for (int i = 1; i < players; i++)
            {
                float deg = k == 1 ? 90f : 180f - (i - 1) * (180f / (k - 1));
                float rad = deg * Mathf.Deg2Rad;
                _seatPos[i] = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * SeatRadius;
            }

            for (int i = 1; i < players; i++)
            {
                var face = Quaternion.LookRotation(-_seatPos[i].normalized);
                var chair = new GameObject("Cadeira " + i).transform;
                chair.SetParent(_world, false);
                chair.position = _seatPos[i];
                chair.rotation = face;
                Palette.Prim(PrimitiveType.Cube, chair, new Vector3(0, 0.44f, -0.02f), new Vector3(0.5f, 0.05f, 0.48f), look.Chair);
                Palette.Prim(PrimitiveType.Cube, chair, new Vector3(0, 0.78f, -0.26f), new Vector3(0.5f, 0.62f, 0.05f), look.Chair);
                for (int s = -1; s <= 1; s += 2)
                    for (int f = -1; f <= 1; f += 2)
                        Palette.Prim(PrimitiveType.Cube, chair, new Vector3(s * 0.22f, 0.21f, f * 0.2f - 0.02f), new Vector3(0.04f, 0.42f, 0.04f), look.Chair);

                var av = AvatarBuilder.Build(_profiles[i], _world, seated: true);
                av.transform.position = _seatPos[i] + face * new Vector3(0, 0, -0.05f);
                av.transform.rotation = face;
                _avatars[i] = av;
                _heldBacks[i] = new List<GameObject>();
            }
            _heldBacks[Me] = new List<GameObject>();

            _turnMarker = Palette.Prim(PrimitiveType.Cylinder, _world, Vector3.zero, new Vector3(0.22f, 0.004f, 0.22f), Palette.Gold, "Marcador da vez").transform;
        }

        private void BuildCenter()
        {
            _stockPos = new Vector3(-0.2f, TableTop, 0.08f);
            _discardPos = new Vector3(0.22f, TableTop, 0.08f);

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
                case GameEventType.DrewFromDiscard:
                    if (e.Seat == Me)
                    {
                        _order.Add(e.Card.Value.Id);
                        _lastDrawn = e.Card.Value.Id;
                        _spawnAt = _discardPos;
                    }
                    else
                    {
                        Log($"{who} pegou {e.Card} do descarte");
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
                    Log($"{who} BATEU{(e.Clean ? " LIMPO" : "")}! Os outros perdem {e.Amount}.");
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

            // Bots
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
            _cam.transform.position = camPos;
            _cam.transform.rotation = Quaternion.LookRotation(camLook - camPos);

            SyncHand();
            LayoutHand(camPos, dt);
            UpdateCenter();
            UpdateSeats();
            UpdateFlyers(dt);
            UpdateHints();
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
                var pos = HandCenter + right * x - up * (x * x * 0.35f) + toCam * (i * 0.003f);
                var rot = Quaternion.LookRotation(-toCam, up) * Quaternion.Euler(0, 0, -x * 35f);
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
                int want = _match.IsEliminated(s) ? 0 : _match.GetHand(s).Count;
                var list = _heldBacks[s];
                if (list.Count != want)
                {
                    foreach (var g in list) Destroy(g);
                    list.Clear();
                    var anchor = HeldAnchor(s);
                    var toCenter = (-_seatPos[s]).normalized;
                    for (int i = 0; i < want; i++)
                    {
                        var g = NewCardQuad("Verso", _world, keepCollider: false);
                        g.GetComponent<Renderer>().sharedMaterial = BackMat;
                        g.transform.localScale = new Vector3(CardW * 0.75f, CardH * 0.75f, 1);
                        float x = (i - (want - 1) / 2f) * 0.028f;
                        var side = Vector3.Cross(Vector3.up, toCenter).normalized;
                        g.transform.position = anchor + side * x - toCenter * (i * 0.002f) + Vector3.up * (-x * x * 1.5f);
                        g.transform.rotation = Quaternion.LookRotation(-toCenter, Vector3.up) * Quaternion.Euler(-15, 0, x * 120f);
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
            var toCenter = (-_seatPos[seat]).normalized;
            return _seatPos[seat] + toCenter * 0.42f + Vector3.up * 1.08f;
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
            string key = string.Join(",", hand.Select(c => c.Id).OrderBy(i => i)) + "|" + _match.Phase + "|" + _match.CurrentSeat;
            if (key == _hintKey) return;
            _hintKey = key;
            _hintInMeld = new HashSet<int>();
            _hintCanBater = false;
            if (hand.Count == 0) return;
            var arr = HandEvaluator.Arrange(hand, _match.Rules, _match.Jokers);
            foreach (var m in arr.Melds) foreach (var c in m.Cards) _hintInMeld.Add(c.Id);
            if (_match.CurrentSeat == Me && _match.Phase == TurnPhase.Descartar)
                _hintCanBater = SimpleBot.FindBater(hand, _match.GetView(Me), _match.PickedFromDiscard).HasValue;
        }

        // ================================================================ entrada (mouse/teclado via IMGUI, funciona com qualquer sistema de input)

        private void HandleWorldInput(bool roundOver)
        {
            var e = Event.current;
            if (_paused || _confirmBater) return;
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
                case EventType.MouseDown when e.button == 1:
                    _zoom = !_zoom;
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    _zoom = e.delta.y < 0;
                    e.Use();
                    break;
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Tab) { _zoom = !_zoom; e.Use(); }
                    else if (e.keyCode == KeyCode.LeftArrow) { MoveSelected(-1); e.Use(); }
                    else if (e.keyCode == KeyCode.RightArrow) { MoveSelected(1); e.Use(); }
                    else if (e.keyCode == KeyCode.Return && _selected.HasValue) { DoDiscard(_selected.Value); e.Use(); }
                    break;
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
            if (_paused) DrawPause();

            HandleWorldInput(roundOver);
        }

        private void DrawNameTags()
        {
            for (int s = 1; s < _match.PlayerCount; s++)
            {
                var world = _seatPos[s] + Vector3.up * 2.12f;
                var sp = _cam.WorldToScreenPoint(world);
                if (sp.z < 0) continue;
                float x = sp.x / UiKit.Scale, y = (Screen.height - sp.y) / UiKit.Scale;
                var r = new Rect(x - 130, y - 70, 260, 70);
                bool turn = _match.CurrentSeat == s && _match.Phase != TurnPhase.FimDaRodada;
                bool out_ = _match.IsEliminated(s);
                UiKit.Box(r, turn ? new Color(1f, 0.78f, 0.1f, 0.9f) : new Color(0, 0, 0, out_ ? 0.3f : 0.6f));
                var txt = turn ? Color.black : Color.white;
                UiKit.Label(new Rect(r.x + 10, r.y + 4, r.width - 20, 30),
                    _match.PlayerNames[s] + (_match.DealerSeat == s ? "  (D)" : ""), UiKit.Small, txt);
                UiKit.Label(new Rect(r.x + 10, r.y + 34, r.width - 20, 30),
                    out_ ? "fora da mesa" : $"{_match.GetTentos(s)} tentos  ·  {_match.GetHand(s).Count} cartas", UiKit.Tiny, txt);
            }
        }

        private void DrawTopBar()
        {
            if (GUI.Button(new Rect(20, 20, 70, 60), "II", UiKit.Button)) _paused = !_paused;
            var r = new Rect(105, 20, 620, 60);
            UiKit.Panel(r, 0.55f);
            GUI.Label(new Rect(r.x + 15, r.y + 14, r.width, 40),
                $"{Names.Of(_room.Mode)}  •  Rodada {_match.RoundNumber}  •  Curinga: {JokerText()}", UiKit.Small);

            string turn;
            if (_match.Phase == TurnPhase.FimDaRodada || _match.IsOver) turn = "";
            else if (_match.CurrentSeat == Me)
                turn = _match.Phase == TurnPhase.Comprar
                    ? "Sua vez: clique no MONTE ou no DESCARTE"
                    : "Sua vez: arraste uma carta até o descarte (ou selecione e Bata)";
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
                UiKit.Label(new Rect(r.x + 4, r.y + 18, r.width - 8, 22), $"♥{_match.GetTentos(s)}  {_match.GetHand(s).Count}c", UiKit.Tiny,
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

        private void DrawBottomBar()
        {
            float y = UiKit.H - 90, x = 30;
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
                "Arraste as cartas para reorganizar  •  clique para selecionar  •  botão direito, roda do mouse ou Espaço: zoom  •  Esc: pausa",
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
            else if (_lastBater != null)
                title = (_lastBater.Seat == Me ? "Você bateu" : $"{_match.PlayerNames[_lastBater.Seat]} bateu") + (_lastBater.Clean ? " LIMPO!" : "!");
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
                GUI.Label(new Rect(r.x + 40, y + 200, r.width - 80, 36), $"Cada um dos outros perdeu {_lastBater.Amount} tento(s).", UiKit.Text);
            }

            string tentos = string.Join("    ", Enumerable.Range(0, _match.PlayerCount)
                .Select(i => $"{(i == Me ? "Você" : _match.PlayerNames[i])}: {(_match.IsEliminated(i) ? "fora" : _match.GetTentos(i).ToString())}"));
            GUI.Label(new Rect(r.x + 40, r.yMax - 200, r.width - 80, 80), tentos, UiKit.Text);

            if (_match.IsOver)
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
