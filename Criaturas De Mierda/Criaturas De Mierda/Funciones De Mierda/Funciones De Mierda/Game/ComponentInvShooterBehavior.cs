using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200003C RID: 60
	public class ComponentInvShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000021 RID: 33
		// (get) Token: 0x06000148 RID: 328 RVA: 0x000123A0 File Offset: 0x000105A0
		public int UpdateOrder
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000022 RID: 34
		// (get) Token: 0x06000149 RID: 329 RVA: 0x000123A3 File Offset: 0x000105A3
		public override float ImportanceLevel
		{
			get
			{
				return 0f;
			}
		}

		// Token: 0x17000023 RID: 35
		// (get) Token: 0x0600014A RID: 330 RVA: 0x000123AA File Offset: 0x000105AA
		UpdateOrder IUpdateable.UpdateOrder
		{
			get
			{
				return this.m_subsystemProjectiles.UpdateOrder;
			}
		}

		// Token: 0x0600014B RID: 331 RVA: 0x000123B8 File Offset: 0x000105B8
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

			// Agregar estados de la máquina de estados
			this.m_stateMachine.AddState("Idle", null, new Action(this.Idle_Update), null);
			this.m_stateMachine.AddState("Aiming", new Action(this.Aiming_Enter), new Action(this.Aiming_Update), null);
			this.m_stateMachine.AddState("Fire", new Action(this.Fire_Enter), new Action(this.Fire_Update), new Action(this.Fire_Leave));
			this.m_stateMachine.AddState("Reloading", null, new Action(this.Reloading_Update), null);
			this.TransitionToState("Idle");

			string value = valuesDictionary.GetValue<string>("ExcludedThrowableItems", string.Empty);
			string value2 = base.ValuesDictionary.GetValue<string>("SpecialThrowableItem", string.Empty);
			bool flag = !string.IsNullOrEmpty(value2);
			if (flag)
			{
				string[] array = value2.Split(',', StringSplitOptions.None);
				foreach (string text in array)
				{
					string text2 = text.Trim();
					bool flag2 = text2.Contains(":");
					if (flag2)
					{
						string[] array3 = text2.Split(':', StringSplitOptions.None);
						string text3 = array3[0].Trim();
						string s = array3[1].Trim();
						int num;
						bool flag3 = int.TryParse(s, out num);
						if (flag3)
						{
							int blockIndex = BlocksManager.GetBlockIndex(text3, false);
							bool flag4 = blockIndex >= 0;
							if (flag4)
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
						if (flag5)
						{
							this.m_specialThrowableItemValues.Add(Terrain.MakeBlockValue(num2));
						}
						else
						{
							int blockIndex2 = BlocksManager.GetBlockIndex(text2, false);
							bool flag6 = blockIndex2 >= 0;
							if (flag6)
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
			if (flag7)
			{
				string[] array4 = value.Split(';', StringSplitOptions.None);
				foreach (string text4 in array4)
				{
					int num3;
					bool flag8 = int.TryParse(text4, out num3);
					if (flag8)
					{
						this.m_excludedItems.Add(Terrain.ExtractContents(Terrain.MakeBlockValue(num3)));
					}
					else
					{
						int blockIndex3 = BlocksManager.GetBlockIndex(text4, false);
						bool flag9 = blockIndex3 >= 0;
						if (flag9)
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
			bool flag10 = array6.Length >= 2 && float.TryParse(array6[0], out this.m_randomWaitMin) && float.TryParse(array6[1], out this.m_randomWaitMax);
			if (!flag10)
			{
				this.m_randomWaitMin = 0.8f;
				this.m_randomWaitMax = 1.2f;
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
			bool flag11 = array7.Length >= 2 && float.TryParse(array7[0], out this.m_minDistance) && float.TryParse(array7[1], out this.m_maxDistance);
			if (!flag11)
			{
				this.m_minDistance = 3f;
				this.m_maxDistance = 15f;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(64, 3);
				defaultInterpolatedStringHandler.AppendLiteral("Invalid MinMaxDistance format: '");
				defaultInterpolatedStringHandler.AppendFormatted(this.MinMaxDistance);
				defaultInterpolatedStringHandler.AppendLiteral("'. Using default values ('");
				defaultInterpolatedStringHandler.AppendFormatted<float>(this.m_minDistance);
				defaultInterpolatedStringHandler.AppendLiteral("';'");
				defaultInterpolatedStringHandler.AppendFormatted<float>(this.m_maxDistance);
				defaultInterpolatedStringHandler.AppendLiteral("').");
				Log.Warning(defaultInterpolatedStringHandler.ToStringAndClear());
			}
			bool flag12 = !string.IsNullOrEmpty(this.SpecialThrowableItem);
			if (flag12)
			{
				int num4;
				bool flag13 = int.TryParse(this.SpecialThrowableItem, out num4);
				if (flag13)
				{
					this.m_specialThrowableItemValue = Terrain.MakeBlockValue(num4);
				}
				else
				{
					int blockIndex4 = BlocksManager.GetBlockIndex(this.SpecialThrowableItem, false);
					bool flag14 = blockIndex4 >= 0;
					if (flag14)
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
			bool flag15 = array8.Length >= 2 && float.TryParse(array8[0], out this.m_randomThrowMin) && float.TryParse(array8[1], out this.m_randomThrowMax);
			if (!flag15)
			{
				this.m_randomThrowMin = 1.0f;
				this.m_randomThrowMax = 1.5f;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(70, 3);
				defaultInterpolatedStringHandler.AppendLiteral("Invalid MinMaxRandomWaitTime format: '");
				defaultInterpolatedStringHandler.AppendFormatted(this.MinMaxRandomWaitTime);
				defaultInterpolatedStringHandler.AppendLiteral("'. Using default values ('");
				defaultInterpolatedStringHandler.AppendFormatted<float>(this.m_randomThrowMin);
				defaultInterpolatedStringHandler.AppendLiteral("';'");
				defaultInterpolatedStringHandler.AppendFormatted<float>(this.m_randomThrowMax);
				defaultInterpolatedStringHandler.AppendLiteral("').");
				Log.Warning(defaultInterpolatedStringHandler.ToStringAndClear());
			}
		}

		// Token: 0x0600014C RID: 332 RVA: 0x00012A68 File Offset: 0x00010C68
		public void Update(float dt)
		{
			bool flag = this.m_componentCreature.ComponentHealth.Health <= 0f;
			if (!flag)
			{
				// Recarga proactiva
				if (this.m_subsystemTime.GameTime >= this.m_nextProactiveReloadTime)
				{
					this.m_nextProactiveReloadTime = this.m_subsystemTime.GameTime + 1.0;
					if (this.m_currentStateName == "Idle")
					{
						this.ProactiveReloadCheck();
					}
				}

				// Actualización de la máquina de estados para armas de proyectiles
				if (this.m_subsystemTime.GameTime >= this.m_nextCombatUpdateTime)
				{
					this.m_stateMachine.Update();
				}

				// Lógica original de armas arrojadizas
				double gameTime = this.m_subsystemTime.GameTime;
				bool isCharging = this.m_isCharging;
				if (isCharging)
				{
					bool flag2 = this.m_componentChaseBehavior.Target != null;
					float num = 0f;
					bool flag3 = flag2;
					if (flag3)
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
						num = Vector3.Distance(vector, this.m_componentChaseBehavior.Target.ComponentBody.Position);
					}
					bool flag4;
					if (flag2 && num >= this.m_minDistance && num <= this.m_maxDistance)
					{
						ComponentHealth componentHealth = this.m_componentChaseBehavior.Target.Entity.FindComponent<ComponentHealth>();
						flag4 = (componentHealth != null && componentHealth.Health <= 0f);
					}
					else
					{
						flag4 = true;
					}
					bool flag5 = flag4;
					if (flag5)
					{
						this.m_isCharging = false;
					}
					else
					{
						bool flag7 = gameTime >= this.m_chargeStartTime + (double)this.m_chargeDuration;
						if (flag7)
						{
							this.FireProjectile();
							this.m_isCharging = false;
							this.m_ChargeTime = (double)this.m_random.Float(this.m_randomThrowMin, this.m_randomThrowMax);
							bool flag8 = this.m_distance < this.m_minDistance;
							if (flag8)
							{
								this.m_ChargeTime *= 0.9;
							}
							this.m_nextUpdateTime = gameTime + this.m_ChargeTime;
						}
					}
				}
				else
				{
					bool flag10 = gameTime >= this.m_nextUpdateTime;
					if (flag10)
					{
						this.m_arrowValue = this.FindAimableItemInInventory();
						bool flag11 = this.m_arrowValue != 0;
						bool flag12 = flag11 && this.m_componentChaseBehavior.Target != null;
						if (flag12)
						{
							bool throwFromHead2 = this.ThrowFromHead;
							Vector3 vector2;
							if (throwFromHead2)
							{
								vector2 = this.m_componentCreature.ComponentCreatureModel.EyePosition;
							}
							else
							{
								vector2 = this.m_componentCreature.ComponentCreatureModel.EyePosition + this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f - this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f + this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
							}
							this.m_distance = (this.m_componentChaseBehavior.Target.ComponentBody.Position - vector2).Length();
							bool flag13 = this.m_distance >= this.m_minDistance && this.m_distance <= this.m_maxDistance;
							if (flag13)
							{
								this.m_isCharging = true;
								// MEJORA: Tiempos de carga más consistentes
								float chargeVariation = this.m_random.Float(0.8f, 1.2f);
								this.m_chargeDuration = ((this.m_randomWaitMin + this.m_randomWaitMax) / 2f) * chargeVariation;
								this.m_chargeStartTime = gameTime;
							}
						}
						else
						{
							this.m_ChargeTime = (double)this.m_random.Float(this.m_randomThrowMin, this.m_randomThrowMax);
							bool flag15 = this.m_distance < this.m_minDistance;
							if (flag15)
							{
								this.m_ChargeTime *= 0.9;
							}
							this.m_nextUpdateTime = gameTime + this.m_ChargeTime;
						}
						bool flag16 = !this.m_isCharging && this.m_componentModel != null;
						if (flag16)
						{
							ComponentHumanModel componentHumanModel = this.m_componentModel as ComponentHumanModel;
							bool flag17 = componentHumanModel != null;
							if (flag17)
							{
								componentHumanModel.m_handAngles2 = Vector2.Lerp(componentHumanModel.m_handAngles2, new Vector2(0f, componentHumanModel.m_handAngles2.Y), 5f * dt);
							}
						}
					}
				}
			}
		}

		// ========== MÉTODOS DE MÁQUINA DE ESTADOS ==========

		private void TransitionToState(string stateName)
		{
			this.m_currentStateName = stateName;
			this.m_stateMachine.TransitionTo(stateName);
		}

		private void Idle_Update()
		{
			if (this.m_componentChaseBehavior.Target == null)
			{
				return;
			}

			float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_componentChaseBehavior.Target.ComponentBody.Position);

			// Buscar armas listas
			ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindReadyRangedWeapon();
			if (weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None)
			{
				weaponInfo = this.FindReloadableRangedWeapon();
			}
			if (weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None)
			{
				weaponInfo = this.FindThrowableWeapon();
			}

			if (weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.None && distance >= this.m_minDistance && distance <= this.m_maxDistance)
			{
				this.m_weaponInfo = weaponInfo;
				this.m_componentInventory.ActiveSlotIndex = this.m_weaponInfo.WeaponSlot;
				this.TransitionToState(this.IsWeaponReady(this.m_weaponInfo) ? "Aiming" : "Reloading");
			}
		}

		private void Aiming_Enter()
		{
			this.m_aimStartTime = this.m_subsystemTime.GameTime;
			this.m_bowDraw = 0;
			this.m_aimDuration = 0f;
		}

		private void Aiming_Update()
		{
			if (this.m_componentChaseBehavior.Target == null)
			{
				this.TransitionToState("Idle");
				return;
			}

			this.ApplyAimingAnimation();
			int activeSlotIndex = this.m_componentInventory.ActiveSlotIndex;
			int slotValue = this.m_componentInventory.GetSlotValue(activeSlotIndex);
			int data = Terrain.ExtractData(slotValue);
			int newValue = slotValue;

			if (this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Bow)
			{
				float progress = (float)((this.m_subsystemTime.GameTime - this.m_aimStartTime) / (double)this.m_aimDuration);
				this.m_bowDraw = MathUtils.Min((int)(progress * 16f), 15);
				newValue = Terrain.ReplaceData(slotValue, BowBlock.SetDraw(data, this.m_bowDraw));
			}
			else if (this.m_weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Musket && !MusketBlock.GetHammerState(data) && this.m_subsystemTime.GameTime > this.m_aimStartTime + 0.5)
			{
				newValue = Terrain.ReplaceData(slotValue, MusketBlock.SetHammerState(data, true));
				this.m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, this.m_random.Float(-0.1f, 0.1f), this.m_componentCreature.ComponentBody.Position, 3f, false);
			}

			if (newValue != slotValue)
			{
				this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
				this.m_componentInventory.AddSlotItems(activeSlotIndex, newValue, 1);
			}

			if (this.m_subsystemTime.GameTime > this.m_aimStartTime + (double)this.m_aimDuration)
			{
				this.TransitionToState("Fire");
			}
		}

		private void Fire_Enter()
		{
			if (this.m_componentChaseBehavior.Target != null)
			{
				this.PerformRangedFireAction();
			}
			this.m_fireStateEndTime = this.m_subsystemTime.GameTime + 0.2;
		}

		private void Fire_Update()
		{
			this.ApplyRecoilAnimation();
			if (this.m_subsystemTime.GameTime >= this.m_fireStateEndTime)
			{
				this.m_nextCombatUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_random.Float(2.5f, 3.5f);
				this.TransitionToState("Reloading");
			}
		}

		private void Fire_Leave()
		{
			if (this.m_componentModel != null)
			{
				this.m_componentModel.AimHandAngleOrder = 0f;
				this.m_componentModel.InHandItemOffsetOrder = Vector3.Zero;
				this.m_componentModel.InHandItemRotationOrder = Vector3.Zero;
			}
		}

		private void Reloading_Update()
		{
			this.TryReloadWeapon(this.m_weaponInfo);
			this.TransitionToState("Idle");
		}

		// ========== MÉTODOS DE ANIMACIÓN ==========

		private void ApplyAimingAnimation()
		{
			if (this.m_aimDuration == 0f)
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

			if (this.m_componentChaseBehavior.Target != null && this.m_componentModel != null)
			{
				this.m_componentModel.LookAtOrder = new Vector3?(this.m_componentChaseBehavior.Target.ComponentCreatureModel.EyePosition);
			}

			if (this.m_componentModel != null)
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

		private void ApplyRecoilAnimation()
		{
			if (this.m_componentModel != null)
			{
				this.m_componentModel.AimHandAngleOrder *= 1.1f;
				this.m_componentModel.InHandItemOffsetOrder -= new Vector3(0f, 0f, 0.05f);
			}
		}

		// ========== MÉTODOS DE DISPARO ==========

		private void PerformRangedFireAction()
		{
			int activeSlotIndex = this.m_componentInventory.ActiveSlotIndex;
			int slotValue = this.m_componentInventory.GetSlotValue(activeSlotIndex);
			if (slotValue == 0) return;

			Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPosition = this.m_componentChaseBehavior.Target.ComponentBody.Position + new Vector3(0f, this.m_componentChaseBehavior.Target.ComponentBody.StanceBoxSize.Y * 0.75f, 0f);
			float distance = Vector3.Distance(eyePosition, targetPosition);
			Vector3 direction = Vector3.Normalize(targetPosition - eyePosition);

			int data = Terrain.ExtractData(slotValue);
			int newValue = slotValue;

			switch (this.m_weaponInfo.Type)
			{
				case ComponentInvShooterBehavior.WeaponType.Bow:
					ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);
					if (arrowType != null)
					{
						Vector3 velocity = (direction + this.m_random.Vector3(0.05f) + new Vector3(0f, 0.15f * (distance / 20f), 0f)) * MathUtils.Lerp(0f, 28f, (float)Math.Pow((double)((float)this.m_bowDraw / 15f), 0.75));
						this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(false, false), 0, ArrowBlock.SetArrowType(0, arrowType.Value)), eyePosition, velocity, Vector3.Zero, this.m_componentCreature);
						this.m_subsystemAudio.PlaySound("Audio/Bow", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 4f, false);
					}
					newValue = Terrain.ReplaceData(slotValue, BowBlock.SetDraw(BowBlock.SetArrowType(data, default(ArrowBlock.ArrowType?)), 0));
					break;

				case ComponentInvShooterBehavior.WeaponType.Crossbow:
					ArrowBlock.ArrowType? arrowType2 = CrossbowBlock.GetArrowType(data);
					if (arrowType2 != null)
					{
						Vector3 velocity = (direction + this.m_random.Vector3(0.02f) + new Vector3(0f, 0.1f * (distance / 30f), 0f)) * 38f;
						this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<ArrowBlock>(false, false), 0, ArrowBlock.SetArrowType(0, arrowType2.Value)), eyePosition, velocity, Vector3.Zero, this.m_componentCreature);
						this.m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 4f, false);
					}
					newValue = Terrain.ReplaceData(slotValue, CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(data, default(ArrowBlock.ArrowType?)), 0));
					break;

				case ComponentInvShooterBehavior.WeaponType.Musket:
					if (MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetHammerState(data))
					{
						BulletBlock.BulletType bulletType = MusketBlock.GetBulletType(data).GetValueOrDefault();
						int bulletData = BulletBlock.SetBulletType(0, bulletType);
						this.m_subsystemProjectiles.FireProjectile(Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<BulletBlock>(false, false), 0, bulletData), eyePosition, direction * 120f, Vector3.Zero, this.m_componentCreature);
						this.m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 10f, false);
					}
					newValue = Terrain.ReplaceData(slotValue, MusketBlock.SetLoadState(data, 0));
					break;
			}

			if (this.m_weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.Throwable)
			{
				this.m_componentInventory.RemoveSlotItems(activeSlotIndex, 1);
				this.m_componentInventory.AddSlotItems(activeSlotIndex, newValue, 1);
			}
		}

		// Token: 0x0600014D RID: 333 RVA: 0x00012F4C File Offset: 0x0001114C
		private void FireProjectile()
		{
			Vector3 position;
			if (this.ThrowFromHead)
			{
				position = this.m_componentCreature.ComponentCreatureModel.EyePosition;
			}
			else
			{
				position = this.m_componentCreature.ComponentCreatureModel.EyePosition +
						   this.m_componentCreature.ComponentBody.Matrix.Right * 0.3f -
						   this.m_componentCreature.ComponentBody.Matrix.Up * 0.2f +
						   this.m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
			}

			Vector3 targetDirection = this.m_componentChaseBehavior.Target.ComponentBody.Position - position;
			this.m_distance = targetDirection.Length();

			// CORRECCIÓN MEJORADA: Disparo perfectamente lineal y preciso
			float baseSpeed = 30f; // Velocidad aumentada para mejor impacto
			float distanceFactor = MathUtils.Clamp(this.m_distance / 12f, 0.6f, 1.8f);
			float speed = baseSpeed * distanceFactor;

			// DISPARO PERFECTAMENTE LINEAL - Sin dispersión aleatoria
			Vector3 direction = Vector3.Normalize(targetDirection);

			// Compensación de gravedad mínima para trayectoria recta
			float gravityCompensation = MathUtils.Lerp(1f, 3f, this.m_distance / 25f);

			// Velocidad directa y precisa
			Vector3 velocity = direction * speed + new Vector3(0f, gravityCompensation, 0f);

			// Disparar el proyectil con máxima precisión
			this.m_subsystemProjectiles.FireProjectile(
				this.m_arrowValue,
				position,
				velocity,
				Vector3.Zero,
				this.m_componentCreature
			);

			// Animación
			if (this.m_componentModel != null)
			{
				ComponentHumanModel componentHumanModel = this.m_componentModel as ComponentHumanModel;
				if (componentHumanModel != null)
				{
					componentHumanModel.m_handAngles2 = new Vector2(MathUtils.DegToRad(-90f), componentHumanModel.m_handAngles2.Y);
				}
			}

			// Sonido
			if (!string.IsNullOrEmpty(this.ThrowingSound))
			{
				float pitch = this.m_random.Float(-0.1f, 0.1f);
				this.m_subsystemAudio.PlaySound(this.ThrowingSound, 1f, pitch, position, this.ThrowingSoundDistance, 0.1f);
			}

			// Remover del inventario
			if (this.DiscountFromInventory)
			{
				this.RemoveAimableItemFromInventory(this.m_arrowValue);
			}
		}

		// Token: 0x0600014E RID: 334 RVA: 0x00013174 File Offset: 0x00011374
		private int FindAimableItemInInventory()
		{
			bool flag = this.m_specialThrowableItemValues.Count > 0;
			int result;
			if (flag)
			{
				int index = this.m_random.Int(0, this.m_specialThrowableItemValues.Count - 1);
				result = this.m_specialThrowableItemValues[index];
			}
			else
			{
				bool flag2 = this.m_componentInventory == null;
				if (flag2)
				{
					result = 0;
				}
				else
				{
					List<int> list = new List<int>();
					int i = 0;
					while (i < this.m_componentInventory.SlotsCount)
					{
						int slotValue = this.m_componentInventory.GetSlotValue(i);
						bool flag3 = slotValue != 0;
						if (flag3)
						{
							int num = Terrain.ExtractContents(slotValue);
							bool flag4 = this.m_excludedItems.Contains(num);
							if (!flag4)
							{
								Block block = BlocksManager.Blocks[num];
								bool flag5 = block.IsAimable_(slotValue);
								if (flag5)
								{
									bool selectRandomThrowableItems = this.SelectRandomThrowableItems;
									if (!selectRandomThrowableItems)
									{
										return slotValue;
									}
									list.Add(slotValue);
								}
							}
						}
					IL_D7:
						i++;
						continue;
						goto IL_D7;
					}
					bool flag6 = list.Count > 0;
					if (flag6)
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

		// Token: 0x0600014F RID: 335 RVA: 0x000132AC File Offset: 0x000114AC
		private void RemoveAimableItemFromInventory(int value)
		{
			bool flag = this.m_componentInventory == null;
			if (!flag)
			{
				for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
				{
					bool flag2 = this.m_componentInventory.GetSlotValue(i) == value && this.m_componentInventory.GetSlotCount(i) > 0;
					if (flag2)
					{
						this.m_componentInventory.RemoveSlotItems(i, 1);
						break;
					}
				}
			}
		}

		// Token: 0x06000150 RID: 336 RVA: 0x00013314 File Offset: 0x00011514
		private void TryReloadWeapon(ComponentInvShooterBehavior.WeaponInfo weaponToReload)
		{
			if (!this.DiscountFromInventory || weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.None)
				return;

			if (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow || weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Crossbow)
			{
				ArrowBlock.ArrowType[] supportedArrows = (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow) ?
					new SubsystemBowBlockBehavior().m_supportedArrowTypes :
					new SubsystemCrossbowBlockBehavior().m_supportedArrowTypes;

				int arrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);

				for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
				{
					int slotValue = this.m_componentInventory.GetSlotValue(i);
					if (Terrain.ExtractContents(slotValue) == arrowBlockIndex)
					{
						ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(slotValue));
						bool isSupported = false;

						foreach (ArrowBlock.ArrowType supportedType in supportedArrows)
						{
							if (supportedType == arrowType)
							{
								isSupported = true;
								break;
							}
						}

						if (isSupported)
						{
							int weaponData = Terrain.ExtractData(weaponToReload.WeaponValue);
							int newData = (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Bow) ?
								BowBlock.SetArrowType(weaponData, new ArrowBlock.ArrowType?(arrowType)) :
								CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(weaponData, new ArrowBlock.ArrowType?(arrowType)), 15);

							this.m_componentInventory.RemoveSlotItems(weaponToReload.WeaponSlot, 1);
							this.m_componentInventory.AddSlotItems(weaponToReload.WeaponSlot, Terrain.ReplaceData(weaponToReload.WeaponValue, newData), 1);
							this.m_componentInventory.RemoveSlotItems(i, 1);
							return;
						}
					}
				}
			}
			else if (weaponToReload.Type == ComponentInvShooterBehavior.WeaponType.Musket)
			{
				int powderSlot = this.FindItemSlotByContents(109);    // Pólvora
				int fuseSlot = this.FindItemSlotByContents(205);      // Mecha
				int bulletValue;
				int bulletSlot = this.FindBulletSlot(out bulletValue); // Bala

				if (powderSlot != -1 && fuseSlot != -1 && bulletSlot != -1)
				{
					BulletBlock.BulletType bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(bulletValue));
					this.m_componentInventory.RemoveSlotItems(powderSlot, 1);
					this.m_componentInventory.RemoveSlotItems(fuseSlot, 1);
					this.m_componentInventory.RemoveSlotItems(bulletSlot, 1);

					int weaponData = MusketBlock.SetLoadState(Terrain.ExtractData(weaponToReload.WeaponValue), (MusketBlock.LoadState)3);
					weaponData = MusketBlock.SetBulletType(weaponData, new BulletBlock.BulletType?(bulletType));

					this.m_componentInventory.RemoveSlotItems(weaponToReload.WeaponSlot, 1);
					this.m_componentInventory.AddSlotItems(weaponToReload.WeaponSlot, Terrain.ReplaceData(weaponToReload.WeaponValue, weaponData), 1);
				}
			}
		}

		// Token: 0x06000151 RID: 337 RVA: 0x00013530 File Offset: 0x00011730
		private int FindItemSlotByContents(int contents)
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				if (Terrain.ExtractContents(this.m_componentInventory.GetSlotValue(i)) == contents)
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x06000152 RID: 338 RVA: 0x0001356C File Offset: 0x0001176C
		private int FindBulletSlot(out int bulletValue)
		{
			int blockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				if (Terrain.ExtractContents(slotValue) == blockIndex)
				{
					bulletValue = slotValue;
					return i;
				}
			}
			bulletValue = 0;
			return -1;
		}

		// Token: 0x06000153 RID: 339 RVA: 0x000135B8 File Offset: 0x000117B8
		private bool IsWeaponReady(ComponentInvShooterBehavior.WeaponInfo weaponInfo)
		{
			if (weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.None)
				return false;

			if (weaponInfo.Type == ComponentInvShooterBehavior.WeaponType.Throwable)
				return true;

			int slotValue = this.m_componentInventory.GetSlotValue(weaponInfo.WeaponSlot);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
			int data = Terrain.ExtractData(slotValue);

			if (block is BowBlock)
				return BowBlock.GetArrowType(data) != null;

			if (block is CrossbowBlock)
				return CrossbowBlock.GetArrowType(data) != null && CrossbowBlock.GetDraw(data) == 15;

			return block is MusketBlock && MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded;
		}

		// Token: 0x06000154 RID: 340 RVA: 0x00013650 File Offset: 0x00011850
		private void ProactiveReloadCheck()
		{
			ComponentInvShooterBehavior.WeaponInfo weaponInfo = this.FindReloadableRangedWeapon();
			if (weaponInfo.Type != ComponentInvShooterBehavior.WeaponType.None)
			{
				if (this.m_componentInventory.ActiveSlotIndex != weaponInfo.WeaponSlot)
				{
					this.m_componentInventory.ActiveSlotIndex = weaponInfo.WeaponSlot;
				}
				this.TryReloadWeapon(weaponInfo);
			}
		}

		// Token: 0x06000155 RID: 341 RVA: 0x00013698 File Offset: 0x00011898
		private ComponentInvShooterBehavior.WeaponInfo FindReadyRangedWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int data = Terrain.ExtractData(slotValue);

					if (block is BowBlock && BowBlock.GetArrowType(data) != null)
					{
						return new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Bow
						};
					}

					if (block is CrossbowBlock && CrossbowBlock.GetArrowType(data) != null && CrossbowBlock.GetDraw(data) == 15)
					{
						return new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Crossbow
						};
					}

					if (block is MusketBlock && MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded)
					{
						return new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Musket
						};
					}
				}
			}
			return default(ComponentInvShooterBehavior.WeaponInfo);
		}

		// Token: 0x06000156 RID: 342 RVA: 0x000137A8 File Offset: 0x000119A8
		private ComponentInvShooterBehavior.WeaponInfo FindReloadableRangedWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					int data = Terrain.ExtractData(slotValue);

					if (block is BowBlock && BowBlock.GetArrowType(data) == null)
					{
						return new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Bow
						};
					}

					if (block is CrossbowBlock && (CrossbowBlock.GetArrowType(data) == null || CrossbowBlock.GetDraw(data) < 15))
					{
						return new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Crossbow
						};
					}

					if (block is MusketBlock && MusketBlock.GetLoadState(data) == 0)
					{
						return new ComponentInvShooterBehavior.WeaponInfo
						{
							WeaponSlot = i,
							WeaponValue = slotValue,
							Type = ComponentInvShooterBehavior.WeaponType.Musket
						};
					}
				}
			}
			return default(ComponentInvShooterBehavior.WeaponInfo);
		}

		// Token: 0x06000157 RID: 343 RVA: 0x000138B8 File Offset: 0x00011AB8
		private ComponentInvShooterBehavior.WeaponInfo FindThrowableWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				if (slotValue != 0 && BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is SpearBlock)
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

		// Token: 0x06000158 RID: 344 RVA: 0x0001392C File Offset: 0x00011B2C
		private ComponentInvShooterBehavior.WeaponInfo FindMeleeWeapon()
		{
			for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
			{
				int slotValue = this.m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					if (block is MacheteBlock || block is WoodenClubBlock || block is StoneClubBlock || block is AxeBlock || block is SpearBlock)
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

		// ========== VARIABLES ADICIONALES ==========

		// Token: 0x0400019C RID: 412
		public ComponentCreature m_componentCreature;

		// Token: 0x0400019D RID: 413
		public ComponentChaseBehavior m_componentChaseBehavior;

		// Token: 0x0400019E RID: 414
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x0400019F RID: 415
		public StateMachine m_stateMachine = new StateMachine();

		// Token: 0x040001A0 RID: 416
		public SubsystemTime m_subsystemTime;

		// Token: 0x040001A1 RID: 417
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x040001A2 RID: 418
		public Random m_random = new Random();

		// Token: 0x040001A3 RID: 419
		public int m_arrowValue;

		// Token: 0x040001A4 RID: 420
		public double m_nextUpdateTime;

		// Token: 0x040001A5 RID: 421
		public double m_ChargeTime;

		// Token: 0x040001A6 RID: 422
		public float m_distance;

		// Token: 0x040001A7 RID: 423
		public bool DiscountFromInventory;

		// Token: 0x040001A8 RID: 424
		public string MinMaxRandomChargeTime;

		// Token: 0x040001A9 RID: 425
		public float m_randomThrowMin;

		// Token: 0x040001AA RID: 426
		public float m_randomThrowMax;

		// Token: 0x040001AB RID: 427
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x040001AC RID: 428
		public string ThrowingSound;

		// Token: 0x040001AD RID: 429
		public float ThrowingSoundDistance;

		// Token: 0x040001AE RID: 430
		public bool SelectRandomThrowableItems;

		// Token: 0x040001AF RID: 431
		public string SpecialThrowableItem;

		// Token: 0x040001B0 RID: 432
		public int m_specialThrowableItemValue;

		// Token: 0x040001B1 RID: 433
		public List<int> m_specialThrowableItemValues = new List<int>();

		// Token: 0x040001B2 RID: 434
		public float m_minDistance;

		// Token: 0x040001B3 RID: 435
		public float m_maxDistance;

		// Token: 0x040001B4 RID: 436
		public string MinMaxDistance;

		// Token: 0x040001B5 RID: 437
		public float m_randomWaitMin;

		// Token: 0x040001B6 RID: 438
		public float m_randomWaitMax;

		// Token: 0x040001B7 RID: 439
		public string MinMaxRandomWaitTime;

		// Token: 0x040001B8 RID: 440
		public double m_chargeStartTime;

		// Token: 0x040001B9 RID: 441
		public bool m_isCharging;

		// Token: 0x040001BA RID: 442
		public float m_chargeDuration;

		// Token: 0x040001BB RID: 443
		public bool ThrowFromHead;

		// Token: 0x040001BC RID: 444
		public ComponentCreatureModel m_componentModel;

		// Token: 0x040001BD RID: 445
		public List<int> m_excludedItems = new List<int>();

		// Token: 0x040001BE RID: 446
		public ComponentInventory m_componentInventory;

		// ========== VARIABLES NUEVAS PARA EL SISTEMA DE RECARGA ==========

		private string m_currentStateName;
		private double m_nextCombatUpdateTime;
		private double m_nextProactiveReloadTime;
		private double m_fireStateEndTime;
		private ComponentInvShooterBehavior.WeaponInfo m_weaponInfo;
		private double m_aimStartTime;
		private float m_aimDuration;
		private int m_bowDraw;

		// Token: 0x0200003D RID: 61
		public enum WeaponType
		{
			// Token: 0x040001C2 RID: 450
			None,
			// Token: 0x040001C3 RID: 451
			Throwable,
			// Token: 0x040001C4 RID: 452
			Bow,
			// Token: 0x040001C5 RID: 453
			Crossbow,
			// Token: 0x040001C6 RID: 454
			Musket,
			// Token: 0x040001C7 RID: 455
			Melee
		}

		// Token: 0x0200003E RID: 62
		public struct WeaponInfo
		{
			// Token: 0x040001C8 RID: 456
			public int WeaponSlot;

			// Token: 0x040001C9 RID: 457
			public int WeaponValue;

			// Token: 0x040001CA RID: 458
			public ComponentInvShooterBehavior.WeaponType Type;
		}
	}
}
