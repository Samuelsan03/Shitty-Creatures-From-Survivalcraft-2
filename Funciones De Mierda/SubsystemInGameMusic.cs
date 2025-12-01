using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000043 RID: 67
	public class SubsystemInGameMusic : Subsystem, IUpdateable
	{
		// Token: 0x0600022F RID: 559 RVA: 0x0001BA2C File Offset: 0x00019C2C
		private void ShowMessageToAllPlayers(string message)
		{
			foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
			{
				componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.White, false, false);
			}
		}

		// Token: 0x1700005D RID: 93
		// (get) Token: 0x06000230 RID: 560 RVA: 0x0001BA9C File Offset: 0x00019C9C
		public UpdateOrder UpdateOrder
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x06000231 RID: 561 RVA: 0x0001BAA0 File Offset: 0x00019CA0
		public void Update(float dt)
		{
			bool flag = (int)MusicManager.CurrentMix != 2;
			if (!flag)
			{
				bool flag2 = this.m_nextMusicTime == 0.0;
				if (flag2)
				{
					this.ScheduleNextMusic();
				}
				bool flag3 = this.m_subsystemTime.GameTime >= this.m_nextMusicTime && !MusicManager.IsPlaying;
				if (flag3)
				{
					this.PlayRandomMusic();
					this.ScheduleNextMusic();
				}
			}
		}

		// Token: 0x06000232 RID: 562 RVA: 0x0001BB10 File Offset: 0x00019D10
		private void PlayRandomMusic()
		{
			bool flag = this.m_availableTracks.Count == 0;
			if (flag)
			{
				for (int i = 0; i < this.m_tracks.Length; i++)
				{
					bool flag2 = !this.m_recentTracks.Contains(i);
					if (flag2)
					{
						this.m_availableTracks.Add(i);
					}
				}
				bool flag3 = this.m_availableTracks.Count == 0;
				if (flag3)
				{
					this.m_recentTracks.Clear();
					for (int j = 0; j < this.m_tracks.Length; j++)
					{
						this.m_availableTracks.Add(j);
					}
				}
			}
			int index = this.m_random.Int(0, this.m_availableTracks.Count - 1);
			int num = this.m_availableTracks[index];
			SubsystemInGameMusic.TrackInfo trackInfo = this.m_tracks[num];
			this.UpdateTrackHistory(num);
			try
			{
				MusicManager.PlayMusic(trackInfo.Path, 0f);
				this.m_musicDuration = (double)trackInfo.Duration;
				Log.Information("Reproduciendo música: " + trackInfo.Path);
			}
			catch (Exception ex)
			{
				Log.Error("SubsystemInGameMusic error: " + ex.Message);
			}
		}

		// Token: 0x06000233 RID: 563 RVA: 0x0001BC68 File Offset: 0x00019E68
		private void ScheduleNextMusic()
		{
			bool flag = SettingsManager.MusicVolume > 0f;
			if (flag)
			{
				double num = (double)this.m_random.Float(30f, 180f);
				this.m_nextMusicTime = this.m_subsystemTime.GameTime + this.m_musicDuration + num;
			}
			else
			{
				this.m_nextMusicTime = this.m_subsystemTime.GameTime + 10.0;
			}
		}

		// Token: 0x06000234 RID: 564 RVA: 0x0001BCD8 File Offset: 0x00019ED8
		private void UpdateTrackHistory(int playedIndex)
		{
			this.m_recentTracks.Enqueue(playedIndex);
			this.m_availableTracks.Remove(playedIndex);
			bool flag = this.m_recentTracks.Count > 2;
			if (flag)
			{
				int item = this.m_recentTracks.Dequeue();
				bool flag2 = !this.m_availableTracks.Contains(item);
				if (flag2)
				{
					this.m_availableTracks.Add(item);
				}
			}
		}

		// Token: 0x06000235 RID: 565 RVA: 0x0001BD40 File Offset: 0x00019F40
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			for (int i = 0; i < this.m_tracks.Length; i++)
			{
				this.m_availableTracks.Add(i);
			}
			this.m_nextMusicTime = this.m_subsystemTime.GameTime + 5.0;
		}

		// Token: 0x06000236 RID: 566 RVA: 0x0001BDB3 File Offset: 0x00019FB3
		public override void Dispose()
		{
			base.Dispose();
		}

		// Token: 0x040002DB RID: 731
		public SubsystemTime m_subsystemTime;

		// Token: 0x040002DC RID: 732
		public Random m_random = new Random();

		// Token: 0x040002DD RID: 733
		private double m_nextMusicTime;

		// Token: 0x040002DE RID: 734
		private double m_musicDuration;

		// Token: 0x040002DF RID: 735
		public SubsystemPlayers m_subsystemPlayers;

		// Token: 0x040002E0 RID: 736
		private readonly Queue<int> m_recentTracks = new Queue<int>(2);

		// Token: 0x040002E1 RID: 737
		private readonly List<int> m_availableTracks = new List<int>();

		// Token: 0x040002E2 RID: 738
		private readonly SubsystemInGameMusic.TrackInfo[] m_tracks = new SubsystemInGameMusic.TrackInfo[]
		{
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Digimon02OpeningThemeSong", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou2MimasThemeCompleteDarkness", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou2EasternWind", 177f),
			new SubsystemInGameMusic.TrackInfo("MenuMusic/Touhou2RecordoftheSealingofanOrientalDemon", 177f)
		};

		// Token: 0x02000065 RID: 101
		private struct TrackInfo
		{
			// Token: 0x06000326 RID: 806 RVA: 0x000241FD File Offset: 0x000223FD
			public TrackInfo(string path, float duration)
			{
				this.Path = path;
				this.Duration = duration;
			}

			// Token: 0x04000384 RID: 900
			public string Path;

			// Token: 0x04000385 RID: 901
			public float Duration;
		}
	}
}
