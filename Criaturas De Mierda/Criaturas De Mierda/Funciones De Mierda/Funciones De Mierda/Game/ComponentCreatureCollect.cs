using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000025 RID: 37
	public class ComponentCreatureCollect : Component, IUpdateable
	{
		// Token: 0x1700001B RID: 27
		// (get) Token: 0x06000102 RID: 258 RVA: 0x0000BD79 File Offset: 0x00009F79
		public UpdateOrder UpdateOrder
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x06000103 RID: 259 RVA: 0x0000BD7C File Offset: 0x00009F7C
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_audio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.componentCreature = base.Entity.FindComponent<ComponentCreature>();
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.Activate = valuesDictionary.GetValue<bool>("Activate");
			this.CanOpenInventory = valuesDictionary.GetValue<bool>("CanOpenInventory");
			this.DetectionDistance = valuesDictionary.GetValue<float>("DetectionDistance");
			this.SpecificItems = valuesDictionary.GetValue<string>("SpecificItems");
			this.IgnoreOrAcept = valuesDictionary.GetValue<bool>("IgnoreOrAcept");
			string[] array = this.SpecificItems.Split(',', StringSplitOptions.None);
			foreach (string text in array)
			{
				bool flag = string.IsNullOrWhiteSpace(text);
				if (!flag)
				{
					int num;
					bool flag2 = int.TryParse(text, out num);
					if (flag2)
					{
						bool flag3 = num > 0 && num < BlocksManager.Blocks.Length;
						if (flag3)
						{
							this.m_specificItemsSet.Add(num);
						}
					}
					else
					{
						Type type = Type.GetType("Game." + text);
						Block block = BlocksManager.GetBlock(type, false, true);
						bool flag4 = block != null;
						if (flag4)
						{
							this.m_specificItemsSet.Add(block.BlockIndex);
						}
					}
				}
			}
		}

		// Token: 0x06000104 RID: 260 RVA: 0x0000BF08 File Offset: 0x0000A108
		public void Update(float dt)
		{
			IInventory inventory = this.m_componentMiner.Inventory;
			ComponentInventory componentInventory = base.Entity.FindComponent<ComponentInventory>();
			ComponentChaseBehavior componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			bool flag = !this.Activate;
			if (!flag)
			{
				foreach (Pickable pickable in this.subsystemPickables.Pickables)
				{
					int item = Terrain.ExtractContents(pickable.Value);
					bool flag2 = this.m_specificItemsSet.Contains(item);
					bool flag3 = this.m_specificItemsSet.Count > 0;
					if (flag3)
					{
						bool flag4 = this.IgnoreOrAcept && !flag2;
						if (flag4)
						{
							continue;
						}
						bool flag5 = !this.IgnoreOrAcept && flag2;
						if (flag5)
						{
							continue;
						}
					}
					TerrainChunk chunkAtCell = this.subsystemTerrain.Terrain.GetChunkAtCell(Terrain.ToCell(pickable.Position.X), Terrain.ToCell(pickable.Position.Z));
					bool flag6 = componentInventory != null && chunkAtCell != null && pickable.FlyToPosition == null && (double)this.componentCreature.ComponentHealth.Health > 0.0 && componentChaseBehavior.Target == null;
					if (flag6)
					{
						Vector3 vector = this.componentCreature.ComponentBody.Position + new Vector3(0f, 0.8f, 0f);
						float num = (vector - pickable.Position).LengthSquared();
						float num2 = this.DetectionDistance * this.DetectionDistance;
						bool flag7 = num < num2;
						if (flag7)
						{
							for (int i = 0; i < inventory.SlotsCount; i++)
							{
								int num3 = ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value);
								bool flag8 = num3 >= 0;
								if (flag8)
								{
									this.m_componentPathfinding.SetDestination(new Vector3?(pickable.Position), 3f, 3.75f, 20, true, false, false, null);
									break;
								}
							}
						}
						bool flag9 = (double)num < 4.0;
						if (flag9)
						{
							for (int j = 0; j < inventory.SlotsCount; j++)
							{
								int num4 = ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value);
								bool flag10 = num4 >= 0;
								if (flag10)
								{
									pickable.ToRemove = true;
									pickable.FlyToPosition = new Vector3?(vector);
									pickable.Count = ComponentInventoryBase.AcquireItems(componentInventory, pickable.Value, pickable.Count);
									this.m_audio.PlaySound("Audio/PickableCollected", 1f, -0.4f, vector, 6f, false);
									break;
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x040000F1 RID: 241
		public SubsystemTerrain subsystemTerrain;

		// Token: 0x040000F2 RID: 242
		public SubsystemPickables subsystemPickables;

		// Token: 0x040000F3 RID: 243
		public ComponentCreature componentCreature;

		// Token: 0x040000F4 RID: 244
		private ComponentPathfinding m_componentPathfinding;

		// Token: 0x040000F5 RID: 245
		public ComponentMiner m_componentMiner;

		// Token: 0x040000F6 RID: 246
		public bool Activate;

		// Token: 0x040000F7 RID: 247
		public bool CanOpenInventory;

		// Token: 0x040000F8 RID: 248
		public float DetectionDistance;

		// Token: 0x040000F9 RID: 249
		public string SpecificItems;

		// Token: 0x040000FA RID: 250
		public bool IgnoreOrAcept;

		// Token: 0x040000FB RID: 251
		public SubsystemAudio m_audio;

		// Token: 0x040000FC RID: 252
		private HashSet<int> m_specificItemsSet = new HashSet<int>();
	}
}
