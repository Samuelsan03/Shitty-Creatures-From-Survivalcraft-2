using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class WaterBowlBlock : BowlBlock
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Bowl");
			// Cuenco
			ModelMesh bowlMesh = model.FindMesh("Bowl", true);
			Matrix bowlBoneTransform = BlockMesh.GetBoneAbsoluteTransform(bowlMesh.ParentBone);
			Matrix bowlTransform = bowlBoneTransform * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f);
			m_standaloneBlockMesh.AppendModelMeshPart(bowlMesh.MeshParts[0], bowlTransform, false, false, false, false, Color.White);

			// Contenido (agua)
			ModelMesh contentMesh = model.FindMesh("Content", true);
			Matrix contentBoneTransform = BlockMesh.GetBoneAbsoluteTransform(contentMesh.ParentBone);
			Matrix contentTransform = contentBoneTransform * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f);
			// Color azul translúcido para simular agua
			Color waterColor = new Color(32, 80, 224, 200);
			m_standaloneBlockMesh.AppendModelMeshPart(contentMesh.MeshParts[0], contentTransform, false, false, false, false, waterColor);

			// Opcional: ajustar coordenadas UV si la textura del agua necesita un recorte especial (como en WaterBucketBlock)
			// m_standaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.8125f, 0.6875f, 0f), -1);

			PriorityUse = 1500;
			base.Initialize();
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		public static int Index = 423; // Índice libre
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
