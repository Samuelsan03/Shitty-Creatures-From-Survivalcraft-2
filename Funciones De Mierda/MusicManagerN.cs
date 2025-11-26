using System;
using Engine;
using Engine.Audio;
using Engine.Media;
using Game;

namespace Game
{
	// Token: 0x020000C1 RID: 193
	public static class MusicManagerN
	{
		// Token: 0x17000084 RID: 132
		// (get) Token: 0x060005C5 RID: 1477 RVA: 0x00024192 File Offset: 0x00022392
		public static bool IsPlaying
		{
			get
			{
				return MusicManagerN._mSound != null && MusicManagerN._mSound.State > 0;
			}
		}

		// Token: 0x17000085 RID: 133
		// (get) Token: 0x060005C6 RID: 1478 RVA: 0x000241AA File Offset: 0x000223AA
		public static float Volume
		{
			get
			{
				return SettingsManager.MusicVolume * 2f;
			}
		}

		// Token: 0x060005C7 RID: 1479 RVA: 0x000241B8 File Offset: 0x000223B8
		public static void PlayMusic(string name, float startPercentage)
		{
			if (string.IsNullOrEmpty(name))
			{
				MusicManagerN.StopMusic();
				return;
			}
			try
			{
				MusicManagerN.StopMusic();
				MusicManagerN._mSound = new StreamingSound(ContentManager.Get<StreamingSource>(name), MusicManagerN.Volume, 1f, 0f, true, true, 1f);
				MusicManagerN._mSound.Play();
			}
			catch
			{
				Log.Warning("Error playing music \"{0}\".", new object[]
				{
					name
				});
			}
		}

		// Token: 0x060005C8 RID: 1480 RVA: 0x00024234 File Offset: 0x00022434
		public static void StopMusic()
		{
			if (MusicManagerN._mSound == null)
			{
				return;
			}
			MusicManagerN._mSound.Stop();
			MusicManagerN._mSound = null;
		}

		// Token: 0x0400035B RID: 859
		private static StreamingSound _mSound;
	}
}
