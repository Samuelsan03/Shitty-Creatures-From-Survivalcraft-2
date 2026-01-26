using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace WonderfulEra
{
	// Token: 0x020000BC RID: 188
	public class ComponentTradeFlushInventory : ComponentInventory, IUpdateable
	{
		// Token: 0x17000069 RID: 105
		// (get) Token: 0x060005D5 RID: 1493 RVA: 0x000212D9 File Offset: 0x0001F4D9
		public UpdateOrder UpdateOrder
		{
			get
			{
				return (UpdateOrder)202;
			}
		}

		// Token: 0x1700006A RID: 106
		// (get) Token: 0x060005D6 RID: 1494 RVA: 0x000212E0 File Offset: 0x0001F4E0
		public int DisplaySlotCount
		{
			get
			{
				return this.SlotsCount - 2;
			}
		}

		// Token: 0x1700006B RID: 107
		// (get) Token: 0x060005D7 RID: 1495 RVA: 0x000212EA File Offset: 0x0001F4EA
		public int TradeResultSlotIndex
		{
			get
			{
				return this.SlotsCount - 1;
			}
		}

		// Token: 0x1700006C RID: 108
		// (get) Token: 0x060005D8 RID: 1496 RVA: 0x000212F4 File Offset: 0x0001F4F4
		public int TradeSellSlotIndex
		{
			get
			{
				return this.SlotsCount - 2;
			}
		}

		// Token: 0x1700006D RID: 109
		// (get) Token: 0x060005D9 RID: 1497 RVA: 0x000212FE File Offset: 0x0001F4FE
		// (set) Token: 0x060005DA RID: 1498 RVA: 0x00021306 File Offset: 0x0001F506
		public float Ratio { get; set; }

		// Token: 0x1700006E RID: 110
		// (get) Token: 0x060005DB RID: 1499 RVA: 0x0002130F File Offset: 0x0001F50F
		// (set) Token: 0x060005DC RID: 1500 RVA: 0x00021317 File Offset: 0x0001F517
		public override int ActiveSlotIndex
		{
			get
			{
				return this.m_activeSlotIndex;
			}
			set
			{
				if (value < 0 || value >= this.SlotsCount - 2)
				{
					return;
				}
				this.m_activeSlotIndex = value;
			}
		}

		// Token: 0x060005DD RID: 1501 RVA: 0x00021330 File Offset: 0x0001F530
		public bool CanTrade(IInventory toInventory, out ComponentTradeFlushInventory.Price price)
		{
			int slotValue = this.GetSlotValue(this.ActiveSlotIndex);
			int sellPrice;
			this.PriceListDic.TryGetValue(slotValue, out sellPrice);
			price = new ComponentTradeFlushInventory.Price
			{
				CurrencyValue = IronIngotBlock.Index,
				SellPrice = sellPrice
			};
			int slotCount = this.GetSlotCount(this.ActiveSlotIndex);
			int num = (int)MathUtils.Round((float)slotCount * this.Ratio * (float)price.SellPrice);
			return slotCount > 0 && this.GetSlotValue(this.TradeSellSlotIndex) == price.CurrencyValue && this.GetSlotCount(this.TradeSellSlotIndex) >= num;
		}

		// Token: 0x060005DE RID: 1502 RVA: 0x000213CC File Offset: 0x0001F5CC
		public bool TradeProcess(int sellValue, int sellCount, ComponentTradeFlushInventory.Price price)
		{
			int slotValue = this.GetSlotValue(this.TradeResultSlotIndex);
			if (slotValue != 0 && (sellValue != slotValue || sellCount + this.GetSlotCount(this.TradeResultSlotIndex) > this.GetSlotCapacity(this.TradeResultSlotIndex, slotValue)))
			{
				return false;
			}
			this.InnerRemoveSlotItems(this.ActiveSlotIndex, sellCount);
			this.InnerRemoveSlotItems(this.TradeSellSlotIndex, (int)MathUtils.Round((float)sellCount * this.Ratio * (float)price.SellPrice));
			this.AddSlotItems(this.TradeResultSlotIndex, sellValue, sellCount);
			return true;
		}

		// Token: 0x060005DF RID: 1503 RVA: 0x0002144D File Offset: 0x0001F64D
		public override int RemoveSlotItems(int slotIndex, int count)
		{
			if (slotIndex >= this.DisplaySlotCount)
			{
				return base.RemoveSlotItems(slotIndex, count);
			}
			return 0;
		}

		// Token: 0x060005E0 RID: 1504 RVA: 0x00021462 File Offset: 0x0001F662
		private int InnerRemoveSlotItems(int slotIndex, int count)
		{
			if (slotIndex >= 0 && slotIndex < this.m_slots.Count)
			{
				ComponentInventoryBase.Slot slot = this.m_slots[slotIndex];
				count = MathUtils.Min(count, this.GetSlotCount(slotIndex));
				slot.Count -= count;
				return count;
			}
			return 0;
		}

		// Token: 0x060005E1 RID: 1505 RVA: 0x000214A4 File Offset: 0x0001F6A4
		public void Update(float dt)
		{
			this.m_currentTime += (double)this.m_subsystemTime.m_gameTimeDelta;
			if (this.m_currentTime < this.m_nextCheckTime)
			{
				return;
			}
			if (this.DisplaySlotCount <= 0 || this.ValidBlockNumInfos.Count <= 0)
			{
				return;
			}
			int num = this.m_random.Int(0, this.DisplaySlotCount - 1);
			if (this.m_slots[num] != null && this.m_slots[num].Count > 0)
			{
				this.m_nextCheckTime = this.m_currentTime + this.m_updateInterval;
				return;
			}
			this.Ratio = Math.Clamp(this.Ratio * this.m_random.Float(0.9f, 1.2f), 0.5f, 2f);
			int index = this.m_random.Int(0, this.ValidBlockNumInfos.Count - 1);
			ComponentTradeFlushInventory.BlockNumInfo blockNumInfo = this.ValidBlockNumInfos[index];
			this.AddSlotItems(num, blockNumInfo.blockValue, blockNumInfo.num);
			this.m_nextCheckTime = this.m_currentTime + this.m_updateInterval;
		}

		// Token: 0x060005E2 RID: 1506 RVA: 0x000215BA File Offset: 0x0001F7BA
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<double>("CurrentTime", this.m_currentTime);
			valuesDictionary.SetValue<double>("NextCheckTime", this.m_nextCheckTime);
			valuesDictionary.SetValue<float>("Ratio", this.Ratio);
			base.Save(valuesDictionary, entityToIdMap);
		}

		// Token: 0x060005E3 RID: 1507 RVA: 0x000215F8 File Offset: 0x0001F7F8
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>();
			this.m_updateInterval = Math.Max(10.0, double.Parse(valuesDictionary.GetValue<string>("UpdateInterval", "180")));
			this.m_currentTime = valuesDictionary.GetValue<double>("CurrentTime", 0.0);
			this.m_nextCheckTime = valuesDictionary.GetValue<double>("NextCheckTime", 0.0);
			this.Ratio = valuesDictionary.GetValue<float>("Ratio", 1f);
			foreach (string text in valuesDictionary.GetValue<string>("ValidBlocks").Split(';', StringSplitOptions.None).ToList<string>())
			{
				string[] array = text.Split(':', StringSplitOptions.None);
				Block block = BlocksManager.GetBlock(array[0], false);
				if (block != null)
				{
					string[] array2 = array[1].Split(',', StringSplitOptions.None);
					int num = (array2.Length == 1) ? int.Parse(array2[0]) : this.m_random.Int(int.Parse(array2[0]), int.Parse(array2[1]));
					this.ValidBlockNumInfos.Add(this.ToBlockNumInfo(block.BlockIndex, num));
					this.PriceListDic.Add(block.BlockIndex, int.Parse(array[2]));
					Log.Information(block.BlockIndex);
				}
			}
			EggBlock block2 = BlocksManager.GetBlock<EggBlock>(false, false);
			this.ValidBlockNumInfos.Add(this.ToBlockNumInfo(this.MakeEggValue("Pirate_Normal_Tame", block2), this.m_random.Int(1, 2)));
			this.ValidBlockNumInfos.Add(this.ToBlockNumInfo(this.MakeEggValue("Pirate_Elite_Tame", block2), 1));
			this.ValidBlockNumInfos.Add(this.ToBlockNumInfo(Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.DiamondArrow)), this.m_random.Int(4, 16)));
			this.ValidBlockNumInfos.Add(this.ToBlockNumInfo(Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.ExplosiveArrow)), this.m_random.Int(4, 16)));
			this.PriceListDic.Add(this.MakeEggValue("Pirate_Normal_Tame", block2), 20);
			this.PriceListDic.Add(this.MakeEggValue("Pirate_Elite_Tame", block2), 40);
			this.PriceListDic.Add(Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.DiamondArrow)), 1);
			this.PriceListDic.Add(Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.ExplosiveArrow)), 1);
		}

		// Token: 0x060005E4 RID: 1508 RVA: 0x000218A0 File Offset: 0x0001FAA0
		public ComponentTradeFlushInventory.BlockNumInfo ToBlockNumInfo(int blockValue, int num)
		{
			return new ComponentTradeFlushInventory.BlockNumInfo
			{
				blockValue = blockValue,
				num = num
			};
		}

		// Token: 0x060005E5 RID: 1509 RVA: 0x000218C8 File Offset: 0x0001FAC8
		public int MakeEggValue(string templateName, EggBlock eggBlock)
		{
			EggBlock.EggType eggTypeByCreatureTemplateName = eggBlock.GetEggTypeByCreatureTemplateName(templateName);
			return Terrain.MakeBlockValue(EggBlock.Index, 0, EggBlock.SetIsLaid(EggBlock.SetEggType(0, eggTypeByCreatureTemplateName.EggTypeIndex), false));
		}

		// Token: 0x04000372 RID: 882
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000373 RID: 883
		private double m_updateInterval;

		// Token: 0x04000374 RID: 884
		private double m_currentTime;

		// Token: 0x04000375 RID: 885
		private double m_nextCheckTime;

		// Token: 0x04000376 RID: 886
		public Dictionary<int, int> PriceListDic = new Dictionary<int, int>();

		// Token: 0x04000377 RID: 887
		public List<ComponentTradeFlushInventory.BlockNumInfo> ValidBlockNumInfos = new List<ComponentTradeFlushInventory.BlockNumInfo>();

		// Token: 0x02000162 RID: 354
		public struct Price
		{
			// Token: 0x04000692 RID: 1682
			public int CurrencyValue;

			// Token: 0x04000693 RID: 1683
			public int SellPrice;
		}

		// Token: 0x02000163 RID: 355
		public struct BlockNumInfo
		{
			// Token: 0x04000694 RID: 1684
			public int blockValue;

			// Token: 0x04000695 RID: 1685
			public int num;
		}
	}
}
