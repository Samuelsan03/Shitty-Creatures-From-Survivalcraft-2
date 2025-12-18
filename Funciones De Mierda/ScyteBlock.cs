using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class ScyteBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Scyte");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Scyte", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Scyte", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			base.Initialize();
		}

		// Método para obtener el nombre mostrado - usa LanguageControl
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("ScyteBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Scythe"; // Valor por defecto en inglés
		}

		// Método para obtener la categoría (opcional, pero puede ser necesario)
		public override string GetCategory(int value)
		{
			return "Weapons"; // O la categoría apropiada
		}

		// Método para obtener la descripción
		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("ScyteBlock:0", "Description", out description))
			{
				return description;
			}
			// Valor por defecto en inglés
			return "The Scythe is a long, curved-bladed weapon originally designed as a farming tool for cutting crops, but in dark fantasy it is seen as a weapon of death, often wielded by demons, skeletons, and reapers. In this world, the Scythe cannot be crafted because it has no recipe and is extremely difficult to obtain. The only way to acquire the Scythe is by killing El Señor De Las Tumbas Moradas. Be careful: its lethality is extreme, capable of killing you with a single strike.";
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		public static int Index = 651;
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
