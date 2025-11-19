using System;
using Engine;
using Game;

// Token: 0x02000003 RID: 3
public static class InventoryExtension
{
	// Token: 0x06000007 RID: 7 RVA: 0x00002844 File Offset: 0x00000A44
	public static void TryAddItems(this IInventory inventory, int value, int count)
	{
		int num = 0;
		while (num < inventory.SlotsCount && count > 0)
		{
			bool flag = inventory.GetSlotValue(num) == value;
			if (flag)
			{
				int num2 = MathUtils.Min(count, inventory.GetSlotCapacity(num, value) - inventory.GetSlotCount(num));
				bool flag2 = num2 > 0;
				if (flag2)
				{
					inventory.AddSlotItems(num, value, num2);
					count -= num2;
				}
			}
			num++;
		}
		int num3 = 0;
		while (num3 < inventory.SlotsCount && count > 0)
		{
			bool flag3 = inventory.GetSlotCount(num3) == 0;
			if (flag3)
			{
				int num4 = MathUtils.Min(count, inventory.GetSlotCapacity(num3, value));
				bool flag4 = num4 > 0;
				if (flag4)
				{
					inventory.AddSlotItems(num3, value, num4);
					count -= num4;
				}
			}
			num3++;
		}
	}
}
