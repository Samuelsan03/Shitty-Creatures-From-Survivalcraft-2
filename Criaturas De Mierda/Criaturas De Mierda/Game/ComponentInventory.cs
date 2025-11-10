using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200016A RID: 362
	public class ComponentInventory : ComponentInventoryBase, IInventory
	{
		// Token: 0x17000110 RID: 272
		// (get) Token: 0x06000A3F RID: 2623 RVA: 0x0003F8EA File Offset: 0x0003DAEA
		// (set) Token: 0x06000A40 RID: 2624 RVA: 0x0003F8F2 File Offset: 0x0003DAF2
		public override int ActiveSlotIndex
		{
			get
			{
				return this.m_activeSlotIndex;
			}
			set
			{
				this.m_activeSlotIndex = Math.Clamp(value, 0, this.VisibleSlotsCount - 1);
			}
		}

		// Token: 0x17000111 RID: 273
		// (get) Token: 0x06000A41 RID: 2625 RVA: 0x0003F909 File Offset: 0x0003DB09
		// (set) Token: 0x06000A42 RID: 2626 RVA: 0x0003F914 File Offset: 0x0003DB14
		public override int VisibleSlotsCount
		{
			get
			{
				return this.m_visibleSlotsCount;
			}
			set
			{
				value = Math.Clamp(value, 0, 10);
				if (value == this.m_visibleSlotsCount)
				{
					return;
				}
				this.m_visibleSlotsCount = value;
				this.ActiveSlotIndex = this.ActiveSlotIndex;
				ComponentFrame componentFrame = base.Entity.FindComponent<ComponentFrame>();
				if (componentFrame != null)
				{
					Vector3 position = componentFrame.Position + new Vector3(0f, 0.5f, 0f);
					Vector3 velocity = 1f * componentFrame.Rotation.GetForwardVector();
					for (int i = this.m_visibleSlotsCount; i < 10; i++)
					{
						ComponentInventoryBase.DropSlotItems(this, i, position, velocity);
					}
				}
			}
		}

		// Token: 0x06000A43 RID: 2627 RVA: 0x0003F9B1 File Offset: 0x0003DBB1
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.ActiveSlotIndex = valuesDictionary.GetValue<int>("ActiveSlotIndex");
		}

		// Token: 0x06000A44 RID: 2628 RVA: 0x0003F9CC File Offset: 0x0003DBCC
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue<int>("ActiveSlotIndex", this.ActiveSlotIndex);
		}

		// Token: 0x06000A45 RID: 2629 RVA: 0x0003F9E7 File Offset: 0x0003DBE7
		public override int GetSlotCapacity(int slotIndex, int value)
		{
			if (slotIndex >= this.VisibleSlotsCount && slotIndex < 10)
			{
				return 0;
			}
			return BlocksManager.Blocks[Terrain.ExtractContents(value)].GetMaxStacking(value);
		}

		// Token: 0x040005D3 RID: 1491
		public int m_activeSlotIndex;

		// Token: 0x040005D4 RID: 1492
		public int m_visibleSlotsCount = 10;

		// Token: 0x040005D5 RID: 1493
		public const int ShortInventorySlotsCount = 10;
	}
}
