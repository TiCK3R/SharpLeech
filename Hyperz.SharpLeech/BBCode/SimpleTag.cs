using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperz.SharpLeech.BBCode
{
    public class SimpleTag : ITag
    {
        public string Name
        {
            get;
            set;
        }

        public string Replacement
        {
            get;
            set;
        }

        #region ITag Members

        public string DoFormating(string vhod)
        {
            return vhod.Replace("[" + Name + "]", Replacement.Braces());
        }

        #endregion
    }
}
