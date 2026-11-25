using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Mesa redonda e cadeiras de formas simples, usadas na partida e na tela do elenco.
    /// A mesa fica na origem; você senta em -Z e os outros se espalham do outro lado.
    /// </summary>
    public static class TableProps
    {
        public const float TableTop = 0.8f;
        public const float TableRadius = 1.15f;
        public const float SeatRadius = 1.62f;

        public static void Table(Transform world, EnvironmentLook look)
        {
            Palette.Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableTop - 0.02f, 0), new Vector3(TableRadius * 2, 0.02f, TableRadius * 2), look.Table, "Tampo");
            Palette.Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableTop - 0.05f, 0), new Vector3(TableRadius * 2 + 0.04f, 0.015f, TableRadius * 2 + 0.04f), look.TableEdge);
            Palette.Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableTop / 2, 0), new Vector3(0.12f, TableTop / 2, 0.12f), look.TableEdge, "Pé");
            Palette.Prim(PrimitiveType.Cylinder, world, new Vector3(0, 0.02f, 0), new Vector3(0.8f, 0.02f, 0.8f), look.TableEdge);
        }

        /// <summary>Lugares ao redor da mesa: o 0 é você (-Z); os outros vão da esquerda para a direita.</summary>
        public static Vector3[] Seats(int players)
        {
            var seats = new Vector3[players];
            seats[0] = new Vector3(0, 0, -SeatRadius);
            int k = players - 1;
            for (int i = 1; i < players; i++)
            {
                float deg = k == 1 ? 90f : 180f - (i - 1) * (180f / (k - 1));
                float rad = deg * Mathf.Deg2Rad;
                seats[i] = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * SeatRadius;
            }
            return seats;
        }

        /// <summary>Rotação de quem senta no lugar, olhando para o centro da mesa.</summary>
        public static Quaternion Facing(Vector3 seat) => Quaternion.LookRotation(-seat.normalized);

        public static Transform Chair(Transform world, Vector3 seat, EnvironmentLook look, string name = "Cadeira")
        {
            var chair = new GameObject(name).transform;
            chair.SetParent(world, false);
            chair.position = seat;
            chair.rotation = Facing(seat);
            Palette.Prim(PrimitiveType.Cube, chair, new Vector3(0, 0.44f, -0.02f), new Vector3(0.5f, 0.05f, 0.48f), look.Chair);
            Palette.Prim(PrimitiveType.Cube, chair, new Vector3(0, 0.78f, -0.26f), new Vector3(0.5f, 0.62f, 0.05f), look.Chair);
            for (int s = -1; s <= 1; s += 2)
                for (int f = -1; f <= 1; f += 2)
                    Palette.Prim(PrimitiveType.Cube, chair, new Vector3(s * 0.22f, 0.21f, f * 0.2f - 0.02f), new Vector3(0.04f, 0.42f, 0.04f), look.Chair);
            return chair;
        }

        /// <summary>Senta o personagem no lugar (a cadeira precisa ser criada à parte).</summary>
        public static GameObject Seated(PlayerProfile p, Transform world, Vector3 seat)
        {
            var face = Facing(seat);
            var av = AvatarBuilder.Build(p, world, seated: true);
            av.transform.position = seat + face * new Vector3(0, 0, -0.05f);
            av.transform.rotation = face;
            return av;
        }
    }
}
