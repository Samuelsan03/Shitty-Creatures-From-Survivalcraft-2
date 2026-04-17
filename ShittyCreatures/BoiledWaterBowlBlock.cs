using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200001E RID: 30
	public class BoiledWaterBowlBlock : Block
	{
		// Token: 0x06000094 RID: 148 RVA: 0x00004C61 File Offset: 0x00002E61
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			throw new NotImplementedException();
		}

		// Token: 0x06000095 RID: 149 RVA: 0x00004C6C File Offset: 0x00002E6C
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Bowl");
			ModelMesh modelMesh = model.FindMesh("Bowl", true);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(modelMesh.ParentBone);
			Matrix matrix = boneAbsoluteTransform * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f);
			this.m_standaloneBlockMesh.AppendModelMeshPart(modelMesh.MeshParts[0], matrix, false, false, false, false, Color.White);
			ModelMesh modelMesh2 = model.FindMesh("Content", true);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(modelMesh2.ParentBone);
			Matrix matrix2 = boneAbsoluteTransform2 * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f);
			Color color = new Color(104, 155, 255, 200);
			this.m_standaloneBlockMesh.AppendModelMeshPart(modelMesh2.MeshParts[0], matrix2, false, false, false, false, color);
			this.PriorityUse = 1500;
			base.Initialize();
		}

		// Token: 0x06000096 RID: 150 RVA: 0x00004D90 File Offset: 0x00002F90
		public override float GetNutritionalValue(int value)
		{
			return 0.001f;
		}

		// Token: 0x06000097 RID: 151 RVA: 0x00004DA7 File Offset: 0x00002FA7
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x0400004F RID: 79
		public static int Index = 423;

		// Token: 0x04000050 RID: 80
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
