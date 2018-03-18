using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassGenAI.Core
{
    public static class Common
    {
        public static void ParseByWord(int index, List<string> pwds, List<string[]> ngrams)
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

        public static void ParseByWord(int index, List<string> pwds, List<string[]> ngrams, bool ignoreCase, int minlength = 0)
        {
            var val = new List<string>();
            var transitionIndexes = new List<int>() { 0 };
            for (int i = 0; i < pwds[index].Length - 1; i++)
            {
                if (CharType(pwds[index][i], ignoreCase) != CharType(pwds[index][i + 1], ignoreCase))
                    transitionIndexes.Add(i + 1);
            }

            for (int i = 0; i < transitionIndexes.Count; i++)
            {
                if (i < transitionIndexes.Count - 1)
                    val.Add(pwds[index].Substring(transitionIndexes[i], transitionIndexes[i + 1] - transitionIndexes[i]));
                else
                    val.Add(pwds[index].Substring(transitionIndexes[i]));
            }

            if(minlength > 0)
            {
                val = val.Where(x => x.Length >= minlength).ToList();
            }

            val = val.Where(y => !y.ToLower().StartsWith("link")).ToList();

            if (val.Count != 0)
            {
                lock (ngrams)
                {
                    ngrams.Add(val.ToArray());
                }
            }

        }

        public static void ParseByChar(int index, List<string> pwds, string[][] ngrams)
        {
            var characters = new string[pwds[index].Length];
            for (int i = 0; i < pwds[index].Length; i++)
            {
                characters[i] = pwds[index].Substring(i, 1);
            }
            ngrams[index] = characters.ToArray();
        }

        public static int CharType(char c, bool ignoreCase = false)
        {
            return char.IsDigit(c) ? 0 : (!char.IsLetterOrDigit(c) ? 1 : (ignoreCase ? 2 : (char.IsUpper(c) ? 2 : 3)));
        }
    }
}
