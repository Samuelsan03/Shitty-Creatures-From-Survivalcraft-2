using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x02000077 RID: 119
	public class RepeatCrossbowBlock : Block
	{
		// Token: 0x0600036F RID: 879 RVA: 0x0000D4C8 File Offset: 0x0000B6C8
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/RepeatCrossbow");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Body", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("BowRelaxed", true).ParentBone);
			Matrix boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringRelaxed", true).ParentBone);
			Matrix boneAbsoluteTransform4 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("BowTensed", true).ParentBone);
			Matrix boneAbsoluteTransform5 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringTensed", true).ParentBone);
			BlockMesh blockMesh = new BlockMesh();
			blockMesh.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			blockMesh.AppendModelMeshPart(model.FindMesh("BowRelaxed", true).MeshParts[0], boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			blockMesh.AppendModelMeshPart(model.FindMesh("StringRelaxed", true).MeshParts[0], boneAbsoluteTransform3 * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			BlockMesh blockMesh2 = new BlockMesh();
			blockMesh2.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			blockMesh2.AppendModelMeshPart(model.FindMesh("BowTensed", true).MeshParts[0], boneAbsoluteTransform4 * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			blockMesh2.AppendModelMeshPart(model.FindMesh("StringTensed", true).MeshParts[0], boneAbsoluteTransform5 * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
			for (int i = 0; i < 16; i++)
			{
				float factor = (float)i / 15f;
				this.m_standaloneBlockMeshes[i] = new BlockMesh();
				this.m_standaloneBlockMeshes[i].AppendBlockMesh(blockMesh);
				this.m_standaloneBlockMeshes[i].BlendBlockMesh(blockMesh2, factor);
			}
			this.arrowBlock = BlocksManager.GetBlockGeneral<RepeatArrowBlock>(false);
			base.Initialize();
		}

		// Token: 0x06000370 RID: 880 RVA: 0x0000D761 File Offset: 0x0000B961
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x06000371 RID: 881 RVA: 0x0000D764 File Offset: 0x0000B964
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int data = Terrain.ExtractData(value);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshes[draw], color, 2f * size, ref matrix, environmentData);
			if (arrowType != null)
			{
				Matrix matrix2 = Matrix.CreateRotationX(-1.5707964f) * Matrix.CreateTranslation(0f, 0.2f * size, -0.09f * size) * matrix;
				int value2 = Terrain.MakeBlockValue(this.arrowBlock.BlockIndex, 0, RepeatArrowBlock.SetArrowType(0, arrowType.Value));
				this.arrowBlock.DrawBlock(primitivesRenderer, value2, color, size, ref matrix2, environmentData);
			}
		}

		// Token: 0x06000372 RID: 882 RVA: 0x0000D811 File Offset: 0x0000BA11
		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 8 & 255;
		}

		// Token: 0x06000373 RID: 883 RVA: 0x0000D824 File Offset: 0x0000BA24
		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= -65281;
			num |= Math.Clamp(damage, 0, 255) << 8;
			return Terrain.ReplaceData(value, num);
		}

		// Token: 0x06000374 RID: 884 RVA: 0x0000D858 File Offset: 0x0000BA58
		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			int num = Terrain.ExtractContents(oldValue);
			int data = Terrain.ExtractData(oldValue);
			int data2 = Terrain.ExtractData(newValue);
			if (num == this.BlockIndex)
			{
				RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
				RepeatArrowBlock.ArrowType? arrowType2 = RepeatCrossbowBlock.GetArrowType(data2);
				return !(arrowType.GetValueOrDefault() == arrowType2.GetValueOrDefault() & arrowType != null == (arrowType2 != null));
			}
			return true;
		}

		// Token: 0x06000375 RID: 885 RVA: 0x0000D8B8 File Offset: 0x0000BAB8
		public static RepeatArrowBlock.ArrowType? GetArrowType(int data)
		{
			int num = data >> 4 & 15;
			if (num != 0)
			{
				return new RepeatArrowBlock.ArrowType?((RepeatArrowBlock.ArrowType)(num - 1));
			}
			return null;
		}

		// Token: 0x06000376 RID: 886 RVA: 0x0000D8E4 File Offset: 0x0000BAE4
		public static int SetArrowType(int data, RepeatArrowBlock.ArrowType? arrowType)
		{
			int num = (int)((arrowType != null) ? (arrowType.Value + 1) : RepeatArrowBlock.ArrowType.CopperArrow);
			return (data & -241) | (num & 15) << 4;
		}

		// Token: 0x06000377 RID: 887 RVA: 0x0000D915 File Offset: 0x0000BB15
		public static int GetDraw(int data)
		{
			return data & 15;
		}

		// Token: 0x06000378 RID: 888 RVA: 0x0000D91B File Offset: 0x0000BB1B
		public static int SetDraw(int data, int draw)
		{
			return (data & -16) | (draw & 15);
		}

		// Token: 0x06000379 RID: 889 RVA: 0x0000D926 File Offset: 0x0000BB26
		public static int GetLoadCount(int value)
		{
			return (value & 15360) >> 10;
		}

		// Token: 0x0600037A RID: 890 RVA: 0x0000D932 File Offset: 0x0000BB32
		public static int SetLoadCount(int value, int count)
		{
			return value ^ ((value ^ count << 10) & 15360);
		}

		// Token: 0x0400011B RID: 283
		public static int Index = 805;

		// Token: 0x0400011C RID: 284
		public BlockMesh[] m_standaloneBlockMeshes = new BlockMesh[16];

		// Token: 0x0400011D RID: 285
		private Block arrowBlock;
	}
}
