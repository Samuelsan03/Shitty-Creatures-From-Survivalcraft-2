using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		// Token: 0x06000449 RID: 1097 RVA: 0x0003C850 File Offset: 0x0003AA50
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_componentZombieHerdBehavior = base.Entity.FindComponent<ComponentZombieHerdBehavior>();
			this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			this.m_attacksSameHerd = valuesDictionary.GetValue<bool>("AttacksSameHerd", false);
			this.m_attacksAllCategories = valuesDictionary.GetValue<bool>("AttacksAllCategories", true);
			this.m_fleeFromSameHerd = valuesDictionary.GetValue<bool>("FleeFromSameHerd", true);
			this.m_fleeDistance = valuesDictionary.GetValue<float>("FleeDistance", 10f);
			this.m_forceAttackDuringGreenNight = valuesDictionary.GetValue<bool>("ForceAttackDuringGreenNight", false);

			// Referencia al ComponentZombieRunAwayBehavior y su LowHealthToEscape
			this.m_zombieRunAwayBehavior = base.Entity.FindComponent<ComponentZombieRunAwayBehavior>();
			if (this.m_zombieRunAwayBehavior != null)
			{
				this.m_lowHealthToEscape = this.m_zombieRunAwayBehavior.LowHealthToEscape;
			}
			else
			{
				this.m_lowHealthToEscape = 0.2f;
			}

			bool attacksAllCategories = this.m_attacksAllCategories;
			if (attacksAllCategories)
			{
				this.m_autoChaseMask = (CreatureCategory.LandPredator | CreatureCategory.LandOther | CreatureCategory.WaterPredator | CreatureCategory.WaterOther | CreatureCategory.Bird);
				this.AttacksNonPlayerCreature = true;
				this.AttacksPlayer = true;
			}
			this.SetupZombieInjuryHandler();
			this.AddFleeState();

			this.m_previousGreenNightActive = false;
			m_defaultTargetInRangeTime = this.TargetInRangeTimeToChase;

			if (this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive)
			{
				this.TargetInRangeTimeToChase = 0f;
				this.m_targetInRangeTime = this.TargetInRangeTimeToChase + 1f;
			}
		}

		// Token: 0x0600044A RID: 1098 RVA: 0x0003C918 File Offset: 0x0003AB18
		private void SetupZombieInjuryHandler()
		{
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			Action<Injury> originalHandler = componentHealth.Injured;

			componentHealth.Injured = (Action<Injury>)Delegate.Combine(originalHandler, new Action<Injury>(delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				if (attacker != null)
				{
					// Registrar al atacante
					this.m_lastAttackTimes[attacker] = this.m_retaliationMemoryDuration;

					// Guardar el último atacante (el más reciente)
					this.m_lastAttacker = attacker;

					// Guardar en la cola de retaliación
					this.m_retaliationQueue.Add(attacker);

					// Limitar tamaño de la cola
					while (this.m_retaliationQueue.Count > 5)
					{
						this.m_retaliationQueue.RemoveAt(0);
					}

					// Determinar si debemos atacar al agresor
					bool shouldAttackAttacker = !this.IsSameHerd(attacker) || this.m_attacksSameHerd;

					// SIEMPRE atacar al agresor si es válido (sin importar el objetivo actual)
					if (shouldAttackAttacker)
					{
						// Detener el ataque actual si es diferente
						if (this.m_target != attacker)
						{
							this.StopAttack();

							// Calcular tiempo de persecución basado en noche verde
							bool isGreenNightActive = this.m_forceAttackDuringGreenNight &&
													  this.m_subsystemGreenNightSky != null &&
													  this.m_subsystemGreenNightSky.IsGreenNightActive;

							float chaseTime = isGreenNightActive ? 120f : 60f; // Más tiempo durante noche verde

							// Atacar al agresor con máxima prioridad
							this.Attack(attacker, 40f, chaseTime, true);
							this.m_retaliationCooldown = 1f;

							// Marcar que estamos en modo retaliación
							this.m_isRetaliating = true;
							this.m_retaliationTarget = attacker;
						}
					}

					// Llamar a ayuda si es necesario
					if (!this.IsSameHerd(attacker) && this.m_componentZombieHerdBehavior != null &&
						this.m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
					{
						this.m_componentZombieHerdBehavior.CallZombiesForHelp(attacker);
					}

					// Manejar ataque de mismo rebaño
					if (attacker != null && !this.m_attacksSameHerd && this.IsSameHerd(attacker))
					{
						if (this.m_componentZombieHerdBehavior != null &&
							this.m_componentZombieHerdBehavior.CallForHelpWhenAttacked)
						{
							ComponentCreature externalAttacker = this.FindExternalAttacker(injury);
							if (externalAttacker != null)
							{
								this.m_componentZombieHerdBehavior.CallZombiesForHelp(externalAttacker);
							}
						}

						if (this.m_fleeFromSameHerd)
						{
							this.FleeFromTarget(attacker);
						}
					}
				}
			}));
		}

		// Token: 0x0600044B RID: 1099 RVA: 0x0003C960 File Offset: 0x0003AB60
		private ComponentCreature FindExternalAttacker(Injury injury)
		{
			if (injury.Attacker == null)
				return null;

			return !this.IsSameHerd(injury.Attacker) ? injury.Attacker : null;
		}

		// Token: 0x0600044C RID: 1100 RVA: 0x0003C9A4 File Offset: 0x0003ABA4
		private bool IsSameHerd(ComponentCreature otherCreature)
		{
			return otherCreature != null && this.m_componentZombieHerdBehavior != null &&
				   this.m_componentZombieHerdBehavior.IsSameZombieHerd(otherCreature);
		}

		// Token: 0x0600044D RID: 1101 RVA: 0x0003C9DC File Offset: 0x0003ABDC
		public override void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			bool isRetaliating = this.m_isRetaliating && target == this.m_retaliationTarget;
			bool isSameHerdTarget = !isRetaliating && !this.m_attacksSameHerd && this.IsSameHerd(target);

			if (isSameHerdTarget)
			{
				if (this.m_componentZombieHerdBehavior != null)
				{
					ComponentCreature externalEnemy = this.FindExternalEnemyNearby(maxRange);
					if (externalEnemy != null)
					{
						this.m_componentZombieHerdBehavior.CoordinateGroupAttack(externalEnemy);
					}
				}
			}
			else
			{
				// Dar prioridad máxima a retaliaciones
				if (isRetaliating)
				{
					this.Suppressed = false;
					this.ImportanceLevelNonPersistent = 500f; // Prioridad máxima
					this.ImportanceLevelPersistent = 500f;

					// Durante retaliación, ignorar supresión
					this.m_autoChaseSuppressionTime = 0f;
				}

				// Verificar si es noche verde
				bool isGreenNightActive = this.m_forceAttackDuringGreenNight &&
										  this.m_subsystemGreenNightSky != null &&
										  this.m_subsystemGreenNightSky.IsGreenNightActive;

				// Durante noche verde, priorizar jugadores pero sin ignorar retaliaciones
				if (isGreenNightActive && !isRetaliating && target.Entity.FindComponent<ComponentPlayer>() == null)
				{
					// Buscar jugador cercano
					ComponentPlayer nearestPlayer = this.FindNearestPlayer(maxRange);
					if (nearestPlayer != null)
					{
						target = nearestPlayer;
					}
				}

				base.Attack(target, maxRange, maxChaseTime, isPersistent);

				// Notificar al rebaño
				if (!isRetaliating && this.m_componentZombieHerdBehavior != null)
				{
					this.m_componentZombieHerdBehavior.CoordinateGroupAttack(target);
				}
			}
		}

		// Token: 0x0600044E RID: 1102 RVA: 0x0003CBEC File Offset: 0x0003ADEC
		private ComponentCreature FindExternalEnemyNearby(float range)
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature bestTarget = null;
			float bestScore = 0f;

			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature != this.m_componentCreature && !this.IsSameHerd(creature))
				{
					float dist = Vector3.Distance(position, creature.ComponentBody.Position);
					float score = range - dist;
					if (score > bestScore)
					{
						bestScore = score;
						bestTarget = creature;
					}
				}
			}
			return bestTarget;
		}

		// Token: 0x0600044F RID: 1103 RVA: 0x0003CCE0 File Offset: 0x0003AEE0
		private ComponentPlayer FindNearestPlayer(float range)
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentPlayer nearestPlayer = null;
			float minDist = float.MaxValue;

			SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			if (subsystemPlayers != null)
			{
				foreach (ComponentPlayer player in subsystemPlayers.ComponentPlayers)
				{
					if (player != null && player.ComponentHealth.Health > 0f)
					{
						float dist = Vector3.Distance(position, player.ComponentBody.Position);
						if (dist <= range && dist < minDist)
						{
							minDist = dist;
							nearestPlayer = player;
						}
					}
				}
			}
			return nearestPlayer;
		}

		// Token: 0x06000450 RID: 1104 RVA: 0x0003D0C4 File Offset: 0x0003B2C4
		public override ComponentCreature FindTarget()
		{
			// PRIORIDAD 1: Retaliación - SIEMPRE verificar primero
			ComponentCreature retaliationTarget = this.GetNextRetaliationTarget();
			if (retaliationTarget != null)
			{
				return retaliationTarget;
			}

			// PRIORIDAD 2: Noche verde - buscar jugadores
			bool isGreenNightActive = this.m_forceAttackDuringGreenNight &&
									  this.m_subsystemGreenNightSky != null &&
									  this.m_subsystemGreenNightSky.IsGreenNightActive;
			if (isGreenNightActive)
			{
				ComponentPlayer nearestPlayer = this.FindNearestPlayer(this.m_range);
				if (nearestPlayer != null)
				{
					return nearestPlayer;
				}
			}

			// PRIORIDAD 3: Búsqueda normal
			if (!this.m_attacksSameHerd)
			{
				Vector3 position = this.m_componentCreature.ComponentBody.Position;
				ComponentCreature bestTarget = null;
				float bestScore = 0f;

				this.m_componentBodies.Clear();
				this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);

				for (int i = 0; i < this.m_componentBodies.Count; i++)
				{
					ComponentCreature candidate = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					if (candidate != null && !this.IsSameHerd(candidate))
					{
						float score = this.ScoreTarget(candidate);
						if (score > bestScore)
						{
							bestScore = score;
							bestTarget = candidate;
						}
					}
				}
				return bestTarget;
			}

			return base.FindTarget();
		}

		// Token: 0x06000451 RID: 1105 RVA: 0x0003D1F0 File Offset: 0x0003B3F0
		private ComponentCreature GetNextRetaliationTarget()
		{
			// Limpiar atacantes muertos
			for (int i = this.m_retaliationQueue.Count - 1; i >= 0; i--)
			{
				ComponentCreature attacker = this.m_retaliationQueue[i];
				if (attacker == null || attacker.ComponentHealth.Health <= 0f ||
					!this.m_lastAttackTimes.ContainsKey(attacker) ||
					this.m_lastAttackTimes[attacker] <= 0f)
				{
					this.m_retaliationQueue.RemoveAt(i);
				}
			}

			// Devolver el atacante más reciente
			if (this.m_retaliationQueue.Count > 0)
			{
				ComponentCreature latestAttacker = this.m_retaliationQueue[this.m_retaliationQueue.Count - 1];

				// Verificar que sea válido para atacar
				bool isValid = (!this.IsSameHerd(latestAttacker) || this.m_attacksSameHerd) &&
							   Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
											   latestAttacker.ComponentBody.Position) <= this.m_range * 2f;

				if (isValid)
				{
					return latestAttacker;
				}
			}

			return null;
		}

		// Token: 0x06000452 RID: 1106 RVA: 0x0003D2F4 File Offset: 0x0003B4F4
		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// Excluir miembros del mismo rebaño
			if (!this.m_attacksSameHerd && this.IsSameHerd(componentCreature))
			{
				return 0f;
			}

			float baseScore = base.ScoreTarget(componentCreature);

			// BONUS MÁXIMO para retaliaciones (10x en lugar de 5x)
			if (this.m_retaliationQueue.Contains(componentCreature) &&
				this.m_lastAttackTimes.ContainsKey(componentCreature) &&
				this.m_lastAttackTimes[componentCreature] > 0f)
			{
				return baseScore * 10f; // Prioridad absoluta
			}

			// BONUS para el último atacante
			if (componentCreature == this.m_lastAttacker &&
				this.m_lastAttackTimes.ContainsKey(componentCreature) &&
				this.m_lastAttackTimes[componentCreature] > 0f)
			{
				return baseScore * 8f;
			}

			return baseScore;
		}

		// Token: 0x06000453 RID: 1107 RVA: 0x0003D3A8 File Offset: 0x0003B5A8
		private void AddFleeState()
		{
			this.m_stateMachine.AddState("Fleeing", delegate
			{
				this.m_importanceLevel = 150f;
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}, delegate
			{
				if (this.m_target == null || this.m_componentCreature.ComponentHealth.Health <= 0f)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else
				{
					Vector3 v = this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position;
					if (v.LengthSquared() > 0.01f)
					{
						v = Vector3.Normalize(v);
						Vector3 destination = this.m_componentCreature.ComponentBody.Position + v * this.m_fleeDistance;
						this.m_componentPathfinding.SetDestination(new Vector3?(destination), 1f, 1.5f, 0, false, true, false, null);
					}

					float dist = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);
					if (dist > this.m_fleeDistance * 1.5f)
					{
						this.m_stateMachine.TransitionTo("LookingForTarget");
					}

					if (this.m_random.Float(0f, 1f) < 0.05f * this.m_dt)
					{
						this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
					}
				}
			}, delegate
			{
				this.m_componentPathfinding.Stop();
				this.m_importanceLevel = 0f;
			});
		}

		// Token: 0x06000454 RID: 1108 RVA: 0x0003D3F0 File Offset: 0x0003B5F0
		private void FleeFromTarget(ComponentCreature target)
		{
			if (target != null && this.m_componentCreature.ComponentHealth.Health > 0f)
			{
				this.m_target = target;
				this.m_stateMachine.TransitionTo("Fleeing");
			}
		}

		// Token: 0x06000455 RID: 1109 RVA: 0x0003D440 File Offset: 0x0003B640
		public override void Update(float dt)
		{
			base.Update(dt);

			// Actualizar cooldown de retaliación
			if (this.m_retaliationCooldown > 0f)
			{
				this.m_retaliationCooldown -= dt;
			}

			// Actualizar tiempos de memoria de ataque
			List<ComponentCreature> expiredAttackers = new List<ComponentCreature>();
			foreach (var kvp in this.m_lastAttackTimes)
			{
				this.m_lastAttackTimes[kvp.Key] = kvp.Value - dt;
				if (this.m_lastAttackTimes[kvp.Key] <= 0f)
				{
					expiredAttackers.Add(kvp.Key);

					// Limpiar de la cola de retaliación
					this.m_retaliationQueue.Remove(kvp.Key);

					if (kvp.Key == this.m_lastAttacker)
					{
						this.m_lastAttacker = null;
					}
					if (kvp.Key == this.m_retaliationTarget)
					{
						this.m_retaliationTarget = null;
						this.m_isRetaliating = false;
					}
				}
			}

			foreach (ComponentCreature attacker in expiredAttackers)
			{
				this.m_lastAttackTimes.Remove(attacker);
			}

			// Detectar estado de noche verde
			bool greenNightActive = this.m_forceAttackDuringGreenNight &&
									this.m_subsystemGreenNightSky != null &&
									this.m_subsystemGreenNightSky.IsGreenNightActive;

			// Manejar cambios de noche verde
			if (greenNightActive != this.m_previousGreenNightActive)
			{
				if (greenNightActive && !this.m_previousGreenNightActive)
				{
					// NOCHE VERDE COMIENZA
					this.TargetInRangeTimeToChase = 0f;

					// Si no estamos en retaliación, buscar jugador inmediatamente
					if (!this.m_isRetaliating)
					{
						ComponentPlayer nearestPlayer = this.FindNearestPlayer(this.m_range);
						if (nearestPlayer != null)
						{
							this.StopAttack();
							this.Attack(nearestPlayer, this.m_range, 120f, true);
						}
					}
				}
				else if (!greenNightActive && this.m_previousGreenNightActive)
				{
					// NOCHE VERDE TERMINA
					this.TargetInRangeTimeToChase = m_defaultTargetInRangeTime;

					// Solo detener ataque si no estamos en retaliación
					if (!this.m_isRetaliating)
					{
						this.StopAttack();
						this.m_target = null;

						if (this.m_stateMachine.CurrentState != "LookingForTarget")
						{
							this.m_stateMachine.TransitionTo("LookingForTarget");
						}
					}

					this.AttacksPlayer = this.m_attacksAllCategories;
				}

				this.m_previousGreenNightActive = greenNightActive;
			}

			// Lógica durante noche verde
			if (greenNightActive)
			{
				this.AttacksPlayer = true;
				this.Suppressed = false;
				this.TargetInRangeTimeToChase = 0f;
				this.m_targetInRangeTime = 1f;

				if (this.m_stateMachine.CurrentState == "Fleeing")
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}

				// IMPORTANTE: Durante noche verde, seguir priorizando retaliaciones
				if (!this.m_isRetaliating && this.m_target == null)
				{
					ComponentPlayer nearestPlayer = this.FindNearestPlayer(this.m_range);
					if (nearestPlayer != null)
					{
						this.Attack(nearestPlayer, this.m_range, 120f, true);
					}
				}
			}
			else
			{
				if (this.m_subsystemGreenNightSky != null && !this.m_subsystemGreenNightSky.IsGreenNightActive)
				{
					this.TargetInRangeTimeToChase = m_defaultTargetInRangeTime;
				}

				// Verificar si debemos cambiar al siguiente objetivo (fuera de noche verde)
				if (!this.m_isRetaliating)
				{
					ComponentCreature nextRetaliation = this.GetNextRetaliationTarget();
					if (nextRetaliation != null && nextRetaliation != this.m_target)
					{
						this.StopAttack();
						this.Attack(nextRetaliation, 30f, 60f, true);
						this.m_isRetaliating = true;
						this.m_retaliationTarget = nextRetaliation;
						this.m_retaliationCooldown = 1f;
					}
				}
			}

			// Verificar si el objetivo de retaliación sigue siendo válido
			if (this.m_isRetaliating && this.m_retaliationTarget != null)
			{
				bool targetStillValid = this.m_retaliationTarget.ComponentHealth.Health > 0f &&
									   Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
													   this.m_retaliationTarget.ComponentBody.Position) <= this.m_range * 2f &&
									   (!this.IsSameHerd(this.m_retaliationTarget) || this.m_attacksSameHerd);

				if (!targetStillValid)
				{
					this.m_isRetaliating = false;
					this.m_retaliationTarget = null;

					// Buscar siguiente objetivo en cola
					ComponentCreature nextTarget = this.GetNextRetaliationTarget();
					if (nextTarget != null)
					{
						this.Attack(nextTarget, 30f, 60f, true);
						this.m_isRetaliating = true;
						this.m_retaliationTarget = nextTarget;
					}
				}
			}
		}

		// Token: 0x06000456 RID: 1110 RVA: 0x0003D7C0 File Offset: 0x0003B9C0
		public override void StopAttack()
		{
			base.StopAttack();

			// No resetear estado de retaliación aquí, se maneja en Update
		}

		// Token: 0x04000485 RID: 1157
		private ComponentZombieHerdBehavior m_componentZombieHerdBehavior;

		// Token: 0x04000486 RID: 1158
		private SubsystemGreenNightSky m_subsystemGreenNightSky;

		// Token: 0x04000487 RID: 1159
		private Dictionary<ComponentCreature, float> m_lastAttackTimes = new Dictionary<ComponentCreature, float>();

		// Token: 0x04000488 RID: 1160
		private float m_retaliationMemoryDuration = 30f;

		// Token: 0x04000489 RID: 1161
		private ComponentCreature m_lastAttacker;

		// Token: 0x0400048A RID: 1162
		private float m_retaliationCooldown;

		// Token: 0x0400048B RID: 1163
		private bool m_attacksSameHerd;

		// Token: 0x0400048C RID: 1164
		private bool m_attacksAllCategories;

		// Token: 0x0400048D RID: 1165
		private bool m_fleeFromSameHerd;

		// Token: 0x0400048E RID: 1166
		private float m_fleeDistance = 10f;

		// Token: 0x0400048F RID: 1167
		private bool m_forceAttackDuringGreenNight;

		// Token: 0x04000490 RID: 1168
		private ComponentZombieRunAwayBehavior m_zombieRunAwayBehavior;

		// Token: 0x04000491 RID: 1169
		private float m_lowHealthToEscape;

		// Token: 0x04000492 RID: 1170
		private bool m_previousGreenNightActive;

		// Token: 0x04000493 RID: 1171
		private float m_defaultTargetInRangeTime = 3f;

		// NUEVOS CAMPOS PARA RETALIACIÓN MEJORADA
		private List<ComponentCreature> m_retaliationQueue = new List<ComponentCreature>();
		private bool m_isRetaliating;
		private ComponentCreature m_retaliationTarget;
	}
}
