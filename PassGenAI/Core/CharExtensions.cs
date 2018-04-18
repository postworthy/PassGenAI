using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassGenAI.Core
{
    public static class CharExtensions
    {
        private static char[] vowels = new[] { 'a', 'e', 'i', 'o', 'u', 'A', 'E', 'I', 'O', 'U' };
        public static bool IsVowel(this char c)
        {
            return vowels.Contains(c);
        }
    }
}
