using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassGenAI.Masks
{
    public static class MaskGen
    {
        public static IEnumerable<(int Matches, ulong Difficulty, string Mask)> AnalyzePasswords(IEnumerable<string> fileNames, int? length = null)
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
            var masks = new List<(ulong, string)>();
            passwords.AsParallel().ForAll(pwd =>
            {
                var maskParts = pwd.ToList().Select(c => char.IsDigit(c) ? "?d" : (char.IsSymbol(c) ? "?s" : (char.IsUpper(c) ? "?u" : "?l"))).ToArray();
                var mask = string.Join("", maskParts);
                ulong difficulty = maskParts.Select(x => x == "?l" || x == "?u" ? (ulong)26 : (x == "?d" ? (ulong)10 : (ulong)33)).Aggregate((ulong)1, (x, y) => x * y);
                lock (masks)
                {
                    masks.Add((difficulty, mask));
                }
            });

            var grps = masks.GroupBy(x => x.Item2).OrderBy(x => x.First().Item1).ThenBy(x => x.Key.Length).ThenByDescending(x => x.Count());
            foreach (var grp in grps)
            {
                yield return (Matches: grp.Count(), Difficulty: grp.First().Item1, Mask: grp.Key);
            }
        }
    }
}
