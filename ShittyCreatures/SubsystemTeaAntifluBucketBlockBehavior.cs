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
		public SubsystemPickables m_subsystemPickables;

		public override int[] HandledBlocks
		{
			get { return new int[] { TeaAntifluBucketBlock.Index, TeaAntifluBowlBlock.Index }; }
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

			bool hasFlu = HasFlu(targetEntity);
			if (!hasFlu) return false;

			m_subsystemAudio.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);
			bool cured = CureFlu(targetEntity);

			if (cured)
			{
				string npcMessage = LanguageControl.Get("AntifluBucket", "FluNPCCured");
				componentPlayer.ComponentGui.DisplaySmallMessage(npcMessage, new Color(102, 178, 255), true, false);

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
					if (usedBlock is TeaAntifluBucketBlock)
						emptyItemIndex = EmptyBucketBlock.Index;
					else if (usedBlock is TeaAntifluBowlBlock)
						emptyItemIndex = EmptyBowlBlock.Index;
					else
						return false; // Tipo desconocido

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
			if (tmax < 0 || tmin > tmax) return null;
			return (tmin < 0) ? tmax : tmin;
		}

		private bool HasFlu(Entity entity)
		{
			if (entity == null) return false;
			var playerFlu = entity.FindComponent<ComponentFlu>();
			if (playerFlu != null && playerFlu.HasFlu) return true;
			var creatureFlu = entity.FindComponent<ComponentFluInfected>();
			if (creatureFlu != null && creatureFlu.IsInfected) return true;
			return false;
		}

		private bool CureFlu(Entity entity)
		{
			if (entity == null) return false;
			var playerFlu = entity.FindComponent<ComponentFlu>();
			if (playerFlu != null && playerFlu.HasFlu)
			{
				playerFlu.m_fluDuration = 0f;
				playerFlu.m_fluOnset = 0f;
				playerFlu.m_coughDuration = 0f;
				playerFlu.m_sneezeDuration = 0f;
				playerFlu.m_blackoutDuration = 0f;
				playerFlu.m_blackoutFactor = 0f;
				var player = entity.FindComponent<ComponentPlayer>();
				if (player != null)
				{
					player.ComponentScreenOverlays.BlackoutFactor = 0f;
				}
				return true;
			}
			var creatureFlu = entity.FindComponent<ComponentFluInfected>();
			if (creatureFlu != null && creatureFlu.IsInfected)
			{
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
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);
		}
	}
}
