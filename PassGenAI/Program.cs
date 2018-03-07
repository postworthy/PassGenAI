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

            switch(args[0])
            {
                case "hmm":
                    TrainModels(args.Length > 1 ? args[1] : null);
                    break;
                case "mask":
                    GenerateMasks(args.Length > 1 ? args[1] : null, args.Length > 2 ? args[2] : null);
                    break;
                default:
                    GeneratePasswords();
                    break;
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

        static void GenerateMasks(string fileName = @"G:\68_linkedin_found_plain_password_only.txt", string outputFormat = "{1:00000}\t{2}\t{0}")
        {
            /*
             * This is an example of how you could possibly analyze a password list to create a 
             * collection of masks for password cracking with Hashcat.
             * 
             */

            var masks = MaskGen.AnalyzePasswords(new[] {
                fileName
            }).Where(x=>x.Mask.Length > 0);

            foreach (var mask in masks)
            {
                Console.WriteLine(string.Format(outputFormat, mask.Mask, mask.Matches, mask.Difficulty));
            }
        }

        static void TrainModels(string fileName = @"G:\68_linkedin_found_plain_password_only.txt")
        {
            /*
             * This is an example of how you could train your own HMM for password guessing.
             * The included HMMs have been trained off of the clear text linked in password leaks.
             * 
             */

            Parallel.For(7, 21, length =>
            {
                //int length = 14;
                HMMUtilities.CreateHiddenMarkovModel(new[] {
                    fileName
                }, length).Save("hmm_length_of_" + length + ".data");
            });
        }
    }
}
