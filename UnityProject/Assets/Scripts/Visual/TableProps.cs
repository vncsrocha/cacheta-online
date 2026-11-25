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
            // Mesa de plástico: tampo torneado com um rebordo arredondado (a borda dobrada para baixo, oca por
            // baixo), um cubo no meio e quatro pernas cônicas um pouco abertas, entre as cadeiras.
            float r = TableRadius, t = TableTop;
            var top = MeshKit.Lathe("Tampo " + r + " " + t, new[]
            {
                new Vector2(0, t), new Vector2(r - 0.04f, t), new Vector2(r - 0.04f, t),
                new Vector2(r - 0.028f, t + 0.005f), new Vector2(r - 0.014f, t + 0.007f), new Vector2(r - 0.004f, t),
                new Vector2(r, t - 0.014f), new Vector2(r - 0.003f, t - 0.03f), new Vector2(r - 0.012f, t - 0.04f),
                new Vector2(r - 0.024f, t - 0.041f), new Vector2(r - 0.03f, t - 0.032f), new Vector2(r - 0.03f, t - 0.022f),
                new Vector2(r - 0.03f, t - 0.022f), new Vector2(0, t - 0.022f),
            }, 72);
            MeshKit.Piece(top, world, Vector3.zero, look.Table, "Tampo");
            var hub = MeshKit.Lathe("Cubo " + t, new[]
            {
                new Vector2(0.16f, t - 0.022f), new Vector2(0.16f, t - 0.1f), new Vector2(0.16f, t - 0.1f), new Vector2(0, t - 0.1f),
            }, 32);
            MeshKit.Piece(hub, world, Vector3.zero, look.TableEdge, "Cubo");
            for (int i = 0; i < 4; i++)
            {
                float a = (22.5f + i * 90f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var upper = dir * 0.55f + Vector3.up * (t - 0.03f);
                var foot = dir * 0.68f;
                float len = (upper - foot).magnitude;
                var leg = MeshKit.Lathe("Perna " + len, new[]
                {
                    new Vector2(0.032f, len), new Vector2(0.022f, 0.03f), new Vector2(0.03f, 0.012f),
                    new Vector2(0.03f, 0f), new Vector2(0.03f, 0f), new Vector2(0, 0f),
                }, 20);
                var g = MeshKit.Piece(leg, world, foot, look.TableEdge, "Perna");
                g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, upper - foot);
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

        /// <summary>Cadeira com braços: a monobloco de plástico se o cenário pedir (e o modelo estiver baixado), senão
        /// uma de formas simples do mesmo tamanho.</summary>
        public static Transform Chair(Transform world, Vector3 seat, EnvironmentLook look, string name = "Cadeira")
        {
            var chair = new GameObject(name).transform;
            chair.SetParent(world, false);
            chair.position = seat;
            chair.rotation = Facing(seat);
            var c = look.Chair;
            float y = SeatHeight;
            // Cadeira de plástico monobloco de verdade (modelo gratuito baixado pelo Unity), quando o cenário pede.
            if (look.Monobloc && Monobloco.Build(chair, c, y, "Monobloco") != null) return chair;
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
        public static void HoldBacks(GameObject seated, int cards)
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
                holder.LayCard(g.transform, i, cards);
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
