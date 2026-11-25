using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Mesa redonda de 8 lugares e cadeiras de plástico, em metros (uma pessoa de 1,75 m tem 1,75 de altura no
    /// jogo). As alturas seguem o corpo dos personagens, que são caricatos (pernas normais, tronco curto e cabeça
    /// grande): com a mesa de 74 cm de verdade ela bateria nos ombros deles. A mesa fica na origem; você senta no lugar 0, em -Z. A mesa tem sempre as 8 cadeiras:
    /// os lugares sem jogador ficam com a cadeira vazia.
    /// </summary>
    public static class TableProps
    {
        public const int Places = 8;
        public const float TableTop = 0.66f;     // altura do tampo: na barriga de quem está sentado
        public const float TableRadius = 0.82f;  // 1,64 m de diâmetro: cabem 8 cadeiras
        public const float SeatRadius = 1.18f;    // do centro da mesa ao centro do assento
        public const float SeatHeight = 0.42f;   // altura do assento: os pés dos personagens alcançam o chão

        public static void Table(Transform world, EnvironmentLook look)
        {
            // Tampo com borda arredondada e uma saia, quatro pés abertos e travessas (mesa de plástico de bar).
            Palette.Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableTop - 0.015f, 0), new Vector3(TableRadius * 2, 0.015f, TableRadius * 2), look.Table, "Tampo");
            Palette.Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableTop - 0.045f, 0), new Vector3(TableRadius * 2 + 0.03f, 0.03f, TableRadius * 2 + 0.03f), look.TableEdge, "Borda");
            Palette.Prim(PrimitiveType.Cylinder, world, new Vector3(0, TableTop - 0.07f, 0), new Vector3(TableRadius * 1.7f, 0.015f, TableRadius * 1.7f), look.TableEdge, "Saia");
            float footR = TableRadius * 0.62f, legH = TableTop - 0.07f;
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                // O pé sai de baixo da saia e abre um pouco até o chão.
                var top = dir * (footR - 0.08f) + Vector3.up * legH;
                var bottom = dir * footR;
                var leg = Palette.Prim(PrimitiveType.Cylinder, world, (top + bottom) / 2, new Vector3(0.06f, (top - bottom).magnitude / 2, 0.06f), look.Table, "Pé");
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
                Palette.Prim(PrimitiveType.Cylinder, world, bottom + Vector3.up * 0.01f, new Vector3(0.09f, 0.01f, 0.09f), look.TableEdge, "Sapata");
            }
        }

        /// <summary>
        /// Os 8 lugares da mesa. O 0 é você (-Z); os outros seguem da sua esquerda para a sua direita, e o 4 fica
        /// de frente para você.
        /// </summary>
        public static Vector3 Place(int place)
        {
            float deg = -90f - place * (360f / Places);
            float rad = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * SeatRadius;
        }

        /// <summary>
        /// Em qual dos 8 lugares senta cada jogador (0 = você). O jogador do meio da lista (o chefe que joga em casa)
        /// sempre fica no lugar 4, de frente para você. Com poucos jogadores eles sentam do outro lado da mesa, onde
        /// você vê todos sem virar a cabeça; os lugares do seu lado só enchem com 5 ou mais.
        /// </summary>
        public static int[] PlacesFor(int players)
        {
            switch (Mathf.Clamp(players, 2, Places))
            {
                case 2: return new[] { 0, 4 };
                case 3: return new[] { 0, 3, 4 };
                case 4: return new[] { 0, 3, 4, 5 };
                case 5: return new[] { 0, 2, 3, 4, 6 };
                case 6: return new[] { 0, 2, 3, 4, 5, 6 };
                case 7: return new[] { 0, 1, 2, 3, 4, 5, 6 };
                default: return new[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            }
        }

        /// <summary>Posição de cada jogador na mesa (0 = você), na ordem da partida.</summary>
        public static Vector3[] Seats(int players)
        {
            var places = PlacesFor(players);
            var seats = new Vector3[places.Length];
            for (int i = 0; i < places.Length; i++) seats[i] = Place(places[i]);
            return seats;
        }

        /// <summary>Rotação de quem senta no lugar, olhando para o centro da mesa.</summary>
        public static Quaternion Facing(Vector3 seat) => Quaternion.LookRotation(-seat.normalized);

        /// <summary>As 8 cadeiras. A sua (lugar 0) fica de fora quando a câmera está nela.</summary>
        public static void Chairs(Transform world, EnvironmentLook look, bool includeMine = false)
        {
            for (int p = includeMine ? 0 : 1; p < Places; p++) Chair(world, Place(p), look, "Cadeira " + p);
        }

        /// <summary>Cadeira de plástico com braços, do tamanho de uma de verdade.</summary>
        public static Transform Chair(Transform world, Vector3 seat, EnvironmentLook look, string name = "Cadeira")
        {
            var chair = new GameObject(name).transform;
            chair.SetParent(world, false);
            chair.position = seat;
            chair.rotation = Facing(seat);
            var c = look.Chair;
            float y = SeatHeight;
            // Assento (a frente da cadeira é +Z, virada para a mesa)
            Palette.Prim(PrimitiveType.Cube, chair, new Vector3(0, y - 0.015f, 0), new Vector3(0.46f, 0.03f, 0.44f), c, "Assento");
            // Encosto inclinado para trás
            var back = Palette.Prim(PrimitiveType.Cube, chair, new Vector3(0, y + 0.21f, -0.25f), new Vector3(0.44f, 0.4f, 0.03f), c, "Encosto");
            back.transform.localRotation = Quaternion.Euler(-10, 0, 0);
            // Braços
            for (int s = -1; s <= 1; s += 2)
            {
                Palette.Prim(PrimitiveType.Cube, chair, new Vector3(s * 0.235f, y + 0.2f, -0.02f), new Vector3(0.04f, 0.03f, 0.42f), c, "Braço");
                Palette.Prim(PrimitiveType.Cube, chair, new Vector3(s * 0.235f, y + 0.1f, 0.17f), new Vector3(0.035f, 0.2f, 0.035f), c);
            }
            // Pernas abertas
            for (int s = -1; s <= 1; s += 2)
                for (int f = -1; f <= 1; f += 2)
                {
                    var leg = Palette.Prim(PrimitiveType.Cube, chair, new Vector3(s * 0.215f, y / 2, f * 0.2f - 0.02f), new Vector3(0.035f, y, 0.035f), c, "Perna");
                    leg.transform.localRotation = Quaternion.Euler(f * -4f, 0, s * 4f);
                }
            return chair;
        }

        /// <summary>Dá um leque de cartas viradas para baixo a quem está sentado (tela do elenco).</summary>
        public static void HoldBacks(GameObject seated, int cards, float cardW = 0.068f, float cardH = 0.1f)
        {
            var holder = seated.GetComponentInChildren<CardHolder>();
            if (holder == null) return;
            holder.Cards = cards;
            var mat = Palette.Textured(CardTextures.Back);
            for (int i = 0; i < cards; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(g.GetComponent<Collider>());
                g.name = "Verso";
                g.transform.SetParent(holder.Fan, false);
                g.GetComponent<Renderer>().sharedMaterial = mat;
                float x = (i - (cards - 1) / 2f) * 0.021f;
                g.transform.localScale = new Vector3(cardW, cardH, 1);
                g.transform.localPosition = new Vector3(x, -x * x * 2f, -i * 0.0015f);
                g.transform.localRotation = Quaternion.Euler(0, 0, -x * 200f);
            }
        }

        /// <summary>Senta o personagem no lugar (a cadeira precisa ser criada à parte).</summary>
        public static GameObject Seated(PlayerProfile p, Transform world, Vector3 seat)
        {
            var face = Facing(seat);
            var av = AvatarBuilder.Build(p, world, seated: true);
            av.transform.position = seat;
            av.transform.rotation = face;
            return av;
        }
    }
}
