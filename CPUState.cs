using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	/// <summary>
	/// For comparing register states in tests.
	/// </summary>
	public struct CPURegisterState
	{
		public ushort AF;
		public ushort BC;
		public ushort DE;
		public ushort HL;
		public ushort SP;
		public ushort PC;
		/// <summary>
		/// Read the CPU register state from JSON, as formatted in the Single Step Tests.
		/// </summary>
		/// <param name="json">Initial or final element from test JSON.</param>
		/// <returns>Values in struct</returns>
		public static CPURegisterState FromJson(JsonElement json)
		{
			int pc = json.GetProperty("pc").GetInt32();
			int sp = json.GetProperty("sp").GetInt32();
			int a = json.GetProperty("a").GetInt32();
			int b = json.GetProperty("b").GetInt32();
			int c = json.GetProperty("c").GetInt32();
			int d = json.GetProperty("d").GetInt32();
			int e = json.GetProperty("e").GetInt32();
			int f = json.GetProperty("f").GetInt32();
			int h = json.GetProperty("h").GetInt32();
			int l = json.GetProperty("l").GetInt32();

			ushort af = (ushort)((a << 8) + f);
			ushort bc = (ushort)((b << 8) + c);
			ushort de = (ushort)((d << 8) + e);
			ushort hl = (ushort)((h << 8) + l);
			return new CPURegisterState()
			{
				AF = af,
				BC = bc,
				DE = de,
				HL = hl,
				SP = (ushort)sp,
				PC = (ushort)pc,
			};
		}
	}
}
