using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BlueberryBushBlock : ShittyTexturesFlat
	{
		public const int Index = 432;

		public BlueberryBushBlock()
			: base("Textures/alimentos/arbusto de arandanos")
		{
		}

		public override bool IsFaceNonAttachable(SubsystemTerrain subsystemTerrain, int face, int value, int attachBlockValue)
		{
			// Solo se adhiere a la cara superior (índice 4) si el bloque inferior es un soporte válido para plantas
			if (face == 4)
			{
				int contents = Terrain.ExtractContents(attachBlockValue);
				Block block = BlocksManager.Blocks[contents];
				// Si el bloque inferior es adecuado para plantas, nos adherimos (devolvemos false)
				if (block.IsSuitableForPlants(value, Terrain.MakeBlockValue(contents, 0, Terrain.ExtractData(attachBlockValue))))
					return false;
			}
			// Cualquier otra cara o soporte no válido → no adherido
			return true;
		}

		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			// Obtenemos la colocación por defecto (celda apuntada y cara)
			BlockPlacementData placementData = base.GetPlacementValue(subsystemTerrain, componentMiner, value, raycastResult);

			// Calculamos la posición REAL donde se pondría el arbusto
			Point3 targetCell = placementData.CellFace.Point + CellFace.FaceToPoint3(placementData.CellFace.Face);

			// Celda inferior a la posición de colocación
			int belowY = targetCell.Y - 1;
			int belowValue = subsystemTerrain.Terrain.GetCellValue(targetCell.X, belowY, targetCell.Z);
			Block belowBlock = BlocksManager.Blocks[Terrain.ExtractContents(belowValue)];

			// Si el bloque de abajo no es adecuado para plantas, cancelar colocación
			if (!belowBlock.IsSuitableForPlants(belowValue, value))
			{
				placementData.Value = 0; // No se coloca nada
			}

			return placementData;
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Sin color adicional (arbusto de arándanos no necesita mapa de color)
			base.DrawBlock(primitivesRenderer, value, Color.White, size, ref matrix, environmentData);
		}
	}
}