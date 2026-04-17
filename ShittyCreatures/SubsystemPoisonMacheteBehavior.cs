using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemPoisonMacheteBehavior : SubsystemBlockBehavior, IUpdateable
	{
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemTime m_subsystemTime;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemPlayers m_subsystemPlayers;
		private Dictionary<ComponentPlayer, float> m_lastAttackTimes = new Dictionary<ComponentPlayer, float>();

		public const float AttackRange = 2.5f;
		public const float PoisonDuration = 300f;
		public const float AttackCooldown = 1.0f; // 1 segundo entre ataques

		public override int[] HandledBlocks
		{
			get { return new int[] { PoisonMacheteBlock.Index }; }
		}

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
		}

		public void Update(float dt)
		{
			float currentTime = (float)m_subsystemTime.GameTime;

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player.ComponentMiner == null)
					continue;

				// Verificar si el jugador tiene el machete envenenado equipado
				int activeBlockValue = player.ComponentMiner.ActiveBlockValue;
				if (activeBlockValue <= 0)
					continue;

				int blockId = Terrain.ExtractContents(activeBlockValue);
				if (blockId != PoisonMacheteBlock.Index)
					continue;

				// Verificar si es tiempo para un nuevo "ataque"
				float lastAttackTime;
				if (!m_lastAttackTimes.TryGetValue(player, out lastAttackTime))
				{
					lastAttackTime = 0f;
				}

				// Aplicar veneno periódicamente cuando el jugador tiene el machete equipado
				if (currentTime - lastAttackTime > AttackCooldown)
				{
					ApplyPoisonToNearbyTargets(player);
					m_lastAttackTimes[player] = currentTime;
				}
			}
		}

		private void ApplyPoisonToNearbyTargets(ComponentPlayer attackerPlayer)
		{
			if (attackerPlayer == null || !m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
				return;

			// Obtener la posición del atacante
			Vector3 attackerPosition = attackerPlayer.ComponentBody.Position;
			float attackRangeSquared = AttackRange * AttackRange;

			// Buscar todas las criaturas cercanas
			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body.Entity == null || body.Entity == attackerPlayer.Entity)
					continue;

				// Verificar distancia
				float distanceSquared = Vector3.DistanceSquared(body.Position, attackerPosition);
				if (distanceSquared <= attackRangeSquared)
				{
					// Aplicar veneno a la criatura
					ComponentCreature targetCreature = body.Entity.FindComponent<ComponentCreature>();
					if (targetCreature != null)
					{
						ApplyPoisonToCreature(targetCreature);
					}
				}
			}
		}

		private void ApplyPoisonToCreature(ComponentCreature target)
		{
			if (target == null)
				return;

			// Para jugadores
			ComponentPlayer targetPlayer = target as ComponentPlayer;
			if (targetPlayer != null)
			{
				if (!targetPlayer.ComponentSickness.IsSick)
				{
					targetPlayer.ComponentSickness.StartSickness();
					targetPlayer.ComponentSickness.m_sicknessDuration = PoisonDuration;
				}
				return;
			}

			// Para NPCs
			ComponentPoisonInfected poisonComponent = target.Entity.FindComponent<ComponentPoisonInfected>();
			if (poisonComponent != null && !poisonComponent.IsInfected)
			{
				poisonComponent.StartInfect(PoisonDuration);
			}
		}
	}
}
