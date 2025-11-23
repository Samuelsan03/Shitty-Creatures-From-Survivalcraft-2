using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000006 RID: 6
	[NullableContext(1)]
	[Nullable(0)]
	public class ComponentCreatureCollect : Component, IUpdateable
	{
		// Token: 0x17000002 RID: 2
		// (get) Token: 0x0600001F RID: 31 RVA: 0x00003284 File Offset: 0x00001484
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000020 RID: 32 RVA: 0x00003298 File Offset: 0x00001498
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
				bool flag5 = !flag;
				if (flag5)
				{
					int num;
					bool flag2 = int.TryParse(text, out num);
					bool flag6 = flag2;
					if (flag6)
					{
						bool flag3 = num > 0 && num < BlocksManager.Blocks.Length;
						bool flag7 = flag3;
						if (flag7)
						{
							this.m_specificItemsSet.Add(num);
						}
					}
					else
					{
						Type type = Type.GetType("Game." + text);
						Block block = BlocksManager.GetBlock(type, false, true);
						bool flag4 = block != null;
						bool flag8 = flag4;
						if (flag8)
						{
							this.m_specificItemsSet.Add(block.BlockIndex);
						}
					}
				}
			}
		}

		// Token: 0x06000021 RID: 33 RVA: 0x0000343C File Offset: 0x0000163C
		public void Update(float dt)
		{
			IInventory inventory = this.m_componentMiner.Inventory;
			ComponentInventory componentInventory = base.Entity.FindComponent<ComponentInventory>();
			ComponentChaseBehavior componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			bool flag = !this.Activate;
			bool flag11 = !flag;
			if (flag11)
			{
				foreach (Pickable pickable in this.subsystemPickables.Pickables)
				{
					int item = Terrain.ExtractContents(pickable.Value);
					bool flag2 = this.m_specificItemsSet.Contains(item);
					bool flag3 = this.m_specificItemsSet.Count > 0;
					bool flag12 = flag3;
					if (flag12)
					{
						bool flag4 = this.IgnoreOrAcept && !flag2;
						bool flag13 = flag4;
						if (flag13)
						{
							continue;
						}
						bool flag5 = !this.IgnoreOrAcept && flag2;
						bool flag14 = flag5;
						if (flag14)
						{
							continue;
						}
					}
					TerrainChunk chunkAtCell = this.subsystemTerrain.Terrain.GetChunkAtCell(Terrain.ToCell(pickable.Position.X), Terrain.ToCell(pickable.Position.Z));
					bool flag6 = componentInventory != null && chunkAtCell != null && pickable.FlyToPosition == null && (double)this.componentCreature.ComponentHealth.Health > 0.0 && componentChaseBehavior.Target == null;
					bool flag15 = flag6;
					if (flag15)
					{
						Vector3 vector = this.componentCreature.ComponentBody.Position + new Vector3(0f, 0.8f, 0f);
						float num = (vector - pickable.Position).LengthSquared();
						float num2 = this.DetectionDistance * this.DetectionDistance;
						bool flag7 = num < num2;
						bool flag16 = flag7;
						if (flag16)
						{
							for (int i = 0; i < inventory.SlotsCount; i++)
							{
								int num3 = ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value);
								bool flag8 = num3 >= 0;
								bool flag17 = flag8;
								if (flag17)
								{
									this.m_componentPathfinding.SetDestination(new Vector3?(pickable.Position), 3f, 3.75f, 20, true, false, false, null);
									break;
								}
							}
						}
						bool flag9 = (double)num < 4.0;
						bool flag18 = flag9;
						if (flag18)
						{
							for (int j = 0; j < inventory.SlotsCount; j++)
							{
								int num4 = ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value);
								bool flag10 = num4 >= 0;
								bool flag19 = flag10;
								if (flag19)
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

		// Token: 0x04000022 RID: 34
		public SubsystemTerrain subsystemTerrain;

		// Token: 0x04000023 RID: 35
		public SubsystemPickables subsystemPickables;

		// Token: 0x04000024 RID: 36
		public ComponentCreature componentCreature;

		// Token: 0x04000025 RID: 37
		private ComponentPathfinding m_componentPathfinding;

		// Token: 0x04000026 RID: 38
		public ComponentMiner m_componentMiner;

		// Token: 0x04000027 RID: 39
		public bool Activate;

		// Token: 0x04000028 RID: 40
		public bool CanOpenInventory;

		// Token: 0x04000029 RID: 41
		public float DetectionDistance;

		// Token: 0x0400002A RID: 42
		public string SpecificItems;

		// Token: 0x0400002B RID: 43
		public bool IgnoreOrAcept;

		// Token: 0x0400002C RID: 44
		public SubsystemAudio m_audio;

		// Token: 0x0400002D RID: 45
		private HashSet<int> m_specificItemsSet = new HashSet<int>();
	}
}
