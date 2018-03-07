using Accord.Statistics.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassGenAI.HMM
{
    public static class FastCodification
    {
        public static int[][] ParallelTransform(this Codification c, string columnName, string[][] values)
        {
            int[][] result = new int[values.Length][];
            Parallel.For(0, values.Length, x => {
                result[x] = c.Columns[columnName].Transform(values[x]);
            });
            return result;
        }

    }
}
