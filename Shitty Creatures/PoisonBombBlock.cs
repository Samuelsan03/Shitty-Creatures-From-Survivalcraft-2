using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x020000BF RID: 191
	public class PoisonBombBlock : Block
	{
		// Token: 0x06000748 RID: 1864 RVA: 0x00052A90 File Offset: 0x00050C90
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Bomb");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Bomb", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Bomb", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f), false, false, false, false, new Color(0f, 255f, 0f));
			base.Initialize();
		}

		// Token: 0x06000749 RID: 1865 RVA: 0x00052B1F File Offset: 0x00050D1F
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		// Token: 0x0600074A RID: 1866 RVA: 0x00052B22 File Offset: 0x00050D22
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		// Token: 0x040006A4 RID: 1700
		public static int Index = 328;

		// Token: 0x040006A5 RID: 1701
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
