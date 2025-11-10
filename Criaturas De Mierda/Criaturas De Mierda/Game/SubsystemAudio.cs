using System;
using System.Collections.Generic;
using Engine;
using Engine.Audio;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000340 RID: 832
	public class SubsystemAudio : Subsystem, IUpdateable
	{
		// Token: 0x17000398 RID: 920
		// (get) Token: 0x06001939 RID: 6457 RVA: 0x000C6BD0 File Offset: 0x000C4DD0
		public ReadOnlyList<Vector3> ListenerPositions
		{
			get
			{
				return new ReadOnlyList<Vector3>(this.m_listenerPositions);
			}
		}

		// Token: 0x17000399 RID: 921
		// (get) Token: 0x0600193A RID: 6458 RVA: 0x000C6BDD File Offset: 0x000C4DDD
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x0600193B RID: 6459 RVA: 0x000C6BE0 File Offset: 0x000C4DE0
		public float CalculateListenerDistanceSquared(Vector3 p)
		{
			float num = float.MaxValue;
			for (int i = 0; i < this.m_listenerPositions.Count; i++)
			{
				float num2 = Vector3.DistanceSquared(this.m_listenerPositions[i], p);
				if (num2 < num)
				{
					num = num2;
				}
			}
			return num;
		}

		// Token: 0x0600193C RID: 6460 RVA: 0x000C6C23 File Offset: 0x000C4E23
		public float CalculateListenerDistance(Vector3 p)
		{
			return MathF.Sqrt(this.CalculateListenerDistanceSquared(p));
		}

		// Token: 0x0600193D RID: 6461 RVA: 0x000C6C34 File Offset: 0x000C4E34
		public void Mute()
		{
			foreach (Sound sound in this.m_sounds)
			{
				if (sound.State == 1)
				{
					this.m_mutedSounds[sound] = true;
					sound.Pause();
				}
			}
		}

		// Token: 0x0600193E RID: 6462 RVA: 0x000C6C9C File Offset: 0x000C4E9C
		public void Unmute()
		{
			foreach (Sound sound in this.m_mutedSounds.Keys)
			{
				sound.Play();
			}
			this.m_mutedSounds.Clear();
		}

		// Token: 0x0600193F RID: 6463 RVA: 0x000C6CFC File Offset: 0x000C4EFC
		public void PlaySound(string name, float volume, float pitch, float pan, float delay)
		{
			double num = this.m_subsystemTime.GameTime + (double)delay;
			this.m_nextSoundTime = Math.Min(this.m_nextSoundTime, num);
			this.m_queuedSounds.Add(new SubsystemAudio.SoundInfo
			{
				Time = num,
				Name = name,
				Volume = volume,
				Pitch = pitch,
				Pan = pan
			});
		}

		// Token: 0x06001940 RID: 6464 RVA: 0x000C6D68 File Offset: 0x000C4F68
		public void PlaySound(string name, float volume, float pitch, float pan, float delay, Vector3 direction)
		{
			double num = this.m_subsystemTime.GameTime + (double)delay;
			this.m_nextSoundTime = Math.Min(this.m_nextSoundTime, num);
			this.m_queuedSounds.Add(new SubsystemAudio.SoundInfo
			{
				Time = num,
				Name = name,
				Volume = volume,
				Pitch = pitch,
				Pan = pan,
				direction = direction
			});
		}

		// Token: 0x06001941 RID: 6465 RVA: 0x000C6DDC File Offset: 0x000C4FDC
		public virtual void PlaySound(string name, float volume, float pitch, Vector3 position, float minDistance, float delay)
		{
			float num = this.CalculateVolume(this.CalculateListenerDistance(position), minDistance, 2f);
			this.PlaySound(name, volume * num, pitch, 0f, delay);
		}

		// Token: 0x06001942 RID: 6466 RVA: 0x000C6E14 File Offset: 0x000C5014
		public virtual void PlaySound(string name, float volume, float pitch, Vector3 position, float minDistance, bool autoDelay)
		{
			float num = this.CalculateVolume(this.CalculateListenerDistance(position), minDistance, 2f);
			this.PlaySound(name, volume * num, pitch, 0f, autoDelay ? this.CalculateDelay(position) : 0f);
		}

		// Token: 0x06001943 RID: 6467 RVA: 0x000C6E5C File Offset: 0x000C505C
		public void PlayRandomSound(string directory, float volume, float pitch, float pan, float delay)
		{
			ReadOnlyList<ContentInfo> readOnlyList = ContentManager.List(directory);
			if (readOnlyList.Count > 0)
			{
				int num = this.m_random.Int(0, readOnlyList.Count - 1);
				this.PlaySound(readOnlyList[num].ContentPath, volume, pitch, pan, delay);
				return;
			}
			Log.Warning("Sounds directory \"{0}\" not found or empty.", new object[]
			{
				directory
			});
		}

		// Token: 0x06001944 RID: 6468 RVA: 0x000C6EC0 File Offset: 0x000C50C0
		public virtual void PlayRandomSound(string directory, float volume, float pitch, Vector3 position, float minDistance, float delay)
		{
			float num = this.CalculateVolume(this.CalculateListenerDistance(position), minDistance, 2f);
			this.PlayRandomSound(directory, volume * num, pitch, 0f, delay);
		}

		// Token: 0x06001945 RID: 6469 RVA: 0x000C6EF8 File Offset: 0x000C50F8
		public virtual void PlayRandomSound(string directory, float volume, float pitch, Vector3 position, float minDistance, bool autoDelay)
		{
			float num = this.CalculateVolume(this.CalculateListenerDistance(position), minDistance, 2f);
			this.PlayRandomSound(directory, volume * num, pitch, 0f, autoDelay ? this.CalculateDelay(position) : 0f);
		}

		// Token: 0x06001946 RID: 6470 RVA: 0x000C6F40 File Offset: 0x000C5140
		public Sound CreateSound(string name)
		{
			Sound sound = new Sound(ContentManager.Get<SoundBuffer>(name), 1f, 1f, 0f, false, false);
			this.m_sounds.Add(sound);
			return sound;
		}

		// Token: 0x06001947 RID: 6471 RVA: 0x000C6F77 File Offset: 0x000C5177
		public float CalculateVolume(float distance, float minDistance, float rolloffFactor = 2f)
		{
			if (distance <= minDistance)
			{
				return 1f;
			}
			return minDistance / (minDistance + Math.Max(rolloffFactor * (distance - minDistance), 0f));
		}

		// Token: 0x06001948 RID: 6472 RVA: 0x000C6F96 File Offset: 0x000C5196
		public float CalculateDelay(Vector3 position)
		{
			return this.CalculateDelay(this.CalculateListenerDistance(position));
		}

		// Token: 0x06001949 RID: 6473 RVA: 0x000C6FA5 File Offset: 0x000C51A5
		public float CalculateDelay(float distance)
		{
			return Math.Min(distance / 120f, 3f);
		}

		// Token: 0x0600194A RID: 6474 RVA: 0x000C6FB8 File Offset: 0x000C51B8
		public void Update(float dt)
		{
			this.m_listenerPositions.Clear();
			foreach (GameWidget gameWidget in this.m_subsystemViews.GameWidgets)
			{
				this.m_listenerPositions.Add(gameWidget.ActiveCamera.ViewPosition);
			}
			if (this.m_subsystemTime.GameTime < this.m_nextSoundTime)
			{
				return;
			}
			this.m_nextSoundTime = double.MaxValue;
			int i = 0;
			while (i < this.m_queuedSounds.Count)
			{
				SubsystemAudio.SoundInfo soundInfo = this.m_queuedSounds[i];
				if (this.m_subsystemTime.GameTime >= soundInfo.Time)
				{
					if (this.m_subsystemTime.GameTimeFactor == 1f && this.m_subsystemTime.FixedTimeStep == null && soundInfo.Volume * SettingsManager.SoundsVolume > AudioManager.MinAudibleVolume && this.UpdateCongestion(soundInfo.Name, soundInfo.Volume))
					{
						AudioManager.PlaySound(soundInfo.Name, soundInfo.Volume, soundInfo.Pitch, soundInfo.Pan, soundInfo.direction);
					}
					this.m_queuedSounds.RemoveAt(i);
				}
				else
				{
					this.m_nextSoundTime = Math.Min(this.m_nextSoundTime, soundInfo.Time);
					i++;
				}
			}
		}

		// Token: 0x0600194B RID: 6475 RVA: 0x000C7134 File Offset: 0x000C5334
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemViews = base.Project.FindSubsystem<SubsystemGameWidgets>(true);
		}

		// Token: 0x0600194C RID: 6476 RVA: 0x000C715C File Offset: 0x000C535C
		public override void Dispose()
		{
			foreach (Sound sound in this.m_sounds)
			{
				sound.Dispose();
			}
		}

		// Token: 0x0600194D RID: 6477 RVA: 0x000C71AC File Offset: 0x000C53AC
		public bool UpdateCongestion(string name, float volume)
		{
			SubsystemAudio.Congestion congestion;
			if (!this.m_congestions.TryGetValue(name, out congestion))
			{
				congestion = new SubsystemAudio.Congestion();
				this.m_congestions.Add(name, congestion);
			}
			double realTime = Time.RealTime;
			double lastUpdateTime = congestion.LastUpdateTime;
			double lastPlayedTime = congestion.LastPlayedTime;
			float num = (lastUpdateTime > 0.0) ? ((float)(realTime - lastUpdateTime)) : 0f;
			congestion.Value = Math.Max(congestion.Value - 10f * num, 0f);
			congestion.LastUpdateTime = realTime;
			if (congestion.Value <= 6f && (lastPlayedTime == 0.0 || volume > congestion.LastPlayedVolume || realTime - lastPlayedTime >= 0.0))
			{
				congestion.LastPlayedTime = realTime;
				congestion.LastPlayedVolume = volume;
				congestion.Value += 1f;
				return true;
			}
			return false;
		}

		// Token: 0x040011E5 RID: 4581
		public SubsystemTime m_subsystemTime;

		// Token: 0x040011E6 RID: 4582
		public SubsystemGameWidgets m_subsystemViews;

		// Token: 0x040011E7 RID: 4583
		public Random m_random = new Random();

		// Token: 0x040011E8 RID: 4584
		public List<Vector3> m_listenerPositions = new List<Vector3>();

		// Token: 0x040011E9 RID: 4585
		public Dictionary<string, SubsystemAudio.Congestion> m_congestions = new Dictionary<string, SubsystemAudio.Congestion>();

		// Token: 0x040011EA RID: 4586
		public double m_nextSoundTime;

		// Token: 0x040011EB RID: 4587
		public List<SubsystemAudio.SoundInfo> m_queuedSounds = new List<SubsystemAudio.SoundInfo>();

		// Token: 0x040011EC RID: 4588
		public List<Sound> m_sounds = new List<Sound>();

		// Token: 0x040011ED RID: 4589
		public Dictionary<Sound, bool> m_mutedSounds = new Dictionary<Sound, bool>();

		// Token: 0x02000632 RID: 1586
		public class Congestion
		{
			// Token: 0x04001EBF RID: 7871
			public double LastUpdateTime;

			// Token: 0x04001EC0 RID: 7872
			public double LastPlayedTime;

			// Token: 0x04001EC1 RID: 7873
			public float LastPlayedVolume;

			// Token: 0x04001EC2 RID: 7874
			public float Value;
		}

		// Token: 0x02000633 RID: 1587
		public struct SoundInfo
		{
			// Token: 0x06002A1B RID: 10779 RVA: 0x0011CA44 File Offset: 0x0011AC44
			public SoundInfo()
			{
				this.Time = 0.0;
				this.Name = null;
				this.Volume = 0f;
				this.Pitch = 0f;
				this.Pan = 0f;
				this.direction = Vector3.Zero;
			}

			// Token: 0x04001EC3 RID: 7875
			public double Time;

			// Token: 0x04001EC4 RID: 7876
			public string Name;

			// Token: 0x04001EC5 RID: 7877
			public float Volume;

			// Token: 0x04001EC6 RID: 7878
			public float Pitch;

			// Token: 0x04001EC7 RID: 7879
			public float Pan;

			// Token: 0x04001EC8 RID: 7880
			public Vector3 direction;
		}
	}
}
