using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior2 : ComponentBehavior, IUpdateable
	{
		// Propiedades públicas
		public ComponentCreature Target
		{
			get { return this.m_target; }
		}

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override float ImportanceLevel
		{
			get { return this.m_importanceLevel; }
		}

		// Método principal de ataque (mejorado para trabajar con manadas)
		public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			bool suppressed = this.Suppressed;
			if (!suppressed)
			{
				// Verificar si podemos atacar según las reglas de la manada
				ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herdBehavior != null)
				{
					bool canAttack = herdBehavior.CanAttackCreature(componentCreature);
					if (!canAttack)
					{
						return; // No atacar a miembros de la misma manada
					}

					// Notificar a la manada sobre el ataque (llamar ayuda si está activado)
					if (herdBehavior.AutoNearbyCreaturesHelp)
					{
						herdBehavior.CallNearbyCreaturesHelp(componentCreature, maxRange + 10f, maxChaseTime + 15f, isPersistent, true);
					}
				}
				else
				{
					// Fallback al sistema de manada original
					ComponentHerdBehavior oldHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
					if (oldHerdBehavior != null)
					{
						bool isSameHerd = !string.IsNullOrEmpty(oldHerdBehavior.HerdName) &&
										 componentCreature.Entity.FindComponent<ComponentHerdBehavior>() != null &&
										 componentCreature.Entity.FindComponent<ComponentHerdBehavior>().HerdName == oldHerdBehavior.HerdName;
						if (isSameHerd)
						{
							return; // No atacar a miembros de la misma manada
						}
					}
				}

				// Iniciar ataque
				this.m_target = componentCreature;
				this.m_nextUpdateTime = 0.0;
				this.m_range = maxRange;
				this.m_chaseTime = maxChaseTime;
				this.m_isPersistent = isPersistent;
				this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);
				this.IsActive = true;
				this.m_stateMachine.TransitionTo("Chasing");

				// Iniciar movimiento inmediato
				if (this.m_target != null && this.m_componentPathfinding != null)
				{
					this.m_componentPathfinding.Stop();
					this.UpdateChasingStateImmediately();
				}
			}
		}

		// Método para detener el ataque
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

		// Actualización principal (solo ataque cuerpo a cuerpo)
		public virtual void Update(float dt)
		{
			bool suppressed = this.Suppressed;
			if (suppressed)
			{
				this.StopAttack();
				return;
			}

			this.m_autoChaseSuppressionTime -= dt;

			// Verificar defensa del jugador (si está en nuestra manada)
			this.CheckDefendPlayer(dt);

			// Solo ataque cuerpo a cuerpo (sin lógica de armas a distancia)
			bool flag = this.IsActive && this.m_target != null;
			if (flag)
			{
				this.m_chaseTime -= dt;
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);

				// Verificar si el objetivo está en rango de ataque
				bool isInAttackRange = this.IsTargetInAttackRange(this.m_target.ComponentBody);
				if (isInAttackRange)
				{
					this.m_componentCreatureModel.AttackOrder = true;

					// Buscar la mejor herramienta de ataque cuerpo a cuerpo
					this.FindBestMeleeTool(this.m_componentMiner);
				}

				// Momento de golpe
				bool isAttackHitMoment = this.m_componentCreatureModel.IsAttackHitMoment;
				if (isAttackHitMoment)
				{
					Vector3 hitPoint;
					ComponentBody hitBody = this.GetHitBody(this.m_target.ComponentBody, out hitPoint);
					bool hitSuccess = hitBody != null;
					if (hitSuccess)
					{
						float x = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
						this.m_chaseTime = MathUtils.Max(this.m_chaseTime, x);
						this.m_componentMiner.Hit(hitBody, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
						this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					}
				}
			}

			// Actualización de la máquina de estados
			bool needsStateUpdate = this.m_subsystemTime.GameTime >= this.m_nextUpdateTime;
			if (needsStateUpdate)
			{
				this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
				this.m_nextUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_dt;
				this.m_stateMachine.Update();
			}
		}

		// Actualizar estado de persecución inmediatamente
		private void UpdateChasingStateImmediately()
		{
			if (this.m_target == null || !this.IsActive)
				return;

			Vector3 targetPosition = this.m_target.ComponentBody.Position;
			this.m_componentPathfinding.SetDestination(new Vector3?(targetPosition), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
			this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
		}

		// Buscar la mejor herramienta de ataque cuerpo a cuerpo
		private bool FindBestMeleeTool(ComponentMiner componentMiner)
		{
			int activeBlockValue = componentMiner.ActiveBlockValue;
			bool hasInventory = componentMiner.Inventory != null;
			if (!hasInventory) return false;

			// Verificar si el arma actual es buena
			bool currentWeaponGood = BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)].GetMeleePower(activeBlockValue) > 1f;
			if (currentWeaponGood) return true;

			float bestPower = 1f;
			int bestSlot = 0;

			// Buscar en los primeros 6 slots (como el original)
			for (int i = 0; i < 6; i++)
			{
				int slotValue = componentMiner.Inventory.GetSlotValue(i);
				float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
				bool isBetter = meleePower > bestPower;
				if (isBetter)
				{
					bestPower = meleePower;
					bestSlot = i;
				}
			}

			// Cambiar al mejor arma encontrada
			bool foundBetterWeapon = bestPower > 1f;
			if (foundBetterWeapon)
			{
				componentMiner.Inventory.ActiveSlotIndex = bestSlot;
				return true;
			}

			return false;
		}

		// Verificar defensa del jugador (si está en nuestra manada)
		private void CheckDefendPlayer(float dt)
		{
			try
			{
				ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
				ComponentHerdBehavior oldHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

				bool isPlayerHerd = false;
				if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
				{
					isPlayerHerd = herdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
				}
				else if (oldHerdBehavior != null && !string.IsNullOrEmpty(oldHerdBehavior.HerdName))
				{
					isPlayerHerd = oldHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
				}

				if (!isPlayerHerd) return;

				bool timeToCheck = this.m_subsystemTime.GameTime >= this.m_nextPlayerCheckTime;
				if (timeToCheck)
				{
					this.m_nextPlayerCheckTime = this.m_subsystemTime.GameTime + 0.5;

					// Buscar jugadores que necesiten ayuda
					foreach (ComponentPlayer player in this.m_subsystemPlayers.ComponentPlayers)
					{
						bool playerAlive = player.ComponentHealth.Health > 0f;
						if (playerAlive)
						{
							ComponentCreature attacker = this.FindPlayerAttacker(player);
							bool shouldHelp = attacker != null && (this.m_target == null || this.m_target != attacker);
							if (shouldHelp)
							{
								// Usar la lógica avanzada de manada si está disponible
								if (herdBehavior != null && herdBehavior.AutoNearbyCreaturesHelp)
								{
									herdBehavior.CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
								}

								// Ataque normal
								this.Attack(attacker, 20f, 30f, false);
							}
						}
					}
				}
			}
			catch { }
		}

		// Encontrar atacante del jugador
		private ComponentCreature FindPlayerAttacker(ComponentPlayer player)
		{
			try
			{
				bool validPlayer = player != null && player.ComponentBody != null;
				if (!validPlayer) return null;

				Vector3 position = player.ComponentBody.Position;
				float range = 20f;
				float rangeSquared = range * range;

				foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
				{
					bool validCreature = creature != null &&
										creature.ComponentHealth != null &&
										creature.ComponentHealth.Health > 0f &&
										creature != this.m_componentCreature &&
										creature.ComponentBody != null;

					if (validCreature)
					{
						float distanceSquared = Vector3.DistanceSquared(position, creature.ComponentBody.Position);
						bool inRange = distanceSquared < rangeSquared;

						if (inRange)
						{
							// Verificar si no es de la misma manada
							ComponentNewHerdBehavior creatureHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
							ComponentHerdBehavior creatureOldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();

							bool isPlayerAlly = false;
							if (creatureHerd != null)
							{
								isPlayerAlly = creatureHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
							}
							else if (creatureOldHerd != null)
							{
								isPlayerAlly = creatureOldHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
							}

							if (!isPlayerAlly)
							{
								// Verificar si está atacando al jugador
								ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
								ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
								ComponentNewChaseBehavior2 newChase2 = creature.Entity.FindComponent<ComponentNewChaseBehavior2>();

								bool isAttackingPlayer = false;
								if (chase != null && chase.Target == player) isAttackingPlayer = true;
								else if (newChase != null && newChase.Target == player) isAttackingPlayer = true;
								else if (newChase2 != null && newChase2.Target == player) isAttackingPlayer = true;

								if (isAttackingPlayer)
								{
									return creature;
								}
							}
						}
					}
				}
			}
			catch { }

			return null;
		}

		// Responder a comandos inmediatamente
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

		// Cargar configuración
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
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);

			this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");

			try { this.m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask"); }
			catch { this.m_autoChaseMask = (CreatureCategory)0; }

			this.m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			this.m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			this.m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");

			this.m_nextPlayerCheckTime = 0.0;

			// Configurar colisiones
			ComponentBody componentBody = this.m_componentCreature.ComponentBody;
			componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, new Action<ComponentBody>(delegate (ComponentBody body)
			{
				bool shouldChase = this.m_target == null &&
								  this.m_autoChaseSuppressionTime <= 0f &&
								  this.m_random.Float(0f, 1f) < this.m_chaseOnTouchProbability;

				if (shouldChase)
				{
					ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
					bool validCreature = creature != null;
					if (validCreature)
					{
						bool isPlayer = this.m_subsystemPlayers.IsPlayer(body.Entity);
						bool hasChaseMask = this.m_autoChaseMask > (CreatureCategory)0;

						// Verificar reglas de manada
						ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
						bool canAttack = true;

						if (herdBehavior != null)
						{
							canAttack = herdBehavior.CanAttackCreature(creature);
						}

						bool shouldAttack = canAttack &&
										  ((this.AttacksPlayer && isPlayer && this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
										   (this.AttacksNonPlayerCreature && !isPlayer && hasChaseMask));

						if (shouldAttack)
						{
							this.Attack(creature, this.ChaseRangeOnTouch, this.ChaseTimeOnTouch, false);
						}
					}
				}

				// Saltar si el objetivo está encima
				bool shouldJump = this.m_target != null &&
								 this.JumpWhenTargetStanding &&
								 body == this.m_target.ComponentBody &&
								 body.StandingOnBody == this.m_componentCreature.ComponentBody;

				if (shouldJump)
				{
					this.m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
				}
			}));

			// Configurar reacción al ser atacado
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, new Action<Injury>(delegate (Injury injury)
			{
				bool shouldChase = injury.Attacker != null &&
								  this.m_random.Float(0f, 1f) < this.m_chaseWhenAttackedProbability;

				if (shouldChase)
				{
					float maxRange = this.ChaseRangeOnAttacked ?? ((this.m_chaseWhenAttackedProbability >= 1f) ? 30f : 7f);
					float maxChaseTime = this.ChaseTimeOnAttacked ?? ((this.m_chaseWhenAttackedProbability >= 1f) ? 60f : 7f);
					bool isPersistent = this.ChasePersistentOnAttacked ?? (this.m_chaseWhenAttackedProbability >= 1f);

					// Verificar reglas de manada
					ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					bool canAttack = true;

					if (herdBehavior != null)
					{
						canAttack = herdBehavior.CanAttackCreature(injury.Attacker);
					}

					if (canAttack)
					{
						this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent);
					}
				}
			}));

			// Configurar máquina de estados
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
					bool canSearch = !this.Suppressed &&
									this.m_autoChaseSuppressionTime <= 0f &&
									(this.m_target == null || this.ScoreTarget(this.m_target) <= 0f) &&
									this.m_componentCreature.ComponentHealth.Health > this.MinHealthToAttackActively;

					if (canSearch)
					{
						this.m_range = ((this.m_subsystemSky.SkyLightIntensity < 0.2f) ? this.m_nightChaseRange : this.m_dayChaseRange);
						ComponentCreature target = this.FindTarget();

						bool foundTarget = target != null;
						if (foundTarget)
						{
							this.m_targetInRangeTime += this.m_dt;
						}
						else
						{
							this.m_targetInRangeTime = 0f;
						}

						bool shouldAttack = this.m_targetInRangeTime > this.TargetInRangeTimeToChase;
						if (shouldAttack)
						{
							bool isDay = this.m_subsystemSky.SkyLightIntensity >= 0.1f;
							float maxRange = isDay ? (this.m_dayChaseRange + 6f) : (this.m_nightChaseRange + 6f);
							float maxChaseTime = isDay ? (this.m_dayChaseTime * this.m_random.Float(0.75f, 1f)) : (this.m_nightChaseTime * this.m_random.Float(0.75f, 1f));
							this.Attack(target, maxRange, maxChaseTime, !isDay);
						}
					}
				}
			}, null);

			this.m_stateMachine.AddState("RandomMoving", delegate
			{
				this.m_componentPathfinding.SetDestination(new Vector3?(this.m_componentCreature.ComponentBody.Position + new Vector3(6f * this.m_random.Float(-1f, 1f), 0f, 6f * this.m_random.Float(-1f, 1f))), 1f, 1f, 0, false, true, false, null);
			}, delegate
			{
				bool shouldTransition = !this.m_componentPathfinding.IsStuck || this.m_componentPathfinding.Destination == null;
				if (shouldTransition)
				{
					this.m_stateMachine.TransitionTo("Chasing");
				}

				bool notActive = !this.IsActive;
				if (notActive)
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

				bool playIdleSound = this.PlayIdleSoundWhenStartToChase;
				if (playIdleSound)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
				}

				this.m_nextUpdateTime = 0.0;
			}, delegate
			{
				bool notActive = !this.IsActive;
				if (notActive)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else
				{
					bool chaseTimeOver = this.m_chaseTime <= 0f;
					if (chaseTimeOver)
					{
						this.m_autoChaseSuppressionTime = this.m_random.Float(10f, 60f);
						this.m_importanceLevel = 0f;
					}
					else
					{
						bool noTarget = this.m_target == null;
						if (noTarget)
						{
							this.m_importanceLevel = 0f;
						}
						else
						{
							bool targetDead = this.m_target.ComponentHealth.Health <= 0f;
							if (targetDead)
							{
								bool hasFeedBehavior = this.m_componentFeedBehavior != null;
								if (hasFeedBehavior)
								{
									this.m_subsystemTime.QueueGameTimeDelayedExecution(this.m_subsystemTime.GameTime + (double)this.m_random.Float(1f, 3f), delegate
									{
										bool stillHasTarget = this.m_target != null;
										if (stillHasTarget)
										{
											this.m_componentFeedBehavior.Feed(this.m_target.ComponentBody.Position);
										}
									});
								}
								this.m_importanceLevel = 0f;
							}
							else
							{
								bool notPersistentStuck = !this.m_isPersistent && this.m_componentPathfinding.IsStuck;
								if (notPersistentStuck)
								{
									this.m_importanceLevel = 0f;
								}
								else
								{
									bool persistentStuck = this.m_isPersistent && this.m_componentPathfinding.IsStuck;
									if (persistentStuck)
									{
										this.m_stateMachine.TransitionTo("RandomMoving");
									}
									else
									{
										bool targetUnsuitable = this.ScoreTarget(this.m_target) <= 0f;
										if (targetUnsuitable)
										{
											this.m_targetUnsuitableTime += this.m_dt;
										}
										else
										{
											this.m_targetUnsuitableTime = 0f;
										}

										bool targetTooUnsuitable = this.m_targetUnsuitableTime > 3f;
										if (targetTooUnsuitable)
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

											BoundingBox ourBox = this.m_componentCreature.ComponentBody.BoundingBox;
											BoundingBox targetBox = this.m_target.ComponentBody.BoundingBox;
											Vector3 ourCenter = 0.5f * (ourBox.Min + ourBox.Max);
											Vector3 targetCenter = 0.5f * (targetBox.Min + targetBox.Max);
											float distance = Vector3.Distance(ourCenter, targetCenter);
											float prediction = (distance < 4f) ? 0.2f : 0f;

											this.m_componentPathfinding.SetDestination(
												new Vector3?(targetCenter + prediction * distance * this.m_target.ComponentBody.Velocity),
												1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);

											bool playAngrySound = this.PlayAngrySoundWhenChasing && this.m_random.Float(0f, 1f) < 0.33f * this.m_dt;
											if (playAngrySound)
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

		// Encontrar objetivo
		public virtual ComponentCreature FindTarget()
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float bestScore = 0f;
			this.m_componentBodies.Clear();

			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					// Verificar reglas de manada
					ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (herdBehavior != null)
					{
						bool canAttack = herdBehavior.CanAttackCreature(creature);
						if (!canAttack)
						{
							continue; // Saltar miembros de la misma manada
						}
					}
					else
					{
						// Fallback a manada original
						ComponentHerdBehavior oldHerd = base.Entity.FindComponent<ComponentHerdBehavior>();
						if (oldHerd != null)
						{
							ComponentHerdBehavior targetHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();
							if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
								targetHerd.HerdName == oldHerd.HerdName)
							{
								continue; // Saltar miembros de la misma manada
							}
						}
					}

					float score = this.ScoreTarget(creature);
					bool isBetter = score > bestScore;
					if (isBetter)
					{
						bestScore = score;
						result = creature;
					}
				}
			}

			return result;
		}

		// Calcular puntuación del objetivo
		public virtual float ScoreTarget(ComponentCreature componentCreature)
		{
			float result = 0f;
			bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool isTargetOrGameMode = componentCreature == this.Target || this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
			bool hasChaseMask = this.m_autoChaseMask > (CreatureCategory)0;

			bool shouldChaseNonPlayer = componentCreature == this.Target ||
									   (hasChaseMask &&
										MathUtils.Remainder(0.004999999888241291 * this.m_subsystemTime.GameTime +
														   (double)((float)(this.GetHashCode() % 1000) / 1000f) +
														   (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) <
										(double)this.m_chaseNonPlayerProbability);

			// Verificar reglas de manada
			ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
			bool canAttack = true;

			if (herdBehavior != null)
			{
				canAttack = herdBehavior.CanAttackCreature(componentCreature);
			}
			else
			{
				ComponentHerdBehavior oldHerd = base.Entity.FindComponent<ComponentHerdBehavior>();
				if (oldHerd != null)
				{
					ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
					if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) &&
						targetHerd.HerdName == oldHerd.HerdName)
					{
						canAttack = false;
					}
				}
			}

			bool isValidTarget = componentCreature != this.m_componentCreature &&
								canAttack &&
								((!isPlayer && shouldChaseNonPlayer) || (isPlayer && isTargetOrGameMode)) &&
								componentCreature.Entity.IsAddedToProject &&
								componentCreature.ComponentHealth.Health > 0f;

			if (isValidTarget)
			{
				float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				bool inRange = distance < this.m_range;
				if (inRange)
				{
					result = this.m_range - distance;
				}
			}

			return result;
		}

		// Métodos de rango de ataque (cuerpo a cuerpo)
		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f ||
				  (target.ParentBody != null && this.IsTargetInWater(target.ParentBody)) ||
				  (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			bool directHit = this.IsBodyInAttackRange(target);
			if (directHit)
			{
				return true;
			}

			BoundingBox ourBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox targetBox = target.BoundingBox;
			Vector3 ourCenter = 0.5f * (ourBox.Min + ourBox.Max);
			Vector3 targetCenter = 0.5f * (targetBox.Min + targetBox.Max) - ourCenter;
			float distance = targetCenter.Length();
			Vector3 direction = targetCenter / distance;

			float width = 0.5f * (ourBox.Max.X - ourBox.Min.X + targetBox.Max.X - targetBox.Min.X);
			float height = 0.5f * (ourBox.Max.Y - ourBox.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

			if (MathUtils.Abs(targetCenter.Y) < height * 0.99f)
			{
				if (distance < width + 0.99f && Vector3.Dot(direction, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else
			{
				if (distance < height + 0.3f && MathUtils.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.8f)
				{
					return true;
				}
			}

			return (target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) ||
				  (this.AllowAttackingStandingOnBody && target.StandingOnBody != null &&
				   target.StandingOnBody.Position.Y < target.Position.Y && this.IsTargetInAttackRange(target.StandingOnBody));
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox ourBox = this.m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox targetBox = target.BoundingBox;
			Vector3 ourCenter = 0.5f * (ourBox.Min + ourBox.Max);
			Vector3 targetCenter = 0.5f * (targetBox.Min + targetBox.Max) - ourCenter;
			float distance = targetCenter.Length();
			Vector3 direction = targetCenter / distance;

			float width = 0.5f * (ourBox.Max.X - ourBox.Min.X + targetBox.Max.X - targetBox.Min.X);
			float height = 0.5f * (ourBox.Max.Y - ourBox.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

			if (MathUtils.Abs(targetCenter.Y) < height * 0.99f)
			{
				if (distance < width + 0.99f && Vector3.Dot(direction, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
				{
					return true;
				}
			}
			else
			{
				if (distance < height + 0.3f && MathUtils.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.8f)
				{
					return true;
				}
			}

			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 ourCenter = this.m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 targetCenter = target.BoundingBox.Center();
			Ray3 ray = new Ray3(ourCenter, Vector3.Normalize(targetCenter - ourCenter));

			BodyRaycastResult? hitResult = this.m_componentMiner.Raycast<BodyRaycastResult>(
				ray, RaycastMode.Interaction, true, true, true, null);

			bool validHit = hitResult != null &&
						   hitResult.Value.Distance < this.MaxAttackRange &&
						   (hitResult.Value.ComponentBody == target ||
							hitResult.Value.ComponentBody.IsChildOfBody(target) ||
							target.IsChildOfBody(hitResult.Value.ComponentBody) ||
							(target.StandingOnBody == hitResult.Value.ComponentBody && this.AllowAttackingStandingOnBody));

			if (validHit)
			{
				hitPoint = hitResult.Value.HitPoint();
				return hitResult.Value.ComponentBody;
			}

			hitPoint = default(Vector3);
			return null;
		}

		// Campos
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
		public ComponentCreature m_target;
		public float m_dt;
		public float m_range;
		public float m_chaseTime;
		public bool m_isPersistent;
		public float m_autoChaseSuppressionTime;

		// Configuración
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
