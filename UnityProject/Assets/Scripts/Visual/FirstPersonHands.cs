using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// As suas mãos em primeira pessoa: seguram as duas pontas do leque de cartas. Cada mão tem palma (com a base do
    /// polegar e o lado do mínimo mais cheios), polegar e quatro dedos de três falanges cada, com as juntas marcadas e
    /// unhas. O polegar fica por cima da carta, com a unha para você; os outros dedos ficam retos por trás dela (sem
    /// cobrir o canto da carta) e o mínimo fecha por fora da beirada. Os dedos mexem um pouquinho, como quem segura de
    /// verdade. O antebraço desce até o
    /// seu corpo (abaixo da câmera). Cor da pele e da manga vêm do seu perfil.
    /// </summary>
    public class FirstPersonHands
    {
        private class Hand
        {
            public Transform Root;        // na pegada, com os eixos da carta: +X direita, +Y cima, +Z para trás da carta
            public Transform Wrist;       // pulso: +Y aponta para os dedos e a palma fica para -Z (para a carta)
            public Transform[][] Fingers; // indicador, médio, anelar e mínimo: juntas da base, do meio e da ponta
            public Transform[] Thumb;     // juntas do polegar (base, meio e ponta)
            public Transform Forearm, Sleeve;
            public int Side;              // -1 esquerda, +1 direita
            public float Phase;
        }

        // Medidas de uma mão adulta, em metros (o mundo está em metros).
        private static readonly float[][] Lengths =
        {
            new[] { 0.042f, 0.025f, 0.020f }, // indicador
            new[] { 0.046f, 0.028f, 0.021f }, // médio
            new[] { 0.043f, 0.026f, 0.020f }, // anelar
            new[] { 0.034f, 0.020f, 0.018f }, // mínimo
        };
        private static readonly float[] Widths = { 0.0185f, 0.019f, 0.0175f, 0.0155f };
        /// <summary>Onde cada dedo nasce na mão (x positivo = lado do polegar) e quanto ele abre para o lado (graus).</summary>
        private static readonly Vector3[] Knuckles =
        {
            new Vector3(0.026f, 0.092f, 0f), new Vector3(0.008f, 0.097f, 0f), new Vector3(-0.011f, 0.093f, 0f), new Vector3(-0.028f, 0.083f, 0f),
        };
        private static readonly float[] Splay = { 3f, 0f, -3f, -7f };
        /// <summary>
        /// Dobra de cada junta segurando a carta (graus: base, meio, ponta). Os dedos que ficam atrás da carta quase não
        /// dobram (dobrando, as pontas atravessariam a carta e apareceriam na frente dela); só o mínimo, que fica fora
        /// da beirada, fecha.
        /// </summary>
        private static readonly Vector3[] Curl =
        {
            new Vector3(2, 4, 2), new Vector3(3, 5, 2), new Vector3(4, 6, 3), new Vector3(55, 60, 40),
        };
        private static readonly float[] ThumbLengths = { 0.044f, 0.031f, 0.027f };
        private static readonly float[] ThumbWidths = { 0.026f, 0.022f, 0.021f };
        /// <summary>Dobra do polegar (graus; a base fica parada, quem dobra são o meio e a ponta, para a carta).</summary>
        private static readonly Vector3 ThumbCurl = new Vector3(0, 10, 16);

        private Hand _left, _right;
        private Material _skin, _nail, _shirt;

        public static FirstPersonHands Build(PlayerProfile p, Transform parent)
        {
            var skin = Pick(Palette.SkinTones, p.SkinTone);
            var h = new FirstPersonHands
            {
                _skin = Thin(skin, 1.6f),
                _nail = Thin(Color.Lerp(skin, new Color(1f, 0.9f, 0.88f), 0.5f), 1.2f),
                _shirt = Thin(Pick(Palette.ClothColors, p.ShirtColor), 2f),
            };
            var root = new GameObject("Suas mãos").transform;
            root.SetParent(parent, false);
            h._left = h.MakeHand(root, -1);
            h._right = h.MakeHand(root, 1);
            return h;
        }

        private static Color Pick(Color[] list, int i) => list[Mathf.Clamp(i, 0, list.Length - 1)];

        /// <summary>Material do jogo com contorno mais fino (as peças da mão são pequenas e ficam perto da câmera).</summary>
        private static Material Thin(Color c, float outline)
        {
            var m = new Material(Palette.Mat(c));
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", outline);
            return m;
        }

        private Hand MakeHand(Transform parent, int side)
        {
            var hand = new Hand { Side = side, Phase = side < 0 ? 0.3f : 1.9f };
            float st = -side; // o polegar fica do lado do meio do leque
            hand.Root = new GameObject(side < 0 ? "Mão esquerda" : "Mão direita").transform;
            hand.Root.SetParent(parent, false);

            // Pulso abaixo da pegada, com a mão inclinada para o meio do leque e a beirada do mínimo um pouco para a
            // frente (a mão abraça a ponta da carta). A beirada de baixo da carta passa no meio da palma, entre o
            // polegar e o indicador; a palma fica logo atrás da carta e um pouco para fora, para o mínimo passar da
            // beirada. Baixa o bastante para as pontas dos dedos não aparecerem em cima da carta.
            const float tilt = 14f, cup = 12f, gripAlong = 0.08f, behind = 0.02f, outward = 0.016f;
            hand.Wrist = new GameObject("Pulso").transform;
            hand.Wrist.SetParent(hand.Root, false);
            var q = Quaternion.Euler(0, 0, side * tilt) * Quaternion.Euler(0, side * cup, 0);
            hand.Wrist.localRotation = q;
            hand.Wrist.localPosition = -(q * new Vector3(0, gripAlong, 0)) + new Vector3(side * outward, 0, behind);

            var w = hand.Wrist;
            Piece(PrimitiveType.Sphere, w, new Vector3(0, 0.05f, 0.002f), Quaternion.identity, new Vector3(0.08f, 0.1f, 0.03f), _skin, "Palma", MeshKit.RoundBox());
            Piece(PrimitiveType.Sphere, w, new Vector3(st * 0.024f, 0.032f, -0.008f), Quaternion.Euler(0, 0, -st * 18f), new Vector3(0.036f, 0.056f, 0.026f), _skin, "Base do polegar");
            Piece(PrimitiveType.Sphere, w, new Vector3(-st * 0.026f, 0.038f, -0.004f), Quaternion.identity, new Vector3(0.03f, 0.06f, 0.024f), _skin, "Lado do mínimo");

            hand.Fingers = new Transform[4][];
            for (int k = 0; k < 4; k++)
            {
                var at = Knuckles[k];
                at.x *= st;
                hand.Fingers[k] = Chain(w, at, Quaternion.Euler(0, 0, -st * Splay[k]), Lengths[k], new[] { Widths[k], Widths[k] * 0.93f, Widths[k] * 0.86f }, k == 0 ? "Indicador" : k == 1 ? "Médio" : k == 2 ? "Anelar" : "Mínimo");
            }

            // Polegar: sai da base da palma para a frente (passa por baixo da beirada da carta) e deita sobre a carta,
            // apontando para cima e para o meio do leque, com a unha virada para você.
            var dir = new Vector3(st * 0.5f, 0.55f, -0.67f).normalized;
            var pad = Vector3.ProjectOnPlane(Vector3.forward, dir).normalized;
            hand.Thumb = Chain(w, new Vector3(st * 0.022f, 0.018f, -0.004f), Quaternion.LookRotation(-pad, dir), ThumbLengths, ThumbWidths, "Polegar");

            hand.Forearm = Palette.Prim(PrimitiveType.Capsule, parent, Vector3.zero, Vector3.one, Color.white, "Antebraço").transform;
            hand.Forearm.GetComponent<Renderer>().sharedMaterial = _skin;
            hand.Sleeve = Palette.Prim(PrimitiveType.Capsule, parent, Vector3.zero, Vector3.one, Color.white, "Manga").transform;
            hand.Sleeve.GetComponent<Renderer>().sharedMaterial = _shirt;
            foreach (var r in hand.Root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hand.Forearm.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hand.Sleeve.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Pose(hand, 0f);
            return hand;
        }

        /// <summary>
        /// Um dedo: três juntas encadeadas (cada uma dobra em volta do seu X, para o lado da palma), com a falange, a
        /// bolinha da junta e a unha na ponta.
        /// </summary>
        private Transform[] Chain(Transform parent, Vector3 pos, Quaternion rot, float[] lengths, float[] widths, string name)
        {
            var joints = new Transform[3];
            var at = parent;
            for (int j = 0; j < 3; j++)
            {
                var joint = new GameObject(name + " " + (j + 1)).transform;
                joint.SetParent(at, false);
                joint.localPosition = j == 0 ? pos : new Vector3(0, lengths[j - 1], 0);
                joint.localRotation = j == 0 ? rot : Quaternion.identity;
                joints[j] = joint;
                float len = lengths[j], wd = widths[j];
                Piece(PrimitiveType.Capsule, joint, new Vector3(0, len / 2f, 0), Quaternion.identity, new Vector3(wd, (len + wd) / 2f, wd * 0.9f), _skin, "Falange");
                if (j > 0) Piece(PrimitiveType.Sphere, joint, Vector3.zero, Quaternion.identity, Vector3.one * (wd * 1.06f), _skin, "Junta");
                if (j == 2) Piece(PrimitiveType.Sphere, joint, new Vector3(0, len * 0.62f, wd * 0.36f), Quaternion.Euler(8f, 0, 0), new Vector3(wd * 0.68f, len * 0.6f, wd * 0.24f), _nail, "Unha", MeshKit.RoundBox(0.35f));
                at = joint;
            }
            return joints;
        }

        private static void Piece(PrimitiveType type, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat, string name, Mesh mesh = null)
        {
            var go = Palette.Prim(type, parent, pos, scale, Color.white, name);
            go.transform.localRotation = rot;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (mesh != null) go.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        /// <summary>Dobra os dedos na pegada, com um mexer pequeno e lento de cada dedo e do polegar.</summary>
        private static void Pose(Hand h, float t)
        {
            for (int k = 0; k < 4; k++)
            {
                float wiggle = (Mathf.Sin(t * (0.9f + 0.21f * k) + h.Phase * (k + 2)) + 1f) * (k == 3 ? 2f : 0.6f); // só dobra um pouco mais, nunca estica
                var c = Curl[k];
                h.Fingers[k][0].localRotation = Base(h, k) * Quaternion.Euler(-(c.x + wiggle), 0, 0);
                h.Fingers[k][1].localRotation = Quaternion.Euler(-(c.y + wiggle * 0.6f), 0, 0);
                h.Fingers[k][2].localRotation = Quaternion.Euler(-(c.z + wiggle * 0.3f), 0, 0);
            }
            float thumb = Mathf.Sin(t * 0.6f + h.Phase * 3f) * 3f;
            h.Thumb[1].localRotation = Quaternion.Euler(-(ThumbCurl.y + thumb), 0, 0);
            h.Thumb[2].localRotation = Quaternion.Euler(-(ThumbCurl.z + thumb * 0.5f), 0, 0);
        }

        private static Quaternion Base(Hand h, int k) => Quaternion.Euler(0, 0, h.Side * Splay[k]);

        /// <summary>
        /// Coloca as mãos nas pontas do leque. <paramref name="eye"/> é a posição dos olhos sem o giro da cabeça
        /// (os cotovelos ficam presos ao corpo, abaixo dela).
        /// </summary>
        public void Place(Vector3 leftCard, Quaternion leftRot, Vector3 rightCard, Quaternion rightRot, bool visible,
                          Vector3 eye, float cardW, float cardH, float dt)
        {
            float k = 1f - Mathf.Exp(-14f * dt);
            PlaceHand(_left, leftCard, leftRot, visible, eye, cardW, cardH, k);
            PlaceHand(_right, rightCard, rightRot, visible, eye, cardW, cardH, k);
        }

        private void PlaceHand(Hand h, Vector3 card, Quaternion rot, bool visible, Vector3 eye, float cardW, float cardH, float k)
        {
            h.Root.gameObject.SetActive(visible);
            h.Forearm.gameObject.SetActive(visible);
            h.Sleeve.gameObject.SetActive(visible);
            if (!visible) return;

            // Pegada no canto de baixo da carta da ponta, por fora.
            var grip = card + rot * new Vector3(h.Side * cardW * 0.3f, -cardH * 0.36f, 0);
            h.Root.position = Vector3.Lerp(h.Root.position, grip, h.Root.position == Vector3.zero ? 1f : k);
            h.Root.rotation = Quaternion.Slerp(h.Root.rotation, rot, k);
            Pose(h, Time.time);

            // Antebraço do pulso até o cotovelo, junto do corpo, um pouco para o lado e abaixo da mesa.
            var wrist = h.Wrist.position;
            var elbow = eye + new Vector3(h.Side * 0.25f, -0.47f, 0.16f);
            Segment(h.Forearm, wrist, elbow, 0.056f);
            Segment(h.Sleeve, Vector3.Lerp(elbow, wrist, 0.42f), elbow + (elbow - wrist).normalized * 0.05f, 0.1f);
        }

        /// <summary>Estica uma cápsula entre dois pontos.</summary>
        private static void Segment(Transform t, Vector3 a, Vector3 b, float diameter)
        {
            var d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            t.position = (a + b) / 2;
            t.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            t.localScale = new Vector3(diameter, len / 2, diameter);
        }
    }
}
