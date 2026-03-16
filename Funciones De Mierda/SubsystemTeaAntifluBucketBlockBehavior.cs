using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemTeaAntifluBucketBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemAudio m_subsystemAudio;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemBodies m_subsystemBodies;

		public override int[] HandledBlocks
		{
			get { return new int[] { TeaAntifluBucketBlock.Index }; }
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
			if (componentPlayer == null)
				return false;

			// Realizar raycast para detectar entidades cercanas (NPCs)
			Entity targetEntity = null;
			ComponentBody hitBody = null;
			float? hitDistance = null;

			var componentBodies = new DynamicArray<ComponentBody>();
			Vector2 center = new Vector2(componentPlayer.ComponentBody.Position.X, componentPlayer.ComponentBody.Position.Z);
			float searchRadius = 6f;
			m_subsystemBodies.FindBodiesAroundPoint(center, searchRadius, componentBodies);

			Vector3 rayStart = componentPlayer.ComponentBody.Position + new Vector3(0f, 1.5f, 0f); // Altura de ojos
			Vector3 rayDirection = ray.Direction;
			float maxDistance = 6f;

			foreach (ComponentBody body in componentBodies)
			{
				if (body.Entity == componentPlayer.Entity) continue;

				float? distance = RayBoxIntersection(rayStart, rayDirection, body.BoundingBox);
				if (distance.HasValue && distance.Value < maxDistance &&
					(!hitDistance.HasValue || distance.Value < hitDistance.Value))
				{
					hitBody = body;
					hitDistance = distance.Value;
				}
			}

			if (hitBody != null)
			{
				targetEntity = hitBody.Entity;
			}

			bool isTargetingPlayer = (targetEntity == null || targetEntity == componentPlayer.Entity);
			if (isTargetingPlayer)
			{
				targetEntity = componentPlayer.Entity;

				// Solo se puede curar al jugador en modos de supervivencia
				if (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
					!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
				{
					return false;
				}
			}

			// Verificar si el objetivo tiene gripe
			bool hasFlu = HasFlu(targetEntity);
			if (!hasFlu)
				return false;

			// Reproducir sonido de beber
			m_subsystemAudio.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);

			// Curar la gripe
			bool cured = CureFlu(targetEntity);

			// --- AÑADIR SED AL JUGADOR SI ES LA ENTIDAD OBJETIVO ---
			if (targetEntity != null)
			{
				var thirst = targetEntity.FindComponent<ComponentThirst>();
				if (thirst != null)
				{
					thirst.Drink(0.4f); // Restaura un 40% de sed
				}
			}
			// -------------------------------------------------------

			// Mostrar mensajes según el objetivo
			ComponentPlayer targetPlayer = targetEntity.FindComponent<ComponentPlayer>();
			if (targetPlayer != null)
			{
				string message = LanguageControl.Get("AntifluBucket", "FluCured");
				targetPlayer.ComponentGui.DisplaySmallMessage(message, new Color(102, 178, 255), true, false);
			}
			else if (!isTargetingPlayer)
			{
				string npcMessage = LanguageControl.Get("AntifluBucket", "FluNPCCured");
				componentPlayer.ComponentGui.DisplaySmallMessage(npcMessage, new Color(102, 178, 255), true, false);
			}

			// Reemplazar el cubo de antigripe por un cubo vacío
			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, EmptyBucketBlock.Index, 1);
			}

			return true;
		}

		private float? RayBoxIntersection(Vector3 rayOrigin, Vector3 rayDirection, BoundingBox box)
		{
			Vector3 dirFrac = new Vector3(
				1.0f / (rayDirection.X != 0 ? rayDirection.X : float.Epsilon),
				1.0f / (rayDirection.Y != 0 ? rayDirection.Y : float.Epsilon),
				1.0f / (rayDirection.Z != 0 ? rayDirection.Z : float.Epsilon)
			);

			float t1 = (box.Min.X - rayOrigin.X) * dirFrac.X;
			float t2 = (box.Max.X - rayOrigin.X) * dirFrac.X;
			float t3 = (box.Min.Y - rayOrigin.Y) * dirFrac.Y;
			float t4 = (box.Max.Y - rayOrigin.Y) * dirFrac.Y;
			float t5 = (box.Min.Z - rayOrigin.Z) * dirFrac.Z;
			float t6 = (box.Max.Z - rayOrigin.Z) * dirFrac.Z;

			float tmin = MathUtils.Max(MathUtils.Min(t1, t2), MathUtils.Min(t3, t4), MathUtils.Min(t5, t6));
			float tmax = MathUtils.Min(MathUtils.Max(t1, t2), MathUtils.Max(t3, t4), MathUtils.Max(t5, t6));

			if (tmax < 0 || tmin > tmax)
				return null;

			return (tmin < 0) ? tmax : tmin;
		}

		private bool HasFlu(Entity entity)
		{
			if (entity == null) return false;

			// Jugador: ComponentFlu
			var playerFlu = entity.FindComponent<ComponentFlu>();
			if (playerFlu != null && playerFlu.HasFlu)
				return true;

			// Criatura: ComponentFluInfected
			var creatureFlu = entity.FindComponent<ComponentFluInfected>();
			if (creatureFlu != null && creatureFlu.IsInfected)
				return true;

			return false;
		}

		private bool CureFlu(Entity entity)
		{
			if (entity == null) return false;

			// Curar al jugador (ComponentFlu)
			var playerFlu = entity.FindComponent<ComponentFlu>();
			if (playerFlu != null && playerFlu.HasFlu)
			{
				// Los campos son públicos, asignamos directamente
				playerFlu.m_fluDuration = 0f;
				playerFlu.m_fluOnset = 0f;
				playerFlu.m_coughDuration = 0f;
				playerFlu.m_sneezeDuration = 0f;
				playerFlu.m_blackoutDuration = 0f;
				playerFlu.m_blackoutFactor = 0f;

				// Restablecer el efecto de pantalla
				var player = entity.FindComponent<ComponentPlayer>();
				if (player != null)
				{
					player.ComponentScreenOverlays.BlackoutFactor = 0f;
				}
				return true;
			}

			// Curar a una criatura (ComponentFluInfected)
			var creatureFlu = entity.FindComponent<ComponentFluInfected>();
			if (creatureFlu != null && creatureFlu.IsInfected)
			{
				// Campo público
				creatureFlu.m_fluDuration = 0f;
				return true;
			}

			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);
		}
	}
}
