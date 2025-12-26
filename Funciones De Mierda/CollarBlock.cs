using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class CollarBlock : Block
	{
		// Define el nuevo color como un campo estático o constante
		private static readonly Color NewCollarColor = new Color(0, 195, 0);

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Collar");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Collar", true).ParentBone);

			// Cambia Color.White por el nuevo color
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Collar", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.2f, 0f), false, false, false, false, NewCollarColor);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Collar", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.2f, 0f), false, true, false, false, NewCollarColor);

			base.Initialize();
		}

		// Método para obtener el nombre mostrado - usa LanguageControl
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("CollarBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Zombie Collar"; // Valor por defecto en inglés
		}

		// Método para obtener la descripción
		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("CollarBlock:0", "Description", out description))
			{
				return description;
			}
			// Valor por defecto en inglés
			return "A special collar that can be crafted and is essential for taming hostile zombies. Once placed on a zombie, it pacifies the creature and allows it to be domesticated as a loyal companion. The collar also provides protection against other zombie attacks and can be enchanted for additional effects. Extremely useful for players looking to build an undead army or secure their base with zombie guardians.";
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Usa el nuevo color en lugar del color que viene como parámetro
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, NewCollarColor, 2f * size, ref matrix, environmentData);
		}

		public const int Index = 437;
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}