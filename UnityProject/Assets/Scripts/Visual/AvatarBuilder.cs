using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Monta o personagem provisório a partir do <see cref="PlayerProfile"/>, na proporção "meio-termo"
    /// escolhida (cabeça ~1:4,5 do corpo, formas gordinhas, mãos grandes), com contorno preto pelo shader
    /// Cacheta/ToonOutline. Só usa esferas e cápsulas, que têm normais suaves e deixam o contorno contínuo.
    /// O personagem olha para +Z. Quando os modelos 3D finais existirem, esta classe passa a instanciar o
    /// prefab e trocar as peças, mantendo a mesma entrada (o perfil).
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
            if (ModelAvatar.TryBuild(root.transform, p.Model, seated)) return; // modelo 3D pronto, se existir
            new Builder(root.transform, p, seated).Build();
        }

        private sealed class Builder
        {
            private readonly Transform _t;
            private readonly PlayerProfile _p;
            private readonly bool _seated;
            private readonly Color _skin, _hair, _shirt, _shirt2, _pants;
            private readonly Material _shirtMat;
            private static readonly Color Dark = new Color(0.08f, 0.07f, 0.09f);
            private static readonly Color White = new Color(0.97f, 0.97f, 0.95f);
            private static readonly Color GoldChain = new Color(0.95f, 0.75f, 0.2f);

            public Builder(Transform t, PlayerProfile p, bool seated)
            {
                _t = t;
                _p = p;
                _seated = seated;
                _skin = Pick(Palette.SkinTones, p.SkinTone);
                _hair = Pick(Palette.HairColors, p.HairColor);
                _shirt = Pick(Palette.ClothColors, p.ShirtColor);
                _shirt2 = Pick(Palette.ClothColors, p.ShirtColor2);
                _pants = Pick(Palette.ClothColors, p.PantsColor);
                _shirtMat = p.Pattern == ShirtPattern.Listrada ? Palette.ToonTextured(Palette.Stripes(_shirt, _shirt2, 3), Color.white) : null;
            }

            private GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Vector3? euler = null, Material mat = null)
            {
                var go = Palette.Prim(type, _t, pos, scale, color);
                if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
                if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
                return go;
            }

            private GameObject Shirt(Vector3 pos, Vector3 scale, Vector3? euler = null)
                => Part(PrimitiveType.Capsule, pos, scale, _shirt, euler, _shirtMat);

            public void Build()
            {
                float bodyW = _p.Body == BodyType.Magro ? 0.44f : _p.Body == BodyType.Medio ? 0.52f : 0.58f;
                if (_p.Gender == Gender.Feminino) bodyW -= 0.05f;
                float hipY = _seated ? 0.5f : 0.78f;

                Legs(hipY);

                // Quadril / bermuda
                Part(PrimitiveType.Capsule, new Vector3(0, hipY, 0), new Vector3(bodyW * 0.95f, 0.17f, 0.32f), _pants, new Vector3(0, 0, 90));

                // Tronco e barriga
                float torsoY = hipY + 0.34f;
                Shirt(new Vector3(0, torsoY, 0), new Vector3(bodyW, 0.36f, 0.36f));
                if (_p.Body != BodyType.Magro)
                {
                    float b = _p.Body == BodyType.Barrigudo ? 1f : 0.8f;
                    Shirt(new Vector3(0, torsoY - 0.1f, 0.06f * b), new Vector3(bodyW * 0.92f, 0.22f * b + 0.1f, 0.42f * b));
                }
                if (_p.Gender == Gender.Feminino)
                {
                    Shirt(new Vector3(-0.09f, torsoY + 0.1f, 0.13f), new Vector3(0.16f, 0.08f, 0.14f), new Vector3(90, 0, 0));
                    Shirt(new Vector3(0.09f, torsoY + 0.1f, 0.13f), new Vector3(0.16f, 0.08f, 0.14f), new Vector3(90, 0, 0));
                }

                Arms(torsoY, bodyW);

                // Pescoço, corrente e cabeça
                float neckY = torsoY + 0.36f;
                Part(PrimitiveType.Capsule, new Vector3(0, neckY, 0), new Vector3(0.15f, 0.07f, 0.15f), _skin);
                if (_p.Chain)
                    for (int i = -3; i <= 3; i++)
                        Part(PrimitiveType.Sphere, new Vector3(i * 0.025f, neckY - 0.06f - (3 - Mathf.Abs(i)) * 0.012f, 0.13f - Mathf.Abs(i) * 0.012f),
                             Vector3.one * 0.03f, GoldChain);

                Head(neckY + 0.27f);
            }

            private void Legs(float hipY)
            {
                bool shorts = _p.Bottom == Bottoms.Bermuda;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * 0.12f;
                    if (_seated)
                    {
                        // Coxa horizontal, canela vertical
                        Part(PrimitiveType.Capsule, new Vector3(x, hipY, 0.2f), new Vector3(0.22f, 0.22f, 0.22f), _pants, new Vector3(90, 0, 0));
                        Part(PrimitiveType.Capsule, new Vector3(x, 0.27f, 0.42f), new Vector3(shorts ? 0.14f : 0.18f, 0.22f, shorts ? 0.14f : 0.18f), shorts ? _skin : _pants);
                        Foot(new Vector3(x, 0.03f, 0.48f));
                    }
                    else
                    {
                        Part(PrimitiveType.Capsule, new Vector3(x, 0.6f, 0), new Vector3(0.22f, 0.2f, 0.22f), _pants);
                        Part(PrimitiveType.Capsule, new Vector3(x, 0.3f, 0), new Vector3(shorts ? 0.14f : 0.18f, 0.22f, shorts ? 0.14f : 0.18f), shorts ? _skin : _pants);
                        Foot(new Vector3(x, 0.03f, 0.04f));
                    }
                }
            }

            private void Foot(Vector3 pos)
            {
                if (_p.Shoes == Shoes.Tenis)
                {
                    Part(PrimitiveType.Capsule, pos + new Vector3(0, 0.04f, 0.02f), new Vector3(0.15f, 0.15f, 0.12f), White, new Vector3(90, 0, 0));
                    Part(PrimitiveType.Capsule, pos + new Vector3(0, 0.0f, 0.02f), new Vector3(0.16f, 0.16f, 0.04f), _shirt2, new Vector3(90, 0, 0));
                }
                else
                {
                    Part(PrimitiveType.Capsule, pos + new Vector3(0, 0.035f, 0.03f), new Vector3(0.13f, 0.13f, 0.08f), _skin, new Vector3(90, 0, 0));
                    Part(PrimitiveType.Capsule, pos + new Vector3(0, -0.01f, 0.03f), new Vector3(0.15f, 0.16f, 0.025f), Dark * 3f, new Vector3(90, 0, 0));
                }
            }

            private void Arms(float torsoY, float bodyW)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float sx = s * (bodyW / 2 + 0.06f);
                    if (_seated)
                    {
                        // Braços apoiados na mesa, mãos à frente
                        Shirt(new Vector3(sx, torsoY + 0.1f, 0.06f), new Vector3(0.17f, 0.12f, 0.17f), new Vector3(35, 0, s * -10));
                        Part(PrimitiveType.Capsule, new Vector3(s * (bodyW / 2), torsoY - 0.02f, 0.3f), new Vector3(0.13f, 0.19f, 0.13f), _skin, new Vector3(80, 0, s * -20));
                        Part(PrimitiveType.Sphere, new Vector3(s * 0.2f, torsoY - 0.04f, 0.5f), new Vector3(0.16f, 0.12f, 0.17f), _skin);
                    }
                    else
                    {
                        Shirt(new Vector3(sx, torsoY + 0.14f, 0), new Vector3(0.17f, 0.12f, 0.17f), new Vector3(0, 0, s * 12));
                        Part(PrimitiveType.Capsule, new Vector3(sx + s * 0.05f, torsoY - 0.1f, 0), new Vector3(0.13f, 0.19f, 0.13f), _skin, new Vector3(0, 0, s * 12));
                        Part(PrimitiveType.Sphere, new Vector3(sx + s * 0.09f, torsoY - 0.32f, 0.01f), new Vector3(0.15f, 0.17f, 0.12f), _skin);
                    }
                }
            }

            private void Head(float y)
            {
                Part(PrimitiveType.Sphere, new Vector3(0, y, 0), new Vector3(0.44f, 0.48f, 0.42f), _skin);
                // Orelhas
                Part(PrimitiveType.Sphere, new Vector3(-0.22f, y, -0.01f), new Vector3(0.07f, 0.12f, 0.08f), _skin);
                Part(PrimitiveType.Sphere, new Vector3(0.22f, y, -0.01f), new Vector3(0.07f, 0.12f, 0.08f), _skin);
                // Olhos (branco + pupila), sobrancelhas grossas
                for (int s = -1; s <= 1; s += 2)
                {
                    Part(PrimitiveType.Sphere, new Vector3(s * 0.085f, y + 0.05f, 0.17f), new Vector3(0.1f, 0.11f, 0.06f), White);
                    Part(PrimitiveType.Sphere, new Vector3(s * 0.08f, y + 0.045f, 0.2f), new Vector3(0.045f, 0.055f, 0.02f), Dark);
                    Part(PrimitiveType.Capsule, new Vector3(s * 0.085f, y + 0.125f, 0.18f), new Vector3(0.035f, 0.055f, 0.03f), _hair * 0.8f,
                         new Vector3(0, 0, 90 + s * 8));
                }
                // Nariz grande
                Part(PrimitiveType.Sphere, new Vector3(0, y - 0.01f, 0.215f), new Vector3(0.11f, 0.1f, 0.1f), _skin * 0.95f);
                // Boca (sorriso)
                Part(PrimitiveType.Capsule, new Vector3(0, y - 0.115f, 0.18f), new Vector3(0.03f, 0.06f, 0.02f),
                     _p.Gender == Gender.Feminino ? new Color(0.75f, 0.15f, 0.25f) : new Color(0.35f, 0.1f, 0.1f), new Vector3(0, 0, 90));
                if (_p.Mustache)
                {
                    Part(PrimitiveType.Capsule, new Vector3(-0.05f, y - 0.07f, 0.2f), new Vector3(0.06f, 0.07f, 0.05f), _hair, new Vector3(0, 0, 70));
                    Part(PrimitiveType.Capsule, new Vector3(0.05f, y - 0.07f, 0.2f), new Vector3(0.06f, 0.07f, 0.05f), _hair, new Vector3(0, 0, -70));
                }
                if (_p.Glasses)
                {
                    for (int s = -1; s <= 1; s += 2)
                        Part(PrimitiveType.Cylinder, new Vector3(s * 0.085f, y + 0.05f, 0.215f), new Vector3(0.12f, 0.004f, 0.12f), Dark, new Vector3(90, 0, 0));
                    Part(PrimitiveType.Capsule, new Vector3(0, y + 0.06f, 0.22f), new Vector3(0.015f, 0.03f, 0.015f), Dark, new Vector3(0, 0, 90));
                }
                Hair(y);
                Hat(y);
            }

            private void Hair(float y)
            {
                var c = _hair;
                switch (_p.Hair)
                {
                    case HairStyle.Curto: // cachinhos
                        Part(PrimitiveType.Sphere, new Vector3(0, y + 0.1f, -0.04f), new Vector3(0.44f, 0.34f, 0.42f), c);
                        for (int i = 0; i < 9; i++)
                        {
                            float a = (i / 8f - 0.5f) * 2.6f;
                            Part(PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * 0.19f, y + 0.17f + Mathf.Cos(a) * 0.06f, Mathf.Cos(a) * 0.04f - 0.04f),
                                 Vector3.one * 0.13f, c);
                        }
                        break;
                    case HairStyle.BlackPower:
                        Part(PrimitiveType.Sphere, new Vector3(0, y + 0.14f, -0.07f), new Vector3(0.72f, 0.62f, 0.64f), c);
                        break;
                    case HairStyle.Comprido:
                        Part(PrimitiveType.Sphere, new Vector3(0, y + 0.08f, -0.03f), new Vector3(0.48f, 0.4f, 0.46f), c);
                        Part(PrimitiveType.Capsule, new Vector3(0, y - 0.18f, -0.14f), new Vector3(0.42f, 0.3f, 0.16f), c);
                        break;
                    case HairStyle.Moicano:
                        for (int i = 0; i < 5; i++)
                            Part(PrimitiveType.Sphere, new Vector3(0, y + 0.22f - i * 0.02f, 0.12f - i * 0.08f), new Vector3(0.08f, 0.18f, 0.12f), c);
                        break;
                    case HairStyle.Coque:
                        Part(PrimitiveType.Sphere, new Vector3(0, y + 0.08f, -0.03f), new Vector3(0.46f, 0.38f, 0.45f), c);
                        Part(PrimitiveType.Sphere, new Vector3(0, y + 0.27f, -0.12f), Vector3.one * 0.19f, c);
                        break;
                }
            }

            private void Hat(float y)
            {
                var c = Pick(Palette.ClothColors, _p.ShirtColor + 3);
                var straw = new Color(0.92f, 0.86f, 0.66f);
                switch (_p.Hat)
                {
                    case HatStyle.Bone:
                        Part(PrimitiveType.Sphere, new Vector3(0, y + 0.13f, -0.01f), new Vector3(0.47f, 0.32f, 0.47f), c);
                        Part(PrimitiveType.Cylinder, new Vector3(0, y + 0.1f, 0.24f), new Vector3(0.3f, 0.012f, 0.22f), c * 0.85f, new Vector3(-8, 0, 0));
                        break;
                    case HatStyle.Panama:
                        Part(PrimitiveType.Cylinder, new Vector3(0, y + 0.14f, 0), new Vector3(0.68f, 0.012f, 0.68f), straw);
                        Part(PrimitiveType.Capsule, new Vector3(0, y + 0.23f, 0), new Vector3(0.38f, 0.1f, 0.38f), straw);
                        Part(PrimitiveType.Cylinder, new Vector3(0, y + 0.17f, 0), new Vector3(0.4f, 0.025f, 0.4f), Dark * 2f);
                        break;
                    case HatStyle.Touca:
                        Part(PrimitiveType.Sphere, new Vector3(0, y + 0.12f, -0.02f), new Vector3(0.48f, 0.38f, 0.48f), c);
                        Part(PrimitiveType.Sphere, new Vector3(0, y + 0.33f, -0.02f), Vector3.one * 0.11f, White);
                        break;
                }
            }

            private static Color Pick(Color[] arr, int i) => arr[((i % arr.Length) + arr.Length) % arr.Length];
        }
    }
}
