using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000BD RID: 189
	public class MusketBlock : Block
	{
		// Token: 0x06000461 RID: 1121 RVA: 0x000182EC File Offset: 0x000164EC
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Musket");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Musket", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Hammer", true).ParentBone);
			this.m_standaloneBlockMeshUnloaded = new BlockMesh();
			this.m_standaloneBlockMeshUnloaded.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			this.m_standaloneBlockMeshUnloaded.AppendModelMeshPart(model.FindMesh("Hammer", true).MeshParts[0], boneAbsoluteTransform2, false, false, false, false, Color.White);
			this.m_standaloneBlockMeshLoaded = new BlockMesh();
			this.m_standaloneBlockMeshLoaded.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			this.m_standaloneBlockMeshLoaded.AppendModelMeshPart(model.FindMesh("Hammer", true).MeshParts[0], Matrix.CreateRotationX(0.7f) * boneAbsoluteTransform2, false, false, false, false, Color.White);
			base.Initialize();
		}

		// Token: 0x06000462 RID: 1122 RVA: 0x00018419 File Offset: 0x00016619
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x06000463 RID: 1123 RVA: 0x0001841C File Offset: 0x0001661C
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			if (MusketBlock.GetHammerState(Terrain.ExtractData(value)))
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshLoaded, color, 2f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshUnloaded, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x06000464 RID: 1124 RVA: 0x0001846C File Offset: 0x0001666C
		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) != this.BlockIndex)
			{
				return true;
			}
			int data = Terrain.ExtractData(oldValue);
			return MusketBlock.SetHammerState(Terrain.ExtractData(newValue), true) != MusketBlock.SetHammerState(data, true);
		}

		// Token: 0x06000465 RID: 1125 RVA: 0x000184A8 File Offset: 0x000166A8
		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 8 & 255;
		}

		// Token: 0x06000466 RID: 1126 RVA: 0x000184B8 File Offset: 0x000166B8
		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= -65281;
			num |= Math.Clamp(damage, 0, 255) << 8;
			return Terrain.ReplaceData(value, num);
		}

		// Token: 0x06000467 RID: 1127 RVA: 0x000184EC File Offset: 0x000166EC
		public static MusketBlock.LoadState GetLoadState(int data)
		{
			return (MusketBlock.LoadState)(data & 3);
		}

		// Token: 0x06000468 RID: 1128 RVA: 0x000184F1 File Offset: 0x000166F1
		public static int SetLoadState(int data, MusketBlock.LoadState loadState)
		{
			return (data & -4) | (int)(loadState & MusketBlock.LoadState.Loaded);
		}

		// Token: 0x06000469 RID: 1129 RVA: 0x000184FB File Offset: 0x000166FB
		public static bool GetHammerState(int data)
		{
			return (data & 4) != 0;
		}

		// Token: 0x0600046A RID: 1130 RVA: 0x00018503 File Offset: 0x00016703
		public static int SetHammerState(int data, bool state)
		{
			return (data & -5) | ((((state > false) ? 1 : 0) << 2) ? 1 : 0);
		}

		// Token: 0x0600046B RID: 1131 RVA: 0x00018510 File Offset: 0x00016710
		public static BulletBlock.BulletType? GetBulletType(int data)
		{
			int num = data >> 4 & 15;
			if (num != 0)
			{
				return new BulletBlock.BulletType?((BulletBlock.BulletType)(num - 1));
			}
			return null;
		}

		// Token: 0x0600046C RID: 1132 RVA: 0x0001853C File Offset: 0x0001673C
		public static int SetBulletType(int data, BulletBlock.BulletType? bulletType)
		{
			int num = (int)((bulletType != null) ? (bulletType.Value + 1) : BulletBlock.BulletType.MusketBall);
			return (data & -241) | (num & 15) << 4;
		}

		// Token: 0x040001E8 RID: 488
		public static int Index = 212;

		// Token: 0x040001E9 RID: 489
		public BlockMesh m_standaloneBlockMeshUnloaded;

		// Token: 0x040001EA RID: 490
		public BlockMesh m_standaloneBlockMeshLoaded;

		// Token: 0x02000460 RID: 1120
		public enum LoadState
		{
			// Token: 0x040018BF RID: 6335
			Empty,
			// Token: 0x040018C0 RID: 6336
			Gunpowder,
			// Token: 0x040018C1 RID: 6337
			Wad,
			// Token: 0x040018C2 RID: 6338
			Loaded
		}
	}
}
