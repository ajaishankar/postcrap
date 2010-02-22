using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Cecil;

namespace PostCrap
{
	public class Program
	{
		static void Main(string[] args)
		{
			if (args.Length != 2)
			{
				Console.Error.WriteLine("usage: postcrap [source assembly path] [target path]");
				return;
			}

			CodeInjector.ProcessAssembly(args[0], args[1]);
		}
	}
}
