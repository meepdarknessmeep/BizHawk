﻿using System;
using System.Collections.Generic;
namespace BizHawk.Client.EmuHawk
{
	public partial class TAStudio : IControlMainform
	{
		private bool _suppressAskSave = false;

		public bool WantsToControlSavestates { get { return true; } }

		public void SaveState()
		{
			BookMarkControl.UpdateBranchExternal();
		}
		public void LoadState()
		{
			BookMarkControl.LoadBranchExternal();
		}
		public void SaveStateAs()
		{
			// dummy
		}
		public void LoadStateAs()
		{
			// dummy
		}
		public void SaveQuickSave(int slot)
		{
			BookMarkControl.UpdateBranchExternal(slot);
		}
		public void LoadQuickSave(int slot)
		{
			BookMarkControl.LoadBranchExternal(slot);
		}
		public void SelectSlot(int slot)
		{
			BookMarkControl.SelectBranchExternal(slot);
		}
		public void PreviousSlot()
		{
			BookMarkControl.SelectBranchExternal(false);
		}
		public void NextSlot()
		{
			BookMarkControl.SelectBranchExternal(true);
		}

		public bool WantsToControlReadOnly { get { return true; } }

		public void ToggleReadOnly()
		{
			if (CurrentTasMovie.IsPlaying)
			{
				TastudioRecordMode();
			}
			else if (CurrentTasMovie.IsRecording)
			{
				TastudioPlayMode();
			}
		}

		public bool WantsToControlStopMovie { get; private set; }

		public void StopMovie(bool supressSave)
		{
			this.Focus();
			_suppressAskSave = supressSave;
			NewTasMenuItem_Click(null, null);
			_suppressAskSave = false;
		}

		public bool WantsToControlRewind { get { return true; } }

		public void CaptureRewind()
		{
			// Do nothing, Tastudio handles this just fine
		}

		public bool Rewind()
		{
			//if (GlobalWin.MainForm.IsSeeking)
			//{
			//	GlobalWin.MainForm.PauseOnFrame--;
			//	if (Emulator.Frame == GlobalWin.MainForm.PauseOnFrame)
			//	{
			//		GlobalWin.MainForm.PauseEmulator();
			//		GlobalWin.MainForm.PauseOnFrame = null;
			//		StopSeeking();
			//	}
			//	RefreshDialog();
			//}
			//else
			//{
				GoToPreviousFrame();
			//}
			return true;
		}

		public bool WantsToControlRestartMovie { get; private set; }

		public void RestartMovie()
		{
			if (AskSaveChanges())
			{
				WantsToControlStopMovie = false;
				StartNewMovieWrapper(false);
				WantsToControlStopMovie = true;
				RefreshDialog();
			}
		}
	}
}
