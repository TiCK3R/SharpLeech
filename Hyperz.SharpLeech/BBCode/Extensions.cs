using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperz.SharpLeech.BBCode
{
    static class Extensions
    {
        public static string Braces(this string me)
        {
            return me.Replace("[[", "<").Replace("]]", ">");
        }
        public static string NamedFormat(this string me, Dictionary<string, string> parametri)
        {
            foreach (var kvp in parametri)
                me = me.Replace("$" + kvp.Key, kvp.Value);
            return me;
        }
    }
}
