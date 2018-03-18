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
using PassGenAI.Core;

namespace PassGenAI.HMM
{
    public static class HMMUtilities
    {
        public enum SplitAlgorithm
        {
            Simple,
            ByWord
        }
        public static HMMGroup CreateHiddenMarkovModel(IEnumerable<string> fileNames, int? length = null, SplitAlgorithm algo = SplitAlgorithm.Simple, bool ignoreCase = false)
        {
            string[][] ngrams = null;
            switch (algo)
            {
                case SplitAlgorithm.Simple:
                    ngrams = GetNgramsSimple(fileNames, length ?? 8);
                    break;
                case SplitAlgorithm.ByWord:
                    ngrams = GetNgrams(fileNames, length, true, ignoreCase);
                    break;
            }

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
                            ngrams.Add(line.Select(x => x.ToString()).ToArray());
                        }
                    }
                }
            }
            return ngrams.ToArray();
        }

        private static string[][] GetNgrams(IEnumerable<string> fileNames, int? length = null, bool byWord = false, bool ignoreCase = false)
        {
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
                            if (!byWord && line.Length == length) passwords.Add(line);
                            else if (byWord && line.Length >= length) passwords.Add(line);
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
                    Common.ParseByWord(x, passwords, temp, ignoreCase, length ?? 0);
                });

                if (length == null)
                    ngrams = temp.GroupBy(x => x[0]).OrderByDescending(x => x.Count()).SelectMany(x => x).Take(1000000).ToArray();
                else
                    ngrams = temp.GroupBy(x => x[0]).OrderByDescending(x => x.Count()).SelectMany(x => x).Take(1000000).ToArray();
            }
            else
            {
                ngrams = new string[passwords.Count][];

                Parallel.For(0, passwords.Count, x =>
                {
                    Common.ParseByChar(x, passwords, ngrams);
                });

                if (length == null)
                    ngrams = ngrams.GroupBy(x => x[0]).OrderByDescending(x => x.Count()).SelectMany(x => x.Take(100000)).Take(10000000).ToArray();
                else
                    ngrams = ngrams.GroupBy(x => x[0]).OrderByDescending(x => x.Count()).SelectMany(x => x).ToArray();
            }
            return ngrams;
        }

        
    }
}
