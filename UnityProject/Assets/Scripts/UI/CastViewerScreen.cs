using System.Collections.Generic;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Elenco nos cenários: mostra cada chefe na casa dele, sentado de frente para você na mesa (com mais dois
    /// personagens nos lados) ou em pé ao lado da mesa, para revisar os modelos 3D dentro do jogo.
    /// Arraste com o mouse para girar a câmera e use a roda para aproximar.
    /// </summary>
    public class CastViewerScreen : IScreen
    {
        private readonly GameApp _app;
        private List<PlayerProfile> _cast;
        private int _index;
        private bool _standing;
        private GameObject _root;

        // Câmera em órbita ao redor do alvo
        private Vector3 _target;
        private float _yaw, _pitch, _dist;
        private bool _dragging;

        private static readonly Vector3 PlayerEye = new Vector3(0, 1.5f, -1.95f);
        private static readonly Vector3 StandingSpot = new Vector3(1.05f, 0, -1.05f);
        private const float PanelH = 150f;

        public CastViewerScreen(GameApp app) => _app = app;

        public void Enter()
        {
            _cast = PlayerProfile.Chefes();
            _cast.Add(PlayerProfile.LittleTiger(revelado: true));
            _index = Mathf.Clamp(_index, 0, _cast.Count - 1);
            _app.Cam.fieldOfView = 55f;
            Build();
        }

        public void Exit()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        private PlayerProfile Current => _cast[_index];

        private EnvironmentId CurrentEnvironment
            => Current.Model == "LittleTigerRevelado" || Current.Name == "Little Tiger" ? EnvironmentId.SalaVip : Names.EnvironmentOf(Current.Name);

        private void Build()
        {
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("Elenco no cenário");
            var world = _root.transform;
            var look = EnvironmentBuilder.Build(CurrentEnvironment, world, _app.Cam);
            TableProps.Table(world, look);

            // Mesa de 4: você, alguém à esquerda, o chefe de frente e alguém à direita.
            var seats = TableProps.Seats(4);
            var others = new List<PlayerProfile>();
            for (int k = 1; others.Count < 2 && k < _cast.Count; k++)
            {
                var p = _cast[(_index + k) % _cast.Count];
                if (p.Name != Current.Name) others.Add(p);
            }
            for (int i = 1; i < 4; i++) TableProps.Chair(world, seats[i], look);
            TableProps.Seated(others[0], world, seats[1]);
            TableProps.Seated(others[1], world, seats[3]);

            if (_standing)
            {
                var av = AvatarBuilder.Build(Current, world, seated: false);
                av.transform.position = StandingSpot;
                av.transform.rotation = Quaternion.Euler(0, 200f, 0); // de frente para a câmera, levemente virado
                float h = ModelAvatar.StandingHeight * Current.Height / ModelAvatar.ReferenceHeight;
                _target = StandingSpot + Vector3.up * h * 0.55f;
                SetOrbit(_target + new Vector3(-0.6f, 0.05f, -2.9f));
            }
            else
            {
                TableProps.Seated(Current, world, seats[2]);
                _target = seats[2] + Vector3.up * 1.2f;
                SetOrbit(PlayerEye);
            }
        }

        /// <summary>Coloca a câmera na posição dada, olhando para o alvo.</summary>
        private void SetOrbit(Vector3 camPos)
        {
            var dir = camPos - _target;
            _dist = dir.magnitude;
            _pitch = Mathf.Asin(Mathf.Clamp(dir.y / _dist, -1f, 1f)) * Mathf.Rad2Deg;
            _yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            var rot = Quaternion.Euler(_pitch, _yaw, 0);
            var cam = _app.Cam.transform;
            cam.position = _target + rot * new Vector3(0, 0, -_dist);
            cam.rotation = Quaternion.LookRotation(_target - cam.position);
        }

        public void Tick() => ApplyCamera();

        /// <summary>Mouse e teclado pelo IMGUI (funciona com qualquer sistema de input do projeto).</summary>
        private void HandleInput()
        {
            var e = Event.current;
            bool overPanel = e.mousePosition.y > UiKit.H - PanelH;
            switch (e.type)
            {
                case EventType.KeyDown when e.keyCode == KeyCode.RightArrow: Step(1); e.Use(); break;
                case EventType.KeyDown when e.keyCode == KeyCode.LeftArrow: Step(-1); e.Use(); break;
                case EventType.KeyDown when e.keyCode == KeyCode.Space: _standing = !_standing; Build(); e.Use(); break;
                case EventType.MouseDown when e.button == 0 && !overPanel: _dragging = true; break;
                case EventType.MouseUp: _dragging = false; break;
                case EventType.MouseDrag when _dragging:
                    _yaw += e.delta.x * 0.35f;
                    _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.25f, -10f, 75f);
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    _dist = Mathf.Clamp(_dist * (1f + e.delta.y * 0.04f), 0.5f, 8f);
                    e.Use();
                    break;
            }
        }

        private void Step(int d)
        {
            _index = (_index + d + _cast.Count) % _cast.Count;
            Build();
        }

        public void Draw()
        {
            HandleInput();
            var p = Current;
            bool hasModel = ModelAvatar.Exists(p.Model);
            string nome = p.Model == "LittleTigerRevelado" ? "Little Tiger (revelado)" : p.Model == "LittleTiger" ? "Little Tiger (disfarçado)" : p.Name;

            var info = new Rect(30, 30, 760, 190);
            UiKit.Panel(info, 0.55f);
            GUI.Label(new Rect(50, 40, 720, 56), nome, UiKit.Subtitle);
            UiKit.Label(new Rect(50, 100, 720, 34), $"Cenário: {Names.Of(CurrentEnvironment)}   ·   {_index + 1} de {_cast.Count}", UiKit.Small);
            UiKit.Label(new Rect(50, 136, 720, 34),
                hasModel ? $"Modelo 3D: Personagens/{p.Model}.glb  ·  {p.Height:0.00} m" : $"Sem modelo 3D (falta Personagens/{p.Model}.glb): formas simples",
                UiKit.Small, hasModel ? new Color(0.75f, 1f, 0.75f) : new Color(1f, 0.8f, 0.5f));
            UiKit.Label(new Rect(50, 172, 720, 30), "Arraste para girar · roda do mouse aproxima · setas trocam de personagem · Espaço: em pé / na mesa", UiKit.Tiny, new Color(1, 1, 1, 0.7f));

            float y = UiKit.H - PanelH + 35, h = 80, gap = 20;
            UiKit.Panel(new Rect(0, UiKit.H - PanelH, UiKit.W, PanelH), 0.5f);
            float x = 40;
            if (GUI.Button(new Rect(x, y, 260, h), "Voltar", UiKit.Button)) { _app.Go(ScreenId.MainMenu); return; }
            x += 260 + gap * 3;
            if (GUI.Button(new Rect(x, y, 240, h), "◀ Anterior", UiKit.Button)) Step(-1);
            x += 240 + gap;
            if (GUI.Button(new Rect(x, y, 240, h), "Próximo ▶", UiKit.Button)) Step(1);
            x += 240 + gap * 3;
            if (GUI.Button(new Rect(x, y, 300, h), _standing ? "Ver na mesa" : "Ver em pé", UiKit.Selected)) { _standing = !_standing; Build(); }
            x += 300 + gap;
            if (GUI.Button(new Rect(x, y, 320, h), _standing ? "Câmera de frente" : "Visão do jogador", UiKit.Button)) Build();
        }
    }
}
