namespace Brackethouse.GB
{
	/// <summary>
	/// Audio channel 4. Makes noise.
	/// </summary>
	class NoiseChannel : AudioChannel
	{
		IORegisters IO;
		readonly ushort StartAddress;
		public NoiseChannel(IORegisters io, ushort startAddr)
		{
			IO = io;
			StartAddress = startAddr;
		}
		public override void Step(ushort tick)
		{
			throw new NotImplementedException();
		}
	}
}
