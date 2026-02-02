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
				}
				else
				{
					// AGREGADO: Lógica mejorada para manadas originales con guardianes
					ComponentHerdBehavior oldHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
					if (oldHerdBehavior != null)
					{
						ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
						if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) && !string.IsNullOrEmpty(oldHerdBehavior.HerdName))
						{
							bool isSameHerd = targetHerd.HerdName == oldHerdBehavior.HerdName;

							// Verificar relación player-guardian
							bool isPlayerAlly = false;

							if (oldHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
							{
								if (targetHerd.HerdName.ToLower().Contains("guardian"))
								{
									isPlayerAlly = true;
								}
							}
							else if (targetHerd.HerdName.ToLower().Contains("guardian"))
							{
								if (targetHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
								{
									isPlayerAlly = true;
								}
							}

							if (isSameHerd || isPlayerAlly)
							{
								//FIX: NO ATACAR A MIEMBROS DE LA MISMA MANADA
								return; // No atacar a aliados
							}
						}
						else
						{
							//FIX: SI EL OBJETIVO NO TIENE MANADA, NO ATACAR SI ES LA MISMA ENTIDAD
							if (base.Entity == componentCreature.Entity)
								return;
						}
					}
				}

				// Durante noche verde, ignorar algunas restricciones para zombies
				if (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
				{
					// Si el objetivo es un zombie/infectado, priorizar ataque
					if (IsZombieOrInfected(componentCreature))
					{
						// Extender tiempo de persecución durante noche verde
						maxChaseTime *= 1.5f;
						isPersistent = true;
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

			// Lógica adicional para noche verde: alta guardia permanente
			if (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
			{
				// Asegurarse de que no estamos suprimidos durante noche verde
				if (this.Suppressed)
				{
					this.Suppressed = false;
				}

				// Aumentar importancia durante noche verde
				if (this.IsActive && this.m_importanceLevel < 250f)
				{
					this.m_importanceLevel = 250f;
				}

				// AGREGADO: Durante noche verde, chequear amenazas más frecuentemente (GUARDIA EXTREMA)
				this.CheckHighAlertPlayerThreats(dt);
			}
			else
			{
				// Fuera de la noche verde, comportamiento normal (solo reaccionar a ataques)
				this.CheckPlayerThreats(dt);
			}

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

		// AGREGADO: Sistema de alta guardia durante noche verde (GUARDIA EXTREMA)
		private void CheckHighAlertPlayerThreats(float dt)
		{
			try
			{
				// Verificar si somos aliados del jugador
				if (!IsPlayerAlly())
					return;

				// Chequear cada 0.1 segundos durante noche verde (muy rápido)
				bool timeToCheck = this.m_subsystemTime.GameTime >= this.m_nextHighAlertCheckTime;
				if (timeToCheck)
				{
					this.m_nextHighAlertCheckTime = this.m_subsystemTime.GameTime + 0.1;

					// Rango extendido durante noche verde (GUARDIA EXTREMA)
					float highAlertRange = 40f;

					foreach (ComponentPlayer player in this.m_subsystemPlayers.ComponentPlayers)
					{
						if (player.ComponentHealth.Health > 0f)
						{
							// Buscar zombies/infectados que se acerquen al jugador
							ComponentCreature approachingZombie = FindApproachingZombie(player, highAlertRange);

							if (approachingZombie != null && (this.m_target == null || this.m_target != approachingZombie))
							{
								// Atacar inmediatamente sin esperar a que ataque (GUARDIA EXTREMA)
								this.Attack(approachingZombie, highAlertRange, 60f, true);

								// Llamar a otros aliados para ayuda
								ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
								if (herdBehavior != null && herdBehavior.AutoNearbyCreaturesHelp)
								{
									herdBehavior.CallNearbyCreaturesHelp(approachingZombie, highAlertRange, 60f, false, true);
								}

								return; // Atacar solo a una amenaza a la vez
							}
						}
					}
				}
			}
			catch { }
		}

		// AGREGADO: Encontrar zombies que se acerquen al jugador
		private ComponentCreature FindApproachingZombie(ComponentPlayer player, float range)
		{
			try
			{
				if (player.ComponentBody == null)
					return null;

				Vector3 playerPosition = player.ComponentBody.Position;
				float rangeSquared = range * range;

				ComponentCreature mostThreateningZombie = null;
				float highestThreatScore = 0f;

				foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
				{
					if (creature != null &&
						creature.ComponentHealth != null &&
						creature.ComponentHealth.Health > 0f &&
						creature != this.m_componentCreature &&
						creature.ComponentBody != null)
					{
						// Verificar si es zombie/infectado
						if (!IsZombieOrInfected(creature))
							continue;

						// Verificar distancia
						float distanceSquared = Vector3.DistanceSquared(playerPosition, creature.ComponentBody.Position);
						if (distanceSquared > rangeSquared)
							continue;

						// Calcular puntuación de amenaza
						float threatScore = CalculateZombieThreatScore(creature, player, distanceSquared);

						if (threatScore > highestThreatScore)
						{
							highestThreatScore = threatScore;
							mostThreateningZombie = creature;
						}
					}
				}

				return mostThreateningZombie;
			}
			catch { }

			return null;
		}

		// AGREGADO: Calcular puntuación de amenaza de zombie
		private float CalculateZombieThreatScore(ComponentCreature zombie, ComponentPlayer player, float distanceSquared)
		{
			float threatScore = 0f;

			// Base: inversamente proporcional a la distancia
			float distance = (float)Math.Sqrt(distanceSquared);
			threatScore = 100f / (distance + 1f);

			// Bonus si está mirando hacia el jugador
			if (IsFacingPlayer(zombie, player))
			{
				threatScore += 50f;
			}

			// Bonus si se está moviendo hacia el jugador
			if (IsMovingTowardPlayer(zombie, player))
			{
				threatScore += 70f;
			}

			// Bonus extra si está muy cerca
			if (distance < 10f)
			{
				threatScore += 100f;
			}

			// Verificar si ya está atacando a alguien (no necesariamente al jugador)
			ComponentChaseBehavior chase = zombie.Entity.FindComponent<ComponentChaseBehavior>();
			ComponentNewChaseBehavior newChase = zombie.Entity.FindComponent<ComponentNewChaseBehavior>();
			ComponentNewChaseBehavior2 newChase2 = zombie.Entity.FindComponent<ComponentNewChaseBehavior2>();
			ComponentZombieChaseBehavior zombieChase = zombie.Entity.FindComponent<ComponentZombieChaseBehavior>();

			if ((chase != null && chase.Target != null) ||
				(newChase != null && newChase.Target != null) ||
				(newChase2 != null && newChase2.Target != null) ||
				(zombieChase != null && zombieChase.Target != null))
			{
				threatScore += 80f; // Ya es agresivo, más peligroso
			}

			return threatScore;
		}

		// AGREGADO: Verificar si es aliado del jugador
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

		// AGREGADO: Verificar si está mirando hacia el jugador
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
			return dot > 0.7f; // Mirando hacia el jugador (45 grados o menos)
		}

		// Método para verificar si es zombie/infectado
		private bool IsZombieOrInfected(ComponentCreature creature)
		{
			// Verificar por componentes de zombies
			ComponentZombieHerdBehavior zombieHerd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
			ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();

			// También verificar por comportamiento agresivo hacia jugadores
			if (zombieHerd == null && zombieChase == null)
			{
				// Verificar si tiene comportamiento de persecución que ataque jugadores
				ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
				if (chase != null && chase.AttacksPlayer)
				{
					// Verificar si es hostil por naturaleza
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

		// Método para verificar si se está moviendo hacia el jugador
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
				return false; // No se está moviendo

			creatureVelocity = Vector3.Normalize(creatureVelocity);
			float dot = Vector3.Dot(creatureVelocity, toPlayer);
			return dot > 0.5f; // Moviéndose hacia el jugador
		}

		// MODIFICADO: Sistema NORMAL de verificación de amenazas (solo fuera de noche verde)
		private void CheckPlayerThreats(float dt)
		{
			try
			{
				// Solo si somos aliados del jugador
				if (!IsPlayerAlly())
					return;

				bool timeToCheck = this.m_subsystemTime.GameTime >= this.m_nextPlayerCheckTime;
				if (timeToCheck)
				{
					this.m_nextPlayerCheckTime = this.m_subsystemTime.GameTime + 1.0; // Reducido de 0.25 a 1.0 (menos frecuente)

					foreach (ComponentPlayer player in this.m_subsystemPlayers.ComponentPlayers)
					{
						if (player.ComponentHealth.Health > 0f)
						{
							// Buscar amenazas cercanas al jugador que ya lo estén atacando
							ComponentCreature threat = FindImmediateThreat(player, 15f); // Reducido de 25f a 15f

							if (threat != null && (this.m_target == null || this.m_target != threat))
							{
								// Solo atacar si la amenaza ya está atacando al jugador
								if (IsAttackingPlayer(threat, player))
								{
									// Atacar amenaza
									this.Attack(threat, 15f, 20f, false); // Reducido de 30f a 20f

									// Pedir ayuda si es posible
									ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
									if (herdBehavior != null && herdBehavior.AutoNearbyCreaturesHelp)
									{
										herdBehavior.CallNearbyCreaturesHelp(threat, 15f, 20f, false, true);
									}
								}
							}
						}
					}
				}
			}
			catch { }
		}

		// AGREGADO: Encontrar amenazas inmediatas (versión normal)
		private ComponentCreature FindImmediateThreat(ComponentPlayer player, float range)
		{
			try
			{
				if (player.ComponentBody == null)
					return null;

				Vector3 playerPosition = player.ComponentBody.Position;
				float rangeSquared = range * range;

				ComponentCreature mostDangerousThreat = null;
				float highestDanger = 0f;

				foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
				{
					if (creature != null &&
						creature.ComponentHealth != null &&
						creature.ComponentHealth.Health > 0f &&
						creature != this.m_componentCreature &&
						creature.ComponentBody != null)
					{
						float distanceSquared = Vector3.DistanceSquared(playerPosition, creature.ComponentBody.Position);
						if (distanceSquared > rangeSquared)
							continue;

						// Verificar si es una amenaza
						if (!IsCreatureThreat(creature, player))
							continue;

						// Calcular nivel de peligro
						float dangerLevel = CalculateDangerLevel(creature, player, distanceSquared);

						if (dangerLevel > highestDanger)
						{
							highestDanger = dangerLevel;
							mostDangerousThreat = creature;
						}
					}
				}

				return mostDangerousThreat;
			}
			catch { }

			return null;
		}

		// AGREGADO: Calcular nivel de peligro
		private float CalculateDangerLevel(ComponentCreature creature, ComponentPlayer player, float distanceSquared)
		{
			float danger = 0f;
			float distance = (float)Math.Sqrt(distanceSquared);

			// Base por distancia
			danger = 100f / (distance + 1f);

			// Bonus si es zombie
			if (IsZombieOrInfected(creature))
			{
				danger *= 1.5f; // Reducido de 2.0f a 1.5f
			}

			// Bonus si está atacando al jugador
			if (IsAttackingPlayer(creature, player))
			{
				danger += 200f; // Aumentado de 100f a 200f (prioridad alta si está atacando)
			}

			// Bonus si está cerca
			if (distance < 5f)
			{
				danger += 50f;
			}

			return danger;
		}

		// AGREGADO: Verificar si está atacando al jugador
		private bool IsAttackingPlayer(ComponentCreature creature, ComponentPlayer player)
		{
			ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
			ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
			ComponentNewChaseBehavior2 newChase2 = creature.Entity.FindComponent<ComponentNewChaseBehavior2>();
			ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();

			return (chase != null && chase.Target == player) ||
				   (newChase != null && newChase.Target == player) ||
				   (newChase2 != null && newChase2.Target == player) ||
				   (zombieChase != null && zombieChase.Target == player);
		}

		// Método auxiliar para verificar si una criatura es una amenaza
		private bool IsCreatureThreat(ComponentCreature creature, ComponentPlayer player)
		{
			// Verificar si es aliado del jugador
			ComponentNewHerdBehavior creatureHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			ComponentHerdBehavior creatureOldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();

			bool isPlayerAlly = false;

			if (creatureHerd != null && !string.IsNullOrEmpty(creatureHerd.HerdName))
			{
				string herdName = creatureHerd.HerdName;
				isPlayerAlly = herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
							  herdName.ToLower().Contains("guardian");
			}
			else if (creatureOldHerd != null && !string.IsNullOrEmpty(creatureOldHerd.HerdName))
			{
				string herdName = creatureOldHerd.HerdName;
				isPlayerAlly = herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
							  herdName.ToLower().Contains("guardian");
			}

			// No es amenaza si es aliado
			if (isPlayerAlly)
				return false;

			// Es amenaza si es zombie/infectado
			if (IsZombieOrInfected(creature))
				return true;

			// Es amenaza si está atacando al jugador
			if (IsAttackingPlayer(creature, player))
				return true;

			return false;
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

		// Actualizar estado de persecución inmediatamente
		private void UpdateChasingStateImmediately()
		{
			if (this.m_target == null || !this.IsActive)
				return;

			Vector3 targetPosition = this.m_target.ComponentBody.Position;
			this.m_componentPathfinding.SetDestination(new Vector3?(targetPosition), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
			this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
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

			// AGREGADO: Subsistema de noche verde
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(false);

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
			this.m_nextHighAlertCheckTime = 0.0; // AGREGADO: Tiempo para chequeo de alta alerta

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
					if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) && !string.IsNullOrEmpty(oldHerd.HerdName))
					{
						// AGREGADO: Lógica de guardianes para ComponentHerdBehavior original
						bool isSameHerd = targetHerd.HerdName == oldHerd.HerdName;

						// Verificar relación player-guardian
						bool isPlayerAlly = false;

						if (oldHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
						{
							if (targetHerd.HerdName.ToLower().Contains("guardian"))
							{
								isPlayerAlly = true;
							}
						}
						else if (oldHerd.HerdName.ToLower().Contains("guardian"))
						{
							if (targetHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
							{
								isPlayerAlly = true;
							}
						}

						if (isSameHerd || isPlayerAlly)
						{
							canAttack = false; // No atacar a aliados
						}
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
		public SubsystemGreenNightSky m_subsystemGreenNightSky; // AGREGADO: Subsistema de noche verde
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
		public double m_nextHighAlertCheckTime; // AGREGADO: Para chequeo de alta alerta
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
