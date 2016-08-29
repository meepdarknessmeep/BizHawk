﻿//http://nesdev.parodius.com/bbs/viewtopic.php?p=4571&sid=db4c7e35316cc5d734606dd02f11dccb

using System;
using System.Runtime.CompilerServices;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Nintendo.NES
{
	public sealed partial class PPU
	{
		// this only handles region differences within the PPU
		int preNMIlines;
		int postNMIlines;
		bool chopdot;
		public enum Region { NTSC, PAL, Dendy, RGB };
		Region _region;
		public Region region { set { _region = value; SyncRegion(); } get { return _region; } }
		void SyncRegion()
		{
			switch (region)
			{
				case Region.NTSC:
					preNMIlines = 1; postNMIlines = 20; chopdot = true; break;
				case Region.PAL:
					preNMIlines = 1; postNMIlines = 70; chopdot = false; break;
				case Region.Dendy:
					preNMIlines = 51; postNMIlines = 20; chopdot = false; break;
				case Region.RGB:
					preNMIlines = 1; postNMIlines = 20; chopdot = false; break;
			}
		}

		public class DebugCallback
		{
			public int Scanline;
			//public int Dot; //not supported
			public Action Callback;
		}

		public DebugCallback NTViewCallback;
		public DebugCallback PPUViewCallback;

		// true = light sensed
		public bool LightGunCallback(int x, int y)
		{
			// the actual light gun circuit is very complex
			// and this doesn't do it justice at all, as expected

			const int radius = 10; // look at pixel values up to this far away, roughly

			int sum = 0;
			int ymin = Math.Max(Math.Max(y - radius, ppur.status.sl - 20), 0);
			int ymax = Math.Min(y + radius, 239);
			int xmin = Math.Max(x - radius, 0);
			int xmax = Math.Min(x + radius, 255);

			int ystop = ppur.status.sl - 2;
			int xstop = ppur.status.cycle - 20;

			for (int j = ymin; j <= ymax; j++)
			{
				for (int i = xmin; i <= xmax; i++)
				{
					if (j >= ystop && i >= xstop || j > ystop)
						goto loopout;

					short s = xbuf[j * 256 + i];
					int lum = s & 0x30;
					if ((s & 0x0f) >= 0x0e)
						lum = 0;
					sum += lum;
				}
			}
			loopout:
			return sum >= 2000;
		}


		//when the ppu issues a write it goes through here and into the game board
		public void ppubus_write(int addr, byte value)
		{
			nes.Board.AddressPPU(addr);
			nes.Board.WritePPU(addr, value);
		}

		//when the ppu issues a read it goes through here and into the game board
		public byte ppubus_read(int addr, bool ppu)
		{
			//hardware doesnt touch the bus when the PPU is disabled
			if (!reg_2001.PPUON && ppu)
				return 0xFF;

			nes.Board.AddressPPU(addr);
			return nes.Board.ReadPPU(addr);
		}

		//debug tools peek into the ppu through this
		public byte ppubus_peek(int addr)
		{
			return nes.Board.PeekPPU(addr);
		}

		public enum PPUPHASE
		{
			VBL, BG, OBJ
		};
		public PPUPHASE ppuphase;

		private readonly NES nes;
		public PPU(NES nes)
		{
			this.nes = nes;

			OAM = new byte[0x100];
			PALRAM = new byte[0x20];

			//power-up palette verified by blargg's power_up_palette test.
			//he speculates that these may differ depending on the system tested..
			//and I don't see why the ppu would waste any effort setting these.. 
			//but for the sake of uniformity, we'll do it.
			Array.Copy(new byte[] {
				0x09,0x01,0x00,0x01,0x00,0x02,0x02,0x0D,0x08,0x10,0x08,0x24,0x00,0x00,0x04,0x2C,
				0x09,0x01,0x34,0x03,0x00,0x04,0x00,0x14,0x08,0x3A,0x00,0x02,0x00,0x20,0x2C,0x08
			}, PALRAM, 0x20);

			Reset();
		}

		public void NESSoftReset()
		{
			//this hasn't been brought up to date since NEShawk was first made.
			//in particular http://wiki.nesdev.com/w/index.php/PPU_power_up_state should be studied, but theres no use til theres test cases
			Reset();
		}

		//state
		int ppudead; //measured in frames
		bool idleSynch;
		int NMI_PendingInstructions;
		byte PPUGenLatch;
		bool vtoggle;
		byte VRAMBuffer;
		public byte[] OAM;
		public byte[] PALRAM;

		public void SyncState(Serializer ser)
		{
			ser.Sync("ppudead", ref ppudead);
			ser.Sync("idleSynch", ref idleSynch);
			ser.Sync("NMI_PendingInstructions", ref NMI_PendingInstructions);
			ser.Sync("PPUGenLatch", ref PPUGenLatch);
			ser.Sync("vtoggle", ref vtoggle);
			ser.Sync("VRAMBuffer", ref VRAMBuffer);
			ser.Sync("ppu_addr_temp", ref ppu_addr_temp);

            ser.Sync("Read_Value", ref read_value);
            ser.Sync("Prev_soam_index", ref soam_index_prev);
            ser.Sync("Spr_Zero_Go", ref sprite_zero_go);
            ser.Sync("Spr_zero_in_Range", ref sprite_zero_in_range);
			ser.Sync("Is_even_cycle", ref is_even_cycle);
			ser.Sync("soam_index", ref soam_index);

			ser.Sync("ppu_open_bus", ref ppu_open_bus);
			ser.Sync("double_2007_read", ref double_2007_read);
			ser.Sync("ppu_open_bus_decay_timer", ref ppu_open_bus_decay_timer, false);

			ser.Sync("OAM", ref OAM, false);
			ser.Sync("PALRAM", ref PALRAM, false);

			ser.Sync("Reg2002_objoverflow", ref Reg2002_objoverflow);
			ser.Sync("Reg2002_objhit", ref Reg2002_objhit);
			ser.Sync("Reg2002_vblank_active", ref Reg2002_vblank_active);
			ser.Sync("Reg2002_vblank_active_pending", ref Reg2002_vblank_active_pending);
			ser.Sync("Reg2002_vblank_clear_pending", ref Reg2002_vblank_clear_pending);
			ppur.SyncState(ser);
			byte temp8 = reg_2000.Value; ser.Sync("reg_2000.Value", ref temp8); reg_2000.Value = temp8;
			temp8 = reg_2001.Value; ser.Sync("reg_2001.Value", ref temp8); reg_2001.Value = temp8;
			ser.Sync("reg_2003", ref reg_2003);

			//don't sync framebuffer into binary (rewind) states
			if(ser.IsText)
				ser.Sync("xbuf", ref xbuf, false);
		}

		public void Reset()
		{
			regs_reset();
			ppudead = 2;
			idleSynch = true;
			ppu_open_bus = 0;
			ppu_open_bus_decay_timer = new int[8];
		}

#if VS2012
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		void TriggerNMI()
		{
			nes.cpu.NMI = true;
		}

		//this gets called once after each cpu instruction executes.
		//anything that needs to happen at instruction granularity can get checked here
		//to save having to check it at ppu cycle granularity
		public void PostCpuInstructionOne()
		{
			if (NMI_PendingInstructions > 0)
			{
				NMI_PendingInstructions--;
				if (NMI_PendingInstructions <= 0)
				{
					TriggerNMI();
				}
			}
		}

#if VS2012
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		void runppu(int x)
		{
			//run one ppu cycle at a time so we can interact with the ppu and clockPPU at high granularity
			for (int i = 0; i < x; i++)
			{
				ppur.status.cycle++;
                is_even_cycle = !is_even_cycle;
				//might not actually run a cpu cycle if there are none to be run right now
				nes.RunCpuOne();

				if (Reg2002_vblank_active_pending)
				{
					//if (Reg2002_vblank_active_pending)
						Reg2002_vblank_active = 1;
					Reg2002_vblank_active_pending = false;
				}

				if (Reg2002_vblank_clear_pending)
				{
					Reg2002_vblank_active = 0;
					Reg2002_vblank_clear_pending = false;
				}

				nes.Board.ClockPPU();
			}
		}

		//hack
		//public bool PAL = false;
		//bool SPRITELIMIT = true;
	
	}
}
