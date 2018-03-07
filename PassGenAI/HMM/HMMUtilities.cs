using Accord.Statistics.Filters;
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Models.Markov.Learning;
using Accord.Statistics.Models.Markov.Topology;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PassGenAI.HMM;

namespace PassGenAI.HMM
{
    public static class HMMUtilities
    {
        public static HMMGroup CreateHiddenMarkovModel(IEnumerable<string> fileNames, int? length = null)
        {
            var ngrams = GetNgramsSimple(fileNames, length ?? 8);

            var codebook = new Codification("data", ngrams);
            var sequence = codebook.ParallelTransform("data", ngrams);

            ngrams = null;

            var topology = new Forward(states: 4);
            int symbols = codebook["data"].NumberOfSymbols;
            var hmm = new HiddenMarkovModel(topology, symbols);
            var teacher = new BaumWelchLearning(hmm);
            teacher.Learn(sequence);
            return new HMMGroup
            {
                Model = hmm,
                Codebook = codebook,
                Length = length
            };
        }

        private static string[][] GetNgramsSimple(IEnumerable<string> fileNames, int length)
        {
            var ngrams = new List<string[]>(60000000);
            int i = 0;
            foreach (var fileName in fileNames)
            {
                using (var stream = File.OpenRead(fileName))
                using (var reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (line.Length == length)
                        {
                            ngrams.Add(line.Select(x=>x.ToString()).ToArray());
                        }
                    }
                }
            }
            return ngrams.ToArray();
        }

        private static string[][] GetNgrams(IEnumerable<string> fileNames, int? length = null)
        {
            bool byWord = false;
            var passwords = new List<string>(50000000);
            foreach (var fileName in fileNames)
            {
                using (var stream = File.OpenRead(fileName))
                using (var reader = new StreamReader(stream))
                {
                    if (length != null)
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (line.Length == length) passwords.Add(line);
                        }
                    }
                    else
                    {
                        while (!reader.EndOfStream)
                        {
                            passwords.Add(reader.ReadLine());
                        }
                    }
                }
            }
            //var passwords = fileNames.SelectMany(fileName => File.ReadAllLines(fileName)).Where(x => length == null || x.Length == length).Distinct().ToList();
            string[][] ngrams = null;
            if (byWord)
            {
                var temp = new List<string[]>(passwords.Count);
                Parallel.For(0, passwords.Count, x =>
                {
                    ParseByWord(x, passwords, temp);
                });
                if (length == null)
                    ngrams = temp.GroupBy(x => x[0]).OrderByDescending(x => x.Count()).SelectMany(x => x).Take(1000000).ToArray();
                else
                    ngrams = temp.GroupBy(x => x[0]).OrderByDescending(x => x.Count()).SelectMany(x => x).ToArray();
            }
            else
            {
                ngrams = new string[passwords.Count][];

                Parallel.For(0, passwords.Count, x =>
                {
                    ParseByChar(x, passwords, ngrams);
                });

                if (length == null)
                    ngrams = ngrams.GroupBy(x => x[0]).OrderByDescending(x => x.Count()).SelectMany(x => x.Take(100000)).Take(10000000).ToArray();
                else
                    ngrams = ngrams.GroupBy(x => x[0]).OrderByDescending(x => x.Count()).SelectMany(x => x).ToArray();
            }
            return ngrams;
        }

        private static void ParseByWord(int index, List<string> pwds, List<string[]> ngrams)
        {
            var values = new List<List<string>>();
            int dos = 0;
            while (dos < 2)
            {
                var val = new List<string>();
                var transitionIndexes = new List<int>() { 0 };
                for (int i = 0; i < pwds[index].Length - 1; i++)
                {
                    if (CharType(pwds[index][i], dos == 1) != CharType(pwds[index][i + 1], dos == 1))
                        transitionIndexes.Add(i + 1);
                }

                for (int i = 0; i < transitionIndexes.Count; i++)
                {
                    if (i < transitionIndexes.Count - 1)
                        val.Add(pwds[index].Substring(transitionIndexes[i], transitionIndexes[i + 1] - transitionIndexes[i]));
                    else
                        val.Add(pwds[index].Substring(transitionIndexes[i]));
                }

                values.Add(val);

                dos++;
            }


            lock (ngrams)
            {
                ngrams.Add(values[0].ToArray());
                if (values[0].Count != values[1].Count)
                    ngrams.Add(values[1].ToArray());
            }

        }

        private static void ParseByChar(int index, List<string> pwds, string[][] ngrams)
        {
            var characters = new string[pwds[index].Length];
            for (int i = 0; i < pwds[index].Length; i++)
            {
                characters[i] = pwds[index].Substring(i, 1);
            }
            ngrams[index] = characters.ToArray();
        }

        private static int CharType(char c, bool ignoreCase = false)
        {
            return char.IsDigit(c) ? 0 : (!char.IsLetterOrDigit(c) ? 1 : (ignoreCase ? 2 : (char.IsUpper(c) ? 2 : 3)));
        }
    }
}
