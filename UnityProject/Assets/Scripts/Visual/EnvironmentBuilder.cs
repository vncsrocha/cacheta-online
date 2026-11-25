using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cacheta.App
{
    /// <summary>Cores que cada ambiente usa para a mesa e as cadeiras.</summary>
    public struct EnvironmentLook
    {
        public Color Table;
        public Color TableEdge;
        public Color Chair;
        /// <summary>Usa a cadeira de plástico monobloco (modelo 3D) em vez da cadeira de formas simples.</summary>
        public bool Monobloc;
    }

    /// <summary>
    /// Monta os cenários dos chefes com primitivos. Todos seguem a mesma estrutura: uma sala compacta (chão, teto e
    /// três paredes) com uma abertura grande para fora (porta de enrolar levantada, varanda aberta ou janelão), por onde
    /// se vê a rua, o quintal, a praia ou a cidade e por onde entra a luz do sol.
    /// A mesa fica sempre na origem, você senta em -Z e o fundo da sala fica em +Z (de frente para a câmera).
    /// Cada cenário vai virar uma cena de arte de verdade; esta é a planta e o volume de cada um.
    /// </summary>
    public static class EnvironmentBuilder
    {
        // ------------------------------------------------------------------ medidas da sala
        // Vêm da planta de cada cenário (Resources/Plantas); estes são os valores padrão.
        private static float W = 5.2f;   // meia largura: paredes laterais em x = ±W
        private static float D = 4.6f;   // face interna da parede do fundo
        private static float F = -7.6f;  // frente da sala (atrás da câmera do menu, não tem parede)
        private static float H = 3.4f;   // pé-direito
        private const float T = 0.2f;    // espessura das paredes

        private enum Side { Fundo, Esquerda, Direita }
        private enum Kind { PortaDeEnrolar, Livre, Janela }

        /// <summary>Um vão na parede. Para o fundo, From/To são posições em x; nas laterais, em z.</summary>
        private struct Opening
        {
            public Side Side;
            public float From, To, Bottom, Top;
            public Kind Kind;
            public Opening(Side side, float from, float to, float top, Kind kind, float bottom = 0f)
            {
                Side = side; From = from; To = to; Top = top; Kind = kind; Bottom = bottom;
            }
        }

        private class RoomSpec
        {
            public Color Floor, Wall, Wainscot, Ceiling, Trim;
            public Texture2D FloorTex, WallTex, WainscotTex, CeilingTex;
            public float FloorTile = 0.5f, WallTile = 1f, WainscotTile = 0.3f, CeilingTile = 1f;
            public float WainscotHeight;          // altura do barrado (azulejo, tijolo, lambri). 0 = sem barrado
            public bool WallShadows = true;       // paredes fazem sombra no lado de fora
            public Color SunPatch;                // cor da mancha de sol que entra pelas aberturas (alpha 0 = sem)
            public float SunPatchDepth = 2.2f;
            public float SunPatchAngle = 14f;
            public readonly List<Opening> Openings = new List<Opening>();
        }

        public static EnvironmentLook Build(EnvironmentId id, Transform parent, Camera cam)
        {
            var root = new GameObject("Ambiente " + id).transform;
            root.SetParent(parent, false);

            // A planta (editada no artefato de plantas) manda nas medidas da sala, nos vãos e na posição de cada peça.
            _planta = Planta.Load(id);
            _original = Planta.LoadOriginal(id);
            var sala = (_planta ?? _original)?.sala;
            W = sala != null && sala.largura > 1 ? sala.largura / 2 : 5.2f;
            D = sala != null && sala.fundo > 1 ? sala.fundo : 4.6f;
            F = sala != null && sala.frente < -1 ? sala.frente : -7.6f;
            H = sala != null && sala.pe > 2 ? sala.pe : 3.4f;
            _pieces.Clear();

            EnvironmentLook look;
            switch (id)
            {
                case EnvironmentId.SalaoDeBingo: look = SalaoDeBingo(root, cam); break;
                case EnvironmentId.GaragemDaMarcela: look = GaragemDaMarcela(root, cam); break;
                case EnvironmentId.QuintalDoRamiro: look = QuintalDoRamiro(root, cam); break;
                case EnvironmentId.QuiosqueDoMarquinho: look = QuiosqueDoMarquinho(root, cam); break;
                case EnvironmentId.PatioDaUniversidade: look = PatioDaUniversidade(root, cam); break;
                case EnvironmentId.SalaVip: look = SalaVip(root, cam); break;
                default: look = BarDoTioBeto(root, cam); break;
            }
            ApplyPlanta(root);
            _pieces.Clear();
            return look;
        }

        // ------------------------------------------------------------------ planta: peças com id

        private static Planta _planta, _original;
        private static readonly Dictionary<string, Transform> _pieces = new Dictionary<string, Transform>();

        /// <summary>
        /// A peça da planta com esse id (o1, o2...). Tudo o que é montado dentro dela anda, gira, muda de tamanho ou
        /// some junto quando a peça é mexida na planta.
        /// </summary>
        private static Transform P(Transform r, string id)
        {
            if (_pieces.TryGetValue(id, out var t) && t != null) return t;
            var orig = _original?.Find(id);
            t = new GameObject(orig != null ? $"{id} {orig.nome}" : id).transform;
            t.SetParent(r, false);
            _pieces[id] = t;
            return t;
        }

        /// <summary>
        /// Compara a planta atual com a original e aplica as diferenças: peça movida, girada ou redimensionada, peça
        /// apagada e peça nova (que aparece como um bloco na cor e no tamanho da planta, até ganhar modelo).
        /// </summary>
        private static void ApplyPlanta(Transform root)
        {
            if (_planta == null || _original == null) return;
            foreach (var kv in _pieces)
            {
                var piece = kv.Value;
                var o = _original.Find(kv.Key);
                if (piece == null || o == null) continue;
                var n = _planta.Find(kv.Key);
                if (n == null) { Object.Destroy(piece.gameObject); continue; }
                float sx = o.w > 0.01f && n.w > 0.01f ? n.w / o.w : 1f;
                float sz = o.d > 0.01f && n.d > 0.01f ? n.d / o.d : 1f;
                float sy = o.h > 0.01f && n.h > 0.01f ? n.h / o.h : 1f;
                bool moved = Mathf.Abs(n.x - o.x) > 0.001f || Mathf.Abs(n.z - o.z) > 0.001f || Mathf.Abs(n.rot - o.rot) > 0.01f;
                bool resized = Mathf.Abs(sx - 1) > 0.001f || Mathf.Abs(sy - 1) > 0.001f || Mathf.Abs(sz - 1) > 0.001f;
                if (!moved && !resized) continue;

                // A peça gira e estica em volta do próprio centro: desfaz a posição e o giro originais, estica nos
                // eixos dela e põe no lugar novo, com o giro novo.
                var outer = new GameObject(piece.name).transform;
                outer.SetParent(root, false);
                outer.localPosition = new Vector3(o.x, 0, o.z);
                var inner = new GameObject("giro original").transform;
                inner.SetParent(outer, false);
                inner.localRotation = Quaternion.Euler(0, -o.rot, 0);
                piece.SetParent(inner, true);
                outer.localPosition = new Vector3(n.x, 0, n.z);
                outer.localRotation = Quaternion.Euler(0, n.rot, 0);
                outer.localScale = new Vector3(sx, sy, sz);
            }

            foreach (var n in _planta.objetos ?? new PlantaObjeto[0])
                if (_original.Find(n.id) == null) NewPiece(root, n);
        }

        /// <summary>Peça que só existe na planta: um bloco (ou cilindro) com a cor e o tamanho de lá.</summary>
        private static void NewPiece(Transform root, PlantaObjeto n)
        {
            var c = Planta.Color(n.cor, new Color(0.8f, 0.8f, 0.8f));
            float w = Mathf.Max(0.05f, n.w), d = Mathf.Max(0.05f, n.d);
            float h, y;
            switch (n.onde)
            {
                case "parede": h = n.h > 0 ? n.h : 0.8f; y = 1.6f; break;
                case "teto": h = n.h > 0 ? n.h : 0.12f; y = H - h / 2; break;
                case "fora": h = n.h > 0 ? n.h : 0.05f; y = h / 2; break;
                default: h = n.h > 0 ? n.h : 0.8f; y = h / 2; break;
            }
            var go = Palette.Prim(n.forma == "circ" ? PrimitiveType.Cylinder : PrimitiveType.Cube, root, new Vector3(n.x, y, n.z),
                                  n.forma == "circ" ? new Vector3(w, h / 2, d) : new Vector3(w, h, d), c, $"{n.id} {n.nome} (novo)");
            go.transform.localRotation = Quaternion.Euler(0, n.rot, 0);
        }

        // ================================================================== 1. Tio Beto: fundos do bar
        private static EnvironmentLook BarDoTioBeto(Transform r, Camera cam)
        {
            var red = new Color(0.72f, 0.3f, 0.22f);
            var s = new RoomSpec
            {
                Floor = Color.white, FloorTex = Checker(red, new Color(0.62f, 0.24f, 0.18f)), FloorTile = 0.6f,
                Wall = new Color(0.96f, 0.88f, 0.64f),
                Wainscot = Color.white, WainscotTex = Grid(new Color(0.36f, 0.64f, 0.56f), new Color(0.9f, 0.92f, 0.88f)), WainscotTile = 0.15f,
                WainscotHeight = 1.4f,
                Ceiling = new Color(0.93f, 0.9f, 0.84f), Trim = new Color(0.55f, 0.57f, 0.6f),
                SunPatch = new Color(0.98f, 0.62f, 0.44f, 1f),
            };
            // Porta de enrolar levantada, do lado direito do fundo: dá para a calçada e a rua.
            s.Openings.Add(new Opening(Side.Fundo, 0.4f, 4.9f, 2.85f, Kind.PortaDeEnrolar));
            Room(r, s);
            Sky(cam, new Color(0.66f, 0.84f, 0.96f));
            Light(r, new Color(1f, 0.9f, 0.72f), 1.05f, new Vector3(48, -22, 0), new Color(0.48f, 0.42f, 0.4f));

            // Lá fora: calçada, rua e casas do outro lado.
            Street(P(r, "o15"), new Color(0.8f, 0.77f, 0.72f));
            float[] widths = { 4.5f, 5f, 4f, 5.5f, 4.5f };
            Color[] houses = { new Color(0.95f, 0.6f, 0.62f), new Color(0.98f, 0.85f, 0.4f), new Color(0.5f, 0.72f, 0.9f),
                               new Color(0.6f, 0.82f, 0.55f), new Color(0.98f, 0.72f, 0.45f) };
            float x = -9f;
            for (int i = 0; i < widths.Length; i++)
            {
                Facade(P(r, "o16"), new Vector3(x + widths[i] / 2, 0, D + 13f), widths[i] - 0.1f, 3.6f + (i % 3) * 1.2f, houses[i],
                       new Color(0.25f, 0.35f, 0.45f), floors: i % 3 == 2 ? 2 : 1);
                x += widths[i];
            }
            Tree(P(r, "o17"), new Vector3(6.5f, 0, D + 3.2f), 2.4f, new Color(0.24f, 0.5f, 0.24f), 2.2f);
            LampPost(P(r, "o18"), new Vector3(-1.5f, 0, D + 3.0f));

            // Dentro: geladeiras, balcão, prateleira de garrafas, quadro de preços, TV, engradados.
            Fridge(P(r, "o1"), new Vector3(-4.3f, 0, D - 0.42f), 0f, new Color(0.86f, 0.18f, 0.16f));
            Fridge(P(r, "o2"), new Vector3(-3.15f, 0, D - 0.42f), 0f, new Color(0.92f, 0.92f, 0.9f));
            var wood = new Color(0.5f, 0.3f, 0.18f);
            Box(P(r, "o3"), new Vector3(-4.7f, 0.55f, -0.8f), new Vector3(0.9f, 1.1f, 3.6f), wood, "Balcão");
            Box(P(r, "o3"), new Vector3(-4.65f, 1.13f, -0.8f), new Vector3(1.05f, 0.06f, 3.7f), new Color(0.86f, 0.82f, 0.72f));
            Shelf(P(r, "o4"), new Vector3(-W + 0.16f, 1.95f, -0.8f), 3.2f, wood);
            Chalkboard(P(r, "o5"), new Vector3(-1.3f, 1.9f, D - 0.03f), 1.5f, 0.95f);
            Clock(P(r, "o6"), new Vector3(-1.3f, 2.85f, D - 0.04f));
            Tv(P(r, "o7"), new Vector3(W - 0.1f, 2.45f, 1.6f));
            Crates(P(r, "o8"), new Vector3(4.55f, 0, -0.6f), 3, new[] { new Color(0.95f, 0.78f, 0.1f), new Color(0.88f, 0.26f, 0.2f) });
            Crates(P(r, "o9"), new Vector3(4.55f, 0, -1.35f), 2, new[] { new Color(0.88f, 0.26f, 0.2f), new Color(0.95f, 0.78f, 0.1f) });
            StackedChairs(P(r, "o10"), new Vector3(4.3f, 0, -3.8f), PlasticWhite, 4, monobloc: true);
            Poster(P(r, "o11"), Side.Direita, -2.6f, 1.9f, 0.7f, 1.0f, new Color(0.98f, 0.95f, 0.85f), new Color(0.2f, 0.55f, 0.3f));
            TubeLight(P(r, "o12"), new Vector3(-2.2f, H, 0.2f), true);
            TubeLight(P(r, "o13"), new Vector3(2.2f, H, -1.6f), true);
            Fan(P(r, "o14"), new Vector3(0, H, 0.6f), new Color(0.9f, 0.9f, 0.88f));

            // Mesa e cadeiras de plástico branco, de bar.
            return new EnvironmentLook { Table = PlasticWhite, TableEdge = new Color(0.86f, 0.86f, 0.84f), Chair = PlasticWhite, Monobloc = true };
        }

        // ================================================================== 2. Dona Cleide: salão de bingo do bairro
        private static EnvironmentLook SalaoDeBingo(Transform r, Camera cam)
        {
            var wood = new Color(0.55f, 0.36f, 0.22f);
            var s = new RoomSpec
            {
                Floor = Color.white, FloorTex = Planks(new Color(0.74f, 0.54f, 0.34f), new Color(0.66f, 0.47f, 0.29f), false), FloorTile = 1.2f,
                Wall = new Color(0.7f, 0.84f, 0.9f),
                Wainscot = Color.white, WainscotTex = Planks(wood, new Color(0.49f, 0.31f, 0.19f), true), WainscotTile = 0.8f,
                WainscotHeight = 1.0f,
                Ceiling = new Color(0.96f, 0.96f, 0.94f), Trim = wood,
                SunPatch = new Color(1f, 0.86f, 0.56f, 1f), SunPatchAngle = -14f,
            };
            // Portão de madeira aberto do lado esquerdo do fundo, para a pracinha. Janelas na lateral direita.
            s.Openings.Add(new Opening(Side.Fundo, -4.9f, -1.3f, 2.7f, Kind.Livre));
            s.Openings.Add(new Opening(Side.Direita, -2.6f, -0.4f, 2.45f, Kind.Janela, 1.1f));
            s.Openings.Add(new Opening(Side.Direita, 0.6f, 2.8f, 2.45f, Kind.Janela, 1.1f));
            Room(r, s);
            Sky(cam, new Color(0.7f, 0.86f, 0.98f));
            Light(r, new Color(1f, 0.92f, 0.76f), 1.05f, new Vector3(50, 22, 0), new Color(0.48f, 0.46f, 0.5f));

            // As duas folhas do portão, abertas para dentro.
            Box(P(r, "o19"), new Vector3(-4.83f, 1.33f, D - 0.9f), new Vector3(0.06f, 2.62f, 1.75f), wood, "Folha do portão");
            Box(P(r, "o20"), new Vector3(-1.37f, 1.33f, D - 0.9f), new Vector3(0.06f, 2.62f, 1.75f), wood, "Folha do portão");

            // Lá fora: pracinha com árvores, banco e poste; casas ao fundo.
            Ground(r, new Color(0.47f, 0.64f, 0.32f));
            Box(P(r, "o29"), new Vector3(-3f, 0.005f, D + 6f), new Vector3(3.2f, 0.02f, 12f), new Color(0.8f, 0.74f, 0.62f), "Caminho");
            Tree(P(r, "o30"), new Vector3(-6.5f, 0, D + 5f), 2.6f, new Color(0.22f, 0.5f, 0.25f), 2.6f);
            Tree(P(r, "o31"), new Vector3(1.5f, 0, D + 8f), 2.2f, new Color(0.3f, 0.56f, 0.26f), 2.2f);
            Bench(P(r, "o32"), new Vector3(-0.8f, 0, D + 4.5f), new Color(0.5f, 0.32f, 0.2f));
            LampPost(P(r, "o33"), new Vector3(-4.8f, 0, D + 3.5f));
            Facade(P(r, "o34"), new Vector3(-8f, 0, D + 16f), 6f, 4.2f, new Color(0.96f, 0.92f, 0.78f), new Color(0.25f, 0.35f, 0.45f));
            Facade(P(r, "o34"), new Vector3(-1.5f, 0, D + 16f), 6.5f, 5.4f, new Color(0.62f, 0.8f, 0.72f), new Color(0.25f, 0.35f, 0.45f), floors: 2);
            Facade(P(r, "o34"), new Vector3(5f, 0, D + 16f), 6f, 4f, new Color(0.98f, 0.76f, 0.6f), new Color(0.25f, 0.35f, 0.45f));
            Box(r, new Vector3(W + 6f, -0.05f, 0), new Vector3(12f, 0.1f, 20f), new Color(0.47f, 0.64f, 0.32f), "Gramado lateral");
            Tree(P(r, "o35"), new Vector3(W + 3.5f, 0, 0.8f), 2.4f, new Color(0.24f, 0.52f, 0.25f), 2.4f);

            // Dentro: painel do bingo, globo de sorteio, mesas compridas, cadeiras empilhadas, mural de recados.
            BingoBoard(P(r, "o21"), new Vector3(2.3f, 2.05f, D - 0.04f));
            Box(P(r, "o22"), new Vector3(3.7f, 0.4f, 2.8f), new Vector3(0.9f, 0.8f, 0.6f), wood, "Mesinha do globo");
            Palette.Prim(PrimitiveType.Sphere, P(r, "o22"), new Vector3(3.7f, 1.15f, 2.8f), Vector3.one * 0.55f, new Color(0.95f, 0.78f, 0.32f), "Globo do bingo");
            Box(P(r, "o22"), new Vector3(3.7f, 0.86f, 2.8f), new Vector3(0.3f, 0.12f, 0.3f), new Color(0.3f, 0.3f, 0.32f));
            Box(P(r, "o22"), new Vector3(4.02f, 1.15f, 2.8f), new Vector3(0.12f, 0.04f, 0.04f), new Color(0.3f, 0.3f, 0.32f), "Manivela");
            FoldingTable(P(r, "o23"), new Vector3(4.55f, 0, -2.6f), new Vector3(0.8f, 0.74f, 2.6f), new Color(0.96f, 0.96f, 0.94f));
            Palette.Prim(PrimitiveType.Cylinder, P(r, "o23"), new Vector3(4.55f, 0.95f, -3.4f), new Vector3(0.22f, 0.2f, 0.22f), new Color(0.75f, 0.76f, 0.78f), "Garrafa de café");
            StackedChairs(P(r, "o24"), new Vector3(-4.4f, 0, -3.8f), wood, 5);
            Board(P(r, "o25"), Side.Esquerda, -1.6f, 1.75f, 2.2f, 1.1f, new Color(0.78f, 0.6f, 0.4f),
                  new[] { new Color(0.98f, 0.95f, 0.6f), new Color(0.6f, 0.85f, 0.98f), new Color(0.98f, 0.7f, 0.75f), Color.white });
            Fan(P(r, "o26"), new Vector3(-2.2f, H, 0.2f), Color.white);
            Fan(P(r, "o27"), new Vector3(2.2f, H, -2.4f), Color.white);
            TubeLight(P(r, "o28"), new Vector3(0, H, -3.5f), true);

            return new EnvironmentLook { Table = new Color(0.93f, 0.88f, 0.76f), TableEdge = new Color(0.6f, 0.45f, 0.3f), Chair = wood };
        }

        // ================================================================== 3. Marcela: garagem virada sala de jogos
        private static EnvironmentLook GaragemDaMarcela(Transform r, Camera cam)
        {
            var s = new RoomSpec
            {
                Floor = Color.white, FloorTex = Grid(new Color(0.62f, 0.62f, 0.6f), new Color(0.5f, 0.5f, 0.49f)), FloorTile = 1.5f,
                Wall = Color.white, WallTex = Bricks(new Color(0.76f, 0.76f, 0.74f), new Color(0.64f, 0.64f, 0.62f)), WallTile = 0.8f,
                Ceiling = new Color(0.52f, 0.52f, 0.54f), Trim = new Color(0.45f, 0.47f, 0.5f),
                SunPatch = new Color(0.98f, 0.66f, 0.46f, 1f), SunPatchDepth = 2.6f, SunPatchAngle = 6f,
            };
            // O portão da garagem fica bem atrás da mesa, levantado, para a rua no fim da tarde.
            s.Openings.Add(new Opening(Side.Fundo, -2.4f, 2.4f, 2.6f, Kind.PortaDeEnrolar));
            Room(r, s);
            Sky(cam, new Color(0.98f, 0.62f, 0.44f));
            Light(r, new Color(1f, 0.78f, 0.6f), 1.0f, new Vector3(44, -18, 0), new Color(0.44f, 0.38f, 0.48f));

            // Lá fora: entrada de carro, rua e casas com as janelas acesas.
            Box(P(r, "o48"), new Vector3(0, 0.005f, D + 2.5f), new Vector3(4.8f, 0.02f, 5f), new Color(0.7f, 0.7f, 0.68f), "Entrada");
            Street(P(r, "o49"), new Color(0.72f, 0.7f, 0.66f), 5f);
            Color[] houses = { new Color(0.72f, 0.62f, 0.8f), new Color(0.95f, 0.85f, 0.65f), new Color(0.6f, 0.78f, 0.82f), new Color(0.9f, 0.66f, 0.55f) };
            for (int i = 0; i < houses.Length; i++)
                Facade(P(r, "o50"), new Vector3(-9f + i * 6f, 0, D + 15f), 5.6f, 3.8f + (i % 2) * 1.4f, houses[i], new Color(1f, 0.86f, 0.5f),
                       floors: 1 + i % 2, lit: true);
            Tree(P(r, "o51"), new Vector3(-4f, 0, D + 3f), 2.2f, new Color(0.26f, 0.42f, 0.26f), 2f);
            Hoop(P(r, "o52"), new Vector3(3.4f, 0, D + 2.6f));

            // Dentro: estante de jogos de tabuleiro, fliperama, quadro de ferramentas, pôsteres, pufes e fita de LED.
            var metal = new Color(0.35f, 0.37f, 0.4f);
            Bookcase(P(r, "o36"), new Vector3(-W + 0.26f, 0, -0.6f), 4.4f, metal, new[]
            {
                new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.55f, 0.9f), new Color(0.98f, 0.8f, 0.2f), new Color(0.4f, 0.75f, 0.4f),
                new Color(0.7f, 0.4f, 0.8f), new Color(0.98f, 0.55f, 0.25f), new Color(0.9f, 0.9f, 0.9f), new Color(0.2f, 0.2f, 0.25f),
            });
            Arcade(P(r, "o37"), new Vector3(4.45f, 0, 3.7f), new Color(0.42f, 0.22f, 0.6f));
            Board(P(r, "o38"), Side.Direita, -1.2f, 1.75f, 2.6f, 1.2f, new Color(0.78f, 0.62f, 0.42f),
                  new[] { new Color(0.85f, 0.25f, 0.25f), new Color(0.3f, 0.3f, 0.32f), new Color(0.98f, 0.8f, 0.2f), new Color(0.3f, 0.55f, 0.9f) });
            Box(P(r, "o39"), new Vector3(W - 0.4f, 0.85f, -1.2f), new Vector3(0.75f, 0.06f, 2.6f), new Color(0.6f, 0.42f, 0.26f), "Bancada");
            Box(P(r, "o39"), new Vector3(W - 0.4f, 0.42f, -1.2f), new Vector3(0.7f, 0.84f, 2.5f), metal);
            Poster(P(r, "o40"), Side.Fundo, -3.8f, 1.85f, 1.0f, 1.4f, new Color(0.15f, 0.15f, 0.3f), new Color(0.98f, 0.45f, 0.7f));
            Poster(P(r, "o41"), Side.Fundo, 3.6f, 1.85f, 1.0f, 1.4f, new Color(0.98f, 0.9f, 0.6f), new Color(0.3f, 0.6f, 0.85f));
            Box(P(r, "o42"), new Vector3(-4.5f, 0.45f, D - 0.4f), new Vector3(0.7f, 0.9f, 0.65f), new Color(0.9f, 0.92f, 0.95f), "Frigobar");
            Palette.Prim(PrimitiveType.Sphere, P(r, "o43"), new Vector3(3.4f, 0.32f, -3.6f), new Vector3(1.1f, 0.65f, 1.1f), new Color(0.98f, 0.45f, 0.6f), "Pufe");
            Palette.Prim(PrimitiveType.Sphere, P(r, "o44"), new Vector3(4.4f, 0.32f, -2.9f), new Vector3(1.0f, 0.6f, 1.0f), new Color(0.3f, 0.7f, 0.75f), "Pufe");
            Palette.Prim(PrimitiveType.Cylinder, P(r, "o45"), new Vector3(0, 0.006f, 0), new Vector3(4.6f, 0.006f, 4.6f), new Color(0.3f, 0.25f, 0.42f), "Tapete");
            var led = new Color(0.72f, 0.35f, 1f);
            Glow(r, new Vector3(0, H - 0.04f, D - 0.05f), new Vector3(2 * W, 0.04f, 0.04f), led, "Fita de LED");
            Glow(r, new Vector3(-W + 0.05f, H - 0.04f, (F + D) / 2), new Vector3(0.04f, 0.04f, D - F), led);
            Glow(r, new Vector3(W - 0.05f, H - 0.04f, (F + D) / 2), new Vector3(0.04f, 0.04f, D - F), led);
            for (int i = -2; i <= 2; i++)
                NoShadow(Box(r, new Vector3(i * 2.2f, H - 0.12f, (F + D) / 2), new Vector3(0.14f, 0.24f, D - F), new Color(0.6f, 0.45f, 0.3f), "Viga"));
            TubeLight(P(r, "o46"), new Vector3(-2.2f, H - 0.24f, -0.5f), false);
            TubeLight(P(r, "o47"), new Vector3(2.2f, H - 0.24f, -0.5f), false);

            return new EnvironmentLook { Table = new Color(0.42f, 0.25f, 0.62f), TableEdge = new Color(0.15f, 0.12f, 0.18f), Chair = new Color(0.2f, 0.2f, 0.24f) };
        }

        // ================================================================== 4. Ramiro: área de churrasco coberta, aberta para o quintal
        private static EnvironmentLook QuintalDoRamiro(Transform r, Camera cam)
        {
            var brick = Bricks(new Color(0.72f, 0.36f, 0.24f), new Color(0.86f, 0.78f, 0.7f));
            var s = new RoomSpec
            {
                Floor = Color.white, FloorTex = Grid(new Color(0.8f, 0.46f, 0.3f), new Color(0.68f, 0.62f, 0.56f)), FloorTile = 0.45f,
                Wall = new Color(0.96f, 0.94f, 0.88f),
                Wainscot = Color.white, WainscotTex = brick, WainscotTile = 0.6f, WainscotHeight = 1.1f,
                Ceiling = Color.white, CeilingTex = Planks(new Color(0.62f, 0.42f, 0.26f), new Color(0.55f, 0.36f, 0.22f), false), CeilingTile = 1.2f,
                Trim = new Color(0.55f, 0.36f, 0.22f),
                SunPatch = new Color(1f, 0.82f, 0.5f, 1f),
            };
            // A varanda abre para o quintal no fundo (com um pilar no meio) e na lateral direita.
            s.Openings.Add(new Opening(Side.Fundo, -1.0f, 1.8f, 2.8f, Kind.Livre));
            s.Openings.Add(new Opening(Side.Fundo, 2.2f, 4.9f, 2.8f, Kind.Livre));
            s.Openings.Add(new Opening(Side.Direita, -2.4f, 3.0f, 2.8f, Kind.Livre));
            Room(r, s);
            Sky(cam, new Color(0.64f, 0.83f, 0.96f));
            Light(r, new Color(1f, 0.86f, 0.64f), 1.05f, new Vector3(46, -28, 0), new Color(0.48f, 0.42f, 0.38f));

            // Lá fora: gramado, mangueira, varal, casinha de cachorro, muro com primavera e a caixa d'água do vizinho.
            Ground(r, new Color(0.45f, 0.62f, 0.3f));
            Box(r, new Vector3(W + 7f, -0.05f, 0), new Vector3(14f, 0.1f, 20f), new Color(0.45f, 0.62f, 0.3f), "Gramado lateral");
            Tree(P(r, "o61"), new Vector3(2.8f, 0, D + 5.2f), 2.8f, new Color(0.2f, 0.45f, 0.2f), 3.4f, trunk: 0.45f);
            for (int i = 0; i < 6; i++)
                Palette.Prim(PrimitiveType.Sphere, P(r, "o61"), new Vector3(1.6f + i * 0.5f, 2.9f + (i % 3) * 0.35f, D + 3.6f + (i % 2) * 0.4f),
                             Vector3.one * 0.16f, new Color(0.98f, 0.7f, 0.2f), "Manga");
            var wall = new Color(0.92f, 0.86f, 0.72f);
            Box(P(r, "o62"), new Vector3(0, 1.2f, D + 10f), new Vector3(24f, 2.4f, 0.25f), wall, "Muro");
            Box(P(r, "o63"), new Vector3(W + 8f, 1.2f, 0), new Vector3(0.25f, 2.4f, 20f), wall, "Muro lateral");
            for (int i = 0; i < 9; i++)
                Palette.Prim(PrimitiveType.Sphere, P(r, "o62"), new Vector3(-9f + i * 1.1f, 2.5f + (i % 2) * 0.15f, D + 9.9f),
                             new Vector3(1.2f, 0.7f, 0.6f), i % 3 == 0 ? new Color(0.3f, 0.55f, 0.28f) : new Color(0.92f, 0.32f, 0.6f), "Primavera");
            Clothesline(P(r, "o64"), new Vector3(-6f, 0, D + 4.5f), new Vector3(-1.5f, 0, D + 6f));
            DogHouse(P(r, "o65"), new Vector3(6.4f, 0, D + 7.6f));
            Box(P(r, "o66"), new Vector3(-3f, 3.2f, D + 14f), new Vector3(7f, 1.6f, 5f), new Color(0.75f, 0.38f, 0.28f), "Telhado do vizinho");
            Box(P(r, "o66"), new Vector3(-3f, 1.2f, D + 14f), new Vector3(6.6f, 2.4f, 4.6f), new Color(0.96f, 0.9f, 0.82f));
            Palette.Prim(PrimitiveType.Cylinder, P(r, "o66"), new Vector3(-1.2f, 4.5f, D + 14f), new Vector3(1.3f, 0.5f, 1.3f), new Color(0.3f, 0.55f, 0.85f), "Caixa d'água");

            // Dentro: churrasqueira de tijolo com chaminé, pia, caixa térmica, cadeiras empilhadas, lâmpadas penduradas.
            Barbecue(P(r, "o53"), new Vector3(-3.3f, 0, D - 0.42f), brick);
            Box(P(r, "o54"), new Vector3(-4.75f, 0.45f, 1.7f), new Vector3(0.8f, 0.9f, 1.5f), new Color(0.9f, 0.9f, 0.88f), "Pia");
            Box(P(r, "o54"), new Vector3(-4.75f, 0.92f, 1.7f), new Vector3(0.86f, 0.05f, 1.56f), new Color(0.6f, 0.6f, 0.62f));
            Box(P(r, "o54"), new Vector3(-5.0f, 1.12f, 1.7f), new Vector3(0.06f, 0.35f, 0.06f), new Color(0.7f, 0.7f, 0.72f), "Torneira");
            Box(P(r, "o55"), new Vector3(-4.4f, 0.26f, -1.6f), new Vector3(0.9f, 0.52f, 0.6f), new Color(0.97f, 0.97f, 0.95f), "Caixa térmica");
            Box(P(r, "o55"), new Vector3(-4.4f, 0.55f, -1.6f), new Vector3(0.94f, 0.08f, 0.64f), new Color(0.25f, 0.5f, 0.85f));
            StackedChairs(P(r, "o56"), new Vector3(-4.3f, 0, -3.9f), Color.white, 4);
            Box(P(r, "o57"), new Vector3(W - 0.45f, 0.4f, -4.2f), new Vector3(0.5f, 0.8f, 0.5f), new Color(0.15f, 0.15f, 0.17f), "Caixa de som");
            Palette.Prim(PrimitiveType.Cylinder, P(r, "o57"), new Vector3(W - 0.24f, 0.55f, -4.2f), new Vector3(0.3f, 0.01f, 0.3f), new Color(0.35f, 0.35f, 0.38f))
                .transform.localRotation = Quaternion.Euler(0, 0, 90);
            for (int i = 0; i < 3; i++) Bulb(P(r, "o" + (58 + i)), new Vector3(-2.4f + i * 2.4f, H, 0.4f - (i % 2) * 1.6f), 0.7f);

            var woodTable = new Color(0.62f, 0.42f, 0.26f);
            return new EnvironmentLook { Table = woodTable, TableEdge = new Color(0.45f, 0.3f, 0.18f), Chair = new Color(0.55f, 0.36f, 0.22f) };
        }

        // ================================================================== 5. Marquinho: quiosque coberto de frente para o mar
        private static EnvironmentLook QuiosqueDoMarquinho(Transform r, Camera cam)
        {
            var s = new RoomSpec
            {
                Floor = Color.white, FloorTex = Waves(new Color(0.96f, 0.95f, 0.92f), new Color(0.14f, 0.14f, 0.15f)), FloorTile = 1.6f,
                Wall = Color.white, WallTex = Planks(new Color(0.8f, 0.64f, 0.42f), new Color(0.72f, 0.56f, 0.36f), true), WallTile = 0.6f,
                Wainscot = new Color(0.26f, 0.56f, 0.76f), WainscotHeight = 0.9f,
                Ceiling = Color.white, CeilingTex = Stripes(new Color(0.84f, 0.7f, 0.44f), new Color(0.76f, 0.62f, 0.38f), 6), CeilingTile = 1.2f,
                Trim = new Color(0.62f, 0.44f, 0.26f),
                SunPatch = new Color(1f, 0.93f, 0.7f, 1f), SunPatchDepth = 2.0f, SunPatchAngle = 10f,
            };
            // O fundo é todo aberto para o mar (três vãos entre os pilares) e a lateral direita abre para a areia.
            s.Openings.Add(new Opening(Side.Fundo, -4.9f, -1.7f, 2.9f, Kind.Livre));
            s.Openings.Add(new Opening(Side.Fundo, -1.3f, 1.3f, 2.9f, Kind.Livre));
            s.Openings.Add(new Opening(Side.Fundo, 1.7f, 4.9f, 2.9f, Kind.Livre));
            s.Openings.Add(new Opening(Side.Direita, -3.4f, 3.0f, 2.9f, Kind.Livre));
            Room(r, s);
            Sky(cam, new Color(0.52f, 0.78f, 0.97f));
            Light(r, new Color(1f, 0.97f, 0.88f), 1.15f, new Vector3(52, -30, 0), new Color(0.52f, 0.56f, 0.62f));

            // Lá fora: areia, espuma, mar até o horizonte, coqueiros, guarda-sol, barquinho e uma ilha.
            var sand = new Color(0.94f, 0.86f, 0.66f);
            Box(P(r, "o77"), new Vector3(0, -0.05f, D + 6f), new Vector3(80f, 0.1f, 12f), sand, "Areia");
            Box(r, new Vector3(W + 10f, -0.05f, -1f), new Vector3(20f, 0.1f, 16f), sand, "Areia lateral");
            Box(r, new Vector3(0, -0.06f, D + 12.6f), new Vector3(80f, 0.1f, 1.2f), new Color(0.68f, 0.88f, 0.9f), "Espuma");
            Box(P(r, "o78"), new Vector3(0, -0.08f, D + 113f), new Vector3(400f, 0.1f, 200f), new Color(0.16f, 0.52f, 0.76f), "Mar");
            Palette.Prim(PrimitiveType.Sphere, r, new Vector3(-38f, -2f, D + 150f), new Vector3(40f, 12f, 16f), new Color(0.3f, 0.55f, 0.4f), "Ilha");
            Palm(P(r, "o79"), new Vector3(-6.4f, 0, D + 4f), 4.6f, 8f);
            Palm(P(r, "o80"), new Vector3(7f, 0, D + 6f), 5.2f, -10f);
            Palm(P(r, "o81"), new Vector3(W + 3.5f, 0, -1.5f), 4.2f, 6f);
            Umbrella(P(r, "o82"), new Vector3(2.2f, 0, D + 6.5f), new Color(0.98f, 0.4f, 0.3f), Color.white);
            Box(r, new Vector3(-12f, 0.1f, D + 34f), new Vector3(2.4f, 0.5f, 0.9f), Color.white, "Barquinho");
            Box(r, new Vector3(-12f, 0.75f, D + 34f), new Vector3(0.05f, 1.3f, 0.05f), new Color(0.6f, 0.45f, 0.3f));
            Box(r, new Vector3(-11.6f, 0.9f, D + 34f), new Vector3(0.7f, 0.9f, 0.03f), new Color(0.98f, 0.85f, 0.3f));

            // Dentro: carrinho de coco, balcão com banquinhos, freezer, pranchas e luminárias de palha.
            CoconutCart(P(r, "o67"), new Vector3(-3.7f, 0, 2.9f));
            var blue = new Color(0.26f, 0.56f, 0.76f);
            Box(P(r, "o68"), new Vector3(-4.7f, 0.55f, -1.2f), new Vector3(0.9f, 1.1f, 3.4f), blue, "Balcão");
            Box(P(r, "o68"), new Vector3(-4.65f, 1.13f, -1.2f), new Vector3(1.05f, 0.06f, 3.5f), new Color(0.75f, 0.58f, 0.38f));
            for (int i = 0; i < 3; i++) Stool(P(r, "o" + (69 + i)), new Vector3(-3.95f, 0, -2.4f + i * 1.1f), new Color(0.98f, 0.55f, 0.3f));
            Box(P(r, "o72"), new Vector3(-4.4f, 0.45f, -4.1f), new Vector3(0.9f, 0.9f, 1.3f), new Color(0.97f, 0.97f, 0.97f), "Freezer");
            Surfboard(P(r, "o73"), new Vector3(4.8f, 0, -4.4f), new Color(0.98f, 0.8f, 0.25f), -8f);
            Surfboard(P(r, "o74"), new Vector3(4.8f, 0, -5.1f), new Color(0.35f, 0.75f, 0.7f), 6f);
            NoShadow(Box(r, new Vector3(0, H - 0.14f, D - 0.1f), new Vector3(2 * W, 0.28f, 0.2f), new Color(0.62f, 0.44f, 0.26f), "Viga do fundo"));
            for (int i = -1; i <= 1; i += 2) WovenLamp(P(r, "o" + (i < 0 ? 75 : 76)), new Vector3(i * 2.2f, H, -0.6f));

            return new EnvironmentLook { Table = new Color(0.97f, 0.97f, 0.97f), TableEdge = new Color(0.85f, 0.85f, 0.85f), Chair = new Color(0.2f, 0.45f, 0.8f) };
        }

        // ================================================================== 6. Ana: corredor coberto de frente para o pátio da faculdade
        private static EnvironmentLook PatioDaUniversidade(Transform r, Camera cam)
        {
            var s = new RoomSpec
            {
                Floor = Color.white, FloorTex = Grid(new Color(0.8f, 0.78f, 0.74f), new Color(0.68f, 0.66f, 0.62f)), FloorTile = 0.6f,
                Wall = new Color(0.96f, 0.96f, 0.94f),
                Wainscot = Color.white, WainscotTex = Tiles(new Color(0.2f, 0.4f, 0.76f), new Color(0.94f, 0.96f, 0.98f)), WainscotTile = 0.3f,
                WainscotHeight = 1.2f,
                Ceiling = new Color(0.92f, 0.92f, 0.9f), Trim = new Color(0.82f, 0.82f, 0.8f),
                SunPatch = new Color(1f, 0.9f, 0.62f, 1f), SunPatchAngle = -10f,
            };
            // Três vãos entre pilares para o pátio interno.
            s.Openings.Add(new Opening(Side.Fundo, -4.9f, -1.9f, 2.9f, Kind.Livre));
            s.Openings.Add(new Opening(Side.Fundo, -1.4f, 1.4f, 2.9f, Kind.Livre));
            s.Openings.Add(new Opening(Side.Fundo, 1.9f, 4.9f, 2.9f, Kind.Livre));
            Room(r, s);
            Sky(cam, new Color(0.62f, 0.82f, 0.98f));
            Light(r, new Color(1f, 0.94f, 0.8f), 1.05f, new Vector3(50, 24, 0), new Color(0.48f, 0.48f, 0.54f));

            // Lá fora: gramado com caminhos, o ipê amarelo, outra árvore, bancos e o prédio do outro lado.
            Ground(r, new Color(0.46f, 0.64f, 0.3f));
            var stone = new Color(0.82f, 0.8f, 0.74f);
            Box(P(r, "o93"), new Vector3(0, 0.005f, D + 7f), new Vector3(2.2f, 0.02f, 14f), stone, "Caminho");
            Box(P(r, "o93"), new Vector3(0, 0.005f, D + 7f), new Vector3(24f, 0.02f, 2f), stone, "Caminho");
            Tree(P(r, "o91"), new Vector3(3.6f, 0, D + 4.5f), 2.6f, new Color(0.99f, 0.82f, 0.14f), 3.2f, trunk: 0.32f);
            Palette.Prim(PrimitiveType.Cylinder, P(r, "o91"), new Vector3(3.6f, 0.012f, D + 4.5f), new Vector3(5f, 0.008f, 5f), new Color(0.96f, 0.84f, 0.3f), "Flores caídas");
            Tree(P(r, "o92"), new Vector3(-6.5f, 0, D + 8f), 2.6f, new Color(0.24f, 0.5f, 0.26f), 2.8f);
            Bench(P(r, "o94"), new Vector3(-3f, 0, D + 4.2f), new Color(0.6f, 0.6f, 0.58f));
            Bench(P(r, "o95"), new Vector3(-3f, 0, D + 9.6f), new Color(0.6f, 0.6f, 0.58f));
            Facade(P(r, "o96"), new Vector3(0, 0, D + 16f), 30f, 9.6f, new Color(0.95f, 0.94f, 0.9f), new Color(0.35f, 0.55f, 0.75f), floors: 3);

            // Dentro: mural de avisos, máquina de lanches, lixeiras de reciclagem, banco e cavalete com cartaz.
            Board(P(r, "o83"), Side.Esquerda, -0.4f, 1.8f, 2.8f, 1.2f, new Color(0.78f, 0.6f, 0.4f),
                  new[] { new Color(0.98f, 0.95f, 0.6f), new Color(0.6f, 0.85f, 0.98f), new Color(0.98f, 0.7f, 0.75f), new Color(0.7f, 0.95f, 0.7f) });
            Board(P(r, "o84"), Side.Esquerda, -4.2f, 1.8f, 1.8f, 1.2f, new Color(0.25f, 0.4f, 0.3f), new[] { Color.white, new Color(0.98f, 0.95f, 0.6f) });
            VendingMachine(P(r, "o85"), new Vector3(W - 0.45f, 0, 3.6f));
            Color[] bins = { new Color(0.95f, 0.8f, 0.15f), new Color(0.25f, 0.45f, 0.85f), new Color(0.85f, 0.25f, 0.2f), new Color(0.3f, 0.65f, 0.3f) };
            for (int i = 0; i < bins.Length; i++)
                Palette.Prim(PrimitiveType.Cylinder, P(r, "o86"), new Vector3(W - 0.35f, 0.4f, -0.4f - i * 0.6f), new Vector3(0.45f, 0.4f, 0.45f), bins[i], "Lixeira");
            Bench(P(r, "o87"), new Vector3(W - 0.5f, 0, -4.2f), new Color(0.55f, 0.36f, 0.22f), alongZ: true);
            Box(P(r, "o88"), new Vector3(-3.6f, 0.9f, 3.4f), new Vector3(0.9f, 1.2f, 0.05f), new Color(0.98f, 0.98f, 0.96f), "Cartaz");
            Box(P(r, "o88"), new Vector3(-3.6f, 1.15f, 3.37f), new Vector3(0.6f, 0.4f, 0.02f), new Color(0.2f, 0.5f, 0.35f));
            Box(P(r, "o88"), new Vector3(-3.6f, 0.4f, 3.5f), new Vector3(0.06f, 0.8f, 0.3f), new Color(0.55f, 0.36f, 0.22f), "Cavalete");
            for (int i = -2; i <= 2; i++)
                NoShadow(Box(r, new Vector3(i * 2.4f, H - 0.15f, (F + D) / 2), new Vector3(0.3f, 0.3f, D - F), new Color(0.86f, 0.86f, 0.84f), "Viga"));
            for (int i = -1; i <= 1; i += 2) DomeLight(P(r, "o" + (i < 0 ? 89 : 90)), new Vector3(i * 1.2f, H, -0.8f));

            return new EnvironmentLook { Table = new Color(0.74f, 0.74f, 0.72f), TableEdge = new Color(0.58f, 0.58f, 0.56f), Chair = new Color(0.2f, 0.5f, 0.32f) };
        }

        // ================================================================== 7. Little Tiger: sala VIP do cassino, janelão para a cidade à noite
        private static EnvironmentLook SalaVip(Transform r, Camera cam)
        {
            var gold = Palette.Gold;
            var s = new RoomSpec
            {
                Floor = Color.white, FloorTex = Grid(new Color(0.45f, 0.08f, 0.12f), new Color(0.7f, 0.52f, 0.18f)), FloorTile = 0.9f,
                Wall = new Color(0.48f, 0.11f, 0.18f),
                Wainscot = Color.white, WainscotTex = Planks(new Color(0.3f, 0.17f, 0.1f), new Color(0.24f, 0.13f, 0.08f), true), WainscotTile = 0.5f,
                WainscotHeight = 1.1f,
                Ceiling = new Color(0.32f, 0.13f, 0.15f), Trim = gold,
                WallShadows = false,
            };
            // Janelão de ponta a ponta no fundo, com a cidade iluminada lá embaixo.
            s.Openings.Add(new Opening(Side.Fundo, -4.9f, 4.9f, 3.05f, Kind.Janela, 0.45f));
            Room(r, s);
            Sky(cam, new Color(0.06f, 0.07f, 0.18f));
            Light(r, new Color(1f, 0.84f, 0.6f), 1.05f, new Vector3(52, 12, 0), new Color(0.44f, 0.32f, 0.4f));
            Box(r, new Vector3(0, 1.1f, D - 0.05f), new Vector3(2 * W, 0.04f, 0.06f), gold, "Friso");

            // Lá fora: prédios descendo até lá embaixo, com janelas acesas, e a lua.
            var rng = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                float bx = -30f + i * 2.4f + (float)rng.NextDouble();
                float bz = D + 10f + (float)rng.NextDouble() * 40f;
                float top = -4f + (float)rng.NextDouble() * 14f;
                float bw = 2f + (float)rng.NextDouble() * 2.5f;
                var shade = Color.Lerp(new Color(0.1f, 0.12f, 0.24f), new Color(0.18f, 0.2f, 0.34f), (float)rng.NextDouble());
                Tower(P(r, "o105"), new Vector3(bx, top, bz), bw, top + 40f, shade, rng);
            }
            Glow(r, new Vector3(16f, 22f, D + 90f), Vector3.one * 5f, new Color(0.98f, 0.96f, 0.85f), "Lua", PrimitiveType.Sphere);

            // Dentro: roleta, caça-níqueis, estátua do tigre dourado, bar, fichas, dado de ouro e lustre.
            Roulette(P(r, "o97"), new Vector3(-3.0f, 0, 3.35f));
            for (int i = 0; i < 4; i++) SlotMachine(P(r, "o98"), new Vector3(W - 0.42f, 0, -1.0f + i * 0.95f), i);
            TigerStatue(P(r, "o99"), new Vector3(4.15f, 0, 3.75f));
            var darkWood = new Color(0.28f, 0.15f, 0.09f);
            Box(P(r, "o100"), new Vector3(-4.7f, 0.55f, -1.6f), new Vector3(0.9f, 1.1f, 3.2f), darkWood, "Bar");
            Box(P(r, "o100"), new Vector3(-4.65f, 1.13f, -1.6f), new Vector3(1.05f, 0.06f, 3.3f), gold);
            Shelf(P(r, "o101"), new Vector3(-W + 0.16f, 1.95f, -1.6f), 3.0f, darkWood, glowing: true);
            GoldDie(P(r, "o102"), new Vector3(-4.6f, 1.31f, -0.3f), 0.3f);
            ChipStacks(P(r, "o103"), new Vector3(-4.6f, 1.16f, -2.6f));
            Chandelier(P(r, "o104"), new Vector3(0, H, 0.4f));
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 2; k++)
                    Glow(r, new Vector3(i * (W - 0.06f), 2.2f, -3.6f + k * 4.2f), new Vector3(0.08f, 0.3f, 0.2f), new Color(1f, 0.85f, 0.55f), "Arandela");

            return new EnvironmentLook { Table = Palette.Felt, TableEdge = gold, Chair = new Color(0.5f, 0.08f, 0.12f) };
        }

        // ================================================================== estrutura da sala

        private static void Room(Transform r, RoomSpec s)
        {
            // Os vãos da planta valem mais que os do código (que são a planta original).
            var ab = _planta?.aberturas;
            if (ab != null && ab.Length > 0)
            {
                s.Openings.Clear();
                foreach (var a in ab)
                {
                    var side = a.lado == "esquerda" ? Side.Esquerda : a.lado == "direita" ? Side.Direita : Side.Fundo;
                    var kind = a.tipo == "enrolar" ? Kind.PortaDeEnrolar : a.tipo == "janela" ? Kind.Janela : Kind.Livre;
                    float top = a.altura > 0.3f ? Mathf.Min(a.altura, H) : 2.8f;
                    s.Openings.Add(new Opening(side, Mathf.Min(a.de, a.ate), Mathf.Max(a.de, a.ate), top, kind, Mathf.Clamp(a.peitoril, 0, top - 0.2f)));
                }
            }

            var room = new GameObject("Sala").transform;
            room.SetParent(r, false);
            float len = D - F + T;
            Block(room, new Vector3(0, -0.05f, F + len / 2), new Vector3(2 * W + 2 * T, 0.1f, len), s.Floor, s.FloorTex,
                  new Vector2((2 * W) / s.FloorTile, len / s.FloorTile), "Piso");
            var ceiling = Block(room, new Vector3(0, H + 0.05f, F + len / 2), new Vector3(2 * W + 2 * T, 0.1f, len), s.Ceiling, s.CeilingTex,
                                new Vector2((2 * W) / s.CeilingTile, len / s.CeilingTile), "Teto");
            // O teto não faz sombra: a sala fica iluminada como se as lâmpadas estivessem acesas.
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            Wall(room, s, Side.Fundo);
            Wall(room, s, Side.Esquerda);
            Wall(room, s, Side.Direita);
            Box(room, new Vector3(0, 0.05f, D - 0.01f), new Vector3(2 * W, 0.1f, 0.03f), s.Trim, "Rodapé");

            if (s.SunPatch.a > 0)
                foreach (var o in s.Openings)
                    if (o.Bottom <= 0.01f) SunPatch(room, s, o);
        }

        private static void Wall(Transform r, RoomSpec s, Side side)
        {
            float u0 = side == Side.Fundo ? -W - T : F;
            float u1 = side == Side.Fundo ? W + T : D;
            var holes = new List<Opening>();
            foreach (var o in s.Openings) if (o.Side == side) holes.Add(o);
            holes.Sort((a, b) => a.From.CompareTo(b.From));

            float u = u0;
            foreach (var o in holes)
            {
                if (o.From > u) Column(r, s, side, u, o.From, 0, H);
                if (o.Bottom > 0) Column(r, s, side, o.From, o.To, 0, o.Bottom);
                if (o.Top < H) Column(r, s, side, o.From, o.To, o.Top, H);
                Frame(r, s, o);
                u = o.To;
            }
            if (u1 > u) Column(r, s, side, u, u1, 0, H);
        }

        /// <summary>Um trecho de parede entre as alturas y0 e y1, dividido em barrado e parede.</summary>
        private static void Column(Transform r, RoomSpec s, Side side, float a, float b, float y0, float y1)
        {
            float wh = s.WainscotHeight;
            if (wh > y0) Piece(r, s, side, a, b, y0, Mathf.Min(y1, wh), s.Wainscot, s.WainscotTex, s.WainscotTile, "Barrado");
            if (y1 > wh) Piece(r, s, side, a, b, Mathf.Max(y0, wh), y1, s.Wall, s.WallTex, s.WallTile, "Parede");
        }

        private static void Piece(Transform r, RoomSpec s, Side side, float a, float b, float y0, float y1, Color c, Texture2D tex, float tile, string name)
        {
            float len = b - a, h = y1 - y0, mid = (a + b) / 2, y = (y0 + y1) / 2;
            Vector3 pos, size;
            switch (side)
            {
                case Side.Fundo: pos = new Vector3(mid, y, D + T / 2); size = new Vector3(len, h, T); break;
                case Side.Esquerda: pos = new Vector3(-W - T / 2, y, mid); size = new Vector3(T, h, len); break;
                default: pos = new Vector3(W + T / 2, y, mid); size = new Vector3(T, h, len); break;
            }
            var go = Block(r, pos, size, c, tex, new Vector2(len / tile, h / tile), name);
            if (!s.WallShadows) go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Ponto na face interna de uma parede (u ao longo dela, inset para dentro da sala).</summary>
        private static Vector3 At(Side side, float u, float y, float inset)
        {
            switch (side)
            {
                case Side.Fundo: return new Vector3(u, y, D - inset);
                case Side.Esquerda: return new Vector3(-W + inset, y, u);
                default: return new Vector3(W - inset, y, u);
            }
        }

        private static Vector3 Dim(Side side, float along, float h, float depth)
            => side == Side.Fundo ? new Vector3(along, h, depth) : new Vector3(depth, h, along);

        /// <summary>Acabamento de cada vão: trilhos e rolo da porta de enrolar, batente do vão livre ou caixilho da janela.</summary>
        private static void Frame(Transform r, RoomSpec s, Opening o)
        {
            float w = o.To - o.From, mid = (o.From + o.To) / 2;
            switch (o.Kind)
            {
                case Kind.PortaDeEnrolar:
                {
                    var rail = new Color(0.5f, 0.52f, 0.55f);
                    foreach (float u in new[] { o.From + 0.04f, o.To - 0.04f })
                        Box(r, At(o.Side, u, o.Top / 2, 0.05f), Dim(o.Side, 0.08f, o.Top, 0.1f), rail, "Trilho");
                    Box(r, At(o.Side, mid, o.Top + 0.18f, 0.2f), Dim(o.Side, w + 0.2f, 0.36f, 0.4f), rail, "Rolo da porta");
                    // A ponta da porta aparece embaixo do rolo, com a ondulação da chapa.
                    var stripes = Palette.Stripes(new Color(0.72f, 0.74f, 0.76f), new Color(0.58f, 0.6f, 0.63f), 3);
                    Block(r, At(o.Side, mid, o.Top - 0.14f, 0.12f), Dim(o.Side, w - 0.1f, 0.28f, 0.04f), Color.white, stripes, Vector2.one, "Porta de enrolar");
                    Box(r, At(o.Side, mid, o.Top - 0.3f, 0.12f), Dim(o.Side, w - 0.1f, 0.05f, 0.06f), new Color(0.4f, 0.42f, 0.45f), "Puxador");
                    break;
                }
                case Kind.Livre:
                    Box(r, At(o.Side, mid, o.Top + 0.06f, -T / 2), Dim(o.Side, w, 0.12f, T + 0.06f), s.Trim, "Verga");
                    break;
                case Kind.Janela:
                {
                    float h = o.Top - o.Bottom, ym = (o.Top + o.Bottom) / 2;
                    var frame = s.Trim;
                    Box(r, At(o.Side, mid, o.Bottom, 0.02f), Dim(o.Side, w + 0.1f, 0.06f, T + 0.14f), frame, "Peitoril");
                    Box(r, At(o.Side, mid, o.Top, -T / 2), Dim(o.Side, w, 0.06f, 0.08f), frame);
                    Box(r, At(o.Side, mid, ym, -T / 2), Dim(o.Side, w, 0.04f, 0.06f), frame);
                    int panes = Mathf.Max(1, Mathf.RoundToInt(w / 1.2f));
                    for (int i = 0; i <= panes; i++)
                        Box(r, At(o.Side, o.From + i * w / panes, ym, -T / 2), Dim(o.Side, 0.06f, h, 0.08f), frame, "Caixilho");
                    break;
                }
            }
        }

        /// <summary>A mancha de sol no chão, logo depois de cada abertura que vai até o chão.</summary>
        private static void SunPatch(Transform r, RoomSpec s, Opening o)
        {
            float w = o.To - o.From, mid = (o.From + o.To) / 2, depth = s.SunPatchDepth;
            var c = new Color(s.SunPatch.r, s.SunPatch.g, s.SunPatch.b);
            GameObject go;
            if (o.Side == Side.Fundo)
            {
                // A mancha não passa por baixo da mesa e das cadeiras (z < 2,2); o que sobra entra na parede.
                float z = Mathf.Max(D - depth / 2, 2.2f + depth / 2);
                go = Box(r, new Vector3(mid, 0.004f, z), new Vector3(w * 0.96f, 0.004f, depth), c, "Sol no chão");
                go.transform.localRotation = Quaternion.Euler(0, s.SunPatchAngle, 0);
            }
            else
            {
                float sx = o.Side == Side.Direita ? 1 : -1;
                go = Box(r, new Vector3(sx * (W - depth / 2), 0.004f, mid), new Vector3(depth, 0.004f, w * 0.96f), c, "Sol no chão");
                go.transform.localRotation = Quaternion.Euler(0, sx * s.SunPatchAngle, 0);
            }
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ================================================================== lado de fora

        private static void Ground(Transform r, Color c)
            => Box(r, new Vector3(0, -0.05f, D + 30f), new Vector3(120f, 0.1f, 52f), c, "Chão lá fora");

        /// <summary>Calçada logo na saída, rua e a calçada do outro lado.</summary>
        private static void Street(Transform r, Color sidewalk, float sidewalkDepth = 3f)
        {
            float z = D + T;
            Box(r, new Vector3(0, -0.04f, z + sidewalkDepth / 2), new Vector3(120f, 0.1f, sidewalkDepth), sidewalk, "Calçada");
            Box(r, new Vector3(0, -0.1f, z + sidewalkDepth + 3.5f), new Vector3(120f, 0.1f, 7f), new Color(0.3f, 0.3f, 0.33f), "Rua");
            for (int i = -10; i <= 10; i++)
                Box(r, new Vector3(i * 3f, -0.045f, z + sidewalkDepth + 3.5f), new Vector3(1.4f, 0.01f, 0.15f), new Color(0.95f, 0.95f, 0.9f));
            Box(r, new Vector3(0, -0.04f, z + sidewalkDepth + 7f + 2f), new Vector3(120f, 0.1f, 4f), sidewalk, "Calçada");
            Box(r, new Vector3(0, -0.05f, z + sidewalkDepth + 11f + 20f), new Vector3(120f, 0.1f, 40f), new Color(0.55f, 0.6f, 0.5f));
        }

        /// <summary>Fachada de casa ou prédio virada para a sala, com janelas e porta.</summary>
        private static void Facade(Transform r, Vector3 basePos, float w, float h, Color wall, Color window, int floors = 1, bool lit = false)
        {
            Box(r, basePos + new Vector3(0, h / 2, 1.5f), new Vector3(w, h, 3f), wall, "Fachada");
            Box(r, basePos + new Vector3(0, h + 0.08f, 1.4f), new Vector3(w + 0.2f, 0.16f, 3.2f), Color.Lerp(wall, Color.black, 0.25f));
            float fh = h / floors;
            int cols = Mathf.Max(1, Mathf.FloorToInt(w / 1.6f));
            for (int f = 0; f < floors; f++)
                for (int c = 0; c < cols; c++)
                {
                    float x = -w / 2 + (c + 0.5f) * (w / cols);
                    if (f == 0 && c == cols / 2)
                    {
                        Box(r, basePos + new Vector3(x, 1.0f, -0.02f), new Vector3(0.9f, 2.0f, 0.06f), Color.Lerp(wall, Color.black, 0.45f), "Porta");
                        continue;
                    }
                    var pos = basePos + new Vector3(x, f * fh + fh * 0.58f, -0.02f);
                    var size = new Vector3(0.8f, Mathf.Min(1.1f, fh * 0.45f), 0.06f);
                    if (lit && (c + f) % 2 == 0) Glow(r, pos, size, window, "Janela acesa");
                    else Box(r, pos, size, window, "Janela");
                }
        }

        private static void Tower(Transform r, Vector3 top, float w, float h, Color c, System.Random rng)
        {
            Box(r, top - new Vector3(0, h / 2, 0), new Vector3(w, h, w), c, "Prédio");
            var lit = new Color(1f, 0.85f, 0.45f);
            int cols = Mathf.Max(2, Mathf.FloorToInt(w / 0.6f));
            for (int row = 0; row < 10; row++)
                for (int col = 0; col < cols; col++)
                {
                    if (rng.NextDouble() > 0.32) continue;
                    float x = -w / 2 + (col + 0.5f) * (w / cols);
                    Glow(r, top + new Vector3(x, -0.6f - row * 0.9f, -w / 2 - 0.01f), new Vector3(0.3f, 0.45f, 0.02f), lit, "Janela acesa");
                }
            if (rng.NextDouble() < 0.3)
                Glow(r, top + new Vector3(0, 0.4f, 0), new Vector3(0.12f, 0.12f, 0.12f), new Color(1f, 0.2f, 0.2f), "Luz de aviso", PrimitiveType.Sphere);
        }

        private static void Tree(Transform r, Vector3 pos, float trunkH, Color crown, float size, float trunk = 0.28f)
        {
            Palette.Prim(PrimitiveType.Cylinder, r, pos + new Vector3(0, trunkH / 2, 0), new Vector3(trunk, trunkH / 2, trunk), new Color(0.42f, 0.28f, 0.18f), "Árvore");
            Palette.Prim(PrimitiveType.Sphere, r, pos + new Vector3(0, trunkH + size * 0.35f, 0), new Vector3(size, size * 0.75f, size), crown);
            Palette.Prim(PrimitiveType.Sphere, r, pos + new Vector3(size * 0.35f, trunkH + size * 0.15f, -size * 0.2f), Vector3.one * size * 0.6f, Color.Lerp(crown, Color.white, 0.08f));
            Palette.Prim(PrimitiveType.Sphere, r, pos + new Vector3(-size * 0.35f, trunkH + size * 0.2f, size * 0.1f), Vector3.one * size * 0.55f, Color.Lerp(crown, Color.black, 0.08f));
        }

        private static void Palm(Transform r, Vector3 pos, float h, float lean)
        {
            var palm = new GameObject("Coqueiro").transform;
            palm.SetParent(r, false);
            palm.localPosition = pos;
            palm.localRotation = Quaternion.Euler(0, 0, lean);
            Palette.Prim(PrimitiveType.Cylinder, palm, new Vector3(0, h / 2, 0), new Vector3(0.28f, h / 2, 0.28f), new Color(0.58f, 0.43f, 0.27f));
            for (int i = 0; i < 6; i++)
            {
                var leaf = Palette.Prim(PrimitiveType.Cube, palm, new Vector3(0, h, 0), new Vector3(0.35f, 0.05f, 2.2f), new Color(0.22f, 0.6f, 0.26f), "Folha");
                leaf.transform.localRotation = Quaternion.Euler(20f, i * 60f, 0);
                leaf.transform.localPosition = new Vector3(0, h, 0) + leaf.transform.localRotation * new Vector3(0, 0, 1f);
            }
            for (int i = 0; i < 3; i++)
                Palette.Prim(PrimitiveType.Sphere, palm, new Vector3(Mathf.Cos(i * 2.1f) * 0.2f, h - 0.2f, Mathf.Sin(i * 2.1f) * 0.2f), Vector3.one * 0.25f, new Color(0.45f, 0.55f, 0.2f), "Coco");
        }

        private static void LampPost(Transform r, Vector3 pos)
        {
            var c = new Color(0.25f, 0.27f, 0.3f);
            Box(r, pos + new Vector3(0, 1.8f, 0), new Vector3(0.12f, 3.6f, 0.12f), c, "Poste");
            Box(r, pos + new Vector3(0.35f, 3.6f, 0), new Vector3(0.8f, 0.08f, 0.1f), c);
            Box(r, pos + new Vector3(0.7f, 3.5f, 0), new Vector3(0.3f, 0.12f, 0.2f), new Color(0.98f, 0.95f, 0.8f));
        }

        private static void Bench(Transform r, Vector3 pos, Color c, bool alongZ = false)
        {
            var b = Group(r, pos, alongZ ? 90f : 0f, "Banco");
            Box(b, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.06f, 0.45f), c);
            Box(b, new Vector3(0, 0.75f, 0.22f), new Vector3(1.6f, 0.35f, 0.05f), c);
            foreach (float x in new[] { -0.7f, 0.7f }) Box(b, new Vector3(x, 0.22f, 0), new Vector3(0.06f, 0.45f, 0.4f), new Color(0.3f, 0.3f, 0.32f));
        }

        private static void Hoop(Transform r, Vector3 pos)
        {
            Box(r, pos + new Vector3(0, 1.5f, 0), new Vector3(0.1f, 3f, 0.1f), new Color(0.3f, 0.3f, 0.32f), "Tabela de basquete");
            Box(r, pos + new Vector3(0, 3.0f, -0.15f), new Vector3(1.1f, 0.7f, 0.05f), Color.white);
            Palette.Prim(PrimitiveType.Cylinder, r, pos + new Vector3(0, 2.8f, -0.45f), new Vector3(0.45f, 0.01f, 0.45f), new Color(0.95f, 0.45f, 0.15f), "Aro");
        }

        private static void Clothesline(Transform r, Vector3 a, Vector3 b)
        {
            var pole = new Color(0.55f, 0.55f, 0.58f);
            Box(r, a + new Vector3(0, 0.9f, 0), new Vector3(0.06f, 1.8f, 0.06f), pole, "Varal");
            Box(r, b + new Vector3(0, 0.9f, 0), new Vector3(0.06f, 1.8f, 0.06f), pole);
            var line = Box(r, (a + b) / 2 + new Vector3(0, 1.75f, 0), new Vector3(0.015f, 0.015f, (b - a).magnitude), Color.white);
            line.transform.localRotation = Quaternion.LookRotation(b - a);
            Color[] clothes = { new Color(0.95f, 0.4f, 0.4f), Color.white, new Color(0.4f, 0.6f, 0.95f), new Color(0.98f, 0.85f, 0.3f) };
            for (int i = 0; i < clothes.Length; i++)
            {
                var p = Vector3.Lerp(a, b, (i + 1f) / (clothes.Length + 1)) + new Vector3(0, 1.45f, 0);
                var piece = Box(r, p, new Vector3(0.6f, 0.6f, 0.02f), clothes[i], "Roupa");
                piece.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(b - a, Vector3.up));
            }
        }

        private static void DogHouse(Transform r, Vector3 pos)
        {
            Box(r, pos + new Vector3(0, 0.4f, 0), new Vector3(0.9f, 0.8f, 1.0f), new Color(0.75f, 0.5f, 0.3f), "Casinha de cachorro");
            Box(r, pos + new Vector3(0, 0.85f, 0), new Vector3(1.05f, 0.12f, 1.1f), new Color(0.75f, 0.3f, 0.2f));
            Box(r, pos + new Vector3(0, 0.3f, -0.51f), new Vector3(0.4f, 0.5f, 0.02f), new Color(0.15f, 0.1f, 0.08f));
        }

        private static void Umbrella(Transform r, Vector3 pos, Color a, Color b)
        {
            Palette.Prim(PrimitiveType.Cylinder, r, pos + new Vector3(0, 1.2f, 0), new Vector3(0.05f, 1.2f, 0.05f), Color.white, "Guarda-sol");
            var top = Palette.Prim(PrimitiveType.Sphere, r, pos + new Vector3(0, 2.35f, 0), new Vector3(2.6f, 0.45f, 2.6f), Color.white);
            top.GetComponent<Renderer>().sharedMaterial = TexMat(Palette.Stripes(a, b, 4), Vector2.one);
            Box(r, pos + new Vector3(0.9f, 0.2f, -0.3f), new Vector3(0.6f, 0.06f, 1.6f), new Color(0.98f, 0.85f, 0.3f), "Cadeira de praia");
        }

        // ================================================================== objetos de dentro

        private static void Fridge(Transform r, Vector3 pos, float yaw, Color body)
        {
            var g = Group(r, pos, yaw, "Geladeira");
            Box(g, new Vector3(0, 1.0f, 0), new Vector3(1.0f, 2.0f, 0.8f), body);
            Glow(g, new Vector3(0, 0.95f, -0.41f), new Vector3(0.82f, 1.5f, 0.02f), new Color(0.78f, 0.9f, 0.95f), "Vidro");
            for (int i = 0; i < 4; i++)
            {
                Box(g, new Vector3(0, 0.3f + i * 0.4f, -0.42f), new Vector3(0.82f, 0.03f, 0.02f), Color.Lerp(body, Color.black, 0.3f), "Prateleira");
                for (int k = 0; k < 5; k++)
                    Box(g, new Vector3(-0.32f + k * 0.16f, 0.42f + i * 0.4f, -0.43f), new Vector3(0.08f, 0.2f, 0.02f),
                        (i + k) % 3 == 0 ? new Color(0.35f, 0.6f, 0.3f) : (i + k) % 3 == 1 ? new Color(0.6f, 0.35f, 0.15f) : new Color(0.9f, 0.75f, 0.2f));
            }
            Box(g, new Vector3(0, 1.86f, -0.42f), new Vector3(0.9f, 0.18f, 0.02f), Color.Lerp(body, Color.white, 0.4f), "Placa");
        }

        private static void Crates(Transform r, Vector3 pos, int n, Color[] colors)
        {
            for (int i = 0; i < n; i++)
            {
                var c = colors[i % colors.Length];
                Box(r, pos + new Vector3(0, 0.17f + i * 0.34f, 0), new Vector3(0.55f, 0.32f, 0.7f), c, "Engradado");
                for (int k = 0; k < 4; k++)
                    Palette.Prim(PrimitiveType.Cylinder, r, pos + new Vector3(-0.12f + (k % 2) * 0.24f, 0.36f + i * 0.34f, -0.15f + (k / 2) * 0.3f),
                                 new Vector3(0.08f, 0.04f, 0.08f), new Color(0.45f, 0.25f, 0.1f));
            }
        }

        private static readonly Color PlasticWhite = new Color(0.96f, 0.96f, 0.94f);

        private static void StackedChairs(Transform r, Vector3 pos, Color c, int n, bool monobloc = false)
        {
            if (monobloc && Monobloco.Available)
            {
                // Monoblocos empilhados: cada uma encaixa na de baixo, um pouco mais alta e para trás.
                for (int i = 0; i < n; i++)
                {
                    var g = Monobloco.Build(r, c, TableProps.SeatHeight, i == 0 ? "Cadeiras empilhadas" : "Cadeira empilhada");
                    g.transform.localPosition = pos + new Vector3(0, i * 0.075f, -i * 0.025f);
                }
                return;
            }
            for (int i = 0; i < n; i++)
            {
                Box(r, pos + new Vector3(0, 0.44f + i * 0.09f, 0), new Vector3(0.5f, 0.05f, 0.48f), c, "Cadeira empilhada");
                Box(r, pos + new Vector3(0, 0.78f + i * 0.09f, 0.26f + i * 0.02f), new Vector3(0.5f, 0.62f, 0.05f), c);
            }
            for (int s = -1; s <= 1; s += 2)
                for (int f = -1; f <= 1; f += 2)
                    Box(r, pos + new Vector3(s * 0.22f, 0.21f, f * 0.2f), new Vector3(0.04f, 0.42f, 0.04f), c);
        }

        private static void Shelf(Transform r, Vector3 pos, float length, Color wood, bool glowing = false)
        {
            Box(r, pos, new Vector3(0.3f, 0.04f, length), wood, "Prateleira");
            Box(r, pos + new Vector3(0, 0.5f, 0), new Vector3(0.3f, 0.04f, length), wood);
            Color[] bottles = { new Color(0.3f, 0.55f, 0.3f), new Color(0.55f, 0.3f, 0.15f), new Color(0.85f, 0.75f, 0.3f), new Color(0.75f, 0.2f, 0.25f), new Color(0.85f, 0.88f, 0.9f) };
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < Mathf.FloorToInt(length / 0.22f); i++)
                {
                    var p = pos + new Vector3(0, 0.15f + row * 0.5f, -length / 2 + 0.15f + i * 0.22f);
                    var size = new Vector3(0.09f, 0.13f, 0.09f);
                    var c = bottles[(i + row * 2) % bottles.Length];
                    if (glowing) Glow(r, p, size, c, "Garrafa", PrimitiveType.Cylinder);
                    else Palette.Prim(PrimitiveType.Cylinder, r, p, size, c, "Garrafa");
                }
        }

        private static void Chalkboard(Transform r, Vector3 pos, float w, float h)
        {
            Box(r, pos, new Vector3(w + 0.1f, h + 0.1f, 0.04f), new Color(0.5f, 0.3f, 0.18f), "Quadro de preços");
            Box(r, pos + new Vector3(0, 0, -0.02f), new Vector3(w, h, 0.02f), new Color(0.12f, 0.25f, 0.18f));
            for (int i = 0; i < 5; i++)
            {
                float y = h / 2 - 0.15f - i * (h - 0.2f) / 5f;
                Glow(r, pos + new Vector3(-w * 0.12f, y, -0.035f), new Vector3(w * 0.55f, 0.03f, 0.005f), new Color(0.92f, 0.92f, 0.88f), "Escrita");
                Glow(r, pos + new Vector3(w * 0.36f, y, -0.035f), new Vector3(w * 0.14f, 0.03f, 0.005f), new Color(0.98f, 0.85f, 0.4f));
            }
        }

        private static void Clock(Transform r, Vector3 pos)
        {
            var c = Palette.Prim(PrimitiveType.Cylinder, r, pos, new Vector3(0.4f, 0.02f, 0.4f), Color.white, "Relógio");
            c.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Box(r, pos + new Vector3(0, 0.06f, -0.03f), new Vector3(0.02f, 0.13f, 0.01f), Color.black);
            Box(r, pos + new Vector3(0.04f, 0, -0.03f), new Vector3(0.09f, 0.02f, 0.01f), Color.black);
        }

        private static void Tv(Transform r, Vector3 pos)
        {
            Box(r, pos, new Vector3(0.1f, 0.62f, 1.05f), new Color(0.1f, 0.1f, 0.11f), "TV");
            Glow(r, pos + new Vector3(-0.055f, 0, 0), new Vector3(0.01f, 0.52f, 0.94f), new Color(0.35f, 0.7f, 0.45f), "Tela");
            Box(r, pos + new Vector3(0.1f, -0.36f, 0), new Vector3(0.2f, 0.05f, 0.3f), new Color(0.2f, 0.2f, 0.22f), "Suporte");
        }

        private static void Poster(Transform r, Side side, float u, float y, float w, float h, Color bg, Color accent)
        {
            Box(r, At(side, u, y, 0.01f), Dim(side, w, h, 0.02f), bg, "Pôster");
            Box(r, At(side, u, y + h * 0.1f, 0.025f), Dim(side, w * 0.7f, h * 0.45f, 0.01f), accent);
            Box(r, At(side, u, y - h * 0.32f, 0.025f), Dim(side, w * 0.6f, h * 0.08f, 0.01f), Color.Lerp(accent, Color.black, 0.3f));
        }

        /// <summary>Quadro de avisos/ferramentas com papéis coloridos.</summary>
        private static void Board(Transform r, Side side, float u, float y, float w, float h, Color bg, Color[] notes)
        {
            Box(r, At(side, u, y, 0.02f), Dim(side, w + 0.08f, h + 0.08f, 0.03f), new Color(0.45f, 0.3f, 0.2f), "Mural");
            Box(r, At(side, u, y, 0.04f), Dim(side, w, h, 0.02f), bg);
            int n = Mathf.FloorToInt(w / 0.4f);
            for (int i = 0; i < n; i++)
            {
                float du = -w / 2 + 0.25f + i * (w - 0.4f) / Mathf.Max(1, n - 1);
                float dy = (i % 2 == 0 ? 0.2f : -0.18f) * h;
                Box(r, At(side, u + du, y + dy, 0.055f), Dim(side, 0.28f, 0.34f, 0.01f), notes[i % notes.Length], "Papel");
            }
        }

        private static void TubeLight(Transform r, Vector3 ceiling, bool alongX)
        {
            var size = alongX ? new Vector3(1.3f, 0.06f, 0.14f) : new Vector3(0.14f, 0.06f, 1.3f);
            NoShadow(Box(r, ceiling - new Vector3(0, 0.03f, 0), size, new Color(0.85f, 0.85f, 0.85f), "Luminária"));
            Glow(r, ceiling - new Vector3(0, 0.07f, 0), Vector3.Scale(size, new Vector3(0.92f, 0.5f, 0.5f)), new Color(1f, 1f, 0.95f), "Lâmpada");
        }

        private static void Fan(Transform r, Vector3 ceiling, Color c)
        {
            var g = Group(r, ceiling, 20f, "Ventilador de teto");
            Box(g, new Vector3(0, -0.2f, 0), new Vector3(0.04f, 0.4f, 0.04f), c);
            Palette.Prim(PrimitiveType.Cylinder, g, new Vector3(0, -0.42f, 0), new Vector3(0.22f, 0.06f, 0.22f), c);
            for (int i = 0; i < 3; i++)
            {
                var blade = Box(g, Vector3.zero, new Vector3(0.16f, 0.015f, 0.6f), Color.Lerp(c, Color.black, 0.15f), "Pá");
                blade.transform.localRotation = Quaternion.Euler(0, i * 120f, 0);
                blade.transform.localPosition = new Vector3(0, -0.44f, 0) + blade.transform.localRotation * new Vector3(0, 0, 0.38f);
            }
        }

        private static void Bulb(Transform r, Vector3 ceiling, float drop)
        {
            Box(r, ceiling - new Vector3(0, drop / 2, 0), new Vector3(0.015f, drop, 0.015f), new Color(0.15f, 0.15f, 0.15f), "Fio");
            Glow(r, ceiling - new Vector3(0, drop + 0.08f, 0), Vector3.one * 0.16f, new Color(1f, 0.9f, 0.6f), "Lâmpada", PrimitiveType.Sphere);
        }

        private static void WovenLamp(Transform r, Vector3 ceiling)
        {
            Box(r, ceiling - new Vector3(0, 0.3f, 0), new Vector3(0.015f, 0.6f, 0.015f), new Color(0.3f, 0.2f, 0.1f), "Fio");
            Palette.Prim(PrimitiveType.Sphere, r, ceiling - new Vector3(0, 0.75f, 0), new Vector3(0.5f, 0.38f, 0.5f), new Color(0.82f, 0.66f, 0.4f), "Luminária de palha");
            Glow(r, ceiling - new Vector3(0, 0.92f, 0), Vector3.one * 0.18f, new Color(1f, 0.9f, 0.6f), "Lâmpada", PrimitiveType.Sphere);
        }

        private static void DomeLight(Transform r, Vector3 ceiling)
        {
            Box(r, ceiling - new Vector3(0, 0.02f, 0), new Vector3(0.6f, 0.04f, 0.6f), new Color(0.85f, 0.85f, 0.85f), "Luminária");
            Glow(r, ceiling - new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.04f, 0.5f), new Color(1f, 1f, 0.95f), "Lâmpada");
        }

        private static void BingoBoard(Transform r, Vector3 pos)
        {
            float w = 4.2f, h = 1.35f;
            Box(r, pos, new Vector3(w + 0.12f, h + 0.12f, 0.06f), new Color(0.55f, 0.36f, 0.22f), "Painel do bingo");
            Box(r, pos + new Vector3(0, 0, -0.03f), new Vector3(w, h, 0.02f), new Color(0.12f, 0.16f, 0.36f));
            Color[] letters = { new Color(0.9f, 0.25f, 0.25f), new Color(0.98f, 0.75f, 0.2f), new Color(0.3f, 0.7f, 0.35f), new Color(0.3f, 0.55f, 0.95f), new Color(0.75f, 0.4f, 0.85f) };
            var rng = new System.Random(3);
            for (int row = 0; row < 5; row++)
            {
                float y = h / 2 - 0.14f - row * 0.265f;
                Box(r, pos + new Vector3(-w / 2 + 0.16f, y, -0.045f), new Vector3(0.2f, 0.2f, 0.01f), letters[row], "Letra");
                for (int col = 0; col < 15; col++)
                {
                    var p = pos + new Vector3(-w / 2 + 0.42f + col * 0.25f, y, -0.045f);
                    var size = new Vector3(0.18f, 0.18f, 0.01f);
                    if (rng.NextDouble() < 0.3) Glow(r, p, size, new Color(1f, 0.92f, 0.5f), "Número sorteado");
                    else Box(r, p, size, new Color(0.24f, 0.3f, 0.52f));
                }
            }
        }

        private static void FoldingTable(Transform r, Vector3 pos, Vector3 size, Color top)
        {
            Box(r, pos + new Vector3(0, size.y, 0), new Vector3(size.x, 0.04f, size.z), top, "Mesa comprida");
            for (int s = -1; s <= 1; s += 2)
                for (int f = -1; f <= 1; f += 2)
                    Box(r, pos + new Vector3(s * (size.x / 2 - 0.06f), size.y / 2, f * (size.z / 2 - 0.08f)), new Vector3(0.04f, size.y, 0.04f), new Color(0.4f, 0.4f, 0.42f));
        }

        private static void Bookcase(Transform r, Vector3 pos, float length, Color metal, Color[] boxes)
        {
            for (int i = 0; i < 4; i++)
                Box(r, pos + new Vector3(0, 0.1f + i * 0.55f, 0), new Vector3(0.48f, 0.04f, length), metal, "Estante");
            foreach (float z in new[] { -length / 2, 0f, length / 2 })
                Box(r, pos + new Vector3(0, 1.0f, z), new Vector3(0.48f, 2.0f, 0.04f), metal);
            var rng = new System.Random(11);
            for (int i = 0; i < 4; i++)
            {
                float z = -length / 2 + 0.1f;
                while (z < length / 2 - 0.35f)
                {
                    float bw = 0.25f + (float)rng.NextDouble() * 0.3f, bh = 0.12f + (float)rng.NextDouble() * 0.3f;
                    if (i == 3) bh = Mathf.Min(bh, 0.25f);
                    Box(r, pos + new Vector3(0.02f, 0.12f + i * 0.55f + bh / 2, z + bw / 2), new Vector3(0.38f, bh, bw - 0.02f),
                        boxes[rng.Next(boxes.Length)], "Jogo de tabuleiro");
                    z += bw + 0.02f;
                }
            }
        }

        private static void Arcade(Transform r, Vector3 pos, Color body)
        {
            var g = Group(r, pos, -90f, "Fliperama");
            Box(g, new Vector3(0, 0.85f, 0), new Vector3(0.7f, 1.7f, 0.7f), body);
            Glow(g, new Vector3(0, 1.3f, -0.36f), new Vector3(0.52f, 0.42f, 0.02f), new Color(0.3f, 0.85f, 0.95f), "Tela");
            Glow(g, new Vector3(0, 1.62f, -0.36f), new Vector3(0.6f, 0.12f, 0.02f), new Color(1f, 0.55f, 0.85f), "Letreiro");
            var panel = Box(g, new Vector3(0, 0.98f, -0.45f), new Vector3(0.66f, 0.06f, 0.3f), Color.Lerp(body, Color.black, 0.3f), "Controles");
            panel.transform.localRotation = Quaternion.Euler(-15, 0, 0);
            Palette.Prim(PrimitiveType.Sphere, g, new Vector3(-0.15f, 1.06f, -0.48f), Vector3.one * 0.07f, new Color(0.9f, 0.2f, 0.2f));
            Palette.Prim(PrimitiveType.Sphere, g, new Vector3(0.1f, 1.05f, -0.48f), Vector3.one * 0.05f, new Color(0.98f, 0.85f, 0.2f));
            Palette.Prim(PrimitiveType.Sphere, g, new Vector3(0.2f, 1.05f, -0.48f), Vector3.one * 0.05f, new Color(0.3f, 0.6f, 0.95f));
        }

        private static void Barbecue(Transform r, Vector3 pos, Texture2D brick)
        {
            var g = Group(r, pos, 0, "Churrasqueira");
            Block(g, new Vector3(0, 0.45f, 0), new Vector3(2.0f, 0.9f, 0.8f), Color.white, brick, new Vector2(3.3f, 1.5f), "Base");
            Box(g, new Vector3(0, 0.92f, -0.02f), new Vector3(2.04f, 0.06f, 0.84f), new Color(0.6f, 0.58f, 0.55f), "Bancada");
            Box(g, new Vector3(0, 1.25f, 0.15f), new Vector3(1.4f, 0.6f, 0.5f), new Color(0.12f, 0.1f, 0.1f), "Boca");
            Block(g, new Vector3(-0.85f, 1.25f, 0), new Vector3(0.3f, 0.6f, 0.8f), Color.white, brick, new Vector2(0.5f, 1f));
            Block(g, new Vector3(0.85f, 1.25f, 0), new Vector3(0.3f, 0.6f, 0.8f), Color.white, brick, new Vector2(0.5f, 1f));
            var hood = Block(g, new Vector3(0, 1.85f, 0.05f), new Vector3(2.0f, 0.6f, 0.7f), Color.white, brick, new Vector2(3.3f, 1f), "Coifa");
            Block(g, new Vector3(0, (2.15f + H) / 2, 0.15f), new Vector3(0.7f, H - 2.15f, 0.5f), Color.white, brick, new Vector2(1.2f, 2f), "Chaminé");
            Box(g, new Vector3(0, 1.05f, -0.05f), new Vector3(1.3f, 0.02f, 0.5f), new Color(0.3f, 0.3f, 0.32f), "Grelha");
            Glow(g, new Vector3(0, 0.98f, 0.1f), new Vector3(1.2f, 0.04f, 0.35f), new Color(1f, 0.45f, 0.15f), "Brasa");
            for (int i = 0; i < 4; i++)
            {
                var skewer = Box(g, new Vector3(-0.45f + i * 0.3f, 1.09f, -0.05f), new Vector3(0.02f, 0.02f, 0.7f), new Color(0.75f, 0.75f, 0.78f), "Espeto");
                for (int k = 0; k < 3; k++)
                    Palette.Prim(PrimitiveType.Sphere, g, new Vector3(-0.45f + i * 0.3f, 1.1f, -0.2f + k * 0.14f), new Vector3(0.1f, 0.08f, 0.1f),
                                 k == 1 ? new Color(0.75f, 0.35f, 0.2f) : new Color(0.55f, 0.25f, 0.15f), "Carne");
            }
        }

        private static void CoconutCart(Transform r, Vector3 pos)
        {
            var g = Group(r, pos, 25f, "Carrinho de coco");
            var stripes = Palette.Stripes(new Color(0.3f, 0.65f, 0.35f), Color.white, 3);
            Block(g, new Vector3(0, 0.6f, 0), new Vector3(1.4f, 0.6f, 0.75f), Color.white, stripes, Vector2.one, "Caixa");
            foreach (float x in new[] { -0.5f, 0.5f })
            {
                var wheel = Palette.Prim(PrimitiveType.Cylinder, g, new Vector3(x, 0.22f, -0.4f), new Vector3(0.44f, 0.03f, 0.44f), new Color(0.2f, 0.2f, 0.22f), "Roda");
                wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            Box(g, new Vector3(0.95f, 0.85f, 0), new Vector3(0.5f, 0.04f, 0.04f), new Color(0.6f, 0.6f, 0.62f), "Puxador");
            for (int i = 0; i < 9; i++)
                Palette.Prim(PrimitiveType.Sphere, g, new Vector3(-0.5f + (i % 5) * 0.25f, 0.99f + (i / 5) * 0.16f, -0.12f + (i % 2) * 0.24f),
                             new Vector3(0.24f, 0.26f, 0.24f), new Color(0.36f, 0.62f, 0.22f), "Coco verde");
            Box(g, new Vector3(-0.6f, 1.6f, 0), new Vector3(0.04f, 1.2f, 0.04f), Color.white, "Haste");
            var top = Palette.Prim(PrimitiveType.Sphere, g, new Vector3(-0.6f, 2.2f, 0), new Vector3(1.5f, 0.3f, 1.5f), Color.white, "Guarda-sol");
            top.GetComponent<Renderer>().sharedMaterial = TexMat(Palette.Stripes(new Color(0.98f, 0.85f, 0.25f), new Color(0.3f, 0.65f, 0.35f), 3), Vector2.one);
        }

        private static void Stool(Transform r, Vector3 pos, Color seat)
        {
            Palette.Prim(PrimitiveType.Cylinder, r, pos + new Vector3(0, 0.38f, 0), new Vector3(0.06f, 0.38f, 0.06f), new Color(0.6f, 0.6f, 0.62f), "Banquinho");
            Palette.Prim(PrimitiveType.Cylinder, r, pos + new Vector3(0, 0.78f, 0), new Vector3(0.38f, 0.04f, 0.38f), seat);
            Palette.Prim(PrimitiveType.Cylinder, r, pos + new Vector3(0, 0.02f, 0), new Vector3(0.32f, 0.02f, 0.32f), new Color(0.6f, 0.6f, 0.62f));
        }

        private static void Surfboard(Transform r, Vector3 pos, Color c, float tilt)
        {
            var b = Palette.Prim(PrimitiveType.Capsule, r, pos + new Vector3(0, 0.95f, 0), new Vector3(0.5f, 0.95f, 0.08f), c, "Prancha");
            b.transform.localRotation = Quaternion.Euler(0, 90, tilt);
        }

        private static void VendingMachine(Transform r, Vector3 pos)
        {
            var g = Group(r, pos, -90f, "Máquina de lanches");
            Box(g, new Vector3(0, 0.95f, 0), new Vector3(0.9f, 1.9f, 0.75f), new Color(0.2f, 0.4f, 0.72f));
            Glow(g, new Vector3(-0.1f, 1.15f, -0.38f), new Vector3(0.6f, 1.3f, 0.02f), new Color(0.85f, 0.92f, 0.98f), "Vitrine");
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 4; col++)
                    Box(g, new Vector3(-0.32f + col * 0.15f, 0.62f + row * 0.25f, -0.4f), new Vector3(0.1f, 0.14f, 0.02f),
                        (row + col) % 3 == 0 ? new Color(0.9f, 0.3f, 0.3f) : (row + col) % 3 == 1 ? new Color(0.98f, 0.8f, 0.2f) : new Color(0.3f, 0.7f, 0.4f), "Lanche");
            Box(g, new Vector3(0.32f, 1.2f, -0.39f), new Vector3(0.15f, 0.4f, 0.02f), new Color(0.15f, 0.15f, 0.17f), "Teclado");
        }

        private static void Roulette(Transform r, Vector3 pos)
        {
            var g = Group(r, pos, 0, "Roleta");
            var wood = new Color(0.35f, 0.18f, 0.08f);
            Box(g, new Vector3(0, 0.42f, 0), new Vector3(2.2f, 0.84f, 1.1f), wood);
            Box(g, new Vector3(0.3f, 0.86f, 0), new Vector3(1.5f, 0.04f, 1.0f), Palette.Felt, "Pano");
            for (int i = 0; i < 6; i++)
                Box(g, new Vector3(-0.3f + i * 0.24f, 0.885f, 0), new Vector3(0.2f, 0.01f, 0.8f), i % 2 == 0 ? new Color(0.75f, 0.12f, 0.12f) : new Color(0.1f, 0.1f, 0.1f));
            Palette.Prim(PrimitiveType.Cylinder, g, new Vector3(-0.72f, 0.92f, 0), new Vector3(0.8f, 0.06f, 0.8f), wood, "Roda da roleta");
            var wheel = Palette.Prim(PrimitiveType.Cylinder, g, new Vector3(-0.72f, 0.95f, 0), new Vector3(0.62f, 0.02f, 0.62f), Color.white);
            wheel.GetComponent<Renderer>().sharedMaterial = TexMat(Sectors(new Color(0.75f, 0.12f, 0.12f), new Color(0.1f, 0.1f, 0.1f)), Vector2.one);
            Palette.Prim(PrimitiveType.Cylinder, g, new Vector3(-0.72f, 1.0f, 0), new Vector3(0.18f, 0.06f, 0.18f), Palette.Gold);
            Palette.Prim(PrimitiveType.Sphere, g, new Vector3(-0.5f, 0.98f, 0.12f), Vector3.one * 0.04f, Color.white, "Bolinha");
            ChipStacks(r, pos + new Vector3(0.6f, 0.88f, -0.2f));
        }

        private static void ChipStacks(Transform r, Vector3 pos)
        {
            Color[] chips = { new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.35f, 0.85f), new Color(0.1f, 0.1f, 0.1f), Palette.Gold, new Color(0.2f, 0.65f, 0.3f) };
            for (int i = 0; i < chips.Length; i++)
            {
                int h = 3 + (i * 3) % 5;
                Palette.Prim(PrimitiveType.Cylinder, r, pos + new Vector3((i % 3) * 0.1f, h * 0.012f, (i / 3) * 0.1f), new Vector3(0.08f, h * 0.012f, 0.08f), chips[i], "Fichas");
            }
        }

        private static void SlotMachine(Transform r, Vector3 pos, int seed)
        {
            var g = Group(r, pos, -90f, "Caça-níquel");
            Box(g, new Vector3(0, 0.85f, 0), new Vector3(0.7f, 1.7f, 0.65f), new Color(0.45f, 0.08f, 0.12f));
            Box(g, new Vector3(0, 1.75f, 0), new Vector3(0.74f, 0.12f, 0.69f), Palette.Gold, "Topo");
            Color[] screens = { new Color(0.98f, 0.8f, 0.3f), new Color(0.4f, 0.9f, 0.6f), new Color(0.98f, 0.5f, 0.6f), new Color(0.5f, 0.75f, 0.98f) };
            Glow(g, new Vector3(0, 1.25f, -0.33f), new Vector3(0.5f, 0.4f, 0.02f), screens[seed % screens.Length], "Tela");
            for (int i = 0; i < 3; i++)
                Box(g, new Vector3(-0.15f + i * 0.15f, 1.25f, -0.345f), new Vector3(0.1f, 0.3f, 0.01f), Color.white, "Rolo");
            Box(g, new Vector3(0, 0.88f, -0.4f), new Vector3(0.6f, 0.06f, 0.2f), new Color(0.25f, 0.05f, 0.08f), "Painel");
            Box(g, new Vector3(0.4f, 1.3f, 0), new Vector3(0.04f, 0.4f, 0.04f), new Color(0.75f, 0.75f, 0.78f), "Alavanca");
            Palette.Prim(PrimitiveType.Sphere, g, new Vector3(0.4f, 1.52f, 0), Vector3.one * 0.1f, new Color(0.9f, 0.15f, 0.15f));
        }

        /// <summary>Tigre dourado sentado num pedestal (com listras escuras).</summary>
        private static void TigerStatue(Transform r, Vector3 pos)
        {
            var g = Group(r, pos, -35f, "Estátua do tigre");
            Box(g, new Vector3(0, 0.45f, 0), new Vector3(0.8f, 0.9f, 0.8f), new Color(0.12f, 0.1f, 0.12f), "Pedestal");
            Box(g, new Vector3(0, 0.92f, 0), new Vector3(0.86f, 0.05f, 0.86f), Palette.Gold);
            var striped = TexMat(Palette.Stripes(Palette.Gold, new Color(0.35f, 0.22f, 0.05f), 5), Vector2.one);
            var body = Palette.Prim(PrimitiveType.Capsule, g, new Vector3(0, 1.35f, 0.08f), new Vector3(0.42f, 0.42f, 0.42f), Color.white, "Corpo");
            body.transform.localRotation = Quaternion.Euler(-18, 0, 0);
            body.GetComponent<Renderer>().sharedMaterial = striped;
            Palette.Prim(PrimitiveType.Sphere, g, new Vector3(0, 1.82f, -0.12f), Vector3.one * 0.34f, Palette.Gold, "Cabeça");
            Palette.Prim(PrimitiveType.Sphere, g, new Vector3(0, 1.76f, -0.28f), new Vector3(0.16f, 0.12f, 0.12f), Color.Lerp(Palette.Gold, Color.white, 0.3f), "Focinho");
            foreach (float x in new[] { -0.12f, 0.12f })
            {
                Palette.Prim(PrimitiveType.Sphere, g, new Vector3(x, 1.98f, -0.08f), Vector3.one * 0.1f, Palette.Gold, "Orelha");
                Palette.Prim(PrimitiveType.Cylinder, g, new Vector3(x * 0.8f, 1.12f, -0.18f), new Vector3(0.1f, 0.2f, 0.1f), Palette.Gold, "Pata");
            }
            var tail = Palette.Prim(PrimitiveType.Capsule, g, new Vector3(0.25f, 1.05f, 0.3f), new Vector3(0.07f, 0.28f, 0.07f), Color.white, "Rabo");
            tail.transform.localRotation = Quaternion.Euler(60, 0, 40);
            tail.GetComponent<Renderer>().sharedMaterial = striped;
        }

        private static void GoldDie(Transform r, Vector3 pos, float size)
        {
            var die = Box(r, pos, Vector3.one * size, Palette.Gold, "Dado de ouro");
            die.transform.localRotation = Quaternion.Euler(0, 30, 0);
            var pip = new Color(0.15f, 0.1f, 0.05f);
            float p = size * 0.25f, e = size * 0.5f + 0.003f;
            // Face da frente (5) e de cima (1), o que aparece para a câmera.
            foreach (var o in new[] { new Vector2(-p, -p), new Vector2(p, p), new Vector2(-p, p), new Vector2(p, -p), Vector2.zero })
                Palette.Prim(PrimitiveType.Sphere, die.transform, new Vector3(o.x / size, o.y / size, -e / size), Vector3.one * 0.16f, pip, "Pinta");
            Palette.Prim(PrimitiveType.Sphere, die.transform, new Vector3(0, e / size, 0), Vector3.one * 0.18f, pip, "Pinta");
        }

        private static void Chandelier(Transform r, Vector3 ceiling)
        {
            Box(r, ceiling - new Vector3(0, 0.25f, 0), new Vector3(0.03f, 0.5f, 0.03f), Palette.Gold, "Lustre");
            Palette.Prim(PrimitiveType.Cylinder, r, ceiling - new Vector3(0, 0.55f, 0), new Vector3(1.2f, 0.03f, 1.2f), Palette.Gold);
            Palette.Prim(PrimitiveType.Sphere, r, ceiling - new Vector3(0, 0.62f, 0), Vector3.one * 0.22f, Palette.Gold);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                Glow(r, ceiling + new Vector3(Mathf.Cos(a) * 0.58f, -0.5f, Mathf.Sin(a) * 0.58f), Vector3.one * 0.13f, new Color(1f, 0.92f, 0.7f), "Lâmpada", PrimitiveType.Sphere);
            }
        }

        // ================================================================== peças básicas

        private static GameObject Box(Transform p, Vector3 pos, Vector3 size, Color c, string name = null)
            => Palette.Prim(PrimitiveType.Cube, p, pos, size, c, name);

        private static GameObject Block(Transform p, Vector3 pos, Vector3 size, Color c, Texture2D tex, Vector2 tiles, string name = null)
        {
            var go = Box(p, pos, size, c, name);
            if (tex != null) go.GetComponent<Renderer>().sharedMaterial = TexMat(tex, tiles, c);
            return go;
        }

        /// <summary>Peças presas no teto não fazem sombra (senão riscam o chão e a mesa).</summary>
        private static GameObject NoShadow(GameObject go)
        {
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static Transform Group(Transform r, Vector3 pos, float yaw, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(r, false);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            return g;
        }

        private static readonly Dictionary<(Texture2D, Vector2, Color), Material> TexCache = new Dictionary<(Texture2D, Vector2, Color), Material>();
        private static readonly Dictionary<Color, Material> GlowCache = new Dictionary<Color, Material>();

        private static Material TexMat(Texture2D tex, Vector2 tiles) => TexMat(tex, tiles, Color.white);

        private static Material TexMat(Texture2D tex, Vector2 tiles, Color tint)
        {
            var key = (tex, tiles, tint);
            if (TexCache.TryGetValue(key, out var m) && m != null) return m;
            m = Palette.ToonTextured(tex, tint);
            m.mainTextureScale = tiles;
            TexCache[key] = m;
            return m;
        }

        /// <summary>Peça que brilha sozinha (lâmpadas, telas, janelas acesas): não depende da luz da cena.</summary>
        private static GameObject Glow(Transform p, Vector3 pos, Vector3 size, Color c, string name = null, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = Palette.Prim(type, p, pos, size, c, name);
            if (!GlowCache.TryGetValue(c, out var m) || m == null) GlowCache[c] = m = Palette.UnlitColor(c);
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = m;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ================================================================== texturas geradas (azulejo, tijolo, tábuas, ondas)

        private static readonly Dictionary<string, Texture2D> TexStore = new Dictionary<string, Texture2D>();

        private static Texture2D Make(string key, System.Func<int, int, Color> pixel, int size = 64)
        {
            if (TexStore.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = pixel(x, y);
            t.SetPixels(px);
            t.Apply(true);
            TexStore[key] = t;
            return t;
        }

        private static string K(string kind, Color a, Color b) => kind + a + b;

        /// <summary>Xadrez de 2x2 casas por repetição.</summary>
        private static Texture2D Checker(Color a, Color b)
            => Make(K("checker", a, b), (x, y) => ((x / 32) + (y / 32)) % 2 == 0 ? a : b);

        /// <summary>Uma placa por repetição, com rejunte nas bordas.</summary>
        private static Texture2D Grid(Color tile, Color grout)
            => Make(K("grid", tile, grout), (x, y) => x < 3 || y < 3 ? grout : tile);

        /// <summary>Azulejo decorado: quadrado com um losango no meio.</summary>
        private static Texture2D Tiles(Color ink, Color glaze)
            => Make(K("tiles", ink, glaze), (x, y) =>
            {
                if (x < 2 || y < 2) return Color.Lerp(glaze, ink, 0.25f);
                int dx = Mathf.Abs(x - 32), dy = Mathf.Abs(y - 32);
                if (dx + dy < 14) return ink;
                if (dx + dy > 22 && dx + dy < 26) return ink;
                return (dx < 3 || dy < 3) && dx + dy > 26 ? ink : glaze;
            });

        /// <summary>Dois tijolos por fileira, fileiras desencontradas.</summary>
        private static Texture2D Bricks(Color brick, Color mortar)
            => Make(K("bricks", brick, mortar), (x, y) =>
            {
                int row = y / 16;
                int xx = (x + (row % 2) * 16) % 32;
                return y % 16 < 2 || xx < 2 ? mortar : brick;
            });

        /// <summary>Tábuas (verticais ou horizontais) com junta escura.</summary>
        private static Texture2D Planks(Color a, Color b, bool vertical)
            => Make(K(vertical ? "planksV" : "planksH", a, b), (x, y) =>
            {
                int u = vertical ? x : y;
                int band = u / 16;
                if (u % 16 < 1) return Color.Lerp(b, Color.black, 0.35f);
                return band % 2 == 0 ? a : b;
            });

        /// <summary>Faixas horizontais.</summary>
        private static Texture2D Stripes(Color a, Color b, int bands)
            => Make(K("stripes" + bands, a, b), (x, y) => (y * bands * 2 / 64) % 2 == 0 ? a : b);

        /// <summary>Ondas do calçadão, em preto e branco.</summary>
        private static Texture2D Waves(Color a, Color b)
            => Make(K("waves", a, b), (x, y) =>
            {
                float v = y / 64f + 0.18f * Mathf.Sin(x / 64f * Mathf.PI * 2f);
                v -= Mathf.Floor(v);
                return v < 0.5f ? a : b;
            });

        /// <summary>Casas vermelhas e pretas em volta da roleta (vista de cima de um cilindro).</summary>
        private static Texture2D Sectors(Color a, Color b)
            => Make(K("sectors", a, b), (x, y) =>
            {
                float dx = x - 31.5f, dy = y - 31.5f;
                float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f;
                int s = Mathf.FloorToInt(ang * 36f);
                return s % 2 == 0 ? a : b;
            });

        // ================================================================== céu e luz

        private static void Sky(Camera cam, Color c)
        {
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = c;
        }

        /// <summary>
        /// Luz principal. Vem do lado da câmera, de cima, para os rostos dos personagens ficarem iluminados; o sol que
        /// entra pela abertura é a mancha clara no chão.
        /// </summary>
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
            l.shadowStrength = 0.85f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
        }
    }
}
