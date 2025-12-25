using System;
using System.Runtime.CompilerServices;

namespace Game
{
	// Token: 0x020000B4 RID: 180
	public struct Radar : IEquatable<Radar>
	{
		// Token: 0x17000070 RID: 112
		// (get) Token: 0x06000574 RID: 1396 RVA: 0x0002324E File Offset: 0x0002144E
		// (set) Token: 0x06000575 RID: 1397 RVA: 0x00023256 File Offset: 0x00021456
		public int X { readonly get; set; }

		// Token: 0x17000071 RID: 113
		// (get) Token: 0x06000576 RID: 1398 RVA: 0x0002325F File Offset: 0x0002145F
		// (set) Token: 0x06000577 RID: 1399 RVA: 0x00023267 File Offset: 0x00021467
		public int Y { readonly get; set; }

		// Token: 0x17000072 RID: 114
		// (get) Token: 0x06000578 RID: 1400 RVA: 0x00023270 File Offset: 0x00021470
		// (set) Token: 0x06000579 RID: 1401 RVA: 0x00023278 File Offset: 0x00021478
		public int Z { readonly get; set; }

		// Token: 0x17000073 RID: 115
		// (get) Token: 0x0600057A RID: 1402 RVA: 0x00023281 File Offset: 0x00021481
		// (set) Token: 0x0600057B RID: 1403 RVA: 0x00023289 File Offset: 0x00021489
		public int Radius { readonly get; set; }

		// Token: 0x0600057C RID: 1404 RVA: 0x00023292 File Offset: 0x00021492
		public Radar(int x, int y, int z, int radius)
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
			this.Radius = radius;
		}

		// Token: 0x0600057D RID: 1405 RVA: 0x000232B1 File Offset: 0x000214B1
		public override int GetHashCode()
		{
			return this.X + this.Y + this.Z;
		}

		// Token: 0x0600057E RID: 1406 RVA: 0x000232C8 File Offset: 0x000214C8
		public override bool Equals(object obj)
		{
			if (obj is Radar)
			{
				Radar other = (Radar)obj;
				return this.Equals(other);
			}
			return false;
		}

		// Token: 0x0600057F RID: 1407 RVA: 0x000232ED File Offset: 0x000214ED
		public bool Equals(Radar other)
		{
			return other.X == this.X && other.Y == this.Y && other.Z == this.Z;
		}

		// Token: 0x06000580 RID: 1408 RVA: 0x00023320 File Offset: 0x00021520
		public override string ToString()
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 3);
			defaultInterpolatedStringHandler.AppendFormatted<int>(this.X);
			defaultInterpolatedStringHandler.AppendLiteral(",");
			defaultInterpolatedStringHandler.AppendFormatted<int>(this.Y);
			defaultInterpolatedStringHandler.AppendLiteral(",");
			defaultInterpolatedStringHandler.AppendFormatted<int>(this.Z);
			return defaultInterpolatedStringHandler.ToStringAndClear();
		}

		// Token: 0x06000581 RID: 1409 RVA: 0x0002337C File Offset: 0x0002157C
		public static bool operator ==(Radar left, Radar right)
		{
			return left.Equals(right);
		}

		// Token: 0x06000582 RID: 1410 RVA: 0x00023386 File Offset: 0x00021586
		public static bool operator !=(Radar left, Radar right)
		{
			return !(left == right);
		}
	}
}
