using System;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentBehavior, IUpdateable
	{
		public ComponentCreature Target
		{
			get
			{
				return this.m_target;
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		// Propiedades específicas para zombis
		public bool AttacksAllCategories { get; set; } = true;
		public bool AttacksSameHerd { get; set; } = false;
		public string ZombieHerdName { get; set; } = "Zombie";

		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent, bool isRetaliation = false)
		{
			bool suppressed = this.Suppressed;
			if (!suppressed)
			{
				// Verificar si es del mismo rebaño zombi
				ComponentZombieHerdBehavior thisHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
				if (thisHerd != null && !string.IsNullOrEmpty(thisHerd.HerdName))
				{
					ComponentZombieHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (targetHerd != null && targetHerd.HerdName == thisHerd.HerdName)
					{
						if (!this.AttacksSameHerd)
						{
							return;
						}
					}
				}

				// VERIFICACIÓN DE MODO DE JUEGO
				bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
				if (isPlayer && !isRetaliation)
				{
					GameMode currentGameMode = this.m_subsystemGameInfo.WorldSettings.GameMode;
					if (currentGameMode == GameMode.Creative || currentGameMode == GameMode.Harmless)
					{
						return;
					}
				}

				this.m_target = componentCreature;
				this.m_nextUpdateTime = 0.0;
				this.m_range = maxRange;
				this.m_chaseTime = maxChaseTime;
				this.m_isPersistent = isPersistent;
				this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);
				this.IsActive = true;
				this.m_stateMachine.TransitionTo("Chasing");
				if (this.m_target != null && this.m_componentPathfinding != null)
				{
					this.m_componentPathfinding.Stop();
					this.UpdateChasingStateImmediately();
				}
			}
		}

		private void UpdateChasingStateImmediately()
		{
			if (this.m_target == null || !this.IsActive)
				return;
			Vector3 targetPosition = this.m_target.ComponentBody.Position;
			this.m_componentPathfinding.SetDestination(new Vector3?(targetPosition), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
			this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
		}

		public virtual void StopAttack()
		{
			this.m_stateMachine.TransitionTo("LookingForTarget");
			this.IsActive = false;
			this.m_target = null;
			this.m_nextUpdateTime = 0.0;
			this.m_range = 0f;
			this.m_chaseTime = 0f;
			this.m_isPersistent = false;
			this.m_importanceLevel = 0f;
		}

		public virtual void Update(float dt)
		{
			bool suppressed = this.Suppressed;
			if (suppressed)
			{
				this.StopAttack();
				return;
			}
			this.m_autoChaseSuppressionTime -= dt;
			this.CheckDefendPlayer(dt);
			bool hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			bool isOriginalHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>() != null;
			if (hasRangedWeapon || isOriginalHerd)
			{
				this.UpdateRangedWeaponLogic(dt);
			}
			bool flag = this.IsActive && this.m_target != null;
			if (flag)
			{
				this.m_chaseTime -= dt;
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
				float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);
				if (distance < 5f && this.m_target != null && this.IsActive)
				{
					this.SwitchToMeleeModeImmediately();
				}
				else if (distance > 5f)
				{
					this.FindAimTool(this.m_componentMiner);
				}
				float num = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;
				bool flag2 = this.m_attackMode != AttackMode.OnlyHand;
				if (flag2)
				{
					float num2;
					ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out num2);
					bool flag3 = hitBody != null && num2 > 5f && this.FindAimTool(this.m_componentMiner);
					if (flag3)
					{
						Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
						Vector3 targetPosition = this.m_target.ComponentCreatureModel.EyePosition;
						float verticalAdjustment = MathUtils.Lerp(0f, 0.3f, (float)Vector3.Distance(eyePosition, targetPosition) / 20f);
						Vector3 adjustedTargetPosition = targetPosition + new Vector3(0f, verticalAdjustment, 0f);
						Vector3 vector = Vector3.Normalize(adjustedTargetPosition - eyePosition);
						this.m_chaseTime = Math.Max(this.m_chaseTime, this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f);
						bool flag4 = num2 >= this.m_attackRange.X && num2 <= this.m_attackRange.Y;
						if (flag4)
						{
							bool flag5 = (double)Vector3.Dot(this.m_componentCreature.ComponentBody.Matrix.Forward, vector) > 0.8;
							bool flag6 = !flag5;
							if (flag6)
							{
								this.m_componentPathfinding.SetDestination(new Vector3?(this.m_target.ComponentCreatureModel.EyePosition), 1f, 1f, 0, false, true, false, null);
							}
							else
							{
								if (!this.IsFirearmActive())
								{
									this.m_componentPathfinding.Destination = null;
								}
								else
								{
									float desiredDistance = (this.m_attackRange.X + this.m_attackRange.Y) * 0.5f;
									if (Math.Abs(distance - desiredDistance) > 2f)
									{
										Vector3 toTarget = this.m_target.ComponentBody.Position - this.m_componentCreature.ComponentBody.Position;
										Vector3 direction = Vector3.Normalize(toTarget);
										Vector3 destination = this.m_target.ComponentBody.Position - direction * desiredDistance;
										this.m_componentPathfinding.SetDestination(new Vector3?(destination), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
									}
								}
							}
							string category = BlocksManager.Blocks[Terrain.ExtractContents(this.m_componentMiner.ActiveBlockValue)].GetCategory(this.m_componentMiner.ActiveBlockValue);
							bool flag7 = this.m_subsystemTime.GameTime - this.m_lastActionTime > (double)num;
							if (flag7)
							{
								bool flag8 = this.m_componentMiner.Use(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector));
								if (flag8)
								{
									this.m_lastActionTime = this.m_subsystemTime.GameTime;
									this.CreateFirearmEffects(vector);
								}
								else
								{
									bool flag9 = flag5;
									if (flag9)
									{
										this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector), AimState.Completed);
										this.m_lastActionTime = this.m_subsystemTime.GameTime;
									}
								}
							}
							else
							{
								bool flag10 = flag5;
								if (flag10)
								{
									this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector), AimState.InProgress);
								}
							}
							return;
						}
					}
				}
				bool flag11 = this.IsTargetInAttackRange(this.m_target.ComponentBody);
				if (flag11)
				{
					this.m_componentCreatureModel.AttackOrder = true;
					bool flag12 = this.m_attackMode != AttackMode.OnlyHand && !this.HasActiveRangedWeaponComponent();
					if (flag12)
					{
						this.FindHitTool(this.m_componentMiner);
					}
				}
				bool isAttackHitMoment = this.m_componentCreatureModel.IsAttackHitMoment;
				if (isAttackHitMoment)
				{
					Vector3 hitPoint;
					ComponentBody hitBody2 = this.GetHitBody(this.m_target.ComponentBody, out hitPoint);
					bool flag13 = hitBody2 != null;
					if (flag13)
					{
						float x = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
						this.m_chaseTime = MathUtils.Max(this.m_chaseTime, x);
						this.m_componentMiner.Hit(hitBody2, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
						this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}
			bool flag14 = this.m_subsystemTime.GameTime >= this.m_nextUpdateTime;
			if (flag14)
			{
				this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
				this.m_nextUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_dt;
				this.m_stateMachine.Update();
			}
		}

		private void CreateFirearmEffects(Vector3 direction)
		{
			if (this.m_subsystemParticles == null)
			{
				this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			}
			if (this.m_subsystemAudio == null)
			{
				this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			}
			if (this.m_subsystemTerrain == null)
			{
				this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			}

			if (this.m_subsystemParticles != null && this.m_subsystemTerrain != null)
			{
				Vector3 position = this.m_componentCreature.ComponentCreatureModel.EyePosition + direction * 1.3f;
				this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(this.m_subsystemTerrain, position, direction), false);
			}

			if (this.m_subsystemAudio != null)
			{
				this.m_subsystemAudio.PlayRandomSound("Audio/Armas/reload", 0.5f, this.m_random.Float(-0.1f, 0.1f), this.m_componentCreature.ComponentCreatureModel.EyePosition, 3f, true);
			}
		}

		private void SwitchToMeleeModeImmediately()
		{
			float bestPower = 1f;
			int bestSlot = -1;
			if (this.m_componentMiner.Inventory != null)
			{
				for (int i = 0; i < 6; i++)
				{
					int slotValue = this.m_componentMiner.Inventory.GetSlotValue(i);
					float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
					if (meleePower > bestPower)
					{
						bestPower = meleePower;
						bestSlot = i;
					}
				}
				if (bestSlot >= 0)
				{
					this.m_componentMiner.Inventory.ActiveSlotIndex = bestSlot;
				}
			}
		}

		private void UpdateRangedWeaponLogic(float dt)
		{
			if (this.m_target == null || !this.IsActive)
				return;
			bool hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			if (!hasRangedWeapon)
			{
				this.FindAimTool(this.m_componentMiner);
				hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			}
			if (hasRangedWeapon && this.m_target != null)
			{
				float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);
				if (distance < 5f)
				{
					this.SwitchToMeleeModeImmediately();
					return;
				}
				if (distance >= this.m_attackRange.X && distance <= this.m_attackRange.Y)
				{
					Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
					Vector3 targetPosition = this.m_target.ComponentCreatureModel.EyePosition;
					float verticalAdjustment = MathUtils.Lerp(0f, 0.3f, distance / 20f);
					Vector3 adjustedTargetPosition = targetPosition + new Vector3(0f, verticalAdjustment, 0f);
					Vector3 direction = Vector3.Normalize(adjustedTargetPosition - eyePosition);
					float rayDistance;
					ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out rayDistance);
					if (hitBody != null && Math.Abs(rayDistance - distance) < 2f)
					{
						float actionDelay = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;
						if (this.m_subsystemTime.GameTime - this.m_lastActionTime > actionDelay)
						{
							if (this.IsFirearmActive())
							{
								this.CreateFirearmEffects(direction);
							}
							this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, direction), AimState.Completed);
							this.m_lastActionTime = this.m_subsystemTime.GameTime;
							this.m_chaseTime = Math.Max(this.m_chaseTime, this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f);
						}
						else
						{
							this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, direction), AimState.InProgress);
						}
						if (!this.IsFirearmActive())
						{
							this.m_componentPathfinding.Destination = null;
						}
						else
						{
							float desiredDistance = (this.m_attackRange.X + this.m_attackRange.Y) * 0.5f;
							if (Math.Abs(distance - desiredDistance) > 2f)
							{
								Vector3 toTarget = this.m_target.ComponentBody.Position - this.m_componentCreature.ComponentBody.Position;
								Vector3 moveDirection = Vector3.Normalize(toTarget);
								Vector3 destination = this.m_target.ComponentBody.Position - moveDirection * desiredDistance;
								this.m_componentPathfinding.SetDestination(new Vector3?(destination), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
							}
						}
					}
					else
					{
						this.m_componentPathfinding.SetDestination(new Vector3?(this.m_target.ComponentBody.Position), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
					}
				}
				else if (distance > this.m_attackRange.Y)
				{
					this.m_componentPathfinding.SetDestination(new Vector3?(this.m_target.ComponentBody.Position), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
				}
				else if (distance < this.m_attackRange.X)
				{
					Vector3 retreatDirection = Vector3.Normalize(this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position);
					Vector3 retreatPosition = this.m_componentCreature.ComponentBody.Position + retreatDirection * 3f;
					this.m_componentPathfinding.SetDestination(new Vector3?(retreatPosition), 1f, 1f, 0, false, true, false, null);
				}
			}
		}

		private bool IsFirearmActive()
		{
			if (this.m_componentMiner == null || this.m_componentMiner.ActiveBlockValue == 0)
				return false;
			int blockId = Terrain.ExtractContents(this.m_componentMiner.ActiveBlockValue);
			return blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false);
		}

		private bool IsFirearmBlock(int blockValue)
		{
			int blockId = Terrain.ExtractContents(blockValue);
			return blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false) || blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false);
		}

		private bool HasActiveRangedWeaponComponent()
		{
			if (this.m_target == null) return false;
			if (this.IsFirearmActive()) return true;
			ComponentMusketShooterBehavior musket = base.Entity.FindComponent<ComponentMusketShooterBehavior>();
			if (musket != null && musket.IsActive) return true;
			ComponentBowShooterBehavior bow = base.Entity.FindComponent<ComponentBowShooterBehavior>();
			if (bow != null && bow.IsActive) return true;
			ComponentCrossbowShooterBehavior crossbow = base.Entity.FindComponent<ComponentCrossbowShooterBehavior>();
			if (crossbow != null && crossbow.IsActive) return true;
			ComponentFlameThrowerShooterBehavior flamethrower = base.Entity.FindComponent<ComponentFlameThrowerShooterBehavior>();
			if (flamethrower != null && flamethrower.IsActive) return true;
			ComponentItemsLauncherShooterBehavior launcher = base.Entity.FindComponent<ComponentItemsLauncherShooterBehavior>();
			if (launcher != null && launcher.IsActive) return true;
			return this.IsFirearmActive();
		}

		private int GetFirearmBulletNum(int slotValue)
		{
			int blockId = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);
			if (blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false)) return AKBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false)) return G3Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false)) return Izh43Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false)) return M4Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false)) return Mac10Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false)) return MinigunBlock.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false)) return SPAS12Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false)) return SWM500Block.GetBulletNum(data);
			if (blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false)) return UziBlock.GetBulletNum(data);
			return 0;
		}

		private void ReloadFirearm(ComponentMiner componentMiner, int slotIndex, int slotValue)
		{
			int blockId = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);
			int maxCapacity = 0;
			if (blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false)) maxCapacity = 30;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false)) maxCapacity = 30;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false)) maxCapacity = 2;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false)) maxCapacity = 22;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false)) maxCapacity = 30;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false)) maxCapacity = 100;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false)) maxCapacity = 8;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false)) maxCapacity = 5;
			else if (blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false)) maxCapacity = 30;
			if (maxCapacity > 0)
			{
				if (blockId == BlocksManager.GetBlockIndex(typeof(AKBlock), true, false)) data = AKBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(G3Block), true, false)) data = G3Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(Izh43Block), true, false)) data = Izh43Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(M4Block), true, false)) data = M4Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(Mac10Block), true, false)) data = Mac10Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false)) data = MinigunBlock.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false)) data = SPAS12Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(SWM500Block), true, false)) data = SWM500Block.SetBulletNum(maxCapacity);
				else if (blockId == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false)) data = UziBlock.SetBulletNum(maxCapacity);
				int value2 = Terrain.MakeBlockValue(blockId, 0, data);
				componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				componentMiner.Inventory.AddSlotItems(slotIndex, value2, 1);
				if (this.m_subsystemAudio != null)
				{
					this.m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, this.m_random.Float(-0.1f, 0.1f), this.m_componentCreature.ComponentCreatureModel.EyePosition, 5f, true);
				}
			}
		}

		public void RespondToCommandImmediately(ComponentCreature target)
		{
			if (this.Suppressed || target == null)
				return;
			this.m_target = target;
			this.m_nextUpdateTime = 0.0;
			this.m_range = 20f;
			this.m_chaseTime = 30f;
			this.m_isPersistent = false;
			this.m_importanceLevel = this.ImportanceLevelNonPersistent;
			this.IsActive = true;
			this.m_stateMachine.TransitionTo("Chasing");
			this.UpdateChasingStateImmediately();
		}

		private void UpdateChaseMovementOnly(float dt)
		{
			bool flag = !this.HasActiveRangedWeaponComponent();
			if (!flag)
			{
				bool flag2 = this.m_target == null || !this.IsActive;
				if (!flag2)
				{
					this.m_chaseTime -= dt;
					this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
					bool flag3 = this.m_subsystemTime.GameTime >= this.m_nextUpdateTime;
					if (flag3)
					{
						this.m_dt = this.m_random.Float(0.1f, 0.2f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.05f);
						this.m_nextUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_dt;
						bool flag4 = this.m_stateMachine.CurrentState == "Chasing";
						if (flag4)
						{
							this.UpdateChasingState();
						}
					}
				}
			}
		}

		private void UpdateChasingState()
		{
			bool flag = !this.IsActive || this.m_target == null || this.m_chaseTime <= 0f;
			if (!flag)
			{
				bool flag2 = this.m_target.ComponentHealth.Health <= 0f;
				if (flag2)
				{
					this.m_importanceLevel = 0f;
				}
				else
				{
					int maxPathfindingPositions = this.m_isPersistent ? ((this.m_subsystemTime.FixedTimeStep != null) ? 2000 : 500) : 0;
					BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
					BoundingBox boundingBox2 = this.m_target.ComponentBody.BoundingBox;
					Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
					Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
					float num = Vector3.Distance(v, vector);
					float num2 = (num < 4f) ? 0.2f : 0f;
					float num3 = 10f;
					bool flag3 = num > num3 + 3f;
					if (flag3)
					{
						this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
					}
					else
					{
						bool flag4 = num < num3 - 3f;
						if (flag4)
						{
							Vector3 v2 = Vector3.Normalize(this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position);
							Vector3 value = this.m_componentCreature.ComponentBody.Position + v2 * 3f;
							this.m_componentPathfinding.SetDestination(new Vector3?(value), 1f, 1f, 0, false, true, false, null);
						}
						else
						{
							if (!this.IsFirearmActive())
							{
								this.m_componentPathfinding.Destination = null;
							}
							else
							{
								float desiredDistance = (this.m_attackRange.X + this.m_attackRange.Y) * 0.5f;
								if (Math.Abs(num - desiredDistance) > 2f)
								{
									Vector3 toTarget = this.m_target.ComponentBody.Position - this.m_componentCreature.ComponentBody.Position;
									Vector3 direction = Vector3.Normalize(toTarget);
									Vector3 destination = this.m_target.ComponentBody.Position - direction * desiredDistance;
									this.m_componentPathfinding.SetDestination(new Vector3?(destination), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
								}
							}
						}
					}
				}
			}
		}

		public ComponentBody GetHitBody1(ComponentBody target, out float distance)
		{
			distance = 0f;
			bool flag = target == null || this.m_subsystemBodies == null;
			ComponentBody result;
			if (flag)
			{
				result = null;
			}
			else
			{
				Vector3 eyePosition = this.m_componentCreature.ComponentCreatureModel.EyePosition;
				Vector3 v = Vector3.Normalize(target.BoundingBox.Center() - eyePosition);
				BodyRaycastResult? bodyRaycastResult = this.m_subsystemBodies.Raycast(eyePosition, eyePosition + v * this.m_attackRange.Y, 0.35f, (ComponentBody body, float dist) => body.Entity != base.Entity && !body.IsChildOfBody(this.m_componentCreature.ComponentBody) && !this.m_componentCreature.ComponentBody.IsChildOfBody(body));
				ComponentMiner componentMiner = this.m_componentMiner;
				TerrainRaycastResult? terrainRaycastResult;
				if (componentMiner == null)
				{
					terrainRaycastResult = null;
				}
				else
				{
					SubsystemTerrain subsystemTerrain = componentMiner.m_subsystemTerrain;
					if (subsystemTerrain == null)
					{
						terrainRaycastResult = null;
					}
					else
					{
						terrainRaycastResult = subsystemTerrain.Raycast(eyePosition, eyePosition + v * this.m_attackRange.Y, true, true, (int value, float dist) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable);
					}
				}
				TerrainRaycastResult? terrainRaycastResult2 = terrainRaycastResult;
				distance = ((bodyRaycastResult != null) ? bodyRaycastResult.GetValueOrDefault().Distance : float.PositiveInfinity);
				bool flag2 = this.m_componentMiner.Inventory != null && bodyRaycastResult != null;
				if (flag2)
				{
					bool flag3 = terrainRaycastResult2 != null && (double)terrainRaycastResult2.Value.Distance < (double)bodyRaycastResult.Value.Distance;
					if (flag3)
					{
						return null;
					}
					bool flag4 = bodyRaycastResult.Value.ComponentBody == target || bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) || target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) || target.StandingOnBody == bodyRaycastResult.Value.ComponentBody;
					if (flag4)
					{
						return bodyRaycastResult.Value.ComponentBody;
					}
				}
				result = null;
			}
			return result;
		}

		private TerrainRaycastResult? PickTerrain(Vector3 position, Vector3 direction, float reach)
		{
			ComponentMiner componentMiner = this.m_componentMiner;
			bool flag = ((componentMiner != null) ? componentMiner.m_subsystemTerrain : null) == null;
			TerrainRaycastResult? result;
			if (flag)
			{
				result = null;
			}
			else
			{
				direction = Vector3.Normalize(direction);
				Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
				Vector3 end = position + direction * reach;
				result = this.m_componentMiner.m_subsystemTerrain.Raycast(position, end, true, true, (int value, float distance) => (double)Vector3.Distance(position + distance * direction, creaturePosition) <= (double)reach && BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable);
			}
			return result;
		}

		private BodyRaycastResult? PickBody(Vector3 position, Vector3 direction, float reach)
		{
			bool flag = this.m_subsystemBodies == null;
			BodyRaycastResult? result;
			if (flag)
			{
				result = null;
			}
			else
			{
				direction = Vector3.Normalize(direction);
				Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
				Vector3 end = position + direction * reach;
				result = this.m_subsystemBodies.Raycast(position, end, 0.35f, (ComponentBody body, float distance) => (double)Vector3.Distance(position + distance * direction, creaturePosition) <= (double)reach && body.Entity != this.Entity && !body.IsChildOfBody(this.m_componentMiner.ComponentCreature.ComponentBody) && !this.m_componentMiner.ComponentCreature.ComponentBody.IsChildOfBody(body));
			}
			return result;
		}

		public bool FindAimTool(ComponentMiner componentMiner)
		{
			bool flag = componentMiner.Inventory == null;
			if (flag)
			{
				return false;
			}
			int activeSlotIndex = componentMiner.Inventory.ActiveSlotIndex;
			int activeBlockValue = componentMiner.ActiveBlockValue;
			int num = Terrain.ExtractContents(activeBlockValue);
			Block block = BlocksManager.Blocks[num];
			bool isFirearm = this.IsFirearmActive();
			if (block.IsAimable_(activeBlockValue) || isFirearm)
			{
				if (!(block is FlameThrowerBlock))
				{
					bool flag4 = this.IsAimToolNeedToReady(componentMiner, activeSlotIndex);
					if (flag4)
					{
						this.HandleComplexAimTool(componentMiner, activeSlotIndex);
					}
					return true;
				}
				bool flag5 = this.IsReady(activeBlockValue);
				if (flag5)
				{
					return true;
				}
			}
			for (int i = 0; i < Math.Min(componentMiner.Inventory.SlotsCount, 10); i++)
			{
				if (i == activeSlotIndex) continue;
				int slotValue = componentMiner.Inventory.GetSlotValue(i);
				int num2 = Terrain.ExtractContents(slotValue);
				Block block2 = BlocksManager.Blocks[num2];
				bool flag6 = (block2.IsAimable_(slotValue) || this.IsFirearmBlock(slotValue)) && (!(block2 is FlameThrowerBlock) || this.IsReady(slotValue));
				if (flag6)
				{
					componentMiner.Inventory.ActiveSlotIndex = i;
					return true;
				}
			}
			return false;
		}

		public bool IsReady(int slotValue)
		{
			int data = Terrain.ExtractData(slotValue);
			return !(BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock) || (FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded && FlameThrowerBlock.GetBulletType(data) != null);
		}

		public bool IsAimToolNeedToReady(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
			if (block is ItemsLauncherBlock) return false;
			bool flag = !(block is BowBlock);
			if (flag)
			{
				bool flag2 = !(block is CrossbowBlock);
				if (flag2)
				{
					bool flag3 = !(block is RepeatCrossbowBlock);
					if (flag3)
					{
						bool flag4 = !(block is MusketBlock);
						if (flag4)
						{
							if (this.IsFirearmBlock(slotValue))
							{
								int bulletNum = this.GetFirearmBulletNum(slotValue);
								if (bulletNum <= 0)
								{
									this.ReloadFirearm(componentMiner, slotIndex, slotValue);
									return true;
								}
								return false;
							}
							return false;
						}
						bool flag5 = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetBulletType(data) != null;
						if (flag5) return false;
					}
					else
					{
						bool flag6 = RepeatCrossbowBlock.GetDraw(data) >= 15 && RepeatCrossbowBlock.GetArrowType(data) != null;
						if (flag6) return false;
					}
				}
				else
				{
					bool flag7 = CrossbowBlock.GetDraw(data) >= 15 && CrossbowBlock.GetArrowType(data) != null;
					if (flag7) return false;
				}
			}
			else
			{
				bool flag8 = BowBlock.GetDraw(data) >= 15 && BowBlock.GetArrowType(data) != null;
				if (flag8) return false;
			}
			return true;
		}

		public void HandleComplexAimTool(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int num2 = Terrain.ExtractContents(slotValue);
			Block block = BlocksManager.Blocks[num2];
			if (this.IsFirearmBlock(slotValue))
			{
				int bulletNum = this.GetFirearmBulletNum(slotValue);
				if (bulletNum <= 0)
				{
					this.ReloadFirearm(componentMiner, slotIndex, slotValue);
				}
				return;
			}
			if (!(block is ItemsLauncherBlock))
			{
				if (!(block is BowBlock))
				{
					bool flag2 = !(block is CrossbowBlock);
					if (flag2)
					{
						bool flag3 = !(block is RepeatCrossbowBlock);
						if (flag3)
						{
							bool flag4 = block is MusketBlock;
							if (flag4)
							{
								data = MusketBlock.SetLoadState(MusketBlock.SetBulletType(0, new BulletBlock.BulletType?(BulletBlock.BulletType.MusketBall)), MusketBlock.LoadState.Loaded);
							}
						}
						else
						{
							float randomValue = this.m_random.Float(0f, 1f);
							RepeatArrowBlock.ArrowType value;
							if (randomValue < 0.166f) value = RepeatArrowBlock.ArrowType.ExplosiveArrow;
							else if (randomValue < 0.332f) value = RepeatArrowBlock.ArrowType.PoisonArrow;
							else if (randomValue < 0.498f) value = RepeatArrowBlock.ArrowType.CopperArrow;
							else if (randomValue < 0.664f) value = RepeatArrowBlock.ArrowType.DiamondArrow;
							else if (randomValue < 0.83f) value = RepeatArrowBlock.ArrowType.SeriousPoisonArrow;
							else value = RepeatArrowBlock.ArrowType.IronArrow;
							data = RepeatCrossbowBlock.SetArrowType(data, new RepeatArrowBlock.ArrowType?(value));
							data = RepeatCrossbowBlock.SetDraw(data, 15);
						}
					}
					else
					{
						float randomValue = this.m_random.Float(0f, 1f);
						ArrowBlock.ArrowType value;
						if (randomValue < 0.333f) value = ArrowBlock.ArrowType.IronBolt;
						else if (randomValue < 0.666f) value = ArrowBlock.ArrowType.DiamondBolt;
						else value = ArrowBlock.ArrowType.ExplosiveBolt;
						data = CrossbowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(value));
						data = CrossbowBlock.SetDraw(data, 15);
					}
				}
				else
				{
					float randomValue = this.m_random.Float(0f, 1f);
					ArrowBlock.ArrowType value;
					if (randomValue < 0.166f) value = ArrowBlock.ArrowType.WoodenArrow;
					else if (randomValue < 0.332f) value = ArrowBlock.ArrowType.StoneArrow;
					else if (randomValue < 0.498f) value = ArrowBlock.ArrowType.IronArrow;
					else if (randomValue < 0.664f) value = ArrowBlock.ArrowType.DiamondArrow;
					else if (randomValue < 0.83f) value = ArrowBlock.ArrowType.FireArrow;
					else value = ArrowBlock.ArrowType.CopperArrow;
					data = BowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(value));
					data = BowBlock.SetDraw(data, 15);
				}
				int value2 = Terrain.MakeBlockValue(num2, 0, data);
				componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				componentMiner.Inventory.AddSlotItems(slotIndex, value2, 1);
			}
		}

		public bool FindHitTool(ComponentMiner componentMiner)
		{
			int activeBlockValue = componentMiner.ActiveBlockValue;
			bool flag = componentMiner.Inventory == null;
			if (flag) return false;
			bool flag2 = BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)].GetMeleePower(activeBlockValue) > 1f;
			if (flag2) return true;
			float num = 1f;
			int activeSlotIndex = 0;
			for (int i = 0; i < 6; i++)
			{
				int slotValue = componentMiner.Inventory.GetSlotValue(i);
				float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
				bool flag3 = meleePower > num;
				if (flag3)
				{
					num = meleePower;
					activeSlotIndex = i;
				}
			}
			bool flag4 = num > 1f;
			if (flag4)
			{
				componentMiner.Inventory.ActiveSlotIndex = activeSlotIndex;
				return true;
			}
			return false;
		}

		private void CheckDefendPlayer(float dt)
		{
			try
			{
				ComponentZombieHerdBehavior componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
				bool isPlayerHerd = false;
				if (componentZombieHerdBehavior != null && !string.IsNullOrEmpty(componentZombieHerdBehavior.HerdName))
				{
					isPlayerHerd = componentZombieHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
				}
				if (!isPlayerHerd) return;
				bool flag3 = this.m_subsystemTime.GameTime < this.m_nextPlayerCheckTime;
				if (!flag3)
				{
					this.m_nextPlayerCheckTime = this.m_subsystemTime.GameTime + 0.5;
					foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
					{
						bool flag4 = componentPlayer.ComponentHealth.Health > 0f;
						if (flag4)
						{
							ComponentCreature componentCreature = this.FindPlayerAttacker(componentPlayer);
							bool flag5 = componentCreature != null && (this.m_target == null || this.m_target != componentCreature);
							if (flag5)
							{
								this.Attack(componentCreature, 20f, 30f, false);
							}
						}
					}
				}
			}
			catch { }
		}

		private ComponentCreature FindPlayerAttacker(ComponentPlayer player)
		{
			try
			{
				bool flag = player == null || player.ComponentBody == null;
				if (flag) return null;
				Vector3 position = player.ComponentBody.Position;
				float num = 20f;
				float num2 = num * num;
				foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
				{
					bool flag2 = componentCreature != null && componentCreature.ComponentHealth != null && componentCreature.ComponentHealth.Health > 0f && componentCreature != this.m_componentCreature && componentCreature.ComponentBody != null;
					if (flag2)
					{
						float num3 = Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position);
						bool flag3 = num3 < num2;
						if (flag3)
						{
							ComponentZombieHerdBehavior componentZombieHerdBehavior = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
							bool isPlayerHerd = false;
							if (componentZombieHerdBehavior != null) isPlayerHerd = componentZombieHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
							if (!isPlayerHerd)
							{
								ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
								ComponentNewChaseBehavior componentNewChaseBehavior = componentCreature.Entity.FindComponent<ComponentNewChaseBehavior>();
								ComponentZombieChaseBehavior componentZombieChaseBehavior = componentCreature.Entity.FindComponent<ComponentZombieChaseBehavior>();
								bool isAttackingPlayer = false;
								if (componentChaseBehavior != null && componentChaseBehavior.Target == player) isAttackingPlayer = true;
								else if (componentNewChaseBehavior != null && componentNewChaseBehavior.Target == player) isAttackingPlayer = true;
								else if (componentZombieChaseBehavior != null && componentZombieChaseBehavior.Target == player) isAttackingPlayer = true;
								if (isAttackingPlayer) return componentCreature;
							}
						}
					}
				}
			}
			catch { }
			return null;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.m_componentFeedBehavior = base.Entity.FindComponent<ComponentRandomFeedBehavior>();
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.m_componentFactors = base.Entity.FindComponent<ComponentFactors>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			string attackModeString = valuesDictionary.GetValue<string>("AttackMode", "Default");
			if (Enum.TryParse<AttackMode>(attackModeString, true, out AttackMode parsedAttackMode))
			{
				this.m_attackMode = parsedAttackMode;
			}
			else
			{
				this.m_attackMode = AttackMode.Default;
			}
			this.m_attackRange = valuesDictionary.GetValue<Vector2>("AttackRange", new Vector2(2f, 15f));
			this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			try { this.m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask"); }
			catch { this.m_autoChaseMask = (CreatureCategory)0; }
			this.m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			this.m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			this.m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");
			this.m_autoDismount = valuesDictionary.GetValue<bool>("AutoDismount", true);
			this.m_nextPlayerCheckTime = 0.0;
			this.m_lastActionTime = 0.0;
			ComponentBody componentBody = this.m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				bool flag = this.m_target == null && this.m_autoChaseSuppressionTime <= 0f && this.m_random.Float(0f, 1f) < this.m_chaseOnTouchProbability;
				if (flag)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					bool flag2 = componentCreature != null;
					if (flag2)
					{
						bool flag3 = this.m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag4 = this.m_autoChaseMask > (CreatureCategory)0;
						ComponentZombieHerdBehavior componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
						bool flag5 = true;
						bool flag6 = componentZombieHerdBehavior != null;
						if (flag6)
						{
							bool canAttack = true;
							if (componentZombieHerdBehavior != null && !string.IsNullOrEmpty(componentZombieHerdBehavior.HerdName))
							{
								ComponentZombieHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
								if (targetHerd != null && targetHerd.HerdName == componentZombieHerdBehavior.HerdName)
								{
									canAttack = this.AttacksSameHerd;
								}
							}
							flag5 = canAttack;
						}
						bool flag7 = flag5 && ((this.AttacksPlayer && flag3 && this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) || (this.AttacksNonPlayerCreature && !flag3 && flag4));
						if (flag7)
						{
							this.Attack(componentCreature, this.ChaseRangeOnTouch, this.ChaseTimeOnTouch, false);
						}
					}
				}
				bool flag8 = this.m_target != null && this.JumpWhenTargetStanding && body == this.m_target.ComponentBody && body.StandingOnBody == this.m_componentCreature.ComponentBody;
				if (flag8)
				{
					this.m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			}));
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				bool flag = injury.Attacker == null || this.m_random.Float(0f, 1f) >= this.m_chaseWhenAttackedProbability;
				if (!flag)
				{
					float maxRange = this.ChaseRangeOnAttacked ?? ((this.m_chaseWhenAttackedProbability >= 1f) ? 30f : 7f);
					float maxChaseTime = this.ChaseTimeOnAttacked ?? ((this.m_chaseWhenAttackedProbability >= 1f) ? 60f : 7f);
					bool isPersistent = this.ChasePersistentOnAttacked ?? (this.m_chaseWhenAttackedProbability >= 1f);
					ComponentZombieHerdBehavior componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
					bool flag2 = true;
					bool flag3 = componentZombieHerdBehavior != null;
					if (flag3)
					{
						bool canAttack = true;
						if (componentZombieHerdBehavior != null && !string.IsNullOrEmpty(componentZombieHerdBehavior.HerdName))
						{
							ComponentZombieHerdBehavior attackerHerd = injury.Attacker.Entity.FindComponent<ComponentZombieHerdBehavior>();
							if (attackerHerd != null && attackerHerd.HerdName == componentZombieHerdBehavior.HerdName)
							{
								canAttack = this.AttacksSameHerd;
							}
						}
						flag2 = canAttack;
					}
					if (flag2)
					{
						this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent, true);
					}
				}
			}));
			this.m_stateMachine.AddState("LookingForTarget", delegate
			{
				this.m_importanceLevel = 0f;
				this.m_target = null;
			}, delegate
			{
				bool isActive = this.IsActive;
				if (isActive)
				{
					this.m_stateMachine.TransitionTo("Chasing");
				}
				else
				{
					bool flag = !this.Suppressed && this.m_autoChaseSuppressionTime <= 0f && (this.m_target == null || this.ScoreTarget(this.m_target) <= 0f) && this.m_componentCreature.ComponentHealth.Health > this.MinHealthToAttackActively;
					if (flag)
					{
						this.m_range = ((this.m_subsystemSky.SkyLightIntensity < 0.2f) ? this.m_nightChaseRange : this.m_dayChaseRange);
						this.m_range *= this.m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);
						ComponentCreature componentCreature = this.FindTarget();
						bool flag2 = componentCreature != null;
						if (flag2)
						{
							this.m_targetInRangeTime += this.m_dt;
						}
						else
						{
							this.m_targetInRangeTime = 0f;
						}
						bool flag3 = this.m_targetInRangeTime > this.TargetInRangeTimeToChase;
						if (flag3)
						{
							bool flag4 = this.m_subsystemSky.SkyLightIntensity >= 0.1f;
							float maxRange = flag4 ? (this.m_dayChaseRange + 6f) : (this.m_nightChaseRange + 6f);
							float maxChaseTime = flag4 ? (this.m_dayChaseTime * this.m_random.Float(0.75f, 1f)) : (this.m_nightChaseTime * this.m_random.Float(0.75f, 1f));
							this.Attack(componentCreature, maxRange, maxChaseTime, !flag4);
						}
					}
				}
			}, null);
			this.m_stateMachine.AddState("RandomMoving", delegate
			{
				this.m_componentPathfinding.SetDestination(new Vector3?(this.m_componentCreature.ComponentBody.Position + new Vector3(6f * this.m_random.Float(-1f, 1f), 0f, 6f * this.m_random.Float(-1f, 1f))), 1f, 1f, 0, false, true, false, null);
			}, delegate
			{
				bool flag = !this.m_componentPathfinding.IsStuck || this.m_componentPathfinding.Destination == null;
				if (flag)
				{
					this.m_stateMachine.TransitionTo("Chasing");
				}
				bool flag2 = !this.IsActive;
				if (flag2)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
			}, delegate
			{
				this.m_componentPathfinding.Stop();
			});
			this.m_stateMachine.AddState("Chasing", delegate
			{
				this.m_subsystemNoise.MakeNoise(this.m_componentCreature.ComponentBody, 0.25f, 6f);
				bool playIdleSoundWhenStartToChase = this.PlayIdleSoundWhenStartToChase;
				if (playIdleSoundWhenStartToChase)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				this.m_nextUpdateTime = 0.0;
			}, delegate
			{
				bool flag = !this.IsActive;
				if (flag)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else
				{
					bool flag2 = this.m_chaseTime <= 0f;
					if (flag2)
					{
						this.m_autoChaseSuppressionTime = this.m_random.Float(10f, 60f);
						this.m_importanceLevel = 0f;
					}
					else
					{
						bool flag3 = this.m_target == null;
						if (flag3)
						{
							this.m_importanceLevel = 0f;
						}
						else
						{
							bool flag4 = this.m_target.ComponentHealth.Health <= 0f;
							if (flag4)
							{
								bool flag5 = this.m_componentFeedBehavior != null;
								if (flag5)
								{
									this.m_subsystemTime.QueueGameTimeDelayedExecution(this.m_subsystemTime.GameTime + (double)this.m_random.Float(1f, 3f), delegate
									{
										bool flag16 = this.m_target != null;
										if (flag16)
										{
											this.m_componentFeedBehavior.Feed(this.m_target.ComponentBody.Position);
										}
									});
								}
								this.m_importanceLevel = 0f;
							}
							else
							{
								bool flag6 = !this.m_isPersistent && this.m_componentPathfinding.IsStuck;
								if (flag6)
								{
									this.m_importanceLevel = 0f;
								}
								else
								{
									bool flag7 = this.m_isPersistent && this.m_componentPathfinding.IsStuck;
									if (flag7)
									{
										this.m_stateMachine.TransitionTo("RandomMoving");
									}
									else
									{
										bool flag8 = this.ScoreTarget(this.m_target) <= 0f;
										if (flag8)
										{
											this.m_targetUnsuitableTime += this.m_dt;
										}
										else
										{
											this.m_targetUnsuitableTime = 0f;
										}
										bool flag9 = this.m_targetUnsuitableTime > 3f;
										if (flag9)
										{
											this.m_importanceLevel = 0f;
										}
										else
										{
											int maxPathfindingPositions = 0;
											bool isPersistent = this.m_isPersistent;
											if (isPersistent)
											{
												maxPathfindingPositions = ((this.m_subsystemTime.FixedTimeStep != null) ? 2000 : 500);
											}
											BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
											BoundingBox boundingBox2 = this.m_target.ComponentBody.BoundingBox;
											Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
											Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max);
											float num = Vector3.Distance(v, vector);
											float num2 = (num < 4f) ? 0.2f : 0f;
											bool hasOriginalHerd = base.Entity.FindComponent<ComponentZombieHerdBehavior>() != null;
											bool flag10 = (this.m_attackMode != AttackMode.OnlyHand && num > 5f && this.FindAimTool(this.m_componentMiner)) || (hasOriginalHerd && num > 5f && this.FindAimTool(this.m_componentMiner));
											if (flag10)
											{
												float num3;
												ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out num3);
												bool flag11 = hitBody != null && num3 >= this.m_attackRange.X && num3 <= this.m_attackRange.Y;
												if (flag11)
												{
													float num4 = (this.m_attackRange.X + this.m_attackRange.Y) * 0.5f;
													bool flag12 = Math.Abs(num3 - num4) > 2f;
													if (flag12)
													{
														bool flag13 = num3 > num4;
														if (flag13)
														{
															this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
														}
														else
														{
															Vector3 v2 = Vector3.Normalize(this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position);
															Vector3 value = this.m_componentCreature.ComponentBody.Position + v2 * 3f;
															this.m_componentPathfinding.SetDestination(new Vector3?(value), 1f, 1f, 0, false, true, false, null);
														}
													}
													else
													{
														if (!this.IsFirearmActive())
														{
															this.m_componentPathfinding.Destination = null;
														}
														else
														{
															float desiredDistance = (this.m_attackRange.X + this.m_attackRange.Y) * 0.5f;
															if (Math.Abs(num3 - desiredDistance) > 2f)
															{
																Vector3 toTarget = this.m_target.ComponentBody.Position - this.m_componentCreature.ComponentBody.Position;
																Vector3 direction = Vector3.Normalize(toTarget);
																Vector3 destination = this.m_target.ComponentBody.Position - direction * desiredDistance;
																this.m_componentPathfinding.SetDestination(new Vector3?(destination), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
															}
														}
													}
												}
												else
												{
													bool flag14 = num3 > this.m_attackRange.Y;
													if (flag14)
													{
														this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
													}
												}
											}
											else
											{
												this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
											}
											bool flag15 = this.PlayAngrySoundWhenChasing && this.m_random.Float(0f, 1f) < 0.33f * this.m_dt;
											if (flag15)
											{
												this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
											}
										}
									}
								}
							}
						}
					}
				}
			}, null);
			this.m_stateMachine.TransitionTo("LookingForTarget");
		}

		public virtual ComponentCreature FindTarget()
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float num = 0f;
			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);
			int i = 0;
			while (i < this.m_componentBodies.Count)
			{
				ComponentCreature componentCreature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				bool flag = componentCreature != null;
				if (flag)
				{
					ComponentZombieHerdBehavior componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
					bool flag2 = componentZombieHerdBehavior != null;
					if (flag2)
					{
						bool canAttack = true;
						if (componentZombieHerdBehavior != null && !string.IsNullOrEmpty(componentZombieHerdBehavior.HerdName))
						{
							ComponentZombieHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
							if (targetHerd != null && targetHerd.HerdName == componentZombieHerdBehavior.HerdName)
							{
								canAttack = this.AttacksSameHerd;
							}
						}
						bool flag3 = !canAttack;
						if (flag3)
						{
							goto IL_C7;
						}
					}
					float num2 = this.ScoreTarget(componentCreature);
					bool flag4 = num2 > num;
					if (flag4)
					{
						num = num2;
						result = componentCreature;
					}
				}
			IL_C7:
				i++;
				continue;
				goto IL_C7;
			}
			return result;
		}

		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			float result = 0f;
			bool flag = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool flag2 = componentCreature == this.Target || this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool flag3 = this.m_autoChaseMask > (CreatureCategory)0;
			bool flag4 = componentCreature == this.Target || (flag3 && MathUtils.Remainder(0.004999999888241291 * this.m_subsystemTime.GameTime + (double)((float)(this.GetHashCode() % 1000) / 1000f) + (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) < (double)this.m_chaseNonPlayerProbability);
			ComponentZombieHerdBehavior componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			bool flag5 = true;
			if (componentZombieHerdBehavior != null)
			{
				bool canAttack = true;
				if (componentZombieHerdBehavior != null && !string.IsNullOrEmpty(componentZombieHerdBehavior.HerdName))
				{
					ComponentZombieHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentZombieHerdBehavior>();
					if (targetHerd != null && targetHerd.HerdName == componentZombieHerdBehavior.HerdName)
					{
						canAttack = this.AttacksSameHerd;
					}
				}
				flag5 = canAttack;
			}
			bool flag6 = componentCreature != this.m_componentCreature && flag5 && ((!flag && flag4) || (flag && flag2)) && componentCreature.Entity.IsAddedToProject && componentCreature.ComponentHealth.Health > 0f;
			if (flag6)
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				bool flag7 = num < this.m_range;
				if (flag7)
				{
					result = this.m_range - num;
				}
			}
			return result;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f || (target.ParentBody != null && this.IsTargetInWater(target.ParentBody)) || (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			bool flag = this.IsBodyInAttackRange(target);
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
				BoundingBox boundingBox2 = target.BoundingBox;
				Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
				Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
				float num = vector.Length();
				Vector3 v2 = vector / num;
				float num2 = 0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
				float num3 = 0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);
				bool flag2 = MathF.Abs(vector.Y) < num3 * 0.99f;
				if (flag2)
				{
					bool flag3 = num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f;
					if (flag3)
					{
						return true;
					}
				}
				else
				{
					bool flag4 = num < num3 + 0.3f && MathF.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f;
					if (flag4)
					{
						return true;
					}
				}
				result = ((target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) || (this.AllowAttackingStandingOnBody && target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInAttackRange(target.StandingOnBody)));
			}
			return result;
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = target.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
			float num = vector.Length();
			Vector3 v2 = vector / num;
			float num2 = 0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
			float num3 = 0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);
			bool flag = MathF.Abs(vector.Y) < num3 * 0.99f;
			if (flag)
			{
				bool flag2 = num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f;
				if (flag2)
				{
					return true;
				}
			}
			else
			{
				bool flag3 = num < num3 + 0.3f && MathF.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f;
				if (flag3)
				{
					return true;
				}
			}
			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 vector = this.m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 v = target.BoundingBox.Center();
			Ray3 ray = new Ray3(vector, Vector3.Normalize(v - vector));
			BodyRaycastResult? bodyRaycastResult = this.m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			bool flag = bodyRaycastResult != null && bodyRaycastResult.Value.Distance < this.MaxAttackRange && (bodyRaycastResult.Value.ComponentBody == target || bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) || target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) || (target.StandingOnBody == bodyRaycastResult.Value.ComponentBody && this.AllowAttackingStandingOnBody));
			ComponentBody result;
			if (flag)
			{
				hitPoint = bodyRaycastResult.Value.HitPoint();
				result = bodyRaycastResult.Value.ComponentBody;
			}
			else
			{
				hitPoint = default(Vector3);
				result = null;
			}
			return result;
		}

		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemSky m_subsystemSky;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTime m_subsystemTime;
		public SubsystemNoise m_subsystemNoise;
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemTerrain m_subsystemTerrain;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentMiner m_componentMiner;
		public ComponentRandomFeedBehavior m_componentFeedBehavior;
		public ComponentCreatureModel m_componentCreatureModel;
		public ComponentFactors m_componentFactors;
		public ComponentBody m_componentBody;
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		public Random m_random = new Random();
		public StateMachine m_stateMachine = new StateMachine();
		public float m_dayChaseRange;
		public float m_nightChaseRange;
		public float m_dayChaseTime;
		public float m_nightChaseTime;
		public float m_chaseNonPlayerProbability;
		public float m_chaseWhenAttackedProbability;
		public float m_chaseOnTouchProbability;
		public CreatureCategory m_autoChaseMask;
		public float m_importanceLevel;
		public float m_targetUnsuitableTime;
		public float m_targetInRangeTime;
		public double m_nextUpdateTime;
		public double m_nextPlayerCheckTime;
		public double m_lastActionTime;
		public ComponentCreature m_target;
		public float m_dt;
		public float m_range;
		public float m_chaseTime;
		public bool m_isPersistent;
		public bool m_autoDismount = true;
		public float m_autoChaseSuppressionTime;
		private AttackMode m_attackMode = AttackMode.Default;
		private Vector2 m_attackRange = new Vector2(2f, 15f);
		public float ImportanceLevelNonPersistent = 200f;
		public float ImportanceLevelPersistent = 200f;
		public float MaxAttackRange = 1.75f;
		public bool AllowAttackingStandingOnBody = true;
		public bool JumpWhenTargetStanding = true;
		public bool AttacksPlayer = true;
		public bool AttacksNonPlayerCreature = true;
		public float ChaseRangeOnTouch = 7f;
		public float ChaseTimeOnTouch = 7f;
		public float? ChaseRangeOnAttacked;
		public float? ChaseTimeOnAttacked;
		public bool? ChasePersistentOnAttacked;
		public float MinHealthToAttackActively = 0.4f;
		public bool Suppressed;
		public bool PlayIdleSoundWhenStartToChase = true;
		public bool PlayAngrySoundWhenChasing = true;
		public float TargetInRangeTimeToChase = 3f;

		public enum AttackMode
		{
			Default,
			OnlyHand,
			Ranged
		}
	}
}
