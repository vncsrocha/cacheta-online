using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// As suas mãos em primeira pessoa: seguram as duas pontas do leque de cartas, com o polegar por cima da carta e
    /// o antebraço descendo até o seu corpo (abaixo da câmera). Cor da pele e da manga vêm do seu perfil.
    /// </summary>
    public class FirstPersonHands
    {
        private class Hand
        {
            public Transform Root;   // na pegada (canto de baixo da carta da ponta)
            public Transform Forearm, Sleeve;
            public int Side;         // -1 esquerda, +1 direita
        }

        private Hand _left, _right;
        private Color _skin, _shirt;

        public static FirstPersonHands Build(PlayerProfile p, Transform parent)
        {
            var h = new FirstPersonHands
            {
                _skin = Pick(Palette.SkinTones, p.SkinTone),
                _shirt = Pick(Palette.ClothColors, p.ShirtColor),
            };
            var root = new GameObject("Suas mãos").transform;
            root.SetParent(parent, false);
            h._left = h.MakeHand(root, -1);
            h._right = h.MakeHand(root, 1);
            return h;
        }

        private static Color Pick(Color[] list, int i) => list[Mathf.Clamp(i, 0, list.Length - 1)];

        private Hand MakeHand(Transform parent, int side)
        {
            var hand = new Hand { Side = side };
            hand.Root = new GameObject(side < 0 ? "Mão esquerda" : "Mão direita").transform;
            hand.Root.SetParent(parent, false);
            // Eixos da mão = eixos da carta: +X para a direita, +Y para cima, +Z para trás da carta (longe de você).
            // Palma e dedos por trás da carta, aparecendo embaixo e do lado dela.
            Palette.Prim(PrimitiveType.Sphere, hand.Root, new Vector3(side * 0.006f, -0.012f, 0.022f), new Vector3(0.082f, 0.1f, 0.036f), _skin, "Palma");
            Palette.Prim(PrimitiveType.Capsule, hand.Root, new Vector3(side * 0.03f, 0.012f, 0.018f), new Vector3(0.026f, 0.034f, 0.026f), _skin, "Dedos")
                .transform.localRotation = Quaternion.Euler(0, 0, side * -20f);
            // Polegar por cima da frente da carta.
            Palette.Prim(PrimitiveType.Capsule, hand.Root, new Vector3(side * -0.014f, 0.004f, -0.011f), new Vector3(0.024f, 0.032f, 0.02f), _skin, "Polegar")
                .transform.localRotation = Quaternion.Euler(0, 0, side * 32f);
            hand.Forearm = Palette.Prim(PrimitiveType.Capsule, parent, Vector3.zero, Vector3.one, _skin, "Antebraço").transform;
            hand.Sleeve = Palette.Prim(PrimitiveType.Capsule, parent, Vector3.zero, Vector3.one, _shirt, "Manga").transform;
            foreach (var r in hand.Root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hand.Forearm.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hand.Sleeve.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return hand;
        }

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

            // Pulso logo abaixo da palma; cotovelo junto do corpo, um pouco para o lado e abaixo da mesa.
            var wrist = h.Root.TransformPoint(new Vector3(h.Side * 0.018f, -0.07f, 0.03f));
            var elbow = eye + new Vector3(h.Side * 0.25f, -0.47f, 0.16f);
            Segment(h.Forearm, wrist, elbow, 0.064f);
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
