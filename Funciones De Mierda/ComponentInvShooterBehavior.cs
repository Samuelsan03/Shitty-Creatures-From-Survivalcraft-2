using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000007 RID: 7
	[NullableContext(1)]
	[Nullable(0)]
	public class ComponentInvShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000023 RID: 35 RVA: 0x00003774 File Offset: 0x00001974
		public int UpdateOrder
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000024 RID: 36 RVA: 0x00003788 File Offset: 0x00001988
		public override float ImportanceLevel
		{
			get
			{
				return 0f;
			}
		}

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x06000025 RID: 37 RVA: 0x000037A0 File Offset: 0x000019A0
		UpdateOrder IUpdateable.UpdateOrder
		{
			get
			{
				return this.m_subsystemProjectiles.UpdateOrder;
			}
		}

		// Token: 0x06000026 RID: 38 RVA: 0x000037C0 File Offset: 0x000019C0
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
			bool flag16 = flag;
			if (flag16)
			{
				string[] array = value2.Split(',', StringSplitOptions.None);
				foreach (string text in array)
				{
					string text2 = text.Trim();
					bool flag2 = text2.Contains(":");
					bool flag17 = flag2;
					if (flag17)
					{
						string[] array2 = text2.Split(':', StringSplitOptions.None);
						string text3 = array2[0].Trim();
						string s = array2[1].Trim();
						int num;
						bool flag3 = int.TryParse(s, out num);
						bool flag18 = flag3;
						if (flag18)
						{
							int blockIndex = BlocksManager.GetBlockIndex(text3, false);
							bool flag4 = blockIndex >= 0;
							bool flag19 = flag4;
							if (flag19)
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
						bool flag5 = int.TryParse(text2, out num2);
						bool flag20 = flag5;
						if (flag20)
						{
							this.m_specialThrowableItemValues.Add(Terrain.MakeBlockValue(num2));
						}
						else
						{
							int blockIndex2 = BlocksManager.GetBlockIndex(text2, false);
							bool flag6 = blockIndex2 >= 0;
							bool flag21 = flag6;
							if (flag21)
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
			bool flag7 = !string.IsNullOrEmpty(value);
			bool flag22 = flag7;
			if (flag22)
			{
				string[] array3 = value.Split(';', StringSplitOptions.None);
				foreach (string text4 in array3)
				{
					int num3;
					bool flag8 = int.TryParse(text4, out num3);
					bool flag23 = flag8;
					if (flag23)
					{
						this.m_excludedItems.Add(Terrain.ExtractContents(Terrain.MakeBlockValue(num3)));
					}
					else
					{
						int blockIndex3 = BlocksManager.GetBlockIndex(text4, false);
						bool flag9 = blockIndex3 >= 0;
						bool flag24 = flag9;
						if (flag24)
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
			string[] array4 = this.MinMaxRandomWaitTime.Split(';', StringSplitOptions.None);
			bool flag10 = array4.Length >= 2 && float.TryParse(array4[0], out this.m_randomWaitMin) && float.TryParse(array4[1], out this.m_randomWaitMax);
			bool flag25 = !flag10;
			if (flag25)
			{
				this.m_randomWaitMin = 0.3f;
				this.m_randomWaitMax = 0.3f;
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
			string[] array5 = this.MinMaxDistance.Split(';', StringSplitOptions.None);
			bool flag11 = array5.Length >= 2 && float.TryParse(array5[0], out this.m_minDistance) && float.TryParse(array5[1], out this.m_maxDistance);
			bool flag26 = !flag11;
			if (flag26)
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
			bool flag12 = !string.IsNullOrEmpty(this.SpecialThrowableItem);
			bool flag27 = flag12;
			if (flag27)
			{
				int num4;
				bool flag13 = int.TryParse(this.SpecialThrowableItem, out num4);
				bool flag28 = flag13;
				if (flag28)
				{
					this.m_specialThrowableItemValue = Terrain.MakeBlockValue(num4);
				}
				else
				{
					int blockIndex4 = BlocksManager.GetBlockIndex(this.SpecialThrowableItem, false);
					bool flag14 = blockIndex4 >= 0;
					bool flag29 = flag14;
					if (flag29)
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
			string[] array6 = this.MinMaxRandomChargeTime.Split(';', StringSplitOptions.None);
			bool flag15 = array6.Length >= 2 && float.TryParse(array6[0], out this.m_randomThrowMin) && float.TryParse(array6[1], out this.m_randomThrowMax);
			bool flag30 = !flag15;
			if (flag30)
			{
				this.m_randomThrowMin = 0.3f;
				this.m_randomThrowMax = 0.3f;
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

		// Token: 0x06000027 RID: 39 RVA: 0x00003F74 File Offset: 0x00002174
		public void Update(float dt)
		{
			bool flag = this.m_componentCreature.ComponentHealth.Health <= 0f;
			bool flag15 = !flag;
			if (flag15)
			{
				bool flag16 = this.m_subsystemTime.GameTime >= this.m_nextProactiveReloadTime;
				if (flag16)
				{
					this.m_nextProactiveReloadTime = this.m_subsystemTime.GameTime + 0.3;
					bool flag17 = this.m_currentStateName == "Idle";
					if (flag17)
					{
						this.ProactiveReloadCheck();
					}
				}
				bool flag18 = this.m_subsystemTime.GameTime >= this.m_nextCombatUpdateTime;
				if (flag18)
				{
					this.m_stateMachine.Update();
				}
				double gameTime = this.m_subsystemTime.GameTime;
				bool isCharging = this.m_isCharging;
				bool flag19 = isCharging;
				if (flag19)
				{
					bool flag2 = this.m_componentChaseBehavior.Target != null;
					float num = 0f;
					bool flag3 = flag2;
					bool flag20 = flag3;
					if (flag20)
					{
						bool throwFromHead = this.ThrowFromHead;
						bool flag21 = throwFromHead;
						Vector3 vector;
						if (flag21)
						{
							vector = this.m_componentCreature.ComponentCreatureModel.EyePosition;
						}
						else
						{
							vector = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
						}
						num = Vector3.Distance(vector, this.m_componentChaseBehavior.Target.ComponentBody.Position);
					}
					bool flag22 = flag2 && num >= this.m_minDistance && num <= this.m_maxDistance;
					bool flag4;
					if (flag22)
					{
						ComponentHealth componentHealth = this.m_componentChaseBehavior.Target.Entity.FindComponent<ComponentHealth>();
						flag4 = (componentHealth != null && componentHealth.Health <= 0f);
					}
					else
					{
						flag4 = true;
					}
					bool flag5 = flag4;
					bool flag23 = flag5;
					if (flag23)
					{
						this.m_isCharging = false;
					}
					else
					{
						bool flag6 = gameTime >= this.m_chargeStartTime + (double)this.m_chargeDuration;
						bool flag24 = flag6;
						if (flag24)
						{
							this.FireProjectile();
							this.m_isCharging = false;
							this.m_ChargeTime = (double)this.m_random.Float(this.m_randomThrowMin, this.m_randomThrowMax);
							bool flag7 = this.m_distance < this.m_minDistance;
							bool flag25 = flag7;
							if (flag25)
							{
								this.m_ChargeTime *= 0.9;
							}
							this.m_nextUpdateTime = gameTime + this.m_ChargeTime;
						}
					}
				}
				else
				{
					bool flag8 = gameTime >= this.m_nextUpdateTime;
					bool flag26 = flag8;
					if (flag26)
					{
						this.m_arrowValue = this.FindAimableItemInInventory();
						bool flag9 = this.m_arrowValue != 0;
						bool flag10 = flag9 && this.m_componentChaseBehavior.Target != null;
						bool flag27 = flag10;
						if (flag27)
						{
							bool throwFromHead2 = this.ThrowFromHead;
							bool flag28 = throwFromHead2;
							Vector3 vector2;
							if (flag28)
							{
								vector2 = this.m_componentCreature.ComponentCreatureModel.EyePosition;
							}
							else
							{
								vector2 = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
							}
							this.m_distance = (this.m_componentChaseBehavior.Target.ComponentBody.Position - vector2).Length();
							bool flag11 = this.m_distance >= this.m_minDistance && this.m_distance <= this.m_maxDistance;
							bool flag29 = flag11;
							if (flag29)
							{
								this.m_isCharging = true;
								float chargeVariation = this.m_random.Float(0.8f, 1.2f);
								this.m_chargeDuration = (this.m_randomWaitMin + this.m_randomWaitMax) / 2f * chargeVariation;
								this.m_chargeStartTime = gameTime;
							}
						}
						else
						{
							this.m_ChargeTime = (double)this.m_random.Float(this.m_randomThrowMin, this.m_randomThrowMax);
							bool flag12 = this.m_distance < this.m_minDistance;
							bool flag30 = flag12;
							if (flag30)
							{
								this.m_ChargeTime *= 0.9;
							}
							this.m_nextUpdateTime = gameTime + this.m_ChargeTime;
						}
						bool flag13 = !this.m_isCharging && this.m_componentModel != null;
						bool flag31 = flag13;
						if (flag31)
						{
							ComponentHumanModel componentHumanModel = this.m_componentModel as ComponentHumanModel;
							bool flag14 = componentHumanModel != null;
							bool flag32 = flag14;
							if (flag32)
							{
								componentHumanModel.m_handAngles2 = Vector2.Lerp(componentHumanModel.m_handAngles2, new Vector2(0f, componentHumanModel.m_handAngles2.Y), 5f * dt);
							}
						}
					}
				}
			}
		}

		// Token: 0x06000028 RID: 40 RVA: 0x000044C4 File Offset: 0x000026C4
		private void TransitionToState(string stateName)
		{
			this.m_currentStateName = stateName;
			this.m_stateMachine.TransitionTo(stateName);
		}

		// Token: 0x06000029 RID: 41 RVA: 0x000044DC File Offset: 0x000026DC
		private void Idle_Update()
		{
			bool flag = this.m_componentChaseBehavior.Target == null;
			if (!flag)
			{
				float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_componentChaseBehavior.Target.ComponentBody.Position);
				bool flag2 = distance <= this.MeleeRange;
				if (flag2)
				{
					ComponentInvShooterBehavior.WeaponInfo meleeWeapon = this.FindMeleeWeapon();
					bool flag3 = meleeWeapon.Type > ComponentInvShooterBehavior.WeaponType.None;
					if (flag3)
					{
						this.m_weaponInfo = meleeWeapon;
						this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
						return;
					}
				}
				ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindReadyRangedWeapon();
				bool flag4 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None;
				if (flag4)
				{
					weaponInfo = this.FindReloadableRangedWeapon();
				}
				bool flag5 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None;
				if (flag5)
				{
					weaponInfo = this.FindThrowableWeapon();
				}
				bool flag6 = weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.None && distance >= this.m_minDistance && distance <= this.m_maxDistance;
				if (flag6)
				{
					this.m_weaponInfo = weaponInfo;
					this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
					this.TransitionToState(this.IsWeaponReady(this.m_weaponInfo) ? "Aiming" : "Reloading");
				}
			}
		}

		// Token: 0x0600002A RID: 42 RVA: 0x0000461A File Offset: 0x0000281A
		private void Aiming_Enter()
		{
			this.m_aimStartTime = this.m_subsystemTime.GameTime;
			this.m_bowDraw = 0;
			this.m_aimDuration = 0f;
		}

		// Token: 0x0600002B RID: 43 RVA: 0x00004640 File Offset: 0x00002840
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
				int data = Terrain.ExtractData(slotValue);
				int newValue = slotValue;
				bool flag2 = this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Bow;
				if (flag2)
				{
					float progress = (float)((this.m_subsystemTime.GameTime - this.m_aimStartTime) / (double)this.m_aimDuration);
					this.m_bowDraw = MathUtils.Min((int)(progress * 16f), 15);
					newValue = Terrain.ReplaceData(slotValue, BowBlock.SetDraw(data, this.m_bowDraw));
				}
				else
				{
					bool flag3 = this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Musket && !MusketBlock.GetHammerState(data) && this.m_subsystemTime.GameTime > this.m_aimStartTime + 0.5;
					if (flag3)
					{
						newValue = Terrain.ReplaceData(slotValue, MusketBlock.SetHammerState(data, true));
						this.m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, this.m_random.Float(-0.1f, 0.1f), this.m_componentCreature.ComponentBody.Position, 3f, false);
					}
				}
				bool flag4 = newValue != slotValue;
				if (flag4)
				{
					this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
					this.m_componentInventory.AddSlotItems(activeSlotIndex, newValue, 1);
				}
				bool flag5 = this.m_subsystemTime.GameTime > this.m_aimStartTime + (double)this.m_aimDuration;
				if (flag5)
				{
					this.TransitionToState("Fire");
				}
			}
		}

		// Token: 0x0600002C RID: 44 RVA: 0x000047E8 File Offset: 0x000029E8
		private void Fire_Enter()
		{
			bool flag = this.m_componentChaseBehavior.Target != null;
			if (flag)
			{
				this.PerformRangedFireAction();
			}
			this.m_fireStateEndTime = this.m_subsystemTime.GameTime + 0.2;
		}

		// Token: 0x0600002D RID: 45 RVA: 0x0000482C File Offset: 0x00002A2C
		private void Fire_Update()
		{
			this.ApplyRecoilAnimation();
			bool flag = this.m_subsystemTime.GameTime >= this.m_fireStateEndTime;
			if (flag)
			{
				this.m_nextCombatUpdateTime = this.m_subsystemTime.GameTime + 0.3;
				this.TransitionToState("Reloading");
			}
		}

		// Token: 0x0600002E RID: 46 RVA: 0x00004884 File Offset: 0x00002A84
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

		// Token: 0x0600002F RID: 47 RVA: 0x000048D4 File Offset: 0x00002AD4
		private void Reloading_Update()
		{
			bool flag = this.CanReloadWeapon(this.m_weaponInfo);
			if (flag)
			{
				this.TryReloadWeapon(this.m_weaponInfo);
			}
			else
			{
				ComponentInvShooterBehavior.WeaponInfo alternativeWeapon = this.FindMeleeWeapon();
				bool flag2 = alternativeWeapon.Type > ComponentInvShooterBehavior.WeaponType.None;
				if (flag2)
				{
					this.m_weaponInfo = alternativeWeapon;
					this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
				}
			}
			this.TransitionToState("Idle");
		}

		// Token: 0x06000030 RID: 48 RVA: 0x00004948 File Offset: 0x00002B48
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
				}
			}
		}

		// Token: 0x06000031 RID: 49 RVA: 0x00004B80 File Offset: 0x00002D80
		private void ApplyRecoilAnimation()
		{
			bool flag = this.m_componentModel != null;
			if (flag)
			{
				this.m_componentModel.AimHandAngleOrder *= 1.1f;
				this.m_componentModel.InHandItemOffsetOrder -= new Vector3(0f, 0f, 0.05f);
			}
		}

		// Token: 0x06000032 RID: 50 RVA: 0x00004BE0 File Offset: 0x00002DE0
		private void PerformRangedFireAction()
		{
			int activeSlotIndex = this.m_componentInventory.ActiveSlotIndex;
			int slotValue = this.m_componentInventory.GetSlotValue(activeSlotIndex);
			bool flag = slotValue == 0;
			if (!flag)
			{
				Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 targetPosition = this.m_componentChaseBehavior.Target.ComponentBody.Position + new Vector3(0f, this.m_componentChaseBehavior.Target.ComponentBody.StanceBoxSize.Y * 0.75f, 0f);
				float distance = Vector3.Distance(eyePosition, targetPosition);
				Vector3 direction = Vector3.Normalize(targetPosition - eyePosition);
				int data = Terrain.ExtractData(slotValue);
				int newValue = slotValue;
				switch (this.m_weaponInfo.Type)
				{
				case ComponentInvShooterBehavior.WeaponType.Bow:
				{
					ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);
					bool flag2 = arrowType != null;
					if (flag2)
					{
						Vector3 velocity = (direction + this.m_random.Vector3(0.05f) + new Vector3(0f, 0.15f * (distance / 20f), 0f)) * MathUtils.Lerp(0f, 28f, (float)Math.Pow((double)((float)this.m_bowDraw / 15f), 0.75));
						this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(false, false), 0, ArrowBlock.SetArrowType(0, arrowType.Value)), eyePosition, velocity, Vector3.Zero, this.m_componentCreature);
						this.m_subsystemAudio.PlaySound("Audio/Bow", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 4f, false);
					}
					newValue = Terrain.ReplaceData(slotValue, BowBlock.SetDraw(BowBlock.SetArrowType(data, null), 0));
					break;
				}
				case ComponentInvShooterBehavior.WeaponType.Crossbow:
				{
					ArrowBlock.ArrowType? arrowType2 = CrossbowBlock.GetArrowType(data);
					bool flag3 = arrowType2 != null;
					if (flag3)
					{
						Vector3 velocity2 = (direction + this.m_random.Vector3(0.02f) + new Vector3(0f, 0.1f * (distance / 30f), 0f)) * 38f;
						this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(false, false), 0, ArrowBlock.SetArrowType(0, arrowType2.Value)), eyePosition, velocity2, Vector3.Zero, this.m_componentCreature);
						this.m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 4f, false);
					}
					newValue = Terrain.ReplaceData(slotValue, CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(data, null), 0));
					break;
				}
				case ComponentInvShooterBehavior.WeaponType.Musket:
				{
					bool flag4 = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetHammerState(data);
					if (flag4)
					{
						BulletBlock.BulletType bulletType = MusketBlock.GetBulletType(data).GetValueOrDefault();
						int bulletData = BulletBlock.SetBulletType(0, bulletType);
						this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<BulletBlock>(false, false), 0, bulletData), eyePosition, direction * 120f, Vector3.Zero, this.m_componentCreature);
						this.m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 10f, false);
					}
					newValue = Terrain.ReplaceData(slotValue, MusketBlock.SetLoadState(data, MusketBlock.LoadState.Empty));
					break;
				}
				}
				bool flag5 = this.m_weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.Throwable;
				if (flag5)
				{
					this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
					this.m_componentInventory.AddSlotItems(activeSlotIndex, newValue, 1);
				}
			}
		}

		// Token: 0x06000033 RID: 51 RVA: 0x00004FA4 File Offset: 0x000031A4
		private void FireProjectile()
		{
			bool throwFromHead = this.ThrowFromHead;
			Vector3 position;
			if (throwFromHead)
			{
				position = this.m_componentCreature.ComponentCreatureModel.EyePosition;
			}
			else
			{
				position = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
			}
			Vector3 targetDirection = this.m_componentChaseBehavior.Target.ComponentBody.Position - position;
			this.m_distance = targetDirection.Length();
			float baseSpeed = 30f;
			float distanceFactor = MathUtils.Clamp(this.m_distance / 12f, 0.6f, 1.8f);
			float speed = baseSpeed * distanceFactor;
			Vector3 direction = Vector3.Normalize(targetDirection);
			float gravityCompensation = MathUtils.Lerp(1f, 3f, this.m_distance / 25f);
			Vector3 velocity = direction * speed + new Vector3(0f, gravityCompensation, 0f);
			this.m_subsystemProjectiles.FireProjectile(this.m_arrowValue, position, velocity, Vector3.Zero, this.m_componentCreature);
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
				float pitch = this.m_random.Float(-0.1f, 0.1f);
				this.m_subsystemAudio.PlaySound(this.ThrowingSound, 1f, pitch, position, this.ThrowingSoundDistance, 0.1f);
			}
			bool discountFromInventory = this.DiscountFromInventory;
			if (discountFromInventory)
			{
				this.RemoveAimableItemFromInventory(this.m_arrowValue);
			}
		}

		// Token: 0x06000034 RID: 52 RVA: 0x000051D4 File Offset: 0x000033D4
		private int FindAimableItemInInventory()
		{
			bool flag = this.m_specialThrowableItemValues.Count > 0;
			bool flag7 = flag;
			int result;
			if (flag7)
			{
				int index = this.m_random.Int(0, this.m_specialThrowableItemValues.Count - 1);
				result = this.m_specialThrowableItemValues[index];
			}
			else
			{
				bool flag2 = this.m_componentInventory == null;
				bool flag8 = flag2;
				if (flag8)
				{
					result = 0;
				}
				else
				{
					List<int> list = new List<int>();
					for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
					{
						int slotValue = this.m_componentInventory.GetSlotValue(i);
						bool flag3 = slotValue != 0;
						bool flag9 = flag3;
						if (flag9)
						{
							int num = Terrain.ExtractContents(slotValue);
							bool flag4 = this.m_excludedItems.Contains(num);
							bool flag10 = !flag4;
							if (flag10)
							{
								Block block = BlocksManager.Blocks[num];
								bool flag5 = block.IsAimable_(slotValue);
								bool flag11 = flag5;
								if (flag11)
								{
									bool selectRandomThrowableItems = this.SelectRandomThrowableItems;
									bool flag12 = !selectRandomThrowableItems;
									if (flag12)
									{
										return slotValue;
									}
									list.Add(slotValue);
								}
							}
						}
					}
					bool flag6 = list.Count > 0;
					bool flag13 = flag6;
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

		// Token: 0x06000035 RID: 53 RVA: 0x00005344 File Offset: 0x00003544
		private void RemoveAimableItemFromInventory(int value)
		{
			bool flag = this.m_componentInventory == null;
			bool flag3 = !flag;
			if (flag3)
			{
				for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
				{
					bool flag2 = this.m_componentInventory.GetSlotValue(i) == value && this.m_componentInventory.GetSlotCount(i) > 0;
					bool flag4 = flag2;
					if (flag4)
					{
						this.m_componentInventory.RemoveSlotItems(i, 1);
						break;
					}
				}
			}
		}

		// Token: 0x06000036 RID: 54 RVA: 0x000053C0 File Offset: 0x000035C0
		private void TryReloadWeapon(ComponentInvShooterBehavior.WeaponInfo weaponToReload)
		{
			bool flag = !this.DiscountFromInventory || weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.None;
			if (!flag)
			{
				bool flag2 = weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow || weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Crossbow;
				if (flag2)
				{
					ArrowBlock.ArrowType[] supportedArrows = (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow) ? new SubsystemBowBlockBehavior().m_supportedArrowTypes : new SubsystemCrossbowBlockBehavior().m_supportedArrowTypes;
					int arrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);
					for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
					{
						int slotValue = this.m_componentInventory.GetSlotValue(i);
						bool flag3 = Terrain.ExtractContents(slotValue) == arrowBlockIndex;
						if (flag3)
						{
							ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(slotValue));
							bool isSupported = false;
							foreach (ArrowBlock.ArrowType supportedType in supportedArrows)
							{
								bool flag4 = supportedType == arrowType;
								if (flag4)
								{
									isSupported = true;
									break;
								}
							}
							bool flag5 = isSupported;
							if (flag5)
							{
								int weaponData = Terrain.ExtractData(weaponToReload.WeaponValue);
								int newData = (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow) ? BowBlock.SetArrowType(weaponData, new ArrowBlock.ArrowType?(arrowType)) : CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(weaponData, new ArrowBlock.ArrowType?(arrowType)), 15);
								this.m_componentInventory.RemoveSlotItems(weaponToReload.WeaponSlot, 1);
								this.m_componentInventory.AddSlotItems(weaponToReload.WeaponSlot, Terrain.ReplaceData(weaponToReload.WeaponValue, newData), 1);
								this.m_componentInventory.RemoveSlotItems(i, 1);
								break;
							}
						}
					}
				}
				else
				{
					bool flag6 = weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Musket;
					if (flag6)
					{
						int powderSlot = this.FindItemSlotByContents(109);
						int fuseSlot = this.FindItemSlotByContents(205);
						int bulletValue;
						int bulletSlot = this.FindBulletSlot(out bulletValue);
						bool flag7 = powderSlot != -1 && fuseSlot != -1 && bulletSlot != -1;
						if (flag7)
						{
							BulletBlock.BulletType bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(bulletValue));
							this.m_componentInventory.RemoveSlotItems(powderSlot, 1);
							this.m_componentInventory.RemoveSlotItems(fuseSlot, 1);
							this.m_componentInventory.RemoveSlotItems(bulletSlot, 1);
							int weaponData2 = MusketBlock.SetLoadState(Terrain.ExtractData(weaponToReload.WeaponValue), MusketBlock.LoadState.Loaded);
							weaponData2 = MusketBlock.SetBulletType(weaponData2, new BulletBlock.BulletType?(bulletType));
							this.m_componentInventory.RemoveSlotItems(weaponToReload.WeaponSlot, 1);
							this.m_componentInventory.AddSlotItems(weaponToReload.WeaponSlot, Terrain.ReplaceData(weaponToReload.WeaponValue, weaponData2), 1);
						}
					}
				}
			}
		}

		// Token: 0x06000037 RID: 55 RVA: 0x0000563C File Offset: 0x0000383C
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

		// Token: 0x06000038 RID: 56 RVA: 0x00005688 File Offset: 0x00003888
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

		// Token: 0x06000039 RID: 57 RVA: 0x000056EC File Offset: 0x000038EC
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
					int data = Terrain.ExtractData(slotValue);
					bool flag3 = block is BowBlock;
					if (flag3)
					{
						result = (BowBlock.GetArrowType(data) != null);
					}
					else
					{
						bool flag4 = block is CrossbowBlock;
						if (flag4)
						{
							result = (CrossbowBlock.GetArrowType(data) != null && CrossbowBlock.GetDraw(data) == 15);
						}
						else
						{
							result = (block is MusketBlock && MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded);
						}
					}
				}
			}
			return result;
		}

		// Token: 0x0600003A RID: 58 RVA: 0x000057BC File Offset: 0x000039BC
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

		// Token: 0x0600003B RID: 59 RVA: 0x00005818 File Offset: 0x00003A18
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
						int arrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);
						for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
						{
							bool flag4 = Terrain.ExtractContents(this.m_componentInventory.GetSlotValue(i)) == arrowBlockIndex;
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
							bool hasPowder = this.FindItemSlotByContents(109) != -1;
							bool hasFuse = this.FindItemSlotByContents(205) != -1;
							int num;
							int bulletSlot = this.FindBulletSlot(out num);
							result = (hasPowder && hasFuse && bulletSlot != -1);
						}
						else
						{
							result = false;
						}
					}
				}
			}
			return result;
		}

		// Token: 0x0600003C RID: 60 RVA: 0x00005910 File Offset: 0x00003B10
		private bool CanReloadWeapon(ComponentInvShooterBehavior.WeaponInfo weaponInfo)
		{
			return this.HasAmmoForWeapon(weaponInfo);
		}

		// Token: 0x0600003D RID: 61 RVA: 0x0000592C File Offset: 0x00003B2C
		private ComponentInvShooterBehavior.WeaponInfo FindReadyRangedWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0;
				if (flag)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int data = Terrain.ExtractData(slotValue);
					bool flag2 = block is BowBlock && BowBlock.GetArrowType(data) != null;
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
						bool flag3 = block is CrossbowBlock && CrossbowBlock.GetArrowType(data) != null && CrossbowBlock.GetDraw(data) == 15;
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
							bool flag4 = block is MusketBlock && MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded;
							if (!flag4)
							{
								goto IL_11A;
							}
							result = new ComponentInvShooterBehavior.WeaponInfo
							{
								WeaponSlot = i,
								WeaponValue = slotValue,
								Type = ComponentInvShooterBehavior.WeaponType.Musket
							};
						}
					}
					return result;
				}
				IL_11A:;
			}
			return this.FindMeleeWeapon();
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00005A7C File Offset: 0x00003C7C
		private ComponentInvShooterBehavior.WeaponInfo FindReloadableRangedWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0;
				if (flag)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int data = Terrain.ExtractData(slotValue);
					bool flag2 = block is BowBlock && BowBlock.GetArrowType(data) == null;
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
						bool flag3 = block is CrossbowBlock && (CrossbowBlock.GetArrowType(data) == null || CrossbowBlock.GetDraw(data) < 15);
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
							bool flag4 = block is MusketBlock && MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Empty;
							if (!flag4)
							{
								goto IL_120;
							}
							result = new ComponentInvShooterBehavior.WeaponInfo
							{
								WeaponSlot = i,
								WeaponValue = slotValue,
								Type = ComponentInvShooterBehavior.WeaponType.Musket
							};
						}
					}
					return result;
				}
				IL_120:;
			}
			return default(ComponentInvShooterBehavior.WeaponInfo);
		}

		// Token: 0x0600003F RID: 63 RVA: 0x00005BD8 File Offset: 0x00003DD8
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

		// Token: 0x06000040 RID: 64 RVA: 0x00005C64 File Offset: 0x00003E64
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

		// Token: 0x0400002E RID: 46
		public ComponentCreature m_componentCreature;

		// Token: 0x0400002F RID: 47
		public float MeleeRange;

		// Token: 0x04000030 RID: 48
		public ComponentChaseBehavior m_componentChaseBehavior;

		// Token: 0x04000031 RID: 49
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000032 RID: 50
		public StateMachine m_stateMachine = new StateMachine();

		// Token: 0x04000033 RID: 51
		public SubsystemTime m_subsystemTime;

		// Token: 0x04000034 RID: 52
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x04000035 RID: 53
		public Random m_random = new Random();

		// Token: 0x04000036 RID: 54
		public int m_arrowValue;

		// Token: 0x04000037 RID: 55
		public double m_nextUpdateTime;

		// Token: 0x04000038 RID: 56
		public double m_ChargeTime;

		// Token: 0x04000039 RID: 57
		public float m_distance;

		// Token: 0x0400003A RID: 58
		public bool DiscountFromInventory;

		// Token: 0x0400003B RID: 59
		public string MinMaxRandomChargeTime;

		// Token: 0x0400003C RID: 60
		public float m_randomThrowMin;

		// Token: 0x0400003D RID: 61
		public float m_randomThrowMax;

		// Token: 0x0400003E RID: 62
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x0400003F RID: 63
		public string ThrowingSound;

		// Token: 0x04000040 RID: 64
		public float ThrowingSoundDistance;

		// Token: 0x04000041 RID: 65
		public bool SelectRandomThrowableItems;

		// Token: 0x04000042 RID: 66
		public string SpecialThrowableItem;

		// Token: 0x04000043 RID: 67
		public int m_specialThrowableItemValue;

		// Token: 0x04000044 RID: 68
		public List<int> m_specialThrowableItemValues = new List<int>();

		// Token: 0x04000045 RID: 69
		public float m_minDistance;

		// Token: 0x04000046 RID: 70
		public float m_maxDistance;

		// Token: 0x04000047 RID: 71
		public string MinMaxDistance;

		// Token: 0x04000048 RID: 72
		public float m_randomWaitMin;

		// Token: 0x04000049 RID: 73
		public float m_randomWaitMax;

		// Token: 0x0400004A RID: 74
		public string MinMaxRandomWaitTime;

		// Token: 0x0400004B RID: 75
		public double m_chargeStartTime;

		// Token: 0x0400004C RID: 76
		public bool m_isCharging;

		// Token: 0x0400004D RID: 77
		public float m_chargeDuration;

		// Token: 0x0400004E RID: 78
		public bool ThrowFromHead;

		// Token: 0x0400004F RID: 79
		public ComponentCreatureModel m_componentModel;

		// Token: 0x04000050 RID: 80
		public List<int> m_excludedItems = new List<int>();

		// Token: 0x04000051 RID: 81
		public ComponentInventory m_componentInventory;

		// Token: 0x04000052 RID: 82
		private string m_currentStateName;

		// Token: 0x04000053 RID: 83
		private double m_nextCombatUpdateTime;

		// Token: 0x04000054 RID: 84
		private double m_nextProactiveReloadTime;

		// Token: 0x04000055 RID: 85
		private double m_fireStateEndTime;

		// Token: 0x04000056 RID: 86
		private ComponentInvShooterBehavior.WeaponInfo m_weaponInfo;

		// Token: 0x04000057 RID: 87
		private double m_aimStartTime;

		// Token: 0x04000058 RID: 88
		private float m_aimDuration;

		// Token: 0x04000059 RID: 89
		private int m_bowDraw;

		// Token: 0x0200000E RID: 14
		[NullableContext(0)]
		public enum WeaponType
		{
			// Token: 0x0400009D RID: 157
			None,
			// Token: 0x0400009E RID: 158
			Throwable,
			// Token: 0x0400009F RID: 159
			Bow,
			// Token: 0x040000A0 RID: 160
			Crossbow,
			// Token: 0x040000A1 RID: 161
			Musket,
			// Token: 0x040000A2 RID: 162
			Melee
		}

		// Token: 0x0200000F RID: 15
		[NullableContext(0)]
		public struct WeaponInfo
		{
			// Token: 0x040000A3 RID: 163
			public int WeaponSlot;

			// Token: 0x040000A4 RID: 164
			public int WeaponValue;

			// Token: 0x040000A5 RID: 165
			public ComponentInvShooterBehavior.WeaponType Type;
		}
	}
}
