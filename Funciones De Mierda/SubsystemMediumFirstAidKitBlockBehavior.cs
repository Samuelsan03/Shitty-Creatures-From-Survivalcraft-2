using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemMediumFirstAidKitBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemAudio m_subsystemAudio;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemGameInfo m_subsystemGameInfo;

		public override int[] HandledBlocks => new int[] {
			BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>()
		};

		public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
		{
			if (componentMiner == null || componentMiner.ComponentCreature == null)
				return false;

			// Verificar si el bloque clickeado es un MediumFirstAidKitBlock
			int blockValue = raycastResult.Value;
			int blockIndex = Terrain.ExtractContents(blockValue);

			if (blockIndex != BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>())
				return false;

			// Intentar curar al objetivo
			bool healed = TryHealTargetFromInteract(raycastResult, componentMiner);

			if (healed)
			{
				// Reproducir sonido de curación
				m_subsystemAudio.PlaySound("Audio/UI/cured", 1f, 0f, raycastResult.HitPoint(), 5f, false);

				// Destruir el botiquín después de usarlo
				SubsystemTerrain terrain = componentMiner.Project.FindSubsystem<SubsystemTerrain>(true);
				terrain.DestroyCell(0, raycastResult.CellFace.X, raycastResult.CellFace.Y,
					raycastResult.CellFace.Z, 0, false, false);
			}

			return healed;
		}

		public override bool OnAim(Ray3 ray, ComponentMiner componentMiner, AimState state)
		{
			if (state == AimState.Completed)
			{
				return OnUse(ray, componentMiner);
			}
			return false;
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
			if (componentPlayer == null)
				return false;

			// Verificar si el item activo es un MediumFirstAidKitBlock
			int activeBlockValue = componentMiner.ActiveBlockValue;
			int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);

			if (activeBlockIndex != BlocksManager.GetBlockIndex<MediumFirstAidKitBlock>())
				return false;

			// Realizar raycast para detectar entidades cercanas (igual que el antidote)
			Entity targetEntity = null;
			ComponentBody hitBody = null;
			float? hitDistance = null;

			// Buscar todas las entidades cercanas
			var componentBodies = new DynamicArray<ComponentBody>();

			Vector2 center = new Vector2(componentPlayer.ComponentBody.Position.X, componentPlayer.ComponentBody.Position.Z);
			float searchRadius = 5f;

			m_subsystemBodies.FindBodiesAroundPoint(center, searchRadius, componentBodies);

			// Crear un rayo desde la posición del jugador
			Vector3 rayStart = componentPlayer.ComponentBody.Position + new Vector3(0f, 1.5f, 0f);
			Vector3 rayDirection = ray.Direction;
			float maxDistance = 5f;

			// Verificar intersección con cada cuerpo (excluyendo al jugador primero)
			foreach (ComponentBody body in componentBodies)
			{
				if (body.Entity == componentPlayer.Entity)
					continue; // Saltar al jugador primero

				// Verificar si el rayo intersecta con el bounding box del cuerpo
				float? distance = RayBoxIntersection(rayStart, rayDirection, body.BoundingBox);
				if (distance.HasValue && distance.Value < maxDistance &&
					(!hitDistance.HasValue || distance.Value < hitDistance.Value))
				{
					hitBody = body;
					hitDistance = distance.Value;
				}
			}

			// Si no se encontró un NPC, verificar si estamos apuntando al jugador mismo
			if (hitBody == null)
			{
				float? playerDistance = RayBoxIntersection(rayStart, rayDirection, componentPlayer.ComponentBody.BoundingBox);
				if (playerDistance.HasValue && playerDistance.Value < maxDistance)
				{
					hitBody = componentPlayer.ComponentBody;
				}
			}

			if (hitBody != null)
			{
				targetEntity = hitBody.Entity;
			}
			else
			{
				// Si no se encontró ningún objetivo, NO hacer nada
				return false;
			}

			// Verificar si el objetivo necesita curación
			ComponentHealth health = targetEntity.FindComponent<ComponentHealth>();
			if (health == null || health.Health >= 1f) // Solo cura si NO está al 100%
				return false;

			// Calcular curación según el nivel actual
			float targetHealth;

			if (health.Health < 0.5f)
			{
				// Si está por debajo del 50%, curar hasta el 50%
				targetHealth = 0.5f;
			}
			else
			{
				// Si está al 50% o más, curar hasta el 100%
				targetHealth = 1f;
			}

			float healAmount = targetHealth - health.Health;

			// Asegurarse de que la cantidad de curación sea positiva
			if (healAmount <= 0)
				return false;

			// Aplicar curación
			health.Heal(healAmount);

			// Reproducir sonido
			Vector3 position = componentPlayer.ComponentBody.Position;
			m_subsystemAudio.PlaySound("Audio/UI/cured", 1f, 0f, position, 5f, false);

			// Mostrar mensaje
			if (targetEntity == componentPlayer.Entity)
			{
				string message = LanguageControl.Get("MediumFirstAidKit", "PlayerHealed");
				componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Green, true, false);
			}
			else
			{
				ComponentCreature creature = targetEntity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					string creatureName = creature.DisplayName;
					string message = string.Format(
						LanguageControl.Get("MediumFirstAidKit", "NPCHealedFormat"),
						creatureName
					);
					componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Green, true, false);
				}
			}

			// Consumir un item del botiquín
			if (componentMiner.Inventory != null)
			{
				componentMiner.Inventory.RemoveSlotItems(
					componentMiner.Inventory.ActiveSlotIndex,
					1
				);
			}

			return true;
		}

		private bool TryHealTargetFromInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
		{
			ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
			if (componentPlayer == null)
				return false;

			// Buscar todas las entidades cercanas al punto de impacto
			var componentBodies = new DynamicArray<ComponentBody>();

			Vector3 hitPoint = raycastResult.HitPoint();
			Vector2 center = new Vector2(hitPoint.X, hitPoint.Z);
			float searchRadius = 2f;

			m_subsystemBodies.FindBodiesAroundPoint(center, searchRadius, componentBodies);

			// Buscar la entidad más cercana al punto de impacto
			ComponentBody closestBody = null;
			float closestDistance = float.MaxValue;

			foreach (ComponentBody body in componentBodies)
			{
				float distance = Vector3.Distance(body.Position, hitPoint);
				if (distance < closestDistance && distance < 2f)
				{
					closestBody = body;
					closestDistance = distance;
				}
			}

			if (closestBody == null)
				return false;

			Entity targetEntity = closestBody.Entity;

			// Verificar si el objetivo necesita curación
			ComponentHealth health = targetEntity.FindComponent<ComponentHealth>();
			if (health == null || health.Health >= 1f) // Solo cura si NO está al 100%
				return false;

			// Calcular curación según el nivel actual
			float targetHealth;

			if (health.Health < 0.5f)
			{
				// Si está por debajo del 50%, curar hasta el 50%
				targetHealth = 0.5f;
			}
			else
			{
				// Si está al 50% o más, curar hasta el 100%
				targetHealth = 1f;
			}

			float healAmount = targetHealth - health.Health;

			// Asegurarse de que la cantidad de curación sea positiva
			if (healAmount <= 0)
				return false;

			// Aplicar curación
			health.Heal(healAmount);

			// Mostrar mensaje
			if (targetEntity == componentPlayer.Entity)
			{
				string message = LanguageControl.Get("MediumFirstAidKit", "PlayerHealed");
				componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Green, true, false);
			}
			else
			{
				ComponentCreature creature = targetEntity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					string creatureName = creature.DisplayName;
					string message = string.Format(
						LanguageControl.Get("MediumFirstAidKit", "NPCHealedFormat"),
						creatureName
					);
					componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Green, true, false);
				}
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

			if (tmax < 0)
			{
				return null;
			}

			if (tmin > tmax)
			{
				return null;
			}

			if (tmin < 0)
			{
				return tmax;
			}

			return tmin;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);

			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);
		}
	}
}
