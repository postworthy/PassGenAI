using PassGenAI.HMM;
using PassGenAI.Masks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassGenAI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args?.Length < 1) args = new[] { "pwds1" };

            if (!ValidateArgs(args))
            {
                Console.WriteLine("Invalid Arguments!");
                PrintUsage();
                return;
            }

            switch (args[0])
            {
                case "hmm":
                    TrainModels(args[1]);
                    break;
                case "hmm2":
                    TrainModels(args[1], byWord: true);
                    break;
                case "hmm3":
                    TrainModels(args[1], byPairs: true);
                    break;
                case "mask":
                    GenerateMasks(args[1], args[2]);
                    break;
                case "deepmask":
                    GenerateDeepMasks(args[1], args[2]);
                    break;
                case "pwds2":
                    if (args.Length == 1) args = new string[] { args[0], "", "" };
                    if (args.Length == 2) args = new string[] { args[0], args[1], "8" };
                    var length = int.TryParse(args[1], out var len) ? len : (int.TryParse(args[2], out len) ? (int?)len : null);
                    var path = File.Exists(args[1]) ? args[1] : (File.Exists(args[2]) ? args[2] : null);
                    GeneratePasswords(args[1], length);
                    break;
                case "walks":
                    GenerateWalks(args.Length == 1 ? 8 : int.Parse(args[1]), args.Any(x=>x.ToLower() == "simple"));
                    break;
                default:
                    GeneratePasswords();
                    break;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("1) Generate Passwords");
            Console.WriteLine("\tPassGenAI.exe pwds1");
            Console.WriteLine("\tPassGenAI.exe pwds2 <FileName> [PasswordLength (Default=8)]");
            Console.WriteLine("2) Train Hidden Markov Model (Charater Based)");
            Console.WriteLine("\tPassGenAI.exe hmm");
            Console.WriteLine("3) Train Hidden Markov Model (Word Based)");
            Console.WriteLine("\tPassGenAI.exe hmm2");
            Console.WriteLine("4) Train Hidden Markov Model (Pair Based)");
            Console.WriteLine("\tPassGenAI.exe hmm2");
            Console.WriteLine("5) Generate Masks");
            Console.WriteLine("\tPassGenAI.exe mask");
            Console.WriteLine("6) Generate Deep Masks");
            Console.WriteLine("\tPassGenAI.exe deepmask");
            Console.WriteLine("7) Generate Keyboard Walks");
            Console.WriteLine("\tPassGenAI.exe walks [WalkLength (Default=8)]");
        }

        private static bool ValidateArgs(string[] args)
        {
            try
            {
                if (args?.Length < 1) args = new[] { "" };

                switch (args[0])
                {
                    case "hmm":
                        return args.Length > 1 && File.Exists(args[1]);
                    case "hmm2":
                        return args.Length > 1 && File.Exists(args[1]);
                    case "hmm3":
                        return args.Length > 1 && File.Exists(args[1]);
                    case "mask":
                        return args.Length > 2 && File.Exists(args[1]);
                    case "deepmask":
                        return args.Length > 2 && File.Exists(args[1]);
                    case "pwds2":
                        return (args.Length == 2 && (File.Exists(args[1])) || int.TryParse(args[2], out var _)) || (args.Length == 3 && File.Exists(args[1]) && int.TryParse(args[2], out var _));
                    case "walks":
                        return args.Length == 1 || (args.Length > 1 && int.TryParse(args[1], out var _));
                    default:
                        return true;
                }
            }
            catch { return false; }
        }

        private static void GenerateWalks(int length, bool simple)
        {
            var walks = Keyboard.KeyboardWalks.CanOclWalk() ? Keyboard.KeyboardWalks.OclWalk(length, simple) : Keyboard.KeyboardWalks.Walk(length, simple);
            foreach (var walk in walks)
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

        static void GeneratePasswords(string path, int? length = null)
        {
            //foreach (var pwd in HMMGroup.Load(path).Generate(length))
            //{
            //    Console.WriteLine(pwd);
            //}

            var enumerables = new List<IEnumerator<string>>();
            int threadMax = Process.GetCurrentProcess().Threads.Count;
            var lengths = length == null ? new[] { 1, 2, 3, 4, 5 } : new[] { length.Value };
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

        static void TrainModels(string fileName = @"G:\68_linkedin_found_plain_password_only.txt", bool byWord = false, bool byPairs = false)
        {
            /*
             * This is an example of how you could train your own HMM for password guessing.
             * The included HMMs have been trained off of the clear text linked in password leaks.
             * 
             */

            if (byWord)
            {
                HMMUtilities.CreateHiddenMarkovModel(
                    new[] { fileName },
                    length: 4,
                    algo: HMMUtilities.SplitAlgorithm.ByWord,
                    ignoreCase: true).Save("hmm_words.data");

                
            }
            else if (byPairs)
            {
                HMMUtilities.CreateHiddenMarkovModel(
                    new[] { fileName },
                    length: 0,
                    algo: HMMUtilities.SplitAlgorithm.Pairs
                    ).Save("hmm_pairs.data");
            }
            else
            {
                HMMUtilities.CreateHiddenMarkovModel(
                            new[] { fileName },
                            0).Save("hmm.data");
                /*
                Parallel.For(7, 21, length =>
                {
                    //int length = 14;
                    HMMUtilities.CreateHiddenMarkovModel(
                            new[] { fileName },
                            length).Save("hmm_length_of_" + length + ".data");
                });
                */
            }
        }
    }
}
