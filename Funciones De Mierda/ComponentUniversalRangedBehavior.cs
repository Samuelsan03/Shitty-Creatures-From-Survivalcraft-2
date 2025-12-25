using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentUniversalRangedBehavior : ComponentBehavior, IUpdateable
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
			bool suppressed = this.Suppressed;
			if (!suppressed)
			{
				ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
				ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

				bool canAttack = true;

				if (componentNewHerdBehavior != null)
				{
					canAttack = componentNewHerdBehavior.CanAttackCreature(componentCreature);
				}
				else if (componentHerdBehavior != null)
				{
					ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
					if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
						targetHerd.HerdName == componentHerdBehavior.HerdName)
					{
						canAttack = false;
					}
				}

				if (!canAttack)
				{
					return;
				}

				this.m_target = componentCreature;
				this.m_nextUpdateTime = 0.0;
				this.m_range = maxRange;
				this.m_chaseTime = maxChaseTime;
				this.m_isPersistent = isPersistent;
				this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);
			}
		}

		public virtual void StopAttack()
		{
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
			this.CheckDefendPlayer(dt); // AÑADIDO: Para defensa del jugador

			// VERIFICACIÓN MEJORADA: Permitir que todos los NPCs con armas de distancia disparen
			// Esto incluye a los que tienen ComponentHerdBehavior original
			bool hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			bool isOriginalHerd = base.Entity.FindComponent<ComponentHerdBehavior>() != null &&
								  base.Entity.FindComponent<ComponentNewHerdBehavior>() == null;

			// Si tiene arma de distancia (ya sea con new herd o original), usar lógica de disparo
			if (hasRangedWeapon || isOriginalHerd)
			{
				this.UpdateRangedWeaponLogic(dt);
			}

			bool flag = this.IsActive && this.m_target != null;
			if (flag)
			{
				this.m_chaseTime -= dt;
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
				float num = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;

				// LÓGICA DE ATAQUE COPIADA DE ComponentNewChaseBehavior
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

				bool flag11 = this.IsTargetInAttackRange(this.m_target.ComponentBody);
				if (flag11)
				{
					this.m_componentCreatureModel.AttackOrder = true;
					bool flag12 = !this.HasActiveRangedWeaponComponent();
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
				this.UpdateState();
			}
		}

		// NUEVO MÉTODO: Lógica específica para armas de distancia (MEJORADA)
		private void UpdateRangedWeaponLogic(float dt)
		{
			if (this.m_target == null || !this.IsActive)
				return;

			// Verificar si tiene arma de distancia equipada o puede encontrar una
			bool hasRangedWeapon = this.HasActiveRangedWeaponComponent();

			if (!hasRangedWeapon)
			{
				// Intentar encontrar un arma de distancia en el inventario
				this.FindAimTool(this.m_componentMiner);
				hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			}

			if (hasRangedWeapon && this.m_target != null)
			{
				// Calcular distancia al objetivo
				float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);

				// Si está dentro del rango de ataque a distancia
				if (distance >= this.m_attackRange.X && distance <= this.m_attackRange.Y)
				{
					// Apuntar al objetivo
					Vector3 direction = Vector3.Normalize(this.m_target.ComponentCreatureModel.EyePosition - this.m_componentCreature.ComponentCreatureModel.EyePosition);

					// Verificar si hay línea de visión
					float rayDistance;
					ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out rayDistance);

					if (hitBody != null && Math.Abs(rayDistance - distance) < 2f)
					{
						// Disparar si está listo - USANDO LA MISMA LÓGICA QUE ComponentNewChaseBehavior
						float actionDelay = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;

						bool isFacingTarget = (double)Vector3.Dot(this.m_componentCreature.ComponentBody.Matrix.Forward, direction) > 0.8;

						if (this.m_subsystemTime.GameTime - this.m_lastActionTime > actionDelay)
						{
							// Intentar usar el arma (disparar)
							bool used = this.m_componentMiner.Use(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, direction));
							if (used)
							{
								this.m_lastActionTime = this.m_subsystemTime.GameTime;
								// Reproducir sonido de ataque si es necesario
								this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
							}
							else if (isFacingTarget)
							{
								// Si no se pudo usar (por cooldown u otra razón), apuntar
								this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, direction), AimState.Completed);
								this.m_lastActionTime = this.m_subsystemTime.GameTime;
							}
						}
						else if (isFacingTarget)
						{
							// Durante el tiempo de espera, mantener apuntando
							this.m_componentMiner.Aim(new Ray3(this.m_componentCreature.ComponentCreatureModel.EyePosition, direction), AimState.InProgress);
						}

						// Extender tiempo de persecución cuando se usa arma de distancia
						this.m_chaseTime = Math.Max(this.m_chaseTime, this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f);

						// Detener movimiento cuando está apuntando
						this.m_componentPathfinding.Destination = null;
					}
					else
					{
						// Moverse para tener línea de visión
						this.m_componentPathfinding.SetDestination(
							new Vector3?(this.m_target.ComponentBody.Position),
							1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
					}
				}
				else if (distance > this.m_attackRange.Y)
				{
					// Acercarse si está demasiado lejos
					this.m_componentPathfinding.SetDestination(
						new Vector3?(this.m_target.ComponentBody.Position),
						1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
				}
				else if (distance < this.m_attackRange.X)
				{
					// Alejarse si está demasiado cerca para arma de distancia
					Vector3 retreatDirection = Vector3.Normalize(this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position);
					Vector3 retreatPosition = this.m_componentCreature.ComponentBody.Position + retreatDirection * 3f;
					this.m_componentPathfinding.SetDestination(new Vector3?(retreatPosition), 1f, 1f, 0, false, true, false, null);
				}
			}
		}

		// AÑADIDO: Método para defender al jugador (similar a ComponentNewChaseBehavior)
		private void CheckDefendPlayer(float dt)
		{
			try
			{
				// Verificar ambos tipos de herd behavior para defensa de jugador
				ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
				ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

				bool isPlayerHerd = false;

				if (componentNewHerdBehavior != null && !string.IsNullOrEmpty(componentNewHerdBehavior.HerdName))
				{
					isPlayerHerd = componentNewHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
				}
				else if (componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
				{
					isPlayerHerd = componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
				}

				if (!isPlayerHerd)
				{
					return;
				}

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
			catch
			{
			}
		}

		// AÑADIDO: Método para encontrar atacantes del jugador
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
							// Verificar si NO es del rebaño "player"
							ComponentNewHerdBehavior componentNewHerdBehavior = componentCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
							ComponentHerdBehavior componentHerdBehavior = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();

							bool isPlayerHerd = false;

							if (componentNewHerdBehavior != null)
							{
								isPlayerHerd = componentNewHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
							}
							else if (componentHerdBehavior != null)
							{
								isPlayerHerd = componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
							}

							if (!isPlayerHerd)
							{
								// Verificar si está atacando al jugador
								ComponentChaseBehavior componentChaseBehavior = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
								ComponentNewChaseBehavior componentNewChaseBehavior = componentCreature.Entity.FindComponent<ComponentNewChaseBehavior>();

								bool isAttackingPlayer = false;

								if (componentChaseBehavior != null && componentChaseBehavior.Target == player)
								{
									isAttackingPlayer = true;
								}
								else if (componentNewChaseBehavior != null && componentNewChaseBehavior.Target == player)
								{
									isAttackingPlayer = true;
								}

								if (isAttackingPlayer)
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

		private void TryGetTargetFromOtherBehaviors()
		{
			ComponentNewChaseBehavior newChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (newChaseBehavior != null && newChaseBehavior.IsActive && newChaseBehavior.Target != null)
			{
				this.m_target = newChaseBehavior.Target;
				return;
			}

			ComponentChaseBehavior chaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			if (chaseBehavior != null && chaseBehavior.IsActive && chaseBehavior.Target != null)
			{
				this.m_target = chaseBehavior.Target;
				return;
			}
		}

		private void UpdateMeleeAttackLogic(float dt)
		{
			if (this.m_target == null || !this.IsActive)
				return;

			bool flag = this.IsTargetInAttackRange(this.m_target.ComponentBody);
			if (flag)
			{
				this.m_componentCreatureModel.AttackOrder = true;
				this.FindHitTool(this.m_componentMiner);
			}

			bool isAttackHitMoment = this.m_componentCreatureModel.IsAttackHitMoment;
			if (isAttackHitMoment)
			{
				Vector3 hitPoint;
				ComponentBody hitBody = this.GetHitBody(this.m_target.ComponentBody, out hitPoint);
				bool flag13 = hitBody != null;
				if (flag13)
				{
					float x = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
					this.m_chaseTime = MathUtils.Max(this.m_chaseTime, x);
					this.m_componentMiner.Hit(hitBody, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
					this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
				}
			}

			float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
											this.m_target.ComponentBody.Position);
			if (distance > this.MaxAttackRange * 1.5f)
			{
				this.m_componentPathfinding.SetDestination(
					new Vector3?(this.m_target.ComponentBody.Position),
					1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
			}
		}

		private void UpdateState()
		{
			if (!this.IsActive || this.m_target == null || this.m_chaseTime <= 0f)
			{
				this.StopAttack();
				return;
			}

			if (this.m_target.ComponentHealth.Health <= 0f)
			{
				this.m_importanceLevel = 0f;
				return;
			}

			bool hasRangedWeapon = this.HasActiveRangedWeaponComponent();
			if (hasRangedWeapon)
			{
				this.UpdateRangedMovement();
			}
			else
			{
				this.UpdateMeleeMovement();
			}
		}

		private void UpdateRangedMovement()
		{
			int maxPathfindingPositions = this.m_isPersistent ?
				((this.m_subsystemTime.FixedTimeStep != null) ? 2000 : 500) : 0;

			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = this.m_target.ComponentBody.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max);
			float distance = Vector3.Distance(v, vector);

			float optimalDistance = (this.m_attackRange.X + this.m_attackRange.Y) * 0.5f;

			if (distance > optimalDistance + 2f)
			{
				float num2 = (distance < 4f) ? 0.2f : 0f;
				this.m_componentPathfinding.SetDestination(
					new Vector3?(vector + num2 * distance * this.m_target.ComponentBody.Velocity),
					1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
			}
			else if (distance < optimalDistance - 2f)
			{
				Vector3 retreatDir = Vector3.Normalize(this.m_componentCreature.ComponentBody.Position -
													this.m_target.ComponentBody.Position);
				Vector3 retreatPos = this.m_componentCreature.ComponentBody.Position + retreatDir * 3f;
				this.m_componentPathfinding.SetDestination(
					new Vector3?(retreatPos), 1f, 1f, 0, false, true, false, null);
			}
			else
			{
				this.m_componentPathfinding.Destination = null;
			}
		}

		private void UpdateMeleeMovement()
		{
			int maxPathfindingPositions = this.m_isPersistent ?
				((this.m_subsystemTime.FixedTimeStep != null) ? 2000 : 500) : 0;

			BoundingBox boundingBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox boundingBox2 = this.m_target.ComponentBody.BoundingBox;
			Vector3 v = 0.5f * (boundingBox.Min + boundingBox.Max);
			Vector3 vector = 0.5f * (boundingBox2.Min + boundingBox2.Max);
			float distance = Vector3.Distance(v, vector);

			float optimalMeleeDistance = this.MaxAttackRange * 1.2f;

			if (distance > optimalMeleeDistance)
			{
				float num2 = (distance < 4f) ? 0.2f : 0f;
				this.m_componentPathfinding.SetDestination(
					new Vector3?(vector + num2 * distance * this.m_target.ComponentBody.Velocity),
					1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
			}
			else
			{
				this.m_componentPathfinding.Destination = null;
			}
		}

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
				BodyRaycastResult? bodyRaycastResult = this.m_subsystemBodies.Raycast(
					eyePosition, eyePosition + v * this.m_attackRange.Y, 0.35f,
					(ComponentBody body, float dist) => body.Entity != base.Entity &&
					!body.IsChildOfBody(this.m_componentCreature.ComponentBody) &&
					!this.m_componentCreature.ComponentBody.IsChildOfBody(body));

				TerrainRaycastResult? terrainRaycastResult = this.m_componentMiner?.m_subsystemTerrain?.Raycast(
					eyePosition, eyePosition + v * this.m_attackRange.Y, true, true,
					(int value, float dist) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable);

				distance = ((bodyRaycastResult != null) ? bodyRaycastResult.GetValueOrDefault().Distance : float.PositiveInfinity);

				bool flag2 = this.m_componentMiner.Inventory != null && bodyRaycastResult != null;
				if (flag2)
				{
					bool flag3 = terrainRaycastResult != null &&
								(double)terrainRaycastResult.Value.Distance < (double)bodyRaycastResult.Value.Distance;
					if (flag3)
					{
						return null;
					}

					bool flag4 = bodyRaycastResult.Value.ComponentBody == target ||
								bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) ||
								target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) ||
								target.StandingOnBody == bodyRaycastResult.Value.ComponentBody;
					if (flag4)
					{
						return bodyRaycastResult.Value.ComponentBody;
					}
				}
				result = null;
			}
			return result;
		}

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
					bool flag6 = block2.IsAimable_(slotValue) &&
								(!(block2 is FlameThrowerBlock) || this.IsReady(slotValue));
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

		public bool IsReady(int slotValue)
		{
			int data = Terrain.ExtractData(slotValue);
			return !(BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock) ||
				   (FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded &&
					FlameThrowerBlock.GetBulletType(data) != null);
		}

		public bool IsAimToolNeedToReady(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

			if (block is ItemsLauncherBlock)
			{
				return false;
			}

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
						bool flag5 = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded &&
									MusketBlock.GetBulletType(data) != null;
						if (flag5)
						{
							return false;
						}
					}
					else
					{
						bool flag6 = RepeatCrossbowBlock.GetDraw(data) >= 15 &&
									RepeatCrossbowBlock.GetArrowType(data) != null;
						if (flag6)
						{
							return false;
						}
					}
				}
				else
				{
					bool flag7 = CrossbowBlock.GetDraw(data) >= 15 &&
								CrossbowBlock.GetArrowType(data) != null;
					if (flag7)
					{
						return false;
					}
				}
			}
			else
			{
				bool flag8 = BowBlock.GetDraw(data) >= 15 &&
							BowBlock.GetArrowType(data) != null;
				if (flag8)
				{
					return false;
				}
			}
			return true;
		}

		public void HandleComplexAimTool(ComponentMiner componentMiner, int slotIndex)
		{
			int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int num2 = Terrain.ExtractContents(slotValue);
			Block block = BlocksManager.Blocks[num2];

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
								data = MusketBlock.SetLoadState(
									MusketBlock.SetBulletType(0, new BulletBlock.BulletType?(BulletBlock.BulletType.MusketBall)),
									MusketBlock.LoadState.Loaded);
							}
						}
						else
						{
							RepeatArrowBlock.ArrowType value;
							float randomValue = this.m_random.Float(0f, 1f);

							if (randomValue < 0.166666f)
							{
								value = RepeatArrowBlock.ArrowType.ExplosiveArrow;
							}
							else if (randomValue < 0.333333f)
							{
								value = RepeatArrowBlock.ArrowType.PoisonArrow;
							}
							else if (randomValue < 0.5f)
							{
								value = RepeatArrowBlock.ArrowType.CopperArrow;
							}
							else if (randomValue < 0.666666f)
							{
								value = RepeatArrowBlock.ArrowType.DiamondArrow;
							}
							else if (randomValue < 0.833333f)
							{
								value = RepeatArrowBlock.ArrowType.SeriousPoisonArrow;
							}
							else
							{
								value = RepeatArrowBlock.ArrowType.IronArrow;
							}

							data = RepeatCrossbowBlock.SetArrowType(data, new RepeatArrowBlock.ArrowType?(value));
							data = RepeatCrossbowBlock.SetDraw(data, 15);
						}
					}
					else
					{
						ArrowBlock.ArrowType value;
						float randomValue = this.m_random.Float(0f, 1f);

						if (randomValue < 0.333333f)
						{
							value = ArrowBlock.ArrowType.IronBolt;
						}
						else if (randomValue < 0.666666f)
						{
							value = ArrowBlock.ArrowType.DiamondBolt;
						}
						else
						{
							value = ArrowBlock.ArrowType.ExplosiveBolt;
						}

						data = CrossbowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(value));
						data = CrossbowBlock.SetDraw(data, 15);
					}
				}
				else
				{
					ArrowBlock.ArrowType value;
					float randomValue = this.m_random.Float(0f, 1f);

					if (randomValue < 0.166666f)
					{
						value = ArrowBlock.ArrowType.WoodenArrow;
					}
					else if (randomValue < 0.333333f)
					{
						value = ArrowBlock.ArrowType.StoneArrow;
					}
					else if (randomValue < 0.5f)
					{
						value = ArrowBlock.ArrowType.IronArrow;
					}
					else if (randomValue < 0.666666f)
					{
						value = ArrowBlock.ArrowType.DiamondArrow;
					}
					else if (randomValue < 0.833333f)
					{
						value = ArrowBlock.ArrowType.FireArrow;
					}
					else
					{
						value = ArrowBlock.ArrowType.CopperArrow;
					}

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
				result = ((target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) ||
						 (this.AllowAttackingStandingOnBody && target.StandingOnBody != null &&
						  target.StandingOnBody.Position.Y < target.Position.Y &&
						  this.IsTargetInAttackRange(target.StandingOnBody)));
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
			bool flag = bodyRaycastResult != null && bodyRaycastResult.Value.Distance < this.MaxAttackRange &&
					   (bodyRaycastResult.Value.ComponentBody == target ||
						bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) ||
						target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) ||
						(target.StandingOnBody == bodyRaycastResult.Value.ComponentBody && this.AllowAttackingStandingOnBody));
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

			this.m_attackRange = valuesDictionary.GetValue<Vector2>("AttackRange", new Vector2(2f, 15f));
			this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");

			try
			{
				string autoChaseMaskString = valuesDictionary.GetValue<string>("AutoChaseMask", "");
				if (Enum.TryParse<CreatureCategory>(autoChaseMaskString, out CreatureCategory mask))
				{
					this.m_autoChaseMask = mask;
				}
				else
				{
					this.m_autoChaseMask = (CreatureCategory)0;
				}
			}
			catch
			{
				this.m_autoChaseMask = (CreatureCategory)0;
			}

			this.m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			this.m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			this.m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");
			this.AttacksPlayer = valuesDictionary.GetValue<bool>("AttacksPlayer", true);
			this.m_autoDismount = valuesDictionary.GetValue<bool>("AutoDismount", true);
			this.AttacksNonPlayerCreature = valuesDictionary.GetValue<bool>("AttacksNonPlayerCreature", true);

			this.m_nextPlayerCheckTime = 0.0;
			this.m_lastActionTime = 0.0;

			// AÑADIDO: Suscribirse a eventos de colisión y daño como en ComponentNewChaseBehavior
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
					bool flag2 = true;
					bool flag3 = componentNewHerdBehavior != null;
					if (flag3)
					{
						flag2 = componentNewHerdBehavior.CanAttackCreature(injury.Attacker);
					}
					if (flag2)
					{
						this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent);
					}
				}
			}));
		}

		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemSky m_subsystemSky;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemTime m_subsystemTime;
		public SubsystemNoise m_subsystemNoise;
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
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
	}
}