using System.Collections.Generic;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>Jeitos de segurar as cartas.</summary>
    public enum HoldStyle
    {
        /// <summary>Duas mãos, leque na frente do peito.</summary>
        Duas,
        /// <summary>Leque erguido numa mão só, perto do rosto; a outra mão descansa na mesa.</summary>
        Alto,
        /// <summary>Debruçado, com as cartas baixas numa mão, perto da mesa, olhando para elas; a outra descansa na mesa.</summary>
        Mesa,
        /// <summary>Cotovelo apoiado na mesa e o leque levantado numa mão; a outra descansa na mesa.</summary>
        Cotovelo,
    }

    /// <summary>
    /// Faz o personagem sentado segurar as cartas e dá a cada um a sua postura.
    /// <see cref="Fan"/> é o ponto onde o leque de cartas fica (quem desenha as cartas coloca elas dentro dele, com
    /// <see cref="LayCard"/>).
    /// Nos modelos 3D, por cima da animação de sentado e a cada quadro:
    ///  1. postura: coluna mais ereta (cada um com a sua inclinação e um leve giro), ombros levantados (a animação
    ///     deixa todos com o ombro caído) e a cabeça olhando para um lado, com uma respiração lenta;
    ///  2. o jeito de segurar (<see cref="HoldStyle"/>) decide onde fica o leque e o que cada mão faz: segura o leque
    ///     por trás (dedos para cima, palma para quem segura) ou descansa na mesa com a palma para baixo;
    ///  3. braços dobrados por IK de dois ossos e mãos viradas para isso;
    ///  4. dedos (nos modelos com ossos de dedos): fecham nas cartas por trás do leque sem atravessar as cartas, a mão
    ///     da mesa encosta as pontas dos dedos no tampo e de vez em quando tamborila, e tudo mexe um pouquinho.
    /// O jeito vem do personagem (<see cref="StyleOf"/>) e o resto é sorteado pelo nome do modelo, sempre igual para
    /// o mesmo personagem. No boneco de formas simples o leque só fica na frente dele.
    /// </summary>
    public class CardHolder : MonoBehaviour
    {
        /// <summary>Centro do leque. +Z aponta para quem segura (as cartas mostram o verso para a mesa).</summary>
        public Transform Fan { get; private set; }

        /// <summary>Quantas cartas estão na mão (sem cartas, os braços descansam na pose da animação).</summary>
        public int Cards { get; set; } = 9;

        /// <summary>Jeito de segurar as cartas.</summary>
        public HoldStyle Style { get; set; }

        /// <summary>Tamanho das cartas na mão dos personagens (iguais às suas).</summary>
        public const float CardW = 0.085f, CardH = 0.125f;

        /// <summary>O jeito de cada personagem (pelo nome do modelo); quem não está aqui sorteia um.</summary>
        public static HoldStyle? StyleOf(string model)
        {
            switch (model)
            {
                case "TioBeto": return HoldStyle.Mesa;
                case "DonaCleide": return HoldStyle.Duas;
                case "Marcela": return HoldStyle.Alto;
                case "Ramiro": return HoldStyle.Cotovelo;
                case "Marquinho": return HoldStyle.Alto;
                case "Ana": return HoldStyle.Mesa;
                case "LittleTiger":
                case "LittleTigerRevelado": return HoldStyle.Cotovelo;
                default: return null;
            }
        }

        private Transform _body;
        private Arm _left, _right;
        private Transform _spineLow, _spineMid, _spineTop, _neck, _head, _hips, _clavL, _clavR;
        private SkinnedMeshRenderer _skin;
        private bool _rig;
        private readonly List<Guard> _guards = new List<Guard>();

        // Jeito de cada um (sorteado pelo nome, fixo por personagem).
        private uint _seed;
        private int _holdSide; // nos jeitos de uma mão: -1 esquerda, +1 direita
        private float _lean, _twist, _shrug, _lookYaw, _lookPitch, _fanTilt, _fanYaw, _spread, _phase;
        private Vector3 _elbowOnTable;

        private class Arm
        {
            public Transform Upper, Lower, Hand;
            public int LowerIndex = -1, HandIndex = -1; // índices no SkinnedMeshRenderer (pose de origem)
            public HandRig Rig;                         // ossos dos dedos (nulo nos modelos sem dedos)
        }

        private enum Role { Both, Hold, Rest }

        /// <summary>
        /// Ossos dos dedos de uma mão e as direções para dobrar cada um, no espaço do osso da mão. Os ossos dos dedos
        /// saem do modelo sem giro nenhum, então a mesma direção serve para as três falanges.
        /// </summary>
        private class HandRig
        {
            public readonly Transform[][] Fingers = new Transform[4][]; // indicador, médio, anelar e mínimo: 3 falanges
            public readonly Transform[] Tips = new Transform[4];        // ponta de cada dedo
            public readonly Vector3[] Axis = new Vector3[4];            // em volta do que cada dedo dobra
            public readonly float[] Spread = new float[4];              // graus que faltam para o dedo encostar no médio
            public readonly float[] Curl = new float[4];                // quanto cada dedo está dobrado agora (suavizado)
            public Transform[] Thumb;
            public Vector3 ThumbAxis;
            public Vector3 Along, Palm;  // para onde apontam os dedos e a palma
            public float KnuckleDepth;   // quanto os nós dos dedos ficam para o lado da palma, a partir do pulso
            public bool Posed;
        }

        /// <summary>Quanto cada articulação dobra, em graus (base, meio e ponta de cada dedo; o polegar igual).</summary>
        private sealed class Grip
        {
            public Vector3[] Fingers; // indicador, médio, anelar, mínimo
            public Vector3 Thumb;
            public float Together;    // 0 = dedos abertos como no modelo, 1 = todos encostados no médio
        }

        private static Grip MakeGrip(float together, Vector3 thumb, params Vector3[] fingers)
            => new Grip { Fingers = fingers, Thumb = thumb, Together = together };

        // As dobras são o máximo: segurando, cada dedo para quando a ponta chega nas cartas; na mesa, quando encosta no tampo.
        private static readonly Grip GripOne = MakeGrip(0.9f, new Vector3(25, 18, 12),
            new Vector3(30, 40, 20), new Vector3(34, 42, 22), new Vector3(50, 60, 35), new Vector3(58, 65, 40));
        private static readonly Grip GripTwo = MakeGrip(0.75f, new Vector3(22, 15, 10),
            new Vector3(26, 36, 18), new Vector3(30, 40, 20), new Vector3(45, 55, 32), new Vector3(52, 60, 36));
        private static readonly Grip GripTable = MakeGrip(0.3f, new Vector3(8, 10, 8),
            new Vector3(14, 20, 12), new Vector3(16, 22, 12), new Vector3(19, 25, 15), new Vector3(22, 27, 17));
        private static readonly Grip GripLoose = MakeGrip(0.2f, new Vector3(5, 8, 8),
            new Vector3(8, 12, 8), new Vector3(10, 14, 8), new Vector3(12, 16, 10), new Vector3(14, 18, 12));

        private static readonly string[] FingerNames = { "Index", "Middle", "Ring", "Pinky" };

        /// <summary>Guarda a rotação de um osso para desfazer o que a postura e a IK mexeram no quadro anterior.</summary>
        private class Guard
        {
            public Transform Bone;
            public Quaternion Set, Base;
            public bool HasBase;
        }

        private void Awake()
        {
            _left = MakeArm("LeftArm", "LeftForeArm", "LeftHand");
            _right = MakeArm("RightArm", "RightForeArm", "RightHand");
            _rig = _left != null && _right != null;
            _hips = Bone("Hips");
            _spineLow = Bone("Spine02");
            _spineMid = Bone("Spine01");
            _spineTop = Bone("Spine");
            _neck = Bone("neck") ?? Bone("Neck");
            _head = Bone("Head");
            _clavL = Bone("LeftShoulder");
            _clavR = Bone("RightShoulder");
            _skin = GetComponentInChildren<SkinnedMeshRenderer>();
            if (_rig)
            {
                _left.Rig = MakeHandRig(_left.Hand, "Left", true);
                _right.Rig = MakeHandRig(_right.Hand, "Right", false);
            }
            if (_rig && _skin != null)
            {
                var bones = _skin.bones;
                foreach (var a in new[] { _left, _right })
                {
                    a.LowerIndex = System.Array.IndexOf(bones, a.Lower);
                    a.HandIndex = System.Array.IndexOf(bones, a.Hand);
                }
            }
            foreach (var b in new[] { _spineLow, _spineMid, _spineTop, _neck, _head, _clavL, _clavR })
                if (b != null) _guards.Add(new Guard { Bone = b });
            if (_rig)
                foreach (var a in new[] { _left, _right })
                    foreach (var b in new[] { a.Upper, a.Lower, a.Hand })
                        _guards.Add(new Guard { Bone = b });

            // No modelo 3D este componente fica no modelo, dentro do objeto do personagem (que está virado para a
            // mesa); no boneco simples ele fica no próprio objeto do personagem.
            _body = _rig && transform.parent != null ? transform.parent : transform;
            Fan = new GameObject("Leque de cartas").transform;
            Fan.SetParent(_body.parent != null ? _body.parent : _body, false);

            // O mesmo sorteio da prévia (o nome do arquivo do modelo decide o jeito de cada um).
            _seed = Hash(name + ".glb");
            Style = StyleOf(name) ?? (HoldStyle)(_seed % 4);
            _holdSide = R(_seed, 12) < 0.5f ? -1 : 1;
            bool oneHand = Style != HoldStyle.Duas;
            _lean = 4 + R(_seed, 1) * 12 + (Style == HoldStyle.Mesa || Style == HoldStyle.Cotovelo ? 6 : 0);
            _twist = (R(_seed, 2) - 0.5f) * 14 + (oneHand ? _holdSide * 8 : 0);
            _shrug = 10 + R(_seed, 3) * 8;
            _lookYaw = (R(_seed, 4) - 0.5f) * (Style == HoldStyle.Mesa ? 20 : 40);
            _lookPitch = Style == HoldStyle.Mesa ? 20 + R(_seed, 5) * 6
                       : Style == HoldStyle.Alto ? 2 + R(_seed, 5) * 6
                       : 8 + R(_seed, 5) * 14;
            _fanTilt = Style == HoldStyle.Alto ? 4 + R(_seed, 8) * 8
                     : Style == HoldStyle.Mesa ? 36 + R(_seed, 8) * 8
                     : Style == HoldStyle.Cotovelo ? 10
                     : 8 + R(_seed, 8) * 8;
            _spread = Style == HoldStyle.Duas ? 0.032f + R(_seed, 9) * 0.004f : 0.026f;
            _fanYaw = (R(_seed, 10) - 0.5f) * 16 + (oneHand ? _holdSide * 8 : 0);
            _phase = R(_seed, 11) * 6.28f;
        }

        private void OnDestroy()
        {
            if (Fan != null) Destroy(Fan.gameObject);
        }

        private static uint Hash(string s)
        {
            uint h = 7;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }

        private static float R(uint seed, int i)
        {
            double x = System.Math.Sin((seed % 10000) * 12.9898 + i * 78.233) * 43758.5453;
            return (float)(x - System.Math.Floor(x));
        }

        private Transform Bone(string boneName) => ModelAvatar.FindDeep(transform, boneName);

        private Arm MakeArm(string upper, string lower, string hand)
        {
            var a = new Arm { Upper = Bone(upper), Lower = Bone(lower), Hand = Bone(hand) };
            return a.Upper != null && a.Lower != null && a.Hand != null ? a : null;
        }

        /// <summary>
        /// Acha os ossos dos dedos ({Lado}Hand{Dedo}1 a 4, o 4 é só a ponta) e mede a mão na pose de origem: para onde
        /// apontam os dedos, para onde fica a palma e em volta do que cada dedo dobra. Nulo se o modelo não tiver dedos.
        /// </summary>
        private static HandRig MakeHandRig(Transform hand, string side, bool left)
        {
            var rig = new HandRig();
            var pts = new Vector3[5][];
            for (int k = 0; k < 5; k++)
            {
                string finger = k < 4 ? FingerNames[k] : "Thumb";
                var bones = new Transform[4];
                var p = new Vector3[4];
                var at = Vector3.zero;
                var rot = Quaternion.identity;
                for (int j = 0; j < 4; j++)
                {
                    bones[j] = ModelAvatar.FindDeep(hand, side + "Hand" + finger + (j + 1));
                    if (bones[j] == null) return null;
                    at += rot * bones[j].localPosition;
                    rot *= bones[j].localRotation;
                    p[j] = at; // no espaço do osso da mão
                }
                pts[k] = p;
                var three = new[] { bones[0], bones[1], bones[2] };
                if (k < 4) { rig.Fingers[k] = three; rig.Tips[k] = bones[3]; }
                else rig.Thumb = three;
            }

            Vector3[] index = pts[0], middle = pts[1], pinky = pts[3], thumb = pts[4];
            rig.Along = (middle[3] - middle[0]).normalized;
            var toThumb = (index[0] - pinky[0]).normalized;
            // O Unity espelha o glTF no X, então a palma sai deste produto vetorial (conferido nas duas mãos).
            rig.Palm = (left ? Vector3.Cross(toThumb, rig.Along) : Vector3.Cross(rig.Along, toThumb)).normalized;
            for (int k = 0; k < 4; k++)
            {
                var u = (pts[k][3] - pts[k][0]).normalized;
                rig.Axis[k] = Vector3.Cross(u, rig.Palm).normalized;
                rig.Spread[k] = Vector3.SignedAngle(Vector3.ProjectOnPlane(u, rig.Palm), Vector3.ProjectOnPlane(rig.Along, rig.Palm), rig.Palm);
                rig.KnuckleDepth += Vector3.Dot(pts[k][0], rig.Palm) / 4f;
            }
            // O polegar dobra para a base do médio, um pouco acima da palma.
            float length = (middle[3] - middle[0]).magnitude;
            var ut = (thumb[3] - thumb[1]).normalized;
            var toPalm = Vector3.ProjectOnPlane(middle[0] + rig.Palm * (0.25f * length) - thumb[1], ut).normalized;
            rig.ThumbAxis = Vector3.Cross(ut, toPalm).normalized;
            return rig;
        }

        /// <summary>Quantas cartas de espessura o leque tem (as cartas ficam atrás do plano do leque, para a mesa).</summary>
        private float Stack => Mathf.Max(0, Cards - 1) * 0.0015f;

        /// <summary>Põe a carta i de n no leque (posição, giro e tamanho dentro de <see cref="Fan"/>).</summary>
        public void LayCard(Transform card, int i, int n)
        {
            float x = (i - (n - 1) / 2f) * _spread;
            card.localScale = new Vector3(CardW, CardH, 1);
            card.localPosition = new Vector3(x, -x * x * 1.5f, -i * 0.0015f);
            card.localRotation = Quaternion.Euler(0, 0, -x * 170f);
        }

        private void LateUpdate()
        {
            var body = _body;
            var fwd = body.forward;
            var right = body.right;
            var up = Vector3.up;

            if (_rig)
            {
                // Desfaz o que este componente mexeu no quadro anterior, se a animação não reescreveu o osso.
                foreach (var g in _guards)
                {
                    if (!g.HasBase) { g.Base = g.Bone.localRotation; g.HasBase = true; }
                    else if (Quaternion.Angle(g.Bone.localRotation, g.Set) < 0.01f) g.Bone.localRotation = g.Base;
                    else g.Base = g.Bone.localRotation;
                }
                // O lado direito vem dos próprios ombros (não depende de como o modelo foi importado).
                var across = Vector3.ProjectOnPlane(_right.Upper.position - _left.Upper.position, up);
                if (across.sqrMagnitude > 1e-4f) right = across.normalized;
                Posture(fwd, right, up);
            }

            // Leque: cada jeito põe ele num lugar, com a sua inclinação e um leve giro.
            var sh = _rig ? (_left.Upper.position + _right.Upper.position) / 2 : body.position + up * 0.85f;
            Vector3 fan;
            switch (Style)
            {
                case HoldStyle.Alto:
                    fan = sh + fwd * 0.3f + right * (_holdSide * 0.06f) + up * (0.02f + R(_seed, 7) * 0.06f);
                    break;
                case HoldStyle.Mesa:
                    fan = sh + fwd * 0.46f + right * (_holdSide * 0.05f);
                    fan.y = TableProps.TableTop + 0.1f;
                    break;
                case HoldStyle.Cotovelo:
                    _elbowOnTable = sh + right * (_holdSide * 0.12f) + fwd * 0.4f;
                    _elbowOnTable.y = TableProps.TableTop + 0.03f;
                    fan = _elbowOnTable + up * 0.28f - fwd * 0.12f;
                    break;
                default:
                    fan = sh + fwd * (0.27f + R(_seed, 6) * 0.07f) - up * (0.03f + R(_seed, 7) * 0.06f);
                    fan.y = Mathf.Max(fan.y, TableProps.TableTop + 0.15f);
                    break;
            }
            Fan.position = fan;
            var fanDir = (fwd + right * Mathf.Tan(_fanYaw * Mathf.Deg2Rad)).normalized;
            // O verso das cartas fica virado para a mesa; o topo do leque inclina para quem segura.
            Fan.rotation = Quaternion.LookRotation(-fanDir, up) * Quaternion.Euler(_fanTilt, 0, 0);

            if (!_rig) return;
            if (Cards > 0)
            {
                KeepFanInReach(right);
                UseArm(_left, -1, sh, right, up, fwd);
                UseArm(_right, 1, sh, right, up, fwd);
            }
            else
            {
                // Sem cartas os braços ficam na pose da animação, com os dedos soltos.
                foreach (var a in new[] { _left, _right })
                    if (a.Rig != null) PoseFingers(a.Rig, GripLoose, Role.Rest, a == _left ? -1 : 1, fit: false);
            }
            foreach (var g in _guards) g.Set = g.Bone.localRotation;
        }

        /// <summary>Coluna mais ereta, ombros levantados e cabeça olhando para o seu lado, com respiração.</summary>
        private void Posture(Vector3 fwd, Vector3 right, Vector3 up)
        {
            float t = Time.time;
            if (_hips != null && _neck != null)
            {
                float lean = (_lean + Mathf.Sin(t * 1.6f + _phase) * 1.2f) * Mathf.Deg2Rad;
                var want = up * Mathf.Cos(lean) + fwd * Mathf.Sin(lean);
                var q = Quaternion.FromToRotation(_neck.position - _hips.position, want);
                var spine = new List<Transform>();
                foreach (var b in new[] { _spineLow, _spineMid, _spineTop }) if (b != null) spine.Add(b);
                var step = Quaternion.Slerp(Quaternion.identity, q, 1f / Mathf.Max(1, spine.Count));
                foreach (var b in spine) b.rotation = step * b.rotation;
            }
            var top = _spineTop ?? _spineMid ?? _spineLow;
            if (top != null)
                top.rotation = Quaternion.FromToRotation(fwd, fwd + right * Mathf.Tan(_twist * Mathf.Deg2Rad)) * top.rotation;

            Shrug(_clavL, _left.Upper, up);
            Shrug(_clavR, _right.Upper, up);

            float yaw = _lookYaw + Mathf.Sin(t * 0.23f + _phase) * 8f;
            float pitch = _lookPitch + Mathf.Sin(t * 0.31f + _phase * 2) * 3f;
            var look = fwd + right * Mathf.Tan(yaw * Mathf.Deg2Rad) - up * Mathf.Tan(pitch * Mathf.Deg2Rad);
            var hq = Quaternion.FromToRotation(fwd, look);
            if (_neck != null) _neck.rotation = Quaternion.Slerp(Quaternion.identity, hq, 0.4f) * _neck.rotation;
            if (_head != null) _head.rotation = Quaternion.Slerp(Quaternion.identity, hq, 0.6f) * _head.rotation;
        }

        private void Shrug(Transform clavicle, Transform shoulder, Vector3 up)
        {
            if (clavicle == null) return;
            var d = shoulder.position - clavicle.position;
            var raised = Vector3.RotateTowards(d, up * d.magnitude, _shrug * Mathf.Deg2Rad, 0f);
            clavicle.rotation = Quaternion.FromToRotation(d, raised) * clavicle.rotation;
        }

        /// <summary>
        /// Decide o que o braço faz (segura o leque por trás ou descansa na mesa), leva o pulso até lá com IK, vira a
        /// mão e fecha os dedos.
        /// </summary>
        private void UseArm(Arm arm, int side, Vector3 sh, Vector3 right, Vector3 up, Vector3 fwd)
        {
            var fY = Fan.up;
            var fZ = Fan.forward; // para quem segura
            var role = RoleOf(side);
            Vector3 wrist, pole, fingers, palm;
            if (role == Role.Both)
            {
                // As duas mãos seguram os lados do leque; os dedos cobrem só a parte de baixo das cartas.
                wrist = HoldWrist(arm, side, role, right);
                pole = arm.Upper.position + right * (side * 0.3f) - up * 0.5f - fwd * 0.05f;
                fingers = (fY - right * (side * 0.45f)).normalized;
                palm = fZ;
            }
            else if (role == Role.Hold)
            {
                // Uma mão só segura o leque por baixo, pelo meio.
                wrist = HoldWrist(arm, side, role, right);
                pole = Style == HoldStyle.Cotovelo
                    ? _elbowOnTable - up * 0.3f + fwd * 0.1f
                    : arm.Upper.position + right * (side * 0.3f) - up * 0.5f - fwd * 0.05f;
                fingers = (fY - right * (side * 0.15f)).normalized;
                palm = fZ;
            }
            else
            {
                // A outra mão descansa na mesa, com a palma para baixo e os dedos para o meio da mesa.
                wrist = sh + right * (side * 0.17f) + fwd * 0.44f;
                wrist.y = TableProps.TableTop + 0.04f;
                wrist = InReach(arm, wrist);
                pole = arm.Upper.position + right * (side * 0.35f) - up * 0.3f;
                fingers = (fwd - right * (side * 0.35f)).normalized;
                palm = -up;
            }
            TwoBoneIK.Solve(arm.Upper, arm.Lower, arm.Hand, wrist, pole);
            OrientHand(arm, fingers, palm);
            if (arm.Rig != null)
                PoseFingers(arm.Rig, role == Role.Both ? GripTwo : role == Role.Hold ? GripOne : GripTable, role, side, fit: true);
        }

        private Role RoleOf(int side) => Style == HoldStyle.Duas ? Role.Both : side == _holdSide ? Role.Hold : Role.Rest;

        /// <summary>
        /// Onde fica o pulso da mão que segura: abaixo do leque e atrás das cartas. Com dedos, a mão fica mais para trás
        /// (os dedos dobram até encostar no verso das cartas) e mais alta (os dedos cobrem a parte de baixo do leque).
        /// </summary>
        private Vector3 HoldWrist(Arm arm, int side, Role role, Vector3 right)
        {
            var fY = Fan.up;
            var fZ = Fan.forward;
            bool fingers = arm.Rig != null;
            float depth = fingers ? Stack + 0.05f + arm.Rig.KnuckleDepth * arm.Hand.lossyScale.x : 0.02f;
            if (role == Role.Both)
                return Fan.position + right * (side * 0.13f) - fY * (fingers ? 0.125f : 0.19f) - fZ * depth;
            float low = Style == HoldStyle.Mesa ? (fingers ? 0.09f : 0.12f) : (fingers ? 0.12f : 0.17f);
            return Fan.position + right * (side * 0.03f) - fY * low - fZ * depth;
        }

        /// <summary>Até onde o braço alcança, quase esticado.</summary>
        private static float Reach(Arm a)
            => 0.97f * ((a.Lower.position - a.Upper.position).magnitude + (a.Hand.position - a.Lower.position).magnitude);

        /// <summary>
        /// Personagens de braço curto não alcançam o leque em alguns jeitos (ele ficava solto no ar): traz o leque para
        /// perto até a mão que segura alcançar.
        /// </summary>
        private void KeepFanInReach(Vector3 right)
        {
            var shift = Vector3.zero;
            for (int i = 0; i < 2; i++)
            {
                var arm = i == 0 ? _left : _right;
                int side = i == 0 ? -1 : 1;
                var role = RoleOf(side);
                if (role == Role.Rest) continue;
                var wrist = HoldWrist(arm, side, role, right);
                var d = wrist - arm.Upper.position;
                float reach = Reach(arm);
                if (d.magnitude <= reach) continue;
                var need = arm.Upper.position + d.normalized * reach - wrist;
                if (need.sqrMagnitude > shift.sqrMagnitude) shift = need;
            }
            Fan.position += shift;
        }

        /// <summary>A mão que descansa: se a mesa ficar longe demais, apoia mais perto do corpo, na mesma altura.</summary>
        private static Vector3 InReach(Arm arm, Vector3 wrist)
        {
            var s = arm.Upper.position;
            var d = wrist - s;
            float reach = Reach(arm);
            if (d.magnitude <= reach) return wrist;
            var flat = new Vector3(d.x, 0f, d.z);
            float h = reach > Mathf.Abs(d.y) ? Mathf.Sqrt(reach * reach - d.y * d.y) : 0f;
            return s + flat.normalized * h + Vector3.up * d.y;
        }

        /// <summary>
        /// Fecha os dedos no jeito pedido. Com <paramref name="fit"/>, cada dedo para antes de atravessar as cartas
        /// (segurando) ou o tampo da mesa (descansando: aí as pontas encostam na mesa). Por cima disso, um mexer bem
        /// pequeno de cada dedo e do polegar e, na mão da mesa, o tamborilar de vez em quando.
        /// </summary>
        private void PoseFingers(HandRig rig, Grip grip, Role role, int side, bool fit)
        {
            float t = Time.time;
            float smooth = rig.Posed ? 1f - Mathf.Exp(-12f * Time.deltaTime) : 1f;
            bool table = role == Role.Rest;
            for (int k = 0; k < 4; k++)
            {
                float c = fit ? FitFinger(rig, k, grip, table ? 1.6f : 1f, table) : 1f;
                rig.Curl[k] = Mathf.Lerp(rig.Curl[k], c, smooth);
                float wiggle = Mathf.Sin(t * (0.8f + 0.17f * k) + _phase * (k + 1) + side) * 1.5f;
                float lift = table && fit ? Drum(t, k) * 24f : 0f;
                SetFinger(rig, k, grip, rig.Curl[k], wiggle - lift);
            }
            rig.Posed = true;
            float thumb = Mathf.Sin(t * 0.5f + _phase * 2f + side) * 3f;
            rig.Thumb[0].localRotation = Quaternion.AngleAxis(grip.Thumb.x + thumb, rig.ThumbAxis);
            rig.Thumb[1].localRotation = Quaternion.AngleAxis(grip.Thumb.y + thumb * 0.5f, rig.ThumbAxis);
            rig.Thumb[2].localRotation = Quaternion.AngleAxis(grip.Thumb.z + thumb * 0.5f, rig.ThumbAxis);
        }

        /// <summary>
        /// Dobra um dedo: <paramref name="amount"/> vezes o que o jeito pede, mais <paramref name="extra"/> graus na base
        /// (negativo levanta o dedo). Antes de dobrar, o dedo chega para perto do médio.
        /// </summary>
        private static void SetFinger(HandRig rig, int k, Grip grip, float amount, float extra)
        {
            var b = rig.Fingers[k];
            var deg = grip.Fingers[k];
            var axis = rig.Axis[k];
            b[0].localRotation = Quaternion.AngleAxis(rig.Spread[k] * grip.Together, rig.Palm)
                                 * Quaternion.AngleAxis(deg.x * amount + extra, axis);
            b[1].localRotation = Quaternion.AngleAxis(deg.y * amount + extra * 0.35f, axis);
            b[2].localRotation = Quaternion.AngleAxis(deg.z * amount, axis);
        }

        /// <summary>
        /// O quanto dá para dobrar o dedo (0 a <paramref name="max"/>) sem a ponta atravessar o limite: o verso das
        /// cartas ou o tampo da mesa. Busca binária na dobra; o dedo fica dobrado no fim (quem chama ajusta de novo).
        /// </summary>
        private float FitFinger(HandRig rig, int k, Grip grip, float max, bool table)
        {
            SetFinger(rig, k, grip, max, 0f);
            if (TipOk(rig.Tips[k].position, true, table)) return max;
            SetFinger(rig, k, grip, 0f, 0f);
            if (!TipOk(rig.Tips[k].position, false, table)) return 0f;
            float lo = 0f, hi = max;
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) / 2f;
                SetFinger(rig, k, grip, mid, 0f);
                if (TipOk(rig.Tips[k].position, false, table)) lo = mid;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// A ponta do dedo está no lugar? Na mesa: acima do tampo. Segurando: atrás da última carta; com o dedo todo
        /// dobrado (<paramref name="full"/>), também vale ficar fora do leque (abaixo dele ou do lado), onde o dedo pode
        /// fechar à vontade.
        /// </summary>
        private bool TipOk(Vector3 tip, bool full, bool table)
        {
            if (table) return tip.y >= TableProps.TableTop + 0.008f;
            var l = Fan.InverseTransformPoint(tip);
            if (full)
            {
                float half = Mathf.Max(0, Cards - 1) / 2f * _spread + CardW / 2f;
                if (l.y < -CardH / 2f - 1.5f * l.x * l.x - 0.01f || Mathf.Abs(l.x) > half + 0.005f) return true;
            }
            return -l.z >= Stack + 0.006f;
        }

        /// <summary>
        /// Mania de tamborilar na mesa (nem todo mundo tem): de tempos em tempos os dedos batem do mínimo para o
        /// indicador, duas vezes. Devolve quanto o dedo está levantado (0 a 1).
        /// </summary>
        private float Drum(float t, int finger)
        {
            if (R(_seed, 13) < 0.35f) return 0f;
            float period = 7f + R(_seed, 14) * 6f;
            float u = Mathf.Repeat(t + _phase * 2f, period);
            float lift = 0f;
            for (int round = 0; round < 2; round++)
            {
                float x = (u - round * 0.55f - (3 - finger) * 0.09f) / 0.2f;
                if (x > 0f && x < 1f) lift = Mathf.Max(lift, Mathf.Sin(x * Mathf.PI));
            }
            return lift;
        }

        /// <summary>Vira a mão para os dedos apontarem em <paramref name="fingers"/> com a palma para <paramref name="palm"/>.</summary>
        private void OrientHand(Arm arm, Vector3 fingers, Vector3 palm)
        {
            if (arm.Rig != null)
            {
                // Com dedos, a mão sabe para onde aponta e onde fica a palma (vale para qualquer modelo, mesmo os que
                // vieram com a palma virada para trás na pose de origem).
                arm.Hand.rotation = Quaternion.LookRotation(fingers, palm)
                                    * Quaternion.Inverse(Quaternion.LookRotation(arm.Rig.Along, arm.Rig.Palm));
                return;
            }
            if (_skin == null || arm.LowerIndex < 0 || arm.HandIndex < 0) return;
            // Pose de origem (A-pose) da mão: dedos na direção do antebraço e palma virada para o meio do corpo.
            var toWorld = _skin.transform.localToWorldMatrix;
            var binds = _skin.sharedMesh.bindposes;
            var bindLower = toWorld * binds[arm.LowerIndex].inverse;
            var bindHand = toWorld * binds[arm.HandIndex].inverse;
            Vector3 pLower = bindLower.GetColumn(3), pHand = bindHand.GetColumn(3);
            var fingersBind = (pHand - pLower).normalized;
            var meshX = _skin.transform.TransformDirection(Vector3.right).normalized;
            float hx = Vector3.Dot(pHand - _skin.transform.position, meshX);
            var palmBind = meshX * (hx > 0 ? -1f : 1f);
            arm.Hand.rotation = Quaternion.LookRotation(fingers, palm)
                                * Quaternion.Inverse(Quaternion.LookRotation(fingersBind, palmBind))
                                * bindHand.rotation;
        }
    }

    /// <summary>IK analítica de dois ossos (lei dos cossenos), igual para braços e pernas de qualquer esqueleto.</summary>
    public static class TwoBoneIK
    {
        public static void Solve(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            const float eps = 0.001f;
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float lab = (pb - pa).magnitude, lcb = (pc - pb).magnitude;
            if (lab < eps || lcb < eps) return;
            float lat = Mathf.Clamp((target - pa).magnitude, eps, lab + lcb - eps);

            // 1) Abre ou fecha o cotovelo para a distância ombro-pulso ficar igual à do alvo.
            float ac_ab0 = Angle(pc - pa, pb - pa);
            float ba_bc0 = Angle(pa - pb, pc - pb);
            float ac_ab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float ba_bc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));
            var axis0 = Vector3.Cross(pc - pa, pb - pa);
            if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(pc - pa, pole - pa);
            if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.up;
            axis0.Normalize();
            a.rotation = Quaternion.AngleAxis((ac_ab1 - ac_ab0) * Mathf.Rad2Deg, axis0) * a.rotation;
            b.rotation = Quaternion.AngleAxis((ba_bc1 - ba_bc0) * Mathf.Rad2Deg, axis0) * b.rotation;

            // 2) Aponta o braço inteiro para o alvo.
            pc = c.position;
            a.rotation = Quaternion.FromToRotation(pc - pa, target - pa) * a.rotation;

            // 3) Gira o braço em volta da linha ombro-alvo até o cotovelo apontar para o pole.
            var axis = (target - pa).normalized;
            var elbow = Vector3.ProjectOnPlane(b.position - pa, axis);
            var want = Vector3.ProjectOnPlane(pole - pa, axis);
            if (elbow.sqrMagnitude > 1e-8f && want.sqrMagnitude > 1e-8f)
                a.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbow, want, axis), axis) * a.rotation;
        }

        private static float Angle(Vector3 u, Vector3 v)
            => Mathf.Acos(Mathf.Clamp(Vector3.Dot(u.normalized, v.normalized), -1f, 1f));
    }
}
