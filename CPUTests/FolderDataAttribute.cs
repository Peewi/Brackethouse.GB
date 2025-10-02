using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace CPUTests
{
	internal class FolderDataAttribute : DataAttribute
	{
		string Path;

		public FolderDataAttribute(string path)
		{
			Path = path;
		}

		public override IEnumerable<object[]> GetData(MethodInfo testMethod)
		{
			ArgumentNullException.ThrowIfNull(testMethod);

			IEnumerable<string> files = Directory.EnumerateFiles(Path);
			int count = files.Count();
			object[][] retval = new object[count][];
			int i = 0;
			foreach (string item in files)
			{
				retval[i] = [item];
				i++;
			}
			return retval;
		}
	}
}
