using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x0200001A RID: 26
	public class AKBlock : Block
	{
		// Token: 0x06000090 RID: 144 RVA: 0x000077C4 File Offset: 0x000059C4
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Armas De Porqueria/AK47", null);
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Gun", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Gun", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, true, false, Color.White);
			base.Initialize();
		}

		// Token: 0x06000091 RID: 145 RVA: 0x00007848 File Offset: 0x00005A48
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateShadedMeshVertices(this, x, y, z, this.m_standaloneBlockMesh, Color.White, null, null, geometry.SubsetOpaque);
		}

		// Token: 0x06000092 RID: 146 RVA: 0x0000787F File Offset: 0x00005A7F
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, this.texture, color, 2.6f * size, ref matrix, environmentData);
		}

		// Token: 0x06000093 RID: 147 RVA: 0x000078A4 File Offset: 0x00005AA4
		public static int GetBulletNum(int data)
		{
			return data >> 4 & 63;
		}

		// Token: 0x06000094 RID: 148 RVA: 0x000078BC File Offset: 0x00005ABC
		public static int SetBulletNum(int data)
		{
			return (data & -64) | (data & 63) << 4;
		}

		// Token: 0x04000074 RID: 116
		public const int Index = 338;

		// Token: 0x04000075 RID: 117
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Token: 0x04000076 RID: 118
		public Texture2D texture = ContentManager.Get<Texture2D>("Textures/Armas De Mierda/AK47", null);
	}
}
