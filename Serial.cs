using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	/// <summary>
	/// A dummy implementation of the serial port, meant to mimic nothing being connected.
	/// </summary>
	class Serial
	{
		const ushort SerialDataAddress = 0xff01;
		const ushort SerialControlAddress = 0xff02;
		IORegisters IO;
		int TransferTicks = 0;
		bool TransferInProgress = false;
		const int TicksPerBitTransferred = 512;
		byte BitsTransferred = 0;
		public Serial(IORegisters io)
		{
			IO = io;
		}

		public void Step(ushort ticks)
		{
			const byte TransferStartMask = 0x81;
			bool startTransfer = (IO[SerialControlAddress] & TransferStartMask) == TransferStartMask;
			if (!TransferInProgress && startTransfer)
			{
				TransferInProgress = true;
				TransferTicks = 0;
				BitsTransferred = 0;
			}
			if (TransferInProgress)
			{
				TransferTicks += ticks;
				bool transfer = TransferTicks >= TicksPerBitTransferred;
				if (transfer)
				{
					TransferTicks -= TicksPerBitTransferred;
					BitsTransferred++;
					IO[SerialDataAddress] <<= 1;
					IO[SerialDataAddress] |= 1;
					if (BitsTransferred == 8)
					{
						TransferDone();
					}
				}
			}
		}
		void TransferDone()
		{
			TransferInProgress = false;
			IO[SerialControlAddress] &= 0x7f;
			const int SerialInterruptBit = 0x08;
			IO[IORegisters.InterruptFlagAddress] |= SerialInterruptBit;
		}
	}
}
