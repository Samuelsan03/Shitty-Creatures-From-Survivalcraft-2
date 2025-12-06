using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class RepeatCrossbowBlock : Block
	{
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

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

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

		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 8 & 255;
		}

		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= -65281;
			num |= Math.Clamp(damage, 0, 255) << 8;
			return Terrain.ReplaceData(value, num);
		}

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

		public static RepeatArrowBlock.ArrowType? GetArrowType(int data)
		{
			int num = data >> 4 & 15;
			if (num != 0)
			{
				return new RepeatArrowBlock.ArrowType?((RepeatArrowBlock.ArrowType)(num - 1));
			}
			return null;
		}

		public static int SetArrowType(int data, RepeatArrowBlock.ArrowType? arrowType)
		{
			int num = (int)((arrowType != null) ? (arrowType.Value + 1) : RepeatArrowBlock.ArrowType.CopperArrow);
			return (data & -241) | (num & 15) << 4;
		}

		public static int GetDraw(int data)
		{
			return data & 15;
		}

		public static int SetDraw(int data, int draw)
		{
			return (data & -16) | (draw & 15);
		}

		public static int GetLoadCount(int value)
		{
			return (Terrain.ExtractData(value) & 15360) >> 10; // CORREGIDO: usar Terrain.ExtractData
		}

		public static int SetLoadCount(int value, int count)
		{
			int data = Terrain.ExtractData(value);
			data &= -15361;
			data |= (Math.Clamp(count, 0, 8) & 15) << 10; // LIMITAR a 8
			return Terrain.ReplaceData(value, data);
		}

		public static int Index = 805;
		public BlockMesh[] m_standaloneBlockMeshes = new BlockMesh[16];
		private Block arrowBlock;
	}
}
