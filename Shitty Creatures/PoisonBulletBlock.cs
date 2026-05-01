using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class PoisonBulletBlock : FlatBlock
	{
		public const int Index = 207;

		// Tamaño fijo para la bala venenosa (similar a MusketBall)
		private const float Scale = 1f;

		// Usar una textura existente (229 = MusketBall)
		private const int TextureSlot = 229;

		// Color venenoso (verde)
		private static readonly Color PoisonColor = new Color(100, 200, 100, 255);

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Aplicar escala fija
			float scaledSize = size * Scale;

			// Usar directamente PoisonColor ignorando el color pasado como parámetro
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, scaledSize, ref matrix, null, PoisonColor, false, environmentData);
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("PoisonBulletBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Poison Bullet";
		}

		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("PoisonBulletBlock:0", "Description", out description))
			{
				return description;
			}
			return "Similar to a musket round, but it cannot be used as musket ammunition and is not available for crafting.";
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			// Usar textura existente en lugar de 325
			return TextureSlot;
		}

		public override float GetProjectilePower(int value)
		{
			// Potencia de proyectil (ajusta según necesites)
			return 80f; // Mismo que MusketBall
		}

		public override float GetExplosionPressure(int value)
		{
			return 0f; // Sin presión de explosión
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			// Devuelve lista vacía para ocultar en creativo
			yield break;
		}
	}
}
