using System;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000011 RID: 17
	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000017 RID: 23
		// (get) Token: 0x060000CE RID: 206 RVA: 0x0000D6F4 File Offset: 0x0000B8F4
		public ComponentCreature Target
		{
			get
			{
				return this.m_target;
			}
		}

		// Token: 0x17000018 RID: 24
		// (get) Token: 0x060000CF RID: 207 RVA: 0x0000D70C File Offset: 0x0000B90C
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x17000019 RID: 25
		// (get) Token: 0x060000D0 RID: 208 RVA: 0x0000D720 File Offset: 0x0000B920
		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		// Token: 0x060000D1 RID: 209 RVA: 0x0000D738 File Offset: 0x0000B938
		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			bool suppressed = this.Suppressed;
			if (!suppressed)
			{
				ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
				bool flag = componentNewHerdBehavior != null;
				if (flag)
				{
					bool flag2 = !componentNewHerdBehavior.CanAttackCreature(componentCreature);
					if (flag2)
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
			}
		}

		// Token: 0x060000D2 RID: 210 RVA: 0x0000D7C0 File Offset: 0x0000B9C0
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

		// Token: 0x060000D3 RID: 211 RVA: 0x0000D828 File Offset: 0x0000BA28
		public virtual void Update(float dt)
		{
			bool suppressed = this.Suppressed;
			if (suppressed)
			{
				this.StopAttack();
			}
			this.m_autoChaseSuppressionTime -= dt;
			this.CheckDefendPlayer(dt);
			bool flag = this.IsActive && this.m_target != null;
			if (flag)
			{
				this.m_chaseTime -= dt;
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
				float num = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;
				bool flag2 = this.m_attackMode != ComponentNewChaseBehavior.AttackMode.OnlyHand;
				if (flag2)
				{
					float num2;
					ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out num2);
					bool flag3 = hitBody != null && num2 > 5f && this.FindAimTool(this.m_componentMiner);
					if (flag3)
					{
						Vector3 vector = Vector3.Normalize(this.m_target.ComponentCreatureModel.EyePosition - this.m_componentCreature.ComponentCreatureModel.EyePosition);
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
								this.m_componentPathfinding.Destination = null;
							}
							string category = BlocksManager.Blocks[Terrain.ExtractContents(this.m_componentMiner.ActiveBlockValue)].GetCategory(this.m_componentMiner.ActiveBlockValue);
							bool flag7 = this.m_subsystemTime.GameTime - this.m_lastActionTime > (double)num;
							if (flag7)
							{
								bool flag8 = this.m_componentMiner.Use(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector));
								if (flag8)
								{
									this.m_lastActionTime = this.m_subsystemTime.GameTime;
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
					bool flag12 = this.m_attackMode != ComponentNewChaseBehavior.AttackMode.OnlyHand && !this.HasActiveRangedWeaponComponent();
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

		// Token: 0x060000D4 RID: 212 RVA: 0x0000DCA8 File Offset: 0x0000BEA8
		private bool HasActiveRangedWeaponComponent()
		{
			ComponentMusketShooterBehavior componentMusketShooterBehavior = base.Entity.FindComponent<ComponentMusketShooterBehavior>();
			ComponentBowShooterBehavior componentBowShooterBehavior = base.Entity.FindComponent<ComponentBowShooterBehavior>();
			ComponentCrossbowShooterBehavior componentCrossbowShooterBehavior = base.Entity.FindComponent<ComponentCrossbowShooterBehavior>();
			ComponentFlameThrowerShooterBehavior componentFlameThrowerShooterBehavior = base.Entity.FindComponent<ComponentFlameThrowerShooterBehavior>();
			ComponentItemsLauncherShooterBehavior componentItemsLauncherShooterBehavior = base.Entity.FindComponent<ComponentItemsLauncherShooterBehavior>();
			bool flag = componentMusketShooterBehavior != null && componentMusketShooterBehavior.IsActive && this.m_target != null;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				bool flag2 = componentBowShooterBehavior != null && componentBowShooterBehavior.IsActive && this.m_target != null;
				if (flag2)
				{
					result = true;
				}
				else
				{
					bool flag3 = componentCrossbowShooterBehavior != null && componentCrossbowShooterBehavior.IsActive && this.m_target != null;
					if (flag3)
					{
						result = true;
					}
					else
					{
						bool flag4 = componentFlameThrowerShooterBehavior != null && componentFlameThrowerShooterBehavior.IsActive && this.m_target != null;
						if (flag4)
						{
							result = true;
						}
						else
						{
							bool flag5 = componentItemsLauncherShooterBehavior != null && componentItemsLauncherShooterBehavior.IsActive && this.m_target != null;
							result = flag5;
						}
					}
				}
			}
			return result;
		}

		// Token: 0x060000D5 RID: 213 RVA: 0x0000DDAC File Offset: 0x0000BFAC
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
						this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
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

		// Token: 0x060000D6 RID: 214 RVA: 0x0000DEB0 File Offset: 0x0000C0B0
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
					Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max);
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
							this.m_componentPathfinding.Destination = null;
						}
					}
				}
			}
		}

		// Token: 0x060000D7 RID: 215 RVA: 0x0000E0D4 File Offset: 0x0000C2D4
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

		// Token: 0x060000D8 RID: 216 RVA: 0x0000E2B4 File Offset: 0x0000C4B4
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

		// Token: 0x060000D9 RID: 217 RVA: 0x0000E378 File Offset: 0x0000C578
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

		// Token: 0x060000DA RID: 218 RVA: 0x0000E434 File Offset: 0x0000C634
		public bool FindAimTool(ComponentMiner componentMiner)
		{
			bool flag = componentMiner.Inventory == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				int activeSlotIndex = componentMiner.Inventory.ActiveSlotIndex;
				int activeBlockValue = componentMiner.ActiveBlockValue;
				int num = Terrain.ExtractContents(activeBlockValue);
				Block block = BlocksManager.Blocks[num];
				bool flag2 = block.IsAimable_(activeBlockValue);
				if (flag2)
				{
					bool flag3 = !(block is FlameThrowerBlock);
					if (flag3)
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
				for (int i = 0; i < componentMiner.Inventory.SlotsCount; i++)
				{
					int slotValue = componentMiner.Inventory.GetSlotValue(i);
					int num2 = Terrain.ExtractContents(slotValue);
					Block block2 = BlocksManager.Blocks[num2];
					bool flag6 = block2.IsAimable_(slotValue) && (!(block2 is FlameThrowerBlock) || this.IsReady(slotValue));
					if (flag6)
					{
						componentMiner.Inventory.ActiveSlotIndex = i;
						return true;
					}
				}
				result = false;
			}
			return result;
		}

		// Token: 0x060000DB RID: 219 RVA: 0x0000E55C File Offset: 0x0000C75C
		public bool IsReady(int slotValue)
		{
			int data = Terrain.ExtractData(slotValue);
			return !(BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock) || (FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded && FlameThrowerBlock.GetBulletType(data) != null);
		}

		// Token: 0x060000DC RID: 220 RVA: 0x0000E5A8 File Offset: 0x0000C7A8
		public bool IsAimToolNeedToReady(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
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
							return false;
						}
						bool flag5 = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetBulletType(data) != null;
						if (flag5)
						{
							return false;
						}
					}
					else
					{
						bool flag6 = RepeatCrossbowBlock.GetDraw(data) >= 15 && RepeatCrossbowBlock.GetArrowType(data) != null;
						if (flag6)
						{
							return false;
						}
					}
				}
				else
				{
					bool flag7 = CrossbowBlock.GetDraw(data) >= 15 && CrossbowBlock.GetArrowType(data) != null;
					if (flag7)
					{
						return false;
					}
				}
			}
			else
			{
				bool flag8 = BowBlock.GetDraw(data) >= 15 && BowBlock.GetArrowType(data) != null;
				if (flag8)
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x060000DD RID: 221 RVA: 0x0000E6E4 File Offset: 0x0000C8E4
		public void HandleComplexAimTool(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int num = Terrain.ExtractData(slotValue);
			int num2 = Terrain.ExtractContents(slotValue);
			Block block = BlocksManager.Blocks[num2];
			int data = num;
			bool flag = !(block is BowBlock);
			if (flag)
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
							data = MusketBlock.SetLoadState(MusketBlock.SetBulletType(0, new BulletBlock.BulletType?(BulletBlock.BulletType.Buckshot)), MusketBlock.LoadState.Loaded);
						}
					}
					else
					{
						RepeatArrowBlock.ArrowType value = this.m_random.Bool(0.8f) ? RepeatArrowBlock.ArrowType.ExplosiveArrow : RepeatArrowBlock.ArrowType.PoisonArrow;
						data = CrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(0, new RepeatArrowBlock.ArrowType?(value)), 15);
					}
				}
				else
				{
					data = CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(ArrowBlock.ArrowType.IronBolt)), 15);
				}
			}
			else
			{
				data = BowBlock.SetDraw(BowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(ArrowBlock.ArrowType.StoneArrow)), 15);
			}
			int value2 = Terrain.MakeBlockValue(num2, 0, data);
			componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
			componentMiner.Inventory.AddSlotItems(slotIndex, value2, 1);
		}

		// Token: 0x060000DE RID: 222 RVA: 0x0000E80C File Offset: 0x0000CA0C
		public bool FindHitTool(ComponentMiner componentMiner)
		{
			int activeBlockValue = componentMiner.ActiveBlockValue;
			bool flag = componentMiner.Inventory == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)].GetMeleePower(activeBlockValue) > 1f;
				if (flag2)
				{
					result = true;
				}
				else
				{
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
						result = true;
					}
					else
					{
						result = false;
					}
				}
			}
			return result;
		}

		// Token: 0x060000DF RID: 223 RVA: 0x0000E8DC File Offset: 0x0000CADC
		private void CheckDefendPlayer(float dt)
		{
			try
			{
				ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
				bool flag = componentNewHerdBehavior == null || string.IsNullOrEmpty(componentNewHerdBehavior.HerdName);
				if (!flag)
				{
					bool flag2 = !componentNewHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
					if (!flag2)
					{
						bool flag3 = this.m_subsystemTime.GameTime < this.m_nextPlayerCheckTime;
						if (!flag3)
						{
							this.m_nextPlayerCheckTime = this.m_subsystemTime.GameTime + 1.0;
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
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000E0 RID: 224 RVA: 0x0000EA44 File Offset: 0x0000CC44
		private ComponentCreature FindPlayerAttacker(ComponentPlayer player)
		{
			try
			{
				bool flag = player == null || player.ComponentBody == null;
				if (flag)
				{
					return null;
				}
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
							ComponentNewHerdBehavior componentNewHerdBehavior = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
							bool flag4 = componentNewHerdBehavior == null || !componentNewHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
							if (flag4)
							{
								ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
								bool flag5 = componentChaseBehavior != null && componentChaseBehavior.Target == player;
								if (flag5)
								{
									return componentCreature;
								}
							}
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		// Token: 0x060000E1 RID: 225 RVA: 0x0000EBC8 File Offset: 0x0000CDC8
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.m_componentFeedBehavior = base.Entity.FindComponent<ComponentRandomFeedBehavior>();
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.m_componentFactors = base.Entity.FindComponent<ComponentFactors>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_attackMode = valuesDictionary.GetValue<ComponentNewChaseBehavior.AttackMode>("AttackMode", ComponentNewChaseBehavior.AttackMode.Default);
			this.m_attackRange = valuesDictionary.GetValue<Vector2>("AttackRange", new Vector2(2f, 15f));
			this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			try
			{
				this.m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
			}
			catch
			{
				this.m_autoChaseMask = (CreatureCategory)0;
			}
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
						ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
						bool flag5 = true;
						bool flag6 = componentNewHerdBehavior != null;
						if (flag6)
						{
							flag5 = componentNewHerdBehavior.CanAttackCreature(componentCreature);
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
					ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					bool flag2 = componentNewHerdBehavior == null || componentNewHerdBehavior.CanAttackCreature(injury.Attacker);
					if (flag2)
					{
						this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent);
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
											bool flag10 = this.m_attackMode != ComponentNewChaseBehavior.AttackMode.OnlyHand && num > 5f && this.FindAimTool(this.m_componentMiner);
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
														this.m_componentPathfinding.Destination = null;
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

		// Token: 0x060000E2 RID: 226 RVA: 0x0000EED4 File Offset: 0x0000D0D4
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
					ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					bool flag2 = componentNewHerdBehavior != null;
					if (flag2)
					{
						bool flag3 = !componentNewHerdBehavior.CanAttackCreature(componentCreature);
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

		// Token: 0x060000E3 RID: 227 RVA: 0x0000EFCC File Offset: 0x0000D1CC
		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			float result = 0f;
			bool flag = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool flag2 = componentCreature == this.Target || this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool flag3 = this.m_autoChaseMask > (CreatureCategory)0;
			bool flag4 = componentCreature == this.Target || (flag3 && MathUtils.Remainder(0.004999999888241291 * this.m_subsystemTime.GameTime + (double)((float)(this.GetHashCode() % 1000) / 1000f) + (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) < (double)this.m_chaseNonPlayerProbability);
			bool flag5 = componentCreature != this.m_componentCreature && ((!flag && flag4) || (flag && flag2)) && componentCreature.Entity.IsAddedToProject && componentCreature.ComponentHealth.Health > 0f;
			if (flag5)
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				bool flag6 = num < this.m_range;
				if (flag6)
				{
					result = this.m_range - num;
				}
			}
			return result;
		}

		// Token: 0x060000E4 RID: 228 RVA: 0x0000F110 File Offset: 0x0000D310
		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f || (target.ParentBody != null && this.IsTargetInWater(target.ParentBody)) || (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInWater(target.StandingOnBody));
		}

		// Token: 0x060000E5 RID: 229 RVA: 0x0000F17C File Offset: 0x0000D37C
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

		// Token: 0x060000E6 RID: 230 RVA: 0x0000F36C File Offset: 0x0000D56C
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

		// Token: 0x060000E7 RID: 231 RVA: 0x0000F4EC File Offset: 0x0000D6EC
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

		// Token: 0x04000158 RID: 344
		public SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x04000159 RID: 345
		public SubsystemPlayers m_subsystemPlayers;

		// Token: 0x0400015A RID: 346
		public SubsystemSky m_subsystemSky;

		// Token: 0x0400015B RID: 347
		public SubsystemBodies m_subsystemBodies;

		// Token: 0x0400015C RID: 348
		public SubsystemTime m_subsystemTime;

		// Token: 0x0400015D RID: 349
		public SubsystemNoise m_subsystemNoise;

		// Token: 0x0400015E RID: 350
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;

		// Token: 0x0400015F RID: 351
		public ComponentCreature m_componentCreature;

		// Token: 0x04000160 RID: 352
		public ComponentPathfinding m_componentPathfinding;

		// Token: 0x04000161 RID: 353
		public ComponentMiner m_componentMiner;

		// Token: 0x04000162 RID: 354
		public ComponentRandomFeedBehavior m_componentFeedBehavior;

		// Token: 0x04000163 RID: 355
		public ComponentCreatureModel m_componentCreatureModel;

		// Token: 0x04000164 RID: 356
		public ComponentFactors m_componentFactors;

		// Token: 0x04000165 RID: 357
		public ComponentBody m_componentBody;

		// Token: 0x04000166 RID: 358
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();

		// Token: 0x04000167 RID: 359
		public Random m_random = new Random();

		// Token: 0x04000168 RID: 360
		public StateMachine m_stateMachine = new StateMachine();

		// Token: 0x04000169 RID: 361
		public float m_dayChaseRange;

		// Token: 0x0400016A RID: 362
		public float m_nightChaseRange;

		// Token: 0x0400016B RID: 363
		public float m_dayChaseTime;

		// Token: 0x0400016C RID: 364
		public float m_nightChaseTime;

		// Token: 0x0400016D RID: 365
		public float m_chaseNonPlayerProbability;

		// Token: 0x0400016E RID: 366
		public float m_chaseWhenAttackedProbability;

		// Token: 0x0400016F RID: 367
		public float m_chaseOnTouchProbability;

		// Token: 0x04000170 RID: 368
		public CreatureCategory m_autoChaseMask;

		// Token: 0x04000171 RID: 369
		public float m_importanceLevel;

		// Token: 0x04000172 RID: 370
		public float m_targetUnsuitableTime;

		// Token: 0x04000173 RID: 371
		public float m_targetInRangeTime;

		// Token: 0x04000174 RID: 372
		public double m_nextUpdateTime;

		// Token: 0x04000175 RID: 373
		public double m_nextPlayerCheckTime;

		// Token: 0x04000176 RID: 374
		public double m_lastActionTime;

		// Token: 0x04000177 RID: 375
		public ComponentCreature m_target;

		// Token: 0x04000178 RID: 376
		public float m_dt;

		// Token: 0x04000179 RID: 377
		public float m_range;

		// Token: 0x0400017A RID: 378
		public float m_chaseTime;

		// Token: 0x0400017B RID: 379
		public bool m_isPersistent;

		// Token: 0x0400017C RID: 380
		public bool m_autoDismount = true;

		// Token: 0x0400017D RID: 381
		public float m_autoChaseSuppressionTime;

		// Token: 0x0400017E RID: 382
		private ComponentNewChaseBehavior.AttackMode m_attackMode = ComponentNewChaseBehavior.AttackMode.Default;

		// Token: 0x0400017F RID: 383
		private Vector2 m_attackRange = new Vector2(2f, 15f);

		// Token: 0x04000180 RID: 384
		public float ImportanceLevelNonPersistent = 200f;

		// Token: 0x04000181 RID: 385
		public float ImportanceLevelPersistent = 200f;

		// Token: 0x04000182 RID: 386
		public float MaxAttackRange = 1.75f;

		// Token: 0x04000183 RID: 387
		public bool AllowAttackingStandingOnBody = true;

		// Token: 0x04000184 RID: 388
		public bool JumpWhenTargetStanding = true;

		// Token: 0x04000185 RID: 389
		public bool AttacksPlayer = true;

		// Token: 0x04000186 RID: 390
		public bool AttacksNonPlayerCreature = true;

		// Token: 0x04000187 RID: 391
		public float ChaseRangeOnTouch = 7f;

		// Token: 0x04000188 RID: 392
		public float ChaseTimeOnTouch = 7f;

		// Token: 0x04000189 RID: 393
		public float? ChaseRangeOnAttacked;

		// Token: 0x0400018A RID: 394
		public float? ChaseTimeOnAttacked;

		// Token: 0x0400018B RID: 395
		public bool? ChasePersistentOnAttacked;

		// Token: 0x0400018C RID: 396
		public float MinHealthToAttackActively = 0.4f;

		// Token: 0x0400018D RID: 397
		public bool Suppressed;

		// Token: 0x0400018E RID: 398
		public bool PlayIdleSoundWhenStartToChase = true;

		// Token: 0x0400018F RID: 399
		public bool PlayAngrySoundWhenChasing = true;

		// Token: 0x04000190 RID: 400
		public float TargetInRangeTimeToChase = 3f;

		// Token: 0x0200004E RID: 78
		public enum AttackMode
		{
			// Token: 0x040002F7 RID: 759
			Default,
			// Token: 0x040002F8 RID: 760
			OnlyHand,
			// Token: 0x040002F9 RID: 761
			Ranged
		}
	}
}
