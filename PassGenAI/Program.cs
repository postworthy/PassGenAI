using PassGenAI.HMM;
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

            var locker = new object();
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

            return;

            /*
             * This is an example of how you could train your own HMM for password guessing.
             * The included HMMs have been trained off of the clear text linked in password leaks
             * 
             */

            Parallel.For(7, 21, length =>
            {
                //int length = 14;
                HMMUtilities.CreateHiddenMarkovModel(new[] {
                    @"G:\68_linkedin_found_plain_password_only.txt"
                }, length).Save("hmm_length_of_" + length + ".data");
            });
        }
    }
}
