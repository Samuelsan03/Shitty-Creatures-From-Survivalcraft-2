using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAntidoteBucketBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemAudio m_subsystemAudio;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;
		public SubsystemBodies m_subsystemBodies;

		public override int[] HandledBlocks
		{
			get { return new int[] { AntidoteBucketBlock.Index }; }
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
			if (componentPlayer == null)
			{
				return false;
			}

			// Realizar raycast para detectar entidades cercanas (NPCs)
			Entity targetEntity = null;
			ComponentBody hitBody = null;
			float? hitDistance = null;

			// Buscar todas las entidades cercanas usando el método correcto
			var componentBodies = new DynamicArray<ComponentBody>();

			// Crear un área de búsqueda alrededor del jugador (usamos Vector2 para XZ)
			Vector2 center = new Vector2(componentPlayer.ComponentBody.Position.X, componentPlayer.ComponentBody.Position.Z);
			float searchRadius = 6f; // Radio de búsqueda

			// Usar el método correcto basado en el código de ComponentBody
			m_subsystemBodies.FindBodiesAroundPoint(center, searchRadius, componentBodies);

			// Crear un rayo desde la posición del jugador
			Vector3 rayStart = componentPlayer.ComponentBody.Position + new Vector3(0f, 1.5f, 0f); // Altura de los ojos
			Vector3 rayDirection = ray.Direction;
			float maxDistance = 6f;

			// Verificar intersección con cada cuerpo
			foreach (ComponentBody body in componentBodies)
			{
				if (body.Entity == componentPlayer.Entity) continue; // Saltar al jugador

				// Verificar si el rayo intersecta con el bounding box del cuerpo
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

			// Determinar si estamos intentando curar al jugador o a un NPC
			bool isTargetingPlayer = (targetEntity == null || targetEntity == componentPlayer.Entity);

			// Si está apuntando al jugador, verificar restricciones de modo de juego
			if (isTargetingPlayer)
			{
				targetEntity = componentPlayer.Entity;

				// Solo funciona para jugadores en modos de supervivencia
				if (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
					!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
				{
					return false;
				}
			}
			// Si está apuntando a un NPC, permitir en cualquier modo de juego

			// Verificar si el objetivo tiene veneno o enfermedad
			bool hasPoison = HasPoison(targetEntity);
			bool hasSickness = HasSickness(targetEntity);

			// Si no tiene ni veneno ni enfermedad, NO usar el antídoto
			if (!hasPoison && !hasSickness)
			{
				return false;
			}

			// Reproducir sonido de bebida
			m_subsystemAudio.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);

			// Curar efectos negativos (solo si los tiene)
			bool curedPoison = false;
			bool curedSickness = false;
			bool restoredHealth = false;

			if (hasPoison)
			{
				curedPoison = CurePoison(targetEntity);
			}

			if (hasSickness)
			{
				curedSickness = CureSickness(targetEntity);
			}

			// Restaurar salud SOLO si se curó veneno o enfermedad
			if (curedPoison || curedSickness)
			{
				restoredHealth = RestoreHealth(targetEntity);
			}

			// Mostrar mensajes solo para jugadores
			ComponentPlayer targetPlayer = targetEntity.FindComponent<ComponentPlayer>();
			if (targetPlayer != null)
			{
				// Mostrar un solo mensaje combinado si se curó veneno O enfermedad
				if (curedPoison || curedSickness)
				{
					string curedMessage = LanguageControl.Get("Messages", "PoisonAndSicknessCured");
					targetPlayer.ComponentGui.DisplaySmallMessage(curedMessage, new Color(0, 255, 0), true, false);
				}
			}
			else if (!isTargetingPlayer)
			{
				// Si curó a un NPC (no al jugador), mostrar un mensaje al jugador
				string npcCuredMessage = LanguageControl.Get("Messages", "NPCCured");
				componentPlayer.ComponentGui.DisplaySmallMessage(npcCuredMessage, new Color(0, 255, 0), true, false);
			}

			// Reemplazar el AntidoteBucketBlock con un EmptyBucketBlock
			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, EmptyBucketBlock.Index, 1);
			}

			return true;
		}

		// Método auxiliar para intersección rayo-boundingbox
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

			// Si tmax < 0, el rayo está intersectando pero en dirección opuesta
			if (tmax < 0)
			{
				return null;
			}

			// Si tmin > tmax, no hay intersección
			if (tmin > tmax)
			{
				return null;
			}

			// Si tmin < 0, el origen está dentro de la caja
			if (tmin < 0)
			{
				return tmax;
			}

			return tmin;
		}

		private bool HasPoison(Entity entity)
		{
			if (entity == null) return false;
			ComponentPoisonInfected poison = entity.FindComponent<ComponentPoisonInfected>();
			return poison != null && poison.IsInfected;
		}

		private bool HasSickness(Entity entity)
		{
			if (entity == null) return false;
			ComponentSickness sickness = entity.FindComponent<ComponentSickness>();
			return sickness != null && sickness.IsSick;
		}

		private bool CurePoison(Entity entity)
		{
			if (entity == null) return false;

			ComponentPoisonInfected poison = entity.FindComponent<ComponentPoisonInfected>();
			if (poison != null && poison.IsInfected)
			{
				System.Reflection.MethodInfo method = typeof(ComponentPoisonInfected).GetMethod("StartInfect",
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

				if (method != null)
				{
					method.Invoke(poison, new object[] { 0f });
				}
				else
				{
					System.Reflection.FieldInfo infectField = typeof(ComponentPoisonInfected).GetField("m_InfectDuration",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

					if (infectField != null)
					{
						infectField.SetValue(poison, 0f);
					}
				}

				poison.PoisonResistance = MathUtils.Max(poison.PoisonResistance, 50f);
				return true;
			}
			return false;
		}

		private bool CureSickness(Entity entity)
		{
			if (entity == null) return false;

			ComponentSickness sickness = entity.FindComponent<ComponentSickness>();
			if (sickness != null && sickness.IsSick)
			{
				sickness.m_sicknessDuration = 0f;
				sickness.m_greenoutDuration = 0f;
				sickness.m_greenoutFactor = 0f;

				ComponentPlayer player = entity.FindComponent<ComponentPlayer>();
				if (player != null)
				{
					player.ComponentScreenOverlays.GreenoutFactor = 0f;
				}
				return true;
			}
			return false;
		}

		private bool RestoreHealth(Entity entity)
		{
			if (entity == null) return false;

			ComponentHealth health = entity.FindComponent<ComponentHealth>();
			if (health != null && health.Health < 1f)
			{
				float missingHealth = 1f - health.Health;
				health.Heal(missingHealth);
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
