using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentInfiniteChallenge : Component, IUpdateable
	{
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_infiniteCreature;
		private ComponentPlayer m_challenger;

		private enum ChallengeState
		{
			Idle,
			WaitingResponse,
			Countdown,
			DuelInProgress,
			Finished
		}

		private ChallengeState m_state = ChallengeState.Idle;
		private bool m_hasBeenDefeated = false;

		// Contador
		private int m_countdownValue = 5;
		private double m_countdownTimer = 0;

		// Valores originales para restaurar
		private string m_originalHerdName;
		private bool m_originalAttacksPlayer;
		private bool m_originalAttacksNonPlayerCreature;
		private float m_originalAttackResilienceFactor;
		private float m_originalAttackPower;
		private float m_originalWalkSpeed;
		private float m_originalFlySpeed;
		private bool m_originalSuppressed;
		private bool m_valuesSaved = false;

		// Componentes cacheados
		private ComponentNewHerdBehavior m_herd;
		private ComponentNewChaseBehavior m_newChase;
		private ComponentChaseBehavior m_baseChase;
		private ComponentHealth m_health;
		private ComponentMiner m_miner;
		private ComponentLocomotion m_locomotion;

		// Multiplicadores
		public float BossHealthMultiplier = 3f;
		public float BossDamageMultiplier = 2.5f;
		public float BossSpeedMultiplier = 1.4f;
		public float VictoryHealthThreshold = 0.1f;

		private const double CountdownInterval = 1.0;
		private const float NumberDisplayDuration = 0.9f;
		private const float NumberDisplayDelay = 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public bool HasBeenDefeated => m_hasBeenDefeated;

		public void StartChallenge(ComponentPlayer player)
		{
			if (m_state != ChallengeState.Idle || m_hasBeenDefeated) return;

			m_challenger = player;
			m_state = ChallengeState.WaitingResponse;

			InfiniteChallengeWidget.Show(player, OnChallengeResponse);
		}

		private void OnChallengeResponse(bool accepted)
		{
			if (m_state != ChallengeState.WaitingResponse) return;

			if (!accepted)
			{
				m_state = ChallengeState.Idle;
				return;
			}

			StartCountdown();
		}

		private void StartCountdown()
		{
			m_state = ChallengeState.Countdown;
			m_countdownValue = 5;
			m_countdownTimer = 0;

			LockInventory();
			DisplayCountdownNumber(m_countdownValue);
		}

		private void DisplayCountdownNumber(int number)
		{
			if (m_challenger == null) return;

			m_challenger.ComponentGui.DisplayLargeMessage(
				number.ToString(),
				string.Empty,
				NumberDisplayDuration,
				NumberDisplayDelay);
		}

		private void UpdateCountdown(float dt)
		{
			m_countdownTimer += dt;

			while (m_countdownTimer >= CountdownInterval)
			{
				m_countdownTimer -= CountdownInterval;
				m_countdownValue--;

				if (m_countdownValue <= 0)
				{
					StartDuel();
					return;
				}

				DisplayCountdownNumber(m_countdownValue);
			}
		}

		private void CacheComponents()
		{
			m_herd = m_infiniteCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
			m_newChase = m_infiniteCreature.Entity.FindComponent<ComponentNewChaseBehavior>();
			// NewChaseBehavior hereda de ChaseBehavior, así que obtenemos la misma instancia
			// pero podemos llamar a los métodos de la clase base
			m_baseChase = m_infiniteCreature.Entity.FindComponent<ComponentChaseBehavior>();
			m_health = m_infiniteCreature.ComponentHealth;
			m_miner = m_infiniteCreature.Entity.FindComponent<ComponentMiner>();
			m_locomotion = m_infiniteCreature.ComponentLocomotion;
		}

		private void SaveOriginalValues()
		{
			if (m_valuesSaved) return;

			if (m_herd != null)
				m_originalHerdName = m_herd.HerdName;

			if (m_newChase != null)
			{
				m_originalAttacksPlayer = m_newChase.AttacksPlayer;
				m_originalAttacksNonPlayerCreature = m_newChase.AttacksNonPlayerCreature;
				m_originalSuppressed = m_newChase.Suppressed;
			}

			if (m_health != null)
				m_originalAttackResilienceFactor = m_health.AttackResilienceFactor;

			if (m_miner != null)
				m_originalAttackPower = m_miner.AttackPower;

			if (m_locomotion != null)
			{
				m_originalWalkSpeed = m_locomotion.WalkSpeed;
				m_originalFlySpeed = m_locomotion.FlySpeed;
			}

			m_valuesSaved = true;
		}

		private void StartDuel()
		{
			m_state = ChallengeState.DuelInProgress;

			if (m_challenger == null || m_infiniteCreature == null)
			{
				EndDuel(false);
				return;
			}

			CacheComponents();
			SaveOriginalValues();

			// 1. Cambiar HerdName para que NO sea aliado durante el duelo
			// Esto permite que los sistemas de chase ataquen al jugador
			if (m_herd != null)
			{
				m_herd.HerdName = "duel_enemy";
			}

			// 2. Aplicar stats de jefe
			ApplyBossStats();
			SetAllyAttackBlock(true);

			// 3. Mostrar mensaje de inicio del duelo
			m_challenger.ComponentGui.DisplaySmallMessage(
				"The duel against Infinite has begun!",
				Color.Yellow,
				false,
				true);

			// 4. Usar el VIEJO ComponentChaseBehavior para forzar la persecución
			// La firma base tiene 4 parámetros (no 5 como NewChaseBehavior)
			if (m_baseChase != null)
			{
				// Asegurar que no esté suprimido
				m_baseChase.Suppressed = false;
				// Forzar que ataque al jugador
				m_baseChase.AttacksPlayer = true;
				m_baseChase.AttacksNonPlayerCreature = false;
				// Iniciar persecución persistente (no se detiene hasta que el duelo termine)
				m_baseChase.Attack(m_challenger, 50f, 600f, true);
			}
		}

		private void EndDuel(bool playerWon)
		{
			if (m_state == ChallengeState.Finished) return;
			m_state = ChallengeState.Finished;

			// 1. Detener toda persecución inmediatamente
			if (m_baseChase != null)
			{
				m_baseChase.StopAttack();
			}

			if (m_newChase != null)
			{
				m_newChase.StopAttack();
			}

			// 2. Restaurar HerdName a "player" (se une a la manada al ganar)
			if (m_herd != null)
			{
				// Si ganó, asegurarse de que esté en "player"
				// Si perdió, restaurar al valor original (que ya era "player" según el XML)
				m_herd.HerdName = playerWon ? "player" : (m_originalHerdName ?? "player");
			}

			// 3. Restaurar configuración de NewChaseBehavior
			if (m_newChase != null)
			{
				m_newChase.AttacksPlayer = m_originalAttacksPlayer;
				m_newChase.AttacksNonPlayerCreature = m_originalAttacksNonPlayerCreature;
				m_newChase.Suppressed = m_originalSuppressed;
			}

			// 4. Restaurar AttacksPlayer del ChaseBehavior base
			if (m_baseChase != null)
			{
				m_baseChase.AttacksPlayer = m_originalAttacksPlayer;
				m_baseChase.AttacksNonPlayerCreature = m_originalAttacksNonPlayerCreature;
				m_baseChase.Suppressed = m_originalSuppressed;
			}

			// 5. Resultado del duelo
			if (playerWon)
			{
				m_hasBeenDefeated = true;
				m_challenger?.ComponentGui?.DisplaySmallMessage(
					"You have proven your strength! Infinite now joins your family.",
					new Color(100, 255, 100),
					false,
					true);
			}
			else
			{
				// Si perdió, restaurar stats originales
				RestoreOriginalStats();
			}

			// 6. Limpiar
			UnlockInventory();
			SetAllyAttackBlock(false);
		}

		private void LockInventory()
		{
			if (m_challenger == null) return;
			InventoryBlocker.LockForPlayer(m_challenger, true);
		}

		private void UnlockInventory()
		{
			if (m_challenger == null) return;
			InventoryBlocker.LockForPlayer(m_challenger, false);
		}

		private void ApplyBossStats()
		{
			if (m_infiniteCreature == null) return;

			if (m_health != null)
			{
				m_health.AttackResilienceFactor = m_originalAttackResilienceFactor * BossHealthMultiplier;
			}

			if (m_miner != null)
			{
				m_miner.AttackPower = m_originalAttackPower * BossDamageMultiplier;
			}

			if (m_locomotion != null)
			{
				m_locomotion.WalkSpeed = m_originalWalkSpeed * BossSpeedMultiplier;
				m_locomotion.FlySpeed = m_originalFlySpeed * BossSpeedMultiplier;
			}
		}

		private void RestoreOriginalStats()
		{
			if (!m_valuesSaved || m_infiniteCreature == null) return;

			if (m_health != null)
			{
				m_health.AttackResilienceFactor = m_originalAttackResilienceFactor;
			}

			if (m_miner != null)
			{
				m_miner.AttackPower = m_originalAttackPower;
			}

			if (m_locomotion != null)
			{
				m_locomotion.WalkSpeed = m_originalWalkSpeed;
				m_locomotion.FlySpeed = m_originalFlySpeed;
			}
		}

		private void SetAllyAttackBlock(bool block)
		{
			if (block)
				BossFightBlocker.BlockAttacksOnCreature(m_infiniteCreature);
			else
				BossFightBlocker.UnblockAttacksOnCreature(m_infiniteCreature);
		}

		public void Update(float dt)
		{
			switch (m_state)
			{
				case ChallengeState.Countdown:
					UpdateCountdown(dt);
					break;

				case ChallengeState.DuelInProgress:
					UpdateDuel();
					break;
			}
		}

		private void UpdateDuel()
		{
			// Verificar si el jugador murió
			if (m_challenger != null && m_challenger.ComponentHealth.Health <= 0f)
			{
				EndDuel(false);
				return;
			}

			// Verificar si Infinite fue derrotado (salud baja al umbral)
			if (m_infiniteCreature != null && m_infiniteCreature.ComponentHealth.Health <= VictoryHealthThreshold)
			{
				EndDuel(true);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_infiniteCreature = Entity.FindComponent<ComponentCreature>(true);

			BossHealthMultiplier = valuesDictionary.GetValue<float>("BossHealthMultiplier", BossHealthMultiplier);
			BossDamageMultiplier = valuesDictionary.GetValue<float>("BossDamageMultiplier", BossDamageMultiplier);
			BossSpeedMultiplier = valuesDictionary.GetValue<float>("BossSpeedMultiplier", BossSpeedMultiplier);
			VictoryHealthThreshold = valuesDictionary.GetValue<float>("VictoryHealthThreshold", VictoryHealthThreshold);
			m_hasBeenDefeated = valuesDictionary.GetValue<bool>("HasBeenDefeated", false);

			// Si ya fue derrotado, asegurarse de que esté en la manada del jugador
			if (m_hasBeenDefeated)
			{
				var herd = Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herd != null)
				{
					herd.HerdName = "player";
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<bool>("HasBeenDefeated", m_hasBeenDefeated);
		}

		public override void Dispose()
		{
			if (m_state == ChallengeState.Countdown || m_state == ChallengeState.DuelInProgress)
			{
				// Forzar fin del duelo sin victoria
				if (m_baseChase != null)
					m_baseChase.StopAttack();
				if (m_newChase != null)
					m_newChase.StopAttack();

				// Restaurar HerdName
				if (m_herd != null)
					m_herd.HerdName = m_originalHerdName ?? "player";

				// Restaurar NewChase
				if (m_newChase != null)
				{
					m_newChase.AttacksPlayer = m_originalAttacksPlayer;
					m_newChase.AttacksNonPlayerCreature = m_originalAttacksNonPlayerCreature;
					m_newChase.Suppressed = m_originalSuppressed;
				}

				// Restaurar stats
				if (!m_hasBeenDefeated)
					RestoreOriginalStats();

				UnlockInventory();
				SetAllyAttackBlock(false);
			}
			base.Dispose();
		}
	}

	public static class InventoryBlocker
	{
		private static System.Collections.Generic.Dictionary<ComponentPlayer, bool> s_lockedPlayers =
			new System.Collections.Generic.Dictionary<ComponentPlayer, bool>();

		public static void LockForPlayer(ComponentPlayer player, bool locked)
		{
			if (locked)
				s_lockedPlayers[player] = true;
			else
				s_lockedPlayers.Remove(player);
		}

		public static bool IsInventoryLocked(ComponentPlayer player)
		{
			return s_lockedPlayers.ContainsKey(player);
		}
	}

	public static class BossFightBlocker
	{
		private static System.Collections.Generic.HashSet<ComponentCreature> s_blockedCreatures =
			new System.Collections.Generic.HashSet<ComponentCreature>();

		public static void BlockAttacksOnCreature(ComponentCreature creature)
		{
			s_blockedCreatures.Add(creature);
		}

		public static void UnblockAttacksOnCreature(ComponentCreature creature)
		{
			s_blockedCreatures.Remove(creature);
		}

		public static bool IsAttackBlocked(ComponentCreature creature)
		{
			return s_blockedCreatures.Contains(creature);
		}
	}
}
