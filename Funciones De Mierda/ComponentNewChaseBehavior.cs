// ComponentNewChaseBehavior.cs (CORREGIDO - Reacciona a ataques del jugador)
using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewChaseBehavior : ComponentChaseBehavior, IUpdateable
	{
		// Subsistemas
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemSky m_subsystemSky;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemGameWidgets m_subsystemGameWidgets;

		// Componentes
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentMiner m_componentMiner;
		private ComponentRandomFeedBehavior m_componentFeedBehavior;
		private ComponentCreatureModel m_componentCreatureModel;
		private ComponentFactors m_componentFactors;
		private ComponentBody m_componentBody;
		private ComponentNewHerdBehavior m_componentNewHerd;
		private ComponentHealth m_componentHealth;
		private ComponentPlayer m_componentPlayer;
		private ComponentHerdBehavior m_componentOldHerd;

		// Listas y random
		private DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
		private Game.Random m_random = new Game.Random();
		private StateMachine m_stateMachine = new StateMachine();

		// Variables de configuración
		private float m_dayChaseRange;
		private float m_nightChaseRange;
		private float m_dayChaseTime;
		private float m_nightChaseTime;
		private float m_chaseNonPlayerProbability;
		private float m_chasePlayerProbability = 1f;
		private float m_chaseWhenAttackedProbability;
		private float m_chaseOnTouchProbability;
		private CreatureCategory m_autoChaseMask;
		private float m_importanceLevel;
		private float m_targetUnsuitableTime;
		private float m_targetInRangeTime;
		private double m_nextUpdateTime;
		private ComponentCreature m_target;
		private float m_dt;
		private float m_range;
		private float m_chaseTime;
		private bool m_isPersistent;
		private float m_autoChaseSuppressionTime;

		// Nuevos campos
		private AttackMode m_attackMode = AttackMode.Default;
		private Vector2 m_attackRange = new Vector2(0f, 5f);
		private double m_lastActionTime;
		private float m_lastShotSoundTime;
		private ComponentCreature m_lastAttacker;
		private double m_lastAttackTime;
		private float m_attackPersistanceFactor = 1f;
		private float m_switchTargetCooldown;
		private bool m_isNearDeath;
		private bool m_extremeProtectionActive;
		private const float AttackerMemoryDuration = 30f;
		private int m_consecutiveBreakCount;
		private double m_lastBreakTime;
		private bool m_allowBreakBlockWhenStuck;
		private bool m_autoDismount = true;
		private int m_targetBodyStyle = 3;
		private float m_herdingRange = 20f;
		private ComponentPlayer m_nearestPlayer;
		private bool m_isPlayerAlly;
		private double m_lastPlayerAttackCheckTime;
		private const float PlayerAttackCheckInterval = 0.5f;
		private ComponentCreature m_lastPlayerAttacker;
		private double m_nextHighAlertCheckTime;
		private double m_nextBanditCheckTime;

		public enum AttackMode
		{
			Default,
			OnlyHand,
			Remote
		}

		public new ComponentCreature Target => m_target;
		public override float ImportanceLevel => m_importanceLevel;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemGreenNightSky = Project.FindSubsystem<SubsystemGreenNightSky>(true);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			m_subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentFeedBehavior = Entity.FindComponent<ComponentRandomFeedBehavior>();
			m_componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentFactors = Entity.FindComponent<ComponentFactors>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentNewHerd = Entity.FindComponent<ComponentNewHerdBehavior>();
			m_componentOldHerd = Entity.FindComponent<ComponentHerdBehavior>();
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentPlayer = Entity.FindComponent<ComponentPlayer>();

			m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
			m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
			m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
			m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
			m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
			m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
			m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");
			m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");

			m_attackRange = valuesDictionary.GetValue<Vector2>("AttackRange", new Vector2(0f, 5f));
			m_allowBreakBlockWhenStuck = valuesDictionary.GetValue<bool>("AllowBreakBlockWhenStuck", false);
			m_autoDismount = valuesDictionary.GetValue<bool>("AutoDismount", true);
			m_targetBodyStyle = valuesDictionary.GetValue<int>("TargetBodyStyle", 3);
			m_chasePlayerProbability = valuesDictionary.GetValue<float>("ChasePlayerProbability", 1f);
			m_herdingRange = valuesDictionary.GetValue<float>("HerdingRange", 20f);

			string attackModeStr = valuesDictionary.GetValue<string>("AttackMode", "Default");

			if (!Enum.TryParse<AttackMode>(attackModeStr, true, out m_attackMode))
			{
				m_attackMode = AttackMode.Default;
			}

			// Determinar si esta criatura es aliada del jugador
			m_isPlayerAlly = IsPlayerAlly(m_componentCreature);

			SetupEventHooks();
			SubscribeToPlayerEvents();
			m_stateMachine = new StateMachine();
			SetupStateMachine();
			m_stateMachine.TransitionTo("LookingForTarget");
		}

		// Determina si una criatura es aliada del jugador (desde la perspectiva global)
		private bool IsPlayerAlly(ComponentCreature creature)
		{
			if (creature == null)
				return false;

			// Si es el propio jugador
			if (creature.Entity.FindComponent<ComponentPlayer>() != null)
				return true;

			// Si tiene manada de jugador o guardián (nuevo sistema)
			ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
			{
				string herdName = newHerd.HerdName.ToLower();
				if (herdName == "player" || herdName.Contains("guardian"))
					return true;
			}

			// Si tiene manada de jugador o guardián (sistema antiguo)
			ComponentHerdBehavior oldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();
			if (oldHerd != null && !string.IsNullOrEmpty(oldHerd.HerdName))
			{
				string herdName = oldHerd.HerdName.ToLower();
				if (herdName == "player" || herdName.Contains("guardian"))
					return true;
			}

			return false;
		}

		// Responder a comandos inmediatamente
		public void RespondToCommandImmediately(ComponentCreature target)
		{
			if (this.Suppressed || target == null)
				return;

			// Verificar si podemos atacar según reglas de manada
			if (m_componentNewHerd != null)
			{
				if (!m_componentNewHerd.CanAttackCreature(target))
					return;
			}
			else
			{
				// Si no hay manada nueva, verificar con el sistema de alianzas
				if (IsAlly(target))
					return;
			}

			this.m_target = target;
			this.m_nextUpdateTime = 0.0;
			this.m_range = 20f;
			this.m_chaseTime = 30f;
			this.m_isPersistent = false;
			this.m_importanceLevel = this.ImportanceLevelNonPersistent;
			this.IsActive = true;
			this.m_stateMachine.TransitionTo("Chasing");

			// Iniciar movimiento inmediato
			if (this.m_target != null && this.m_componentPathfinding != null)
			{
				this.m_componentPathfinding.Stop();
				Vector3 targetPosition = this.m_target.ComponentBody.Position;
				this.m_componentPathfinding.SetDestination(targetPosition, 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = this.m_target.ComponentCreatureModel.EyePosition;
			}
		}


		// Determina si otra criatura es aliada de ESTA criatura
		private bool IsAlly(ComponentCreature other)
		{
			if (other == null || other == m_componentCreature)
				return false;

			// Misma manada o guardián (nuevo sistema)
			if (m_componentNewHerd != null)
			{
				if (m_componentNewHerd.IsSameHerdOrGuardian(other))
					return true;
			}

			// Misma manada o guardián (sistema antiguo)
			if (m_componentOldHerd != null)
			{
				ComponentHerdBehavior otherHerd = other.Entity.FindComponent<ComponentHerdBehavior>();
				if (otherHerd != null && !string.IsNullOrEmpty(otherHerd.HerdName) && !string.IsNullOrEmpty(m_componentOldHerd.HerdName))
				{
					bool isSameHerd = otherHerd.HerdName == m_componentOldHerd.HerdName;

					// Verificar relación player-guardian
					bool isPlayerAllyRelation = false;

					if (m_componentOldHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
					{
						if (otherHerd.HerdName.ToLower().Contains("guardian"))
							isPlayerAllyRelation = true;
					}
					else if (m_componentOldHerd.HerdName.ToLower().Contains("guardian"))
					{
						if (otherHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
							isPlayerAllyRelation = true;
					}

					if (isSameHerd || isPlayerAllyRelation)
						return true;
				}
			}

			// Si el otro es jugador y esta criatura es aliada del jugador
			if (other.Entity.FindComponent<ComponentPlayer>() != null && m_isPlayerAlly)
				return true;

			return false;
		}

		// Verificar si es bandido
		private bool IsBandit(ComponentCreature creature)
		{
			if (creature == null) return false;

			// Verificar manada nueva
			ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
			{
				string herdName = newHerd.HerdName;
				if (herdName.Equals("bandit", StringComparison.OrdinalIgnoreCase))
					return true;
			}

			// Verificar manada vieja
			ComponentHerdBehavior oldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();
			if (oldHerd != null && !string.IsNullOrEmpty(oldHerd.HerdName))
			{
				string herdName = oldHerd.HerdName;
				if (herdName.Equals("bandit", StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		// Verificar si es zombie/infectado
		private bool IsZombieOrInfected(ComponentCreature creature)
		{
			// Verificar por componentes específicos de zombies
			ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
			if (zombieChase != null)
				return true;

			ComponentZombieHerdBehavior zombieHerd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
			if (zombieHerd != null)
				return true;

			return false;
		}

		// Verificar si somos guardián o aliado del jugador
		private bool IsGuardianOrPlayerAlly()
		{
			if (m_componentPlayer != null)
				return true;

			if (m_componentNewHerd != null && !string.IsNullOrEmpty(m_componentNewHerd.HerdName))
			{
				string herdName = m_componentNewHerd.HerdName;
				return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
					   herdName.ToLower().Contains("guardian");
			}

			if (m_componentOldHerd != null && !string.IsNullOrEmpty(m_componentOldHerd.HerdName))
			{
				string herdName = m_componentOldHerd.HerdName;
				return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
					   herdName.ToLower().Contains("guardian");
			}

			return false;
		}

		// Verificar si está atacando al jugador
		private bool IsAttackingPlayer(ComponentCreature creature, ComponentPlayer player)
		{
			if (creature == null || player == null)
				return false;

			ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
			if (chase != null && chase.Target == player)
				return true;

			ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
			if (newChase != null && newChase.Target == player)
				return true;

			return false;
		}

		private void SubscribeToPlayerEvents()
		{
			if (!m_isPlayerAlly)
				return;

			// Encontrar al jugador más cercano
			FindNearestPlayer();

			if (m_nearestPlayer != null)
			{
				// Suscribirse al evento de daño del jugador
				m_nearestPlayer.ComponentHealth.Injured += OnPlayerInjured;
			}
		}

		private void UnsubscribeFromPlayerEvents()
		{
			if (m_nearestPlayer != null)
			{
				m_nearestPlayer.ComponentHealth.Injured -= OnPlayerInjured;
			}
		}

		private void OnPlayerInjured(Injury injury)
		{
			if (!m_isPlayerAlly)
				return;

			ComponentCreature attacker = injury.Attacker;
			if (attacker == null)
				return;

			// No atacar a aliados
			if (IsAlly(attacker))
				return;

			// Recordar al atacante
			m_lastPlayerAttacker = attacker;

			// Atacar al agresor del jugador
			float chaseRange = Math.Max(m_dayChaseRange, m_nightChaseRange) * 1.5f;
			float chaseTime = Math.Max(m_dayChaseTime, m_nightChaseTime) * 1.5f;

			Attack(attacker, chaseRange, chaseTime, true);

			// Llamar a otros aliados cercanos para ayudar
			if (m_componentNewHerd != null)
			{
				m_componentNewHerd.CallNearbyCreaturesHelp(attacker, m_herdingRange, chaseTime, false, true);
			}
		}

		private void FindNearestPlayer()
		{
			if (m_subsystemPlayers == null)
				return;

			float nearestDist = float.MaxValue;
			Vector3 myPos = m_componentCreature.ComponentBody.Position;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player != null && player.ComponentHealth != null && player.ComponentHealth.Health > 0f)
				{
					float dist = Vector3.Distance(myPos, player.ComponentBody.Position);
					if (dist < nearestDist)
					{
						nearestDist = dist;
						m_nearestPlayer = player;
					}
				}
			}
		}

		private void CheckPlayerAttackEvents()
		{
			if (!m_isPlayerAlly || m_subsystemTime.GameTime < m_lastPlayerAttackCheckTime + PlayerAttackCheckInterval)
				return;

			m_lastPlayerAttackCheckTime = m_subsystemTime.GameTime;

			if (m_nearestPlayer == null || m_nearestPlayer.ComponentHealth.Health <= 0f)
			{
				FindNearestPlayer();
				if (m_nearestPlayer != null)
				{
					UnsubscribeFromPlayerEvents();
					SubscribeToPlayerEvents();
				}
				return;
			}

			// Verificar si el jugador está siendo atacado actualmente
			ComponentCreature currentAttacker = FindPlayerAttacker();
			if (currentAttacker != null && currentAttacker != m_lastPlayerAttacker)
			{
				// Crear Injury con los parámetros correctos
				Injury injury = new Injury(0f, currentAttacker, false, "PlayerAttacked");
				OnPlayerInjured(injury);
			}
		}

		private ComponentCreature FindPlayerAttacker()
		{
			if (m_nearestPlayer == null)
				return null;

			Vector3 playerPos = m_nearestPlayer.ComponentBody.Position;
			float checkRange = 15f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(playerPos.X, playerPos.Z), checkRange, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature != m_componentCreature)
				{
					// Verificar si esta criatura está atacando al jugador
					if (IsAttackingPlayer(creature, m_nearestPlayer))
						return creature;

					// Verificar si está muy cerca y mirando al jugador (posible atacante)
					float distToPlayer = Vector3.Distance(creature.ComponentBody.Position, playerPos);
					if (distToPlayer < 5f)
					{
						Vector3 toPlayer = Vector3.Normalize(playerPos - creature.ComponentBody.Position);
						float dot = Vector3.Dot(creature.ComponentBody.Matrix.Forward, toPlayer);
						if (dot > 0.7f) // Está mirando al jugador
						{
							return creature;
						}
					}
				}
			}

			return null;
		}

		private void SetupEventHooks()
		{
			if (m_componentCreature?.ComponentBody != null)
			{
				ComponentBody componentBody = m_componentCreature.ComponentBody;
				componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(
					componentBody.CollidedWithBody,
					new Action<ComponentBody>(OnCollidedWithBody));
			}

			if (m_componentHealth != null)
			{
				m_componentHealth.Injured = (Action<Injury>)Delegate.Combine(
					m_componentHealth.Injured,
					new Action<Injury>(OnInjured));
			}
		}

		private void SetupStateMachine()
		{
			m_stateMachine.AddState("LookingForTarget", LookingForTarget_Enter, LookingForTarget_Update, null);
			m_stateMachine.AddState("RandomMoving", RandomMoving_Enter, RandomMoving_Update, RandomMoving_Leave);
			m_stateMachine.AddState("Chasing", Chasing_Enter, Chasing_Update, null);
		}

		private void LookingForTarget_Enter()
		{
			m_importanceLevel = 0f;
			m_target = null;
		}

		private void LookingForTarget_Update()
		{
			if (IsActive)
			{
				m_stateMachine.TransitionTo("Chasing");
				return;
			}

			if (Suppressed || m_autoChaseSuppressionTime > 0f)
				return;

			if (m_target != null && ScoreTarget(m_target) > 0f)
				return;

			if (m_componentCreature.ComponentHealth.Health <= 0.1f)
				return;

			m_range = (m_subsystemSky.SkyLightIntensity < 0.2f) ? m_nightChaseRange : m_dayChaseRange;
			m_range *= m_componentFactors?.GetOtherFactorResult("ChaseRange", false, false) ?? 1f;

			ComponentCreature potentialTarget = FindTarget();
			if (potentialTarget != null)
				m_targetInRangeTime += m_dt;
			else
				m_targetInRangeTime = 0f;

			if (m_targetInRangeTime > 1.5f)
			{
				bool isDay = m_subsystemSky.SkyLightIntensity >= 0.1f;
				float maxRange = isDay ? (m_dayChaseRange + 6f) : (m_nightChaseRange + 6f);
				float maxChaseTime = isDay ? (m_dayChaseTime * m_random.Float(0.75f, 1f)) : (m_nightChaseTime * m_random.Float(0.75f, 1f));
				Attack(potentialTarget, maxRange, maxChaseTime, !isDay);
			}
		}

		private void RandomMoving_Enter()
		{
			Vector3 offset = new Vector3(6f * m_random.Float(-1f, 1f), 0f, 6f * m_random.Float(-1f, 1f));
			m_componentPathfinding.SetDestination(m_componentCreature.ComponentBody.Position + offset, 1f, 1f, 0, false, true, false, null);
		}

		private void RandomMoving_Update()
		{
			if (m_componentPathfinding.IsStuck || m_componentPathfinding.Destination == null)
				m_stateMachine.TransitionTo("Chasing");

			if (!IsActive)
				m_stateMachine.TransitionTo("LookingForTarget");
		}

		private void RandomMoving_Leave()
		{
			m_componentPathfinding.Stop();
		}

		private void Chasing_Enter()
		{
			m_subsystemNoise.MakeNoise(m_componentCreature.ComponentBody, 0.25f, 6f);
			m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
			m_nextUpdateTime = 0.0;
		}

		private void Chasing_Update()
		{
			if (!IsActive)
			{
				m_stateMachine.TransitionTo("LookingForTarget");
				return;
			}

			if (m_chaseTime <= 0f)
			{
				m_autoChaseSuppressionTime = m_random.Float(10f, 60f);
				m_importanceLevel = 0f;
				return;
			}

			if (m_target == null)
			{
				m_importanceLevel = 0f;
				return;
			}

			if (m_target.ComponentHealth.Health <= 0f)
			{
				if (m_componentFeedBehavior != null)
				{
					m_subsystemTime.QueueGameTimeDelayedExecution(m_subsystemTime.GameTime + m_random.Float(1f, 3f), () =>
					{
						if (m_target != null)
							m_componentFeedBehavior.Feed(m_target.ComponentBody.Position);
					});
				}
				m_importanceLevel = 0f;
				return;
			}

			if (!m_isPersistent && m_componentPathfinding.IsStuck)
			{
				m_importanceLevel = 0f;
				return;
			}

			if (m_isPersistent && m_componentPathfinding.IsStuck)
			{
				m_stateMachine.TransitionTo("RandomMoving");
				return;
			}

			if (ScoreTarget(m_target) <= 0f)
				m_targetUnsuitableTime += m_dt;
			else
				m_targetUnsuitableTime = 0f;

			if (m_targetUnsuitableTime > 3f)
			{
				m_importanceLevel = 0f;
				return;
			}

			int maxPathfindingPositions = m_isPersistent ? (m_subsystemTime.FixedTimeStep != null ? 2000 : 500) : 0;
			Vector3 myCenter = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 targetCenter = m_target.ComponentBody.BoundingBox.Center();
			float distance = Vector3.Distance(myCenter, targetCenter);
			float followFactor = (distance < 4f) ? 0.2f : 0f;
			m_componentPathfinding.SetDestination(targetCenter + followFactor * distance * m_target.ComponentBody.Velocity,
				1f, 1.5f, maxPathfindingPositions, true, false, true, m_target.ComponentBody);

			if (m_random.Float(0f, 1f) < 0.33f * m_dt)
				m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
		}

		public override void Update(float dt)
		{
			bool wasNearDeath = m_isNearDeath;
			m_isNearDeath = (m_componentCreature.ComponentHealth.Health <= 0.2f);
			if (m_isNearDeath && !wasNearDeath)
				m_attackPersistanceFactor = 2f;
			else if (!m_isNearDeath && wasNearDeath)
				m_attackPersistanceFactor = 1f;

			UpdateExtremeProtection();

			if (m_switchTargetCooldown > 0f)
				m_switchTargetCooldown -= dt;

			if (Suppressed)
			{
				StopAttack();
			}

			m_autoChaseSuppressionTime -= dt;

			// Verificar eventos de ataque al jugador si somos aliados
			if (m_isPlayerAlly)
			{
				CheckPlayerAttackEvents();

				// Durante noche verde, mayor vigilancia
				if (m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive)
				{
					CheckHighAlertThreats();
				}

				// Verificar bandidos cercanos
				CheckNearbyBandits();
			}

			if (IsActive && m_target != null)
			{
				m_chaseTime -= dt;
				m_componentCreature.ComponentCreatureModel.LookAtOrder = m_target.ComponentCreatureModel.EyePosition;

				if (m_attackMode != AttackMode.OnlyHand && HasAimableWeapon())
				{
					HandleAiming(dt);
				}
				else
				{
					if (IsTargetInAttackRange(m_target.ComponentBody))
					{
						m_componentCreatureModel.AttackOrder = true;
						if (m_attackMode != AttackMode.OnlyHand)
							FindHitTool();
					}

					if (m_componentCreatureModel.IsAttackHitMoment)
					{
						Vector3 hitPoint;
						ComponentBody hitBody = GetHitBody(m_target.ComponentBody, out hitPoint);
						if (hitBody != null)
						{
							float extraTime = m_isPersistent ? m_random.Float(8f, 10f) : 2f;
							m_chaseTime = MathUtils.Max(m_chaseTime, extraTime);
							m_componentMiner.Hit(hitBody, hitPoint, m_componentCreature.ComponentBody.Matrix.Forward);
							m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
						}
					}
				}

				UpdateShootSound(dt);
			}

			if (m_subsystemTime.GameTime >= m_nextUpdateTime)
			{
				m_dt = m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(m_subsystemTime.GameTime - m_nextUpdateTime), 0.1f);
				m_nextUpdateTime = m_subsystemTime.GameTime + m_dt;
				m_stateMachine.Update();
			}

			if (m_switchTargetCooldown <= 0f)
			{
				ComponentCreature betterTarget = FindBetterTarget();
				if (betterTarget != null && betterTarget != m_target)
				{
					float chaseRange = m_subsystemSky.SkyLightIntensity < 0.2f ? m_nightChaseRange : m_dayChaseRange;
					float chaseTime = m_subsystemSky.SkyLightIntensity < 0.2f ? m_nightChaseTime : m_dayChaseTime;
					Attack(betterTarget, chaseRange, chaseTime * m_attackPersistanceFactor, true);
					m_switchTargetCooldown = 2f;
				}
			}

			if (m_extremeProtectionActive && m_target == null)
			{
				ScanForThreatsToPlayer();
			}
		}

		// Verificar bandidos cercanos
		private void CheckNearbyBandits()
		{
			// Solo si somos aliados del jugador
			if (!IsGuardianOrPlayerAlly())
				return;

			// Verificar cada cierto tiempo
			if (m_subsystemTime.GameTime < m_nextBanditCheckTime)
				return;

			m_nextBanditCheckTime = m_subsystemTime.GameTime + 1.0;

			float banditRange = 25f;
			Vector3 myPos = m_componentCreature.ComponentBody.Position;
			float rangeSquared = banditRange * banditRange;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == null || creature == m_componentCreature || creature.ComponentHealth.Health <= 0f)
					continue;

				if (!IsBandit(creature))
					continue;

				float distSquared = Vector3.DistanceSquared(myPos, creature.ComponentBody.Position);
				if (distSquared > rangeSquared)
					continue;

				if (m_target == null || m_target != creature)
				{
					Attack(creature, banditRange, 40f, true);

					if (m_componentNewHerd != null)
					{
						m_componentNewHerd.CallNearbyCreaturesHelp(creature, banditRange, 40f, false, true);
					}
					break;
				}
			}
		}

		// Verificar amenazas en alerta alta (noche verde)
		private void CheckHighAlertThreats()
		{
			// Solo si somos aliados del jugador
			if (!IsGuardianOrPlayerAlly())
				return;

			// Verificar cada 0.1 segundos durante noche verde
			if (m_subsystemTime.GameTime < m_nextHighAlertCheckTime)
				return;

			m_nextHighAlertCheckTime = m_subsystemTime.GameTime + 0.1;

			float highAlertRange = 40f;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player == null || player.ComponentHealth.Health <= 0f)
					continue;

				ComponentCreature threat = FindMostDangerousThreatForPlayer(player, highAlertRange);
				if (threat != null && (m_target == null || m_target != threat))
				{
					Attack(threat, highAlertRange, 60f, true);

					if (m_componentNewHerd != null)
					{
						m_componentNewHerd.CallNearbyCreaturesHelp(threat, highAlertRange, 60f, false, true);
					}
					return;
				}
			}
		}

		// Encontrar la amenaza más peligrosa para un jugador
		private ComponentCreature FindMostDangerousThreatForPlayer(ComponentPlayer player, float range)
		{
			if (player?.ComponentBody == null)
				return null;

			Vector3 playerPos = player.ComponentBody.Position;
			float rangeSquared = range * range;

			ComponentCreature mostDangerous = null;
			float highestThreat = 0f;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature == null || creature == m_componentCreature || creature == player || creature.ComponentHealth.Health <= 0f)
					continue;

				if (IsAlly(creature))
					continue;

				float distSquared = Vector3.DistanceSquared(playerPos, creature.ComponentBody.Position);
				if (distSquared > rangeSquared)
					continue;

				float threatLevel = CalculateThreatLevel(creature, player, distSquared);
				if (threatLevel > highestThreat)
				{
					highestThreat = threatLevel;
					mostDangerous = creature;
				}
			}

			return mostDangerous;
		}

		// Calcular nivel de amenaza
		private float CalculateThreatLevel(ComponentCreature creature, ComponentPlayer player, float distSquared)
		{
			float distance = (float)Math.Sqrt(distSquared);
			float threat = 100f / (distance + 1f);

			if (IsZombieOrInfected(creature))
				threat *= 1.5f;

			if (IsBandit(creature))
				threat *= 1.8f;

			if (IsAttackingPlayer(creature, player))
				threat += 200f;

			return threat;
		}

		private void UpdateShootSound(float dt)
		{
			if (m_lastShotSoundTime <= 0f)
			{
				bool isShooting = false;
				var field = typeof(ComponentMiner).GetField("m_hasDigOrder",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (field != null)
					isShooting = (bool)field.GetValue(m_componentMiner);

				if (isShooting && m_target != null)
				{
					m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
					m_lastShotSoundTime = 0.3f;
				}
			}
			else
			{
				m_lastShotSoundTime -= dt;
			}
		}

		private void UpdateExtremeProtection()
		{
			bool greenNight = m_subsystemGreenNightSky != null && m_subsystemGreenNightSky.IsGreenNightActive;
			if (!greenNight)
			{
				m_extremeProtectionActive = false;
				return;
			}

			if (m_componentNewHerd != null && !string.IsNullOrEmpty(m_componentNewHerd.HerdName))
			{
				string herd = m_componentNewHerd.HerdName.ToLower();
				if (herd == "player" || herd.Contains("guardian"))
				{
					m_extremeProtectionActive = true;
					return;
				}
			}

			if (Entity.FindComponent<ComponentPlayer>() != null)
			{
				m_extremeProtectionActive = true;
				return;
			}

			m_extremeProtectionActive = false;
		}

		private void ScanForThreatsToPlayer()
		{
			ComponentPlayer player = null;
			float playerDist = float.MaxValue;
			Vector3 myPos = m_componentCreature.ComponentBody.Position;

			foreach (var p in m_subsystemPlayers.ComponentPlayers)
			{
				if (p?.ComponentHealth.Health > 0f)
				{
					float d = Vector3.Distance(myPos, p.ComponentBody.Position);
					if (d < playerDist)
					{
						playerDist = d;
						player = p;
					}
				}
			}

			if (player == null) return;

			Vector3 playerPos = player.ComponentBody.Position;
			float threatRange = m_range * 1.5f;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(playerPos.X, playerPos.Z), threatRange, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature creature = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (creature != null && creature != m_componentCreature)
				{
					if (IsAlly(creature))
						continue;

					ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
					if (chase != null && chase.Target == player)
					{
						Attack(creature, m_range, m_chaseTime, true);
						break;
					}
				}
			}
		}

		private bool HasAimableWeapon()
		{
			if (m_componentMiner.Inventory == null)
				return false;

			int activeValue = m_componentMiner.ActiveBlockValue;
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(activeValue)];

			if (IsAimableWeapon(activeValue, block))
			{
				if (IsFlamethrower(block))
					return IsFlamethrowerReady(activeValue);
				return IsRangedWeaponReady(activeValue);
			}

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				Block b = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];

				if (IsAimableWeapon(slotValue, b))
				{
					if (IsFlamethrower(b))
					{
						if (IsFlamethrowerReady(slotValue))
						{
							m_componentMiner.Inventory.ActiveSlotIndex = i;
							return true;
						}
					}
					else if (IsRangedWeaponReady(slotValue))
					{
						m_componentMiner.Inventory.ActiveSlotIndex = i;
						return true;
					}
				}
			}

			return false;
		}

		private bool IsAimableWeapon(int value, Block block)
		{
			return block.IsAimable_(value) && block.GetCategory(value) != "Terrain";
		}

		private bool IsFlamethrower(Block block)
		{
			return block.GetType().Name == "FlameThrowerBlock";
		}

		private bool IsFlamethrowerReady(int value)
		{
			int data = Terrain.ExtractData(value);
			Type type = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetType();

			var loadStateProp = type.GetProperty("LoadState",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			var bulletTypeProp = type.GetProperty("BulletType",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

			if (loadStateProp != null && bulletTypeProp != null)
			{
				var loadState = loadStateProp.GetValue(null);
				var bulletType = bulletTypeProp.GetValue(null);

				if (loadState.ToString() == "Loaded" && bulletType != null)
					return true;
			}

			return TryReloadFlamethrower(value);
		}

		private bool TryReloadFlamethrower(int value)
		{
			if (m_componentMiner.Inventory == null)
				return false;

			for (int i = 0; i < m_componentMiner.Inventory.SlotsCount; i++)
			{
				int slotValue = m_componentMiner.Inventory.GetSlotValue(i);
				int contents = Terrain.ExtractContents(slotValue);
				Block block = BlocksManager.Blocks[contents];

				if (block.GetType().Name == "FlameBulletBlock")
				{
					int ammoData = Terrain.ExtractData(slotValue);
					int newValue = CreateFlamethrowerValueWithAmmo(value, ammoData);
					m_componentMiner.Inventory.RemoveSlotItems(i, 1);
					int activeSlot = m_componentMiner.Inventory.ActiveSlotIndex;
					m_componentMiner.Inventory.RemoveSlotItems(activeSlot, 1);
					m_componentMiner.Inventory.AddSlotItems(activeSlot, newValue, 1);
					return true;
				}
			}

			return false;
		}

		private int CreateFlamethrowerValueWithAmmo(int weaponValue, int ammoData)
		{
			int contents = Terrain.ExtractContents(weaponValue);
			int newData = ammoData & 0xF;
			newData |= 0x10;
			return Terrain.MakeBlockValue(contents, 0, newData);
		}

		private bool IsRangedWeaponReady(int value)
		{
			int data = Terrain.ExtractData(value);
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];

			if (block is BowBlock)
			{
				int draw = (data >> 4) & 0xF;
				int arrowType = data & 0xF;
				return draw >= 15 && arrowType != 0;
			}
			else if (block is CrossbowBlock)
			{
				int draw = (data >> 4) & 0xF;
				int boltType = data & 0xF;
				return draw >= 15 && boltType != 0;
			}
			else if (block.GetType().Name == "RepeatCrossbowBlock")
			{
				int draw = (data >> 4) & 0xF;
				int arrowType = data & 0xF;
				return draw >= 15 && arrowType != 0;
			}

			return true;
		}

		private bool FindHitTool()
		{
			if (m_componentMiner.Inventory == null)
				return false;

			int active = m_componentMiner.ActiveBlockValue;
			if (BlocksManager.Blocks[Terrain.ExtractContents(active)].GetMeleePower(active) > 1f)
				return true;

			float bestPower = 1f;
			int bestSlot = 0;

			for (int i = 0; i < Math.Min(6, m_componentMiner.Inventory.SlotsCount); i++)
			{
				int val = m_componentMiner.Inventory.GetSlotValue(i);
				float power = BlocksManager.Blocks[Terrain.ExtractContents(val)].GetMeleePower(val);
				if (power > bestPower)
				{
					bestPower = power;
					bestSlot = i;
				}
			}

			if (bestPower > 1f)
			{
				m_componentMiner.Inventory.ActiveSlotIndex = bestSlot;
				return true;
			}

			return false;
		}

		private void HandleAiming(float dt)
		{
			float actionDelay = (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;

			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetEye = m_target.ComponentCreatureModel.EyePosition;
			Vector3 direction = Vector3.Normalize(targetEye - eyePos);

			var bodyHit = RaycastBody(eyePos, direction, m_attackRange.Y);
			if (bodyHit == null)
				return;

			m_chaseTime = Math.Max(m_chaseTime, m_isPersistent ? m_random.Float(8f, 10f) : 2f);

			bool facing = Vector3.Dot(m_componentBody.Matrix.Forward, direction) > 0.9f;
			if (!facing)
				m_componentPathfinding.SetDestination(targetEye, 1f, 1f, 0, false, true, false, null);
			else
				m_componentPathfinding.Destination = null;

			string category = BlocksManager.Blocks[Terrain.ExtractContents(m_componentMiner.ActiveBlockValue)]
				.GetCategory(m_componentMiner.ActiveBlockValue);

			if (m_subsystemTime.GameTime - m_lastActionTime > actionDelay)
			{
				if (m_componentMiner.Use(new Ray3(eyePos, direction)))
					m_lastActionTime = m_subsystemTime.GameTime;
				else if (facing && category != "Terrain")
				{
					m_componentMiner.Aim(new Ray3(eyePos, direction), AimState.Completed);
					m_lastActionTime = m_subsystemTime.GameTime;
				}
			}
			else if (facing && category != "Terrain")
			{
				m_componentMiner.Aim(new Ray3(eyePos, direction), AimState.InProgress);
			}
		}

		private ComponentBody RaycastBody(Vector3 start, Vector3 direction, float reach)
		{
			Vector3 end = start + direction * reach;
			var result = m_subsystemBodies.Raycast(start, end, 0.35f, (body, dist) =>
				body.Entity != Entity && !body.IsChildOfBody(m_componentBody) && !m_componentBody.IsChildOfBody(body));

			return result?.ComponentBody;
		}

		private void OnInjured(Injury injury)
		{
			ComponentCreature attacker = injury.Attacker;
			if (attacker == null) return;

			// No atacar a aliados
			if (IsAlly(attacker))
				return;

			m_lastAttacker = attacker;
			m_lastAttackTime = m_subsystemTime.GameTime;

			if (m_componentNewHerd != null)
			{
				m_componentNewHerd.CallNearbyCreaturesHelp(attacker, 20f, 30f, false, true);
			}

			// Determinar si debe ser persistente
			bool isPersistent = (m_chaseWhenAttackedProbability >= 1f) || m_isNearDeath;
			float chaseRange = m_chaseWhenAttackedProbability >= 1f ? 30f : 7f;
			float chaseTime = m_chaseWhenAttackedProbability >= 1f ? 60f : 7f;

			if (m_isNearDeath)
			{
				chaseTime *= 2f;
				isPersistent = true;
			}

			// Si es bandido y somos aliados, respuesta mejorada
			if (IsGuardianOrPlayerAlly() && IsBandit(attacker))
			{
				chaseRange *= 1.3f;
				chaseTime *= 1.5f;
				isPersistent = true;
			}

			// ¡ATACAR AL AGRESOR!
			Attack(attacker, chaseRange, chaseTime, isPersistent);
		}

		private void OnCollidedWithBody(ComponentBody body)
		{
			if (m_target == null && m_autoChaseSuppressionTime <= 0f && m_random.Float(0f, 1f) < m_chaseOnTouchProbability)
			{
				ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					if (IsAlly(creature))
						return;

					bool isPlayer = m_subsystemPlayers.IsPlayer(body.Entity);
					bool isAutoChase = (creature.Category & m_autoChaseMask) != 0;

					if ((isPlayer && m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
						(!isPlayer && isAutoChase))
					{
						float chaseTime = 8f * (m_isNearDeath ? 2f : 1f);
						Attack(creature, 10f, chaseTime, false);
					}
				}
			}

			if (m_target != null && body == m_target.ComponentBody &&
				body.StandingOnBody == m_componentCreature.ComponentBody)
			{
				m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
			}
		}

		public override void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (componentCreature == null) return;

			// Verificar si podemos atacar según reglas de manada
			if (m_componentNewHerd != null)
			{
				if (!m_componentNewHerd.CanAttackCreature(componentCreature))
					return;
			}
			else
			{
				// Si no hay manada nueva, verificar con el sistema de alianzas
				if (IsAlly(componentCreature))
					return;
			}

			// Durante noche verde, comportamiento especial
			if (this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive)
			{
				if (IsZombieOrInfected(componentCreature))
				{
					maxChaseTime *= 1.5f;
					isPersistent = true;
				}
			}

			// Protección especial contra bandidos
			if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature))
			{
				maxRange *= 1.3f;
				maxChaseTime *= 1.5f;
				isPersistent = true;

				if (m_importanceLevel < 280f)
				{
					m_importanceLevel = 280f;
				}
			}

			if (m_isNearDeath)
			{
				maxChaseTime *= 2f;
				isPersistent = true;
				maxRange *= 1.2f;
			}

			if (m_extremeProtectionActive)
			{
				maxRange *= 1.5f;
				maxChaseTime *= 1.5f;
				isPersistent = true;
			}

			base.Attack(componentCreature, maxRange, maxChaseTime, isPersistent);

			if (m_isNearDeath || m_extremeProtectionActive)
			{
				m_importanceLevel = Math.Max(m_importanceLevel, 10f * 1.5f);
			}
		}

		public override void StopAttack()
		{
			if (!m_isNearDeath && !m_extremeProtectionActive)
			{
				base.StopAttack();
			}
		}

		public override ComponentCreature FindTarget()
		{
			if (m_extremeProtectionActive)
			{
				ComponentCreature threat = FindThreatToPlayer();
				if (threat != null)
					return threat;
			}

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			ComponentCreature best = null;
			float bestScore = 0f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(pos.X, pos.Z), m_range, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature c = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (c != null)
				{
					float score = ScoreTarget(c);
					if (score > bestScore)
					{
						bestScore = score;
						best = c;
					}
				}
			}

			// Priorizar al último atacante
			if (m_lastAttacker != null && m_lastAttacker.ComponentHealth.Health > 0f)
			{
				float timeSince = (float)(m_subsystemTime.GameTime - m_lastAttackTime);
				if (timeSince <= AttackerMemoryDuration)
				{
					return m_lastAttacker;
				}
			}

			if (m_isPlayerAlly && m_lastPlayerAttacker != null && m_lastPlayerAttacker.ComponentHealth.Health > 0f)
			{
				float timeSince = (float)(m_subsystemTime.GameTime - m_lastPlayerAttackCheckTime);
				if (timeSince <= AttackerMemoryDuration)
				{
					return m_lastPlayerAttacker;
				}
			}

			return best;
		}

		private ComponentCreature FindThreatToPlayer()
		{
			ComponentPlayer player = null;
			float playerDist = float.MaxValue;
			Vector3 myPos = m_componentCreature.ComponentBody.Position;

			foreach (var p in m_subsystemPlayers.ComponentPlayers)
			{
				if (p?.ComponentHealth.Health > 0f)
				{
					float d = Vector3.Distance(myPos, p.ComponentBody.Position);
					if (d < playerDist)
					{
						playerDist = d;
						player = p;
					}
				}
			}

			if (player == null) return null;

			Vector3 playerPos = player.ComponentBody.Position;
			float threatRange = m_range * 1.2f;
			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(playerPos.X, playerPos.Z), threatRange, m_componentBodies);

			ComponentCreature bestThreat = null;
			float bestScore = 0f;

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature c = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (c != null && c != m_componentCreature)
				{
					if (IsAlly(c))
						continue;

					ComponentChaseBehavior chase = c.Entity.FindComponent<ComponentChaseBehavior>();
					if (chase != null && chase.Target == player)
						return c;

					float dist = Vector3.Distance(playerPos, c.ComponentBody.Position);
					if (dist < threatRange)
					{
						float score = threatRange - dist;
						if (score > bestScore)
						{
							bestScore = score;
							bestThreat = c;
						}
					}
				}
			}

			return bestThreat;
		}

		private ComponentCreature FindBetterTarget()
		{
			if (m_target == null) return null;

			Vector3 pos = m_componentCreature.ComponentBody.Position;
			ComponentCreature best = m_target;
			float bestScore = ScoreTarget(m_target);
			float searchRange = m_range * 1.5f;

			m_componentBodies.Clear();
			m_subsystemBodies.FindBodiesAroundPoint(new Vector2(pos.X, pos.Z), searchRange, m_componentBodies);

			for (int i = 0; i < m_componentBodies.Count; i++)
			{
				ComponentCreature c = m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
				if (c != null && c != m_target)
				{
					float score = ScoreTarget(c);
					if (score > bestScore * 1.2f)
					{
						bestScore = score;
						best = c;
					}
				}
			}

			return best != m_target ? best : null;
		}

		public override float ScoreTarget(ComponentCreature componentCreature)
		{
			// No considerar a los aliados
			if (IsAlly(componentCreature))
				return 0f;

			// Verificar reglas de manada
			if (m_componentNewHerd != null)
			{
				if (!m_componentNewHerd.CanAttackCreature(componentCreature))
					return 0f;
			}

			float score = 0f;
			bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
			bool isTerrestrial = m_componentCreature.Category != CreatureCategory.WaterPredator &&
								m_componentCreature.Category != CreatureCategory.WaterOther;

			bool isPlayerTarget = componentCreature == m_target ||
								 (m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless);
			bool inAutoMask = (componentCreature.Category & m_autoChaseMask) != 0;
			bool isNonPlayerTarget = componentCreature == m_target ||
									(inAutoMask && MathUtils.Remainder(0.005 * m_subsystemTime.GameTime +
									 (GetHashCode() % 1000) / 1000.0 +
									 (componentCreature.GetHashCode() % 1000) / 1000.0, 1.0) < m_chaseNonPlayerProbability);

			if (componentCreature != m_componentCreature &&
				((!isPlayer && isNonPlayerTarget) || (isPlayer && isPlayerTarget)) &&
				componentCreature.Entity.IsAddedToProject &&
				componentCreature.ComponentHealth.Health > 0f &&
				(isTerrestrial || IsTargetInWater(componentCreature.ComponentBody)))
			{
				float dist = Vector3.Distance(m_componentCreature.ComponentBody.Position, componentCreature.ComponentBody.Position);
				if (dist < m_range)
					score = m_range - dist;
			}

			// Prioridad máxima al último atacante
			if (componentCreature == m_lastAttacker)
			{
				float timeSince = (float)(m_subsystemTime.GameTime - m_lastAttackTime);
				if (timeSince <= AttackerMemoryDuration)
				{
					return 100000f;
				}
			}

			if (m_isPlayerAlly && componentCreature == m_lastPlayerAttacker)
			{
				float timeSince = (float)(m_subsystemTime.GameTime - m_lastPlayerAttackCheckTime);
				if (timeSince <= AttackerMemoryDuration)
				{
					return 100000f;
				}
			}

			// Bonus para bandidos si somos aliados
			if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature))
			{
				score *= 1.5f;
			}

			if (m_extremeProtectionActive && !isPlayer)
			{
				ComponentChaseBehavior chase = componentCreature.Entity.FindComponent<ComponentChaseBehavior>();
				if (chase != null && chase.Target != null && chase.Target.Entity.FindComponent<ComponentPlayer>() != null)
					score *= 3f;
			}

			if (m_isNearDeath)
				score *= 1.5f;

			return score;
		}

		public virtual bool IsTargetInWater(ComponentBody target)
		{
			return target.ImmersionDepth > 0f ||
				   (target.ParentBody != null && IsTargetInWater(target.ParentBody)) ||
				   (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y &&
					IsTargetInWater(target.StandingOnBody));
		}

		public virtual bool IsTargetInAttackRange(ComponentBody target)
		{
			if (IsBodyInAttackRange(target))
				return true;

			BoundingBox bb = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox tb = target.BoundingBox;
			Vector3 myCenter = 0.5f * (bb.Min + bb.Max);
			Vector3 targetCenter = 0.5f * (tb.Min + tb.Max);
			Vector3 delta = targetCenter - myCenter;
			float dist = delta.Length();
			Vector3 dir = delta / dist;
			float halfWidth = 0.5f * (bb.Max.X - bb.Min.X + tb.Max.X - tb.Min.X);
			float halfHeight = 0.5f * (bb.Max.Y - bb.Min.Y + tb.Max.Y - tb.Min.Y);

			if (MathF.Abs(delta.Y) < halfHeight * 0.99f)
			{
				if (dist < halfWidth + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (dist < halfHeight + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
				return true;

			return (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody)) ||
				   (target.StandingOnBody != null &&
					target.StandingOnBody.Position.Y < target.Position.Y &&
					IsTargetInAttackRange(target.StandingOnBody));
		}

		public virtual bool IsBodyInAttackRange(ComponentBody target)
		{
			BoundingBox bb = m_componentCreature.ComponentBody.BoundingBox;
			BoundingBox tb = target.BoundingBox;
			Vector3 myCenter = 0.5f * (bb.Min + bb.Max);
			Vector3 targetCenter = 0.5f * (tb.Min + tb.Max);
			Vector3 delta = targetCenter - myCenter;
			float dist = delta.Length();
			Vector3 dir = delta / dist;
			float halfWidth = 0.5f * (bb.Max.X - bb.Min.X + tb.Max.X - tb.Min.X);
			float halfHeight = 0.5f * (bb.Max.Y - bb.Min.Y + tb.Max.Y - tb.Min.Y);

			if (MathF.Abs(delta.Y) < halfHeight * 0.99f)
			{
				if (dist < halfWidth + 0.99f && Vector3.Dot(dir, m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
					return true;
			}
			else if (dist < halfHeight + 0.3f && MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.8f)
				return true;

			return false;
		}

		public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
		{
			Vector3 myCenter = m_componentCreature.ComponentBody.BoundingBox.Center();
			Vector3 targetCenter = target.BoundingBox.Center();
			Ray3 ray = new Ray3(myCenter, Vector3.Normalize(targetCenter - myCenter));

			var result = m_componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);

			if (result != null && result.Value.Distance < 5f &&
				(result.Value.ComponentBody == target || result.Value.ComponentBody.IsChildOfBody(target) ||
				 target.IsChildOfBody(result.Value.ComponentBody) ||
				 (target.StandingOnBody == result.Value.ComponentBody)))
			{
				hitPoint = result.Value.HitPoint();
				return result.Value.ComponentBody;
			}

			hitPoint = default;
			return null;
		}

		public override void OnEntityRemoved()
		{
			UnsubscribeFromPlayerEvents();
			base.OnEntityRemoved();
		}
	}
}
