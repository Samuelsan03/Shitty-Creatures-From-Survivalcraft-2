using System;
using System.Collections.Generic;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000C8 RID: 200
	public class SubsystemAirConditionerBlockBehavior : SubsystemBlockBehavior
	{
		// Token: 0x17000088 RID: 136
		// (get) Token: 0x060005F9 RID: 1529 RVA: 0x0002794C File Offset: 0x00025B4C
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex<AirConditionerBlock>(false, false)
				};
			}
		}

		// Token: 0x060005FA RID: 1530 RVA: 0x00027960 File Offset: 0x00025B60
		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			if (componentPlayer.DragHostWidget.IsDragInProgress)
			{
				return false;
			}
			int value = inventory.GetSlotValue(slotIndex);
			int count = inventory.GetSlotCount(slotIndex);
			int data = Terrain.ExtractData(value);
			int temperatureRange = AirConditionerBlock.GetTemperatureRange(data);
			DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditTemperatureDialog(temperatureRange, delegate (int newVoltageLevel)
			{
				int newData = AirConditionerBlock.SetTemperatureRange(data, newVoltageLevel);
				int newValue = Terrain.ReplaceData(value, newData);
				if (newValue == value)
				{
					return;
				}
				inventory.RemoveSlotItems(slotIndex, count);
				inventory.AddSlotItems(slotIndex, newValue, count);
			}));
			return true;
		}

		// Token: 0x060005FB RID: 1531 RVA: 0x000279FC File Offset: 0x00025BFC
		public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
		{
			int data = Terrain.ExtractData(value);
			int temperatureRange = AirConditionerBlock.GetTemperatureRange(data);
			DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditTemperatureDialog(temperatureRange, delegate (int newTemperature)
			{
				int newData = AirConditionerBlock.SetTemperatureRange(data, newTemperature);
				if (newData == data)
				{
					return;
				}
				int newValue = Terrain.ReplaceData(value, newData);
				this.SubsystemTerrain.ChangeCell(x, y, z, newValue, true, null);
			}));
			return true;
		}

		// Token: 0x060005FC RID: 1532 RVA: 0x00027A70 File Offset: 0x00025C70
		public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
		{
			SubsystemAirConditionerBlockBehavior.AddAirConditioner(value, x, y, z);
			int temperatureRange = AirConditionerBlock.GetTemperatureRange(Terrain.ExtractData(value));
			this.ApplyEffect(Math.Min(temperatureRange, 1), x, y, z);
		}

		// Token: 0x060005FD RID: 1533 RVA: 0x00027AA6 File Offset: 0x00025CA6
		public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
		{
			SubsystemAirConditionerBlockBehavior.RemoveAirConditioner(value, x, y, z);
		}

		// Token: 0x060005FE RID: 1534 RVA: 0x00027AB4 File Offset: 0x00025CB4
		public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
		{
			SubsystemAirConditionerBlockBehavior.RemoveAirConditioner(oldValue, x, y, z);
			SubsystemAirConditionerBlockBehavior.AddAirConditioner(value, x, y, z);
			int temperatureRange = AirConditionerBlock.GetTemperatureRange(Terrain.ExtractData(value));
			this.ApplyEffect(Math.Min(temperatureRange, 1), x, y, z);
		}

		// Token: 0x060005FF RID: 1535 RVA: 0x00027AF5 File Offset: 0x00025CF5
		public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
		{
			SubsystemAirConditionerBlockBehavior.AddAirConditioner(value, x, y, z);
		}

		// Token: 0x06000600 RID: 1536 RVA: 0x00027B04 File Offset: 0x00025D04
		public override void OnChunkDiscarding(TerrainChunk chunk)
		{
			List<Radar> list = new List<Radar>();
			foreach (Radar item in AirConditionerManager.AllRadars)
			{
				if (item.X >= chunk.Origin.X && item.X < chunk.Origin.X + 16 && item.Z >= chunk.Origin.Y && item.Z < chunk.Origin.Y + 16)
				{
					list.Add(item);
				}
			}
			foreach (Radar radar in list)
			{
				AirConditionerManager.RemoveRadar(radar);
			}
		}

		// Token: 0x06000601 RID: 1537 RVA: 0x00027BEC File Offset: 0x00025DEC
		public static void AddAirConditioner(int value, int x, int y, int z)
		{
			int temperatureRange = AirConditionerBlock.GetTemperatureRange(Terrain.ExtractData(value));
			AirConditionerManager.AddRadar(new Radar(x, y, z, temperatureRange));
		}

		// Token: 0x06000602 RID: 1538 RVA: 0x00027C14 File Offset: 0x00025E14
		public static void RemoveAirConditioner(int value, int x, int y, int z)
		{
			int temperatureRange = AirConditionerBlock.GetTemperatureRange(Terrain.ExtractData(value));
			AirConditionerManager.RemoveRadar(new Radar(x, y, z, temperatureRange));
		}

		// Token: 0x06000603 RID: 1539 RVA: 0x00027C3C File Offset: 0x00025E3C
		public void ApplyEffect(int range, int x, int y, int z)
		{
			for (int i = -range; i <= range; i++)
			{
				for (int j = -range; j <= range; j++)
				{
					for (int k = -range; k <= range; k++)
					{
						this.ApplyNeighborhoodEffect(x + i, y + j, z + k);
					}
				}
			}
		}

		// Token: 0x06000604 RID: 1540 RVA: 0x00027C80 File Offset: 0x00025E80
		public void ApplyNeighborhoodEffect(int x, int y, int z)
		{
			int cellContents = base.SubsystemTerrain.Terrain.GetCellContents(x, y, z);
			if (cellContents != 8)
			{
				if (cellContents == 61)
				{
					base.SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
					this.m_subsystemParticles.AddParticleSystem(new BurntDebrisParticleSystem(base.SubsystemTerrain, new Vector3((float)x + 0.5f, (float)(y + 1), (float)z + 0.5f)), false);
					return;
				}
			}
			else
			{
				base.SubsystemTerrain.ChangeCell(x, y, z, 2, true, null);
				this.m_subsystemParticles.AddParticleSystem(new BurntDebrisParticleSystem(base.SubsystemTerrain, new Vector3((float)x + 0.5f, (float)(y + 1), (float)z + 0.5f)), false);
			}
		}

		// Token: 0x06000605 RID: 1541 RVA: 0x00027D32 File Offset: 0x00025F32
		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
		}

		// Token: 0x04000374 RID: 884
		public SubsystemParticles m_subsystemParticles;
	}
}