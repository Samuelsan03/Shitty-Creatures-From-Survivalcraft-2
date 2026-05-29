using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemLargeFirstAidKitBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemAudio m_subsystemAudio;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemGameInfo m_subsystemGameInfo;

		public override int[] HandledBlocks => new int[] {
			BlocksManager.GetBlockIndex<LargeFirstAidKitBlock>()
		};

		public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
		{
			if (componentMiner == null || componentMiner.ComponentCreature == null)
				return false;

			int blockValue = raycastResult.Value;
			int blockIndex = Terrain.ExtractContents(blockValue);

			if (blockIndex != BlocksManager.GetBlockIndex<LargeFirstAidKitBlock>())
				return false;

			bool healed = TryHealTargetFromInteract(raycastResult, componentMiner);

			if (healed)
			{
				m_subsystemAudio.PlaySound("Audio/UI/cured", 1f, 0f, raycastResult.HitPoint(), 5f, false);

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

			int activeBlockValue = componentMiner.ActiveBlockValue;
			int activeBlockIndex = Terrain.ExtractContents(activeBlockValue);

			if (activeBlockIndex != BlocksManager.GetBlockIndex<LargeFirstAidKitBlock>())
				return false;

			Entity targetEntity = null;
			ComponentBody hitBody = null;
			float? hitDistance = null;

			var componentBodies = new DynamicArray<ComponentBody>();

			Vector2 center = new Vector2(componentPlayer.ComponentBody.Position.X, componentPlayer.ComponentBody.Position.Z);
			float searchRadius = 5f;

			m_subsystemBodies.FindBodiesAroundPoint(center, searchRadius, componentBodies);

			Vector3 rayStart = componentPlayer.ComponentCreatureModel.EyePosition;
			Vector3 rayDirection = ray.Direction;
			float maxDistance = 5f;

			foreach (ComponentBody body in componentBodies)
			{
				if (body.Entity == componentPlayer.Entity)
					continue;

				float? distance = RayBoxIntersection(rayStart, rayDirection, body.BoundingBox);
				if (distance.HasValue && distance.Value < maxDistance &&
					(!hitDistance.HasValue || distance.Value < hitDistance.Value))
				{
					hitBody = body;
					hitDistance = distance.Value;
				}
			}

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
				return false;
			}

			ComponentHealth health = targetEntity.FindComponent<ComponentHealth>();
			if (health == null || health.Health >= 1f)
				return false;

			if (health.DeathTime.HasValue)
				return false;

			float targetHealth = 1f;
			float healAmount = targetHealth - health.Health;

			if (healAmount <= 0)
				return false;

			health.Heal(healAmount);

			if (IsAlly(componentPlayer, targetEntity))
			{
				AchievementsManager.OnHeal(componentPlayer);
			}

			Vector3 position = componentPlayer.ComponentBody.Position;
			m_subsystemAudio.PlaySound("Audio/UI/cured", 1f, 0f, position, 5f, false);

			if (targetEntity == componentPlayer.Entity)
			{
				string message = LanguageControl.Get("LargeFirstAidKit", "PlayerHealed");
				componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Green, true, false);
			}
			else
			{
				ComponentCreature creature = targetEntity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					string creatureName = creature.DisplayName;
					string message = string.Format(
						LanguageControl.Get("LargeFirstAidKit", "NPCHealedFormat"),
						creatureName
					);
					componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Green, true, false);
				}
			}

			if (componentMiner.Inventory != null)
			{
				componentMiner.Inventory.RemoveSlotItems(
					componentMiner.Inventory.ActiveSlotIndex,
					1
				);
			}

			return true;
		}

		private bool IsAlly(ComponentPlayer player, Entity targetEntity)
		{
			if (targetEntity == player.Entity) return true;
			var playerHerd = player.Entity.FindComponent<ComponentNewHerdBehavior>();
			var targetCreature = targetEntity.FindComponent<ComponentCreature>();
			if (playerHerd == null || targetCreature == null) return false;
			var targetHerd = targetCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (targetHerd == null) return false;
			return playerHerd.IsSameHerdOrGuardian(targetCreature);
		}

		private bool TryHealTargetFromInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
		{
			ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
			if (componentPlayer == null)
				return false;

			var componentBodies = new DynamicArray<ComponentBody>();

			Vector3 hitPoint = raycastResult.HitPoint();
			Vector2 center = new Vector2(hitPoint.X, hitPoint.Z);
			float searchRadius = 2f;

			m_subsystemBodies.FindBodiesAroundPoint(center, searchRadius, componentBodies);

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

			ComponentHealth health = targetEntity.FindComponent<ComponentHealth>();
			if (health == null || health.Health >= 1f)
				return false;

			if (health.DeathTime.HasValue)
				return false;

			float targetHealth = 1f;
			float healAmount = targetHealth - health.Health;

			if (healAmount <= 0)
				return false;

			health.Heal(healAmount);

			AchievementsManager.OnHeal(componentPlayer);

			if (targetEntity == componentPlayer.Entity)
			{
				string message = LanguageControl.Get("LargeFirstAidKit", "PlayerHealed");
				componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Green, true, false);
			}
			else
			{
				ComponentCreature creature = targetEntity.FindComponent<ComponentCreature>();
				if (creature != null)
				{
					string creatureName = creature.DisplayName;
					string message = string.Format(
						LanguageControl.Get("LargeFirstAidKit", "NPCHealedFormat"),
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
