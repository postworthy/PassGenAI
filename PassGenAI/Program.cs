using PassGenAI.HMM;
using PassGenAI.Masks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassGenAI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args?.Length < 1) args = new[] { "" };

            switch (args[0])
            {
                case "hmm":
                    TrainModels(args.Length > 1 ? args[1] : @"G:\68_linkedin_found_plain_password_only.txt");
                    break;
                case "hmm2":
                    TrainModels(args.Length > 1 ? args[1] : @"G:\68_linkedin_found_plain_password_only.txt", true);
                    break;
                case "mask":
                    GenerateMasks(args.Length > 1 ? args[1] : @"G:\68_linkedin_found_plain_password_only.txt", args.Length > 2 ? args[2] : "{1:00000}\t{2}\t{0}");
                    break;
                case "deepmask":
                    GenerateDeepMasks(args.Length > 1 ? args[1] : @"G:\68_linkedin_found_plain_password_only.txt", args.Length > 2 ? args[2] : "{1:00000}\t{2}\t{0}");
                    break;
                case "pwds":
                    GeneratePasswords(args.Length > 1 ? args[1] : @"TrainedModels/hmm_words_v2.data", args.Length > 2 ? int.Parse(args[2]) : 4);
                    break;
                case "walks":
                    GenerateWalks(args.Length > 1 ? int.Parse(args[1]) : 8);
                    break;
                default:
                    GeneratePasswords();
                    break;
            }
        }

        private static void GenerateWalks(int length)
        {
            var walks = Keyboard.KeyboardWalks.Walk(length);
            foreach(var walk in walks)
            {
                Console.WriteLine(walk);
            }
        }

        static void GeneratePasswords()
        {
            var enumerables = new List<IEnumerator<string>>();
            int threadMax = Process.GetCurrentProcess().Threads.Count;
            var lengths = new[] { 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
            var split = (int)Math.Ceiling(lengths.Length / (threadMax * 1.0));
            foreach (var l in lengths)
            {
                enumerables.Add(HMMGroup.Load(string.Format("TrainedModels/hmm_length_of_{0}.data", l)).Generate(l).GetEnumerator());
            }

            Parallel.For(0, threadMax, x =>
            {
                int start = x * split;
                int end = Math.Min(start + split, lengths.Length);
                var items = enumerables.Skip(start).Take(split).ToList();
                int running = items.Count;
                do
                {
                    items.ForEach(i =>
                    {
                        var current = i.Current;
                        Console.WriteLine(current);
                        i.MoveNext();
                        if (current == i.Current) running--;
                    });
                } while (running > 0);
            });
        }

        static void GeneratePasswords(string path, int length = 4)
        {
            //foreach (var pwd in HMMGroup.Load(path).Generate(length))
            //{
            //    Console.WriteLine(pwd);
            //}

            var enumerables = new List<IEnumerator<string>>();
            int threadMax = Process.GetCurrentProcess().Threads.Count;
            var lengths = new[] { 1, 2, 3, 4, 5 };
            var split = (int)Math.Ceiling(lengths.Length / (threadMax * 1.0));
            foreach (var l in lengths)
            {
                enumerables.Add(HMMGroup.Load(path).Generate(l).GetEnumerator());
            }

            Parallel.For(0, threadMax, x =>
            {
                int start = x * split;
                int end = Math.Min(start + split, lengths.Length);
                var items = enumerables.Skip(start).Take(split).ToList();
                int running = items.Count;
                do
                {
                    items.ForEach(i =>
                    {
                        var current = i.Current;
                        Console.WriteLine(current);
                        i.MoveNext();
                        if (current == i.Current) running--;
                    });
                } while (running > 0);
            });

        }

        static void GenerateMasks(string fileName = @"G:\68_linkedin_found_plain_password_only.txt", string outputFormat = "{1:00000}\t{2}\t{0}")
        {
            /*
             * This is an example of how you could possibly analyze a password list to create a 
             * collection of masks for password cracking with Hashcat.
             * 
             */

            var masks = MaskGen.AnalyzePasswords(new[] {
                fileName
            });

            var avg = masks.Average(x => (long)x.Matches);

            //Special filtering just for my purposes...
            var filtered = masks
                .Where(x => x.Mask.Length > 7 * 2)
                .Where(x => x.Matches > avg * 2)
                .Where(x => x.Mask.Contains("u") && x.Mask.Contains("l") && x.Mask.Contains("d"))
                .ToList();

            masks = null;

            foreach (var mask in filtered)
            {
                Console.WriteLine(string.Format(outputFormat, mask.Mask, mask.Matches, mask.Difficulty));
            }
        }

        static void GenerateDeepMasks(string fileName = @"G:\68_linkedin_found_plain_password_only.txt", string outputFormat = "{1:00000}\t{2}\t{0}")
        {
            /*
             * This is an example of how you could possibly analyze a password list to create a 
             * collection of masks for password cracking with Hashcat.
             * 
             */

            var masks = MaskGen.AnalyzePasswords(new[] {
                fileName
            }, deep: true);

            //var avg = masks.Average(x => (long)x.Matches);

            //Special filtering just for my purposes...

            var filtered = masks
                .OrderBy(x => x.Difficulty)
                //.Where(x => x.Mask.Length > 7 * 2)
                //.Where(x => x.Matches > avg * 2)
                //.Where(x => x.Mask.Contains("u") && x.Mask.Contains("l") && x.Mask.Contains("d"))
                .ToList();

            masks = null;

            foreach (var mask in filtered)
            {
                Console.WriteLine(string.Format(outputFormat, mask.Mask, mask.Matches, mask.Difficulty));
            }
        }

        static void TrainModels(string fileName = @"G:\68_linkedin_found_plain_password_only.txt", bool byWord = false)
        {
            /*
             * This is an example of how you could train your own HMM for password guessing.
             * The included HMMs have been trained off of the clear text linked in password leaks.
             * 
             */

            if (!byWord)
            {
                Parallel.For(7, 21, length =>
                {
                    //int length = 14;
                    HMMUtilities.CreateHiddenMarkovModel(
                            new[] { fileName },
                            length).Save("hmm_length_of_" + length + ".data");
                });
            }
            else
            {
                HMMUtilities.CreateHiddenMarkovModel(
                    new[] { fileName },
                    length: 4,
                    algo: HMMUtilities.SplitAlgorithm.ByWord,
                    ignoreCase: true).Save("hmm_words.data");
            }
        }
    }
}
