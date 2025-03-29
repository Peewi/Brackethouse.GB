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
        byte[] ROM;
		private Cartridge(byte[] rom)
		{
			ROM = rom;
			Type = (CartridgeType)Read(TypeAddress);
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
			return ROM[address];
		}
		/// <summary>
		/// Write to an address that belongs to the cartridge.
		/// </summary>
		/// <param name="address"></param>
		/// <param name="value"></param>
		public void Write(ushort address, byte value)
		{
			// TODO: Handle other cartridge types.
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
