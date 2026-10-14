using System;
using UnityEngine;

namespace EstagiarIo.Rede
{
    /// <summary>Os 8 fios de um cabo de rede de par trançado (cada par tem um fio colorido e um branco).</summary>
    public enum RedeFio
    {
        BrancoLaranja = 0,
        Laranja = 1,
        BrancoVerde = 2,
        Azul = 3,
        BrancoAzul = 4,
        Verde = 5,
        BrancoMarrom = 6,
        Marrom = 7,
    }

    /// <summary>
    /// Cores, nomes e a ordem correta dos fios no padrão <b>T568B</b> (o mais usado em redes cabeadas no Brasil).
    /// É lógica pura: não depende de cena, prefab nem UI — por isso é fácil de testar.
    /// </summary>
    public static class RedeCores
    {
        /// <summary>Ordem correta dos 8 fios, da posição 1 (ponta do conector) à 8.</summary>
        public static readonly RedeFio[] OrdemT568B =
        {
            RedeFio.BrancoLaranja,
            RedeFio.Laranja,
            RedeFio.BrancoVerde,
            RedeFio.Azul,
            RedeFio.BrancoAzul,
            RedeFio.Verde,
            RedeFio.BrancoMarrom,
            RedeFio.Marrom,
        };

        /// <summary>Todos os fios (na ordem do enum).</summary>
        public static RedeFio[] Todos() => (RedeFio[])Enum.GetValues(typeof(RedeFio));

        /// <summary>Cor de desenho do fio (para a UI 2D).</summary>
        public static Color Cor(RedeFio fio)
        {
            switch (fio)
            {
                case RedeFio.BrancoLaranja: return new Color(0.98f, 0.74f, 0.55f);
                case RedeFio.Laranja: return new Color(0.95f, 0.51f, 0.13f);
                case RedeFio.BrancoVerde: return new Color(0.68f, 0.90f, 0.62f);
                case RedeFio.Azul: return new Color(0.20f, 0.38f, 0.82f);
                case RedeFio.BrancoAzul: return new Color(0.62f, 0.80f, 0.96f);
                case RedeFio.Verde: return new Color(0.20f, 0.66f, 0.32f);
                case RedeFio.BrancoMarrom: return new Color(0.82f, 0.68f, 0.55f);
                case RedeFio.Marrom: return new Color(0.47f, 0.31f, 0.19f);
            }
            return Color.magenta;
        }

        /// <summary>Nome falado do fio.</summary>
        public static string Nome(RedeFio fio)
        {
            switch (fio)
            {
                case RedeFio.BrancoLaranja: return "Branco-laranja";
                case RedeFio.Laranja: return "Laranja";
                case RedeFio.BrancoVerde: return "Branco-verde";
                case RedeFio.Azul: return "Azul";
                case RedeFio.BrancoAzul: return "Branco-azul";
                case RedeFio.Verde: return "Verde";
                case RedeFio.BrancoMarrom: return "Branco-marrom";
                case RedeFio.Marrom: return "Marrom";
            }
            return fio.ToString();
        }

        /// <summary>Rótulo curto usado nas fichas da UI (ex.: "3 B-verde").</summary>
        public static string Rotulo(RedeFio fio) => (PosicaoCorreta(fio) + 1) + "  " + Nome(fio);

        /// <summary>Posição (0..7) que o fio ocupa na ordem T568B, ou -1 se não pertencer.</summary>
        public static int PosicaoCorreta(RedeFio fio) => Array.IndexOf(OrdemT568B, fio);

        /// <summary>A sequência está exatamente na ordem T568B?</summary>
        public static bool OrdemCorreta(RedeFio[] ordem)
        {
            if (ordem == null || ordem.Length != OrdemT568B.Length) return false;
            for (int i = 0; i < ordem.Length; i++)
                if (ordem[i] != OrdemT568B[i]) return false;
            return true;
        }
    }
}
