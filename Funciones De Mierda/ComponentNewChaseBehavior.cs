using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
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

		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (this.Suppressed) return;

			ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (componentNewHerdBehavior != null)
			{
				if (!componentNewHerdBehavior.CanAttackCreature(componentCreature)) return;

				if (this.m_autoDismount)
				{
					ComponentRider componentRider = base.Entity.FindComponent<ComponentRider>();
					if (componentRider != null)
					{
						componentRider.StartDismounting();
					}
				}

				if (this.IsGuardianOrPlayerAlly() && this.IsBandit(componentCreature))
				{
					maxRange *= 1.3f;
					maxChaseTime *= 1.5f;
					isPersistent = true;
					if (this.m_importanceLevel < 280f)
					{
						this.m_importanceLevel = 280f;
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
			else
			{
				ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
				if (componentHerdBehavior != null)
				{
					ComponentHerdBehavior componentHerdBehavior2 = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
					if (componentHerdBehavior2 != null && !string.IsNullOrEmpty(componentHerdBehavior2.HerdName) && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
					{
						bool flag9 = componentHerdBehavior2.HerdName == componentHerdBehavior.HerdName;
						bool flag10 = false;
						if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
						{
							if (componentHerdBehavior2.HerdName.ToLower().Contains("guardian"))
							{
								flag10 = true;
							}
						}
						else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
						{
							if (componentHerdBehavior2.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
							{
								flag10 = true;
							}
						}
						if (flag9 || flag10) return;
					}

					if (this.IsGuardianOrPlayerAlly() && this.IsBandit(componentCreature))
					{
						maxRange *= 1.3f;
						maxChaseTime *= 1.5f;
						isPersistent = true;
						if (this.m_importanceLevel < 280f)
						{
							this.m_importanceLevel = 280f;
						}
					}

					if (this.m_autoDismount)
					{
						ComponentRider componentRider2 = base.Entity.FindComponent<ComponentRider>();
						if (componentRider2 != null)
						{
							componentRider2.StartDismounting();
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
		}

		private void UpdateChasingStateImmediately()
		{
			if (this.m_target == null || !this.IsActive) return;

			Vector3 position = this.m_target.ComponentBody.Position;
			this.m_componentPathfinding.SetDestination(new Vector3?(position), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
			this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
		}

		public void RespondToCommandImmediately(ComponentCreature target)
		{
			if (this.Suppressed || target == null) return;

			ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
			bool flag2 = true;

			if (componentNewHerdBehavior != null)
			{
				flag2 = componentNewHerdBehavior.CanAttackCreature(target);
			}
			else if (componentHerdBehavior != null)
			{
				ComponentHerdBehavior componentHerdBehavior2 = target.Entity.FindComponent<ComponentHerdBehavior>();
				if (componentHerdBehavior2 != null && !string.IsNullOrEmpty(componentHerdBehavior2.HerdName) && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
				{
					bool flag6 = componentHerdBehavior2.HerdName == componentHerdBehavior.HerdName;
					bool flag7 = false;
					if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
					{
						if (componentHerdBehavior2.HerdName.ToLower().Contains("guardian"))
						{
							flag7 = true;
						}
					}
					else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
					{
						if (componentHerdBehavior2.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
						{
							flag7 = true;
						}
					}
					if (flag6 || flag7)
					{
						flag2 = false;
					}
				}
			}

			if (!flag2) return;

			this.m_target = target;
			this.m_nextUpdateTime = 0.0;
			this.m_range = 20f;
			this.m_chaseTime = 30f;
			this.m_isPersistent = false;
			this.m_importanceLevel = this.ImportanceLevelNonPersistent;
			this.IsActive = true;
			this.m_stateMachine.TransitionTo("Chasing");

			if (this.m_target != null && this.m_componentPathfinding != null)
			{
				this.m_componentPathfinding.Stop();
				this.UpdateChasingStateImmediately();
			}
		}

		private bool IsGuardianOrPlayerAlly()
		{
			ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (componentNewHerdBehavior != null && !string.IsNullOrEmpty(componentNewHerdBehavior.HerdName))
			{
				string herdName = componentNewHerdBehavior.HerdName;
				return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName.ToLower().Contains("guardian");
			}

			ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
			if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
			{
				string herdName2 = componentHerdBehavior.HerdName;
				return herdName2.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName2.ToLower().Contains("guardian");
			}

			return false;
		}

		private bool IsBandit(ComponentCreature creature)
		{
			if (creature == null) return false;

			ComponentNewHerdBehavior componentNewHerdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (componentNewHerdBehavior != null && !string.IsNullOrEmpty(componentNewHerdBehavior.HerdName))
			{
				if (componentNewHerdBehavior.HerdName.Equals("bandit", StringComparison.OrdinalIgnoreCase))
					return true;
			}

			ComponentHerdBehavior componentHerdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();
			if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
			{
				if (componentHerdBehavior.HerdName.Equals("bandit", StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return creature.Entity.FindComponent<ComponentBanditChaseBehavior>() != null;
		}

		private void CheckNearbyBandits(float dt)
		{
			try
			{
				if (!this.IsGuardianOrPlayerAlly()) return;

				double gameTime = this.m_subsystemTime.GameTime;
				for (int i = this.m_banditMemory.Count - 1; i >= 0; i--)
				{
					if (gameTime - this.m_banditMemory[i].LastSeenTime > 30.0)
					{
						this.m_banditMemory.RemoveAt(i);
					}
				}

				if (gameTime < this.m_nextBanditCheckTime) return;

				this.m_nextBanditCheckTime = gameTime + 0.5;
				float num = 30f;
				ComponentCreature componentCreature = null;
				float num2 = 0f;

				foreach (ComponentCreature componentCreature2 in this.m_subsystemCreatureSpawn.Creatures)
				{
					if (componentCreature2 == null || componentCreature2.ComponentHealth == null || componentCreature2.ComponentHealth.Health <= 0f || componentCreature2 == this.m_componentCreature || componentCreature2.ComponentBody == null)
						continue;

					if (!this.IsBandit(componentCreature2)) continue;

					float num3 = Vector3.DistanceSquared(this.m_componentCreature.ComponentBody.Position, componentCreature2.ComponentBody.Position);
					if (num3 > num * num) continue;

					float num4 = 1f;
					bool flag7 = false;
					foreach (BanditMemory banditMemory in this.m_banditMemory)
					{
						if (banditMemory.Creature == componentCreature2)
						{
							flag7 = true;
							if (banditMemory.IsThreatening)
							{
								num4 += 2f;
							}
							banditMemory.LastSeenTime = gameTime;
							break;
						}
					}

					if (!flag7)
					{
						bool flag10 = this.IsBanditThreateningPlayer(componentCreature2);
						this.m_banditMemory.Add(new BanditMemory
						{
							Creature = componentCreature2,
							LastSeenTime = gameTime,
							IsThreatening = flag10
						});
						if (flag10)
						{
							num4 += 2f;
						}
					}

					float num5 = (float)Math.Sqrt(num3);
					num4 += (num - num5) / num * 3f;

					if (num4 > num2)
					{
						num2 = num4;
						componentCreature = componentCreature2;
					}
				}

				if (componentCreature != null && (this.m_target == null || this.m_target != componentCreature))
				{
					this.Attack(componentCreature, num, 60f, true);
					ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (componentNewHerdBehavior != null && componentNewHerdBehavior.AutoNearbyCreaturesHelp)
					{
						componentNewHerdBehavior.CallNearbyCreaturesHelp(componentCreature, num, 60f, false, true);
					}
				}
			}
			catch
			{
			}
		}

		private bool IsBanditThreateningPlayer(ComponentCreature bandit)
		{
			try
			{
				ComponentChaseBehavior componentChaseBehavior = bandit.Entity.FindComponent<ComponentChaseBehavior>();
				if (componentChaseBehavior != null && componentChaseBehavior.Target != null && componentChaseBehavior.Target.Entity.FindComponent<ComponentPlayer>() != null)
					return true;

				ComponentNewChaseBehavior componentNewChaseBehavior = bandit.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (componentNewChaseBehavior != null && componentNewChaseBehavior.Target != null && componentNewChaseBehavior.Target.Entity.FindComponent<ComponentPlayer>() != null)
					return true;

				ComponentBanditChaseBehavior componentBanditChaseBehavior = bandit.Entity.FindComponent<ComponentBanditChaseBehavior>();
				if (componentBanditChaseBehavior != null && componentBanditChaseBehavior.Target != null && componentBanditChaseBehavior.Target.Entity.FindComponent<ComponentPlayer>() != null)
					return true;

				if (this.IsFacingAnyPlayer(bandit) && this.IsHoldingRangedWeapon(bandit))
					return true;

				return false;
			}
			catch
			{
				return false;
			}
		}

		private bool IsFacingAnyPlayer(ComponentCreature creature)
		{
			foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
			{
				if (componentPlayer.ComponentHealth.Health <= 0f) continue;
				if (this.IsFacingPlayer(creature, componentPlayer))
					return true;
			}
			return false;
		}

		private bool IsHoldingRangedWeapon(ComponentCreature creature)
		{
			ComponentMiner componentMiner = creature.Entity.FindComponent<ComponentMiner>();
			if (componentMiner == null || componentMiner.Inventory == null) return false;

			int activeBlockValue = componentMiner.ActiveBlockValue;
			if (activeBlockValue == 0) return false;

			Block block = BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)];
			return block.IsAimable_(activeBlockValue);
		}

		private ComponentCreature FindNearbyBandit(float range)
		{
			try
			{
				Vector3 position = this.m_componentCreature.ComponentBody.Position;
				float num = range * range;
				foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
				{
					if (componentCreature != null && componentCreature.ComponentHealth != null && componentCreature.ComponentHealth.Health > 0f && componentCreature != this.m_componentCreature && componentCreature.ComponentBody != null && this.IsBandit(componentCreature))
					{
						float num2 = Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position);
						if (num2 <= num)
						{
							return componentCreature;
						}
					}
				}
			}
			catch
			{
			}
			return null;
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
			if (this.Suppressed)
			{
				this.StopAttack();
				return;
			}

			this.m_autoChaseSuppressionTime -= dt;

			if (this.IsGuardianOrPlayerAlly())
			{
				this.CheckNearbyBandits(dt);
			}

			if (this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive)
			{
				if (this.Suppressed)
				{
					this.Suppressed = false;
				}
				if (this.IsActive && this.m_importanceLevel < 250f)
				{
					this.m_importanceLevel = 250f;
				}
				if (this.m_target != null && this.m_attackMode != AttackMode.OnlyHand)
				{
					if (!this.FindAimTool(this.m_componentMiner) && this.m_componentMiner.Inventory != null)
					{
						for (int i = 0; i < this.m_componentMiner.Inventory.SlotsCount; i++)
						{
							int slotValue = this.m_componentMiner.Inventory.GetSlotValue(i);
							if (slotValue != 0)
							{
								Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
								if (block.IsAimable_(slotValue) && block.GetCategory(slotValue) != "Terrain")
								{
									this.m_componentMiner.Inventory.ActiveSlotIndex = i;
									break;
								}
							}
						}
					}
				}
				this.CheckHighAlertPlayerThreats(dt);
			}
			else
			{
				this.CheckDefendPlayer(dt);
			}

			if (this.IsActive && this.m_target != null)
			{
				this.m_chaseTime -= dt;
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);

				float num = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;
				float num2;
				ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out num2);

				if (this.m_attackMode != AttackMode.OnlyHand && this.FindAimTool(this.m_componentMiner))
				{
					Vector3 vector = Vector3.Normalize(this.m_target.ComponentCreatureModel.EyePosition - this.m_componentCreature.ComponentCreatureModel.EyePosition);
					if (hitBody != null)
					{
						float num3 = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
						this.m_chaseTime = Math.Max(this.m_chaseTime, num3);

						if (num2 >= this.m_attackRange.X && num2 <= this.m_attackRange.Y)
						{
							bool flag12 = Vector3.Dot(this.m_componentBody.Matrix.Forward, vector) > 0.9f;
							if (!flag12)
							{
								this.m_componentPathfinding.SetDestination(new Vector3?(this.m_target.ComponentCreatureModel.EyePosition), 1f, 1f, 0, false, true, false, null);
							}
							else
							{
								this.m_componentPathfinding.Destination = null;
								this.UpdateRangedCombatPositioning(dt);
							}

							string category = BlocksManager.Blocks[Terrain.ExtractContents(this.m_componentMiner.ActiveBlockValue)].GetCategory(this.m_componentMiner.ActiveBlockValue);
							if (this.m_subsystemTime.GameTime - this.m_lastActionTime > num)
							{
								if (this.m_componentMiner.Use(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector)))
								{
									this.m_lastActionTime = this.m_subsystemTime.GameTime;
								}
								else if (flag12 && category != "Terrain")
								{
									this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector), AimState.Completed);
									this.m_lastActionTime = this.m_subsystemTime.GameTime;
								}
							}
							else if (flag12 && category != "Terrain")
							{
								this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector), AimState.InProgress);
							}
						}
						else
						{
							this.UpdateRangedCombatPositioning(dt);
						}
					}
				}
				else
				{
					if (this.IsTargetInAttackRange(this.m_target.ComponentBody))
					{
						this.m_componentCreatureModel.AttackOrder = true;
						if (this.m_attackMode != AttackMode.OnlyHand)
						{
							this.FindHitTool(this.m_componentMiner);
						}
					}

					if (this.m_componentCreatureModel.IsAttackHitMoment)
					{
						Vector3 hitPoint;
						ComponentBody hitBody2 = this.GetHitBody(this.m_target.ComponentBody, out hitPoint);
						if (hitBody2 != null)
						{
							float x = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
							this.m_chaseTime = MathUtils.Max(this.m_chaseTime, x);
							this.m_componentMiner.Hit(hitBody2, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
							this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}

			if (this.m_subsystemTime.GameTime >= this.m_nextUpdateTime)
			{
				this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
				this.m_nextUpdateTime = this.m_subsystemTime.GameTime + this.m_dt;
				this.m_stateMachine.Update();
			}
		}

		private void UpdateRangedCombatPositioning(float dt)
		{
			if (this.m_target == null || this.m_componentPathfinding == null) return;

			float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);
			if (num > this.m_attackRange.X && num < this.m_attackRange.Y)
			{
				float num2;
				ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out num2);
				if (hitBody != null && Math.Abs(num2 - num) < 1f)
				{
					this.m_componentPathfinding.Destination = null;
					return;
				}
			}

			int maxPathfindingPositions = this.m_isPersistent ? 2000 : 500;
			Vector3 position = this.m_target.ComponentBody.Position;
			this.m_componentPathfinding.SetDestination(new Vector3?(position), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
		}

		public ComponentBody GetHitBody1(ComponentBody target, out float distance)
		{
			Vector3 vector = this.m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(target.BoundingBox.Center() - vector);
			BodyRaycastResult? bodyRaycastResult = this.PickBody(vector, direction, this.m_attackRange.Y);
			TerrainRaycastResult? terrainRaycastResult = this.PickTerrain(vector, direction, this.m_attackRange.Y);
			distance = (bodyRaycastResult != null) ? bodyRaycastResult.Value.Distance : float.PositiveInfinity;

			if (this.m_componentMiner.Inventory != null && bodyRaycastResult != null)
			{
				if (terrainRaycastResult != null && terrainRaycastResult.Value.Distance + 0.1f < bodyRaycastResult.Value.Distance)
				{
					return null;
				}
				if (bodyRaycastResult.Value.ComponentBody == target || bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) || target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) || target.StandingOnBody == bodyRaycastResult.Value.ComponentBody)
				{
					return bodyRaycastResult.Value.ComponentBody;
				}
			}
			return null;
		}

		private TerrainRaycastResult? PickTerrain(Vector3 position, Vector3 direction, float reach)
		{
			direction = Vector3.Normalize(direction);
			Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 end = position + direction * reach;
			return this.m_componentMiner.m_subsystemTerrain.Raycast(position, end, true, true, (int value, float distance) =>
				(double)Vector3.Distance(position + distance * direction, creaturePosition) <= reach &&
				BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable);
		}

		private BodyRaycastResult? PickBody(Vector3 position, Vector3 direction, float reach)
		{
			direction = Vector3.Normalize(direction);
			Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 end = position + direction * reach;
			return this.m_subsystemBodies.Raycast(position, end, 0.35f, (ComponentBody body, float distance) =>
				(double)Vector3.Distance(position + distance * direction, creaturePosition) <= reach &&
				body.Entity != this.Entity &&
				!body.IsChildOfBody(this.m_componentMiner.ComponentCreature.ComponentBody) &&
				!this.m_componentMiner.ComponentCreature.ComponentBody.IsChildOfBody(body));
		}

		public bool FindAimTool(ComponentMiner componentMiner)
		{
			if (componentMiner.Inventory == null) return false;

			int activeBlockValue = componentMiner.ActiveBlockValue;
			int num = Terrain.ExtractContents(activeBlockValue);
			Block block = BlocksManager.Blocks[num];

			if (block.IsAimable_(activeBlockValue) && block.GetCategory(activeBlockValue) != "Terrain")
			{
				if (!(block is FlameThrowerBlock) || this.IsReady(activeBlockValue))
				{
					return true;
				}
			}

			for (int i = 0; i < componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = componentMiner.Inventory.GetSlotValue(i);
				if (slotValue == 0) continue;

				int num2 = Terrain.ExtractContents(slotValue);
				Block block2 = BlocksManager.Blocks[num2];
				if (block2.IsAimable_(slotValue) && block2.GetCategory(slotValue) != "Terrain")
				{
					if (block2 is FlameThrowerBlock && !this.IsReady(slotValue)) continue;

					componentMiner.Inventory.ActiveSlotIndex = i;
					return true;
				}
			}

			return false;
		}

		public bool IsReady(int slotValue)
		{
			int data = Terrain.ExtractData(slotValue);
			if (BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock)
			{
				return FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded && FlameThrowerBlock.GetBulletType(data) != null;
			}
			return true;
		}

		public bool IsAimToolNeedToReady(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

			if (block is BowBlock)
			{
				return !(BowBlock.GetDraw(data) >= 15 && BowBlock.GetArrowType(data) != null);
			}
			if (block is CrossbowBlock)
			{
				return !(CrossbowBlock.GetDraw(data) >= 15 && CrossbowBlock.GetArrowType(data) != null);
			}
			if (block is RepeatCrossbowBlock)
			{
				return !(RepeatCrossbowBlock.GetDraw(data) >= 15 && RepeatCrossbowBlock.GetArrowType(data) != null);
			}
			if (block is MusketBlock)
			{
				return !(MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetBulletType(data) != null);
			}
			return false;
		}

		public void HandleComplexAimTool(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int num = Terrain.ExtractData(slotValue);
			int num2 = Terrain.ExtractContents(slotValue);
			Block block = BlocksManager.Blocks[num2];
			int data = num;

			if (block is BowBlock)
			{
				ArrowBlock.ArrowType[] arrowTypes = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.WoodenArrow,
					ArrowBlock.ArrowType.StoneArrow,
					ArrowBlock.ArrowType.IronArrow,
					ArrowBlock.ArrowType.DiamondArrow,
					ArrowBlock.ArrowType.FireArrow
				};
				ArrowBlock.ArrowType value4 = arrowTypes[this.m_random.Int(0, arrowTypes.Length)];
				data = BowBlock.SetDraw(BowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(value4)), 15);
			}
			else if (block is CrossbowBlock)
			{
				ArrowBlock.ArrowType[] arrowTypes = new ArrowBlock.ArrowType[]
				{
					ArrowBlock.ArrowType.WoodenArrow,
					ArrowBlock.ArrowType.StoneArrow,
					ArrowBlock.ArrowType.IronArrow
				};
				ArrowBlock.ArrowType value3 = arrowTypes[this.m_random.Int(0, arrowTypes.Length)];
				data = CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(value3)), 15);
			}
			else if (block is RepeatCrossbowBlock)
			{
				Array values = Enum.GetValues(typeof(RepeatArrowBlock.ArrowType));
				RepeatArrowBlock.ArrowType value2 = (RepeatArrowBlock.ArrowType)values.GetValue(this.m_random.Int(0, values.Length));
				data = CrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(0, new RepeatArrowBlock.ArrowType?(value2)), 15);
			}
			else if (block is MusketBlock)
			{
				data = MusketBlock.SetLoadState(MusketBlock.SetBulletType(0, new BulletBlock.BulletType?(BulletBlock.BulletType.MusketBall)), MusketBlock.LoadState.Loaded);
			}
			else if (block is FlameThrowerBlock)
			{
				this.m_lastFlameBulletType = (this.m_lastFlameBulletType == FlameBulletBlock.FlameBulletType.Flame) ?
					FlameBulletBlock.FlameBulletType.Poison : FlameBulletBlock.FlameBulletType.Flame;
				int data2 = 0;
				data2 = FlameThrowerBlock.SetBulletType(data2, new FlameBulletBlock.FlameBulletType?(this.m_lastFlameBulletType));
				data2 = FlameThrowerBlock.SetLoadState(data2, FlameThrowerBlock.LoadState.Loaded);
				data2 = FlameThrowerBlock.SetSwitchState(data2, false);
				int value5 = Terrain.MakeBlockValue(num2, 0, data2);
				value5 = FlameThrowerBlock.SetLoadCount(value5, 15);
				componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
				componentMiner.Inventory.AddSlotItems(slotIndex, value5, 1);
				return;
			}

			int value6 = Terrain.MakeBlockValue(num2, 0, data);
			componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
			componentMiner.Inventory.AddSlotItems(slotIndex, value6, 1);
		}

		public bool FindHitTool(ComponentMiner componentMiner)
		{
			if (componentMiner.Inventory == null) return false;

			int activeBlockValue = componentMiner.ActiveBlockValue;
			if (BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)].GetMeleePower(activeBlockValue) > 1f)
			{
				return true;
			}

			float num = 1f;
			int activeSlotIndex = componentMiner.Inventory.ActiveSlotIndex;
			for (int i = 0; i < componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = componentMiner.Inventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
					if (meleePower > num)
					{
						num = meleePower;
						activeSlotIndex = i;
					}
				}
			}

			if (num > 1f)
			{
				componentMiner.Inventory.ActiveSlotIndex = activeSlotIndex;
				return true;
			}
			return false;
		}

		private void CheckHighAlertPlayerThreats(float dt)
		{
			try
			{
				if (!this.IsPlayerAlly()) return;
				if (this.m_subsystemTime.GameTime < this.m_nextHighAlertCheckTime) return;

				this.m_nextHighAlertCheckTime = this.m_subsystemTime.GameTime + 0.1;
				float num = 40f;

				foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
				{
					if (componentPlayer.ComponentHealth.Health <= 0f) continue;

					ComponentCreature componentCreature = this.FindMostDangerousThreatForPlayer(componentPlayer, num);
					if (componentCreature != null && (this.m_target == null || this.m_target != componentCreature))
					{
						this.Attack(componentCreature, num, 60f, true);
						ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
						if (componentNewHerdBehavior != null && componentNewHerdBehavior.AutoNearbyCreaturesHelp)
						{
							componentNewHerdBehavior.CallNearbyCreaturesHelp(componentCreature, num, 60f, false, true);
						}
						break;
					}
				}
			}
			catch
			{
			}
		}

		private ComponentCreature FindMostDangerousThreatForPlayer(ComponentPlayer player, float range)
		{
			try
			{
				if (player == null || player.ComponentBody == null) return null;

				Vector3 position = player.ComponentBody.Position;
				float num = range * range;
				ComponentCreature result = null;
				float num2 = 0f;

				foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
				{
					if (componentCreature == null || componentCreature.ComponentHealth == null || componentCreature.ComponentHealth.Health <= 0f ||
						componentCreature == this.m_componentCreature || componentCreature == player || componentCreature.ComponentBody == null)
						continue;

					if (!this.IsCreatureThreat(componentCreature, player)) continue;

					float num3 = Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position);
					if (num3 > num) continue;

					float num4 = this.CalculateThreatLevel(componentCreature, player, num3);
					if (num4 > num2)
					{
						num2 = num4;
						result = componentCreature;
					}
				}
				return result;
			}
			catch
			{
			}
			return null;
		}

		private float CalculateThreatLevel(ComponentCreature creature, ComponentPlayer player, float distanceSquared)
		{
			float num = (float)Math.Sqrt(distanceSquared);
			float num2 = 100f / (num + 1f);

			if (this.IsZombieOrInfected(creature))
			{
				num2 *= 1.5f;
			}
			else if (this.IsBandit(creature))
			{
				num2 *= 1.8f;
			}
			else
			{
				num2 *= 1.3f;
			}

			if (this.IsFacingPlayer(creature, player))
			{
				num2 += 50f;
			}
			if (this.IsMovingTowardPlayer(creature, player))
			{
				num2 += 70f;
			}
			if (num < 10f)
			{
				num2 += 100f;
			}
			if (this.IsAttackingPlayer(creature, player))
			{
				num2 += 200f;
			}
			if (this.IsCreatureAggressive(creature))
			{
				num2 += 80f;
			}

			return num2;
		}

		private bool IsCreatureAggressive(ComponentCreature creature)
		{
			ComponentChaseBehavior componentChaseBehavior = creature.Entity.FindComponent<ComponentChaseBehavior>();
			ComponentNewChaseBehavior componentNewChaseBehavior = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
			ComponentNewChaseBehavior2 componentNewChaseBehavior2 = creature.Entity.FindComponent<ComponentNewChaseBehavior2>();
			ComponentZombieChaseBehavior componentZombieChaseBehavior = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
			ComponentBanditChaseBehavior componentBanditChaseBehavior = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();

			return (componentChaseBehavior != null && componentChaseBehavior.Target != null) ||
				   (componentNewChaseBehavior != null && componentNewChaseBehavior.Target != null) ||
				   (componentNewChaseBehavior2 != null && componentNewChaseBehavior2.Target != null) ||
				   (componentZombieChaseBehavior != null && componentZombieChaseBehavior.Target != null) ||
				   (componentBanditChaseBehavior != null && componentBanditChaseBehavior.Target != null);
		}

		private void CheckDefendPlayer(float dt)
		{
			try
			{
				if (!this.IsPlayerAlly()) return;
				if (this.m_subsystemTime.GameTime < this.m_nextPlayerCheckTime) return;

				this.m_nextPlayerCheckTime = this.m_subsystemTime.GameTime + 0.5;

				foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
				{
					if (componentPlayer.ComponentHealth.Health <= 0f) continue;

					ComponentCreature componentCreature = this.FindPlayerAttacker(componentPlayer);
					if (componentCreature != null && (this.m_target == null || this.m_target != componentCreature))
					{
						this.Attack(componentCreature, 20f, 30f, false);
					}
				}
			}
			catch
			{
			}
		}

		private ComponentCreature FindPlayerAttacker(ComponentPlayer player)
		{
			try
			{
				if (player == null || player.ComponentBody == null) return null;

				Vector3 position = player.ComponentBody.Position;
				float num = 20f;
				float num2 = num * num;

				foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
				{
					if (componentCreature != null && componentCreature.ComponentHealth != null && componentCreature.ComponentHealth.Health > 0f &&
						componentCreature != this.m_componentCreature && componentCreature.ComponentBody != null)
					{
						float num3 = Vector3.DistanceSquared(position, componentCreature.ComponentBody.Position);
						if (num3 < num2)
						{
							ComponentNewHerdBehavior componentNewHerdBehavior = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
							ComponentHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
							bool flag4 = false;

							if (componentNewHerdBehavior != null && !string.IsNullOrEmpty(componentNewHerdBehavior.HerdName))
							{
								string herdName = componentNewHerdBehavior.HerdName;
								flag4 = (herdName.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName.ToLower().Contains("guardian"));
							}
							else if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
							{
								string herdName2 = componentHerdBehavior.HerdName;
								flag4 = (herdName2.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName2.ToLower().Contains("guardian"));
							}

							if (!flag4)
							{
								ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
								ComponentNewChaseBehavior componentNewChaseBehavior = componentCreature.Entity.FindComponent<ComponentNewChaseBehavior>();

								if ((componentChaseBehavior != null && componentChaseBehavior.Target == player) ||
									(componentNewChaseBehavior != null && componentNewChaseBehavior.Target == player))
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

		private bool IsPlayerAlly()
		{
			ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (componentNewHerdBehavior != null && !string.IsNullOrEmpty(componentNewHerdBehavior.HerdName))
			{
				string herdName = componentNewHerdBehavior.HerdName;
				return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName.ToLower().Contains("guardian");
			}

			ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
			if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
			{
				string herdName2 = componentHerdBehavior.HerdName;
				return herdName2.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName2.ToLower().Contains("guardian");
			}

			return false;
		}

		private bool IsCreatureThreat(ComponentCreature creature, ComponentPlayer player)
		{
			ComponentNewHerdBehavior componentNewHerdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			ComponentHerdBehavior componentHerdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();
			bool flag = false;

			if (componentNewHerdBehavior != null && !string.IsNullOrEmpty(componentNewHerdBehavior.HerdName))
			{
				string herdName = componentNewHerdBehavior.HerdName;
				flag = (herdName.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName.ToLower().Contains("guardian"));
			}
			else if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
			{
				string herdName2 = componentHerdBehavior.HerdName;
				flag = (herdName2.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName2.ToLower().Contains("guardian"));
			}

			if (flag) return false;

			return this.IsZombieOrInfected(creature) || this.IsBandit(creature) || this.IsAttackingPlayer(creature, player);
		}

		private bool IsAttackingPlayer(ComponentCreature creature, ComponentPlayer player)
		{
			ComponentChaseBehavior componentChaseBehavior = creature.Entity.FindComponent<ComponentChaseBehavior>();
			ComponentNewChaseBehavior componentNewChaseBehavior = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
			ComponentNewChaseBehavior2 componentNewChaseBehavior2 = creature.Entity.FindComponent<ComponentNewChaseBehavior2>();
			ComponentZombieChaseBehavior componentZombieChaseBehavior = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
			ComponentBanditChaseBehavior componentBanditChaseBehavior = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();

			return (componentChaseBehavior != null && componentChaseBehavior.Target == player) ||
				   (componentNewChaseBehavior != null && componentNewChaseBehavior.Target == player) ||
				   (componentNewChaseBehavior2 != null && componentNewChaseBehavior2.Target == player) ||
				   (componentZombieChaseBehavior != null && componentZombieChaseBehavior.Target == player) ||
				   (componentBanditChaseBehavior != null && componentBanditChaseBehavior.Target == player);
		}

		private bool IsFacingPlayer(ComponentCreature creature, ComponentPlayer player)
		{
			if (creature.ComponentBody == null || player.ComponentBody == null) return false;

			Vector3 vector = player.ComponentBody.Position - creature.ComponentBody.Position;
			if (vector.LengthSquared() < 0.01f) return false;

			vector = Vector3.Normalize(vector);
			Vector3 forward = creature.ComponentBody.Matrix.Forward;
			return Vector3.Dot(forward, vector) > 0.7f;
		}

		private bool IsZombieOrInfected(ComponentCreature creature)
		{
			ComponentZombieHerdBehavior componentZombieHerdBehavior = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
			ComponentZombieChaseBehavior componentZombieChaseBehavior = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();

			if (componentZombieHerdBehavior == null && componentZombieChaseBehavior == null)
			{
				ComponentChaseBehavior componentChaseBehavior = creature.Entity.FindComponent<ComponentChaseBehavior>();
				if (componentChaseBehavior != null && componentChaseBehavior.AttacksPlayer)
				{
					ComponentHerdBehavior componentHerdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();
					if (componentHerdBehavior != null)
					{
						string text = componentHerdBehavior.HerdName.ToLower();
						if (text.Contains("hostile") || text.Contains("enemy") || text.Contains("monster"))
						{
							return true;
						}
					}
				}
			}

			return componentZombieHerdBehavior != null || componentZombieChaseBehavior != null;
		}

		private bool IsMovingTowardPlayer(ComponentCreature creature, ComponentPlayer player)
		{
			if (creature.ComponentBody == null || player.ComponentBody == null) return false;

			Vector3 vector = player.ComponentBody.Position - creature.ComponentBody.Position;
			if (vector.LengthSquared() < 0.01f) return false;

			vector = Vector3.Normalize(vector);
			Vector3 vector2 = creature.ComponentBody.Velocity;
			if (vector2.LengthSquared() < 0.1f) return false;

			vector2 = Vector3.Normalize(vector2);
			return Vector3.Dot(vector2, vector) > 0.5f;
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
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(false);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.m_componentFeedBehavior = base.Entity.FindComponent<ComponentRandomFeedBehavior>();
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.m_componentFactors = base.Entity.FindComponent<ComponentFactors>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);

			string value = valuesDictionary.GetValue<string>("AttackMode", "Default");
			this.m_attackMode = (AttackMode)Enum.Parse(typeof(AttackMode), value);
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
			this.m_nextHighAlertCheckTime = 0.0;
			this.m_lastActionTime = 0.0;
			this.m_nextBanditCheckTime = 0.0;

			ComponentBody componentBody = this.m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				if (this.m_target == null && this.m_autoChaseSuppressionTime <= 0f && this.m_random.Float(0f, 1f) < this.m_chaseOnTouchProbability)
				{
					ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
					if (componentCreature != null)
					{
						bool flag3 = this.m_subsystemPlayers.IsPlayer(body.Entity);
						bool flag4 = this.m_autoChaseMask > (CreatureCategory)0;
						ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
						bool flag5 = componentNewHerdBehavior == null || componentNewHerdBehavior.CanAttackCreature(componentCreature);
						if (flag5 && ((this.AttacksPlayer && flag3 && this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) || (this.AttacksNonPlayerCreature && !flag3 && flag4)))
						{
							float num = this.ChaseRangeOnTouch;
							float num2 = this.ChaseTimeOnTouch;
							if (this.IsGuardianOrPlayerAlly() && this.IsBandit(componentCreature))
							{
								num *= 1.2f;
								num2 *= 1.3f;
							}
							this.Attack(componentCreature, num, num2, false);
						}
					}
				}

				if (this.m_target != null && this.JumpWhenTargetStanding && body == this.m_target.ComponentBody && body.StandingOnBody == this.m_componentCreature.ComponentBody)
				{
					this.m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			}));

			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				if (injury.Attacker != null && this.m_random.Float(0f, 1f) < this.m_chaseWhenAttackedProbability)
				{
					float num = this.ChaseRangeOnAttacked ?? ((this.m_chaseWhenAttackedProbability >= 1f) ? 30f : 7f);
					float num2 = this.ChaseTimeOnAttacked ?? ((this.m_chaseWhenAttackedProbability >= 1f) ? 60f : 7f);
					bool isPersistent = this.ChasePersistentOnAttacked ?? (this.m_chaseWhenAttackedProbability >= 1f);

					if (this.IsGuardianOrPlayerAlly() && this.IsBandit(injury.Attacker))
					{
						num *= 1.3f;
						num2 *= 1.5f;
						isPersistent = true;
					}

					ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (componentNewHerdBehavior != null)
					{
						if (componentNewHerdBehavior.CanAttackCreature(injury.Attacker))
						{
							this.Attack(injury.Attacker, num, num2, isPersistent);
						}
					}
					else
					{
						ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
						if (componentHerdBehavior != null && injury.Attacker != null)
						{
							ComponentHerdBehavior componentHerdBehavior2 = injury.Attacker.Entity.FindComponent<ComponentHerdBehavior>();
							if (componentHerdBehavior2 != null && !string.IsNullOrEmpty(componentHerdBehavior2.HerdName))
							{
								bool flag7 = componentHerdBehavior2.HerdName == componentHerdBehavior.HerdName;
								bool flag8 = false;
								if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
								{
									if (componentHerdBehavior2.HerdName.ToLower().Contains("guardian"))
									{
										flag8 = true;
									}
								}
								else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
								{
									if (componentHerdBehavior2.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
									{
										flag8 = true;
									}
								}
								if (!flag7 && !flag8)
								{
									this.Attack(injury.Attacker, num, num2, isPersistent);
								}
							}
							else
							{
								this.Attack(injury.Attacker, num, num2, isPersistent);
							}
						}
						else
						{
							this.Attack(injury.Attacker, num, num2, isPersistent);
						}
					}
				}
			}));

			this.m_stateMachine.AddState("LookingForTarget", delegate
			{
				this.m_importanceLevel = 0f;
				this.m_target = null;
			}, delegate
			{
				if (this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Chasing");
				}
				else if (!this.Suppressed && this.m_autoChaseSuppressionTime <= 0f && (this.m_target == null || this.ScoreTarget(this.m_target) <= 0f) && this.m_componentCreature.ComponentHealth.Health > this.MinHealthToAttackActively)
				{
					this.m_range = ((this.m_subsystemSky.SkyLightIntensity < 0.2f) ? this.m_nightChaseRange : this.m_dayChaseRange);
					this.m_range *= this.m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);
					ComponentCreature componentCreature = this.FindTarget();

					if (componentCreature != null)
					{
						this.m_targetInRangeTime += this.m_dt;
					}
					else
					{
						this.m_targetInRangeTime = 0f;
					}

					if (this.m_targetInRangeTime > this.TargetInRangeTimeToChase)
					{
						bool flag4 = this.m_subsystemSky.SkyLightIntensity >= 0.1f;
						float maxRange = flag4 ? (this.m_dayChaseRange + 6f) : (this.m_nightChaseRange + 6f);
						float maxChaseTime = flag4 ? (this.m_dayChaseTime * this.m_random.Float(0.75f, 1f)) : (this.m_nightChaseTime * this.m_random.Float(0.75f, 1f));
						this.Attack(componentCreature, maxRange, maxChaseTime, !flag4);
					}
				}
			}, null);

			this.m_stateMachine.AddState("RandomMoving", delegate
			{
				this.m_componentPathfinding.SetDestination(new Vector3?(this.m_componentCreature.ComponentBody.Position + new Vector3(6f * this.m_random.Float(-1f, 1f), 0f, 6f * this.m_random.Float(-1f, 1f))), 1f, 1f, 0, false, true, false, null);
			}, delegate
			{
				if (!this.m_componentPathfinding.IsStuck || this.m_componentPathfinding.Destination == null)
				{
					this.m_stateMachine.TransitionTo("Chasing");
				}
				if (!this.IsActive)
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
				if (this.PlayIdleSoundWhenStartToChase)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}
				this.m_nextUpdateTime = 0.0;
			}, delegate
			{
				if (!this.IsActive)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else if (this.m_chaseTime <= 0f)
				{
					this.m_autoChaseSuppressionTime = this.m_random.Float(10f, 60f);
					this.m_importanceLevel = 0f;
				}
				else if (this.m_target == null)
				{
					this.m_importanceLevel = 0f;
				}
				else if (this.m_target.ComponentHealth.Health <= 0f)
				{
					if (this.m_componentFeedBehavior != null)
					{
						this.m_subsystemTime.QueueGameTimeDelayedExecution(this.m_subsystemTime.GameTime + this.m_random.Float(1f, 3f), delegate
						{
							if (this.m_target != null)
							{
								this.m_componentFeedBehavior.Feed(this.m_target.ComponentBody.Position);
							}
						});
					}
					this.m_importanceLevel = 0f;
				}
				else if (this.m_componentPathfinding.IsStuck)
				{
					this.m_stateMachine.TransitionTo("RandomMoving");
				}
				else
				{
					this.m_targetUnsuitableTime = ((this.ScoreTarget(this.m_target) <= 0f) ? (this.m_targetUnsuitableTime + this.m_dt) : 0f);
					if (this.m_targetUnsuitableTime > 3f)
					{
						this.m_importanceLevel = 0f;
					}
					else
					{
						int maxPathfindingPositions = 0;
						if (this.m_isPersistent)
						{
							maxPathfindingPositions = ((this.m_subsystemTime.FixedTimeStep != null) ? 2000 : 500);
						}

						BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
						BoundingBox boundingBox2 = this.m_target.ComponentBody.BoundingBox;
						Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
						Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max);
						float num = Vector3.Distance(v, vector);
						float num2 = (num < 4f) ? 0.2f : 0f;

						if (this.m_attackMode != AttackMode.OnlyHand && num > 5f && this.FindAimTool(this.m_componentMiner))
						{
							float num3;
							this.GetHitBody1(this.m_target.ComponentBody, out num3);
							if (num3 < this.m_attackRange.X + 3f)
							{
								Vector2 v2 = Vector2.Normalize(this.m_componentCreature.ComponentBody.Position.XZ - (this.m_target.ComponentBody.Position + 0.5f * this.m_target.ComponentBody.Velocity).XZ);
								Vector2 vector2 = Vector2.Zero;
								float num4 = float.MinValue;
								for (float num5 = 0f; num5 < 6.2831855f; num5 += 0.1f)
								{
									Vector2 vector3 = Vector2.CreateFromAngle(num5);
									if (Vector2.Dot(vector3, v2) > 0.2f)
									{
										float num6 = Vector2.Dot(this.m_componentCreature.ComponentBody.Matrix.Forward.XZ, vector3);
										if (num6 > num4)
										{
											vector2 = vector3;
											num4 = num6;
										}
									}
								}
								float s = 4f;
								this.m_componentPathfinding.SetDestination(new Vector3?(v + s * new Vector3(vector2.X, 0f, vector2.Y)), 1f, 1f, 0, true, true, false, null);
							}
							else if (num3 > this.m_attackRange.Y)
							{
								this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
							}
						}
						else
						{
							this.m_componentPathfinding.SetDestination(new Vector3?(vector + num2 * num * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
						}

						if (this.PlayAngrySoundWhenChasing && this.m_random.Float(0f, 1f) < 0.33f * this.m_dt)
						{
							this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
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

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature componentCreature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (componentCreature != null)
				{
					float num2 = this.ScoreTarget(componentCreature);
					if (num2 > num)
					{
						num = num2;
						result = componentCreature;
					}
				}
			}
			return result;
		}

		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			float num = 0f;
			bool flag = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool flag2 = componentCreature == this.Target || this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool flag3 = this.m_autoChaseMask > (CreatureCategory)0;
			bool flag4 = componentCreature == this.Target || (flag3 && MathUtils.Remainder(0.005 * this.m_subsystemTime.GameTime + (double)((float)(this.GetHashCode() % 1000) / 1000f) + (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) < (double)this.m_chaseNonPlayerProbability);

			ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			bool flag5 = true;

			if (componentNewHerdBehavior != null)
			{
				flag5 = componentNewHerdBehavior.CanAttackCreature(componentCreature);
			}
			else
			{
				ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
				if (componentHerdBehavior != null)
				{
					ComponentHerdBehavior componentHerdBehavior2 = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
					if (componentHerdBehavior2 != null && !string.IsNullOrEmpty(componentHerdBehavior2.HerdName))
					{
						bool flag9 = componentHerdBehavior2.HerdName == componentHerdBehavior.HerdName;
						bool flag10 = !flag9 && componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
						if (flag10)
						{
							if (componentHerdBehavior2.HerdName.ToLower().Contains("guardian"))
							{
								flag5 = false;
							}
						}
						else if (!flag9 && componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
						{
							if (componentHerdBehavior2.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
							{
								flag5 = false;
							}
						}
						else if (flag9)
						{
							flag5 = false;
						}
					}
				}
			}

			bool flag15 = componentCreature != this.m_componentCreature && flag5 && ((!flag && flag4) || (flag && flag2)) && componentCreature.Entity.IsAddedToProject && componentCreature.ComponentHealth.Health > 0f;

			if (flag15)
			{
				float num2 = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (num2 < this.m_range)
				{
					num = this.m_range - num2;
				}
			}

			if (this.IsGuardianOrPlayerAlly() && this.IsBandit(componentCreature) && flag15)
			{
				num *= 1.5f;
			}

			return num;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f || (target.ParentBody != null && this.IsTargetInWater(target.ParentBody)) || (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (this.IsBodyInAttackRange(target)) return true;

			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = target.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
			float num = vector.Length();
			Vector3 v2 = vector / num;
			float num2 = 0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
			float num3 = 0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);

			if (Math.Abs(vector.Y) < num3 * 0.99f)
			{
				if (num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else if (num < num3 + 0.3f && Math.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}

			return ((target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) || (this.AllowAttackingStandingOnBody && target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInAttackRange(target.StandingOnBody)));
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

			if (Math.Abs(vector.Y) < num3 * 0.99f)
			{
				if (num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else if (num < num3 + 0.3f && Math.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}

			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 vector = this.m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 v = target.BoundingBox.Center() - vector;
			Ray3 ray = new Ray3(vector, Vector3.Normalize(v));
			BodyRaycastResult? bodyRaycastResult = this.m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);

			if (bodyRaycastResult != null && bodyRaycastResult.Value.Distance < this.MaxAttackRange && (bodyRaycastResult.Value.ComponentBody == target || bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) || target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) || (target.StandingOnBody == bodyRaycastResult.Value.ComponentBody && this.AllowAttackingStandingOnBody)))
			{
				hitPoint = bodyRaycastResult.Value.HitPoint();
				return bodyRaycastResult.Value.ComponentBody;
			}

			hitPoint = default(Vector3);
			return null;
		}

		private double m_nextBanditCheckTime = 0.0;
		private DynamicArray<BanditMemory> m_banditMemory = new DynamicArray<BanditMemory>();
		private double m_lastActionTime;
		private FlameBulletBlock.FlameBulletType m_lastFlameBulletType = FlameBulletBlock.FlameBulletType.Flame;
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
		public SubsystemGreenNightSky m_subsystemGreenNightSky;
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
		public double m_nextHighAlertCheckTime;
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

		private class BanditMemory
		{
			public ComponentCreature Creature;
			public double LastSeenTime;
			public bool IsThreatening;
		}

		public enum AttackMode
		{
			Default,
			OnlyHand,
			Remote
		}
	}
}
