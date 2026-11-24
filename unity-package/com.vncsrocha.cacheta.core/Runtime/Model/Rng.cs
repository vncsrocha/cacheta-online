using System.Collections.Generic;

namespace Cacheta.Core
{
    /// <summary>
    /// Gerador pseudoaleatório próprio (xorshift64*). Usamos o nosso em vez de System.Random para que a mesma
    /// semente produza exatamente o mesmo embaralhamento no Unity (Mono/IL2CPP), no servidor e nos testes.
    /// </summary>
    public sealed class Rng
    {
        private ulong _state;

        public Rng(ulong seed)
        {
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Inteiro em [0, maxExclusive).</summary>
        public int Next(int maxExclusive) => (int)(NextULong() % (ulong)maxExclusive);

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
