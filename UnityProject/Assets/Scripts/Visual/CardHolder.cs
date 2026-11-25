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
    ///  3. braços dobrados por IK de dois ossos e mãos viradas para isso.
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
        }

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
                UseArm(_left, -1, sh, right, up, fwd);
                UseArm(_right, 1, sh, right, up, fwd);
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
        /// Decide o que o braço faz (segura o leque por trás ou descansa na mesa), leva o pulso até lá com IK e vira a
        /// mão.
        /// </summary>
        private void UseArm(Arm arm, int side, Vector3 sh, Vector3 right, Vector3 up, Vector3 fwd)
        {
            var fY = Fan.up;
            var fZ = Fan.forward; // para quem segura
            bool both = Style == HoldStyle.Duas;
            Vector3 wrist, pole, fingers, palm;
            if (both)
            {
                // As duas mãos seguram os lados do leque; os dedos cobrem só a parte de baixo das cartas.
                wrist = Fan.position + right * (side * 0.13f) - fY * 0.19f - fZ * 0.02f;
                pole = arm.Upper.position + right * (side * 0.3f) - up * 0.5f - fwd * 0.05f;
                fingers = (fY - right * (side * 0.45f)).normalized;
                palm = fZ;
            }
            else if (side == _holdSide)
            {
                // Uma mão só segura o leque por baixo, pelo meio.
                wrist = Fan.position + right * (side * 0.03f) - fY * (Style == HoldStyle.Mesa ? 0.12f : 0.17f) - fZ * 0.02f;
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
                pole = arm.Upper.position + right * (side * 0.35f) - up * 0.3f;
                fingers = (fwd - right * (side * 0.35f)).normalized;
                palm = -up;
            }
            TwoBoneIK.Solve(arm.Upper, arm.Lower, arm.Hand, wrist, pole);
            OrientHand(arm, fingers, palm);
        }

        /// <summary>Vira a mão para os dedos apontarem em <paramref name="fingers"/> com a palma para <paramref name="palm"/>.</summary>
        private void OrientHand(Arm arm, Vector3 fingers, Vector3 palm)
        {
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
