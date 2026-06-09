using System;
using System.Collections.Generic;
using System.Reflection;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentInfiniteChallenge : Component, IUpdateable
	{
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_infiniteCreature;

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

		private int m_countdownValue = 5;
		private double m_countdownTimer = 0;

		private string m_originalHerdName;
		private bool m_originalAttacksPlayer;
		private bool m_originalAttacksNonPlayerCreature;
		private bool m_originalSuppressedNewChase;
		private bool m_originalSuppressedBaseChase;
		private float m_originalAttackResilienceFactor;
		private float m_originalAttackPower;
		private float m_originalWalkSpeed;
		private float m_originalFlySpeed;
		private bool m_valuesSaved = false;

		private ComponentNewHerdBehavior m_herd;
		private ComponentNewChaseBehavior m_newChase;
		private ComponentChaseBehavior m_baseChase;
		private ComponentHealth m_health;
		private ComponentMiner m_miner;
		private ComponentLocomotion m_locomotion;

		private Dictionary<ComponentCreature, bool> m_alliesOriginalSuppressed = new Dictionary<ComponentCreature, bool>();

		public float BossHealthMultiplier = 3f;
		public float BossDamageMultiplier = 2.5f;
		public float BossSpeedMultiplier = 1.4f;
		public float VictoryHealthThreshold = 0.1f;

		private const double CountdownInterval = 1.0;
		private const float NumberDisplayDuration = 0.9f;
		private const float NumberDisplayDelay = 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;
		public bool HasBeenDefeated => m_hasBeenDefeated;
		public bool IsDuelActive => m_state == ChallengeState.Countdown || m_state == ChallengeState.DuelInProgress;

		private ComponentPlayer m_challenger;

		// ===== FLAGS para restaurar en primer Update después de Load =====
		private bool m_needsPostLoadCleanup = false;

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
				m_state = ChallengeState.Idle;  // Back to Idle (NOT Idle!)

				m_challenger = null;    // ← FIX: Clear reference!

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
				m_originalSuppressedNewChase = m_newChase.Suppressed;
			}

			if (m_baseChase != null)
			{
				if (m_newChase == null)
				{
					m_originalAttacksPlayer = m_baseChase.AttacksPlayer;
					m_originalAttacksNonPlayerCreature = m_baseChase.AttacksNonPlayerCreature;
				}
				m_originalSuppressedBaseChase = m_baseChase.Suppressed;
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

		private void ForceImmediateChase()
		{
			if (m_baseChase == null) return;

			try
			{
				FieldInfo targetInRangeField = typeof(ComponentChaseBehavior).GetField("m_targetInRangeTime",
					BindingFlags.Instance | BindingFlags.NonPublic);
				if (targetInRangeField != null)
					targetInRangeField.SetValue(m_baseChase, 10f);

				FieldInfo stateMachineField = typeof(ComponentChaseBehavior).GetField("m_stateMachine",
					BindingFlags.Instance | BindingFlags.NonPublic);
				if (stateMachineField != null)
				{
					StateMachine stateMachine = stateMachineField.GetValue(m_baseChase) as StateMachine;
					if (stateMachine != null && stateMachine.CurrentState == "LookingForTarget")
					{
						MethodInfo transitionMethod = typeof(StateMachine).GetMethod("TransitionTo",
							BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						transitionMethod?.Invoke(stateMachine, new object[] { "Chasing" });
					}
				}

				MethodInfo updateMethod = typeof(ComponentChaseBehavior).GetMethod("Update",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				updateMethod?.Invoke(m_baseChase, new object[] { 0.033f });
			}
			catch (Exception ex)
			{
				Log.Warning($"[InfiniteChallenge] Error al forzar persecución inmediata: {ex.Message}");
			}
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

			if (m_herd != null)
				m_herd.HerdName = "duel_enemy";

			ApplyBossStats();
			SetAllyAttackBlock(true);
			DisableAlliesChase();

			string duelStartMsg = LanguageControl.Get("ComponentInfiniteChallenge", 0);
			m_challenger.ComponentGui.DisplaySmallMessage(
				duelStartMsg,
				Color.Yellow,
				false,
				true);

			if (m_baseChase != null)
			{
				m_baseChase.Suppressed = false;
				m_baseChase.AttacksPlayer = true;
				m_baseChase.AttacksNonPlayerCreature = false;
				m_baseChase.Attack(m_challenger, 50f, 600f, true);
				ForceImmediateChase();
			}

			if (m_newChase != null)
			{
				m_newChase.Suppressed = false;
				m_newChase.AttacksPlayer = true;
				m_newChase.AttacksNonPlayerCreature = false;
				m_newChase.Attack(m_challenger, 50f, 600f, true);
			}
		}

		private void EndDuel(bool playerWon)
		{
			if (m_state == ChallengeState.Finished) return;

			// Detener ataques de forma normal (el juego está corriendo)
			if (m_baseChase != null)
				m_baseChase.StopAttack();
			if (m_newChase != null)
				m_newChase.StopAttack();

			RestoreChaseBehavior();
			RestoreAlliesChase();
			UnlockInventory();
			SetAllyAttackBlock(false);

			if (playerWon)
			{
				m_hasBeenDefeated = true;

				if (m_herd != null)
					m_herd.HerdName = "player";

				string victoryMsg = LanguageControl.Get("ComponentInfiniteChallenge", 1);
				m_challenger?.ComponentGui?.DisplaySmallMessage(
					victoryMsg,
					new Color(100, 255, 100),
					false,
					true);

				// ========== NUEVO: Desbloquear logro por derrotar a Infinite ==========
				if (m_challenger != null)
				{
					string achievementTitle = LanguageControl.Get(AchievementsWidget.fName, 134);
					AchievementsManager.UnlockAchievementStatic(m_challenger, 69, "DefeatInfinite", achievementTitle);
				}

				m_state = ChallengeState.Finished;
			}
			else
			{
				RestoreOriginalStats();

				if (m_herd != null)
					m_herd.HerdName = m_originalHerdName ?? "player";

				m_state = ChallengeState.Idle;
			}

			m_challenger = null;
		}

		private void RestoreChaseBehavior()
		{
			if (m_baseChase != null && m_valuesSaved)
			{
				m_baseChase.AttacksPlayer = m_originalAttacksPlayer;
				m_baseChase.AttacksNonPlayerCreature = m_originalAttacksNonPlayerCreature;
				m_baseChase.Suppressed = m_originalSuppressedBaseChase;
			}

			if (m_newChase != null && m_valuesSaved)
			{
				m_newChase.AttacksPlayer = m_originalAttacksPlayer;
				m_newChase.AttacksNonPlayerCreature = m_originalAttacksNonPlayerCreature;
				m_newChase.Suppressed = m_originalSuppressedNewChase;
			}
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
				m_health.AttackResilienceFactor = m_originalAttackResilienceFactor * BossHealthMultiplier;
			if (m_miner != null)
				m_miner.AttackPower = m_originalAttackPower * BossDamageMultiplier;
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
				m_health.AttackResilienceFactor = m_originalAttackResilienceFactor;
			if (m_miner != null)
				m_miner.AttackPower = m_originalAttackPower;
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

		private void DisableAlliesChase()
		{
			if (Project == null) return;
			var creatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			if (creatureSpawn == null) return;

			m_alliesOriginalSuppressed.Clear();

			foreach (ComponentCreature creature in creatureSpawn.Creatures)
			{
				if (creature == null || creature == m_infiniteCreature) continue;
				if (creature.Entity.FindComponent<ComponentPlayer>() != null) continue;

				bool isAlly = false;
				ComponentNewHerdBehavior herd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
				if (herd != null && !string.IsNullOrEmpty(herd.HerdName))
				{
					string herdName = herd.HerdName.ToLower();
					if (herdName == "player" || herdName.Contains("guardian"))
						isAlly = true;
				}
				ComponentHireableNPC hireable = creature.Entity.FindComponent<ComponentHireableNPC>();
				if (hireable != null && hireable.IsHired)
					isAlly = true;

				if (!isAlly) continue;

				ComponentNewChaseBehavior chase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (chase != null)
				{
					m_alliesOriginalSuppressed[creature] = chase.Suppressed;
					chase.Suppressed = true;
					chase.StopAttack();
				}
				ComponentChaseBehavior baseChase = creature.Entity.FindComponent<ComponentChaseBehavior>();
				if (baseChase != null)
				{
					if (!m_alliesOriginalSuppressed.ContainsKey(creature))
						m_alliesOriginalSuppressed[creature] = baseChase.Suppressed;
					baseChase.Suppressed = true;
					baseChase.StopAttack();
				}
			}
		}

		private void RestoreAlliesChase()
		{
			if (Project == null) return;
			foreach (var kvp in m_alliesOriginalSuppressed)
			{
				ComponentCreature creature = kvp.Key;
				if (creature == null || creature.Entity == null) continue;
				ComponentNewChaseBehavior chase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
				if (chase != null)
					chase.Suppressed = kvp.Value;
				ComponentChaseBehavior baseChase = creature.Entity.FindComponent<ComponentChaseBehavior>();
				if (baseChase != null)
					baseChase.Suppressed = kvp.Value;
			}
			m_alliesOriginalSuppressed.Clear();
		}

		public void Update(float dt)
		{
			// ===== Limpieza post-Load: ejecutar UNA VEZ cuando el juego ya está corriendo =====
			if (m_needsPostLoadCleanup)
			{
				m_needsPostLoadCleanup = false;
				PerformPostLoadCleanup();
			}

			// --- NUEVO: Si estamos esperando respuesta pero el widget ya no está, cancelar ---
			if (m_state == ChallengeState.WaitingResponse && m_challenger != null)
			{
				Widget currentModal = m_challenger.ComponentGui.ModalPanelWidget;
				if (!(currentModal is InfiniteChallengeWidget))
				{
					// El widget fue cerrado sin respuesta (por otro modal o acción externa)
					OnChallengeResponse(false);
				}
			}

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

		/// <summary>
		/// Limpieza que se ejecuta en el primer Update() después del Load()
		/// Aquí es seguro llamar a StopAttack() porque el StateMachine ya está inicializado
		/// </summary>
		private void PerformPostLoadCleanup()
		{
			try
			{
				// Ahora sí es seguro llamar a StopAttack()
				if (m_baseChase != null)
				{
					m_baseChase.Suppressed = true;
					m_baseChase.StopAttack();
				}
				if (m_newChase != null)
				{
					m_newChase.Suppressed = true;
					m_newChase.StopAttack();
				}

				// Limpiar cualquier orden de vuelo residual
				if (m_locomotion != null)
				{
					m_locomotion.FlyOrder = null;
					m_locomotion.WalkOrder = null;
					m_locomotion.SwimOrder = null;
				}

				// Detener pathfinding
				ComponentPathfinding pathfinding = m_infiniteCreature?.Entity?.FindComponent<ComponentPathfinding>();
				if (pathfinding != null)
					pathfinding.Stop();

				Log.Warning("[InfiniteChallenge] Limpieza post-load completada");
			}
			catch (Exception ex)
			{
				Log.Warning($"[InfiniteChallenge] Error en limpieza post-load: {ex.Message}");
			}
		}

		private void UpdateDuel()
		{
			if (m_challenger != null && m_challenger.ComponentHealth.Health <= 0f)
			{
				EndDuel(false);
				return;
			}
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

			int savedState = valuesDictionary.GetValue<int>("ChallengeState", 0);
			m_state = (ChallengeState)savedState;

			// Cachear componentes
			CacheComponents();

			// ===== Si el duelo estaba activo, marcar para limpieza post-Load =====
			if (m_state == ChallengeState.Countdown || m_state == ChallengeState.DuelInProgress)
			{
				Log.Warning("[InfiniteChallenge] Duelo interrumpido por reload, se limpiará en primer Update()");

				// Solo forzar Suppressed (no tocar StateMachine ni locomoción)
				if (m_baseChase != null)
					m_baseChase.Suppressed = true;
				if (m_newChase != null)
					m_newChase.Suppressed = true;

				// Restaurar manada inmediatamente
				if (m_herd != null)
					m_herd.HerdName = "player";

				// Limpiar bloqueos
				SetAllyAttackBlock(false);

				// NO tocar WalkSpeed, FlySpeed ni otros stats
				// NO llamar a StopAttack()

				// Marcar para limpieza en primer Update()
				m_needsPostLoadCleanup = true;

				// Resetear estado
				m_state = ChallengeState.Idle;
				m_valuesSaved = false;
			}

			// Si fue derrotado, asegurar que esté en la manada del jugador
			if (m_hasBeenDefeated)
			{
				if (m_herd != null)
					m_herd.HerdName = "player";
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<bool>("HasBeenDefeated", m_hasBeenDefeated);
			valuesDictionary.SetValue<int>("ChallengeState", (int)m_state);
			valuesDictionary.SetValue<float>("BossHealthMultiplier", BossHealthMultiplier);
			valuesDictionary.SetValue<float>("BossDamageMultiplier", BossDamageMultiplier);
			valuesDictionary.SetValue<float>("BossSpeedMultiplier", BossSpeedMultiplier);
			valuesDictionary.SetValue<float>("VictoryHealthThreshold", VictoryHealthThreshold);
		}

		public override void Dispose()
		{
			if (m_state == ChallengeState.Countdown || m_state == ChallengeState.DuelInProgress)
			{
				RestoreAlliesChase();
				UnlockInventory();
				SetAllyAttackBlock(false);
			}
			base.Dispose();
		}
	}

	public static class InventoryBlocker
	{
		private static Dictionary<ComponentPlayer, bool> s_lockedPlayers = new Dictionary<ComponentPlayer, bool>();

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
		private static HashSet<ComponentCreature> s_blockedCreatures = new HashSet<ComponentCreature>();

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
