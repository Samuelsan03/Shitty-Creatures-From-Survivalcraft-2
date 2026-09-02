using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAntidotePillBehavior : SubsystemBlockBehavior
	{
		private SubsystemAudio m_subsystemAudio;
		private SubsystemBodies m_subsystemBodies;

		public override int[] HandledBlocks
		{
			get
			{
				int blockIndex = BlocksManager.GetBlockIndex("AntidotePillBlock");
				if (blockIndex < 0)
				{
					return Array.Empty<int>();
				}
				return new int[] { blockIndex };
			}
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			int antidoteIndex = BlocksManager.GetBlockIndex("AntidotePillBlock");
			if (antidoteIndex < 0 || Terrain.ExtractContents(componentMiner.ActiveBlockValue) != antidoteIndex)
			{
				return false;
			}

			ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
			if (componentPlayer == null)
			{
				return false;
			}

			// Intentar curar a una criatura primero (funciona en CUALQUIER modo de juego)
			if (TryCureCreature(ray, componentPlayer))
			{
				return true;
			}

			// Si no hay criatura, intentar curar al jugador (solo en supervivencia/aventura)
			SubsystemGameInfo subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			if (subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
			{
				return false;
			}

			bool hasFlu = componentPlayer.ComponentFlu != null && componentPlayer.ComponentFlu.HasFlu;
			bool hasSickness = componentPlayer.ComponentSickness != null && componentPlayer.ComponentSickness.IsSick;

			if (!hasFlu && !hasSickness)
			{
				return false;
			}

			if (hasFlu)
			{
				componentPlayer.ComponentFlu.m_fluDuration = 0f;

				componentPlayer.ComponentGui.DisplaySmallMessage(
					new EnchantedMessageWidget.Message(LanguageControl.Get("SubsystemAntidotePillBehavior", 1), Color.White, false, 1f, true),
					false
				);
			}

			if (hasSickness)
			{
				componentPlayer.ComponentSickness.m_sicknessDuration = 0f;
				componentPlayer.ComponentSickness.m_greenoutDuration = 0f;
				componentPlayer.ComponentSickness.m_greenoutFactor = 0f;
				if (componentPlayer.ComponentSickness.m_pukeParticleSystem != null)
				{
					componentPlayer.ComponentSickness.m_pukeParticleSystem = null;
				}

				componentPlayer.ComponentGui.DisplaySmallMessage(
					new EnchantedMessageWidget.Message(LanguageControl.Get("SubsystemAntidotePillBehavior", 2), Color.White, false, 1f, true),
					false
				);
			}

			componentMiner.RemoveActiveTool(1);

			if (m_subsystemAudio == null)
			{
				m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			}
			m_subsystemAudio.PlaySound("Audio/Items/consumo antidoto", 1f, 0f, componentPlayer.ComponentBody.Position, 2f, false);

			return true;
		}

		private bool TryCureCreature(Ray3 ray, ComponentPlayer componentPlayer)
		{
			if (m_subsystemBodies == null)
			{
				m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			}

			// Hacer raycast para detectar criaturas (rango de 5 bloques)
			Vector3 end = ray.Position + ray.Direction * 5f;
			ComponentBody playerBody = componentPlayer.ComponentBody;

			BodyRaycastResult? bodyRaycastResult = m_subsystemBodies.Raycast(
				ray.Position,
				end,
				0f,
				(ComponentBody body, float dist) =>
					body != playerBody &&
					!body.IsChildOfBody(playerBody) &&
					!playerBody.IsChildOfBody(body) &&
					!body.IsRaycastTransparent &&
					Vector3.Dot(Vector3.Normalize(body.BoundingBox.Center() - ray.Position), ray.Direction) > 0.7f
			);

			if (!bodyRaycastResult.HasValue || bodyRaycastResult.Value.ComponentBody == null)
			{
				return false;
			}

			ComponentCreature creature = bodyRaycastResult.Value.ComponentBody.Entity.FindComponent<ComponentCreature>();
			if (creature == null || creature is ComponentPlayer)
			{
				return false;
			}

			// Verificar si tiene gripa o veneno
			ComponentFluInfected fluInfected = creature.Entity.FindComponent<ComponentFluInfected>();
			ComponentPoisonInfected poisonInfected = creature.Entity.FindComponent<ComponentPoisonInfected>();

			bool hasFlu = fluInfected != null && fluInfected.IsInfected;
			bool hasPoison = poisonInfected != null && poisonInfected.IsInfected;

			if (!hasFlu && !hasPoison)
			{
				return false;
			}

			string creatureName = creature.DisplayName ?? "Criatura";

			if (hasFlu)
			{
				fluInfected.m_fluDuration = 0f;

				componentPlayer.ComponentGui.DisplaySmallMessage(
					new EnchantedMessageWidget.Message(string.Format(LanguageControl.Get("SubsystemAntidotePillBehavior", 3), creatureName), Color.White, false, 1f, true),
					false
				);
			}

			if (hasPoison)
			{
				poisonInfected.m_InfectDuration = 0f;

				componentPlayer.ComponentGui.DisplaySmallMessage(
					new EnchantedMessageWidget.Message(string.Format(LanguageControl.Get("SubsystemAntidotePillBehavior", 4), creatureName), Color.White, false, 1f, true),
					false
				);
			}

			// Consumir la píldora
			componentPlayer.ComponentMiner.RemoveActiveTool(1);

			// Reproducir sonido en la posición de la criatura
			if (m_subsystemAudio == null)
			{
				m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			}
			m_subsystemAudio.PlaySound("Audio/Items/consumo antidoto", 1f, 0f, creature.ComponentBody.Position, 2f, false);

			return true;
		}
	}
}
