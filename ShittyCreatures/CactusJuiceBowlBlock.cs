using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000020 RID: 32
	public class CactusJuiceBowlBlock : Block
	{
		// Token: 0x0600009F RID: 159 RVA: 0x00004F7F File Offset: 0x0000317F
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			throw new NotImplementedException();
		}

		// Token: 0x060000A0 RID: 160 RVA: 0x00004F88 File Offset: 0x00003188
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
			Color color = new Color(0, 255, 0, 200);
			this.m_standaloneBlockMesh.AppendModelMeshPart(modelMesh2.MeshParts[0], matrix2, false, false, false, false, color);
			this.PriorityUse = 1500;
			base.Initialize();
		}

		// Token: 0x060000A1 RID: 161 RVA: 0x000050A8 File Offset: 0x000032A8
		public override float GetNutritionalValue(int value)
		{
			return 0.001f;
		}

		// Token: 0x060000A2 RID: 162 RVA: 0x000050BF File Offset: 0x000032BF
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x04000053 RID: 83
		public static int Index = 424;

		// Token: 0x04000054 RID: 84
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
