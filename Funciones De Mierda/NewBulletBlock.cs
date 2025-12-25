using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000AE RID: 174
	public abstract class NewBulletBlock : FlatBlock
	{
		// Token: 0x06000432 RID: 1074 RVA: 0x0003BF96 File Offset: 0x0003A196
		public NewBulletBlock(string texturePath0, string texturePath1, float defaultProjectilePower, int defaultTextureSlot = 229, bool disintegratesOnHit = true)
		{
			this.m_textures = new Texture2D[]
			{
				ContentManager.Get<Texture2D>(texturePath0),
				ContentManager.Get<Texture2D>(texturePath1)
			};
			this.m_defaultProjectilePower = defaultProjectilePower;
			this.m_defaultTextureSlot = defaultTextureSlot;
			this.m_disintegratesOnHit = disintegratesOnHit;
		}

		// Token: 0x06000433 RID: 1075 RVA: 0x0003BFD5 File Offset: 0x0003A1D5
		public override void Initialize()
		{
			base.Initialize();
			this.DefaultProjectilePower = this.m_defaultProjectilePower;
			this.DefaultTextureSlot = this.m_defaultTextureSlot;
			this.DisintegratesOnHit = this.m_disintegratesOnHit;
		}

		// Token: 0x06000434 RID: 1076 RVA: 0x0003C004 File Offset: 0x0003A204
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			environmentData = (environmentData ?? BlocksManager.m_defaultEnvironmentData);
			bool flag = environmentData.DrawBlockMode == DrawBlockMode.UI;
			if (flag)
			{
				ShittyBlocksManager.DrawFlatBlock(primitivesRenderer, value, size, ref matrix, this.m_textures[0], color, false, environmentData, true);
			}
			else
			{
				bool flag2 = environmentData.DrawBlockMode == DrawBlockMode.FirstPerson || environmentData.DrawBlockMode == DrawBlockMode.ThirdPerson;
				if (flag2)
				{
					ShittyBlocksManager.DrawImageExtrusionBlock(primitivesRenderer, value, size * 0.5f, ref matrix, this.m_textures[1], color, false, environmentData, true);
				}
				else
				{
					bool stateProjectile = NewBulletBlock.GetStateProjectile(value);
					if (stateProjectile)
					{
						BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.5f, ref matrix, null, color, false, environmentData);
					}
					else
					{
						ShittyBlocksManager.DrawImageExtrusionBlock(primitivesRenderer, value, size * 0.5f, ref matrix, this.m_textures[1], color, false, environmentData, true);
					}
				}
			}
		}

		// Token: 0x06000435 RID: 1077 RVA: 0x0003C0D4 File Offset: 0x0003A2D4
		public static bool GetStateProjectile(int value)
		{
			return (Terrain.ExtractData(value) & 131072) != 0;
		}

		// Token: 0x06000436 RID: 1078 RVA: 0x0003C0F8 File Offset: 0x0003A2F8
		public static int SetStateProjectile(int value, bool hasProjectile)
		{
			int num = Terrain.ExtractData(value);
			num &= -131073;
			if (hasProjectile)
			{
				num |= 131072;
			}
			return Terrain.ReplaceData(value, num);
		}

		// Token: 0x06000437 RID: 1079 RVA: 0x0003C12F File Offset: 0x0003A32F
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x06000438 RID: 1080 RVA: 0x0003C134 File Offset: 0x0003A334
		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, 1f, Color.White, this.GetFaceTextureSlot(4, value));
		}

		// Token: 0x040002FF RID: 767
		public float m_defaultProjectilePower;

		// Token: 0x04000300 RID: 768
		public int m_defaultTextureSlot;

		// Token: 0x04000301 RID: 769
		public bool m_disintegratesOnHit;

		// Token: 0x04000302 RID: 770
		public Texture2D[] m_textures;
	}
}
