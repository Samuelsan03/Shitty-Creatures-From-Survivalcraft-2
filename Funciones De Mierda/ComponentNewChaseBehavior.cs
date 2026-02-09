using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
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

		// AGREGADO: Campos para protección contra bandidos
		private double m_nextBanditCheckTime = 0.0;

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
					if (this.m_autoDismount)
					{
						ComponentRider componentRider = base.Entity.FindComponent<ComponentRider>();
						if (componentRider != null)
						{
							componentRider.StartDismounting();
						}
					}

					// AGREGADO: Protección especial contra bandidos (siempre activa)
					if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature))
					{
						// Aumentar rango y tiempo contra bandidos
						maxRange *= 1.3f;
						maxChaseTime *= 1.5f;
						isPersistent = true;

						// Importancia alta contra bandidos
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
						ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
						{
							bool isSameHerd = targetHerd.HerdName == componentHerdBehavior.HerdName;
							bool isPlayerAlly = false;
							if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
							{
								if (targetHerd.HerdName.ToLower().Contains("guardian"))
								{
									isPlayerAlly = true;
								}
							}
							else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
							{
								if (targetHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
								{
									isPlayerAlly = true;
								}
							}

							if (isSameHerd || isPlayerAlly)
							{
								return;
							}
						}
						else
						{
							if (base.Entity.FindComponent<ComponentHerdBehavior>() != null)
							{
								bool isSameHerd = !string.IsNullOrEmpty(base.Entity.FindComponent<ComponentHerdBehavior>().HerdName) && componentCreature.Entity.FindComponent<ComponentHerdBehavior>() != null && componentCreature.Entity.FindComponent<ComponentHerdBehavior>().HerdName == base.Entity.FindComponent<ComponentHerdBehavior>().HerdName;
								if (isSameHerd)
								{
									return;
								}
							}
						}

						// AGREGADO: Protección especial contra bandidos (siempre activa)
						if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature))
						{
							// Aumentar rango y tiempo contra bandidos
							maxRange *= 1.3f;
							maxChaseTime *= 1.5f;
							isPersistent = true;

							// Importancia alta contra bandidos
							if (this.m_importanceLevel < 280f)
							{
								this.m_importanceLevel = 280f;
							}
						}

						if (this.m_autoDismount)
						{
							ComponentRider componentRider = base.Entity.FindComponent<ComponentRider>();
							if (componentRider != null)
							{
								componentRider.StartDismounting();
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
		}

		private void UpdateChasingStateImmediately()
		{
			if (this.m_target == null || !this.IsActive)
				return;
			Vector3 targetPosition = this.m_target.ComponentBody.Position;
			this.m_componentPathfinding.SetDestination(new Vector3?(targetPosition), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
			this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
		}

		public void RespondToCommandImmediately(ComponentCreature target)
		{
			if (this.Suppressed || target == null)
				return;

			ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

			bool canAttack = true;

			if (componentNewHerdBehavior != null)
			{
				canAttack = componentNewHerdBehavior.CanAttackCreature(target);
			}
			else if (componentHerdBehavior != null)
			{
				ComponentHerdBehavior targetHerd = target.Entity.FindComponent<ComponentHerdBehavior>();
				if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
				{
					bool isSameHerd = targetHerd.HerdName == componentHerdBehavior.HerdName;
					bool isPlayerAlly = false;

					if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
					{
						if (targetHerd.HerdName.ToLower().Contains("guardian"))
						{
							isPlayerAlly = true;
						}
					}
					else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
					{
						if (targetHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
						{
							isPlayerAlly = true;
						}
					}

					if (isSameHerd || isPlayerAlly)
					{
						canAttack = false;
					}
				}
			}

			if (!canAttack)
				return;

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

		// AGREGADO: Método para verificar si somos guardián o aliado del jugador
		private bool IsGuardianOrPlayerAlly()
		{
			ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			ComponentHerdBehavior oldHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

			if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
			{
				string herdName = herdBehavior.HerdName;
				return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
					   herdName.ToLower().Contains("guardian");
			}
			else if (oldHerdBehavior != null && !string.IsNullOrEmpty(oldHerdBehavior.HerdName))
			{
				string herdName = oldHerdBehavior.HerdName;
				return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
					   herdName.ToLower().Contains("guardian");
			}

			return false;
		}

		// AGREGADO: Verificar si es bandido
		private bool IsBandit(ComponentCreature creature)
		{
			if (creature == null) return false;

			// Verificar manada nueva
			ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
			{
				string herdName = newHerd.HerdName.ToLower();
				if (herdName.Contains("bandit") || herdName.Contains("outlaw") || herdName.Contains("raider"))
					return true;
			}

			// Verificar manada vieja
			ComponentHerdBehavior oldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();
			if (oldHerd != null && !string.IsNullOrEmpty(oldHerd.HerdName))
			{
				string herdName = oldHerd.HerdName.ToLower();
				if (herdName.Contains("bandit") || herdName.Contains("outlaw") || herdName.Contains("raider"))
					return true;
			}

			// Verificar componente específico de bandido
			ComponentBanditHerdBehavior banditHerd = creature.Entity.FindComponent<ComponentBanditHerdBehavior>();
			if (banditHerd != null)
				return true;

			ComponentBanditChaseBehavior banditChase = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();
			if (banditChase != null)
				return true;

			return false;
		}

		// AGREGADO: Chequear bandidos cercanos
		private void CheckNearbyBandits(float dt)
		{
			try
			{
				// Solo si somos aliados del jugador
				if (!IsGuardianOrPlayerAlly())
					return;

				// Chequear cada 1 segundo
				bool timeToCheck = this.m_subsystemTime.GameTime >= this.m_nextBanditCheckTime;
				if (timeToCheck)
				{
					this.m_nextBanditCheckTime = this.m_subsystemTime.GameTime + 1.0;

					// Rango extendido para detectar bandidos
					float banditDetectionRange = 25f;

					// Buscar bandidos cercanos
					ComponentCreature nearbyBandit = FindNearbyBandit(banditDetectionRange);

					if (nearbyBandit != null && (this.m_target == null || this.m_target != nearbyBandit))
					{
						// Atacar bandido inmediatamente
						this.Attack(nearbyBandit, banditDetectionRange, 40f, true);

						// Llamar ayuda si es posible
						ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
						if (herdBehavior != null && herdBehavior.AutoNearbyCreaturesHelp)
						{
							herdBehavior.CallNearbyCreaturesHelp(nearbyBandit, banditDetectionRange, 40f, false, true);
						}
					}
				}
			}
			catch { }
		}

		// AGREGADO: Encontrar bandido cercano
		private ComponentCreature FindNearbyBandit(float range)
		{
			try
			{
				Vector3 myPosition = this.m_componentCreature.ComponentBody.Position;
				float rangeSquared = range * range;

				foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
				{
					if (creature != null &&
						creature.ComponentHealth != null &&
						creature.ComponentHealth.Health > 0f &&
						creature != this.m_componentCreature &&
						creature.ComponentBody != null)
					{
						// Verificar si es bandido
						if (!IsBandit(creature))
							continue;

						// Verificar distancia
						float distanceSquared = Vector3.DistanceSquared(myPosition, creature.ComponentBody.Position);
						if (distanceSquared > rangeSquared)
							continue;

						// Devolver el primer bandido encontrado
						return creature;
					}
				}
			}
			catch { }

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

			// AGREGADO: Chequear bandidos cercanos (siempre activo para guardianes/aliados)
			if (IsGuardianOrPlayerAlly())
			{
				CheckNearbyBandits(dt);
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
				ComponentBody hitBodyByAim = this.GetHitBody1(this.m_target.ComponentBody, out num2);
				if (this.m_attackMode != ComponentNewChaseBehavior.AttackMode.OnlyHand && num2 > 5f && this.FindAimTool(this.m_componentMiner))
				{
					Vector3 vector = Vector3.Normalize(this.m_target.ComponentCreatureModel.EyePosition - this.m_componentCreature.ComponentCreatureModel.EyePosition);
					if (hitBodyByAim != null)
					{
						this.m_chaseTime = Math.Max(this.m_chaseTime, this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f);
						if ((double)num2 >= (double)this.m_attackRange.X && (double)num2 <= (double)this.m_attackRange.Y)
						{
							bool flag = (double)Vector3.Dot(this.m_componentBody.Matrix.Forward, vector) > 0.8;
							if (!flag)
							{
								this.m_componentPathfinding.SetDestination(new Vector3?(this.m_target.ComponentCreatureModel.EyePosition), 1f, 1f, 0, false, true, false, null);
							}
							else
							{
								this.m_componentPathfinding.Destination = null;
							}
							string category = BlocksManager.Blocks[Terrain.ExtractContents(this.m_componentMiner.ActiveBlockValue)].GetCategory(this.m_componentMiner.ActiveBlockValue);
							if (this.m_subsystemTime.GameTime - this.m_lastActionTime > (double)num)
							{
								if (this.m_componentMiner.Use(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector)))
								{
									this.m_lastActionTime = this.m_subsystemTime.GameTime;
								}
								else if (flag && category != "Terrain")
								{
									this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector), AimState.Completed);
									this.m_lastActionTime = this.m_subsystemTime.GameTime;
								}
							}
							else if (flag && category != "Terrain")
							{
								this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, vector), AimState.InProgress);
							}
						}
					}
				}
				else
				{
					if (this.IsTargetInAttackRange(this.m_target.ComponentBody))
					{
						this.m_componentCreatureModel.AttackOrder = true;
						if (this.m_attackMode != ComponentNewChaseBehavior.AttackMode.OnlyHand)
						{
							this.FindHitTool(this.m_componentMiner);
						}
					}
					if (this.m_componentCreatureModel.IsAttackHitMoment)
					{
						Vector3 hitPoint;
						ComponentBody hitBody = this.GetHitBody(this.m_target.ComponentBody, out hitPoint);
						if (hitBody != null)
						{
							float x = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
							this.m_chaseTime = MathUtils.Max(this.m_chaseTime, x);
							this.m_componentMiner.Hit(hitBody, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
							this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}
			}

			if (this.m_subsystemTime.GameTime >= this.m_nextUpdateTime)
			{
				this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
				this.m_nextUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_dt;
				this.m_stateMachine.Update();
			}
		}

		public ComponentBody GetHitBody1(ComponentBody target, out float distance)
		{
			Vector3 vector = this.m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 direction = Vector3.Normalize(target.BoundingBox.Center() - vector);
			BodyRaycastResult? bodyRaycastResult = this.PickBody(vector, direction, this.m_attackRange.Y);
			TerrainRaycastResult? terrainRaycastResult = this.PickTerrain(vector, direction, this.m_attackRange.Y);
			distance = ((bodyRaycastResult != null) ? bodyRaycastResult.GetValueOrDefault().Distance : float.PositiveInfinity);
			if (this.m_componentMiner.Inventory != null && bodyRaycastResult != null)
			{
				if (terrainRaycastResult != null && (double)terrainRaycastResult.Value.Distance < (double)bodyRaycastResult.Value.Distance)
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
			return this.m_componentMiner.m_subsystemTerrain.Raycast(position, end, true, true, (int value, float distance) => (double)Vector3.Distance(position + distance * direction, creaturePosition) <= (double)reach && BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable);
		}

		private BodyRaycastResult? PickBody(Vector3 position, Vector3 direction, float reach)
		{
			direction = Vector3.Normalize(direction);
			Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 end = position + direction * reach;
			return this.m_subsystemBodies.Raycast(position, end, 0.35f, (ComponentBody body, float distance) => (double)Vector3.Distance(position + distance * direction, creaturePosition) <= (double)reach && body.Entity != this.Entity && !body.IsChildOfBody(this.m_componentMiner.ComponentCreature.ComponentBody) && !this.m_componentMiner.ComponentCreature.ComponentBody.IsChildOfBody(body));
		}

		public bool FindAimTool(ComponentMiner componentMiner)
		{
			if (componentMiner.Inventory == null)
			{
				return false;
			}
			int activeSlotIndex = componentMiner.Inventory.ActiveSlotIndex;
			int activeBlockValue = componentMiner.ActiveBlockValue;
			int num = Terrain.ExtractContents(activeBlockValue);
			Block block = BlocksManager.Blocks[num];
			if (block.IsAimable_(activeBlockValue) && block.GetCategory(activeBlockValue) != "Terrain")
			{
				if (!(block is FlameThrowerBlock))
				{
					if (this.IsAimToolNeedToReady(componentMiner, activeSlotIndex))
					{
						this.HandleComplexAimTool(componentMiner, activeSlotIndex);
					}
					return true;
				}
				else
				{
					// Es un FlameThrowerBlock
					if (this.IsReady(activeBlockValue))
					{
						return true;
					}
					else
					{
						// No está listo, recargarlo
						this.HandleComplexAimTool(componentMiner, activeSlotIndex);
						return true;
					}
				}
			}
			for (int i = 0; i < componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = componentMiner.Inventory.GetSlotValue(i);
				int num2 = Terrain.ExtractContents(slotValue);
				Block block2 = BlocksManager.Blocks[num2];
				if (block2.IsAimable_(slotValue) && block2.GetCategory(slotValue) != "Terrain" && (!(block2 is FlameThrowerBlock) || this.IsReady(slotValue)))
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
			if (BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock)
			{
				// Verificar que esté cargado y tenga tipo de bala
				return FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded &&
					   FlameThrowerBlock.GetBulletType(data) != null;
			}
			return !(BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock) ||
				   (FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded &&
					FlameThrowerBlock.GetBulletType(data) != null);
		}

		public bool IsAimToolNeedToReady(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
			if (!(block is BowBlock))
			{
				if (!(block is CrossbowBlock))
				{
					if (!(block is RepeatCrossbowBlock))
					{
						if (!(block is MusketBlock))
						{
							return false;
						}
						if (MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && MusketBlock.GetBulletType(data) != null)
						{
							return false;
						}
					}
					else if (RepeatCrossbowBlock.GetDraw(data) >= 15 && RepeatCrossbowBlock.GetArrowType(data) != null)
					{
						return false;
					}
				}
				else if (CrossbowBlock.GetDraw(data) >= 15 && CrossbowBlock.GetArrowType(data) != null)
				{
					return false;
				}
			}
			else if (BowBlock.GetDraw(data) >= 15 && BowBlock.GetArrowType(data) != null)
			{
				return false;
			}
			return true;
		}

		public void HandleComplexAimTool(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int num = Terrain.ExtractData(slotValue);
			int num2 = Terrain.ExtractContents(slotValue);
			Block block = BlocksManager.Blocks[num2];
			int data = num;
			if (!(block is BowBlock))
			{
				if (!(block is CrossbowBlock))
				{
					if (!(block is RepeatCrossbowBlock))
					{
						if (block is MusketBlock)
						{
							data = MusketBlock.SetLoadState(MusketBlock.SetBulletType(0, new BulletBlock.BulletType?(BulletBlock.BulletType.MusketBall)), MusketBlock.LoadState.Loaded);
						}
						else if (block is FlameThrowerBlock)
						{
							// Alternar entre fuego y veneno
							if (this.m_lastFlameBulletType == FlameBulletBlock.FlameBulletType.Flame)
								this.m_lastFlameBulletType = FlameBulletBlock.FlameBulletType.Poison;
							else
								this.m_lastFlameBulletType = FlameBulletBlock.FlameBulletType.Flame;

							// Primero, establecer el tipo de bala y el estado de carga
							int newData = 0;
							newData = FlameThrowerBlock.SetBulletType(newData, this.m_lastFlameBulletType);
							newData = FlameThrowerBlock.SetLoadState(newData, FlameThrowerBlock.LoadState.Loaded);
							// El interruptor debe estar apagado (false) al recargar
							newData = FlameThrowerBlock.SetSwitchState(newData, false);
							// Crear el valor del bloque
							int flameThrowerValue = Terrain.MakeBlockValue(num2, 0, newData);
							// Establecer el conteo de carga a 15
							flameThrowerValue = FlameThrowerBlock.SetLoadCount(flameThrowerValue, 15);

							componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
							componentMiner.Inventory.AddSlotItems(slotIndex, flameThrowerValue, 1);
							return;
						}
					}
					else
					{
						// MODIFICADO: Elegir aleatoriamente entre TODOS los tipos de flechas de repeat
						Array repeatArrowTypes = Enum.GetValues(typeof(RepeatArrowBlock.ArrowType));
						RepeatArrowBlock.ArrowType randomRepeatArrowType = (RepeatArrowBlock.ArrowType)repeatArrowTypes.GetValue(this.m_random.Int(0, repeatArrowTypes.Length));
						data = CrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(0, new RepeatArrowBlock.ArrowType?(randomRepeatArrowType)), 15);
					}
				}
				else
				{
					// MODIFICADO: Elegir aleatoriamente entre virotes (bolts)
					ArrowBlock.ArrowType[] crossbowBoltTypes = new ArrowBlock.ArrowType[]
					{
				ArrowBlock.ArrowType.IronBolt,
				ArrowBlock.ArrowType.DiamondBolt,
				ArrowBlock.ArrowType.ExplosiveBolt
					};
					ArrowBlock.ArrowType randomCrossbowBoltType = crossbowBoltTypes[this.m_random.Int(0, crossbowBoltTypes.Length)];
					data = CrossbowBlock.SetDraw(CrossbowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(randomCrossbowBoltType)), 15);
				}
			}
			else
			{
				// MODIFICADO: Elegir aleatoriamente entre flechas normales (no virote)
				ArrowBlock.ArrowType[] bowArrowTypes = new ArrowBlock.ArrowType[]
				{
			ArrowBlock.ArrowType.WoodenArrow,
			ArrowBlock.ArrowType.StoneArrow,
			ArrowBlock.ArrowType.IronArrow,
			ArrowBlock.ArrowType.DiamondArrow,
			ArrowBlock.ArrowType.CopperArrow
				};
				ArrowBlock.ArrowType randomBowArrowType = bowArrowTypes[this.m_random.Int(0, bowArrowTypes.Length)];
				data = BowBlock.SetDraw(BowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(randomBowArrowType)), 15);
			}

			// Crear valor del bloque para armas que no son lanzallamas
			int weaponValue = Terrain.MakeBlockValue(num2, 0, data);
			componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
			componentMiner.Inventory.AddSlotItems(slotIndex, weaponValue, 1);
		}

		public bool FindHitTool(ComponentMiner componentMiner)
		{
			int activeBlockValue = componentMiner.ActiveBlockValue;
			if (componentMiner.Inventory == null)
			{
				return false;
			}
			if (BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)].GetMeleePower(activeBlockValue) > 1f)
			{
				return true;
			}
			float num = 1f;
			int activeSlotIndex = 0;
			int activeSlotValue = componentMiner.Inventory.GetSlotValue(componentMiner.Inventory.ActiveSlotIndex);
			float activeMeleePower = BlocksManager.Blocks[Terrain.ExtractContents(activeSlotValue)].GetMeleePower(activeSlotValue);
			if (activeMeleePower > 1f)
			{
				return true;
			}

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
				if (!IsPlayerAlly())
					return;

				if (this.m_subsystemTime.GameTime < this.m_nextHighAlertCheckTime)
					return;

				this.m_nextHighAlertCheckTime = this.m_subsystemTime.GameTime + 0.1;

				float highAlertRange = 40f;

				foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
				{
					if (componentPlayer.ComponentHealth.Health <= 0f)
						continue;

					ComponentCreature approachingZombie = FindApproachingZombie(componentPlayer, highAlertRange);

					if (approachingZombie != null && (this.m_target == null || this.m_target != approachingZombie))
					{
						this.Attack(approachingZombie, highAlertRange, 60f, true);

						ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
						if (herdBehavior != null && herdBehavior.AutoNearbyCreaturesHelp)
						{
							herdBehavior.CallNearbyCreaturesHelp(approachingZombie, highAlertRange, 60f, false, true);
						}

						return;
					}
				}
			}
			catch { }
		}

		private void CheckDefendPlayer(float dt)
		{
			try
			{
				if (!IsPlayerAlly())
					return;

				if (this.m_subsystemTime.GameTime < this.m_nextPlayerCheckTime)
					return;

				this.m_nextPlayerCheckTime = this.m_subsystemTime.GameTime + 0.5;

				foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
				{
					if (componentPlayer.ComponentHealth.Health <= 0f)
						continue;

					ComponentCreature componentCreature = this.FindPlayerAttacker(componentPlayer);
					if (componentCreature != null && (this.m_target == null || this.m_target != componentCreature))
					{
						this.Attack(componentCreature, 20f, 30f, false);
					}
				}
			}
			catch { }
		}

		private ComponentCreature FindApproachingZombie(ComponentPlayer player, float range)
		{
			try
			{
				if (player == null || player.ComponentBody == null)
					return null;

				Vector3 playerPosition = player.ComponentBody.Position;
				float rangeSquared = range * range;

				ComponentCreature mostThreateningZombie = null;
				float highestThreatScore = 0f;

				foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
				{
					if (creature == null || creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f ||
						creature == this.m_componentCreature || creature.ComponentBody == null)
						continue;

					if (!IsZombieOrInfected(creature))
						continue;

					float distanceSquared = Vector3.DistanceSquared(playerPosition, creature.ComponentBody.Position);
					if (distanceSquared > rangeSquared)
						continue;

					float threatScore = CalculateZombieThreatScore(creature, player, distanceSquared);

					if (threatScore > highestThreatScore)
					{
						highestThreatScore = threatScore;
						mostThreateningZombie = creature;
					}
				}

				return mostThreateningZombie;
			}
			catch { }

			return null;
		}

		private float CalculateZombieThreatScore(ComponentCreature zombie, ComponentPlayer player, float distanceSquared)
		{
			float threatScore = 0f;

			float distance = (float)Math.Sqrt(distanceSquared);
			threatScore = 100f / (distance + 1f);

			if (IsFacingPlayer(zombie, player))
			{
				threatScore += 50f;
			}

			if (IsMovingTowardPlayer(zombie, player))
			{
				threatScore += 70f;
			}

			if (distance < 10f)
			{
				threatScore += 100f;
			}

			ComponentChaseBehavior chase = zombie.Entity.FindComponent<ComponentChaseBehavior>();
			ComponentNewChaseBehavior newChase = zombie.Entity.FindComponent<ComponentNewChaseBehavior>();
			ComponentNewChaseBehavior2 newChase2 = zombie.Entity.FindComponent<ComponentNewChaseBehavior2>();
			ComponentZombieChaseBehavior zombieChase = zombie.Entity.FindComponent<ComponentZombieChaseBehavior>();

			if ((chase != null && chase.Target != null) ||
				(newChase != null && newChase.Target != null) ||
				(newChase2 != null && newChase2.Target != null) ||
				(zombieChase != null && zombieChase.Target != null))
			{
				threatScore += 80f;
			}

			return threatScore;
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
							ComponentNewHerdBehavior componentNewHerdBehavior = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
							ComponentHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
							bool isPlayerAlly = false;

							if (componentNewHerdBehavior != null)
							{
								string herdName = componentNewHerdBehavior.HerdName;
								isPlayerAlly = herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
											  herdName.ToLower().Contains("guardian");
							}
							else if (componentHerdBehavior != null)
							{
								string herdName = componentHerdBehavior.HerdName;
								isPlayerAlly = herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
											  herdName.ToLower().Contains("guardian");
							}

							if (!isPlayerAlly)
							{
								ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
								ComponentNewChaseBehavior componentNewChaseBehavior = componentCreature.Entity.FindComponent<ComponentNewChaseBehavior>();
								bool isAttackingPlayer = false;
								if (componentChaseBehavior != null && componentChaseBehavior.Target == player) isAttackingPlayer = true;
								else if (componentNewChaseBehavior != null && componentNewChaseBehavior.Target == player) isAttackingPlayer = true;
								if (isAttackingPlayer) return componentCreature;
							}
						}
					}
				}
			}
			catch { }
			return null;
		}

		private bool IsPlayerAlly()
		{
			ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			ComponentHerdBehavior oldHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

			if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
			{
				string herdName = herdBehavior.HerdName;
				return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
					   herdName.ToLower().Contains("guardian");
			}
			else if (oldHerdBehavior != null && !string.IsNullOrEmpty(oldHerdBehavior.HerdName))
			{
				string herdName = oldHerdBehavior.HerdName;
				return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
					   herdName.ToLower().Contains("guardian");
			}

			return false;
		}

		private bool IsFacingPlayer(ComponentCreature creature, ComponentPlayer player)
		{
			if (creature.ComponentBody == null || player.ComponentBody == null)
				return false;

			Vector3 toPlayer = player.ComponentBody.Position - creature.ComponentBody.Position;
			if (toPlayer.LengthSquared() < 0.01f)
				return false;

			toPlayer = Vector3.Normalize(toPlayer);
			Vector3 creatureForward = creature.ComponentBody.Matrix.Forward;

			float dot = Vector3.Dot(creatureForward, toPlayer);
			return dot > 0.7f;
		}

		private bool IsZombieOrInfected(ComponentCreature creature)
		{
			ComponentZombieHerdBehavior zombieHerd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
			ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();

			if (zombieHerd == null && zombieChase == null)
			{
				ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
				if (chase != null && chase.AttacksPlayer)
				{
					ComponentHerdBehavior herd = creature.Entity.FindComponent<ComponentHerdBehavior>();
					if (herd != null)
					{
						string herdName = herd.HerdName.ToLower();
						if (herdName.Contains("hostile") || herdName.Contains("enemy") || herdName.Contains("monster"))
						{
							return true;
						}
					}
				}
			}

			return (zombieHerd != null || zombieChase != null);
		}

		private bool IsMovingTowardPlayer(ComponentCreature creature, ComponentPlayer player)
		{
			if (creature.ComponentBody == null || player.ComponentBody == null)
				return false;

			Vector3 toPlayer = player.ComponentBody.Position - creature.ComponentBody.Position;
			if (toPlayer.LengthSquared() < 0.01f)
				return false;

			toPlayer = Vector3.Normalize(toPlayer);
			Vector3 creatureVelocity = creature.ComponentBody.Velocity;

			if (creatureVelocity.LengthSquared() < 0.1f)
				return false;

			creatureVelocity = Vector3.Normalize(creatureVelocity);
			float dot = Vector3.Dot(creatureVelocity, toPlayer);
			return dot > 0.5f;
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
			string attackModeString = valuesDictionary.GetValue<string>("AttackMode", "Default");
			this.m_attackMode = (ComponentNewChaseBehavior.AttackMode)Enum.Parse(typeof(ComponentNewChaseBehavior.AttackMode), attackModeString);
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
			this.m_nextHighAlertCheckTime = 0.0;
			this.m_lastActionTime = 0.0;
			this.m_nextBanditCheckTime = 0.0; // AGREGADO: Inicializar tiempo para chequeo de bandidos

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
							// AGREGADO: Mejorar ataque si es bandido
							float chaseRange = this.ChaseRangeOnTouch;
							float chaseTime = this.ChaseTimeOnTouch;

							if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature))
							{
								chaseRange *= 1.2f;
								chaseTime *= 1.3f;
							}

							this.Attack(componentCreature, chaseRange, chaseTime, false);
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

					// AGREGADO: Mejorar respuesta si el atacante es bandido
					if (IsGuardianOrPlayerAlly() && IsBandit(injury.Attacker))
					{
						maxRange *= 1.3f;
						maxChaseTime *= 1.5f;
						isPersistent = true;
					}

					ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					bool flag2 = true;
					bool flag3 = componentNewHerdBehavior != null;
					if (flag3)
					{
						flag2 = componentNewHerdBehavior.CanAttackCreature(injury.Attacker);
					}
					else
					{
						ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
						if (componentHerdBehavior != null && injury.Attacker != null)
						{
							ComponentHerdBehavior attackerHerd = injury.Attacker.Entity.FindComponent<ComponentHerdBehavior>();
							if (attackerHerd != null && !string.IsNullOrEmpty(attackerHerd.HerdName))
							{
								bool isSameHerd = attackerHerd.HerdName == componentHerdBehavior.HerdName;
								bool isPlayerAlly = false;

								if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
								{
									if (attackerHerd.HerdName.ToLower().Contains("guardian"))
									{
										isPlayerAlly = true;
									}
								}
								else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
								{
									if (attackerHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
									{
										isPlayerAlly = true;
									}
								}

								if (isSameHerd || isPlayerAlly)
								{
									flag2 = false;
								}
							}
						}
					}
					if (flag2)
					{
						this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent);
					}
					else
					{
						ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
						if (componentHerdBehavior != null && injury.Attacker != null)
						{
							ComponentHerdBehavior attackerHerd = injury.Attacker.Entity.FindComponent<ComponentHerdBehavior>();
							if (attackerHerd != null && !string.IsNullOrEmpty(attackerHerd.HerdName))
							{
								bool isSameHerd = attackerHerd.HerdName == componentHerdBehavior.HerdName;
								bool isPlayerAlly = false;

								if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
								{
									if (attackerHerd.HerdName.ToLower().Contains("guardian"))
									{
										isPlayerAlly = true;
									}
								}
								else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
								{
									if (attackerHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
									{
										isPlayerAlly = true;
									}
								}

								if (isSameHerd || isPlayerAlly)
								{
									flag2 = false;
								}
							}
						}
					}
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
				if (this.IsActive)
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
				if (!this.IsActive)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else
				{
					if (this.m_chaseTime <= 0f)
					{
						this.m_autoChaseSuppressionTime = this.m_random.Float(10f, 60f);
						this.m_importanceLevel = 0f;
					}
					else
					{
						if (this.m_target == null)
						{
							this.m_importanceLevel = 0f;
						}
						else
						{
							if (this.m_target.ComponentHealth.Health <= 0f)
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
								if (this.m_componentPathfinding.IsStuck)
								{
									this.m_stateMachine.TransitionTo("RandomMoving");
								}
								else
								{
									this.m_targetUnsuitableTime = ((this.ScoreTarget(this.m_target) <= 0f) ? (this.m_targetUnsuitableTime + this.m_dt) : 0f);
									float num = 3f;
									if (this.m_targetUnsuitableTime > num)
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
										float num2 = Vector3.Distance(v, vector);
										float num3 = 4f;
										float num4 = 0.2f;
										float num5 = 0f;
										float num6 = (num2 < num3) ? num4 : num5;
										float num7 = 5f;
										if (this.m_attackMode != ComponentNewChaseBehavior.AttackMode.OnlyHand && num2 > num7 && this.FindAimTool(this.m_componentMiner))
										{
											float num8;
											this.GetHitBody1(this.m_target.ComponentBody, out num8);
											float num9 = 3f;
											if (num8 < this.m_attackRange.X + num9)
											{
												Vector2 v2 = Vector2.Normalize(this.m_componentCreature.ComponentBody.Position.XZ - (this.m_target.ComponentBody.Position + 0.5f * this.m_target.ComponentBody.Velocity).XZ);
												Vector2 vector2 = Vector2.Zero;
												float num10 = float.MinValue;
												float num11 = 0.1f;
												float num12 = 6.2831855f;
												float num13 = 0.2f;
												for (float num14 = 0f; num14 < num12; num14 += num11)
												{
													Vector2 vector3 = Vector2.CreateFromAngle(num14);
													if (Vector2.Dot(vector3, v2) > num13)
													{
														float num15 = Vector2.Dot(this.m_componentCreature.ComponentBody.Matrix.Forward.XZ, vector3);
														if (num15 > num10)
														{
															vector2 = vector3;
															num10 = num15;
														}
													}
												}
												float s = 4f;
												this.m_componentPathfinding.SetDestination(new Vector3?(v + s * new Vector3(vector2.X, 0f, vector2.Y)), 1f, 1f, 0, true, true, false, null);
											}
											else if (num8 > this.m_attackRange.Y)
											{
												this.m_componentPathfinding.SetDestination(new Vector3?(vector + num6 * num2 * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
											}
										}
										else
										{
											this.m_componentPathfinding.SetDestination(new Vector3?(vector + num6 * num2 * this.m_target.ComponentBody.Velocity), 1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
										}
										float num16 = 0.33f;
										if (this.PlayAngrySoundWhenChasing && this.m_random.Float(0f, 1f) < num16 * this.m_dt)
										{
											this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
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
			float result = 0f;
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
					ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
					if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName))
					{
						bool isSameHerd = targetHerd.HerdName == componentHerdBehavior.HerdName;

						if (!isSameHerd && componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
						{
							if (targetHerd.HerdName.ToLower().Contains("guardian"))
							{
								flag5 = false;
							}
						}
						else if (!isSameHerd && componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
						{
							if (targetHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
							{
								flag5 = false;
							}
						}
						else if (isSameHerd)
						{
							flag5 = false;
						}
					}
				}
			}
			bool flag6 = componentCreature != this.m_componentCreature && flag5 && ((!flag && flag4) || (flag && flag2)) && componentCreature.Entity.IsAddedToProject && componentCreature.ComponentHealth.Health > 0f;
			if (flag6)
			{
				float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (num < this.m_range)
				{
					result = this.m_range - num;
				}
			}

			// AGREGADO: Bonus extra si es bandido y somos aliados del jugador
			if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature) && flag6)
			{
				result *= 1.5f; // 50% más de score para bandidos
			}

			return result;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f || (target.ParentBody != null && this.IsTargetInWater(target.ParentBody)) || (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (this.IsBodyInAttackRange(target))
			{
				return true;
			}
			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = target.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
			float num = vector.Length();
			Vector3 v2 = vector / num;
			float num2 = 0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
			float num3 = 0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);
			if (MathF.Abs(vector.Y) < num3 * 0.99f)
			{
				if (num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else if (num < num3 + 0.3f && MathF.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}
			return (target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) || (this.AllowAttackingStandingOnBody && target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInAttackRange(target.StandingOnBody));
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
			if (MathF.Abs(vector.Y) < num3 * 0.99f)
			{
				if (num < num2 + 0.99f && Vector3.Dot(v2, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else if (num < num3 + 0.3f && MathF.Abs(Vector3.Dot(v2, Vector3.UnitY)) > 0.8f)
			{
				return true;
			}
			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 vector = this.m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 v = target.BoundingBox.Center();
			Ray3 ray = new Ray3(vector, Vector3.Normalize(v - vector));
			BodyRaycastResult? bodyRaycastResult = this.m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
			if (bodyRaycastResult != null && bodyRaycastResult.Value.Distance < this.MaxAttackRange && (bodyRaycastResult.Value.ComponentBody == target || bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) || target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) || (target.StandingOnBody == bodyRaycastResult.Value.ComponentBody && this.AllowAttackingStandingOnBody)))
			{
				hitPoint = bodyRaycastResult.Value.HitPoint();
				return bodyRaycastResult.Value.ComponentBody;
			}
			hitPoint = default(Vector3);
			return null;
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
		public double m_lastActionTime;
		public ComponentCreature m_target;
		public float m_dt;
		public float m_range;
		public float m_chaseTime;
		public bool m_isPersistent;
		public bool m_autoDismount = true;
		public float m_autoChaseSuppressionTime;
		private ComponentNewChaseBehavior.AttackMode m_attackMode = ComponentNewChaseBehavior.AttackMode.Default;
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
		private FlameBulletBlock.FlameBulletType m_lastFlameBulletType = FlameBulletBlock.FlameBulletType.Flame;

		public enum AttackMode
		{
			Default,
			OnlyHand,
			Ranged
		}
	}
}
