using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentFlushInventory : ComponentInventoryBase, IUpdateable
	{
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>();
			foreach (Block item in from blockStr in valuesDictionary.GetValue<string>("ValidBlocks").Split(',', StringSplitOptions.None).ToList<string>()
								   select BlocksManager.GetBlock(blockStr, false) into block
								   where block != null
								   select block)
			{
				this.m_validBlocks.Add(item);
			}
			this.m_updateInterval = Math.Max(10.0, double.Parse(valuesDictionary.GetValue<string>("UpdateInterval", "180")));
			this.m_currentTime = valuesDictionary.GetValue<double>("CurrentTime", 0.0);
			this.m_nextCheckTime = valuesDictionary.GetValue<double>("NextCheckTime", 0.0);
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return (UpdateOrder)202;
			}
		}

		public void Update(float dt)
		{
			this.m_currentTime += (double)this.m_subsystemTime.m_gameTimeDelta;
			if (this.m_currentTime < this.m_nextCheckTime)
			{
				return;
			}
			if (this.SlotsCount <= 0 || this.m_validBlocks.Count <= 0)
			{
				return;
			}
			if (base.Entity.ValuesDictionary.DatabaseObject.Name.Equals("Elk_Car"))
			{
				this.AddBitchItems();
				this.m_nextCheckTime = this.m_currentTime + this.m_updateInterval;
				return;
			}
			int num = this.m_random.Int(0, this.SlotsCount);
			if (this.m_slots[num] != null && this.m_slots[num].Count > 0)
			{
				this.m_nextCheckTime = this.m_currentTime + this.m_updateInterval;
				return;
			}
			int index = this.m_random.Int(0, this.m_validBlocks.Count);
			Block block = this.m_validBlocks[index];
			int count = 1;
			this.AddSlotItems(num, Terrain.MakeBlockValue(block.BlockIndex, 0, 0), count);
			this.m_nextCheckTime = this.m_currentTime + this.m_updateInterval;
		}

		private void AddBitchItems()
		{
			List<Block> list = this.SelectRandomEleWithPartialShuffle(this.m_validBlocks, this.m_random.Int(this.SlotsCount / 3, this.SlotsCount));
			int index = 0;
			foreach (Block block in list)
			{
				if (this.m_slots[index] != null && this.m_slots[index].Count > 0)
				{
					this.m_nextCheckTime = this.m_currentTime + this.m_updateInterval;
					break;
				}
				if (!this.m_random.Bool(0.9f))
				{
					int count = 1;
					this.AddSlotItems(index++, Terrain.MakeBlockValue(block.BlockIndex, 0, 0), count);
				}
			}
		}

		public List<Block> SelectRandomEleWithPartialShuffle(List<Block> list, int count)
		{
			List<Block> list2 = new List<Block>(list);
			int num = Math.Min(count, list2.Count);
			for (int i = 0; i < num; i++)
			{
				int num2 = this.m_random.Int(i, list2.Count);
				List<Block> list3 = list2;
				int index = i;
				List<Block> list4 = list2;
				int index2 = num2;
				Block value = list2[num2];
				Block value2 = list2[i];
				list3[index] = value;
				list4[index2] = value2;
			}
			return list2.Take(num).ToList<Block>();
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<double>("CurrentTime", this.m_currentTime);
			valuesDictionary.SetValue<double>("NextCheckTime", this.m_nextCheckTime);
			base.Save(valuesDictionary, entityToIdMap);
		}

		public List<Block> m_validBlocks = new List<Block>();

		private SubsystemTime m_subsystemTime;

		private double m_updateInterval;

		private double m_currentTime;

		private double m_nextCheckTime;
	}
}