using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Monta um personagem provisório com primitivos a partir do <see cref="PlayerProfile"/>.
    /// O personagem olha para +Z. Quando os modelos 3D reais existirem, esta classe passa a instanciar
    /// o prefab e trocar as peças (cabelo, roupa...), mantendo a mesma entrada.
    /// </summary>
    public static class AvatarBuilder
    {
        public static GameObject Build(PlayerProfile p, Transform parent, bool seated)
        {
            var root = new GameObject("Personagem " + p.Name);
            root.transform.SetParent(parent, false);
            Rebuild(root, p, seated);
            return root;
        }

        /// <summary>Apaga as peças e monta de novo (usado no editor de perfil).</summary>
        public static void Rebuild(GameObject root, PlayerProfile p, bool seated)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.Destroy(root.transform.GetChild(i).gameObject);

            var t = root.transform;
            Color skin = Palette.SkinTones[Mod(p.SkinTone, Palette.SkinTones.Length)];
            Color hair = Palette.HairColors[Mod(p.HairColor, Palette.HairColors.Length)];
            Color shirt = Palette.ClothColors[Mod(p.ShirtColor, Palette.ClothColors.Length)];
            Color pants = Palette.ClothColors[Mod(p.PantsColor, Palette.ClothColors.Length)];
            Color dark = new Color(0.08f, 0.08f, 0.1f);

            float shoulders = p.Gender == Gender.Masculino ? 0.62f : p.Gender == Gender.Feminino ? 0.5f : 0.56f;
            float hip = seated ? 0.55f : 0.95f;

            // Pernas
            if (seated)
            {
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(-0.13f, hip - 0.05f, 0.2f), new Vector3(0.18f, 0.18f, 0.5f), pants);
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(0.13f, hip - 0.05f, 0.2f), new Vector3(0.18f, 0.18f, 0.5f), pants);
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(-0.13f, 0.25f, 0.42f), new Vector3(0.16f, 0.5f, 0.16f), pants);
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(0.13f, 0.25f, 0.42f), new Vector3(0.16f, 0.5f, 0.16f), pants);
            }
            else
            {
                Palette.Prim(PrimitiveType.Capsule, t, new Vector3(-0.13f, 0.47f, 0f), new Vector3(0.18f, 0.47f, 0.18f), pants);
                Palette.Prim(PrimitiveType.Capsule, t, new Vector3(0.13f, 0.47f, 0f), new Vector3(0.18f, 0.47f, 0.18f), pants);
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(-0.13f, 0.04f, 0.05f), new Vector3(0.18f, 0.08f, 0.28f), dark);
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(0.13f, 0.04f, 0.05f), new Vector3(0.18f, 0.08f, 0.28f), dark);
            }

            // Tronco
            float torsoY = hip + 0.38f;
            Palette.Prim(PrimitiveType.Capsule, t, new Vector3(0, torsoY, 0), new Vector3(shoulders, 0.42f, 0.34f), shirt, "Tronco");
            if (p.Gender == Gender.Feminino)
                Palette.Prim(PrimitiveType.Cylinder, t, new Vector3(0, hip + 0.05f, 0), new Vector3(0.52f, 0.12f, 0.4f), shirt);

            // Braços (sentado: apoiados na mesa, à frente)
            float armY = torsoY + 0.18f;
            for (int s = -1; s <= 1; s += 2)
            {
                var arm = Palette.Prim(PrimitiveType.Capsule, t, new Vector3(s * (shoulders / 2 + 0.06f), armY - 0.12f, seated ? 0.18f : 0f),
                                       new Vector3(0.13f, 0.28f, 0.13f), shirt);
                arm.transform.localRotation = seated ? Quaternion.Euler(60f, 0, s * -15f) : Quaternion.Euler(0, 0, s * 8f);
                var hand = seated ? new Vector3(s * 0.2f, armY - 0.3f, 0.48f) : new Vector3(s * (shoulders / 2 + 0.1f), armY - 0.5f, 0f);
                Palette.Prim(PrimitiveType.Sphere, t, hand, Vector3.one * 0.12f, skin);
            }

            // Pescoço e cabeça
            float headY = torsoY + 0.62f;
            Palette.Prim(PrimitiveType.Cylinder, t, new Vector3(0, torsoY + 0.4f, 0), new Vector3(0.14f, 0.08f, 0.14f), skin);
            var head = Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY, 0), new Vector3(0.42f, 0.46f, 0.42f), skin, "Cabeca");

            // Rosto
            Palette.Prim(PrimitiveType.Sphere, t, new Vector3(-0.08f, headY + 0.04f, 0.19f), Vector3.one * 0.06f, dark);
            Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0.08f, headY + 0.04f, 0.19f), Vector3.one * 0.06f, dark);
            Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY - 0.03f, 0.21f), new Vector3(0.07f, 0.09f, 0.08f), skin * 0.92f);
            Palette.Prim(PrimitiveType.Cube, t, new Vector3(0, headY - 0.12f, 0.19f), new Vector3(0.1f, 0.02f, 0.02f),
                         p.Gender == Gender.Feminino ? new Color(0.75f, 0.15f, 0.2f) : skin * 0.6f);
            if (p.Mustache)
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(0, headY - 0.08f, 0.2f), new Vector3(0.14f, 0.035f, 0.03f), hair);
            if (p.Glasses)
            {
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(-0.08f, headY + 0.04f, 0.21f), new Vector3(0.1f, 0.07f, 0.01f), dark * 2f);
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(0.08f, headY + 0.04f, 0.21f), new Vector3(0.1f, 0.07f, 0.01f), dark * 2f);
                Palette.Prim(PrimitiveType.Cube, t, new Vector3(0, headY + 0.06f, 0.21f), new Vector3(0.06f, 0.015f, 0.01f), dark);
            }

            BuildHair(t, p.Hair, headY, hair);
            BuildHat(t, p.Hat, headY, Palette.ClothColors[Mod(p.ShirtColor + 3, Palette.ClothColors.Length)]);
            _ = head;
        }

        private static void BuildHair(Transform t, HairStyle style, float headY, Color c)
        {
            switch (style)
            {
                case HairStyle.Careca:
                    break;
                case HairStyle.Curto:
                    Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY + 0.08f, -0.03f), new Vector3(0.45f, 0.36f, 0.44f), c);
                    break;
                case HairStyle.BlackPower:
                    Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY + 0.12f, -0.06f), new Vector3(0.72f, 0.62f, 0.66f), c);
                    break;
                case HairStyle.Comprido:
                    Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY + 0.07f, -0.03f), new Vector3(0.47f, 0.4f, 0.46f), c);
                    Palette.Prim(PrimitiveType.Cube, t, new Vector3(0, headY - 0.22f, -0.14f), new Vector3(0.44f, 0.55f, 0.16f), c);
                    break;
                case HairStyle.Moicano:
                    Palette.Prim(PrimitiveType.Cube, t, new Vector3(0, headY + 0.24f, -0.04f), new Vector3(0.07f, 0.18f, 0.4f), c);
                    break;
                case HairStyle.Coque:
                    Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY + 0.08f, -0.03f), new Vector3(0.45f, 0.36f, 0.44f), c);
                    Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY + 0.26f, -0.12f), Vector3.one * 0.18f, c);
                    break;
            }
        }

        private static void BuildHat(Transform t, HatStyle style, float headY, Color c)
        {
            switch (style)
            {
                case HatStyle.Bone:
                    Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY + 0.13f, -0.01f), new Vector3(0.47f, 0.3f, 0.47f), c);
                    Palette.Prim(PrimitiveType.Cube, t, new Vector3(0, headY + 0.12f, 0.26f), new Vector3(0.34f, 0.025f, 0.2f), c * 0.8f);
                    break;
                case HatStyle.Panama:
                    Palette.Prim(PrimitiveType.Cylinder, t, new Vector3(0, headY + 0.14f, 0), new Vector3(0.7f, 0.012f, 0.7f), new Color(0.92f, 0.86f, 0.68f));
                    Palette.Prim(PrimitiveType.Cylinder, t, new Vector3(0, headY + 0.24f, 0), new Vector3(0.4f, 0.1f, 0.4f), new Color(0.92f, 0.86f, 0.68f));
                    Palette.Prim(PrimitiveType.Cylinder, t, new Vector3(0, headY + 0.17f, 0), new Vector3(0.41f, 0.025f, 0.41f), new Color(0.15f, 0.12f, 0.1f));
                    break;
                case HatStyle.Touca:
                    Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY + 0.12f, -0.02f), new Vector3(0.48f, 0.38f, 0.48f), c);
                    Palette.Prim(PrimitiveType.Sphere, t, new Vector3(0, headY + 0.33f, -0.02f), Vector3.one * 0.1f, Color.white);
                    break;
            }
        }

        private static int Mod(int i, int n) => ((i % n) + n) % n;
    }
}
