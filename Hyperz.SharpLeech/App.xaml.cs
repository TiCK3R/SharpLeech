using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Hyperz.SharpLeech.Engine;

namespace Hyperz.SharpLeech
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
		private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			Type t = e.Exception.GetType();
			Object ex = Convert.ChangeType(e.Exception, t);
			String msg = String.Format(
				"Exception: \r\n{0}\r\n\r\nThread ID: {1}",
				t.InvokeMember("ToString", System.Reflection.BindingFlags.InvokeMethod, null, ex, new object[0]),
				e.Dispatcher.Thread.ManagedThreadId
			);

			ErrorLog.LogException(e.Exception);

			e.Handled = true;
			this.Shutdown(-1);
		}
	}
}
