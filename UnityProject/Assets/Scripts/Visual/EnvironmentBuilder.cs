using UnityEngine;

namespace Cacheta.App
{
    /// <summary>Cores que cada ambiente usa para a mesa e as cadeiras.</summary>
    public struct EnvironmentLook
    {
        public Color Table;
        public Color TableEdge;
        public Color Chair;
    }

    /// <summary>
    /// Monta os ambientes provisórios com primitivos (chão, fundo, alguns objetos e luz).
    /// Cada ambiente vai virar uma cena/prefab de arte de verdade; a escolha continua vindo do <see cref="EnvironmentId"/>.
    /// O centro da mesa fica sempre na origem.
    /// </summary>
    public static class EnvironmentBuilder
    {
        public static EnvironmentLook Build(EnvironmentId id, Transform parent, Camera cam)
        {
            var root = new GameObject("Ambiente " + id).transform;
            root.SetParent(parent, false);
            switch (id)
            {
                case EnvironmentId.Boteco: return Boteco(root, cam);
                case EnvironmentId.Quiosque: return Quiosque(root, cam);
                default: return Quintal(root, cam);
            }
        }

        private static EnvironmentLook Quintal(Transform r, Camera cam)
        {
            Sky(cam, new Color(0.98f, 0.55f, 0.35f));
            Light(r, new Color(1f, 0.7f, 0.45f), 0.9f, new Vector3(18, -60, 0), new Color(0.45f, 0.35f, 0.45f));
            Palette.Prim(PrimitiveType.Plane, r, new Vector3(0, 0, 0), new Vector3(4, 1, 4), new Color(0.42f, 0.5f, 0.28f), "Gramado");
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(0, 0.002f, 0), new Vector3(5f, 0.004f, 5f), new Color(0.6f, 0.55f, 0.48f), "Piso de cimento");
            // Muro
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(0, 1.1f, 6f), new Vector3(16f, 2.2f, 0.3f), new Color(0.88f, 0.78f, 0.62f), "Muro");
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(-8f, 1.1f, 0), new Vector3(0.3f, 2.2f, 12f), new Color(0.84f, 0.74f, 0.6f));
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(8f, 1.1f, 0), new Vector3(0.3f, 2.2f, 12f), new Color(0.84f, 0.74f, 0.6f));
            // Mangueira
            Palette.Prim(PrimitiveType.Cylinder, r, new Vector3(-4.5f, 1.5f, 4.2f), new Vector3(0.5f, 1.5f, 0.5f), new Color(0.4f, 0.27f, 0.17f), "Mangueira");
            Palette.Prim(PrimitiveType.Sphere, r, new Vector3(-4.5f, 3.6f, 4.2f), new Vector3(3.5f, 2.4f, 3.5f), new Color(0.18f, 0.42f, 0.2f));
            Palette.Prim(PrimitiveType.Sphere, r, new Vector3(-3.4f, 3.2f, 3.4f), new Vector3(2.2f, 1.8f, 2.2f), new Color(0.22f, 0.48f, 0.22f));
            // Varal de lâmpadas
            for (int i = 0; i <= 14; i++)
            {
                float x = -5f + i * (10f / 14f);
                float sag = 0.5f * Mathf.Sin(i / 14f * Mathf.PI);
                Palette.Prim(PrimitiveType.Sphere, r, new Vector3(x, 3.2f - sag, 2.5f), Vector3.one * 0.12f, new Color(1f, 0.92f, 0.55f));
            }
            // Caixas de cerveja e vaso
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(4.5f, 0.3f, 4.8f), new Vector3(0.8f, 0.6f, 0.6f), new Color(0.85f, 0.2f, 0.2f));
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(4.5f, 0.9f, 4.8f), new Vector3(0.8f, 0.6f, 0.6f), new Color(0.95f, 0.8f, 0.1f));
            Palette.Prim(PrimitiveType.Cylinder, r, new Vector3(5.6f, 0.35f, 4.6f), new Vector3(0.6f, 0.35f, 0.6f), new Color(0.72f, 0.35f, 0.2f));
            Palette.Prim(PrimitiveType.Sphere, r, new Vector3(5.6f, 1.0f, 4.6f), new Vector3(0.9f, 1.0f, 0.9f), new Color(0.2f, 0.55f, 0.25f));
            return new EnvironmentLook { Table = new Color(0.95f, 0.95f, 0.93f), TableEdge = new Color(0.85f, 0.85f, 0.83f), Chair = new Color(0.95f, 0.95f, 0.93f) };
        }

        private static EnvironmentLook Boteco(Transform r, Camera cam)
        {
            Sky(cam, new Color(0.2f, 0.15f, 0.1f));
            Light(r, new Color(1f, 0.88f, 0.65f), 1.0f, new Vector3(60, -30, 0), new Color(0.5f, 0.42f, 0.32f));
            Palette.Prim(PrimitiveType.Plane, r, Vector3.zero, new Vector3(2, 1, 2), new Color(0.86f, 0.8f, 0.66f), "Piso");
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(0, 1.75f, 5f), new Vector3(12f, 3.5f, 0.3f), new Color(0.93f, 0.85f, 0.55f), "Parede");
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(0, 0.6f, 4.84f), new Vector3(12f, 1.2f, 0.05f), new Color(0.35f, 0.55f, 0.5f), "Azulejo");
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(-6f, 1.75f, 0), new Vector3(0.3f, 3.5f, 10f), new Color(0.9f, 0.82f, 0.52f));
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(6f, 1.75f, 0), new Vector3(0.3f, 3.5f, 10f), new Color(0.9f, 0.82f, 0.52f));
            // Geladeira, balcão, engradados, TV
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(-4.6f, 1.0f, 4.3f), new Vector3(1.2f, 2.0f, 0.9f), new Color(0.85f, 0.15f, 0.15f), "Geladeira");
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(3.2f, 0.55f, 4.0f), new Vector3(3.5f, 1.1f, 1.0f), new Color(0.45f, 0.28f, 0.18f), "Balcão");
            for (int i = 0; i < 3; i++)
                Palette.Prim(PrimitiveType.Cube, r, new Vector3(-3.0f, 0.3f + i * 0.6f, 4.4f), new Vector3(0.8f, 0.6f, 0.6f),
                             i % 2 == 0 ? new Color(0.95f, 0.8f, 0.1f) : new Color(0.9f, 0.3f, 0.2f));
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(0.5f, 2.6f, 4.75f), new Vector3(1.0f, 0.7f, 0.3f), new Color(0.15f, 0.15f, 0.15f), "TV");
            return new EnvironmentLook { Table = new Color(0.95f, 0.78f, 0.15f), TableEdge = new Color(0.85f, 0.65f, 0.1f), Chair = new Color(0.95f, 0.78f, 0.15f) };
        }

        private static EnvironmentLook Quiosque(Transform r, Camera cam)
        {
            Sky(cam, new Color(0.45f, 0.75f, 0.95f));
            Light(r, new Color(1f, 0.97f, 0.9f), 1.15f, new Vector3(50, -40, 0), new Color(0.55f, 0.6f, 0.7f));
            Palette.Prim(PrimitiveType.Plane, r, Vector3.zero, new Vector3(6, 1, 6), new Color(0.93f, 0.85f, 0.65f), "Areia");
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(0, -0.02f, 22f), new Vector3(80f, 0.05f, 20f), new Color(0.15f, 0.5f, 0.75f), "Mar");
            // Guarda-sol
            Palette.Prim(PrimitiveType.Cylinder, r, new Vector3(4f, 1.4f, 3f), new Vector3(0.06f, 1.4f, 0.06f), new Color(0.9f, 0.9f, 0.9f));
            Palette.Prim(PrimitiveType.Sphere, r, new Vector3(4f, 2.8f, 3f), new Vector3(3.2f, 0.5f, 3.2f), new Color(0.95f, 0.35f, 0.25f), "Guarda-sol");
            // Coqueiros
            for (int k = -1; k <= 1; k += 2)
            {
                Palette.Prim(PrimitiveType.Cylinder, r, new Vector3(k * 5f, 2f, 5f), new Vector3(0.3f, 2f, 0.3f), new Color(0.55f, 0.4f, 0.25f));
                Palette.Prim(PrimitiveType.Sphere, r, new Vector3(k * 5f, 4.1f, 5f), new Vector3(2.6f, 0.6f, 2.6f), new Color(0.2f, 0.6f, 0.25f));
            }
            // Quiosque ao fundo
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(-1.5f, 1.2f, 7f), new Vector3(4f, 2.4f, 2f), new Color(0.6f, 0.42f, 0.25f), "Quiosque");
            Palette.Prim(PrimitiveType.Cube, r, new Vector3(-1.5f, 2.6f, 7f), new Vector3(5f, 0.3f, 3f), new Color(0.85f, 0.75f, 0.45f));
            return new EnvironmentLook { Table = new Color(0.96f, 0.96f, 0.96f), TableEdge = new Color(0.85f, 0.85f, 0.85f), Chair = new Color(0.2f, 0.45f, 0.8f) };
        }

        private static void Sky(Camera cam, Color c)
        {
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = c;
        }

        private static void Light(Transform r, Color color, float intensity, Vector3 euler, Color ambient)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(r, false);
            go.transform.rotation = Quaternion.Euler(euler.x, euler.y, euler.z);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
        }
    }
}
