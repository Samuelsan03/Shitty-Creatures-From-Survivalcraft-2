using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class FireBulletBlock : FlatBlock
	{
		public const int Index = 391; // Índice diferente para diferenciarlo

		// Tamaño fijo para la bala de fuego
		private const float Scale = 1f;

		// Usar textura de bala normal (229 = MusketBall)
		private const int TextureSlot = 229;

		// Color de fuego (similar a BigStoneFlameChunkBlock)
		private static readonly Color FireColor = new Color(255, 100, 0, 255);

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// No generar geometría para terreno
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Aplicar escala fija
			float scaledSize = size * Scale;

			// Usar color de fuego
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, scaledSize, ref matrix, null, FireColor, false, environmentData);
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("FireBulletBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Fire Bullet";
		}

		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("FireBulletBlock:0", "Description", out description))
			{
				return description;
			}
			return "A bullet that ignites targets on impact, creating fire that lasts for 60 seconds.";
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return TextureSlot;
		}

		public override float GetProjectilePower(int value)
		{
			return 80f; // Misma potencia que MusketBall
		}

		public override float GetExplosionPressure(int value)
		{
			return 0f;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			// Ocultar en modo creativo
			yield break;
		}

		// CORREGIDO: IsIncendiary no es un método overrideable en FlatBlock
		// En lugar de eso, verificaremos en el comportamiento del bloque
	}
}