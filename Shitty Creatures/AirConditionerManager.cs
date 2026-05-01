using System;
using System.Collections.Generic;

namespace Game
{
	// Token: 0x020000BB RID: 187
	public static class AirConditionerManager
	{
		// Token: 0x06000598 RID: 1432 RVA: 0x00023910 File Offset: 0x00021B10
		public static void AddRadar(Radar radar)
		{
			if (!AirConditionerManager.AllRadars.Add(radar))
			{
				return;
			}
			HashSet<GridKey> hashSet = AirConditionerManager.CalculateInfluenceGrids(radar);
			AirConditionerManager._radarInGrids[radar] = hashSet;
			AirConditionerManager.AddRadarToGrids(radar, hashSet);
		}

		// Token: 0x06000599 RID: 1433 RVA: 0x00023948 File Offset: 0x00021B48
		public static void RemoveRadar(Radar radar)
		{
			if (!AirConditionerManager.AllRadars.Contains(radar))
			{
				return;
			}
			AirConditionerManager.AllRadars.Remove(radar);
			HashSet<GridKey> grids = AirConditionerManager._radarInGrids[radar];
			AirConditionerManager.RemoveRadarFromGrids(radar, grids);
			AirConditionerManager._radarInGrids.Remove(radar);
		}

		// Token: 0x0600059A RID: 1434 RVA: 0x00023990 File Offset: 0x00021B90
		private static HashSet<GridKey> CalculateInfluenceGrids(Radar radar)
		{
			HashSet<GridKey> hashSet = new HashSet<GridKey>();
			double num = (double)(radar.X - radar.Radius);
			double num2 = (double)(radar.X + radar.Radius);
			double num3 = (double)(radar.Y - radar.Radius);
			double num4 = (double)(radar.Y + radar.Radius);
			double num5 = (double)(radar.Z - radar.Radius);
			double num6 = (double)(radar.Z + radar.Radius);
			int num7 = (int)Math.Floor(num / 16.0);
			int num8 = (int)Math.Floor(num2 / 16.0);
			int num9 = (int)Math.Floor(num3 / 16.0);
			int num10 = (int)Math.Floor(num4 / 16.0);
			int num11 = (int)Math.Floor(num5 / 16.0);
			int num12 = (int)Math.Floor(num6 / 16.0);
			for (int i = num7; i <= num8; i++)
			{
				for (int j = num9; j <= num10; j++)
				{
					for (int k = num11; k <= num12; k++)
					{
						hashSet.Add(new GridKey(i, j, k));
					}
				}
			}
			return hashSet;
		}

		// Token: 0x0600059B RID: 1435 RVA: 0x00023AC4 File Offset: 0x00021CC4
		private static void AddRadarToGrids(Radar radar, IEnumerable<GridKey> grids)
		{
			foreach (GridKey key in grids)
			{
				if (!AirConditionerManager._gridIndex.ContainsKey(key))
				{
					AirConditionerManager._gridIndex[key] = new HashSet<Radar>();
				}
				AirConditionerManager._gridIndex[key].Add(radar);
			}
		}

		// Token: 0x0600059C RID: 1436 RVA: 0x00023B34 File Offset: 0x00021D34
		private static void RemoveRadarFromGrids(Radar radar, IEnumerable<GridKey> grids)
		{
			foreach (GridKey key in grids)
			{
				HashSet<Radar> hashSet;
				if (AirConditionerManager._gridIndex.TryGetValue(key, out hashSet))
				{
					hashSet.Remove(radar);
					if (hashSet.Count == 0)
					{
						AirConditionerManager._gridIndex.Remove(key);
					}
				}
			}
		}

		// Token: 0x0600059D RID: 1437 RVA: 0x00023BA0 File Offset: 0x00021DA0
		private static GridKey GetGridKey(double x, double y, double z)
		{
			int gx = (int)Math.Floor(x / 16.0);
			int gy = (int)Math.Floor(y / 16.0);
			int gz = (int)Math.Floor(z / 16.0);
			return new GridKey(gx, gy, gz);
		}

		// Token: 0x0600059E RID: 1438 RVA: 0x00023BEC File Offset: 0x00021DEC
		public static bool IsInCoverage(double targetX, double targetY, double targetZ)
		{
			GridKey gridKey = AirConditionerManager.GetGridKey(targetX, targetY, targetZ);
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						GridKey key = new GridKey(gridKey.Gx + i, gridKey.Gy + j, gridKey.Gz + k);
						HashSet<Radar> hashSet;
						if (AirConditionerManager._gridIndex.TryGetValue(key, out hashSet))
						{
							foreach (Radar radar in hashSet)
							{
								double num = (double)radar.X - targetX;
								double num2 = (double)radar.Y - targetY;
								double num3 = (double)radar.Z - targetZ;
								if (num * num + num2 * num2 + num3 * num3 <= (double)(radar.Radius * radar.Radius))
								{
									return true;
								}
							}
						}
					}
				}
			}
			return false;
		}

		// Token: 0x04000348 RID: 840
		private const double GridSize = 16.0;

		// Token: 0x04000349 RID: 841
		private static readonly Dictionary<GridKey, HashSet<Radar>> _gridIndex = new Dictionary<GridKey, HashSet<Radar>>();

		// Token: 0x0400034A RID: 842
		private static readonly Dictionary<Radar, HashSet<GridKey>> _radarInGrids = new Dictionary<Radar, HashSet<GridKey>>();

		// Token: 0x0400034B RID: 843
		public static readonly HashSet<Radar> AllRadars = new HashSet<Radar>();
	}
}
