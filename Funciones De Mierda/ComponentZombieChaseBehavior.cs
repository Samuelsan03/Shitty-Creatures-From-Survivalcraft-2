using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000046 RID: 70
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
				this.m_lowHealthToEscape = 0.2f; // Valor por defecto
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
		}

		// Token: 0x0600044A RID: 1098 RVA: 0x0003C918 File Offset: 0x0003AB18
		private void SetupZombieInjuryHandler()
		{
			ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
			Action<Injury> originalHandler = componentHealth.Injured;
			Action<Injury> injured = delegate (Injury injury)
			{
				ComponentCreature attacker = injury.Attacker;
				bool flag = attacker != null;
				if (flag)
				{
					this.m_lastAttackTimes[attacker] = this.m_retaliationMemoryDuration;
					this.m_lastAttacker = attacker;

					// SOLO atacar al agresor si no es del mismo rebaño o si ataca a miembros del mismo rebaño está permitido
					bool shouldAttackAttacker = !this.IsSameHerd(attacker) || this.m_attacksSameHerd;

					if (shouldAttackAttacker && attacker != this.m_target)
					{
						this.StopAttack();
						this.Attack(attacker, 30f, 60f, true);
						this.m_retaliationCooldown = 2f;
					}

					bool flag3 = !this.IsSameHerd(attacker) && this.m_componentZombieHerdBehavior != null && this.m_componentZombieHerdBehavior.CallForHelpWhenAttacked;
					if (flag3)
					{
						this.m_componentZombieHerdBehavior.CallZombiesForHelp(attacker);
					}
					bool flag4 = attacker != null && !this.m_attacksSameHerd && this.IsSameHerd(attacker);
					if (flag4)
					{
						bool flag5 = this.m_componentZombieHerdBehavior != null && this.m_componentZombieHerdBehavior.CallForHelpWhenAttacked;
						if (flag5)
						{
							ComponentCreature componentCreature = this.FindExternalAttacker(injury);
							bool flag6 = componentCreature != null;
							if (flag6)
							{
								this.m_componentZombieHerdBehavior.CallZombiesForHelp(componentCreature);
							}
						}
						bool fleeFromSameHerd = this.m_fleeFromSameHerd;
						if (fleeFromSameHerd)
						{
							this.FleeFromTarget(attacker);
						}
					}
					else
					{
						bool flag7 = originalHandler != null;
						if (flag7)
						{
							originalHandler(injury);
						}
					}
				}
				else
				{
					bool flag8 = originalHandler != null;
					if (flag8)
					{
						originalHandler(injury);
					}
				}
			};
			componentHealth.Injured = injured;
		}

		// Token: 0x0600044B RID: 1099 RVA: 0x0003C960 File Offset: 0x0003AB60
		private ComponentCreature FindExternalAttacker(Injury injury)
		{
			bool flag = injury.Attacker == null;
			ComponentCreature result;
			if (flag)
			{
				result = null;
			}
			else
			{
				bool flag2 = !this.IsSameHerd(injury.Attacker);
				if (flag2)
				{
					result = injury.Attacker;
				}
				else
				{
					result = null;
				}
			}
			return result;
		}

		// Token: 0x0600044C RID: 1100 RVA: 0x0003C9A4 File Offset: 0x0003ABA4
		private bool IsSameHerd(ComponentCreature otherCreature)
		{
			bool flag = otherCreature == null || this.m_componentZombieHerdBehavior == null;
			return !flag && this.m_componentZombieHerdBehavior.IsSameZombieHerd(otherCreature);
		}

		// Token: 0x0600044D RID: 1101 RVA: 0x0003C9DC File Offset: 0x0003ABDC
		public override void Attack(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			// Determinar si es una retaliación contra el último atacante
			bool isRetaliating = this.m_lastAttacker != null && target == this.m_lastAttacker;

			// Si el objetivo es del mismo rebaño y no estamos en modo retaliación, no atacar
			bool isSameHerdTarget = !isRetaliating && !this.m_attacksSameHerd && this.IsSameHerd(target);

			if (isSameHerdTarget)
			{
				bool flag3 = this.m_componentZombieHerdBehavior != null;
				if (flag3)
				{
					ComponentCreature componentCreature = this.FindExternalEnemyNearby(maxRange);
					bool flag4 = componentCreature != null;
					if (flag4)
					{
						this.m_componentZombieHerdBehavior.CoordinateGroupAttack(componentCreature);
					}
				}
			}
			else
			{
				// Siempre dar prioridad a las retaliaciones
				if (isRetaliating && this.m_retaliationCooldown <= 0f)
				{
					this.Suppressed = false;
					this.ImportanceLevelNonPersistent = 300f;
					this.ImportanceLevelPersistent = 300f;
				}

				// Lógica de noche verde
				bool isGreenNightForced = this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive;

				if (isRetaliating && isGreenNightForced && target.Entity.FindComponent<ComponentPlayer>() == null)
				{
					Vector3 position = this.m_componentCreature.ComponentBody.Position;
					ComponentPlayer componentPlayer = null;
					float num = float.MaxValue;
					SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
					bool flag7 = subsystemPlayers != null;
					if (flag7)
					{
						foreach (ComponentPlayer componentPlayer2 in subsystemPlayers.ComponentPlayers)
						{
							bool flag8 = componentPlayer2 != null && componentPlayer2.ComponentHealth.Health > 0f;
							if (flag8)
							{
								float num2 = Vector3.Distance(position, componentPlayer2.ComponentBody.Position);
								bool flag9 = num2 <= maxRange && num2 < num;
								if (flag9)
								{
									num = num2;
									componentPlayer = componentPlayer2;
								}
							}
						}
					}
					bool flag10 = componentPlayer != null && num < maxRange * 0.5f;
					if (flag10)
					{
						target = componentPlayer;
					}
				}

				base.Attack(target, maxRange, maxChaseTime, isPersistent);

				// Notificar al rebaño solo si no es una retaliación
				bool flag11 = !isRetaliating && this.m_componentZombieHerdBehavior != null;
				if (flag11)
				{
					this.m_componentZombieHerdBehavior.CoordinateGroupAttack(target);
				}
			}
		}

		// Token: 0x0600044E RID: 1102 RVA: 0x0003CBEC File Offset: 0x0003ADEC
		private ComponentCreature FindExternalEnemyNearby(float range)
		{
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			ComponentCreature result = null;
			float num = 0f;
			this.m_componentBodies.Clear();
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, this.m_componentBodies);
			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentCreature componentCreature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				bool flag = componentCreature != null && componentCreature != this.m_componentCreature;
				if (flag)
				{
					bool flag2 = !this.IsSameHerd(componentCreature);
					if (flag2)
					{
						float num2 = Vector3.Distance(position, componentCreature.ComponentBody.Position);
						float num3 = range - num2;
						bool flag3 = num3 > num;
						if (flag3)
						{
							num = num3;
							result = componentCreature;
						}
					}
				}
			}
			return result;
		}

		// Token: 0x0600044F RID: 1103 RVA: 0x0003CCE0 File Offset: 0x0003AEE0
		public override ComponentCreature FindTarget()
		{
			// PRIORIDAD 1: Retaliación - siempre enfocarse en el último atacante si está vivo y en rango
			bool hasRecentAttacker = this.m_lastAttacker != null && this.m_lastAttackTimes.ContainsKey(this.m_lastAttacker);
			if (hasRecentAttacker)
			{
				bool isAttackerAlive = this.m_lastAttacker.ComponentHealth.Health > 0f;
				bool isAttackerValid = !this.IsSameHerd(this.m_lastAttacker) || this.m_attacksSameHerd;
				float distanceToAttacker = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_lastAttacker.ComponentBody.Position);
				bool isAttackerInRange = distanceToAttacker <= this.m_range * 2f;

				if (isAttackerAlive && isAttackerValid && isAttackerInRange)
				{
					return this.m_lastAttacker;
				}
			}

			// PRIORIDAD 2: Noche verde - buscar jugadores
			bool flag3 = this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive;
			ComponentCreature result;
			if (flag3)
			{
				Vector3 position = this.m_componentCreature.ComponentBody.Position;
				ComponentCreature componentCreature = null;
				float num = 0f;
				ComponentPlayer componentPlayer = null;
				float num2 = float.MaxValue;
				SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
				bool flag4 = subsystemPlayers != null;
				if (flag4)
				{
					foreach (ComponentPlayer componentPlayer2 in subsystemPlayers.ComponentPlayers)
					{
						bool flag5 = componentPlayer2 != null && componentPlayer2.ComponentHealth.Health > 0f;
						if (flag5)
						{
							float num3 = Vector3.Distance(position, componentPlayer2.ComponentBody.Position);
							bool flag6 = num3 <= this.m_range && num3 < num2;
							if (flag6)
							{
								num2 = num3;
								componentPlayer = componentPlayer2;
							}
						}
					}
				}
				this.m_componentBodies.Clear();
				this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), this.m_range, this.m_componentBodies);
				int i = 0;
				while (i < this.m_componentBodies.Count)
				{
					ComponentCreature componentCreature2 = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
					bool flag7 = componentCreature2 != null && componentCreature2 != this.m_componentCreature;
					if (flag7)
					{
						bool flag8 = !this.m_attacksSameHerd && this.IsSameHerd(componentCreature2);
						if (!flag8)
						{
							float num4 = Vector3.Distance(position, componentCreature2.ComponentBody.Position);
							float num5 = this.ScoreTarget(componentCreature2);
							bool flag9 = componentCreature2.Entity.FindComponent<ComponentPlayer>() != null;
							if (flag9)
							{
								num5 *= 1.5f;
							}
							bool flag10 = num5 > num;
							if (flag10)
							{
								num = num5;
								componentCreature = componentCreature2;
							}
						}
					}
				IL_286:
					i++;
					continue;
					goto IL_286;
				}
				bool flag11 = componentPlayer != null && num2 <= this.m_range * 0.7f;
				if (flag11)
				{
					result = componentPlayer;
				}
				else
				{
					result = componentCreature;
				}
			}
			else
			{
				// PRIORIDAD 3: Búsqueda normal excluyendo miembros del mismo rebaño
				bool flag12 = !this.m_attacksSameHerd;
				if (flag12)
				{
					Vector3 position2 = this.m_componentCreature.ComponentBody.Position;
					ComponentCreature componentCreature3 = null;
					float num6 = 0f;
					this.m_componentBodies.Clear();
					this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position2.X, position2.Z), this.m_range, this.m_componentBodies);
					int j = 0;
					while (j < this.m_componentBodies.Count)
					{
						ComponentCreature componentCreature4 = this.m_componentBodies.Array[j].Entity.FindComponent<ComponentCreature>();
						bool flag13 = componentCreature4 != null;
						if (flag13)
						{
							bool flag14 = this.IsSameHerd(componentCreature4);
							if (!flag14)
							{
								float num7 = this.ScoreTarget(componentCreature4);
								bool flag15 = num7 > num6;
								if (flag15)
								{
									num6 = num7;
									componentCreature3 = componentCreature4;
								}
							}
						}
					IL_39A:
						j++;
						continue;
						goto IL_39A;
					}
					result = componentCreature3;
				}
				else
				{
					result = base.FindTarget();
				}
			}
			return result;
		}

		// Token: 0x06000450 RID: 1104 RVA: 0x0003D0C4 File Offset: 0x0003B2C4
		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// Excluir miembros del mismo rebaño si no está permitido atacarlos
			bool flag = !this.m_attacksSameHerd && this.IsSameHerd(componentCreature);
			float result;
			if (flag)
			{
				result = 0f;
			}
			else
			{
				// Bonus masivo para el último atacante (retaliación)
				bool flag2 = componentCreature == this.m_lastAttacker && this.m_lastAttackTimes.ContainsKey(componentCreature) && this.m_lastAttackTimes[componentCreature] > 0f;
				if (flag2)
				{
					result = base.ScoreTarget(componentCreature) * 5f; // Aumentado de 3x a 5x para dar prioridad absoluta
				}
				else
				{
					result = base.ScoreTarget(componentCreature);
				}
			}
			return result;
		}

		// Token: 0x06000451 RID: 1105 RVA: 0x0003D13E File Offset: 0x0003B33E
		private void AddFleeState()
		{
			this.m_stateMachine.AddState("Fleeing", delegate
			{
				this.m_importanceLevel = 150f;
				this.m_componentCreature.ComponentCreatureSounds.PlayPainSound();
			}, delegate
			{
				// Los zombis NO verifican salud baja para salir del estado de huida
				// Solo dejan de huir cuando:
				// 1. No hay objetivo
				// 2. El objetivo está muerto
				// 3. Han huido lo suficiente

				bool flag = this.m_target == null || this.m_componentCreature.ComponentHealth.Health <= 0f;
				if (flag)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
				else
				{
					Vector3 v = this.m_componentCreature.ComponentBody.Position - this.m_target.ComponentBody.Position;
					bool flag3 = v.LengthSquared() > 0.01f;
					if (flag3)
					{
						v = Vector3.Normalize(v);
						Vector3 value = this.m_componentCreature.ComponentBody.Position + v * this.m_fleeDistance;
						this.m_componentPathfinding.SetDestination(new Vector3?(value), 1f, 1.5f, 0, false, true, false, null);
					}
					float num = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_target.ComponentBody.Position);
					bool flag4 = num > this.m_fleeDistance * 1.5f;
					if (flag4)
					{
						this.m_stateMachine.TransitionTo("LookingForTarget");
					}
					bool flag5 = this.m_random.Float(0f, 1f) < 0.05f * this.m_dt;
					if (flag5)
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

		// Token: 0x06000452 RID: 1106 RVA: 0x0003D178 File Offset: 0x0003B378
		private void FleeFromTarget(ComponentCreature target)
		{
			// Los zombis huyen solo cuando son atacados por miembros de la misma manada,
			// independientemente de su salud (no verificamos salud baja)

			bool flag = target == null || this.m_componentCreature.ComponentHealth.Health <= 0f;
			if (!flag)
			{
				this.m_target = target;
				this.m_stateMachine.TransitionTo("Fleeing");
			}
		}

		// Token: 0x06000453 RID: 1107 RVA: 0x0003D1E4 File Offset: 0x0003B3E4
		public override void Update(float dt)
		{
			base.Update(dt);
			bool flag = this.m_retaliationCooldown > 0f;
			if (flag)
			{
				this.m_retaliationCooldown -= dt;
			}
			List<ComponentCreature> list = new List<ComponentCreature>();
			foreach (KeyValuePair<ComponentCreature, float> keyValuePair in this.m_lastAttackTimes)
			{
				this.m_lastAttackTimes[keyValuePair.Key] = keyValuePair.Value - dt;
				bool flag2 = this.m_lastAttackTimes[keyValuePair.Key] <= 0f;
				if (flag2)
				{
					list.Add(keyValuePair.Key);
					bool flag3 = keyValuePair.Key == this.m_lastAttacker;
					if (flag3)
					{
						this.m_lastAttacker = null;
					}
				}
			}
			foreach (ComponentCreature key in list)
			{
				this.m_lastAttackTimes.Remove(key);
			}
			bool flag4 = this.m_forceAttackDuringGreenNight && this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive;
			if (flag4)
			{
				this.AttacksPlayer = true;
				this.Suppressed = false;
				bool flag5 = this.m_stateMachine.CurrentState == "Fleeing";
				if (flag5)
				{
					this.m_stateMachine.TransitionTo("LookingForTarget");
				}
			}
			else
			{
				bool flag6 = this.m_subsystemGreenNightSky != null && !this.m_subsystemGreenNightSky.IsGreenNightActive;
				if (flag6)
				{
					this.AttacksPlayer = this.m_attacksAllCategories;
				}
			}

			// Verificar si debemos cambiar al último atacante
			bool shouldSwitchToAttacker = this.m_lastAttacker != null &&
										 this.m_retaliationCooldown <= 0f &&
										 this.m_target != this.m_lastAttacker &&
										 (!this.IsSameHerd(this.m_lastAttacker) || this.m_attacksSameHerd);

			if (shouldSwitchToAttacker)
			{
				bool isAttackerAlive = this.m_lastAttacker.ComponentHealth.Health > 0f;
				float distanceToAttacker = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, this.m_lastAttacker.ComponentBody.Position);
				bool isAttackerInRange = distanceToAttacker <= this.m_range * 1.5f;

				if (isAttackerAlive && isAttackerInRange)
				{
					this.StopAttack();
					this.Attack(this.m_lastAttacker, 30f, 60f, true);
					this.m_retaliationCooldown = 1f;
				}
			}
		}

		// Token: 0x06000454 RID: 1108 RVA: 0x0003D470 File Offset: 0x0003B670
		public override void StopAttack()
		{
			base.StopAttack();
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
	}
}
