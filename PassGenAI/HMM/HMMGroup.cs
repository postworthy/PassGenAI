using Accord.Statistics.Filters;
using Accord.Statistics.Models.Markov;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassGenAI.HMM
{
    public class HMMGroup
    {
        public HiddenMarkovModel Model { get; set; }
        public Codification Codebook { get; set; }
        public int? Length { get; set; } = null;

        public HMMGroup Save(string fileName)
        {
            using (var strm = File.OpenWrite(fileName))
            using (var sw = new StreamWriter(strm))
            using (var mstrm = new MemoryStream())
            {
                Accord.IO.Serializer.Save(Model, out byte[] data1);
                var hmmBase64 = Convert.ToBase64String(data1);

                Accord.IO.Serializer.Save(Codebook, out byte[] data2);
                var codebookBase64 = Convert.ToBase64String(data2);

                sw.Write(JsonConvert.SerializeObject((hmmBase64, codebookBase64, Length)));
                sw.Flush();
            }
            return this;
        }

        public static HMMGroup Load(string fileName)
        {
            using (var strm = File.OpenRead(fileName))
            using (var sr = new StreamReader(strm))
            using (var mstrm = new MemoryStream())
            {
                var data = sr.ReadToEnd();
                (string, string, int?) hmmData;
                if (data.Contains("Item3"))
                    hmmData = JsonConvert.DeserializeObject<(string, string, int?)>(data);
                else
                {
                    var temp = JsonConvert.DeserializeObject<(string, string)>(data);
                    hmmData = (temp.Item1, temp.Item2, null);
                }

                return new HMMGroup()
                {
                    Model = Accord.IO.Serializer.Load<HiddenMarkovModel>(Convert.FromBase64String(hmmData.Item1)),
                    Codebook = Accord.IO.Serializer.Load<Codification>(Convert.FromBase64String(hmmData.Item2)),
                    Length = hmmData.Item3
                };
                
            }
        }

        public IEnumerable<string> Generate(int length = 0, long max = long.MaxValue)
        {
            length = length == 0 ? (Length ?? 8) : length;
            var column = Codebook.Columns[0].ColumnName;
            int[] sample = Model.Generate(Math.Max(Math.Min(length, 32), 1));
            string[] result = Codebook.Revert(column, sample);
            while (max-- >= 0)
            {
                yield return string.Join("", result);
                try
                {
                    sample = Model.Generate(Math.Max(Math.Min(length, 32), 1));
                    result = Codebook.Revert(column, sample);
                }
                catch { }
            }
        }
    }
}
