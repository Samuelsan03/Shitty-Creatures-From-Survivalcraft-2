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
		public SubsystemPickables m_subsystemPickables;

		public override int[] HandledBlocks
		{
			get { return new int[] { AntidoteBucketBlock.Index, AntidoteBowlBlock.Index }; }
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
			if (componentPlayer == null) return false;

			Entity targetEntity = null;
			ComponentBody hitBody = null;
			float? hitDistance = null;
			var componentBodies = new DynamicArray<ComponentBody>();
			Vector2 center = new Vector2(componentPlayer.ComponentBody.Position.X, componentPlayer.ComponentBody.Position.Z);
			float searchRadius = 6f;
			m_subsystemBodies.FindBodiesAroundPoint(center, searchRadius, componentBodies);
			Vector3 rayStart = componentPlayer.ComponentBody.Position + new Vector3(0f, 1.5f, 0f);
			Vector3 rayDirection = ray.Direction;
			float maxDistance = 6f;
			foreach (ComponentBody body in componentBodies)
			{
				if (body.Entity == componentPlayer.Entity) continue;
				float? distance = RayBoxIntersection(rayStart, rayDirection, body.BoundingBox);
				if (distance.HasValue && distance.Value < maxDistance && (!hitDistance.HasValue || distance.Value < hitDistance.Value))
				{
					hitBody = body;
					hitDistance = distance.Value;
				}
			}
			if (hitBody != null) targetEntity = hitBody.Entity;

			if (targetEntity == null || targetEntity == componentPlayer.Entity) return false;

			bool hasPoison = HasPoison(targetEntity);
			bool hasSickness = HasSickness(targetEntity);
			ComponentHealth targetHealth = targetEntity.FindComponent<ComponentHealth>();
			bool isDead = targetHealth != null && targetHealth.Health <= 0f;

			if ((!hasPoison && !hasSickness) || isDead) return false;

			m_subsystemAudio.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);
			bool curedPoison = false;
			bool curedSickness = false;
			if (hasPoison) curedPoison = CurePoison(targetEntity);
			if (hasSickness) curedSickness = CureSickness(targetEntity);

			if (curedPoison || curedSickness)
			{
				string npcCuredMessage = LanguageControl.Get("AntidoteBucket", "NPCCured");
				componentPlayer.ComponentGui.DisplaySmallMessage(npcCuredMessage, new Color(0, 255, 0), true, false);
			}

			if (curedPoison || curedSickness)
			{
				IInventory inventory = componentMiner.Inventory;
				if (inventory != null)
				{
					int activeSlotIndex = inventory.ActiveSlotIndex;
					int currentValue = inventory.GetSlotValue(activeSlotIndex);
					int contents = Terrain.ExtractContents(currentValue);
					Block usedBlock = BlocksManager.Blocks[contents];

					// 1. Eliminar el ítem usado
					inventory.RemoveSlotItems(activeSlotIndex, 1);

					// 2. Determinar el ítem vacío correspondiente según el tipo real del bloque
					int emptyItemIndex;
					if (usedBlock is AntidoteBucketBlock)
						emptyItemIndex = EmptyBucketBlock.Index;
					else if (usedBlock is AntidoteBowlBlock)
						emptyItemIndex = EmptyBowlBlock.Index;
					else
						return false; // Tipo desconocido, no debería ocurrir

					// 3. Intentar agregar el ítem vacío al slot activo
					inventory.AddSlotItems(activeSlotIndex, emptyItemIndex, 1);

					// 4. Verificar si el ítem vacío está ahora en el inventario
					bool foundEmpty = false;
					for (int i = 0; i < inventory.SlotsCount; i++)
					{
						int slotValue = inventory.GetSlotValue(i);
						if (Terrain.ExtractContents(slotValue) == emptyItemIndex && inventory.GetSlotCount(i) > 0)
						{
							foundEmpty = true;
							break;
						}
					}

					// 5. Si no se encontró, crear el ítem en el mundo
					if (!foundEmpty)
					{
						Vector3 dropPosition = componentPlayer.ComponentBody.Position + new Vector3(0f, 1.2f, 0f);
						m_subsystemPickables.AddPickable(emptyItemIndex, 1, dropPosition, null, null);
					}
				}
				return true;
			}

			return false;
		}

		private float? RayBoxIntersection(Vector3 rayOrigin, Vector3 rayDirection, BoundingBox box)
		{
			Vector3 dirFrac = new Vector3(1.0f / (rayDirection.X != 0 ? rayDirection.X : float.Epsilon), 1.0f / (rayDirection.Y != 0 ? rayDirection.Y : float.Epsilon), 1.0f / (rayDirection.Z != 0 ? rayDirection.Z : float.Epsilon));
			float t1 = (box.Min.X - rayOrigin.X) * dirFrac.X;
			float t2 = (box.Max.X - rayOrigin.X) * dirFrac.X;
			float t3 = (box.Min.Y - rayOrigin.Y) * dirFrac.Y;
			float t4 = (box.Max.Y - rayOrigin.Y) * dirFrac.Y;
			float t5 = (box.Min.Z - rayOrigin.Z) * dirFrac.Z;
			float t6 = (box.Max.Z - rayOrigin.Z) * dirFrac.Z;
			float tmin = MathUtils.Max(MathUtils.Min(t1, t2), MathUtils.Min(t3, t4), MathUtils.Min(t5, t6));
			float tmax = MathUtils.Min(MathUtils.Max(t1, t2), MathUtils.Max(t3, t4), MathUtils.Max(t5, t6));
			if (tmax < 0) return null;
			if (tmin > tmax) return null;
			if (tmin < 0) return tmax;
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
				var pukeSystemField = typeof(ComponentPoisonInfected).GetField("m_pukeParticleSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var pukeSystem = pukeSystemField?.GetValue(poison) as PukeParticleSystem;
				if (pukeSystem != null && !pukeSystem.IsStopped)
				{
					pukeSystem.IsStopped = true;
					pukeSystemField.SetValue(poison, null);
				}
				poison.m_InfectDuration = 0f;
				var nauseaField = typeof(ComponentPoisonInfected).GetField("m_lastNauseaTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				nauseaField?.SetValue(poison, null);
				var pukeTimeField = typeof(ComponentPoisonInfected).GetField("m_lastPukeTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				pukeTimeField?.SetValue(poison, null);
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
				var pukeSystemField = typeof(ComponentSickness).GetField("m_pukeParticleSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var pukeSystem = pukeSystemField?.GetValue(sickness) as PukeParticleSystem;
				if (pukeSystem != null && !pukeSystem.IsStopped)
				{
					pukeSystem.IsStopped = true;
					pukeSystemField.SetValue(sickness, null);
				}
				ComponentPlayer player = entity.FindComponent<ComponentPlayer>();
				if (player != null)
				{
					player.ComponentScreenOverlays.GreenoutFactor = 0f;
				}
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
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);
		}
	}
}
