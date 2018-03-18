using Accord.Statistics.Filters;
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Models.Markov.Learning;
using Accord.Statistics.Models.Markov.Topology;
using PassGenAI.HMM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PassGenAI.Keyboard
{
    public class KeyboardWalks
    {
        private static string[][] keyboard = new string[][] {
            new[] { "`~","1!","2@","3#","4$","5%","6^","7&","8*","9(","0)","-_","=+", "" },
            new[] { "","qQ","wW","eE","rR","tT","yY","uU","iI","oO","pP","[{","]}", "\\|" },
            new[] { "","aA","sS","dD","fF","gG","hH","jJ","kK","lL",";:","'\"","", "" },
            new[] { "","zZ","xX","cC","vV","bB","nN","mM",",<",".>","/?","","", "" },
            new[] { "  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ", "  " }
        };

        private static (int x, int y)[][] moveMatrix = new[] {
            new (int x, int y)[]{ (-1, -1), (0, -1), (1, -1)},
            new (int x, int y)[]{ (-1, 0), (0, 0), (1, 0)},
            new (int x, int y)[]{ (-1, 1), (0, 1), (1, 1)},
        };

        public static IEnumerable<string> Walk(int length)
        {
            var ngrams = new List<string[]>();

            var moves = moveMatrix.SelectMany(m => m).ToList();
            for (int y = 0; y < keyboard.Length; y++)
            {
                for (int x = 0; x < keyboard[y].Length; x++)
                {
                    moves.ForEach(m =>
                    {
                        if (m.x + x >= 0 && m.y + y >= 0 && m.x + x <= 13 && m.y + y <= 4 && !(m.x == 0 && m.y == 0))
                        {
                            var part1 = keyboard[y][x];
                            var part2 = keyboard[y + m.y][m.x + x];
                            if (!string.IsNullOrEmpty(part1) && !string.IsNullOrEmpty(part2))
                            {
                                ngrams.Add(new[] { part1[0].ToString(), part2[0].ToString() });
                                ngrams.Add(new[] { part1[0].ToString(), part2[1].ToString() });
                                ngrams.Add(new[] { part1[1].ToString(), part2[1].ToString() });
                                ngrams.Add(new[] { part1[1].ToString(), part2[0].ToString() });
                            }
                        }
                    });
                }
            }

            var ngramGroups = ngrams.GroupBy(x => x[0]);

            do
            {
                foreach (var g in ngramGroups)
                {
                    foreach (var item in g)
                    {
                        var result = string.Join("", item);

                        while (result.Length < length)
                        {
                            var next = ngramGroups.Where(x => x.Key == result.Last().ToString()).SelectMany(x => x).OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                            result = result + string.Join("", next.Skip(1));
                        }
                        yield return result;
                    }
                }
            } while (true);
        }
    }
}
