using System;
using System.Collections.Generic;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000EB RID: 235
	public class SubsystemRefrigeratorXiaomiBlockBehavior : SubsystemEntityBlockBehavior
	{
		// Token: 0x170000B1 RID: 177
		// (get) Token: 0x060006CE RID: 1742 RVA: 0x0002DA11 File Offset: 0x0002BC11
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex<RefrigeratorXiaomiBlock>(true, true)
				};
			}
		}

		// Token: 0x060006CF RID: 1743 RVA: 0x0002DA23 File Offset: 0x0002BC23
		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_databaseObject = base.Project.GameDatabase.Database.FindDatabaseObject("RefrigeratorXiaomi", base.Project.GameDatabase.EntityTemplateType, true);
		}

		// Token: 0x060006D0 RID: 1744 RVA: 0x0002DA60 File Offset: 0x0002BC60
		public override bool InteractBlockEntity(ComponentBlockEntity blockEntity, ComponentMiner componentMiner)
		{
			if (blockEntity == null || componentMiner.ComponentPlayer == null)
			{
				return false;
			}
			ComponentRefrigeratorXiaomi componentRefrigeratorXiaomi = blockEntity.Entity.FindComponent<ComponentRefrigeratorXiaomi>(true);
			componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget = new RefrigeratorXiaomiWidget(componentMiner.Inventory, componentRefrigeratorXiaomi);
			AudioManager.PlaySound("Audio/Refrigerator/Open Refrigerator", 1f, 0f, 0f);
			return true;
		}

		// Token: 0x060006D1 RID: 1745 RVA: 0x0002DAC0 File Offset: 0x0002BCC0
		public override void OnChunkDiscarding(TerrainChunk chunk)
		{
			Dictionary<Point3, ComponentBlockEntity> blockEntities = this.m_subsystemBlockEntities.m_blockEntities;
			if (blockEntities == null || blockEntities.Count <= 0)
			{
				return;
			}
			foreach (Point3 point in this.m_subsystemBlockEntities.m_blockEntities.Keys)
			{
				if (point.X >= chunk.Origin.X && point.X < chunk.Origin.X + 16 && point.Z >= chunk.Origin.Y && point.Z < chunk.Origin.Y + 16)
				{
					ComponentRefrigeratorXiaomi componentRefrigeratorXiaomi = this.m_subsystemBlockEntities.m_blockEntities[point].Entity.FindComponent<ComponentRefrigeratorXiaomi>();
					if (componentRefrigeratorXiaomi == null || !componentRefrigeratorXiaomi.m_powerOn)
					if (componentRefrigeratorXiaomi == null || !componentRefrigeratorXiaomi.m_powerOn)
					{
						break;
					}
					int blockValue = this.m_subsystemBlockEntities.m_blockEntities[point].BlockValue;
					int blockValue2 = Terrain.ReplaceData(blockValue, RefrigeratorXiaomiBlock.SetIsInCamperVan(Terrain.ExtractData(blockValue), true));
					this.m_subsystemBlockEntities.m_blockEntities[point].BlockValue = blockValue2;
					this.m_modelchangeFlagByCell.Add(point, blockValue);
				}
			}
		}

		// Token: 0x060006D2 RID: 1746 RVA: 0x0002DC10 File Offset: 0x0002BE10
		public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
		{
			Point3 key = new Point3(x, y, z);
			int blockValue;
			if (!this.m_modelchangeFlagByCell.TryGetValue(key, out blockValue))
			{
				return;
			}
			this.m_subsystemBlockEntities.m_blockEntities[key].BlockValue = blockValue;
			this.m_modelchangeFlagByCell.Remove(key);
		}

		// Token: 0x040003DA RID: 986
		public Dictionary<Point3, int> m_modelchangeFlagByCell = new Dictionary<Point3, int>();
	}
}
