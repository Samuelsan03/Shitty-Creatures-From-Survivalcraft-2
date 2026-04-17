using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200000C RID: 12
	public class AntidoteBowlBlock : Block
	{
		// Token: 0x0600003B RID: 59 RVA: 0x00003C6C File Offset: 0x00001E6C
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
			Color color = new Color(153, 153, 255, 200);
			this.m_standaloneBlockMesh.AppendModelMeshPart(modelMesh2.MeshParts[0], matrix2, false, false, false, false, color);
			this.PriorityUse = 1500;
			base.Initialize();
		}

		// Token: 0x0600003C RID: 60 RVA: 0x00003D94 File Offset: 0x00001F94
		public override float GetNutritionalValue(int value)
		{
			return 0.001f;
		}

		// Token: 0x0600003D RID: 61 RVA: 0x00003DAB File Offset: 0x00001FAB
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00003DC8 File Offset: 0x00001FC8
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			throw new NotImplementedException();
		}

		// Token: 0x0400002A RID: 42
		public static int Index = 425;

		// Token: 0x0400002B RID: 43
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
