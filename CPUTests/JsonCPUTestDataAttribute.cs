using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace CPUTests
{
	internal class JsonCPUTestDataAttribute : DataAttribute
	{
		string Path;

		public JsonCPUTestDataAttribute(string path)
		{
			Path = path;
		}

		public override IEnumerable<object[]> GetData(MethodInfo testMethod)
		{
			ArgumentNullException.ThrowIfNull(testMethod);
			if (!File.Exists(Path))
			{
				throw new ArgumentException($"Could not find file at path: {Path}");
			}
			JsonElement jroot = JsonDocument.Parse(File.ReadAllText(Path)).RootElement;
			int jLength = jroot.GetArrayLength();
			object[][] retval = new object[jLength][];
			for (int i = 0; i < jLength; i++)
			{
				retval[i] = [jroot[i]];
			}
			return retval;
		}
	}
}
