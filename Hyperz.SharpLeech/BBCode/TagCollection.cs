using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperz.SharpLeech.BBCode
{
    public class TagCollection:List<ITag>
    {
        public new void Add(ITag tag)
        {
            base.Add(tag);
        }
    }
}
