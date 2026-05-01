using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x0200008A RID: 138
	public class FlameThrowerBlock : Block
	{
		// Token: 0x060003C4 RID: 964 RVA: 0x0000E260 File Offset: 0x0000C460
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

		// Token: 0x060003C5 RID: 965 RVA: 0x0000E39D File Offset: 0x0000C59D
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x060003C6 RID: 966 RVA: 0x0000E3A0 File Offset: 0x0000C5A0
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			if (FlameThrowerBlock.GetSwitchState(Terrain.ExtractData(value)))
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshLoaded, this.m_texture1, color, 2f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshUnloaded, this.m_texture1, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x060003C7 RID: 967 RVA: 0x0000E3F9 File Offset: 0x0000C5F9
		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, this.DestructionDebrisScale, Color.White, this.GetFaceTextureSlot(0, value), this.m_texture1);
		}

		// Token: 0x060003C8 RID: 968 RVA: 0x0000E420 File Offset: 0x0000C620
		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) != this.BlockIndex)
			{
				return true;
			}
			int data = Terrain.ExtractData(oldValue);
			return FlameThrowerBlock.SetSwitchState(Terrain.ExtractData(newValue), true) != FlameThrowerBlock.SetSwitchState(data, true);
		}

		// Token: 0x060003C9 RID: 969 RVA: 0x0000E45C File Offset: 0x0000C65C
		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 8 & 255;
		}

		// Token: 0x060003CA RID: 970 RVA: 0x0000E46C File Offset: 0x0000C66C
		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= -65281;
			num |= Math.Clamp(damage, 0, 255) << 8;
			return Terrain.ReplaceData(value, num);
		}

		// Token: 0x060003CB RID: 971 RVA: 0x0000E4A0 File Offset: 0x0000C6A0
		public static FlameThrowerBlock.LoadState GetLoadState(int data)
		{
			return (FlameThrowerBlock.LoadState)(data & 3);
		}

		// Token: 0x060003CC RID: 972 RVA: 0x0000E4A5 File Offset: 0x0000C6A5
		public static int SetLoadState(int data, FlameThrowerBlock.LoadState loadState)
		{
			return (data & -4) | (int)(loadState & FlameThrowerBlock.LoadState.Loaded);
		}

		// Token: 0x060003CD RID: 973 RVA: 0x0000E4AF File Offset: 0x0000C6AF
		public static bool GetSwitchState(int data)
		{
			return (data & 4) != 0;
		}

		public static int SetSwitchState(int data, bool state)
		{
			return (data & ~4) | (state ? 4 : 0);
		}

		// Token: 0x060003CF RID: 975 RVA: 0x0000E4C4 File Offset: 0x0000C6C4
		public static FlameBulletBlock.FlameBulletType? GetBulletType(int data)
		{
			int num = data >> 4 & 15;
			if (num != 0)
			{
				return new FlameBulletBlock.FlameBulletType?((FlameBulletBlock.FlameBulletType)(num - 1));
			}
			return null;
		}

		// Token: 0x060003D0 RID: 976 RVA: 0x0000E4F0 File Offset: 0x0000C6F0
		public static int SetBulletType(int data, FlameBulletBlock.FlameBulletType? type)
		{
			int num = (int)((type != null) ? (type.Value + 1) : FlameBulletBlock.FlameBulletType.Flame);
			return (data & -241) | (num & 15) << 4;
		}

		// Token: 0x060003D1 RID: 977 RVA: 0x0000E521 File Offset: 0x0000C721
		public static int GetLoadCount(int value)
		{
			return (value & 15360) >> 10;
		}

		// Token: 0x060003D2 RID: 978 RVA: 0x0000E52D File Offset: 0x0000C72D
		public static int SetLoadCount(int value, int count)
		{
			return value ^ ((value ^ count << 10) & 15360);
		}

		// Token: 0x04000150 RID: 336
		public static int Index = 319;

		// Token: 0x04000151 RID: 337
		public BlockMesh m_standaloneBlockMeshUnloaded;

		// Token: 0x04000152 RID: 338
		public BlockMesh m_standaloneBlockMeshLoaded;

		// Token: 0x04000153 RID: 339
		public Texture2D m_texture1;

		// Token: 0x0200014C RID: 332
		public enum LoadState
		{
			// Token: 0x0400064B RID: 1611
			Empty,
			// Token: 0x0400064C RID: 1612
			Loaded
		}
	}
}
