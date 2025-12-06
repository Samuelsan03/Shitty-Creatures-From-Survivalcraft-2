using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x02000075 RID: 117
	public class FlameThrowerBlock : Block
	{
		// Token: 0x06000352 RID: 850 RVA: 0x0000CC64 File Offset: 0x0000AE64
		public override void Initialize()
		{
			this.m_texture1 = ContentManager.Get<Texture2D>("Textures/FlameThrower");
			Model model = ContentManager.Get<Model>("Models/FlameThrower");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Body", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Switch", true).ParentBone);
			this.m_standaloneBlockMeshUnloaded = new BlockMesh();
			this.m_standaloneBlockMeshUnloaded.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			this.m_standaloneBlockMeshUnloaded.AppendModelMeshPart(model.FindMesh("Switch", true).MeshParts[0], boneAbsoluteTransform2, false, false, false, false, Color.White);
			this.m_standaloneBlockMeshLoaded = new BlockMesh();
			this.m_standaloneBlockMeshLoaded.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			this.m_standaloneBlockMeshLoaded.AppendModelMeshPart(model.FindMesh("Switch", true).MeshParts[0], Matrix.CreateRotationZ(1.5707964f) * boneAbsoluteTransform2, false, false, false, false, Color.White);
			base.Initialize();
		}

		// Token: 0x06000353 RID: 851 RVA: 0x0000CDA1 File Offset: 0x0000AFA1
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x06000354 RID: 852 RVA: 0x0000CDA4 File Offset: 0x0000AFA4
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			if (FlameThrowerBlock.GetSwitchState(Terrain.ExtractData(value)))
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshLoaded, this.m_texture1, color, 2f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshUnloaded, this.m_texture1, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x06000355 RID: 853 RVA: 0x0000CDFD File Offset: 0x0000AFFD
		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, this.DestructionDebrisScale, Color.White, this.GetFaceTextureSlot(0, value), this.m_texture1);
		}

		// Token: 0x06000356 RID: 854 RVA: 0x0000CE24 File Offset: 0x0000B024
		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) != this.BlockIndex)
			{
				return true;
			}
			int data = Terrain.ExtractData(oldValue);
			return FlameThrowerBlock.SetSwitchState(Terrain.ExtractData(newValue), true) != FlameThrowerBlock.SetSwitchState(data, true);
		}

		// Token: 0x06000357 RID: 855 RVA: 0x0000CE60 File Offset: 0x0000B060
		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 8 & 255;
		}

		// Token: 0x06000358 RID: 856 RVA: 0x0000CE70 File Offset: 0x0000B070
		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= -65281;
			num |= Math.Clamp(damage, 0, 255) << 8;
			return Terrain.ReplaceData(value, num);
		}

		// Token: 0x06000359 RID: 857 RVA: 0x0000CEA4 File Offset: 0x0000B0A4
		public static FlameThrowerBlock.LoadState GetLoadState(int data)
		{
			return (FlameThrowerBlock.LoadState)(data & 3);
		}

		// Token: 0x0600035A RID: 858 RVA: 0x0000CEA9 File Offset: 0x0000B0A9
		public static int SetLoadState(int data, FlameThrowerBlock.LoadState loadState)
		{
			return (data & -4) | (int)(loadState & FlameThrowerBlock.LoadState.Loaded);
		}

		// Token: 0x0600035B RID: 859 RVA: 0x0000CEB3 File Offset: 0x0000B0B3
		public static bool GetSwitchState(int data)
		{
			return (data & 4) != 0;
		}

		// Token: 0x0600035C RID: 860 RVA: 0x0000CEBB File Offset: 0x0000B0BB
		public static int SetSwitchState(int data, bool state)
		{
			return (data & -5) | (state ? 4 : 0);
		}

		// Token: 0x0600035D RID: 861 RVA: 0x0000CEC8 File Offset: 0x0000B0C8
		public static FlameBulletBlock.FlameBulletType? GetBulletType(int data)
		{
			int num = data >> 4 & 15;
			if (num != 0)
			{
				return new FlameBulletBlock.FlameBulletType?((FlameBulletBlock.FlameBulletType)(num - 1));
			}
			return null;
		}

		// Token: 0x0600035E RID: 862 RVA: 0x0000CEF4 File Offset: 0x0000B0F4
		public static int SetBulletType(int data, FlameBulletBlock.FlameBulletType? type)
		{
			int num = (int)((type != null) ? (type.Value + 1) : FlameBulletBlock.FlameBulletType.Flame);
			return (data & -241) | (num & 15) << 4;
		}

		// Token: 0x0600035F RID: 863 RVA: 0x0000CF25 File Offset: 0x0000B125
		public static int GetLoadCount(int value)
		{
			return (value & 15360) >> 10;
		}

		// Token: 0x06000360 RID: 864 RVA: 0x0000CF31 File Offset: 0x0000B131
		public static int SetLoadCount(int value, int count)
		{
			return value ^ ((value ^ count << 10) & 15360);
		}

		// Token: 0x0400010A RID: 266
		public static int Index = 738;

		// Token: 0x0400010B RID: 267
		public BlockMesh m_standaloneBlockMeshUnloaded;

		// Token: 0x0400010C RID: 268
		public BlockMesh m_standaloneBlockMeshLoaded;

		// Token: 0x0400010D RID: 269
		public Texture2D m_texture1;

		// Token: 0x02000126 RID: 294
		public enum LoadState
		{
			// Token: 0x040004EE RID: 1262
			Empty,
			// Token: 0x040004EF RID: 1263
			Loaded
		}
	}
}
