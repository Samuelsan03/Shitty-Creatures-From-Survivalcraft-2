using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class EmptyBowlBlock : BowlBlock
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Bowl");
			// Buscar el nodo "Bowl" (cuenco)
			ModelMesh bowlMesh = model.FindMesh("Bowl", true);
			Matrix boneTransform = BlockMesh.GetBoneAbsoluteTransform(bowlMesh.ParentBone);
			// Transformación: rotación 180° en Y para orientarlo correctamente, y traslación para centrarlo
			Matrix transform = boneTransform * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) * Matrix.CreateTranslation(0f, -0.3f, 0f);
			m_standaloneBlockMesh.AppendModelMeshPart(bowlMesh.MeshParts[0], transform, false, false, false, false, Color.White);
			PriorityUse = 1500;
			base.Initialize();
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		public static int Index = 422; // Índice libre (puede ajustarse)
		private BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
