using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200012C RID: 300
	public class TeaAntifluBowlBlock : Block
	{
		// Token: 0x06000B84 RID: 2948 RVA: 0x00088FF0 File Offset: 0x000871F0
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
			Color color = new Color(149, 255, 154, 200);
			this.m_standaloneBlockMesh.AppendModelMeshPart(modelMesh2.MeshParts[0], matrix2, false, false, false, false, color);
			this.PriorityUse = 1500;
			base.Initialize();
		}

		// Token: 0x06000B85 RID: 2949 RVA: 0x00089116 File Offset: 0x00087316
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			throw new NotImplementedException();
		}

		// Token: 0x06000B86 RID: 2950 RVA: 0x00089120 File Offset: 0x00087320
		public override float GetNutritionalValue(int value)
		{
			return 0.001f;
		}

		// Token: 0x06000B87 RID: 2951 RVA: 0x00089137 File Offset: 0x00087337
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x0400096E RID: 2414
		public static int Index = 425;

		// Token: 0x0400096F RID: 2415
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
