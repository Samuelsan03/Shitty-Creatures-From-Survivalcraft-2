using System;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x02000078 RID: 120
	public abstract class ShittyBlocks : Block
	{
		// Token: 0x0600037D RID: 893 RVA: 0x0000D963 File Offset: 0x0000BB63
		public override void Initialize()
		{
			this.m_texture = ContentManager.Get<Texture2D>("Textures/ShittyTextures");
			base.Initialize();
		}

		// Token: 0x0600037E RID: 894 RVA: 0x0000D97B File Offset: 0x0000BB7B
		public virtual float GetDefaultWaterValue(int value)
		{
			return 0f;
		}

		// Token: 0x0600037F RID: 895 RVA: 0x0000D982 File Offset: 0x0000BB82
		public virtual float GetToxicityValue(int value)
		{
			return 0f;
		}

		// Token: 0x06000380 RID: 896 RVA: 0x0000D989 File Offset: 0x0000BB89
		public static bool GetPesticide(int data)
		{
			return (data >> 15 & 1) == 1;
		}

		// Token: 0x06000381 RID: 897 RVA: 0x0000D994 File Offset: 0x0000BB94
		public static int SetPesticide(int data, bool flag)
		{
			return (data & -32769) | (flag ? 32768 : 0);
		}

		// Token: 0x0400011E RID: 286
		public Texture2D m_texture;
	}
}
