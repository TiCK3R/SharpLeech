using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperz.SharpLeech.BBCode
{
    public class EnclosingTag : ITag
    {
        #region ITag Members

        public string StartTag { get; set; }
        public string EndTag { get; set; }
        public string Name { get; set; }

        protected const string startF = "[{0}]";
        protected const string endF = "[/{0}]";

        public virtual string DoFormating(string vhod)
        {
            return
                vhod
                .Replace(String.Format(startF, Name), StartTag.Braces())
                .Replace(String.Format(endF, Name), EndTag.Braces());
        }

        #endregion
    }
}
