using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class TridentBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Trident");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Trident", true).ParentBone);
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Trident", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			base.Initialize();
		}

		// Método para obtener el nombre mostrado - usa LanguageControl
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("TridentBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Trident"; // Valor por defecto en inglés
		}

		// Método para obtener la categoría
		public override string GetCategory(int value)
		{
			return "Weapons"; // O la categoría apropiada
		}

		// Método para obtener la descripción
		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("TridentBlock:0", "Description", out description))
			{
				return description;
			}
			return "A legendary three-pronged spear historically associated with sea deities like Poseidon in Greek mythology, symbolizing dominion over the oceans. In ancient times, it was both a divine symbol and a formidable weapon used by warriors and sea gods alike. Forged from rare materials found in the deepest ocean trenches, this Trident channels the power of the tides. While it can be crafted using extremely expensive resources including iron blocks and enchanted marine crystals, the more direct method to obtain it is by defeating Hombre Agua in battle. Wield it to command respect on both land and sea.";
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
		}

		public static int Index = 321;
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
	}
}
