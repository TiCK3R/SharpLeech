using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperz.SharpLeech.BBCode
{
    public class ParameterValueTag : EnclosingTag
    {
        public override string DoFormating(string vhod)
        {
            string startTagStart = "["+ Name + "=";
            string endTag = String.Format(endF, Name);
            int start = vhod.Length;
            while ((start = vhod.IndexOf(startTagStart)) >= 0)
            {
                int startTagLength = vhod.Substring(start).IndexOf(']') + 1;
                int parameterLength = startTagLength - 1 - startTagStart.Length;
                int parameterStart =start + startTagStart.Length;
                string parameter = vhod.Substring(parameterStart, parameterLength);
                int end = vhod.IndexOf(endTag);
                if (end > vhod.Length) throw new Exception("No closing tag for " + startTagStart);
                int valueStart = start + startTagLength;
                int valueEnd = end - valueStart;
                int elementStart = start;
                int elementEnd = end + endTag.Length;
                string content = vhod.Substring(valueStart, valueEnd);
                Dictionary<string, string> parametri = new Dictionary<string, string>();
                parametri.Add("parameter", parameter);
                parametri.Add("value", content);
                vhod =
                    vhod.Substring(0, elementStart) +
                    StartTag.Braces().NamedFormat(parametri) +
                    EndTag.Braces().NamedFormat(parametri) +
                    vhod.Substring(elementEnd);
            }

            return vhod;
        }
    }
}
