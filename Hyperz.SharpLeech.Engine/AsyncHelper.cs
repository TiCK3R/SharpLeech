using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperz.SharpLeech.Engine
{
	public sealed class AsyncHelper
	{
		/*public static void Run(Action[] actions)
		{
			var po = new ParallelOptions() { MaxDegreeOfParallelism = 4 };
			Parallel.Invoke(po, actions);
		}*/

		public static void Run(Action action)
		{
			action.BeginInvoke(action.EndInvoke, null);
		}
	}
}
