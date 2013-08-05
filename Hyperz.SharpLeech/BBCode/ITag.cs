using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Hyperz.SharpLeech.BBCode
{
    public interface ITag
    {
        string DoFormating(string vhod);
    }
}
