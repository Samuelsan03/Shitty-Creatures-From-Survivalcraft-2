using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000008 RID: 8
	public class ComponentInvShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000028 RID: 40 RVA: 0x00003B7C File Offset: 0x00001D7C
		public int UpdateOrder
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x06000029 RID: 41 RVA: 0x00003B90 File Offset: 0x00001D90
		public override float ImportanceLevel
		{
			get
			{
				return 0f;
			}
		}

		// Token: 0x17000006 RID: 6
		// (get) Token: 0x0600002A RID: 42 RVA: 0x00003BA8 File Offset: 0x00001DA8
		UpdateOrder IUpdateable.UpdateOrder
		{
			get
			{
				return this.m_subsystemProjectiles.UpdateOrder;
			}
		}

		// Token: 0x0600002B RID: 43 RVA: 0x00003BC8 File Offset: 0x00001DC8
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			this.m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(false);
			this.ThrowingSound = valuesDictionary.GetValue<string>("ThrowingSound");
			this.ThrowingSoundDistance = valuesDictionary.GetValue<float>("ThrowingSoundDistance");
			this.DiscountFromInventory = valuesDictionary.GetValue<bool>("DiscountFromInventory");
			this.MinMaxDistance = valuesDictionary.GetValue<string>("MinMaxDistance");
			this.MinMaxRandomChargeTime = base.ValuesDictionary.GetValue<string>("MinMaxRandomChargeTime");
			this.MinMaxRandomWaitTime = base.ValuesDictionary.GetValue<string>("MinMaxRandomWaitTime");
			this.SelectRandomThrowableItems = base.ValuesDictionary.GetValue<bool>("SelectRandomThrowableItems");
			this.ThrowFromHead = base.ValuesDictionary.GetValue<bool>("ThrowFromHead");
			this.MeleeRange = valuesDictionary.GetValue<float>("MeleeRange", 2.5f);
			this.m_stateMachine.AddState("Idle", null, new Action(this.Idle_Update), null);
			this.m_stateMachine.AddState("Aiming", new Action(this.Aiming_Enter), new Action(this.Aiming_Update), null);
			this.m_stateMachine.AddState("Fire", new Action(this.Fire_Enter), new Action(this.Fire_Update), new Action(this.Fire_Leave));
			this.m_stateMachine.AddState("Reloading", null, new Action(this.Reloading_Update), null);
			this.TransitionToState("Idle");
			string value = valuesDictionary.GetValue<string>("ExcludedThrowableItems", string.Empty);
			string value2 = base.ValuesDictionary.GetValue<string>("SpecialThrowableItem", string.Empty);
			bool flag = !string.IsNullOrEmpty(value2);
			bool flag2 = flag;
			if (flag2)
			{
				string[] array = value2.Split(',', StringSplitOptions.None);
				foreach (string text in array)
				{
					string text2 = text.Trim();
					bool flag3 = text2.Contains(":");
					bool flag4 = flag3;
					if (flag4)
					{
						string[] array3 = text2.Split(':', StringSplitOptions.None);
						string text3 = array3[0].Trim();
						string s = array3[1].Trim();
						int num;
						bool flag5 = int.TryParse(s, out num);
						bool flag6 = flag5;
						if (flag6)
						{
							int blockIndex = BlocksManager.GetBlockIndex(text3, false);
							bool flag7 = blockIndex >= 0;
							bool flag8 = flag7;
							if (flag8)
							{
								int item = Terrain.MakeBlockValue(blockIndex, 0, num);
								this.m_specialThrowableItemValues.Add(item);
							}
							else
							{
								Log.Warning("SpecialThrowableItem '" + text2 + "' not found");
							}
						}
						else
						{
							Log.Warning("SpecialThrowableItem '" + text2 + "' has invalid variant format");
						}
					}
					else
					{
						int num2;
						bool flag9 = int.TryParse(text2, out num2);
						bool flag10 = flag9;
						if (flag10)
						{
							this.m_specialThrowableItemValues.Add(Terrain.MakeBlockValue(num2));
						}
						else
						{
							int blockIndex2 = BlocksManager.GetBlockIndex(text2, false);
							bool flag11 = blockIndex2 >= 0;
							bool flag12 = flag11;
							if (flag12)
							{
								this.m_specialThrowableItemValues.Add(Terrain.MakeBlockValue(blockIndex2));
							}
							else
							{
								Log.Warning("SpecialThrowableItem '" + text2 + "' not found");
							}
						}
					}
				}
			}
			else
			{
				this.m_specialThrowableItemValues.Clear();
			}
			bool flag13 = !string.IsNullOrEmpty(value);
			bool flag14 = flag13;
			if (flag14)
			{
				string[] array4 = value.Split(';', StringSplitOptions.None);
				foreach (string text4 in array4)
				{
					int num3;
					bool flag15 = int.TryParse(text4, out num3);
					bool flag16 = flag15;
					if (flag16)
					{
						this.m_excludedItems.Add(Terrain.ExtractContents(Terrain.MakeBlockValue(num3)));
					}
					else
					{
						int blockIndex3 = BlocksManager.GetBlockIndex(text4, false);
						bool flag17 = blockIndex3 >= 0;
						bool flag18 = flag17;
						if (flag18)
						{
							this.m_excludedItems.Add(blockIndex3);
						}
						else
						{
							Log.Warning("ExcludedThrowableItem '" + text4 + "' not found");
						}
					}
				}
			}
			string[] array6 = this.MinMaxRandomWaitTime.Split(';', StringSplitOptions.None);
			bool flag19 = array6.Length >= 2 && float.TryParse(array6[0], out this.m_randomWaitMin) && float.TryParse(array6[1], out this.m_randomWaitMax);
			bool flag20 = !flag19;
			if (flag20)
			{
				this.m_randomWaitMin = 0.55f;
				this.m_randomWaitMax = 0.55f;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(79, 3);
				defaultInterpolatedStringHandler.AppendLiteral("Invalid MinMaxRandomWaitTime format or empty: '");
				defaultInterpolatedStringHandler.AppendFormatted(this.MinMaxRandomWaitTime);
				defaultInterpolatedStringHandler.AppendLiteral("'. Using default values ('");
				defaultInterpolatedStringHandler.AppendFormatted<float>(this.m_randomWaitMin);
				defaultInterpolatedStringHandler.AppendLiteral("';'");
				defaultInterpolatedStringHandler.AppendFormatted<float>(this.m_randomWaitMax);
				defaultInterpolatedStringHandler.AppendLiteral("').");
				Log.Warning(defaultInterpolatedStringHandler.ToStringAndClear());
			}
			string[] array7 = this.MinMaxDistance.Split(';', StringSplitOptions.None);
			bool flag21 = array7.Length >= 2 && float.TryParse(array7[0], out this.m_minDistance) && float.TryParse(array7[1], out this.m_maxDistance);
			bool flag22 = !flag21;
			if (flag22)
			{
				this.m_minDistance = 3f;
				this.m_maxDistance = 15f;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler2 = new DefaultInterpolatedStringHandler(64, 3);
				defaultInterpolatedStringHandler2.AppendLiteral("Invalid MinMaxDistance format: '");
				defaultInterpolatedStringHandler2.AppendFormatted(this.MinMaxDistance);
				defaultInterpolatedStringHandler2.AppendLiteral("'. Using default values ('");
				defaultInterpolatedStringHandler2.AppendFormatted<float>(this.m_minDistance);
				defaultInterpolatedStringHandler2.AppendLiteral("';'");
				defaultInterpolatedStringHandler2.AppendFormatted<float>(this.m_maxDistance);
				defaultInterpolatedStringHandler2.AppendLiteral("').");
				Log.Warning(defaultInterpolatedStringHandler2.ToStringAndClear());
			}
			bool flag23 = !string.IsNullOrEmpty(this.SpecialThrowableItem);
			bool flag24 = flag23;
			if (flag24)
			{
				int num4;
				bool flag25 = int.TryParse(this.SpecialThrowableItem, out num4);
				bool flag26 = flag25;
				if (flag26)
				{
					this.m_specialThrowableItemValue = Terrain.MakeBlockValue(num4);
				}
				else
				{
					int blockIndex4 = BlocksManager.GetBlockIndex(this.SpecialThrowableItem, false);
					bool flag27 = blockIndex4 >= 0;
					bool flag28 = flag27;
					if (flag28)
					{
						this.m_specialThrowableItemValue = Terrain.MakeBlockValue(blockIndex4);
					}
					else
					{
						this.m_specialThrowableItemValue = 0;
						Log.Warning("SpecialThrowableItem '" + this.SpecialThrowableItem + "' not found");
					}
				}
			}
			else
			{
				this.m_specialThrowableItemValue = 0;
			}
			string[] array8 = this.MinMaxRandomChargeTime.Split(';', StringSplitOptions.None);
			bool flag29 = array8.Length >= 2 && float.TryParse(array8[0], out this.m_randomThrowMin) && float.TryParse(array8[1], out this.m_randomThrowMax);
			bool flag30 = !flag29;
			if (flag30)
			{
				this.m_randomThrowMin = 0.55f;
				this.m_randomThrowMax = 0.55f;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler3 = new DefaultInterpolatedStringHandler(70, 3);
				defaultInterpolatedStringHandler3.AppendLiteral("Invalid MinMaxRandomWaitTime format: '");
				defaultInterpolatedStringHandler3.AppendFormatted(this.MinMaxRandomWaitTime);
				defaultInterpolatedStringHandler3.AppendLiteral("'. Using default values ('");
				defaultInterpolatedStringHandler3.AppendFormatted<float>(this.m_randomThrowMin);
				defaultInterpolatedStringHandler3.AppendLiteral("';'");
				defaultInterpolatedStringHandler3.AppendFormatted<float>(this.m_randomThrowMax);
				defaultInterpolatedStringHandler3.AppendLiteral("').");
				Log.Warning(defaultInterpolatedStringHandler3.ToStringAndClear());
			}
		}

		// Token: 0x0600002C RID: 44 RVA: 0x0000437C File Offset: 0x0000257C
		public void Update(float dt)
		{
			bool flag = this.m_componentCreature.ComponentHealth.Health <= 0f;
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = this.m_subsystemTime.GameTime >= this.m_nextProactiveReloadTime;
				if (flag3)
				{
					this.m_nextProactiveReloadTime = this.m_subsystemTime.GameTime + 0.55;
					bool flag4 = this.m_currentStateName == "Idle";
					if (flag4)
					{
						this.ProactiveReloadCheck();
					}
				}
				bool flag5 = this.m_subsystemTime.GameTime >= this.m_nextCombatUpdateTime;
				if (flag5)
				{
					this.m_stateMachine.Update();
				}
				double gameTime = this.m_subsystemTime.GameTime;
				bool isCharging = this.m_isCharging;
				bool flag6 = isCharging;
				if (flag6)
				{
					bool flag7 = this.m_componentChaseBehavior.Target != null;
					float num = 0f;
					bool flag8 = flag7;
					bool flag9 = flag8;
					if (flag9)
					{
						bool throwFromHead = this.ThrowFromHead;
						bool flag10 = throwFromHead;
						Vector3 vector;
						if (flag10)
						{
							vector = this.m_componentCreature.ComponentCreatureModel.EyePosition;
						}
						else
						{
							vector = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
						}
						num = Vector3.Distance(vector, this.m_componentChaseBehavior.Target.ComponentBody.Position);
					}
					bool flag11 = flag7 && num >= this.m_minDistance && num <= this.m_maxDistance;
					bool flag12;
					if (flag11)
					{
						ComponentHealth componentHealth = this.m_componentChaseBehavior.Target.Entity.FindComponent<ComponentHealth>();
						flag12 = (componentHealth != null && componentHealth.Health <= 0f);
					}
					else
					{
						flag12 = true;
					}
					bool flag13 = flag12;
					bool flag14 = flag13;
					if (flag14)
					{
						this.m_isCharging = false;
					}
					else
					{
						bool flag15 = gameTime >= this.m_chargeStartTime + (double)this.m_chargeDuration;
						bool flag16 = flag15;
						if (flag16)
						{
							this.FireProjectile();
							this.m_isCharging = false;
							this.m_ChargeTime = (double)this.m_random.Float(this.m_randomThrowMin, this.m_randomThrowMax);
							bool flag17 = this.m_distance < this.m_minDistance;
							bool flag18 = flag17;
							if (flag18)
							{
								this.m_ChargeTime *= 0.9;
							}
							this.m_nextUpdateTime = gameTime + this.m_ChargeTime;
						}
					}
				}
				else
				{
					bool flag19 = gameTime >= this.m_nextUpdateTime;
					bool flag20 = flag19;
					if (flag20)
					{
						this.m_arrowValue = this.FindAimableItemInInventory();
						bool flag21 = this.m_arrowValue != 0;
						bool flag22 = flag21 && this.m_componentChaseBehavior.Target != null;
						bool flag23 = flag22;
						if (flag23)
						{
							bool throwFromHead2 = this.ThrowFromHead;
							bool flag24 = throwFromHead2;
							Vector3 vector2;
							if (flag24)
							{
								vector2 = this.m_componentCreature.ComponentCreatureModel.EyePosition;
							}
							else
							{
								vector2 = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
							}
							this.m_distance = (this.m_componentChaseBehavior.Target.ComponentBody.Position - vector2).Length();
							bool flag25 = this.m_distance >= this.m_minDistance && this.m_distance <= this.m_maxDistance;
							bool flag26 = flag25;
							if (flag26)
							{
								this.m_isCharging = true;
								float num2 = this.m_random.Float(0.8f, 1.2f);
								this.m_chargeDuration = (this.m_randomWaitMin + this.m_randomWaitMax) / 2f * num2;
								this.m_chargeStartTime = gameTime;
							}
						}
						else
						{
							this.m_ChargeTime = (double)this.m_random.Float(this.m_randomThrowMin, this.m_randomThrowMax);
							bool flag27 = this.m_distance < this.m_minDistance;
							bool flag28 = flag27;
							if (flag28)
							{
								this.m_ChargeTime *= 0.9;
							}
							this.m_nextUpdateTime = gameTime + this.m_ChargeTime;
						}
						bool flag29 = !this.m_isCharging && this.m_componentModel != null;
						bool flag30 = flag29;
						if (flag30)
						{
							ComponentHumanModel componentHumanModel = this.m_componentModel as ComponentHumanModel;
							bool flag31 = componentHumanModel != null;
							bool flag32 = flag31;
							if (flag32)
							{
								componentHumanModel.m_handAngles2 = Vector2.Lerp(componentHumanModel.m_handAngles2, new Vector2(0f, componentHumanModel.m_handAngles2.Y), 5f * dt);
							}
						}
					}
				}
			}
		}

		// Token: 0x0600002D RID: 45 RVA: 0x000048CC File Offset: 0x00002ACC
		private void TransitionToState(string stateName)
		{
			this.m_currentStateName = stateName;
			this.m_stateMachine.TransitionTo(stateName);
		}

		// Token: 0x0600002E RID: 46 RVA: 0x000048E4 File Offset: 0x00002AE4
		private void Idle_Update()
		{
			bool flag = this.m_componentChaseBehavior.Target == null;
			if (!flag)
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_componentChaseBehavior.Target.ComponentBody.Position);
				bool flag2 = num <= this.MeleeRange;
				if (flag2)
				{
					ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindMeleeWeapon();
					bool flag3 = weaponInfo.Type > ComponentInvShooterBehavior.WeaponType.None;
					if (flag3)
					{
						this.m_weaponInfo = weaponInfo;
						this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
						return;
					}
				}
				ComponentInvShooterBehavior.WeaponInfo weaponInfo2 = this.FindReadyRangedWeapon();
				bool flag4 = weaponInfo2.Type == ComponentInvShooterBehavior.WeaponType.None;
				if (flag4)
				{
					weaponInfo2 = this.FindReloadableRangedWeapon();
				}
				bool flag5 = weaponInfo2.Type == ComponentInvShooterBehavior.WeaponType.None;
				if (flag5)
				{
					weaponInfo2 = this.FindThrowableWeapon();
				}
				bool flag6 = weaponInfo2.Type != ComponentInvShooterBehavior.WeaponType.None && num >= this.m_minDistance && num <= this.m_maxDistance;
				if (flag6)
				{
					this.m_weaponInfo = weaponInfo2;
					this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
					this.TransitionToState(this.IsWeaponReady(this.m_weaponInfo) ? "Aiming" : "Reloading");
				}
			}
		}

		// Token: 0x0600002F RID: 47 RVA: 0x00004A22 File Offset: 0x00002C22
		private void Aiming_Enter()
		{
			this.m_aimStartTime = this.m_subsystemTime.GameTime;
			this.m_bowDraw = 0;
			this.m_aimDuration = 0f;
		}

		// Token: 0x06000030 RID: 48 RVA: 0x00004A48 File Offset: 0x00002C48
		private void Aiming_Update()
		{
			bool flag = this.m_componentChaseBehavior.Target == null;
			if (flag)
			{
				this.TransitionToState("Idle");
			}
			else
			{
				this.ApplyAimingAnimation();
				int activeSlotIndex = this.m_componentInventory.ActiveSlotIndex;
				int slotValue = this.m_componentInventory.GetSlotValue(activeSlotIndex);
				int num = Terrain.ExtractData(slotValue);
				int num2 = slotValue;
				bool flag2 = this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Bow;
				if (flag2)
				{
					float num3 = (float)((this.m_subsystemTime.GameTime - this.m_aimStartTime) / (double)this.m_aimDuration);
					this.m_bowDraw = MathUtils.Min((int)(num3 * 16f), 15);
					num2 = Terrain.ReplaceData(slotValue, BowBlock.SetDraw(num, this.m_bowDraw));
				}
				else
				{
					bool flag3 = this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Musket && !MusketBlock.GetHammerState(num) && this.m_subsystemTime.GameTime > this.m_aimStartTime + 0.5;
					if (flag3)
					{
						num2 = Terrain.ReplaceData(slotValue, MusketBlock.SetHammerState(num, true));
						this.m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, this.m_random.Float(-0.1f, 0.1f), this.m_componentCreature.ComponentBody.Position, 3f, false);
					}
				}
				bool flag4 = num2 != slotValue;
				if (flag4)
				{
					this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
					this.m_componentInventory.AddSlotItems(activeSlotIndex, num2, 1);
				}
				bool flag5 = this.m_subsystemTime.GameTime > this.m_aimStartTime + (double)this.m_aimDuration;
				if (flag5)
				{
					this.TransitionToState("Fire");
				}
			}
		}

		// Token: 0x06000031 RID: 49 RVA: 0x00004BF0 File Offset: 0x00002DF0
		private void Fire_Enter()
		{
			bool flag = this.m_componentChaseBehavior.Target != null;
			if (flag)
			{
				this.PerformRangedFireAction();
			}
			this.m_fireStateEndTime = this.m_subsystemTime.GameTime + 0.2;
		}

		// Token: 0x06000032 RID: 50 RVA: 0x00004C34 File Offset: 0x00002E34
		private void Fire_Update()
		{
			this.ApplyRecoilAnimation();
			bool flag = this.m_subsystemTime.GameTime >= this.m_fireStateEndTime;
			if (flag)
			{
				this.m_nextCombatUpdateTime = this.m_subsystemTime.GameTime + 0.55;
				this.TransitionToState("Reloading");
			}
		}

		// Token: 0x06000033 RID: 51 RVA: 0x00004C8C File Offset: 0x00002E8C
		private void Fire_Leave()
		{
			bool flag = this.m_componentModel != null;
			if (flag)
			{
				this.m_componentModel.AimHandAngleOrder = 0f;
				this.m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				this.m_componentModel.InHandItemRotationOrder = Vector3.Zero;
			}
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00004CDC File Offset: 0x00002EDC
		private void Reloading_Update()
		{
			bool flag = this.CanReloadWeapon(this.m_weaponInfo);
			if (flag)
			{
				this.TryReloadWeapon(this.m_weaponInfo);
			}
			else
			{
				ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindMeleeWeapon();
				bool flag2 = weaponInfo.Type > ComponentInvShooterBehavior.WeaponType.None;
				if (flag2)
				{
					this.m_weaponInfo = weaponInfo;
					this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
				}
			}
			this.TransitionToState("Idle");
		}

		// Token: 0x06000035 RID: 53 RVA: 0x00004D50 File Offset: 0x00002F50
		private void ApplyAimingAnimation()
		{
			bool flag = this.m_aimDuration == 0f;
			if (flag)
			{
				switch (this.m_weaponInfo.Type)
				{
					case ComponentInvShooterBehavior.WeaponType.Bow:
						this.m_aimDuration = this.m_random.Float(1.2f, 1.8f);
						break;
					case ComponentInvShooterBehavior.WeaponType.Crossbow:
						this.m_aimDuration = this.m_random.Float(0.8f, 1.2f);
						break;
					case ComponentInvShooterBehavior.WeaponType.Musket:
						this.m_aimDuration = this.m_random.Float(1f, 1.5f);
						break;
					case ComponentInvShooterBehavior.WeaponType.ItemsLauncher:
						this.m_aimDuration = this.m_random.Float(0.3f, 0.6f);
						break;
					default:
						this.m_aimDuration = this.m_random.Float(0.8f, 1.2f);
						break;
				}
			}
			bool flag2 = this.m_componentChaseBehavior.Target != null && this.m_componentModel != null;
			if (flag2)
			{
				this.m_componentModel.LookAtOrder = new Vector3?(this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition);
			}
			bool flag3 = this.m_componentModel != null;
			if (flag3)
			{
				switch (this.m_weaponInfo.Type)
				{
					case ComponentInvShooterBehavior.WeaponType.Throwable:
						this.m_componentModel.AimHandAngleOrder = 1.6f;
						break;
					case ComponentInvShooterBehavior.WeaponType.Bow:
						this.m_componentModel.AimHandAngleOrder = 1.2f;
						this.m_componentModel.InHandItemRotationOrder = new Vector3(0f, -0.2f, 0f);
						break;
					case ComponentInvShooterBehavior.WeaponType.Crossbow:
						this.m_componentModel.AimHandAngleOrder = 1.3f;
						this.m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
						this.m_componentModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
						break;
					case ComponentInvShooterBehavior.WeaponType.Musket:
						this.m_componentModel.AimHandAngleOrder = 1.4f;
						this.m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
						this.m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						break;
					case ComponentInvShooterBehavior.WeaponType.ItemsLauncher:
						this.m_componentModel.AimHandAngleOrder = 1.4f;
						this.m_componentModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
						this.m_componentModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
						break;
				}
			}
		}

		// Token: 0x06000036 RID: 54 RVA: 0x00005004 File Offset: 0x00003204
		private void ApplyRecoilAnimation()
		{
			bool flag = this.m_componentModel != null;
			if (flag)
			{
				this.m_componentModel.AimHandAngleOrder *= 1.1f;
				this.m_componentModel.InHandItemOffsetOrder -= new Vector3(0f, 0f, 0.05f);
			}
		}

		// Token: 0x06000037 RID: 55 RVA: 0x00005064 File Offset: 0x00003264
		private void PerformRangedFireAction()
		{
			int activeSlotIndex = this.m_componentInventory.ActiveSlotIndex;
			int slotValue = this.m_componentInventory.GetSlotValue(activeSlotIndex);
			bool flag = slotValue == 0;
			if (!flag)
			{
				Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 eyePosition2 = this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				float num = Vector3.Distance(eyePosition, eyePosition2);
				Vector3 vector = Vector3.Normalize(eyePosition2 - eyePosition);
				int num2 = Terrain.ExtractData(slotValue);
				int num3 = slotValue;
				switch (this.m_weaponInfo.Type)
				{
					case ComponentInvShooterBehavior.WeaponType.Bow:
						{
							ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(num2);
							bool flag2 = arrowType != null;
							if (flag2)
							{
								Vector3 vector2 = (vector + this.m_random.Vector3(0.05f) + new Vector3(0f, 0.15f * (num / 20f), 0f)) * MathUtils.Lerp(0f, 28f, (float)Math.Pow((double)((float)this.m_bowDraw / 15f), 0.75));
								this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(false, false), 0, ArrowBlock.SetArrowType(0, arrowType.Value)), eyePosition, vector2, Vector3.Zero, this.m_componentCreature);
								this.m_subsystemAudio.PlaySound("Audio/Bow", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 4f, false);
							}
							num3 = Terrain.ReplaceData(slotValue, BowBlock.SetDraw(BowBlock.SetArrowType(num2, null), 0));
							break;
						}
					case ComponentInvShooterBehavior.WeaponType.Crossbow:
						{
							ArrowBlock.ArrowType? arrowType2 = CrossbowBlock.GetArrowType(num2);
							bool flag3 = arrowType2 != null;
							if (flag3)
							{
								Vector3 vector3 = (vector + this.m_random.Vector3(0.02f) + new Vector3(0f, 0.1f * (num / 30f), 0f)) * 38f;
								this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(false, false), 0, ArrowBlock.SetArrowType(0, arrowType2.Value)), eyePosition, vector3, Vector3.Zero, this.m_componentCreature);
								this.m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 4f, false);
							}
							num3 = Terrain.ReplaceData(slotValue, CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(num2, null), 0));
							break;
						}
					case ComponentInvShooterBehavior.WeaponType.Musket:
						{
							bool flag4 = MusketBlock.GetLoadState(num2) == MusketBlock.LoadState.Loaded && MusketBlock.GetHammerState(num2);
							if (flag4)
							{
								BulletBlock.BulletType valueOrDefault = MusketBlock.GetBulletType(num2).GetValueOrDefault();
								int num4 = BulletBlock.SetBulletType(0, valueOrDefault);
								this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<BulletBlock>(false, false), 0, num4), eyePosition, vector * 120f, Vector3.Zero, this.m_componentCreature);
								this.m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 10f, false);
							}
							num3 = Terrain.ReplaceData(slotValue, MusketBlock.SetLoadState(num2, 0));
							break;
						}
					case ComponentInvShooterBehavior.WeaponType.ItemsLauncher:
						{
							bool flag5 = ItemsLauncherBlock.GetFuel(num2) > 0;
							if (flag5)
							{
								List<int> list = new List<int>();
								List<int> list2 = new List<int>();
								for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
								{
									bool flag6 = i != activeSlotIndex && this.m_componentInventory.GetSlotCount(i) > 0;
									if (flag6)
									{
										list.Add(this.m_componentInventory.GetSlotValue(i));
										list2.Add(i);
										bool flag7 = list.Count >= 5;
										if (flag7)
										{
											break;
										}
									}
								}
								bool flag8 = list.Count > 0;
								if (flag8)
								{
									int num5 = ItemsLauncherBlock.GetSpeedLevel(num2);
									int num6 = ItemsLauncherBlock.GetSpreadLevel(num2);
									bool flag9 = num5 == 0;
									if (flag9)
									{
										num5 = 3;
									}
									bool flag10 = num6 == 0;
									if (flag10)
									{
										num6 = 1;
									}
									float num7 = ComponentInvShooterBehavior.m_speedValues[num5 - 1];
									float num8 = ComponentInvShooterBehavior.m_spreadValues[num6 - 1];
									this.m_subsystemAudio.PlaySound("Audio/Items/ItemLauncher/Item Cannon Fire", 0.5f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 10f, false);
									int num9 = Math.Min(5, list.Count);
									for (int j = 0; j < num9; j++)
									{
										Vector3 vector4 = Vector3.Normalize(vector + num8 * this.m_random.Vector3(0.3f));
										Vector3 vector5 = vector4 * num7;
										this.m_subsystemProjectiles.FireProjectile(list[j], eyePosition, vector5, Vector3.Zero, this.m_componentCreature);
										this.m_componentInventory.RemoveSlotItems(list2[j], 1);
										bool flag11 = j == num9 - 1;
										if (flag11)
										{
											this.m_subsystemAudio.PlaySound("Audio/Items/ItemLauncher/AutoCannonFire", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 15f, false);
										}
									}
									int fuel = ItemsLauncherBlock.GetFuel(num2) - 1;
									int num10 = ItemsLauncherBlock.SetFuel(num2, fuel);
									num3 = Terrain.ReplaceData(slotValue, num10);
								}
							}
							break;
						}
				}
				bool flag12 = this.m_weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.Throwable && this.m_weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.ItemsLauncher;
				if (flag12)
				{
					this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
					this.m_componentInventory.AddSlotItems(activeSlotIndex, num3, 1);
				}
				else
				{
					bool flag13 = this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.ItemsLauncher && num3 != slotValue;
					if (flag13)
					{
						this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
						this.m_componentInventory.AddSlotItems(activeSlotIndex, num3, 1);
					}
				}
			}
		}

		// Token: 0x06000038 RID: 56 RVA: 0x00005674 File Offset: 0x00003874
		private void FireProjectile()
		{
			bool throwFromHead = this.ThrowFromHead;
			Vector3 vector;
			if (throwFromHead)
			{
				vector = this.m_componentCreature.ComponentCreatureModel.EyePosition;
			}
			else
			{
				vector = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
			}
			Vector3 vector2 = this.m_componentChaseBehavior.Target.ComponentBody.Position - vector;
			this.m_distance = vector2.Length();
			float num = 30f;
			float num2 = MathUtils.Clamp(this.m_distance / 12f, 0.6f, 1.8f);
			float num3 = num * num2;
			Vector3 vector3 = Vector3.Normalize(vector2);
			float num4 = MathUtils.Lerp(1f, 3f, this.m_distance / 25f);
			Vector3 vector4 = vector3 * num3 + new Vector3(0f, num4, 0f);
			this.m_subsystemProjectiles.FireProjectile(this.m_arrowValue, vector, vector4, Vector3.Zero, this.m_componentCreature);
			bool flag = this.m_componentModel != null;
			if (flag)
			{
				ComponentHumanModel componentHumanModel = this.m_componentModel as ComponentHumanModel;
				bool flag2 = componentHumanModel != null;
				if (flag2)
				{
					componentHumanModel.m_handAngles2 = new Vector2(MathUtils.DegToRad(-90f), componentHumanModel.m_handAngles2.Y);
				}
			}
			bool flag3 = !string.IsNullOrEmpty(this.ThrowingSound);
			if (flag3)
			{
				float num5 = this.m_random.Float(-0.1f, 0.1f);
				this.m_subsystemAudio.PlaySound(this.ThrowingSound, 1f, num5, vector, this.ThrowingSoundDistance, 0.1f);
			}
			bool discountFromInventory = this.DiscountFromInventory;
			if (discountFromInventory)
			{
				this.RemoveAimableItemFromInventory(this.m_arrowValue);
			}
		}

		// Token: 0x06000039 RID: 57 RVA: 0x000058A4 File Offset: 0x00003AA4
		private int FindAimableItemInInventory()
		{
			bool flag = this.m_specialThrowableItemValues.Count > 0;
			bool flag2 = flag;
			int result;
			if (flag2)
			{
				int index = this.m_random.Int(0, this.m_specialThrowableItemValues.Count - 1);
				result = this.m_specialThrowableItemValues[index];
			}
			else
			{
				bool flag3 = this.m_componentInventory == null;
				bool flag4 = flag3;
				if (flag4)
				{
					result = 0;
				}
				else
				{
					List<int> list = new List<int>();
					for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
					{
						int slotValue = this.m_componentInventory.GetSlotValue(i);
						bool flag5 = slotValue != 0;
						bool flag6 = flag5;
						if (flag6)
						{
							int num = Terrain.ExtractContents(slotValue);
							bool flag7 = this.m_excludedItems.Contains(num);
							bool flag8 = !flag7;
							if (flag8)
							{
								Block block = BlocksManager.Blocks[num];
								bool flag9 = block.IsAimable_(slotValue);
								bool flag10 = flag9;
								if (flag10)
								{
									bool selectRandomThrowableItems = this.SelectRandomThrowableItems;
									bool flag11 = !selectRandomThrowableItems;
									if (flag11)
									{
										return slotValue;
									}
									list.Add(slotValue);
								}
							}
						}
					}
					bool flag12 = list.Count > 0;
					bool flag13 = flag12;
					if (flag13)
					{
						int index2 = this.m_random.Int(0, list.Count - 1);
						result = list[index2];
					}
					else
					{
						result = 0;
					}
				}
			}
			return result;
		}

		// Token: 0x0600003A RID: 58 RVA: 0x00005A14 File Offset: 0x00003C14
		private void RemoveAimableItemFromInventory(int value)
		{
			bool flag = this.m_componentInventory == null;
			bool flag2 = !flag;
			if (flag2)
			{
				for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
				{
					bool flag3 = this.m_componentInventory.GetSlotValue(i) == value && this.m_componentInventory.GetSlotCount(i) > 0;
					bool flag4 = flag3;
					if (flag4)
					{
						this.m_componentInventory.RemoveSlotItems(i, 1);
						break;
					}
				}
			}
		}

		// Token: 0x0600003B RID: 59 RVA: 0x00005A90 File Offset: 0x00003C90
		private void TryReloadWeapon(ComponentInvShooterBehavior.WeaponInfo weaponToReload)
		{
			bool flag = !this.DiscountFromInventory || weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.None;
			if (!flag)
			{
				bool flag2 = weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow || weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Crossbow;
				if (flag2)
				{
					ArrowBlock.ArrowType[] array = (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow) ? new SubsystemBowBlockBehavior().m_supportedArrowTypes : new SubsystemCrossbowBlockBehavior().m_supportedArrowTypes;
					int blockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);
					for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
					{
						int slotValue = this.m_componentInventory.GetSlotValue(i);
						bool flag3 = Terrain.ExtractContents(slotValue) == blockIndex;
						if (flag3)
						{
							ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(slotValue));
							bool flag4 = false;
							foreach (ArrowBlock.ArrowType arrowType2 in array)
							{
								bool flag5 = arrowType2 == arrowType;
								if (flag5)
								{
									flag4 = true;
									break;
								}
							}
							bool flag6 = flag4;
							if (flag6)
							{
								int num = Terrain.ExtractData(weaponToReload.WeaponValue);
								int num2 = (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow) ? BowBlock.SetArrowType(num, new ArrowBlock.ArrowType?(arrowType)) : CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(num, new ArrowBlock.ArrowType?(arrowType)), 15);
								this.m_componentInventory.RemoveSlotItems(weaponToReload.WeaponSlot, 1);
								this.m_componentInventory.AddSlotItems(weaponToReload.WeaponSlot, Terrain.ReplaceData(weaponToReload.WeaponValue, num2), 1);
								this.m_componentInventory.RemoveSlotItems(i, 1);
								break;
							}
						}
					}
				}
				else
				{
					bool flag7 = weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Musket;
					if (flag7)
					{
						int num3 = this.FindItemSlotByContents(109);
						int num4 = this.FindItemSlotByContents(205);
						int num6;
						int num5 = this.FindBulletSlot(out num6);
						bool flag8 = num3 != -1 && num4 != -1 && num5 != -1;
						if (flag8)
						{
							BulletBlock.BulletType bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(num6));
							this.m_componentInventory.RemoveSlotItems(num3, 1);
							this.m_componentInventory.RemoveSlotItems(num4, 1);
							this.m_componentInventory.RemoveSlotItems(num5, 1);
							int num7 = MusketBlock.SetLoadState(Terrain.ExtractData(weaponToReload.WeaponValue), MusketBlock.LoadState.Loaded);
							num7 = MusketBlock.SetBulletType(num7, new BulletBlock.BulletType?(bulletType));
							this.m_componentInventory.RemoveSlotItems(weaponToReload.WeaponSlot, 1);
							this.m_componentInventory.AddSlotItems(weaponToReload.WeaponSlot, Terrain.ReplaceData(weaponToReload.WeaponValue, num7), 1);
						}
					}
					else
					{
						bool flag9 = weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.ItemsLauncher;
						if (flag9)
						{
							int num8 = this.FindItemSlotByContents(ItemsLauncherBlock.Index);
							bool flag10 = num8 != -1;
							if (flag10)
							{
								int slotValue2 = this.m_componentInventory.GetSlotValue(num8);
								int data = Terrain.ExtractData(slotValue2);
								int fuel = ItemsLauncherBlock.GetFuel(data);
								bool flag11 = fuel < 15;
								if (flag11)
								{
									int fuel2 = Math.Min(fuel + 5, 15);
									int num9 = ItemsLauncherBlock.SetFuel(data, fuel2);
									int num10 = Terrain.ReplaceData(slotValue2, num9);
									this.m_componentInventory.RemoveSlotItems(num8, 1);
									this.m_componentInventory.AddSlotItems(num8, num10, 1);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x0600003C RID: 60 RVA: 0x00005DB4 File Offset: 0x00003FB4
		private int FindItemSlotByContents(int contents)
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				bool flag = Terrain.ExtractContents(this.m_componentInventory.GetSlotValue(i)) == contents;
				if (flag)
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x0600003D RID: 61 RVA: 0x00005E00 File Offset: 0x00004000
		private int FindBulletSlot(out int bulletValue)
		{
			int blockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = Terrain.ExtractContents(slotValue) == blockIndex;
				if (flag)
				{
					bulletValue = slotValue;
					return i;
				}
			}
			bulletValue = 0;
			return -1;
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00005E64 File Offset: 0x00004064
		private bool IsWeaponReady(ComponentInvShooterBehavior.WeaponInfo weaponInfo)
		{
			bool flag = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Throwable;
				if (flag2)
				{
					result = true;
				}
				else
				{
					int slotValue = this.m_componentInventory.GetSlotValue(weaponInfo.WeaponSlot);
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int num = Terrain.ExtractData(slotValue);
					bool flag3 = block is BowBlock;
					if (flag3)
					{
						result = (BowBlock.GetArrowType(num) != null);
					}
					else
					{
						bool flag4 = block is CrossbowBlock;
						if (flag4)
						{
							result = (CrossbowBlock.GetArrowType(num) != null && CrossbowBlock.GetDraw(num) == 15);
						}
						else
						{
							bool flag5 = block is MusketBlock;
							if (flag5)
							{
								result = (MusketBlock.GetLoadState(num) == MusketBlock.LoadState.Loaded);
							}
							else
							{
								bool flag6 = block is ItemsLauncherBlock;
								result = (flag6 && ItemsLauncherBlock.GetFuel(num) > 0);
							}
						}
					}
				}
			}
			return result;
		}

		// Token: 0x0600003F RID: 63 RVA: 0x00005F58 File Offset: 0x00004158
		private void ProactiveReloadCheck()
		{
			ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindReloadableRangedWeapon();
			bool flag = weaponInfo.Type > ComponentInvShooterBehavior.WeaponType.None;
			if (flag)
			{
				bool flag2 = this.m_componentInventory.ActiveSlotIndex != weaponInfo.WeaponSlot;
				if (flag2)
				{
					this.m_componentInventory.ActiveSlotIndex = weaponInfo.WeaponSlot;
				}
				this.TryReloadWeapon(weaponInfo);
			}
		}

		// Token: 0x06000040 RID: 64 RVA: 0x00005FB4 File Offset: 0x000041B4
		private bool HasAmmoForWeapon(ComponentInvShooterBehavior.WeaponInfo weaponInfo)
		{
			bool flag = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Throwable;
				if (flag2)
				{
					result = true;
				}
				else
				{
					bool flag3 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Bow || weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Crossbow;
					if (flag3)
					{
						int blockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);
						for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
						{
							bool flag4 = Terrain.ExtractContents(this.m_componentInventory.GetSlotValue(i)) == blockIndex;
							if (flag4)
							{
								return true;
							}
						}
						result = false;
					}
					else
					{
						bool flag5 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Musket;
						if (flag5)
						{
							bool flag6 = this.FindItemSlotByContents(109) != -1;
							bool flag7 = this.FindItemSlotByContents(205) != -1;
							int num2;
							int num = this.FindBulletSlot(out num2);
							result = (flag6 && flag7 && num != -1);
						}
						else
						{
							bool flag8 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.ItemsLauncher;
							if (flag8)
							{
								bool flag9 = ItemsLauncherBlock.GetFuel(weaponInfo.WeaponValue) <= 0;
								if (flag9)
								{
									result = false;
								}
								else
								{
									for (int j = 0; j < this.m_componentInventory.SlotsCount; j++)
									{
										bool flag10 = j != weaponInfo.WeaponSlot && this.m_componentInventory.GetSlotCount(j) > 0;
										if (flag10)
										{
											return true;
										}
									}
									result = false;
								}
							}
							else
							{
								result = false;
							}
						}
					}
				}
			}
			return result;
		}

		// Token: 0x06000041 RID: 65 RVA: 0x00006128 File Offset: 0x00004328
		private bool CanReloadWeapon(ComponentInvShooterBehavior.WeaponInfo weaponInfo)
		{
			return this.HasAmmoForWeapon(weaponInfo);
		}

		// Token: 0x06000042 RID: 66 RVA: 0x00006144 File Offset: 0x00004344
		private ComponentInvShooterBehavior.WeaponInfo FindReadyRangedWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0;
				if (flag)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int num = Terrain.ExtractData(slotValue);
					bool flag2 = block is BowBlock && BowBlock.GetArrowType(num) != null;
					ComponentInvShooterBehavior.WeaponInfo result;
					if (flag2)
					{
						result = new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Bow
						};
					}
					else
					{
						bool flag3 = block is CrossbowBlock && CrossbowBlock.GetArrowType(num) != null && CrossbowBlock.GetDraw(num) == 15;
						if (flag3)
						{
							result = new ComponentInvShooterBehavior.WeaponInfo
							{
								WeaponSlot = i,
								WeaponValue = slotValue,
								Type = ComponentInvShooterBehavior.WeaponType.Crossbow
							};
						}
						else
						{
							bool flag4 = block is MusketBlock && MusketBlock.GetLoadState(num) == MusketBlock.LoadState.Loaded;
							if (flag4)
							{
								result = new ComponentInvShooterBehavior.WeaponInfo
								{
									WeaponSlot = i,
									WeaponValue = slotValue,
									Type = ComponentInvShooterBehavior.WeaponType.Musket
								};
							}
							else
							{
								bool flag5 = block is ItemsLauncherBlock && ItemsLauncherBlock.GetFuel(num) > 0;
								if (!flag5)
								{
									goto IL_15F;
								}
								result = new ComponentInvShooterBehavior.WeaponInfo
								{
									WeaponSlot = i,
									WeaponValue = slotValue,
									Type = ComponentInvShooterBehavior.WeaponType.ItemsLauncher
								};
							}
						}
					}
					return result;
				}
			IL_15F:;
			}
			return this.FindMeleeWeapon();
		}

		// Token: 0x06000043 RID: 67 RVA: 0x000062D8 File Offset: 0x000044D8
		private ComponentInvShooterBehavior.WeaponInfo FindReloadableRangedWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0;
				if (flag)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int num = Terrain.ExtractData(slotValue);
					bool flag2 = block is BowBlock && BowBlock.GetArrowType(num) == null;
					ComponentInvShooterBehavior.WeaponInfo result;
					if (flag2)
					{
						result = new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Bow
						};
					}
					else
					{
						bool flag3 = block is CrossbowBlock && (CrossbowBlock.GetArrowType(num) == null || CrossbowBlock.GetDraw(num) < 15);
						if (flag3)
						{
							result = new ComponentInvShooterBehavior.WeaponInfo
							{
								WeaponSlot = i,
								WeaponValue = slotValue,
								Type = ComponentInvShooterBehavior.WeaponType.Crossbow
							};
						}
						else
						{
							bool flag4 = block is MusketBlock && MusketBlock.GetLoadState(num) == 0;
							if (flag4)
							{
								result = new ComponentInvShooterBehavior.WeaponInfo
								{
									WeaponSlot = i,
									WeaponValue = slotValue,
									Type = ComponentInvShooterBehavior.WeaponType.Musket
								};
							}
							else
							{
								bool flag5 = block is ItemsLauncherBlock;
								if (!flag5)
								{
									goto IL_159;
								}
								result = new ComponentInvShooterBehavior.WeaponInfo
								{
									WeaponSlot = i,
									WeaponValue = slotValue,
									Type = ComponentInvShooterBehavior.WeaponType.ItemsLauncher
								};
							}
						}
					}
					return result;
				}
			IL_159:;
			}
			return default(ComponentInvShooterBehavior.WeaponInfo);
		}

		// Token: 0x06000044 RID: 68 RVA: 0x0000646C File Offset: 0x0000466C
		private ComponentInvShooterBehavior.WeaponInfo FindThrowableWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is SpearBlock;
				if (flag)
				{
					return new ComponentInvShooterBehavior.WeaponInfo
					{
						WeaponSlot = i,
						WeaponValue = slotValue,
						Type = ComponentInvShooterBehavior.WeaponType.Throwable
					};
				}
			}
			return default(ComponentInvShooterBehavior.WeaponInfo);
		}

		// Token: 0x06000045 RID: 69 RVA: 0x000064F8 File Offset: 0x000046F8
		private ComponentInvShooterBehavior.WeaponInfo FindMeleeWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0;
				if (flag)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					bool flag2 = block is MacheteBlock || block is WoodenClubBlock || block is StoneClubBlock || block is AxeBlock || block is SpearBlock;
					if (flag2)
					{
						return new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Melee
						};
					}
				}
			}
			return default(ComponentInvShooterBehavior.WeaponInfo);
		}

		// Token: 0x04000036 RID: 54
		private static readonly float[] m_speedValues = new float[]
		{
			50f,
			75f,
			100f
		};

		// Token: 0x04000037 RID: 55
		private static readonly float[] m_spreadValues = new float[]
		{
			0.001f,
			0.01f,
			0.05f
		};

		// Token: 0x04000038 RID: 56
		public ComponentCreature m_componentCreature;

		// Token: 0x04000039 RID: 57
		public float MeleeRange;

		// Token: 0x0400003A RID: 58
		public ComponentChaseBehavior m_componentChaseBehavior;

		// Token: 0x0400003B RID: 59
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x0400003C RID: 60
		public StateMachine m_stateMachine = new StateMachine();

		// Token: 0x0400003D RID: 61
		public SubsystemTime m_subsystemTime;

		// Token: 0x0400003E RID: 62
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x0400003F RID: 63
		public Random m_random = new Random();

		// Token: 0x04000040 RID: 64
		public int m_arrowValue;

		// Token: 0x04000041 RID: 65
		public double m_nextUpdateTime;

		// Token: 0x04000042 RID: 66
		public double m_ChargeTime;

		// Token: 0x04000043 RID: 67
		public float m_distance;

		// Token: 0x04000044 RID: 68
		public bool DiscountFromInventory;

		// Token: 0x04000045 RID: 69
		public string MinMaxRandomChargeTime;

		// Token: 0x04000046 RID: 70
		public float m_randomThrowMin;

		// Token: 0x04000047 RID: 71
		public float m_randomThrowMax;

		// Token: 0x04000048 RID: 72
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x04000049 RID: 73
		public string ThrowingSound;

		// Token: 0x0400004A RID: 74
		public float ThrowingSoundDistance;

		// Token: 0x0400004B RID: 75
		public bool SelectRandomThrowableItems;

		// Token: 0x0400004C RID: 76
		public string SpecialThrowableItem;

		// Token: 0x0400004D RID: 77
		public int m_specialThrowableItemValue;

		// Token: 0x0400004E RID: 78
		public List<int> m_specialThrowableItemValues = new List<int>();

		// Token: 0x0400004F RID: 79
		public float m_minDistance;

		// Token: 0x04000050 RID: 80
		public float m_maxDistance;

		// Token: 0x04000051 RID: 81
		public string MinMaxDistance;

		// Token: 0x04000052 RID: 82
		public float m_randomWaitMin;

		// Token: 0x04000053 RID: 83
		public float m_randomWaitMax;

		// Token: 0x04000054 RID: 84
		public string MinMaxRandomWaitTime;

		// Token: 0x04000055 RID: 85
		public double m_chargeStartTime;

		// Token: 0x04000056 RID: 86
		public bool m_isCharging;

		// Token: 0x04000057 RID: 87
		public float m_chargeDuration;

		// Token: 0x04000058 RID: 88
		public bool ThrowFromHead;

		// Token: 0x04000059 RID: 89
		public ComponentCreatureModel m_componentModel;

		// Token: 0x0400005A RID: 90
		public List<int> m_excludedItems = new List<int>();

		// Token: 0x0400005B RID: 91
		public ComponentInventory m_componentInventory;

		// Token: 0x0400005C RID: 92
		private string m_currentStateName;

		// Token: 0x0400005D RID: 93
		private double m_nextCombatUpdateTime;

		// Token: 0x0400005E RID: 94
		private double m_nextProactiveReloadTime;

		// Token: 0x0400005F RID: 95
		private double m_fireStateEndTime;

		// Token: 0x04000060 RID: 96
		private ComponentInvShooterBehavior.WeaponInfo m_weaponInfo;

		// Token: 0x04000061 RID: 97
		private double m_aimStartTime;

		// Token: 0x04000062 RID: 98
		private float m_aimDuration;

		// Token: 0x04000063 RID: 99
		private int m_bowDraw;

		// Token: 0x0200002D RID: 45
		public enum WeaponType
		{
			// Token: 0x040000C4 RID: 196
			None,
			// Token: 0x040000C5 RID: 197
			Throwable,
			// Token: 0x040000C6 RID: 198
			Bow,
			// Token: 0x040000C7 RID: 199
			Crossbow,
			// Token: 0x040000C8 RID: 200
			Musket,
			// Token: 0x040000C9 RID: 201
			ItemsLauncher,
			// Token: 0x040000CA RID: 202
			Melee
		}

		// Token: 0x0200002E RID: 46
		public struct WeaponInfo
		{
			// Token: 0x040000CB RID: 203
			public int WeaponSlot;

			// Token: 0x040000CC RID: 204
			public int WeaponValue;

			// Token: 0x040000CD RID: 205
			public ComponentInvShooterBehavior.WeaponType Type;
		}
	}
}
