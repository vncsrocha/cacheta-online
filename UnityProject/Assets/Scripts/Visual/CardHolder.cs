using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Faz o personagem sentado segurar as cartas com as duas mãos, na frente do peito.
    /// <see cref="Fan"/> é o ponto onde o leque de cartas fica (quem desenha as cartas coloca elas dentro dele).
    /// Nos modelos 3D os braços são dobrados por IK de dois ossos (braço e antebraço) por cima da animação de
    /// sentado, a cada quadro; no boneco de formas simples o leque só fica na frente das mãos dele.
    /// </summary>
    public class CardHolder : MonoBehaviour
    {
        /// <summary>Centro do leque. +Z aponta para quem segura (as cartas mostram o verso para a mesa).</summary>
        public Transform Fan { get; private set; }

        /// <summary>Quantas cartas estão na mão (sem cartas, os braços descansam na pose da animação).</summary>
        public int Cards { get; set; } = 9;

        // Onde o leque fica em relação ao peito de quem segura, e onde os pulsos ficam em relação ao leque.
        // O leque fica na frente do peito, um pouco abaixo da linha dos ombros (os personagens têm tronco curto,
        // então o peito é medido pelos ombros e não pelos ossos da coluna).
        private const float FanForward = 0.3f, FanBelowShoulders = 0.06f, FanTilt = 18f;
        // O leque nunca desce até a mesa: o centro dele fica pelo menos isto acima do tampo.
        private const float FanAboveTable = 0.11f;
        private static readonly Vector3 WristOffset = new Vector3(0.085f, -0.1f, 0.05f); // x: para o lado, z: para perto do peito

        private Transform _body;
        private Arm _left, _right;
        private bool _rig;

        private class Arm
        {
            public Transform Upper, Lower, Hand;
            public Quaternion SetUpper, SetLower;   // o que a IK deixou no quadro anterior
            public Quaternion BaseUpper, BaseLower; // pose sem IK (para quando a animação não mexe no braço)
            public bool HasBase;
        }

        private void Awake()
        {
            _left = MakeArm("LeftArm", "LeftForeArm", "LeftHand");
            _right = MakeArm("RightArm", "RightForeArm", "RightHand");
            _rig = _left != null && _right != null;

            // No modelo 3D este componente fica no modelo, dentro do objeto do personagem (que está virado para a
            // mesa); no boneco simples ele fica no próprio objeto do personagem.
            _body = _rig && transform.parent != null ? transform.parent : transform;
            Fan = new GameObject("Leque de cartas").transform;
            Fan.SetParent(_body.parent != null ? _body.parent : _body, false);
        }

        private void OnDestroy()
        {
            if (Fan != null) Destroy(Fan.gameObject);
        }

        private Transform Bone(string name) => ModelAvatar.FindDeep(transform, name);

        private Arm MakeArm(string upper, string lower, string hand)
        {
            var a = new Arm { Upper = Bone(upper), Lower = Bone(lower), Hand = Bone(hand) };
            return a.Upper != null && a.Lower != null && a.Hand != null ? a : null;
        }

        private void LateUpdate()
        {
            var body = _body;
            var fwd = body.forward;
            var right = body.right;
            var up = Vector3.up;
            if (_rig)
            {
                // O lado direito vem dos próprios ombros (não depende de como o modelo foi importado).
                var across = Vector3.ProjectOnPlane(_right.Upper.position - _left.Upper.position, up);
                if (across.sqrMagnitude > 1e-4f) right = across.normalized;
            }

            var shoulders = _rig ? (_left.Upper.position + _right.Upper.position) / 2 : body.position + up * 0.85f;
            var fan = shoulders + fwd * FanForward - up * FanBelowShoulders;
            fan.y = Mathf.Max(fan.y, TableProps.TableTop + FanAboveTable);
            Fan.position = fan;
            // O verso das cartas fica virado para a mesa; o topo do leque inclina um pouco para quem segura.
            Fan.rotation = Quaternion.LookRotation(-fwd, up) * Quaternion.Euler(FanTilt, 0, 0);

            if (!_rig) return;
            bool holding = Cards > 0;
            var shoulderL = _left.Upper.position;
            var shoulderR = _right.Upper.position;
            Solve(_left, Wrist(-1, right, up, fwd), shoulderL - right * 0.3f - up * 0.5f - fwd * 0.05f, holding);
            Solve(_right, Wrist(1, right, up, fwd), shoulderR + right * 0.3f - up * 0.5f - fwd * 0.05f, holding);
        }

        private Vector3 Wrist(int side, Vector3 right, Vector3 up, Vector3 fwd)
            => Fan.position + right * (side * WristOffset.x) + up * WristOffset.y - fwd * WristOffset.z;

        /// <summary>IK de dois ossos com direção do cotovelo (pole), aplicada por cima da pose atual.</summary>
        private static void Solve(Arm arm, Vector3 target, Vector3 pole, bool active)
        {
            // Se a animação não reescreveu o braço neste quadro, volta para a pose sem IK antes de resolver de novo.
            if (!arm.HasBase)
            {
                arm.BaseUpper = arm.Upper.localRotation;
                arm.BaseLower = arm.Lower.localRotation;
                arm.HasBase = true;
            }
            else
            {
                if (Quaternion.Angle(arm.Upper.localRotation, arm.SetUpper) < 0.01f) arm.Upper.localRotation = arm.BaseUpper;
                else arm.BaseUpper = arm.Upper.localRotation;
                if (Quaternion.Angle(arm.Lower.localRotation, arm.SetLower) < 0.01f) arm.Lower.localRotation = arm.BaseLower;
                else arm.BaseLower = arm.Lower.localRotation;
            }

            if (active) TwoBoneIK.Solve(arm.Upper, arm.Lower, arm.Hand, target, pole);
            arm.SetUpper = arm.Upper.localRotation;
            arm.SetLower = arm.Lower.localRotation;
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
