using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
    class Cartridge
    {
		/// <summary>
		/// The different types of cartridge.
		/// Values correspond to byte in cartridge header.
		/// Values taken from https://gbdev.io/pandocs/The_Cartridge_Header.html#0147--cartridge-type
		/// </summary>
		enum CartridgeType
        {
			ROM_ONLY = 0x00,
			MBC1 = 0x01,
			MBC1_RAM = 0x02,
			MBC1_RAM_BATTERY = 0x03,
			MBC2 = 0x05,
			MBC2_BATTERY = 0x06,
			ROM_RAM = 0x08,
			ROM_RAM_BATTERY = 0x09,
			MMM01 = 0x0B,
			MMM01_RAM = 0x0C,
			MMM01_RAM_BATTERY = 0x0D,
			MBC3_TIMER_BATTERY = 0x0F,
			MBC3_TIMER_RAM_BATTERY = 0x10,
			MBC3 = 0x11,
			MBC3_RAM = 0x12,
			MBC3_RAM_BATTERY = 0x13,
			MBC5 = 0x19,
			MBC5_RAM = 0x1A,
			MBC5_RAM_BATTERY = 0x1B,
			MBC5_RUMBLE = 0x1C,
			MBC5_RUMBLE_RAM = 0x1D,
			MBC5_RUMBLE_RAM_BATTERY = 0x1E,
			MBC6 = 0x20,
			MBC7_SENSOR_RUMBLE_RAM_BATTERY = 0x22,
			POCKET_CAMERA = 0xFC,
			BANDAI_TAMA5 = 0xFD,
			HuC3 = 0xFE,
			HuC1_RAM_BATTERY = 0xFF,
		}
		const ushort TypeAddress = 0x0147;
		readonly CartridgeType Type;
		public readonly string Title = "";
        readonly byte[] ROM;
		int BankSelect = 1;
		delegate void WriteHandlerDelegate(ushort address, byte value);
		delegate byte ReadHandlerDelegate(ushort address);
		readonly WriteHandlerDelegate WriteHandler;
		readonly ReadHandlerDelegate ReadHandler;
		private Cartridge(byte[] rom)
		{
			WriteHandler = ROMOnlyWrite;
			ReadHandler = ROMOnlyRead;
			ROM = rom;
			Type = (CartridgeType)Read(TypeAddress);
			if (Type == CartridgeType.MBC1
				|| Type == CartridgeType.MBC1_RAM
				|| Type == CartridgeType.MBC1_RAM_BATTERY)
			{
				WriteHandler = MBC1Write;
				ReadHandler = MBC1Read;
			}
			else if (Type == CartridgeType.MBC2
				|| Type == CartridgeType.MBC2_BATTERY)
			{

			}
			else if (Type == CartridgeType.MBC3
				|| Type == CartridgeType.MBC3_RAM
				|| Type == CartridgeType.MBC3_RAM_BATTERY
				|| Type == CartridgeType.MBC3_TIMER_BATTERY
				|| Type == CartridgeType.MBC3_TIMER_RAM_BATTERY)
			{

			}
			else if (Type == CartridgeType.MBC5
				|| Type == CartridgeType.MBC5_RAM
				|| Type == CartridgeType.MBC5_RAM_BATTERY
				|| Type == CartridgeType.MBC5_RUMBLE
				|| Type == CartridgeType.MBC5_RUMBLE_RAM
				|| Type == CartridgeType.MBC5_RUMBLE_RAM_BATTERY)
			{

			}
			else if (Type == CartridgeType.MBC6)
			{

			}
			else if (Type == CartridgeType.MBC7_SENSOR_RUMBLE_RAM_BATTERY)
			{

			}
			for (int i = 0x0134; i < 0x0144; i++)
			{
				Title += (char)rom[i];
			}
		}
		/// <summary>
		/// Make a cartridge from a file.
		/// No checking is done to verify that this is actually a Game Boy ROM.
		/// </summary>
		/// <param name="path">File path.</param>
		/// <returns>Cartridge.</returns>
		public static Cartridge FromFile(string path)
		{
			return new Cartridge(File.ReadAllBytes(path));
		}
		/// <summary>
		/// Makes a cartridge full of zeroes
		/// </summary>
		/// <returns>Empty cartridge.</returns>
		public static Cartridge Empty()
		{
			return new Cartridge(new byte[ushort.MaxValue + 1]);
		}
		/// <summary>
		/// Read from the cartridge.
		/// </summary>
		/// <param name="address">Address to read from.</param>
		/// <returns>A byte from the cartridge.</returns>
		public byte Read(ushort address)
		{
			// TODO: Handle other cartridge types.
			return ReadHandler(address);
		}
		byte ROMOnlyRead(ushort address)
		{
			return ROM[address];
		}
		byte MBC1Read(ushort address)
		{
			const ushort bankSize = 0x4000;
			if (address < bankSize)
			{
				return ROM[address];
			}
			int bank = Math.Max(BankSelect, 1);
			int romAddress = address + (bankSize * (bank - 1));
			return ROM[romAddress];
		}
		/// <summary>
		/// Write to an address that belongs to the cartridge.
		/// </summary>
		/// <param name="address"></param>
		/// <param name="value"></param>
		public void Write(ushort address, byte value)
		{
			// TODO: Handle other cartridge types.
			WriteHandler(address, value);
		}
		/// <summary>
		/// What happens when a byte is written to a ROM address, which is nothing.
		/// </summary>
		void ROMOnlyWrite(ushort address, byte value)
		{

		}
		/// <summary>
		/// What the MBC1 does when a byte is written to a ROM address.
		/// This selects a ROM bank
		/// </summary>
		void MBC1Write(ushort address, byte value)
		{
			const ushort RAMEnableEnd = 0x1fff;
			const ushort ROMBankStart = 0x2000;
			const ushort ROMBankEnd = 0x3fff;
			if (address <= RAMEnableEnd)
			{
				//throw new NotImplementedException();
			}
			if (address >= ROMBankStart && address <= ROMBankEnd)
			{
				const byte fiveBitMask = 0b0001_1111;
				value &= fiveBitMask;
				BankSelect = value;
			}
		}
		static public bool AddressIsCartridge(ushort address)
		{
			return AddressIsROM(address) || AddressIsExternalRAM(address);
		}
		static public bool AddressIsROM(ushort address)
		{
			const ushort ROMStart = 0x0000;
			const ushort ROMBank1End = 0x3FFF;
			return address >= ROMStart && address <= ROMBank1End;
		}
		static public bool AddressIsExternalRAM(ushort address)
		{
			const ushort ExternalRAM = 0xA000;
			const ushort ExternalRAMEnd = 0xBFFF;
			return address >= ExternalRAM && address <= ExternalRAMEnd;
		}
	}
}
