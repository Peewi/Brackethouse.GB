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
		const ushort CGBFlagAddress = 0x0143;
		const ushort TypeAddress = 0x0147;
		const ushort ROMSizeAddress = 0x0148;
		const ushort RAMSizeAddress = 0x0149;
		const ushort ROMBankSize = 0x4000;
		const ushort RAMBankSize = 0x2000;
		const ushort ExternalRAMStartAddress = 0xA000;
		const ushort MBC2RAMSize = 512;
		readonly CartridgeType Type;
		readonly int ROMBankCount;
		readonly int RAMBankCount;
		public readonly string Title = "";
		public readonly GameBoyType CartType = GameBoyType.GameBoy;
		readonly byte[] ROM;
		readonly byte[] RAM;
		readonly bool Battery = false;
		int ROMBankSelect = 1;
		bool RAMEnable = false;
		int RAMBankSelect = 0;
		bool MBC1AdvancedMode = false;
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
			ROMBankCount = 2 << Read(ROMSizeAddress);
			// https://gbdev.io/pandocs/The_Cartridge_Header.html#0149--ram-size
			switch (Read(RAMSizeAddress))
			{
				case 0:
				case 1:
					RAMBankCount = 0;
					break;
				case 2:
					RAMBankCount = 1;
					break;
				case 3:
					RAMBankCount = 4;
					break;
				case 4:
					RAMBankCount = 16;
					break;
				case 5:
					RAMBankCount = 8;
					break;
				default:
					break;
			}
			RAM = new byte[RAMBankSize * RAMBankCount];
			if (Type == CartridgeType.MBC1
				|| Type == CartridgeType.MBC1_RAM
				|| Type == CartridgeType.MBC1_RAM_BATTERY)
			{
				WriteHandler = MBC1Write;
				ReadHandler = MBC1Read;
				Battery = Type == CartridgeType.MBC1_RAM_BATTERY;
			}
			else if (Type == CartridgeType.MBC2
				|| Type == CartridgeType.MBC2_BATTERY)
			{
				RAM = new byte[MBC2RAMSize];
				WriteHandler = MBC2Write;
				ReadHandler = MBC2Read;
				Battery = Type == CartridgeType.MBC2_BATTERY;
			}
			else if (Type == CartridgeType.MBC3
				|| Type == CartridgeType.MBC3_RAM
				|| Type == CartridgeType.MBC3_RAM_BATTERY
				|| Type == CartridgeType.MBC3_TIMER_BATTERY
				|| Type == CartridgeType.MBC3_TIMER_RAM_BATTERY)
			{
				WriteHandler = MBC3Write;
				ReadHandler = MBC3Read;
				Battery = Type == CartridgeType.MBC3_RAM_BATTERY
				|| Type == CartridgeType.MBC3_TIMER_BATTERY
				|| Type == CartridgeType.MBC3_TIMER_RAM_BATTERY;
			}
			else if (Type == CartridgeType.MBC5
				|| Type == CartridgeType.MBC5_RAM
				|| Type == CartridgeType.MBC5_RAM_BATTERY
				|| Type == CartridgeType.MBC5_RUMBLE
				|| Type == CartridgeType.MBC5_RUMBLE_RAM
				|| Type == CartridgeType.MBC5_RUMBLE_RAM_BATTERY)
			{
				WriteHandler = MBC5Write;
				ReadHandler = MBC5Read;
				Battery = Type == CartridgeType.MBC5_RAM_BATTERY
					|| Type == CartridgeType.MBC5_RUMBLE_RAM_BATTERY;
			}
			else if (Type == CartridgeType.MBC6)
			{
				throw new NotImplementedException();
			}
			else if (Type == CartridgeType.MBC7_SENSOR_RUMBLE_RAM_BATTERY)
			{
				Battery = true;
				throw new NotImplementedException();
			}
			for (int i = 0x0134; i < 0x0144; i++)
			{
				Title += (char)rom[i];
			}
			if ((rom[CGBFlagAddress] & 0x80) != 0)
			{
				CartType = GameBoyType.GameBoyColor;
			}
		}
		/// <summary>
		/// Make a cartridge from a file.
		/// No checking is done to verify that this is actually a Game Boy ROM.
		/// If the cartridge has RAM and a battery, and a .sav file of appropriate size exists next to the ROM file, RAM is initialized with the contents of the .sav file.
		/// </summary>
		/// <param name="romPath">File path.</param>
		/// <returns>Cartridge.</returns>
		public static Cartridge FromFile(string romPath, string savePath)
		{
			Cartridge cart = new(File.ReadAllBytes(romPath));
			if (cart.Battery && cart.RAM.Length > 0)
			{
				if (!File.Exists(savePath))
				{
					return cart;
				}
				long size = new FileInfo(savePath).Length;
				if (cart.RAM.Length != size)
				{
					return cart;
				}
				byte[] save = File.ReadAllBytes(savePath);
				for (int i = 0; i < cart.RAM.Length; i++)
				{
					cart.RAM[i] = save[i];
				}
			}
			return cart;
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
		/// Save the contents of cartridge RAM to a file on disk.
		/// </summary>
		/// <param name="savePath">Path to save to.</param>
		public void SaveRAM(string savePath)
		{
			if (RAM.Length == 0)
			{
				return;
			}
			if (!Battery)
			{
				return;
			}
			File.WriteAllBytes(savePath, RAM);
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
			if (AddressIsExternalRAM(address))
			{
				return 0xff;
			}
			return ROM[address];
		}
		byte MBC1Read(ushort address)
		{
			if (address < ROMBankSize)
			{
				int romAddress = address;
				if (MBC1AdvancedMode)
				{
					int bank = RAMBankSelect << 5;
					romAddress += ROMBankSize * bank;
				}
				return ROM[romAddress];
			}
			if (address < ROMBankSize * 2)
			{
				int bank = Math.Max(ROMBankSelect, 1);
				bank %= ROMBankCount;
				if (ROMBankCount > 32)
				{
					// There are more ROM banks than can be addressed with five bits.
					bank += RAMBankSelect << 5;
				}
				int romAddress = address + (ROMBankSize * (bank - 1));
				return ROM[romAddress];
			}
			if (AddressIsExternalRAM(address))
			{
				if (!RAMEnable || RAMBankCount == 0)
				{
					return 0xff;
				}
				int ramAddress = address - ExternalRAMStartAddress;
				if (MBC1AdvancedMode)
				{
					ramAddress += RAMBankSize * RAMBankSelect;
				}
				return RAM[ramAddress];
			}
			return 0xff;
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
			const ushort SecondaryBankStart = 0x4000;
			const ushort SecondaryBankEnd = 0x5fff;
			const ushort BankModeStart = 0x6000;
			const ushort BankModeEnd = 0x7fff;
			if (address <= RAMEnableEnd)
			{
				byte enableMask = 0x0a;
				RAMEnable = (value & enableMask) == enableMask;
				return;
			}
			if (address >= ROMBankStart && address <= ROMBankEnd)
			{
				const byte fiveBitMask = 0b0001_1111;
				value &= fiveBitMask;
				ROMBankSelect = value;
				return;
			}
			if (address >= SecondaryBankStart && address <= SecondaryBankEnd)
			{
				const byte twoBitMask = 0b0000_0011;
				value &= twoBitMask;
				RAMBankSelect = value;
				return;
			}
			if (address >= BankModeStart && address <= BankModeEnd)
			{
				const byte oneBitMask = 0b0000_0001;
				MBC1AdvancedMode = (value & oneBitMask) == oneBitMask;
				return;
			}
			if (AddressIsExternalRAM(address))
			{
				if (!RAMEnable || RAMBankCount == 0)
				{
					return;
				}
				int ramAddress = address - ExternalRAMStartAddress;
				if (RAMBankCount > 1 && MBC1AdvancedMode)
				{
					ramAddress += RAMBankSize * RAMBankSelect;
				}
				RAM[ramAddress] = value;
			}
		}
		static public bool AddressIsCartridge(ushort address)
		{
			return AddressIsROM(address) || AddressIsExternalRAM(address);
		}
		static public bool AddressIsROM(ushort address)
		{
			const ushort ROMStart = 0x0000;
			const ushort SwitchableROMBankEnd = 0x7FFF;
			return address >= ROMStart && address <= SwitchableROMBankEnd;
		}
		static public bool AddressIsExternalRAM(ushort address)
		{
			const ushort ExternalRAM = 0xA000;
			const ushort ExternalRAMEnd = 0xBFFF;
			return address >= ExternalRAM && address <= ExternalRAMEnd;
		}
		#region MBC2
		/// <summary>
		/// MBC2 cart read. ROM or RAM depending on address.
		/// </summary>
		/// <param name="address">Memory address</param>
		/// <returns>A value that was read</returns>
		byte MBC2Read(ushort address)
		{
			if (address < ROMBankSize)
			{
				return ROM[address];
			}
			if (address < ROMBankSize * 2)
			{
				int bank = Math.Max(ROMBankSelect, 1);
				bank %= ROMBankCount;
				int romAddress = address + (ROMBankSize * (bank - 1));
				return ROM[romAddress];
			}
			// If we got to here, surely it must be RAM.
			if (RAMEnable)
			{
				int ramAddr = address - ExternalRAMStartAddress;
				ramAddr %= MBC2RAMSize;
				const byte mbc2RAMMask = 0x0f;
				return (byte)(RAM[ramAddr] & mbc2RAMMask);
			}
			return 0xff;
		}
		/// <summary>
		/// MBC2 cart write. Bank select or RAM?
		/// </summary>
		/// <param name="address">Memory address</param>
		/// <param name="value">Value being written</param>
		void MBC2Write(ushort address, byte value)
		{
			if (address < ROMBankSize)
			{
				const ushort RAMFlagAddrMask = 0x0100;
				if ((address & RAMFlagAddrMask) == 0)
				{
					byte enableMask = 0x0a;
					RAMEnable = (value & enableMask) == enableMask;
					return;
				}
				const byte bankMask = 0x0f;
				ROMBankSelect = Math.Max(1, value & bankMask);
				return;
			}
			// If we got to here, surely it must be RAM.
			if (RAMEnable)
			{
				int ramAddr = address - ExternalRAMStartAddress;
				ramAddr %= MBC2RAMSize;
				const byte mbc2RAMMask = 0x0f;
				RAM[ramAddr] = (byte)(value & mbc2RAMMask);
			}
		}
		#endregion
		#region MBC3
		byte MBC3Read(ushort address)
		{
			if (address < ROMBankSize)
			{
				return ROM[address];
			}
			if (address < ROMBankSize * 2)
			{
				int bank = Math.Max(ROMBankSelect, 1);
				bank %= ROMBankCount;
				int romAddress = address + (ROMBankSize * (bank - 1));
				return ROM[romAddress];
			}
			// If we got to here, surely it must be RAM.
			if (RAMEnable && RAMBankSelect <= RAMBankCount)
			{
				int ramAddr = address - ExternalRAMStartAddress;
				ramAddr += RAMBankSize * RAMBankSelect;
				return RAM[ramAddr];
			}
			if (RAMEnable && RAMBankSelect > RAMBankCount)
			{
				// Clock registers
				// TODO
			}
			return 0xff;
		}

		void MBC3Write(ushort address, byte value)
		{
			const ushort RAMEnableEnd = 0x1fff;
			if (address <= RAMEnableEnd)
			{ // RAM AND timer enabling
				RAMEnable = value == 0x0a;
				return;
			}
			if (address < ROMBankSize)
			{
				const byte sevenBitMask = 0x7f;
				ROMBankSelect = Math.Max(1, value & sevenBitMask);
				return;
			}
			if (address <= 0x5fff)
			{
				// RAM bank
				RAMBankSelect = value;
				return;
			}
			if (address <= 0x7fff)
			{
				// Latch clock
				// TODO
			}
			if (address >= ExternalRAMStartAddress && address <= 0xbfff)
			{
				if (!RAMEnable || RAMBankCount == 0)
				{
					return;
				}
				if (ROMBankSelect > ROMBankCount)
				{
					// Clock registers
					// TODO
				}
				int ramAddr = address - ExternalRAMStartAddress;
				ramAddr += RAMBankSize * RAMBankSelect;
				RAM[ramAddr] = value;
			}
		}
		#endregion
		#region MBC5
		byte MBC5Read(ushort address)
		{
			if (address < ROMBankSize)
			{
				return ROM[address];
			}
			if (address < ROMBankSize * 2)
			{
				int bank = Math.Max(ROMBankSelect, 1);
				bank %= ROMBankCount;
				int romAddress = address + (ROMBankSize * (bank - 1));
				return ROM[romAddress];
			}
			// If we got to here, surely it must be RAM.
			if (RAMEnable && RAMBankSelect < RAMBankCount)
			{
				int ramAddr = address - ExternalRAMStartAddress;
				ramAddr += RAMBankSize * RAMBankSelect;
				return RAM[ramAddr];
			}
			return 0xff;
		}

		void MBC5Write(ushort address, byte value)
		{
			if (address < 0x2000)
			{
				byte enableMask = 0x0a;
				RAMEnable = (value & enableMask) == enableMask;
				return;
			}
			if (address < 0x3000)
			{
				// Set lower 8 bits of ROM bank select.
				ROMBankSelect &= 0xff00;
				ROMBankSelect |= value;
				return;
			}
			if (address < 0x4000)
			{
				// Set 9th bit of ROM bank select.
				ROMBankSelect &= 0xff;
				ROMBankSelect |= (value & 0x01) << 8;
				return;
			}
			if (address < 0x6000)
			{
				// Select RAM bank. Rumble not implemented.
				RAMBankSelect = value;
				return;
			}
			if (address >= ExternalRAMStartAddress && address <= 0xbfff)
			{
				if (!RAMEnable || RAMBankCount == 0)
				{
					return;
				}
				int ramAddr = address - ExternalRAMStartAddress;
				ramAddr += RAMBankSize * RAMBankSelect;
				RAM[ramAddr] = value;
			}
		}
		#endregion
	}
}
