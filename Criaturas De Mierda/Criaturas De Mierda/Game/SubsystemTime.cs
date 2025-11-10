using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200039E RID: 926
	public class SubsystemTime : Subsystem
	{
		// Token: 0x17000447 RID: 1095
		// (get) Token: 0x06001DC4 RID: 7620 RVA: 0x000ED0A4 File Offset: 0x000EB2A4
		// (set) Token: 0x06001DC5 RID: 7621 RVA: 0x000ED0D4 File Offset: 0x000EB2D4
		public float MaxGameTimeDelta
		{
			get
			{
				float? maxGameTimeDelta = this.m_maxGameTimeDelta;
				if (maxGameTimeDelta == null)
				{
					return 1f / SettingsManager.LowFPSToTimeDeceleration;
				}
				return maxGameTimeDelta.GetValueOrDefault();
			}
			set
			{
				this.m_maxGameTimeDelta = new float?(value);
			}
		}

		// Token: 0x17000448 RID: 1096
		// (get) Token: 0x06001DC6 RID: 7622 RVA: 0x000ED0E4 File Offset: 0x000EB2E4
		// (set) Token: 0x06001DC7 RID: 7623 RVA: 0x000ED114 File Offset: 0x000EB314
		public float MaxFixedGameTimeDelta
		{
			get
			{
				float? maxFixedGameTimeDelta = this.m_maxFixedGameTimeDelta;
				if (maxFixedGameTimeDelta == null)
				{
					return 1f / SettingsManager.LowFPSToTimeDeceleration;
				}
				return maxFixedGameTimeDelta.GetValueOrDefault();
			}
			set
			{
				this.m_maxFixedGameTimeDelta = new float?(value);
			}
		}

		// Token: 0x17000449 RID: 1097
		// (get) Token: 0x06001DC8 RID: 7624 RVA: 0x000ED122 File Offset: 0x000EB322
		public double GameTime
		{
			get
			{
				return this.m_gameTime;
			}
		}

		// Token: 0x1700044A RID: 1098
		// (get) Token: 0x06001DC9 RID: 7625 RVA: 0x000ED12A File Offset: 0x000EB32A
		public float GameTimeDelta
		{
			get
			{
				return this.m_gameTimeDelta;
			}
		}

		// Token: 0x1700044B RID: 1099
		// (get) Token: 0x06001DCA RID: 7626 RVA: 0x000ED132 File Offset: 0x000EB332
		public float PreviousGameTimeDelta
		{
			get
			{
				return this.m_prevGameTimeDelta;
			}
		}

		// Token: 0x1700044C RID: 1100
		// (get) Token: 0x06001DCB RID: 7627 RVA: 0x000ED13A File Offset: 0x000EB33A
		// (set) Token: 0x06001DCC RID: 7628 RVA: 0x000ED142 File Offset: 0x000EB342
		public float GameTimeFactor
		{
			get
			{
				return this.m_gameTimeFactor;
			}
			set
			{
				this.m_gameTimeFactor = Math.Clamp(value, 0f, 256f);
			}
		}

		// Token: 0x1700044D RID: 1101
		// (get) Token: 0x06001DCD RID: 7629 RVA: 0x000ED15A File Offset: 0x000EB35A
		// (set) Token: 0x06001DCE RID: 7630 RVA: 0x000ED162 File Offset: 0x000EB362
		public float? FixedTimeStep { get; private set; }

		// Token: 0x06001DCF RID: 7631 RVA: 0x000ED16C File Offset: 0x000EB36C
		public virtual float CalculateGameTimeDalta()
		{
			if (this.FixedTimeStep != null)
			{
				return MathUtils.Min(this.FixedTimeStep.Value, this.MaxFixedGameTimeDelta) * this.m_gameTimeFactor;
			}
			return MathUtils.Min(Time.FrameDuration, this.MaxGameTimeDelta) * this.m_gameTimeFactor;
		}

		// Token: 0x06001DD0 RID: 7632 RVA: 0x000ED1C4 File Offset: 0x000EB3C4
		public virtual bool IsAllPlayerLivingSleeping()
		{
			int num = 0;
			int num2 = 0;
			foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
			{
				if (componentPlayer.ComponentHealth.Health == 0f)
				{
					num2++;
				}
				else if (componentPlayer.ComponentSleep.SleepFactor == 1f)
				{
					num++;
				}
			}
			return num + num2 == this.m_subsystemPlayers.ComponentPlayers.Count && num >= 1;
		}

		// Token: 0x06001DD1 RID: 7633 RVA: 0x000ED270 File Offset: 0x000EB470
		public virtual void NextFrame()
		{
			this.m_prevGameTimeDelta = this.m_gameTimeDelta;
			this.m_gameTimeDelta = this.CalculateGameTimeDalta();
			ModsManager.HookAction("ChangeGameTimeDelta", delegate(ModLoader loader)
			{
				loader.ChangeGameTimeDelta(this, ref this.m_gameTimeDelta);
				return false;
			});
			this.m_gameTime += (double)this.m_gameTimeDelta;
			int i = 0;
			while (i < this.m_delayedExecutionsRequests.Count)
			{
				SubsystemTime.DelayedExecutionRequest delayedExecutionRequest = this.m_delayedExecutionsRequests[i];
				if (delayedExecutionRequest.GameTime >= 0.0 && this.GameTime >= delayedExecutionRequest.GameTime)
				{
					this.m_delayedExecutionsRequests.RemoveAt(i);
					delayedExecutionRequest.Action();
				}
				else
				{
					i++;
				}
			}
			if (this.IsAllPlayerLivingSleeping())
			{
				if (SettingsManager.UseAPISleepTimeAcceleration)
				{
					if (this.m_gameTimeFactorSleep != null)
					{
						this.m_gameTimeFactor = this.m_gameTimeFactorSleep.Value;
					}
				}
				else
				{
					this.FixedTimeStep = new float?(this.DefaultFixedTimeStep);
					this.m_subsystemUpdate.UpdatesPerFrame = this.DefaultFixedUpdateStep;
				}
			}
			else
			{
				this.FixedTimeStep = null;
				this.m_subsystemUpdate.UpdatesPerFrame = 1;
				if (this.m_gameTimeFactorSleep != null)
				{
					this.m_gameTimeFactor = 1f;
				}
			}
			bool flag = true;
			using (ReadOnlyList<ComponentPlayer>.Enumerator enumerator = this.m_subsystemPlayers.ComponentPlayers.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (!enumerator.Current.ComponentGui.IsGameMenuDialogVisible())
					{
						flag = false;
						break;
					}
				}
			}
			if (flag)
			{
				this.GameTimeFactor = this.GameMenuDialogTimeFactor;
				return;
			}
			if (this.GameTimeFactor == this.GameMenuDialogTimeFactor)
			{
				this.GameTimeFactor = 1f;
			}
		}

		// Token: 0x06001DD2 RID: 7634 RVA: 0x000ED424 File Offset: 0x000EB624
		public void QueueGameTimeDelayedExecution(double gameTime, Action action)
		{
			this.m_delayedExecutionsRequests.Add(new SubsystemTime.DelayedExecutionRequest
			{
				GameTime = gameTime,
				Action = action
			});
		}

		// Token: 0x06001DD3 RID: 7635 RVA: 0x000ED458 File Offset: 0x000EB658
		public bool PeriodicGameTimeEvent(double period, double offset)
		{
			double num = this.GameTime - offset;
			double num2 = Math.Floor(num / period) * period;
			return num >= num2 && num - (double)this.GameTimeDelta < num2;
		}

		// Token: 0x06001DD4 RID: 7636 RVA: 0x000ED48B File Offset: 0x000EB68B
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemUpdate = base.Project.FindSubsystem<SubsystemUpdate>(true);
		}

		// Token: 0x0400145E RID: 5214
		public float? m_maxGameTimeDelta;

		// Token: 0x0400145F RID: 5215
		public float? m_maxFixedGameTimeDelta;

		// Token: 0x04001460 RID: 5216
		public float DefaultFixedTimeStep = 0.05f;

		// Token: 0x04001461 RID: 5217
		public int DefaultFixedUpdateStep = 20;

		// Token: 0x04001462 RID: 5218
		public float GameMenuDialogTimeFactor;

		// Token: 0x04001463 RID: 5219
		public double m_gameTime;

		// Token: 0x04001464 RID: 5220
		public float m_gameTimeDelta;

		// Token: 0x04001465 RID: 5221
		public float m_prevGameTimeDelta;

		// Token: 0x04001466 RID: 5222
		public float m_gameTimeFactor = 1f;

		// Token: 0x04001467 RID: 5223
		public float? m_gameTimeFactorSleep = new float?(60f);

		// Token: 0x04001468 RID: 5224
		public List<SubsystemTime.DelayedExecutionRequest> m_delayedExecutionsRequests = new List<SubsystemTime.DelayedExecutionRequest>();

		// Token: 0x04001469 RID: 5225
		public SubsystemPlayers m_subsystemPlayers;

		// Token: 0x0400146A RID: 5226
		public SubsystemUpdate m_subsystemUpdate;

		// Token: 0x0200068E RID: 1678
		public struct DelayedExecutionRequest
		{
			// Token: 0x04002006 RID: 8198
			public double GameTime;

			// Token: 0x04002007 RID: 8199
			public Action Action;
		}
	}
}
