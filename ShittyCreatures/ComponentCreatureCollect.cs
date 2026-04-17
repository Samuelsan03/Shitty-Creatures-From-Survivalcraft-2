using System;
using System.Collections.Generic;
using System.Globalization;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureCollect : Component, IUpdateable
	{
		public ComponentCreature ComponentCreature { get; set; }
		public IInventory Inventory { get; set; }
		public bool CanCollect { get; set; }
		public List<int> CollectableItems { get; set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.ComponentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.Inventory = base.Entity.FindComponent<ComponentInventory>();
			if (this.Inventory == null)
			{
				this.Inventory = base.Entity.FindComponent<IInventory>();
			}
			this.CanCollect = valuesDictionary.GetValue<bool>("CanCollect", true);

			this.CollectableItems = new List<int>();
			string text = valuesDictionary.GetValue<string>("CollectableItems", "");
			if (!string.IsNullOrEmpty(text))
			{
				string[] array = text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string text2 in array)
				{
					string text3 = text2.Trim();
					int num;
					if (int.TryParse(text3, out num))
					{
						this.CollectableItems.Add(num);
					}
					else
					{
						int blockIndex = BlocksManager.GetBlockIndex(text3, false);
						if (blockIndex >= 0)
						{
							this.CollectableItems.Add(blockIndex);
						}
					}
				}
			}
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);

			// TODOS los chase behaviors como en los archivos originales
			this.m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(false);
			this.m_componentNewChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>(false);
			this.m_componentNewChaseBehavior2 = base.Entity.FindComponent<ComponentNewChaseBehavior2>(false);
			this.m_componentZombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>(false);
			this.m_componentBanditChaseBehavior = base.Entity.FindComponent<ComponentBanditChaseBehavior>(false);
		}

		public void Update(float dt)
		{
			if (!this.CanCollect || this.Inventory == null || this.ComponentCreature.ComponentHealth.Health <= 0f)
			{
				return;
			}

			Vector3 centerPosition = this.ComponentCreature.ComponentBody.BoundingBox.Center();
			float closestDistance = float.MaxValue;
			Pickable closestPickable = null;

			foreach (Pickable pickable in this.m_subsystemPickables.Pickables)
			{
				TerrainChunk chunkAtCell = this.m_subsystemTerrain.Terrain.GetChunkAtCell(
					Terrain.ToCell(pickable.Position.X),
					Terrain.ToCell(pickable.Position.Z));

				if (chunkAtCell == null || pickable.FlyToPosition != null)
				{
					continue;
				}

				if (!this.IsCollectable(pickable.Value))
				{
					continue;
				}

				if (ComponentInventoryBase.FindAcquireSlotForItem(this.Inventory, pickable.Value) < 0)
				{
					continue;
				}

				float distance = Vector3.Distance(centerPosition, pickable.Position);

				if (distance <= 2f)
				{
					this.CollectPickable(pickable);
				}
				else if (distance <= 12f && distance < closestDistance)
				{
					closestDistance = distance;
					closestPickable = pickable;
				}
			}

			if (closestPickable != null && this.m_componentPathfinding != null)
			{
				this.m_componentPathfinding.SetDestination(
					new Vector3?(closestPickable.Position),
					1f,
					0.75f,
					24,
					true,
					false,
					false,
					null);
			}
		}

		private bool IsCollectable(int value)
		{
			if (this.CollectableItems.Count == 0)
			{
				return true;
			}

			int contents = Terrain.ExtractContents(value);
			return this.CollectableItems.Contains(contents);
		}

		private void CollectPickable(Pickable pickable)
		{
			Vector3 centerPosition = this.ComponentCreature.ComponentBody.BoundingBox.Center();

			pickable.ToRemove = true;
			pickable.FlyToPosition = new Vector3?(centerPosition);

			int collected = ComponentInventoryBase.AcquireItems(this.Inventory, pickable.Value, pickable.Count);
			if (collected > 0)
			{
				pickable.Count -= collected;
				if (pickable.Count <= 0)
				{
					pickable.ToRemove = true;
				}
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<bool>("CanCollect", this.CanCollect);
			if (this.CollectableItems.Count > 0)
			{
				string text = "";
				for (int i = 0; i < this.CollectableItems.Count; i++)
				{
					if (i > 0)
					{
						text += ",";
					}
					text += this.CollectableItems[i].ToString(CultureInfo.InvariantCulture);
				}
				valuesDictionary.SetValue<string>("CollectableItems", text);
			}
		}

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemPickables m_subsystemPickables;
		private SubsystemGameInfo m_subsystemGameInfo;
		private ComponentPathfinding m_componentPathfinding;

		// TODOS los chase behaviors como en los archivos que me mostraste
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;
		private ComponentNewChaseBehavior2 m_componentNewChaseBehavior2;
		private ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		private ComponentBanditChaseBehavior m_componentBanditChaseBehavior;
	}
}
