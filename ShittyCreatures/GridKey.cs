using System;

namespace Game
{
	// Token: 0x020000B5 RID: 181
	public readonly struct GridKey : IEquatable<GridKey>
	{
		// Token: 0x17000074 RID: 116
		// (get) Token: 0x06000583 RID: 1411 RVA: 0x00023392 File Offset: 0x00021592
		public int Gx { get; }

		// Token: 0x17000075 RID: 117
		// (get) Token: 0x06000584 RID: 1412 RVA: 0x0002339A File Offset: 0x0002159A
		public int Gy { get; }

		// Token: 0x17000076 RID: 118
		// (get) Token: 0x06000585 RID: 1413 RVA: 0x000233A2 File Offset: 0x000215A2
		public int Gz { get; }

		// Token: 0x06000586 RID: 1414 RVA: 0x000233AA File Offset: 0x000215AA
		public GridKey(int gx, int gy, int gz)
		{
			this.Gx = gx;
			this.Gy = gy;
			this.Gz = gz;
		}

		// Token: 0x06000587 RID: 1415 RVA: 0x000233C1 File Offset: 0x000215C1
		public bool Equals(GridKey other)
		{
			return this.Gx == other.Gx && this.Gy == other.Gy && this.Gz == other.Gz;
		}

		// Token: 0x06000588 RID: 1416 RVA: 0x000233F4 File Offset: 0x000215F4
		public override bool Equals(object obj)
		{
			if (obj is GridKey)
			{
				GridKey other = (GridKey)obj;
				return this.Equals(other);
			}
			return false;
		}

		// Token: 0x06000589 RID: 1417 RVA: 0x00023419 File Offset: 0x00021619
		public override int GetHashCode()
		{
			return HashCode.Combine<int, int, int>(this.Gx, this.Gy, this.Gz);
		}

		// Token: 0x0600058A RID: 1418 RVA: 0x00023432 File Offset: 0x00021632
		public static bool operator ==(GridKey left, GridKey right)
		{
			return left.Equals(right);
		}

		// Token: 0x0600058B RID: 1419 RVA: 0x0002343C File Offset: 0x0002163C
		public static bool operator !=(GridKey left, GridKey right)
		{
			return !(left == right);
		}
	}
}
