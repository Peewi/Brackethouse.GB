using Brackethouse.GB;
using System.Text.Json;

namespace CPUTests
{
	public class UnitTest1
	{
		/// <summary>
		/// json tests from https://github.com/SingleStepTests/sm83
		/// </summary>
		[Theory]
		[FolderData("json\\")]
		public void CPUTest(string path)
		{
			Assert.True(Path.GetExtension(path) == ".json", "Non-JSON file.");
			var doc = JsonDocument.Parse(File.ReadAllText(path));
			var foo = doc.RootElement.EnumerateArray();
			foreach (JsonElement item in doc.RootElement.EnumerateArray())
			{
				SingleJsonTest(item);
			}
		}
		void SingleJsonTest(JsonElement jsonTest)
		{
			FlatMemory mem = new FlatMemory();
			// setup initial state
			string? name = jsonTest.GetProperty("name").GetString();
			JsonElement initial = jsonTest.GetProperty("initial");
			CPURegisterState initialState = CPURegisterState.FromJson(initial);
			CPU cpu = new CPU(mem, GameBoyType.GameBoy, initialState);
			foreach (JsonElement item in initial.GetProperty("ram").EnumerateArray())
			{
				int addr = item[0].GetInt32();
				int val = item[1].GetInt32();
				mem[addr] = (byte)val;
			}
			cpu.Step();
			// compare to final state
			JsonElement final = jsonTest.GetProperty("final");
			CPURegisterState finalState = CPURegisterState.FromJson(final);
			foreach (JsonElement item in final.GetProperty("ram").EnumerateArray())
			{
				int addr = item[0].GetInt32();
				int val = item[1].GetInt32();
				Assert.True(mem[addr] == val, $"Test {name}: Address {addr:X4}: expected {val:X2}, was {mem[addr]:X2}");
			}
			var finalActualState = cpu.GetState();
			Assert.True(finalActualState.Equals(finalState), $"Test {name}: register mismatch.");
		}
	}
}