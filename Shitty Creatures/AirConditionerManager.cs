using System;
using System.Collections.Generic;
using Game;

namespace Game
{
	// Token: 0x02000103 RID: 259
	public static class AirConditionerManager
	{
		// Token: 0x060007CC RID: 1996 RVA: 0x000306A4 File Offset: 0x0002E8A4
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

		// Token: 0x060007CD RID: 1997 RVA: 0x000306DC File Offset: 0x0002E8DC
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

		// Token: 0x060007CE RID: 1998 RVA: 0x00030724 File Offset: 0x0002E924
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

		public static int GetMaxCoverageRangeAt(double targetX, double targetY, double targetZ)
		{
			GridKey gridKey = GetGridKey(targetX, targetY, targetZ);
			int maxRange = 0;

			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						GridKey key = new GridKey(gridKey.Gx + i, gridKey.Gy + j, gridKey.Gz + k);
						if (_gridIndex.TryGetValue(key, out HashSet<Radar> radars))
						{
							foreach (Radar radar in radars)
							{
								double dx = radar.X - targetX;
								double dy = radar.Y - targetY;
								double dz = radar.Z - targetZ;
								double distSq = dx * dx + dy * dy + dz * dz;
								if (distSq <= radar.Radius * radar.Radius)
								{
									if (radar.Radius > maxRange)
										maxRange = radar.Radius;
								}
							}
						}
					}
				}
			}
			return maxRange;
		}

		// Token: 0x060007CF RID: 1999 RVA: 0x00030858 File Offset: 0x0002EA58
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

		// Token: 0x060007D0 RID: 2000 RVA: 0x000308C8 File Offset: 0x0002EAC8
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

		// Token: 0x060007D1 RID: 2001 RVA: 0x00030934 File Offset: 0x0002EB34
		private static GridKey GetGridKey(double x, double y, double z)
		{
			int gx = (int)Math.Floor(x / 16.0);
			int gy = (int)Math.Floor(y / 16.0);
			int gz = (int)Math.Floor(z / 16.0);
			return new GridKey(gx, gy, gz);
		}

		// Token: 0x060007D2 RID: 2002 RVA: 0x00030980 File Offset: 0x0002EB80
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

		// Token: 0x04000522 RID: 1314
		private const double GridSize = 16.0;

		// Token: 0x04000523 RID: 1315
		private static readonly Dictionary<GridKey, HashSet<Radar>> _gridIndex = new Dictionary<GridKey, HashSet<Radar>>();

		// Token: 0x04000524 RID: 1316
		private static readonly Dictionary<Radar, HashSet<GridKey>> _radarInGrids = new Dictionary<Radar, HashSet<GridKey>>();

		// Token: 0x04000525 RID: 1317
		public static readonly HashSet<Radar> AllRadars = new HashSet<Radar>();
	}
}
