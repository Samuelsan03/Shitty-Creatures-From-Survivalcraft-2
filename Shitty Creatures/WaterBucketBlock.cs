using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200011E RID: 286
	public class WaterBucketBlock : BucketBlock
	{
		// Token: 0x06000727 RID: 1831 RVA: 0x00022774 File Offset: 0x00020974
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/FullBucket");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Bucket", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Contents", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Contents", true).MeshParts[0], boneAbsoluteTransform2 * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, new Color(32, 80, 224, 255));
			this.m_standaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.8125f, 0.6875f, 0f), -1);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Bucket", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, Color.White);
			this.PriorityUse = 1500;
			base.Initialize();
		}

		// Token: 0x06000728 RID: 1832 RVA: 0x000228B1 File Offset: 0x00020AB1
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x040002F2 RID: 754
		public static int Index = 91;

		public override float GetNutritionalValue(int value)
		{
			return 0.001f;   // Valor mínimo para activar el arrastre
		}

		// Token: 0x040002F3 RID: 755
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
