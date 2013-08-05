using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperz.SharpLeech.BBCode
{
    public class ParametrizedTag : EnclosingTag
    {
        public override string DoFormating(string vhod)
        {
            string sts = "[" + Name;
            int stsIndex = -1;
            string endTag = String.Format(endF, Name);
            while ((stsIndex = vhod.IndexOf(sts)) >= 0)
            {
                string formatedPart = vhod.Substring(0, stsIndex);
                string unformatedPart = vhod.Substring(stsIndex);
                int steIndex = unformatedPart.IndexOf("]");
                int endTagIndex = unformatedPart.IndexOf(endTag);
                string paramString = unformatedPart.Substring(sts.Length, steIndex - sts.Length - 1);
                if (String.IsNullOrEmpty(paramString)) throw new Exception("Ni parametrov");
                Dictionary<string, string> parametri = new Dictionary<string, string>();
                foreach (string kvp in paramString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parameter = kvp.Split('=');
                    if (parameter.Length < 2) throw new Exception("Invalid parameter format: " + kvp);
                    if (parameter[0].CompareTo("content") == 0) throw new Exception("'content' can not be used as a parameter!");
                    parametri.Add(parameter[0], parameter[1]);
                }
                parametri.Add("content", unformatedPart.Substring(steIndex + 1, endTagIndex - steIndex - 1));
                string newEnd = EndTag.Braces();
                vhod =
                    formatedPart +
                    StartTag.Braces().NamedFormat(parametri) +
                    EndTag.Braces().NamedFormat(parametri) +
                    unformatedPart.Substring(endTagIndex + endTag.Length);

            }
            return vhod;
        }

    }
}
