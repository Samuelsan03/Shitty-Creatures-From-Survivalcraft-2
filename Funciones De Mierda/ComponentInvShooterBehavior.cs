using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200001C RID: 28
	public class ComponentInvShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000011 RID: 17
		// (get) Token: 0x060000F1 RID: 241 RVA: 0x0000BA74 File Offset: 0x00009C74
		public int UpdateOrder
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000012 RID: 18
		// (get) Token: 0x060000F2 RID: 242 RVA: 0x0000BA88 File Offset: 0x00009C88
		public override float ImportanceLevel
		{
			get
			{
				return 0f;
			}
		}

		// Token: 0x17000013 RID: 19
		// (get) Token: 0x060000F3 RID: 243 RVA: 0x0000BAA0 File Offset: 0x00009CA0
		UpdateOrder IUpdateable.UpdateOrder
		{
			get
			{
				return this.m_subsystemProjectiles.UpdateOrder;
			}
		}

		// Token: 0x060000F4 RID: 244 RVA: 0x0000BAC0 File Offset: 0x00009CC0
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
			bool flag3 = flag2;
			if (flag3)
			{
				string[] array = value2.Split(',', StringSplitOptions.None);
				foreach (string text in array)
				{
					string text2 = text.Trim();
					bool flag4 = text2.Contains(":");
					bool flag5 = flag4;
					bool flag6 = flag5;
					if (flag6)
					{
						string[] array3 = text2.Split(':', StringSplitOptions.None);
						string blockName = array3[0].Trim();
						string s = array3[1].Trim();
						int data;
						bool flag7 = int.TryParse(s, out data);
						bool flag8 = flag7;
						bool flag9 = flag8;
						if (flag9)
						{
							int blockIndex = BlocksManager.GetBlockIndex(blockName, false);
							bool flag10 = blockIndex >= 0;
							bool flag11 = flag10;
							bool flag12 = flag11;
							if (flag12)
							{
								int item = Terrain.MakeBlockValue(blockIndex, 0, data);
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
						int contents;
						bool flag13 = int.TryParse(text2, out contents);
						bool flag14 = flag13;
						bool flag15 = flag14;
						if (flag15)
						{
							this.m_specialThrowableItemValues.Add(Terrain.MakeBlockValue(contents));
						}
						else
						{
							int blockIndex2 = BlocksManager.GetBlockIndex(text2, false);
							bool flag16 = blockIndex2 >= 0;
							bool flag17 = flag16;
							bool flag18 = flag17;
							if (flag18)
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
			bool flag19 = !string.IsNullOrEmpty(value);
			bool flag20 = flag19;
			bool flag21 = flag20;
			if (flag21)
			{
				string[] array4 = value.Split(';', StringSplitOptions.None);
				foreach (string text3 in array4)
				{
					int contents2;
					bool flag22 = int.TryParse(text3, out contents2);
					bool flag23 = flag22;
					bool flag24 = flag23;
					if (flag24)
					{
						this.m_excludedItems.Add(Terrain.ExtractContents(Terrain.MakeBlockValue(contents2)));
					}
					else
					{
						int blockIndex3 = BlocksManager.GetBlockIndex(text3, false);
						bool flag25 = blockIndex3 >= 0;
						bool flag26 = flag25;
						bool flag27 = flag26;
						if (flag27)
						{
							this.m_excludedItems.Add(blockIndex3);
						}
						else
						{
							Log.Warning("ExcludedThrowableItem '" + text3 + "' not found");
						}
					}
				}
			}
			string[] array6 = this.MinMaxRandomWaitTime.Split(';', StringSplitOptions.None);
			bool flag28 = array6.Length >= 2 && float.TryParse(array6[0], out this.m_randomWaitMin) && float.TryParse(array6[1], out this.m_randomWaitMax);
			bool flag29 = !flag28;
			bool flag30 = flag29;
			if (flag30)
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
			bool flag31 = array7.Length >= 2 && float.TryParse(array7[0], out this.m_minDistance) && float.TryParse(array7[1], out this.m_maxDistance);
			bool flag32 = !flag31;
			bool flag33 = flag32;
			if (flag33)
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
			bool flag34 = !string.IsNullOrEmpty(this.SpecialThrowableItem);
			bool flag35 = flag34;
			bool flag36 = flag35;
			if (flag36)
			{
				int contents3;
				bool flag37 = int.TryParse(this.SpecialThrowableItem, out contents3);
				bool flag38 = flag37;
				bool flag39 = flag38;
				if (flag39)
				{
					this.m_specialThrowableItemValue = Terrain.MakeBlockValue(contents3);
				}
				else
				{
					int blockIndex4 = BlocksManager.GetBlockIndex(this.SpecialThrowableItem, false);
					bool flag40 = blockIndex4 >= 0;
					bool flag41 = flag40;
					bool flag42 = flag41;
					if (flag42)
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
			bool flag43 = array8.Length >= 2 && float.TryParse(array8[0], out this.m_randomThrowMin) && float.TryParse(array8[1], out this.m_randomThrowMax);
			bool flag44 = !flag43;
			bool flag45 = flag44;
			if (flag45)
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

		// Token: 0x060000F5 RID: 245 RVA: 0x0000C2B4 File Offset: 0x0000A4B4
		public void Update(float dt)
		{
			bool flag = this.m_componentCreature.ComponentHealth.Health <= 0f;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = this.m_subsystemTime.GameTime >= this.m_nextProactiveReloadTime;
				bool flag5 = flag4;
				if (flag5)
				{
					this.m_nextProactiveReloadTime = this.m_subsystemTime.GameTime + 0.55;
					bool flag6 = this.m_currentStateName == "Idle";
					bool flag7 = flag6;
					if (flag7)
					{
						this.ProactiveReloadCheck();
					}
				}
				bool flag8 = this.m_subsystemTime.GameTime >= this.m_nextCombatUpdateTime;
				bool flag9 = flag8;
				if (flag9)
				{
					this.m_stateMachine.Update();
				}
				double gameTime = this.m_subsystemTime.GameTime;
				bool isCharging = this.m_isCharging;
				bool flag10 = isCharging;
				bool flag11 = flag10;
				if (flag11)
				{
					bool flag12 = this.m_componentChaseBehavior.Target != null;
					float num = 0f;
					bool flag13 = flag12;
					bool flag14 = flag13;
					bool flag15 = flag14;
					if (flag15)
					{
						bool throwFromHead = this.ThrowFromHead;
						bool flag16 = throwFromHead;
						bool flag17 = flag16;
						Vector3 v;
						if (flag17)
						{
							v = this.m_componentCreature.ComponentCreatureModel.EyePosition;
						}
						else
						{
							v = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
						}
						num = Vector3.Distance(v, this.m_componentChaseBehavior.Target.ComponentBody.Position);
					}
					bool flag18 = flag12 && num >= this.m_minDistance && num <= this.m_maxDistance;
					bool flag19 = flag18;
					bool flag20;
					if (flag19)
					{
						ComponentHealth componentHealth = this.m_componentChaseBehavior.Target.Entity.FindComponent<ComponentHealth>();
						flag20 = (componentHealth != null && componentHealth.Health <= 0f);
					}
					else
					{
						flag20 = true;
					}
					bool flag21 = flag20;
					bool flag22 = flag21;
					bool flag23 = flag22;
					if (flag23)
					{
						this.m_isCharging = false;
					}
					else
					{
						bool flag24 = gameTime >= this.m_chargeStartTime + (double)this.m_chargeDuration;
						bool flag25 = flag24;
						bool flag26 = flag25;
						if (flag26)
						{
							this.FireProjectile();
							this.m_isCharging = false;
							this.m_ChargeTime = (double)this.m_random.Float(this.m_randomThrowMin, this.m_randomThrowMax);
							bool flag27 = this.m_distance < this.m_minDistance;
							bool flag28 = flag27;
							bool flag29 = flag28;
							if (flag29)
							{
								this.m_ChargeTime *= 0.9;
							}
							this.m_nextUpdateTime = gameTime + this.m_ChargeTime;
						}
					}
				}
				else
				{
					bool flag30 = gameTime >= this.m_nextUpdateTime;
					bool flag31 = flag30;
					bool flag32 = flag31;
					if (flag32)
					{
						this.m_arrowValue = this.FindAimableItemInInventory();
						bool flag33 = this.m_arrowValue != 0;
						bool flag34 = flag33 && this.m_componentChaseBehavior.Target != null;
						bool flag35 = flag34;
						bool flag36 = flag35;
						if (flag36)
						{
							bool throwFromHead2 = this.ThrowFromHead;
							bool flag37 = throwFromHead2;
							bool flag38 = flag37;
							Vector3 v2;
							if (flag38)
							{
								v2 = this.m_componentCreature.ComponentCreatureModel.EyePosition;
							}
							else
							{
								v2 = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
							}
							this.m_distance = (this.m_componentChaseBehavior.Target.ComponentBody.Position - v2).Length();
							bool flag39 = this.m_distance >= this.m_minDistance && this.m_distance <= this.m_maxDistance;
							bool flag40 = flag39;
							bool flag41 = flag40;
							if (flag41)
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
							bool flag42 = this.m_distance < this.m_minDistance;
							bool flag43 = flag42;
							bool flag44 = flag43;
							if (flag44)
							{
								this.m_ChargeTime *= 0.9;
							}
							this.m_nextUpdateTime = gameTime + this.m_ChargeTime;
						}
						bool flag45 = !this.m_isCharging && this.m_componentModel != null;
						bool flag46 = flag45;
						bool flag47 = flag46;
						if (flag47)
						{
							ComponentHumanModel componentHumanModel = this.m_componentModel as ComponentHumanModel;
							bool flag48 = componentHumanModel != null;
							bool flag49 = flag48;
							bool flag50 = flag49;
							if (flag50)
							{
								componentHumanModel.m_handAngles2 = Vector2.Lerp(componentHumanModel.m_handAngles2, new Vector2(0f, componentHumanModel.m_handAngles2.Y), 5f * dt);
							}
						}
					}
				}
			}
		}

		// Token: 0x060000F6 RID: 246 RVA: 0x0000C850 File Offset: 0x0000AA50
		private void TransitionToState(string stateName)
		{
			this.m_currentStateName = stateName;
			this.m_stateMachine.TransitionTo(stateName);
		}

		// Token: 0x060000F7 RID: 247 RVA: 0x0000C868 File Offset: 0x0000AA68
		private void Idle_Update()
		{
			bool flag = this.m_componentChaseBehavior.Target == null;
			bool flag2 = !flag;
			if (flag2)
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_componentChaseBehavior.Target.ComponentBody.Position);
				bool flag3 = num <= this.MeleeRange;
				bool flag4 = flag3;
				if (flag4)
				{
					ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindMeleeWeapon();
					bool flag5 = weaponInfo.Type > ComponentInvShooterBehavior.WeaponType.None;
					bool flag6 = flag5;
					if (flag6)
					{
						this.m_weaponInfo = weaponInfo;
						this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
						return;
					}
				}
				ComponentInvShooterBehavior.WeaponInfo weaponInfo2 = this.FindReadyRangedWeapon();
				bool flag7 = weaponInfo2.Type == ComponentInvShooterBehavior.WeaponType.None;
				bool flag8 = flag7;
				if (flag8)
				{
					weaponInfo2 = this.FindReloadableRangedWeapon();
				}
				bool flag9 = weaponInfo2.Type == ComponentInvShooterBehavior.WeaponType.None;
				bool flag10 = flag9;
				if (flag10)
				{
					weaponInfo2 = this.FindThrowableWeapon();
				}
				bool flag11 = weaponInfo2.Type != ComponentInvShooterBehavior.WeaponType.None && num >= this.m_minDistance && num <= this.m_maxDistance;
				bool flag12 = flag11;
				if (flag12)
				{
					this.m_weaponInfo = weaponInfo2;
					this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
					this.TransitionToState(this.IsWeaponReady(this.m_weaponInfo) ? "Aiming" : "Reloading");
				}
			}
		}

		// Token: 0x060000F8 RID: 248 RVA: 0x0000C9C5 File Offset: 0x0000ABC5
		private void Aiming_Enter()
		{
			this.m_aimStartTime = this.m_subsystemTime.GameTime;
			this.m_bowDraw = 0;
			this.m_aimDuration = 0f;
		}

		// Token: 0x060000F9 RID: 249 RVA: 0x0000C9EC File Offset: 0x0000ABEC
		private void Aiming_Update()
		{
			bool flag = this.m_componentChaseBehavior.Target == null;
			bool flag2 = flag;
			if (flag2)
			{
				this.TransitionToState("Idle");
			}
			else
			{
				this.ApplyAimingAnimation();
				int activeSlotIndex = this.m_componentInventory.ActiveSlotIndex;
				int slotValue = this.m_componentInventory.GetSlotValue(activeSlotIndex);
				int data = Terrain.ExtractData(slotValue);
				int num = slotValue;
				bool flag3 = this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Bow;
				bool flag4 = flag3;
				if (flag4)
				{
					float num2 = (float)((this.m_subsystemTime.GameTime - this.m_aimStartTime) / (double)this.m_aimDuration);
					this.m_bowDraw = MathUtils.Min((int)(num2 * 16f), 15);
					num = Terrain.ReplaceData(slotValue, BowBlock.SetDraw(data, this.m_bowDraw));
				}
				else
				{
					bool flag5 = this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Musket && !MusketBlock.GetHammerState(data) && this.m_subsystemTime.GameTime > this.m_aimStartTime + 0.5;
					bool flag6 = flag5;
					if (flag6)
					{
						num = Terrain.ReplaceData(slotValue, MusketBlock.SetHammerState(data, true));
						this.m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, this.m_random.Float(-0.1f, 0.1f), this.m_componentCreature.ComponentBody.Position, 3f, false);
					}
				}
				bool flag7 = num != slotValue;
				bool flag8 = flag7;
				if (flag8)
				{
					this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
					this.m_componentInventory.AddSlotItems(activeSlotIndex, num, 1);
				}
				bool flag9 = this.m_subsystemTime.GameTime > this.m_aimStartTime + (double)this.m_aimDuration;
				bool flag10 = flag9;
				if (flag10)
				{
					this.TransitionToState("Fire");
				}
			}
		}

		// Token: 0x060000FA RID: 250 RVA: 0x0000CBB0 File Offset: 0x0000ADB0
		private void Fire_Enter()
		{
			bool flag = this.m_componentChaseBehavior.Target != null;
			bool flag2 = flag;
			if (flag2)
			{
				this.PerformRangedFireAction();
			}
			this.m_fireStateEndTime = this.m_subsystemTime.GameTime + 0.2;
		}

		// Token: 0x060000FB RID: 251 RVA: 0x0000CBF8 File Offset: 0x0000ADF8
		private void Fire_Update()
		{
			this.ApplyRecoilAnimation();
			bool flag = this.m_subsystemTime.GameTime >= this.m_fireStateEndTime;
			bool flag2 = flag;
			if (flag2)
			{
				this.m_nextCombatUpdateTime = this.m_subsystemTime.GameTime + 0.55;
				this.TransitionToState("Reloading");
			}
		}

		// Token: 0x060000FC RID: 252 RVA: 0x0000CC54 File Offset: 0x0000AE54
		private void Fire_Leave()
		{
			bool flag = this.m_componentModel != null;
			bool flag2 = flag;
			if (flag2)
			{
				this.m_componentModel.AimHandAngleOrder = 0f;
				this.m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				this.m_componentModel.InHandItemRotationOrder = Vector3.Zero;
			}
		}

		// Token: 0x060000FD RID: 253 RVA: 0x0000CCA8 File Offset: 0x0000AEA8
		private void Reloading_Update()
		{
			bool flag = this.CanReloadWeapon(this.m_weaponInfo);
			bool flag2 = flag;
			if (flag2)
			{
				this.TryReloadWeapon(this.m_weaponInfo);
			}
			else
			{
				ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindMeleeWeapon();
				bool flag3 = weaponInfo.Type > ComponentInvShooterBehavior.WeaponType.None;
				bool flag4 = flag3;
				if (flag4)
				{
					this.m_weaponInfo = weaponInfo;
					this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
				}
			}
			this.TransitionToState("Idle");
		}

		// Token: 0x060000FE RID: 254 RVA: 0x0000CD20 File Offset: 0x0000AF20
		private void ApplyAimingAnimation()
		{
			bool flag = this.m_aimDuration == 0f;
			bool flag2 = flag;
			if (flag2)
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
			bool flag3 = this.m_componentChaseBehavior.Target != null && this.m_componentModel != null;
			bool flag4 = flag3;
			if (flag4)
			{
				this.m_componentModel.LookAtOrder = new Vector3?(this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition);
			}
			bool flag5 = this.m_componentModel != null;
			bool flag6 = flag5;
			if (flag6)
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

		// Token: 0x060000FF RID: 255 RVA: 0x0000CFE0 File Offset: 0x0000B1E0
		private void ApplyRecoilAnimation()
		{
			bool flag = this.m_componentModel != null;
			bool flag2 = flag;
			if (flag2)
			{
				this.m_componentModel.AimHandAngleOrder *= 1.1f;
				this.m_componentModel.InHandItemOffsetOrder -= new Vector3(0f, 0f, 0.05f);
			}
		}

		// Token: 0x06000100 RID: 256 RVA: 0x0000D044 File Offset: 0x0000B244
		private void PerformRangedFireAction()
		{
			int activeSlotIndex = this.m_componentInventory.ActiveSlotIndex;
			int slotValue = this.m_componentInventory.GetSlotValue(activeSlotIndex);
			bool flag = slotValue == 0;
			bool flag2 = !flag;
			if (flag2)
			{
				Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 eyePosition2 = this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition;
				float num = Vector3.Distance(eyePosition, eyePosition2);
				Vector3 vector = Vector3.Normalize(eyePosition2 - eyePosition);
				int data = Terrain.ExtractData(slotValue);
				int num2 = slotValue;
				switch (this.m_weaponInfo.Type)
				{
					case ComponentInvShooterBehavior.WeaponType.Bow:
						{
							ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);
							bool flag3 = arrowType != null;
							bool flag4 = flag3;
							if (flag4)
							{
								Vector3 velocity = (vector + this.m_random.Vector3(0.05f) + new Vector3(0f, 0.15f * (num / 20f), 0f)) * MathUtils.Lerp(0f, 28f, (float)Math.Pow((double)((float)this.m_bowDraw / 15f), 0.75));
								this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(false, false), 0, ArrowBlock.SetArrowType(0, arrowType.Value)), eyePosition, velocity, Vector3.Zero, this.m_componentCreature);
								this.m_subsystemAudio.PlaySound("Audio/Bow", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 4f, false);
							}
							num2 = Terrain.ReplaceData(slotValue, BowBlock.SetDraw(BowBlock.SetArrowType(data, null), 0));
							break;
						}
					case ComponentInvShooterBehavior.WeaponType.Crossbow:
						{
							ArrowBlock.ArrowType? arrowType2 = CrossbowBlock.GetArrowType(data);
							bool flag5 = arrowType2 != null;
							bool flag6 = flag5;
							if (flag6)
							{
								Vector3 velocity2 = (vector + this.m_random.Vector3(0.02f) + new Vector3(0f, 0.1f * (num / 30f), 0f)) * 38f;
								this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(false, false), 0, ArrowBlock.SetArrowType(0, arrowType2.Value)), eyePosition, velocity2, Vector3.Zero, this.m_componentCreature);
								this.m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 4f, false);
							}
							num2 = Terrain.ReplaceData(slotValue, CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(data, null), 0));
							break;
						}
					case ComponentInvShooterBehavior.WeaponType.Musket:
						{
							bool flag7 = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetHammerState(data);
							bool flag8 = flag7;
							if (flag8)
							{
								BulletBlock.BulletType valueOrDefault = MusketBlock.GetBulletType(data).GetValueOrDefault();
								int data2 = BulletBlock.SetBulletType(0, valueOrDefault);
								this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<BulletBlock>(false, false), 0, data2), eyePosition, vector * 120f, Vector3.Zero, this.m_componentCreature);
								this.m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 10f, false);
							}
							num2 = Terrain.ReplaceData(slotValue, MusketBlock.SetLoadState(data, MusketBlock.LoadState.Empty));
							break;
						}
					case ComponentInvShooterBehavior.WeaponType.ItemsLauncher:
						{
							bool flag9 = ItemsLauncherBlock.GetFuel(data) > 0;
							bool flag10 = flag9;
							if (flag10)
							{
								List<int> list = new List<int>();
								List<int> list2 = new List<int>();
								for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
								{
									bool flag11 = i != activeSlotIndex && this.m_componentInventory.GetSlotCount(i) > 0;
									bool flag12 = flag11;
									if (flag12)
									{
										list.Add(this.m_componentInventory.GetSlotValue(i));
										list2.Add(i);
										bool flag13 = list.Count >= 5;
										bool flag14 = flag13;
										if (flag14)
										{
											break;
										}
									}
								}
								bool flag15 = list.Count > 0;
								bool flag16 = flag15;
								if (flag16)
								{
									int num3 = ItemsLauncherBlock.GetSpeedLevel(data);
									int num4 = ItemsLauncherBlock.GetSpreadLevel(data);
									bool flag17 = num3 == 0;
									bool flag18 = flag17;
									if (flag18)
									{
										num3 = 3;
									}
									bool flag19 = num4 == 0;
									bool flag20 = flag19;
									if (flag20)
									{
										num4 = 1;
									}
									float s = ComponentInvShooterBehavior.m_speedValues[num3 - 1];
									float s2 = ComponentInvShooterBehavior.m_spreadValues[num4 - 1];
									this.m_subsystemAudio.PlaySound("Audio/Items/ItemLauncher/Item Cannon Fire", 0.5f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 10f, false);
									int num5 = Math.Min(5, list.Count);
									for (int j = 0; j < num5; j++)
									{
										Vector3 v = Vector3.Normalize(vector + s2 * this.m_random.Vector3(0.3f));
										Vector3 velocity3 = v * s;
										this.m_subsystemProjectiles.FireProjectile(list[j], eyePosition, velocity3, Vector3.Zero, this.m_componentCreature);
										this.m_componentInventory.RemoveSlotItems(list2[j], 1);
										bool flag21 = j == num5 - 1;
										bool flag22 = flag21;
										if (flag22)
										{
											this.m_subsystemAudio.PlaySound("Audio/Items/ItemLauncher/AutoCannonFire", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 15f, false);
										}
									}
									int fuel = ItemsLauncherBlock.GetFuel(data) - 1;
									int data3 = ItemsLauncherBlock.SetFuel(data, fuel);
									num2 = Terrain.ReplaceData(slotValue, data3);
								}
							}
							break;
						}
				}
				bool flag23 = this.m_weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.Throwable && this.m_weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.ItemsLauncher;
				bool flag24 = flag23;
				if (flag24)
				{
					this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
					this.m_componentInventory.AddSlotItems(activeSlotIndex, num2, 1);
				}
				else
				{
					bool flag25 = this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.ItemsLauncher && num2 != slotValue;
					bool flag26 = flag25;
					if (flag26)
					{
						this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
						this.m_componentInventory.AddSlotItems(activeSlotIndex, num2, 1);
					}
				}
			}
		}

		// Token: 0x06000101 RID: 257 RVA: 0x0000D69C File Offset: 0x0000B89C
		private void FireProjectile()
		{
			bool throwFromHead = this.ThrowFromHead;
			bool flag = throwFromHead;
			Vector3 vector;
			if (flag)
			{
				vector = this.m_componentCreature.ComponentCreatureModel.EyePosition;
			}
			else
			{
				vector = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
			}
			Vector3 v = this.m_componentChaseBehavior.Target.ComponentBody.Position - vector;
			this.m_distance = v.Length();
			float num = 30f;
			float num2 = MathUtils.Clamp(this.m_distance / 12f, 0.6f, 1.8f);
			float s = num * num2;
			Vector3 v2 = Vector3.Normalize(v);
			float y = MathUtils.Lerp(1f, 3f, this.m_distance / 25f);
			Vector3 velocity = v2 * s + new Vector3(0f, y, 0f);
			this.m_subsystemProjectiles.FireProjectile(this.m_arrowValue, vector, velocity, Vector3.Zero, this.m_componentCreature);
			bool flag2 = this.m_componentModel != null;
			bool flag3 = flag2;
			if (flag3)
			{
				ComponentHumanModel componentHumanModel = this.m_componentModel as ComponentHumanModel;
				bool flag4 = componentHumanModel != null;
				bool flag5 = flag4;
				if (flag5)
				{
					componentHumanModel.m_handAngles2 = new Vector2(MathUtils.DegToRad(-90f), componentHumanModel.m_handAngles2.Y);
				}
			}
			bool flag6 = !string.IsNullOrEmpty(this.ThrowingSound);
			bool flag7 = flag6;
			if (flag7)
			{
				float pitch = this.m_random.Float(-0.1f, 0.1f);
				this.m_subsystemAudio.PlaySound(this.ThrowingSound, 1f, pitch, vector, this.ThrowingSoundDistance, 0.1f);
			}
			bool discountFromInventory = this.DiscountFromInventory;
			bool flag8 = discountFromInventory;
			if (flag8)
			{
				this.RemoveAimableItemFromInventory(this.m_arrowValue);
			}
		}

		// Token: 0x06000102 RID: 258 RVA: 0x0000D8E0 File Offset: 0x0000BAE0
		private int FindAimableItemInInventory()
		{
			bool flag = this.m_specialThrowableItemValues.Count > 0;
			bool flag2 = flag;
			bool flag3 = flag2;
			int result;
			if (flag3)
			{
				int index = this.m_random.Int(0, this.m_specialThrowableItemValues.Count - 1);
				result = this.m_specialThrowableItemValues[index];
			}
			else
			{
				bool flag4 = this.m_componentInventory == null;
				bool flag5 = flag4;
				bool flag6 = flag5;
				if (flag6)
				{
					result = 0;
				}
				else
				{
					List<int> list = new List<int>();
					for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
					{
						int slotValue = this.m_componentInventory.GetSlotValue(i);
						bool flag7 = slotValue != 0;
						bool flag8 = flag7;
						bool flag9 = flag8;
						if (flag9)
						{
							int num = Terrain.ExtractContents(slotValue);
							bool flag10 = this.m_excludedItems.Contains(num);
							bool flag11 = !flag10;
							bool flag12 = flag11;
							if (flag12)
							{
								Block block = BlocksManager.Blocks[num];
								bool flag13 = block.IsAimable_(slotValue);
								bool flag14 = flag13;
								bool flag15 = flag14;
								if (flag15)
								{
									bool selectRandomThrowableItems = this.SelectRandomThrowableItems;
									bool flag16 = !selectRandomThrowableItems;
									bool flag17 = flag16;
									if (flag17)
									{
										return slotValue;
									}
									list.Add(slotValue);
								}
							}
						}
					}
					bool flag18 = list.Count > 0;
					bool flag19 = flag18;
					bool flag20 = flag19;
					if (flag20)
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

		// Token: 0x06000103 RID: 259 RVA: 0x0000DA68 File Offset: 0x0000BC68
		private void RemoveAimableItemFromInventory(int value)
		{
			bool flag = this.m_componentInventory == null;
			bool flag2 = !flag;
			bool flag3 = flag2;
			if (flag3)
			{
				for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
				{
					bool flag4 = this.m_componentInventory.GetSlotValue(i) == value && this.m_componentInventory.GetSlotCount(i) > 0;
					bool flag5 = flag4;
					bool flag6 = flag5;
					if (flag6)
					{
						this.m_componentInventory.RemoveSlotItems(i, 1);
						break;
					}
				}
			}
		}

		// Token: 0x06000104 RID: 260 RVA: 0x0000DAEC File Offset: 0x0000BCEC
		private void TryReloadWeapon(ComponentInvShooterBehavior.WeaponInfo weaponToReload)
		{
			bool flag = !this.DiscountFromInventory || weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.None;
			bool flag2 = !flag;
			if (flag2)
			{
				bool flag3 = weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow || weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Crossbow;
				bool flag4 = flag3;
				if (flag4)
				{
					ArrowBlock.ArrowType[] array = (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow) ? new SubsystemBowBlockBehavior().m_supportedArrowTypes : new SubsystemCrossbowBlockBehavior().m_supportedArrowTypes;
					int blockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);
					for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
					{
						int slotValue = this.m_componentInventory.GetSlotValue(i);
						bool flag5 = Terrain.ExtractContents(slotValue) == blockIndex;
						bool flag6 = flag5;
						if (flag6)
						{
							ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(slotValue));
							bool flag7 = false;
							foreach (ArrowBlock.ArrowType arrowType2 in array)
							{
								bool flag8 = arrowType2 == arrowType;
								bool flag9 = flag8;
								if (flag9)
								{
									flag7 = true;
									break;
								}
							}
							bool flag10 = flag7;
							bool flag11 = flag10;
							if (flag11)
							{
								int data = Terrain.ExtractData(weaponToReload.WeaponValue);
								int data2 = (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow) ? BowBlock.SetArrowType(data, new ArrowBlock.ArrowType?(arrowType)) : CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(data, new ArrowBlock.ArrowType?(arrowType)), 15);
								this.m_componentInventory.RemoveSlotItems(weaponToReload.WeaponSlot, 1);
								this.m_componentInventory.AddSlotItems(weaponToReload.WeaponSlot, Terrain.ReplaceData(weaponToReload.WeaponValue, data2), 1);
								this.m_componentInventory.RemoveSlotItems(i, 1);
								break;
							}
						}
					}
				}
				else
				{
					bool flag12 = weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Musket;
					bool flag13 = flag12;
					if (flag13)
					{
						int num = this.FindItemSlotByContents(109);
						int num2 = this.FindItemSlotByContents(205);
						int value;
						int num3 = this.FindBulletSlot(out value);
						bool flag14 = num != -1 && num2 != -1 && num3 != -1;
						bool flag15 = flag14;
						if (flag15)
						{
							BulletBlock.BulletType bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(value));
							this.m_componentInventory.RemoveSlotItems(num, 1);
							this.m_componentInventory.RemoveSlotItems(num2, 1);
							this.m_componentInventory.RemoveSlotItems(num3, 1);
							int data3 = MusketBlock.SetLoadState(Terrain.ExtractData(weaponToReload.WeaponValue), MusketBlock.LoadState.Loaded);
							data3 = MusketBlock.SetBulletType(data3, new BulletBlock.BulletType?(bulletType));
							this.m_componentInventory.RemoveSlotItems(weaponToReload.WeaponSlot, 1);
							this.m_componentInventory.AddSlotItems(weaponToReload.WeaponSlot, Terrain.ReplaceData(weaponToReload.WeaponValue, data3), 1);
						}
					}
					else
					{
						bool flag16 = weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.ItemsLauncher;
						bool flag17 = flag16;
						if (flag17)
						{
							int num4 = this.FindItemSlotByContents(ItemsLauncherBlock.Index);
							bool flag18 = num4 != -1;
							bool flag19 = flag18;
							if (flag19)
							{
								int slotValue2 = this.m_componentInventory.GetSlotValue(num4);
								int data4 = Terrain.ExtractData(slotValue2);
								int fuel = ItemsLauncherBlock.GetFuel(data4);
								bool flag20 = fuel < 15;
								bool flag21 = flag20;
								if (flag21)
								{
									int fuel2 = Math.Min(fuel + 5, 15);
									int data5 = ItemsLauncherBlock.SetFuel(data4, fuel2);
									int value2 = Terrain.ReplaceData(slotValue2, data5);
									this.m_componentInventory.RemoveSlotItems(num4, 1);
									this.m_componentInventory.AddSlotItems(num4, value2, 1);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x06000105 RID: 261 RVA: 0x0000DE3C File Offset: 0x0000C03C
		private int FindItemSlotByContents(int contents)
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				bool flag = Terrain.ExtractContents(this.m_componentInventory.GetSlotValue(i)) == contents;
				bool flag2 = flag;
				if (flag2)
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x06000106 RID: 262 RVA: 0x0000DE8C File Offset: 0x0000C08C
		private int FindBulletSlot(out int bulletValue)
		{
			int blockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = Terrain.ExtractContents(slotValue) == blockIndex;
				bool flag2 = flag;
				if (flag2)
				{
					bulletValue = slotValue;
					return i;
				}
			}
			bulletValue = 0;
			return -1;
		}

		// Token: 0x06000107 RID: 263 RVA: 0x0000DEF4 File Offset: 0x0000C0F4
		private bool IsWeaponReady(ComponentInvShooterBehavior.WeaponInfo weaponInfo)
		{
			bool flag = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None;
			bool flag2 = flag;
			bool result;
			if (flag2)
			{
				result = false;
			}
			else
			{
				bool flag3 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Throwable;
				bool flag4 = flag3;
				if (flag4)
				{
					result = true;
				}
				else
				{
					int slotValue = this.m_componentInventory.GetSlotValue(weaponInfo.WeaponSlot);
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int data = Terrain.ExtractData(slotValue);
					bool flag5 = block is BowBlock;
					bool flag6 = flag5;
					if (flag6)
					{
						result = (BowBlock.GetArrowType(data) != null);
					}
					else
					{
						bool flag7 = block is CrossbowBlock;
						bool flag8 = flag7;
						if (flag8)
						{
							result = (CrossbowBlock.GetArrowType(data) != null && CrossbowBlock.GetDraw(data) == 15);
						}
						else
						{
							bool flag9 = block is MusketBlock;
							bool flag10 = flag9;
							if (flag10)
							{
								result = (MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded);
							}
							else
							{
								bool flag11 = block is ItemsLauncherBlock;
								result = (flag11 && ItemsLauncherBlock.GetFuel(data) > 0);
							}
						}
					}
				}
			}
			return result;
		}

		// Token: 0x06000108 RID: 264 RVA: 0x0000E018 File Offset: 0x0000C218
		private void ProactiveReloadCheck()
		{
			ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindReloadableRangedWeapon();
			bool flag = weaponInfo.Type > ComponentInvShooterBehavior.WeaponType.None;
			bool flag2 = flag;
			if (flag2)
			{
				bool flag3 = this.m_componentInventory.ActiveSlotIndex != weaponInfo.WeaponSlot;
				bool flag4 = flag3;
				if (flag4)
				{
					this.m_componentInventory.ActiveSlotIndex = weaponInfo.WeaponSlot;
				}
				this.TryReloadWeapon(weaponInfo);
			}
		}

		// Token: 0x06000109 RID: 265 RVA: 0x0000E078 File Offset: 0x0000C278
		private bool HasAmmoForWeapon(ComponentInvShooterBehavior.WeaponInfo weaponInfo)
		{
			bool flag = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None;
			bool flag2 = flag;
			bool result;
			if (flag2)
			{
				result = false;
			}
			else
			{
				bool flag3 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Throwable;
				bool flag4 = flag3;
				if (flag4)
				{
					result = true;
				}
				else
				{
					bool flag5 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Bow || weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Crossbow;
					bool flag6 = flag5;
					if (flag6)
					{
						int blockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);
						for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
						{
							bool flag7 = Terrain.ExtractContents(this.m_componentInventory.GetSlotValue(i)) == blockIndex;
							bool flag8 = flag7;
							if (flag8)
							{
								return true;
							}
						}
						result = false;
					}
					else
					{
						bool flag9 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Musket;
						bool flag10 = flag9;
						if (flag10)
						{
							bool flag11 = this.FindItemSlotByContents(109) != -1;
							bool flag12 = this.FindItemSlotByContents(205) != -1;
							int num2;
							int num = this.FindBulletSlot(out num2);
							result = (flag11 && flag12 && num != -1);
						}
						else
						{
							bool flag13 = weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.ItemsLauncher;
							bool flag14 = flag13;
							if (flag14)
							{
								bool flag15 = ItemsLauncherBlock.GetFuel(weaponInfo.WeaponValue) <= 0;
								bool flag16 = flag15;
								if (flag16)
								{
									result = false;
								}
								else
								{
									for (int j = 0; j < this.m_componentInventory.SlotsCount; j++)
									{
										bool flag17 = j != weaponInfo.WeaponSlot && this.m_componentInventory.GetSlotCount(j) > 0;
										bool flag18 = flag17;
										if (flag18)
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

		// Token: 0x0600010A RID: 266 RVA: 0x0000E22C File Offset: 0x0000C42C
		private bool CanReloadWeapon(ComponentInvShooterBehavior.WeaponInfo weaponInfo)
		{
			return this.HasAmmoForWeapon(weaponInfo);
		}

		// Token: 0x0600010B RID: 267 RVA: 0x0000E248 File Offset: 0x0000C448
		private ComponentInvShooterBehavior.WeaponInfo FindReadyRangedWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0;
				bool flag2 = flag;
				if (flag2)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int data = Terrain.ExtractData(slotValue);
					bool flag3 = block is BowBlock && BowBlock.GetArrowType(data) != null;
					bool flag4 = flag3;
					ComponentInvShooterBehavior.WeaponInfo result;
					if (flag4)
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
						bool flag5 = block is CrossbowBlock && CrossbowBlock.GetArrowType(data) != null && CrossbowBlock.GetDraw(data) == 15;
						bool flag6 = flag5;
						if (flag6)
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
							bool flag7 = block is MusketBlock && MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded;
							bool flag8 = flag7;
							if (flag8)
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
								bool flag9 = block is ItemsLauncherBlock && ItemsLauncherBlock.GetFuel(data) > 0;
								bool flag10 = !flag9;
								if (flag10)
								{
									goto IL_187;
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
			IL_187:;
			}
			return this.FindMeleeWeapon();
		}

		// Token: 0x0600010C RID: 268 RVA: 0x0000E408 File Offset: 0x0000C608
		private ComponentInvShooterBehavior.WeaponInfo FindReloadableRangedWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0;
				bool flag2 = flag;
				if (flag2)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int data = Terrain.ExtractData(slotValue);
					bool flag3 = block is BowBlock && BowBlock.GetArrowType(data) == null;
					bool flag4 = flag3;
					ComponentInvShooterBehavior.WeaponInfo result;
					if (flag4)
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
						bool flag5 = block is CrossbowBlock && (CrossbowBlock.GetArrowType(data) == null || CrossbowBlock.GetDraw(data) < 15);
						bool flag6 = flag5;
						if (flag6)
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
							bool flag7 = block is MusketBlock && MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Empty;
							bool flag8 = flag7;
							if (flag8)
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
								bool flag9 = block is ItemsLauncherBlock;
								bool flag10 = !flag9;
								if (flag10)
								{
									goto IL_181;
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
			IL_181:;
			}
			return default(ComponentInvShooterBehavior.WeaponInfo);
		}

		// Token: 0x0600010D RID: 269 RVA: 0x0000E5C4 File Offset: 0x0000C7C4
		private ComponentInvShooterBehavior.WeaponInfo FindThrowableWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is SpearBlock;
				bool flag2 = flag;
				if (flag2)
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

		// Token: 0x0600010E RID: 270 RVA: 0x0000E654 File Offset: 0x0000C854
		private ComponentInvShooterBehavior.WeaponInfo FindMeleeWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				bool flag = slotValue != 0;
				bool flag2 = flag;
				if (flag2)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					bool flag3 = block is MacheteBlock || block is WoodenClubBlock || block is StoneClubBlock || block is AxeBlock || block is SpearBlock;
					bool flag4 = flag3;
					if (flag4)
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

		// Token: 0x04000119 RID: 281
		private static readonly float[] m_speedValues = new float[]
		{
			50f,
			75f,
			100f
		};

		// Token: 0x0400011A RID: 282
		private static readonly float[] m_spreadValues = new float[]
		{
			0.001f,
			0.01f,
			0.05f
		};

		// Token: 0x0400011B RID: 283
		public ComponentCreature m_componentCreature;

		// Token: 0x0400011C RID: 284
		public float MeleeRange;

		// Token: 0x0400011D RID: 285
		public ComponentChaseBehavior m_componentChaseBehavior;

		// Token: 0x0400011E RID: 286
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x0400011F RID: 287
		public StateMachine m_stateMachine = new StateMachine();

		// Token: 0x04000120 RID: 288
		public SubsystemTime m_subsystemTime;

		// Token: 0x04000121 RID: 289
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x04000122 RID: 290
		public Random m_random = new Random();

		// Token: 0x04000123 RID: 291
		public int m_arrowValue;

		// Token: 0x04000124 RID: 292
		public double m_nextUpdateTime;

		// Token: 0x04000125 RID: 293
		public double m_ChargeTime;

		// Token: 0x04000126 RID: 294
		public float m_distance;

		// Token: 0x04000127 RID: 295
		public bool DiscountFromInventory;

		// Token: 0x04000128 RID: 296
		public string MinMaxRandomChargeTime;

		// Token: 0x04000129 RID: 297
		public float m_randomThrowMin;

		// Token: 0x0400012A RID: 298
		public float m_randomThrowMax;

		// Token: 0x0400012B RID: 299
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x0400012C RID: 300
		public string ThrowingSound;

		// Token: 0x0400012D RID: 301
		public float ThrowingSoundDistance;

		// Token: 0x0400012E RID: 302
		public bool SelectRandomThrowableItems;

		// Token: 0x0400012F RID: 303
		public string SpecialThrowableItem;

		// Token: 0x04000130 RID: 304
		public int m_specialThrowableItemValue;

		// Token: 0x04000131 RID: 305
		public List<int> m_specialThrowableItemValues = new List<int>();

		// Token: 0x04000132 RID: 306
		public float m_minDistance;

		// Token: 0x04000133 RID: 307
		public float m_maxDistance;

		// Token: 0x04000134 RID: 308
		public string MinMaxDistance;

		// Token: 0x04000135 RID: 309
		public float m_randomWaitMin;

		// Token: 0x04000136 RID: 310
		public float m_randomWaitMax;

		// Token: 0x04000137 RID: 311
		public string MinMaxRandomWaitTime;

		// Token: 0x04000138 RID: 312
		public double m_chargeStartTime;

		// Token: 0x04000139 RID: 313
		public bool m_isCharging;

		// Token: 0x0400013A RID: 314
		public float m_chargeDuration;

		// Token: 0x0400013B RID: 315
		public bool ThrowFromHead;

		// Token: 0x0400013C RID: 316
		public ComponentCreatureModel m_componentModel;

		// Token: 0x0400013D RID: 317
		public List<int> m_excludedItems = new List<int>();

		// Token: 0x0400013E RID: 318
		public ComponentInventory m_componentInventory;

		// Token: 0x0400013F RID: 319
		private string m_currentStateName;

		// Token: 0x04000140 RID: 320
		private double m_nextCombatUpdateTime;

		// Token: 0x04000141 RID: 321
		private double m_nextProactiveReloadTime;

		// Token: 0x04000142 RID: 322
		private double m_fireStateEndTime;

		// Token: 0x04000143 RID: 323
		private ComponentInvShooterBehavior.WeaponInfo m_weaponInfo;

		// Token: 0x04000144 RID: 324
		private double m_aimStartTime;

		// Token: 0x04000145 RID: 325
		private float m_aimDuration;

		// Token: 0x04000146 RID: 326
		private int m_bowDraw;

		// Token: 0x0200008B RID: 139
		public enum WeaponType
		{
			// Token: 0x04000471 RID: 1137
			None,
			// Token: 0x04000472 RID: 1138
			Throwable,
			// Token: 0x04000473 RID: 1139
			Bow,
			// Token: 0x04000474 RID: 1140
			Crossbow,
			// Token: 0x04000475 RID: 1141
			Musket,
			// Token: 0x04000476 RID: 1142
			ItemsLauncher,
			// Token: 0x04000477 RID: 1143
			Melee
		}

		// Token: 0x0200008C RID: 140
		public struct WeaponInfo
		{
			// Token: 0x04000478 RID: 1144
			public int WeaponSlot;

			// Token: 0x04000479 RID: 1145
			public int WeaponValue;

			// Token: 0x0400047A RID: 1146
			public ComponentInvShooterBehavior.WeaponType Type;
		}
	}
}
