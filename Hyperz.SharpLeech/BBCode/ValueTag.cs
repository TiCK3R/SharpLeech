using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;

namespace Hyperz.SharpLeech.BBCode
{
    public class ValueTag : EnclosingTag
    {
        public override string DoFormating(string vhod)
        {
            string startTag = String.Format(startF,Name);
            string endTag = String.Format(endF,Name);
            int start = vhod.Length;
            while ((start = vhod.IndexOf(startTag)) >= 0)
            {
                int end = vhod.IndexOf(endTag);
                if (end > vhod.Length) throw new Exception("No closing tag for " + startTag);
                int valueStart =start + startTag.Length;
                int valueEnd = end - valueStart;
                int elementStart = start;
                int elementEnd = end + endTag.Length;
                string content = vhod.Substring(valueStart, valueEnd);

                vhod =
                    vhod.Substring(0, elementStart) +
                    StartTag.Replace("$value", content).Braces() +
                    EndTag.Replace("$value", content).Braces() +
                    vhod.Substring(elementEnd);
            }

            return vhod;
        }
    }
}
