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
            //new[] { "  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ", "  " }
        };

        private static (int x, int y)[][] complexMoveMatrix = new[] {
            new (int x, int y)[]{ (-1, -1), (0, -1), (1, -1)},
            new (int x, int y)[]{ (-1, 0), (0, 0), (1, 0)},
            new (int x, int y)[]{ (-1, 1), (0, 1), (1, 1)},
        };

        private static (int x, int y)[][] simpleMoveMatrix = new[] {
            new (int x, int y)[]{ (0, 0), (0, -1), (0, 0)},
            new (int x, int y)[]{ (-1, 0), (0, 0), (1, 0)},
            new (int x, int y)[]{ (0, 0), (0, 1), (0, 0)},
        };

        public static IEnumerable<string> Walk(int length, bool simple = true)
        {
            var ngrams = new List<string[]>();

            var moves = simple ? simpleMoveMatrix.SelectMany(m => m).ToList() : complexMoveMatrix.SelectMany(m => m).ToList();
            for (int y = 0; y < keyboard.Length; y++)
            {
                for (int x = 0; x < keyboard[y].Length; x++)
                {
                    moves.ForEach(m =>
                    {
                        if (m.x + x >= 0 && m.y + y >= 0 && m.x + x <= keyboard[0].Length-1 && m.y + y <= keyboard.Length-1 && !(m.x == 0 && m.y == 0))
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

            var ngramGroups = ngrams.GroupBy(x => x[0]).ToList();

            var ngramLookup = new Dictionary<string, List<string[]>>();

            ngramGroups.ForEach(ng => ngramLookup.Add(ng.Key, ng.Select(x => x).ToList()));
            ngramGroups = null;

            var queue = new System.Collections.Concurrent.ConcurrentQueue<List<string>>();

            var t = Task.Factory.StartNew(() =>
            {
                ngramLookup.Keys.AsParallel().ForAll(key =>
                {
                    var results = ngramLookup[key.ToString()].Select(x => key + x[1]).ToList();
                    while (results.First().Length < length)
                    {
                        var nextResults = new List<string>(results.Count * ngramLookup.Keys.Count);
                        foreach (var r in results)
                        {
                            nextResults.AddRange(ngramLookup[r.Last().ToString()].Select(x => r + x[1]));
                        }
                        results = nextResults;
                    }

                    queue.Enqueue(results);
                });
            });

            while (t.Status != TaskStatus.Running) ; //Wait for it to get started

            do
            {
                while(queue.TryDequeue(out var items))
                {
                    foreach (var item in items)
                    {
                        yield return item;
                    }
                }
            } while (t.Status == TaskStatus.Running);
        }
    }
}
