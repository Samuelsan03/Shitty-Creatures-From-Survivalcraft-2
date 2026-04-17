using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000119 RID: 281
	public class TeaAntifluBucketBlock : BucketBlock
	{
		// Token: 0x0600059E RID: 1438 RVA: 0x000218D8 File Offset: 0x0001FAD8
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/FullBucket", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Bucket", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Contents", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Contents", true).MeshParts[0], boneAbsoluteTransform2 * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, new Color(149, 255, 154, 255));
			this.m_standaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.8125f, 0.6875f, 0f), -1);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Bucket", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, Color.White);
			base.Initialize();
		}

		// Token: 0x0600059F RID: 1439 RVA: 0x00021A0B File Offset: 0x0001FC0B
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x0400028B RID: 651
		public const int Index = 388;

		// Token: 0x0400028C RID: 652
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
